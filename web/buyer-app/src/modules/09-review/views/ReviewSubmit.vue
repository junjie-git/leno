<script setup lang="ts">
import { computed, onMounted, ref } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import { showFailToast, showToast } from 'vant'
import { orderApi } from '@/modules/06-order/api/order.api'
import { reviewApi } from '@/modules/09-review/api/review.api'
import type { OrderDto, OrderItemDto } from '@/modules/06-order/types/order.dto'
import PriceText from '@/shared/components/PriceText.vue'
import ErrorState from '@/shared/components/ErrorState.vue'
import { logger } from '@/shared/utils/logger'

/**
 * 提交评价页（/review/submit/:orderLineId）
 *
 * 结构（对齐设计稿 review-submit）：
 * NavBar（返回 / 发表评价）→ 滚动主体（商品卡 → 总体评分 → 评价内容 → 匿名开关）
 * → 底部固定提交栏（提交评价）
 *
 * 交互流：
 * - 路由仅携带 orderLineId，按页遍历 GET /orders（以 total 为上界）定位所属订单行；
 * - 订单非已完成 → 提示「订单完成后才能评价」；订单行已评价 → 提示「该商品已评价」；
 * - 评分默认 5 星（van-rate），文案随分数联动（1-2 差评 / 3 中评 / 4-5 好评）；
 * - 评价内容 5-500 字，实时字数统计；
 * - 匿名开关默认关闭；
 * - 提交（防重复：按钮 disabled + loading；POST 幂等键由拦截器注入）
 *   → POST /orders/{orderId}/reviews → 成功 toast「提交成功，等待审核」→ 返回订单详情。
 */

const route = useRoute()
const router = useRouter()

/** 评价内容字数限制（与 API 契约一致） */
const CONTENT_MIN = 5
const CONTENT_MAX = 500

// ---- 页面状态 ----
const loading = ref(true)
const loadError = ref(false)
const notFound = ref(false)
const notCompleted = ref(false)
const alreadyReviewed = ref(false)
const order = ref<OrderDto | null>(null)
const orderLine = ref<OrderItemDto | null>(null)

// ---- 表单状态 ----
const rating = ref(5)
const content = ref('')
const isAnonymous = ref(false)
const submitting = ref(false)

/** 评语文案（随分数联动） */
const ratingDesc = computed(() => {
  if (rating.value >= 4) {
    return { text: '好评', cls: 'good' }
  }
  if (rating.value === 3) {
    return { text: '中评', cls: 'mid' }
  }
  return { text: '差评', cls: 'bad' }
})

/** 当前字数 */
const contentLength = computed(() => content.value.length)

onMounted(() => {
  void loadOrderLine()
})

/**
 * 按 orderLineId 定位所属订单与订单行
 *
 * 订单接口无单行查询端点，按页遍历订单列表（pageSize=50，total 为上界）。
 */
async function loadOrderLine(): Promise<void> {
  const orderLineId = String(route.params.orderLineId ?? '')
  loading.value = true
  loadError.value = false
  notFound.value = false
  notCompleted.value = false
  alreadyReviewed.value = false
  order.value = null
  orderLine.value = null
  try {
    const pageSize = 50
    const first = await orderApi.list({ page: 1, pageSize })
    const maxPage = Math.max(1, Math.ceil(first.total / pageSize))
    let matched: OrderDto | null = null
    let line: OrderItemDto | null = null
    for (let page = 1; page <= maxPage; page++) {
      const result = page === 1 ? first : await orderApi.list({ page, pageSize })
      for (const o of result.items) {
        const hit = o.items.find((l) => l.orderLineId === orderLineId)
        if (hit) {
          matched = o
          line = hit
          break
        }
      }
      if (matched) break
    }
    if (!matched || !line) {
      notFound.value = true
      return
    }
    if (matched.status !== 'Completed') {
      order.value = matched
      notCompleted.value = true
      return
    }
    if (line.reviewed) {
      order.value = matched
      alreadyReviewed.value = true
      return
    }
    order.value = matched
    orderLine.value = line
  } catch (e) {
    logger.error('评价订单行定位失败', e)
    loadError.value = true
  } finally {
    loading.value = false
  }
}

