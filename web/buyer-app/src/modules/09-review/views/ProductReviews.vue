<script setup lang="ts">
import { computed, onMounted, ref } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import { showImagePreview } from 'vant'
import { reviewApi } from '@/modules/09-review/api/review.api'
import type { ProductReviewSummaryDto, ReviewDto } from '@/modules/09-review/types/review.dto'
import ErrorState from '@/shared/components/ErrorState.vue'
import EmptyState from '@/shared/components/EmptyState.vue'
import { formatDate, formatDateTime } from '@/shared/utils/format'
import { logger } from '@/shared/utils/logger'

/**
 * 商品评价列表页（/product/:spuId/reviews，匿名可访问）
 *
 * 结构（对齐设计稿 product-reviews）：
 * NavBar（返回 / 商品评价 N 条）→ 滚动主体：
 * 评分概览卡（均值大字 + 5 星分布柱状条 + 好评率）
 * → 标签筛选（全部 / 有图 / 好评 / 差评，对应 API filter 参数）
 * → van-list 无限滚动评价卡片（头像 + 昵称 + 星级 + 日期 + SKU + 内容
 *   + 图片缩略图全屏预览 + 追评块 + 商家回复块）
 *
 * 交互流：
 * - GET /products/{spuId}/reviews?page=1&filter=all 拉取首页与摘要；
 * - 切换筛选标签重置分页重新拉取；滚动到底 van-list 追加下一页；
 * - 下拉刷新重置列表；图片点击 showImagePreview 全屏预览；
 * - 空态「去购买」CTA 跳回商品详情页。
 */

const route = useRoute()
const router = useRouter()

const pageSize = 20

/** 筛选标签（与 reviewApi.listProductReviews 的 filter 参数一致） */
type ReviewFilter = 'all' | 'withImage' | 'good' | 'bad'

const FILTER_TABS: Array<{ key: ReviewFilter; label: string }> = [
  { key: 'all', label: '全部' },
  { key: 'withImage', label: '有图' },
  { key: 'good', label: '好评' },
  { key: 'bad', label: '差评' },
]

// ---- 页面状态 ----
const activeFilter = ref<ReviewFilter>('all')
const firstLoading = ref(true)
const loadError = ref(false)
const summary = ref<ProductReviewSummaryDto | null>(null)
const reviews = ref<ReviewDto[]>([])
const page = ref(1)
const finished = ref(false)
const listLoading = ref(false)
const listError = ref(false)
const refreshing = ref(false)

/** 列表请求序号（切换筛选时旧响应作废） */
let listSeq = 0

/** 头像底色（按昵称散列取色，保证同一昵称稳定） */
const AVATAR_COLORS = ['#1677FF', '#52C41A', '#FAAD14', '#FF4D4F', '#722ED1']

/** 星级分布（5★ → 1★，缺失星级补 0） */
const distributionRows = computed(() => {
  const s = summary.value
  const map = new Map<number, number>()
  for (const d of s?.distribution ?? []) {
    map.set(d.star, d.count)
  }
  return [5, 4, 3, 2, 1].map((star) => {
    const count = map.get(star) ?? 0
    const base = s && s.count > 0 ? s.count : 1
    return { star, count, pct: Math.round((count / base) * 100) }
  })
})

/** 筛选标签角标数量（有图数后端未提供，不展示角标） */
function filterBadge(filter: ReviewFilter): number | null {
  const s = summary.value
  if (!s) return null
  const countOf = (star: number) => s.distribution.find((d) => d.star === star)?.count ?? 0
  if (filter === 'all') return s.count
  if (filter === 'good') return countOf(5) + countOf(4)
  if (filter === 'bad') return countOf(1) + countOf(2)
  return null
}

onMounted(() => {
  void reload()
})

/** 构建请求参数 */
function buildParams(targetPage: number): { page: number; pageSize: number; filter: ReviewFilter } {
  return { page: targetPage, pageSize, filter: activeFilter.value }
}

/** 重置分页并加载第一页 */
async function reload(): Promise<void> {
  const seq = ++listSeq
  page.value = 1
  finished.value = false
  listError.value = false
  firstLoading.value = true
  try {
    const result = await reviewApi.listProductReviews(getSpuId(), buildParams(1))
    if (seq !== listSeq) return
    summary.value = result.summary
    reviews.value = result.items
    if (result.items.length < pageSize) {
      finished.value = true
    }
  } catch (e) {
    if (seq !== listSeq) return
    logger.error('商品评价加载失败', e)
    loadError.value = true
  } finally {
    if (seq === listSeq) {
      firstLoading.value = false
      refreshing.value = false
    }
  }
}

