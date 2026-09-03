<script setup lang="ts">
import { computed, onMounted, ref } from 'vue'
import { useRouter } from 'vue-router'
import { showFailToast, showImagePreview, showToast } from 'vant'
import { reviewApi } from '@/modules/09-review/api/review.api'
import type { ReviewDto } from '@/modules/09-review/types/review.dto'
import ErrorState from '@/shared/components/ErrorState.vue'
import EmptyState from '@/shared/components/EmptyState.vue'
import { formatDate, formatDateTime } from '@/shared/utils/format'
import { logger } from '@/shared/utils/logger'

/**
 * 我的评价页（/reviews/mine）
 *
 * 结构（对齐设计稿 my-reviews）：
 * NavBar（返回 / 我的评价）→ 滚动主体（我的评价卡片列表，van-pull-refresh 下拉刷新）
 *
 * 评价卡片：头像 + 昵称 + 星级 + 评价时间 → SKU 规格 → 评价内容 → 图片缩略图（点击全屏预览）
 * → 追评块（已追评时展示）→ 商家回复块 → 底部操作（追评 / 查看商品）
 *
 * 交互流：
 * - GET /reviews/mine 拉取我的全部评价；
 * - 未追评的评价显示「追评」按钮 → 底部弹层输入追评内容（5-500 字）
 *   → POST /reviews/{reviewId}/append → 成功 toast「追评成功」并以响应回填卡片；
 * - 「查看商品」→ /product/:spuId；图片缩略图点击 showImagePreview 全屏预览。
 */

const router = useRouter()

/** 追评内容字数限制（与评价内容契约一致） */
const APPEND_MIN = 5
const APPEND_MAX = 500

// ---- 列表状态 ----
const loading = ref(true)
const loadError = ref(false)
const reviews = ref<ReviewDto[]>([])
const refreshing = ref(false)

// ---- 追评弹层状态 ----
const appendVisible = ref(false)
const appendTarget = ref<ReviewDto | null>(null)
const appendContent = ref('')
const appendSubmitting = ref(false)

/** 头像底色（按昵称散列取色，保证同一昵称稳定） */
const AVATAR_COLORS = ['#1677FF', '#52C41A', '#FAAD14', '#FF4D4F', '#722ED1']

/** 评价数量（NavBar 标题角标） */
const reviewCount = computed(() => reviews.value.length)

onMounted(() => {
  void loadReviews()
})

/** 加载我的评价列表 */
async function loadReviews(): Promise<void> {
  loading.value = true
  loadError.value = false
  try {
    reviews.value = await reviewApi.listMine()
  } catch (e) {
    logger.error('我的评价加载失败', e)
    loadError.value = true
  } finally {
    loading.value = false
  }
}

/** 下拉刷新 */
async function onRefresh(): Promise<void> {
  try {
    reviews.value = await reviewApi.listMine()
  } catch (e) {
    logger.warn('我的评价刷新失败', e)
    showFailToast('刷新失败')
  } finally {
    refreshing.value = false
  }
}

/** 头像底色 */
function avatarColor(nickname: string): string {
  let hash = 0
  for (let i = 0; i < nickname.length; i++) {
    hash = (hash * 31 + nickname.charCodeAt(i)) >>> 0
  }
  return AVATAR_COLORS[hash % AVATAR_COLORS.length]
}

/** 图片预览 */
function previewImages(review: ReviewDto, index: number): void {
  if (review.images.length === 0) return
  showImagePreview({ images: review.images, startPosition: index })
}

// ---- 追评 ----
function openAppend(review: ReviewDto): void {
  if (review.appendContent) return
  appendTarget.value = review
  appendContent.value = ''
  appendVisible.value = true
}

async function submitAppend(): Promise<void> {
  const target = appendTarget.value
  if (!target || appendSubmitting.value) return
  const text = appendContent.value.trim()
  if (text.length < APPEND_MIN) {
    showToast(`追评内容至少 ${APPEND_MIN} 个字`)
    return
  }
  if (text.length > APPEND_MAX) {
    showToast(`追评内容最多 ${APPEND_MAX} 个字`)
    return
  }
  appendSubmitting.value = true
  try {
    const updated = await reviewApi.append(target.id, { content: text })
    const index = reviews.value.findIndex((r) => r.id === target.id)
    if (index >= 0) {
      reviews.value.splice(index, 1, updated)
    }
    appendVisible.value = false
    appendContent.value = ''
    appendTarget.value = null
    showToast('追评成功')
  } catch (e) {
    logger.warn('追评提交失败', e)
    showFailToast(e instanceof Error ? e.message : '追评失败，请稍后重试')
  } finally {
    appendSubmitting.value = false
  }
}