/** 提交评价 */
async function submitReview(): Promise<void> {
  const line = orderLine.value
  const target = order.value
  if (!line || !target || submitting.value) return
  const text = content.value.trim()
  if (rating.value < 1) {
    showToast('请先选择评分')
    return
  }
  if (text.length < CONTENT_MIN) {
    showToast(`评价内容至少 ${CONTENT_MIN} 个字`)
    return
  }
  if (text.length > CONTENT_MAX) {
    showToast(`评价内容最多 ${CONTENT_MAX} 个字`)
    return
  }
  submitting.value = true
  try {
    await reviewApi.submitOrderReviews(target.id, {
      reviews: [
        {
          orderLineId: line.orderLineId,
          rating: rating.value,
          content: text,
          images: [],
          isAnonymous: isAnonymous.value,
        },
      ],
    })
    showToast('提交成功，等待审核')
    router.replace(`/order/${target.id}`)
  } catch (e) {
    logger.warn('提交评价失败', e)
    showFailToast(e instanceof Error ? e.message : '提交失败，请稍后重试')
  } finally {
    submitting.value = false
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

function goOrder(): void {
  if (order.value) {
    router.replace(`/order/${order.value.id}`)
  } else {
    goBack()
  }
}

function goHome(): void {
  router.replace('/')
}
</script>

<template>
  <div class="review-submit-page">
    <!-- NavBar -->
    <header class="navbar">
      <button class="nav-back" type="button" aria-label="返回" @click="goBack">
        <svg width="20" height="20" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round">
          <path d="M15 18l-6-6 6-6" />
        </svg>
      </button>
      <div class="nav-title">发表评价</div>
    </header>

    <!-- 滚动主体 -->
    <main class="body">
      <!-- 骨架屏 -->
      <div v-if="loading" class="content" aria-label="加载中">
        <div class="skeleton-block sk-product" />
        <div class="skeleton-block sk-rating" />
        <div class="skeleton-block sk-textarea" />
        <div class="skeleton-block sk-switch" />
      </div>

      <!-- 加载失败 -->
      <ErrorState
        v-else-if="loadError"
        title="订单信息加载失败"
        description="网络异常，请检查网络连接后重试"
        @retry="loadOrderLine"
      />

      <!-- 订单行不存在 -->
      <ErrorState
        v-else-if="notFound"
        title="未找到对应订单"
        description="订单不存在或已被删除，无法评价"
        retry-text="返回首页"
        @retry="goHome"
      />

      <!-- 订单未完成 -->
      <ErrorState
        v-else-if="notCompleted"
        title="订单完成后才能评价"
        description="确认收货并完成订单后，再来分享你的购物体验吧"
        retry-text="返回订单"
        @retry="goOrder"
      />

      <!-- 已评价过 -->
      <ErrorState
        v-else-if="alreadyReviewed"
        title="该商品已评价"
        description="每个订单商品仅可评价一次，感谢你的分享"
        retry-text="返回订单"
        @retry="goOrder"
      />

      <!-- 评价表单 -->
      <div v-else-if="orderLine && order" class="content">
        <!-- 商品卡 -->
        <section class="product-card" aria-label="评价商品">
          <img class="product-card__img" :src="orderLine.image" :alt="orderLine.name">
          <div class="product-card__info">
            <div class="product-card__name">{{ orderLine.name }}</div>
            <div class="product-card__meta">
              <span class="product-card__sku">{{ orderLine.specs }}</span>
              <PriceText :amount="orderLine.price" :size="14" />
            </div>
          </div>
        </section>

        <!-- 总体评分 -->
        <section class="rating-section">
          <div class="section-title">总体评分</div>
          <div class="rating-overall" role="radiogroup" aria-label="总体评分">
            <van-rate
              v-model="rating"
              :size="32"
              color="#FAAD14"
              void-icon="star"
              void-color="#D9D9D9"
              aria-label="评分"
            />
            <div class="rating-overall__desc" :class="ratingDesc.cls">{{ ratingDesc.text }}</div>
          </div>
        </section>

        <!-- 评价内容 -->
        <section class="textarea-section">
          <div class="section-title">评价内容</div>
          <textarea
            v-model="content"
            class="textarea"
            :maxlength="CONTENT_MAX"
            placeholder="说说商品体验吧～ 宝宝的真实评价对其他买家很重要哦"
            aria-label="评价内容"
          />
          <div class="textarea-counter">
            <span class="current">{{ contentLength }}</span>/<span class="max">{{ CONTENT_MAX }}</span>
          </div>
          <div class="textarea-tips">
            <svg width="12" height="12" viewBox="0 0 24 24" fill="currentColor">
              <path d="M12 2C6.5 2 2 6.5 2 12s4.5 10 10 10 10-4.5 10-10S17.5 2 12 2zm1 15h-2v-2h2v2zm0-4h-2V7h2v6z" />
            </svg>
            <span>至少输入 {{ CONTENT_MIN }} 个字，最多 {{ CONTENT_MAX }} 字</span>
          </div>
        </section>

        <!-- 匿名开关 -->
        <section class="switch-cell">
          <div class="switch-cell__left">
            <div class="switch-cell__label">匿名评价</div>
            <div class="switch-cell__desc">开启后将以「匿名用户」展示</div>
          </div>
          <van-switch v-model="isAnonymous" size="24" aria-label="匿名评价开关" />
        </section>
      </div>
    </main>

    <!-- 底部提交栏 -->
    <footer v-if="!loading && !loadError && !notFound && !notCompleted && !alreadyReviewed" class="submit-bar">
      <button
        class="submit-btn"
        :class="{ loading: submitting }"
        type="button"
        :disabled="submitting"
        aria-label="提交评价"
        @click="submitReview"
      >
        {{ submitting ? '提交中...' : '提交评价' }}
      </button>
    </footer>
  </div>
</template>

<style scoped>
.review-submit-page {
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
  background: var(--n2);
  padding-bottom: calc(var(--s4) + env(safe-area-inset-bottom));
}

.content {
  padding: var(--s3);
  display: flex;
  flex-direction: column;
  gap: var(--s3);
}

/* 商品卡 */
.product-card {
  background: var(--n1);
  border-radius: var(--r-lg);
  box-shadow: var(--sh-card);
  padding: var(--s3);
  display: flex;
  gap: var(--s3);
}

.product-card__img {
  width: 72px;
  height: 72px;
  border-radius: var(--r-card);
  flex-shrink: 0;
  object-fit: cover;
  background: var(--n3);
}

.product-card__info {
  flex: 1;
  display: flex;
  flex-direction: column;
  justify-content: space-between;
  min-width: 0;
}

.product-card__name {
  font-size: var(--fs-base);
  color: var(--n10);
  line-height: 1.4;
  display: -webkit-box;
  -webkit-line-clamp: 2;
  -webkit-box-orient: vertical;
  overflow: hidden;
}

.product-card__meta {
  display: flex;
  align-items: center;
  justify-content: space-between;
  gap: var(--s2);
}

.product-card__sku {
  font-size: var(--fs-sm);
  color: var(--n7);
  min-width: 0;
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
}

/* 评分区 */
.rating-section {
  background: var(--n1);
  border-radius: var(--r-lg);
  box-shadow: var(--sh-card);
  padding: var(--s3);
}

.section-title {
  font-size: var(--fs-base);
  color: var(--n10);
  font-weight: var(--fw-medium);
  margin-bottom: var(--s2);
}

.rating-overall {
  display: flex;
  flex-direction: column;
  align-items: center;
  gap: var(--s2);
  padding: var(--s3) 0;
}

.rating-overall__desc {
  font-size: var(--fs-lg);
  font-weight: var(--fw-medium);
}

.rating-overall__desc.good {
  color: var(--c-success);
}

.rating-overall__desc.mid {
  color: var(--c-warning);
}

.rating-overall__desc.bad {
  color: var(--c-error);
}

/* 评价内容 */
.textarea-section {
  background: var(--n1);
  border-radius: var(--r-lg);
  box-shadow: var(--sh-card);
  padding: var(--s3);
}

.textarea {
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

.textarea:focus {
  border-color: var(--c-primary);
}

.textarea::placeholder {
  color: var(--n7);
}

.textarea-counter {
  text-align: right;
  font-size: var(--fs-sm);
  color: var(--n7);
  margin-top: var(--s1);
}

.textarea-counter .current {
  color: var(--n9);
}

.textarea-tips {
  font-size: var(--fs-sm);
  color: var(--n7);
  margin-top: var(--s2);
  display: flex;
  align-items: center;
  gap: 4px;
}

.textarea-tips svg {
  color: var(--c-warning);
}

/* 匿名开关 */
.switch-cell {
  background: var(--n1);
  border-radius: var(--r-lg);
  box-shadow: var(--sh-card);
  padding: var(--s3) var(--s4);
  display: flex;
  align-items: center;
  justify-content: space-between;
}

.switch-cell__left {
  display: flex;
  flex-direction: column;
  gap: 2px;
}

.switch-cell__label {
  font-size: var(--fs-base);
  color: var(--n10);
}

.switch-cell__desc {
  font-size: var(--fs-sm);
  color: var(--n7);
}

/* 底部提交栏 */
.submit-bar {
  background: var(--n1);
  border-top: 1px solid var(--n3);
  display: flex;
  align-items: center;
  padding: var(--s2) var(--s3);
  box-shadow: 0 -2px 8px rgba(0, 0, 0, 0.04);
  flex-shrink: 0;
  padding-bottom: env(safe-area-inset-bottom);
}

.submit-btn {
  width: 100%;
  height: 44px;
  background: var(--c-primary);
  color: #fff;
  border: none;
  border-radius: var(--r-base);
  font-size: var(--fs-lg);
  font-weight: var(--fw-semibold);
  font-family: inherit;
  cursor: pointer;
  display: flex;
  align-items: center;
  justify-content: center;
  gap: var(--s2);
  transition: opacity var(--d-mid);
}

.submit-btn:active {
  opacity: 0.85;
}

.submit-btn.loading {
  background: var(--n5);
  cursor: not-allowed;
}

/* 骨架屏 */
.sk-product {
  height: 96px;
  border-radius: var(--r-lg);
}

.sk-rating {
  height: 130px;
  border-radius: var(--r-lg);
}

.sk-textarea {
  height: 200px;
  border-radius: var(--r-lg);
}

.sk-switch {
  height: 56px;
  border-radius: var(--r-lg);
}
</style>