/** van-list 无限加载 */
async function onLoad(): Promise<void> {
  if (finished.value || firstLoading.value) return
  const seq = listSeq
  listLoading.value = true
  listError.value = false
  try {
    const next = await reviewApi.listProductReviews(getSpuId(), buildParams(page.value + 1))
    if (seq !== listSeq) return
    reviews.value.push(...next.items)
    page.value += 1
    if (next.items.length < pageSize) {
      finished.value = true
    }
  } catch (e) {
    if (seq !== listSeq) return
    logger.warn('商品评价翻页加载失败', e)
    listError.value = true
  } finally {
    if (seq === listSeq) {
      listLoading.value = false
    }
  }
}

/** 下拉刷新 */
async function onRefresh(): Promise<void> {
  await reload()
}

/** 路由参数中的 spuId */
function getSpuId(): string {
  return String(route.params.spuId ?? '')
}

/** 切换筛选标签 */
function setFilter(filter: ReviewFilter): void {
  if (activeFilter.value === filter) return
  activeFilter.value = filter
  void reload()
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

// ---- 跳转 ----
function goBack(): void {
  if (window.history.length > 1) {
    router.back()
  } else {
    router.replace(`/product/${getSpuId()}`)
  }
}

function goProduct(): void {
  router.push(`/product/${getSpuId()}`)
}
</script>

<template>
  <div class="product-reviews-page">
    <!-- NavBar -->
    <header class="navbar">
      <button class="nav-back" type="button" aria-label="返回" @click="goBack">
        <svg width="20" height="20" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round">
          <path d="M15 18l-6-6 6-6" />
        </svg>
      </button>
      <div class="nav-title">
        商品评价<span v-if="summary" class="count">（{{ summary.count }}条）</span>
      </div>
    </header>

    <!-- 滚动主体 -->
    <main class="body">
      <!-- 骨架屏 -->
      <div v-if="firstLoading" aria-label="加载中">
        <div class="skeleton-block sk-overview" />
        <div class="sk-tabs">
          <div v-for="i in 4" :key="i" class="skeleton-block sk-tab-item" />
        </div>
        <div class="sk-card">
          <div class="sk-card-head">
            <div class="skeleton-block sk-avatar" />
            <div class="sk-card-lines">
              <div class="skeleton-block sk-line" />
              <div class="skeleton-block sk-line short" />
            </div>
          </div>
          <div class="skeleton-block sk-block" />
        </div>
      </div>

      <!-- 错误态 -->
      <ErrorState
        v-else-if="loadError && reviews.length === 0"
        title="评价加载失败"
        description="网络异常，请检查网络连接后重试"
        @retry="reload"
      />

      <!-- 空态 -->
      <EmptyState
        v-else-if="summary && summary.count === 0"
        title="暂无评价"
        action-text="去购买"
        @action="goProduct"
      />

      <!-- 内容 -->
      <van-pull-refresh v-else v-model="refreshing" success-text="刷新成功" @refresh="onRefresh">
        <!-- 评分概览卡 -->
        <section v-if="summary" class="rating-overview" aria-label="评分概览">
          <div class="rating-overview__top">
            <div class="rating-overview__score">
              <div class="score-num">{{ summary.averageRating.toFixed(1) }}</div>
              <div class="score-stars" :aria-label="`${Math.round(summary.averageRating)} 星`">
                <svg
                  v-for="s in 5"
                  :key="s"
                  class="star"
                  :class="{ 'star-empty': s > Math.round(summary.averageRating) }"
                  width="14"
                  height="14"
                  viewBox="0 0 24 24"
                  fill="currentColor"
                >
                  <path d="M12 2l2.9 6.3 6.9.6-5.2 4.6 1.6 6.8L12 17.3 5.8 20.9l1.6-6.8L2.2 8.9l6.9-.6z" />
                </svg>
              </div>
              <div class="score-desc">累计 {{ summary.count }} 条评价</div>
            </div>
            <div class="rating-overview__dist">
              <div v-for="row in distributionRows" :key="row.star" class="dist-row">
                <div class="dist-row__label">
                  {{ row.star }}<span class="star-fill">★</span>
                </div>
                <div class="dist-row__bar">
                  <div class="dist-row__bar-fill" :class="`s${row.star}`" :style="{ width: `${row.pct}%` }" />
                </div>
                <div class="dist-row__pct">{{ row.count }}</div>
              </div>
            </div>
          </div>
          <div class="rating-overview__bottom">
            <div class="rate-text">好评率 <strong>{{ summary.goodRate }}%</strong></div>
            <div class="rate-sub">购买后即可分享你的评价</div>
          </div>
        </section>

        <!-- 标签筛选 -->
        <nav class="tag-filter" role="tablist" aria-label="评价筛选">
          <button
            v-for="tab in FILTER_TABS"
            :key="tab.key"
            class="tag-filter__item"
            :class="{ active: activeFilter === tab.key }"
            type="button"
            role="tab"
            :aria-selected="activeFilter === tab.key"
            @click="setFilter(tab.key)"
          >
            {{ tab.label }}<span v-if="filterBadge(tab.key) != null" class="badge">{{ filterBadge(tab.key) }}</span>
          </button>
        </nav>

        <!-- 评价列表 -->
        <van-list
          v-model:loading="listLoading"
          :finished="finished"
          :error="listError"
          error-text="加载失败，点击重试"
          :finished-text="reviews.length > 0 ? '没有更多了' : ''"
          loading-text="加载中..."
          @load="onLoad"
        >
          <div v-if="reviews.length > 0" class="review-list">
            <article v-for="review in reviews" :key="review.id" class="review-card" role="article">
              <!-- 头部：头像 + 昵称 + 星级 + 日期 -->
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
                    <span class="review-card__date">{{ formatDate(review.createdAt) }}</span>
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
                <div class="append-label">买家追评</div>
                <div class="append-text">{{ review.appendContent }}</div>
                <div v-if="review.appendAt" class="append-time">{{ formatDate(review.appendAt) }}</div>
              </div>

              <!-- 商家回复块 -->
              <div v-if="review.reply" class="review-card__reply">
                <div class="reply-label">卖家回复</div>
                <div class="reply-text">{{ review.reply.content }}</div>
                <div class="reply-time">{{ formatDateTime(review.reply.repliedAt) }}</div>
              </div>

              <!-- 底部时间 -->
              <div class="review-card__footer">
                <div class="review-card__time">{{ formatDateTime(review.createdAt) }}</div>
              </div>
            </article>
          </div>

          <!-- 当前筛选无结果（列表为空且首页已加载完成） -->
          <EmptyState
            v-if="!firstLoading && !loadError && reviews.length === 0 && finished"
            title="该筛选下暂无评价"
            action-text="查看全部"
            @action="setFilter('all')"
          />
        </van-list>
      </van-pull-refresh>
    </main>
  </div>
</template>

<style scoped>
.product-reviews-page {
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

.nav-title .count {
  color: var(--n7);
  font-size: var(--fs-base);
  font-weight: var(--fw-normal);
}

/* 滚动主体 */
.body {
  flex: 1;
  overflow-y: auto;
  padding: var(--s3);
  padding-bottom: calc(var(--s8) + env(safe-area-inset-bottom));
}

/* 评分概览卡 */
.rating-overview {
  background: var(--n1);
  border-radius: var(--r-lg);
  padding: var(--s4);
  box-shadow: var(--sh-card);
  margin-bottom: var(--s3);
}

.rating-overview__top {
  display: flex;
  align-items: center;
  gap: var(--s6);
}

.rating-overview__score {
  display: flex;
  flex-direction: column;
  align-items: center;
  min-width: 96px;
  border-right: 1px solid var(--n3);
  padding-right: var(--s4);
}

.score-num {
  font-size: var(--fs-3xl);
  font-weight: var(--fw-semibold);
  color: var(--c-primary);
  line-height: 1.1;
}

.score-stars {
  display: flex;
  gap: 2px;
  margin-top: var(--s1);
}

.score-stars .star {
  color: var(--c-warning);
}

.score-stars .star-empty {
  color: var(--n5);
}

.score-desc {
  font-size: var(--fs-sm);
  color: var(--n7);
  margin-top: var(--s2);
}

.rating-overview__dist {
  flex: 1;
  display: flex;
  flex-direction: column;
  gap: 6px;
}

.dist-row {
  display: flex;
  align-items: center;
  gap: var(--s2);
}

.dist-row__label {
  font-size: var(--fs-sm);
  color: var(--n9);
  width: 28px;
  display: flex;
  align-items: center;
  gap: 2px;
  flex-shrink: 0;
}

.dist-row__label .star-fill {
  color: var(--c-warning);
  font-size: 10px;
}

.dist-row__bar {
  flex: 1;
  height: 8px;
  background: var(--n3);
  border-radius: 4px;
  overflow: hidden;
}

.dist-row__bar-fill {
  height: 100%;
  border-radius: 4px;
  transition: width 0.6s var(--ease-std);
}

.dist-row__bar-fill.s5 {
  background: var(--c-success);
}

.dist-row__bar-fill.s4,
.dist-row__bar-fill.s3 {
  background: var(--c-warning);
}

.dist-row__bar-fill.s2,
.dist-row__bar-fill.s1 {
  background: var(--c-error);
}

.dist-row__pct {
  font-size: var(--fs-sm);
  color: var(--n7);
  width: 36px;
  text-align: right;
  flex-shrink: 0;
}

.rating-overview__bottom {
  margin-top: var(--s4);
  padding-top: var(--s3);
  border-top: 1px solid var(--n3);
  display: flex;
  align-items: center;
  justify-content: space-between;
}

.rate-text {
  font-size: var(--fs-sm);
  color: var(--n7);
}

.rate-text strong {
  color: var(--c-success);
  font-size: var(--fs-lg);
  font-weight: var(--fw-semibold);
  margin: 0 4px;
}

.rate-sub {
  font-size: var(--fs-sm);
  color: var(--n7);
}

/* 标签筛选 */
.tag-filter {
  display: flex;
  gap: var(--s2);
  padding: var(--s3);
  background: var(--n1);
  border-radius: var(--r-lg);
  box-shadow: var(--sh-card);
  margin-bottom: var(--s3);
  overflow-x: auto;
  scrollbar-width: none;
}

.tag-filter::-webkit-scrollbar {
  display: none;
}

.tag-filter__item {
  flex-shrink: 0;
  display: flex;
  align-items: center;
  gap: 4px;
  padding: 6px 14px;
  border-radius: var(--r-base);
  background: var(--n2);
  font-size: var(--fs-sm);
  color: var(--n9);
  cursor: pointer;
  transition: all var(--d-mid);
  white-space: nowrap;
  font-family: inherit;
}

.tag-filter__item.active {
  background: var(--c-primary);
  color: #fff;
}

.tag-filter__item .badge {
  display: inline-flex;
  align-items: center;
  justify-content: center;
  min-width: 16px;
  height: 16px;
  padding: 0 4px;
  border-radius: 8px;
  background: var(--n3);
  font-size: 10px;
  color: var(--n7);
  font-weight: var(--fw-medium);
}

.tag-filter__item.active .badge {
  background: rgba(255, 255, 255, 0.3);
  color: #fff;
}

/* 评价列表 */
.review-list {
  display: flex;
  flex-direction: column;
  gap: var(--s3);
}

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

.review-card__content {
  font-size: var(--fs-base);
  color: var(--n9);
  line-height: 1.6;
  margin-bottom: var(--s2);
}

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

.review-card__footer {
  display: flex;
  align-items: center;
  justify-content: space-between;
  margin-top: var(--s2);
}

.review-card__time {
  font-size: var(--fs-sm);
  color: var(--n7);
}

/* 骨架屏 */
.sk-overview {
  height: 140px;
  border-radius: var(--r-lg);
  margin-bottom: var(--s3);
}

.sk-tabs {
  display: flex;
  gap: var(--s2);
  margin-bottom: var(--s3);
}

.sk-tab-item {
  width: 72px;
  height: 30px;
  border-radius: var(--r-base);
}

.sk-card {
  background: var(--n1);
  border-radius: var(--r-lg);
  padding: var(--s3);
  box-shadow: var(--sh-card);
}

.sk-card-head {
  display: flex;
  align-items: center;
  gap: var(--s2);
  margin-bottom: var(--s2);
}

.sk-avatar {
  width: 36px;
  height: 36px;
  border-radius: 50%;
}

.sk-card-lines {
  flex: 1;
  display: flex;
  flex-direction: column;
  gap: var(--s1);
}

.sk-line {
  width: 80%;
  height: 12px;
}

.sk-line.short {
  width: 60%;
  height: 10px;
}

.sk-block {
  height: 72px;
  border-radius: var(--r-card);
}
</style>