// ---- 跳转 ----
function goBack(): void {
  if (window.history.length > 1) {
    router.back()
  } else {
    router.replace('/')
  }
}

function goProduct(review: ReviewDto): void {
  router.push(`/product/${review.spuId}`)
}

function goHome(): void {
  router.replace('/')
}
</script>

<template>
  <div class="my-reviews-page">
    <!-- NavBar -->
    <header class="navbar">
      <button class="nav-back" type="button" aria-label="返回" @click="goBack">
        <svg width="20" height="20" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round">
          <path d="M15 18l-6-6 6-6" />
        </svg>
      </button>
      <div class="nav-title">我的评价</div>
    </header>

    <!-- 滚动主体 -->
    <main class="body">
      <!-- 骨架屏 -->
      <div v-if="loading" class="review-list" aria-label="加载中">
        <div v-for="i in 3" :key="i" class="review-card is-skeleton">
          <div class="skeleton-block sk-head" />
          <div class="skeleton-block sk-line" />
          <div class="skeleton-block sk-line short" />
          <div class="skeleton-block sk-block" />
        </div>
      </div>

      <!-- 错误态 -->
      <ErrorState
        v-else-if="loadError"
        title="评价加载失败"
        description="网络异常，请检查网络连接后重试"
        @retry="loadReviews"
      />

      <!-- 空态 -->
      <EmptyState
        v-else-if="reviews.length === 0"
        title="暂无评价"
        action-text="去逛逛"
        @action="goHome"
      />

      <!-- 评价列表 -->
      <van-pull-refresh v-else v-model="refreshing" success-text="刷新成功" @refresh="onRefresh">
        <div class="list-meta">共 {{ reviewCount }} 条评价</div>
        <div class="review-list">
          <article v-for="review in reviews" :key="review.id" class="review-card" role="article">
            <!-- 头部：头像 + 昵称 + 星级 + 时间 -->
            <div class="review-card__head">
              <img v-if="review.avatar" class="review-card__avatar" :src="review.avatar" :alt="review.nickname">
              <div
                v-else
                class="review-card__avatar avatar-text"
                :style="{ background: avatarColor(review.nickname) }"
                aria-hidden="true"
              >
                {{ review.nickname.charAt(0) }}
              </div>
              <div class="review-card__user">
                <div class="review-card__name">{{ review.nickname }}</div>
                <div class="review-card__meta">
                  <span class="review-card__stars" :aria-label="`${review.rating} 星`">
                    <svg
                      v-for="s in 5"
                      :key="s"
                      class="star"
                      :class="{ 'star-empty': s > review.rating }"
                      width="14"
                      height="14"
                      viewBox="0 0 24 24"
                      fill="currentColor"
                    >
                      <path d="M12 2l2.9 6.3 6.9.6-5.2 4.6 1.6 6.8L12 17.3 5.8 20.9l1.6-6.8L2.2 8.9l6.9-.6z" />
                    </svg>
                  </span>
                  <span class="review-card__date">{{ formatDateTime(review.createdAt) }}</span>
                </div>
              </div>
            </div>

            <!-- SKU 规格 -->
            <div v-if="review.skuSpecs" class="review-card__sku">
              <span class="sku-tag">{{ review.skuSpecs }}</span>
            </div>

            <!-- 评价内容 -->
            <div class="review-card__content">{{ review.content }}</div>

            <!-- 评价图片 -->
            <div v-if="review.images.length > 0" class="review-card__images">
              <img
                v-for="(img, index) in review.images"
                :key="index"
                class="review-card__img"
                :src="img"
                :alt="`评价图 ${index + 1}`"
                loading="lazy"
                @click="previewImages(review, index)"
              >
            </div>

            <!-- 追评块 -->
            <div v-if="review.appendContent" class="review-card__append">
              <div class="append-label">已追评</div>
              <div class="append-text">{{ review.appendContent }}</div>
              <div v-if="review.appendAt" class="append-time">{{ formatDate(review.appendAt) }}</div>
            </div>

            <!-- 商家回复块 -->
            <div v-if="review.reply" class="review-card__reply">
              <div class="reply-label">商家回复</div>
              <div class="reply-text">{{ review.reply.content }}</div>
              <div class="reply-time">{{ formatDateTime(review.reply.repliedAt) }}</div>
            </div>

            <!-- 底部操作 -->
            <div class="review-card__footer">
              <div class="review-card__time">{{ formatDateTime(review.createdAt) }}</div>
              <div class="review-card__actions">
                <button
                  v-if="!review.appendContent"
                  class="btn-sm btn-sm--outline"
                  type="button"
                  aria-label="追评"
                  @click="openAppend(review)"
                >
                  追评
                </button>
                <button class="btn-sm btn-sm--default" type="button" aria-label="查看商品" @click="goProduct(review)">
                  查看商品
                </button>
              </div>
            </div>
          </article>
        </div>
      </van-pull-refresh>
    </main>

    <!-- 追评弹层 -->
    <van-popup
      v-model:show="appendVisible"
      position="bottom"
      round
      role="dialog"
      aria-label="追评弹层"
    >
      <div class="append-panel">
        <div class="append-head">
          <span class="t">追加评价</span>
          <button class="close" type="button" @click="appendVisible = false">关闭</button>
        </div>
        <div class="append-body">
          <textarea
            v-model="appendContent"
            class="append-textarea"
            :maxlength="APPEND_MAX"
            placeholder="补充使用后的感受，帮助更多买家做决策～"
            aria-label="追评内容"
          />
          <div class="append-counter">
            <span class="current">{{ appendContent.length }}</span>/<span class="max">{{ APPEND_MAX }}</span>
          </div>
          <button
            class="append-submit"
            :class="{ loading: appendSubmitting }"
            type="button"
            :disabled="appendSubmitting"
            aria-label="提交追评"
            @click="submitAppend"
          >
            {{ appendSubmitting ? '提交中...' : '提交追评' }}
          </button>
        </div>
      </div>
    </van-popup>
  </div>
</template>

<style scoped>
.my-reviews-page {
  height: 100%;
  display: flex;
  flex-direction: column;
  background: var(--n2);
}

/* NavBar */
.navbar {
  height: 46px;
  background: var(--n1);
  border-bottom: 1px solid var(--n3);
  display: flex;
  align-items: center;
  padding: 0 var(--s3);
  flex-shrink: 0;
  position: relative;
}

.nav-back {
  display: flex;
  align-items: center;
  color: var(--n10);
  background: none;
  border: none;
  padding: 0;
  cursor: pointer;
}

.nav-title {
  position: absolute;
  left: 50%;
  transform: translateX(-50%);
  font-size: var(--fs-lg);
  font-weight: var(--fw-medium);
  color: var(--n10);
}

/* 滚动主体 */
.body {
  flex: 1;
  overflow-y: auto;
  padding: var(--s3);
  padding-bottom: calc(var(--s8) + env(safe-area-inset-bottom));
}

.list-meta {
  font-size: var(--fs-sm);
  color: var(--n7);
  padding: 0 var(--s1) var(--s2);
}

/* 评价列表 */
.review-list {
  display: flex;
  flex-direction: column;
  gap: var(--s3);
}

/* 评价卡片 */
.review-card {
  background: var(--n1);
  border-radius: var(--r-lg);
  padding: var(--s3);
  box-shadow: var(--sh-card);
}

.review-card__head {
  display: flex;
  align-items: center;
  gap: var(--s2);
  margin-bottom: var(--s2);
}

.review-card__avatar {
  width: 36px;
  height: 36px;
  border-radius: 50%;
  flex-shrink: 0;
  object-fit: cover;
}

.avatar-text {
  display: flex;
  align-items: center;
  justify-content: center;
  color: #fff;
  font-size: var(--fs-base);
  font-weight: var(--fw-medium);
}

.review-card__user {
  flex: 1;
  min-width: 0;
}

.review-card__name {
  font-size: var(--fs-base);
  color: var(--n10);
  font-weight: var(--fw-medium);
}

.review-card__meta {
  display: flex;
  align-items: center;
  gap: var(--s2);
  margin-top: 2px;
}

.review-card__stars {
  display: flex;
  gap: 1px;
}

.review-card__stars .star {
  color: var(--c-warning);
}

.review-card__stars .star-empty {
  color: var(--n5);
}

.review-card__date {
  font-size: var(--fs-sm);
  color: var(--n7);
}

/* SKU 规格 */
.review-card__sku {
  margin-bottom: var(--s2);
}

.sku-tag {
  display: inline-block;
  background: var(--n2);
  border-radius: var(--r-base);
  padding: 2px var(--s2);
  font-size: var(--fs-sm);
  color: var(--n9);
}

/* 评价内容 */
.review-card__content {
  font-size: var(--fs-base);
  color: var(--n9);
  line-height: 1.6;
  margin-bottom: var(--s2);
}

/* 评价图片 */
.review-card__images {
  display: flex;
  flex-wrap: wrap;
  gap: var(--s2);
  margin-bottom: var(--s2);
}

.review-card__img {
  width: 72px;
  height: 72px;
  border-radius: var(--r-card);
  object-fit: cover;
  cursor: pointer;
  background: var(--n3);
}

/* 追评块 */
.review-card__append {
  margin-top: var(--s2);
  padding: var(--s2) var(--s3);
  background: #fffbe6;
  border-radius: var(--r-card);
  border-left: 3px solid var(--c-warning);
}

.append-label {
  font-size: var(--fs-sm);
  color: var(--c-warning);
  font-weight: var(--fw-medium);
  margin-bottom: 4px;
}

.append-text {
  font-size: 13px;
  color: var(--n9);
  line-height: 1.5;
}

.append-time {
  font-size: var(--fs-sm);
  color: var(--n7);
  margin-top: 4px;
}

/* 商家回复块 */
.review-card__reply {
  background: var(--n2);
  border-radius: var(--r-card);
  padding: var(--s2) var(--s3);
  margin-top: var(--s2);
}

.reply-label {
  font-size: var(--fs-sm);
  color: var(--c-primary);
  font-weight: var(--fw-medium);
  margin-bottom: 4px;
}

.reply-text {
  font-size: 13px;
  color: var(--n9);
  line-height: 1.5;
}

.reply-time {
  font-size: var(--fs-sm);
  color: var(--n7);
  margin-top: 4px;
}

/* 底部操作 */
.review-card__footer {
  display: flex;
  align-items: center;
  justify-content: space-between;
  margin-top: var(--s2);
  padding-top: var(--s2);
  border-top: 1px solid var(--n3);
}

.review-card__time {
  font-size: var(--fs-sm);
  color: var(--n7);
}

.review-card__actions {
  display: flex;
  gap: var(--s2);
}

.btn-sm {
  display: inline-flex;
  align-items: center;
  justify-content: center;
  padding: 5px 14px;
  border-radius: var(--r-base);
  font-size: var(--fs-sm);
  font-weight: var(--fw-medium);
  cursor: pointer;
  border: none;
  font-family: inherit;
  transition: opacity var(--d-mid);
}

.btn-sm:active {
  opacity: 0.85;
}

.btn-sm--outline {
  background: var(--n1);
  color: var(--c-primary);
  border: 1px solid var(--c-primary);
}

.btn-sm--default {
  background: var(--n2);
  color: var(--n9);
}

/* 追评弹层 */
.append-panel {
  padding: var(--s4) var(--s4) calc(var(--s4) + env(safe-area-inset-bottom));
  display: flex;
  flex-direction: column;
}

.append-head {
  display: flex;
  align-items: center;
  justify-content: space-between;
  margin-bottom: var(--s3);
}

.append-head .t {
  font-size: var(--fs-lg);
  font-weight: var(--fw-semibold);
  color: var(--n10);
}

.append-head .close {
  font-size: var(--fs-sm);
  color: var(--n7);
  cursor: pointer;
  font-family: inherit;
  padding: var(--s1);
}

.append-textarea {
  width: 100%;
  min-height: 120px;
  border: 1px solid var(--n3);
  border-radius: var(--r-card);
  padding: var(--s3);
  font-size: var(--fs-base);
  font-family: inherit;
  color: var(--n10);
  resize: none;
  line-height: 1.6;
  outline: none;
  transition: border-color var(--d-mid);
  background: var(--n1);
}

.append-textarea:focus {
  border-color: var(--c-primary);
}

.append-textarea::placeholder {
  color: var(--n7);
}

.append-counter {
  text-align: right;
  font-size: var(--fs-sm);
  color: var(--n7);
  margin-top: var(--s1);
}

.append-counter .current {
  color: var(--n9);
}

.append-submit {
  margin-top: var(--s3);
  width: 100%;
  height: 44px;
  background: var(--c-primary);
  color: #fff;
  border: none;
  border-radius: var(--r-base);
  font-size: var(--fs-base);
  font-weight: var(--fw-medium);
  font-family: inherit;
  cursor: pointer;
  transition: opacity var(--d-mid);
}

.append-submit:active {
  opacity: 0.85;
}

.append-submit.loading {
  background: var(--n5);
  cursor: not-allowed;
}

/* 骨架屏 */
.review-card.is-skeleton {
  display: flex;
  flex-direction: column;
  gap: var(--s2);
}

.sk-head {
  width: 50%;
  height: 36px;
  border-radius: 18px;
}

.sk-line {
  width: 90%;
  height: 14px;
}

.sk-line.short {
  width: 60%;
}

.sk-block {
  height: 72px;
  border-radius: var(--r-card);
}
</style>
