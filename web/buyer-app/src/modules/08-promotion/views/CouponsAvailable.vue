<script setup lang="ts">
import { computed, onMounted, ref } from 'vue'
import { useRouter } from 'vue-router'
import { showFailToast, showToast } from 'vant'
import { couponApi } from '@/modules/08-promotion/api/coupon.api'
import type { AvailableCouponDto, CouponType } from '@/modules/08-promotion/types/promotion.dto'
import ErrorState from '@/shared/components/ErrorState.vue'
import EmptyState from '@/shared/components/EmptyState.vue'
import { formatPrice } from '@/shared/utils/format'
import { logger } from '@/shared/utils/logger'

/**
 * 领券中心页（/coupons/available）
 *
 * 结构（对齐设计稿 coupons-available）：
 * NavBar（返回 / 领券中心）→ Tab（可领取 / 已领取）
 * → 滚动主体（优惠券卡片列表：左侧面额区 + 右侧信息区 + 领取按钮）
 * → 空态（暂无可领优惠券 + 去逛逛）
 *
 * 交互流：
 * - GET /coupons/available 全量加载，received 标记区分「可领取 / 已领取」两个 Tab；
 * - 点击「立即领取」→ 按钮 disabled + loading（防重复点击）→ POST /coupons/{couponId}/receive
 *   → 成功 toast「领取成功」并本地回写 received / remainCount；失败展示后端错误信息；
 * - remainCount ≤ 0 的券按钮置灰「已抢光」，received 的券按钮置灰「已领取」；
 * - 卡片样式按类型差异化：满减（红）/ 包邮（蓝）/ 折扣（橙）。
 */

const router = useRouter()

// ---- 状态 ----
const loading = ref(true)
const loadError = ref(false)
const coupons = ref<AvailableCouponDto[]>([])
const activeTab = ref<'available' | 'received'>('available')

/** 领取中的券 ID 集合（防重复提交） */
const receivingIds = ref<Set<string>>(new Set())

/** 券类型元信息（左侧面额区配色与文案） */
const TYPE_META: Record<CouponType, { cls: string; amountCls: string; badge: string }> = {
  Threshold: { cls: 'threshold', amountCls: 'amount--threshold', badge: '满减券' },
  Shipping: { cls: 'shipping', amountCls: 'amount--shipping', badge: '包邮券' },
  Discount: { cls: 'discount', amountCls: 'amount--discount', badge: '折扣券' },
}

/** 可领取 Tab：未领取的券（含已抢光，按钮置灰） */
const availableList = computed(() => coupons.value.filter((c) => !c.received))

/** 已领取 Tab：已领取的券 */
const receivedList = computed(() => coupons.value.filter((c) => c.received))

/** 当前 Tab 展示的列表 */
const currentList = computed(() =>
  activeTab.value === 'available' ? availableList.value : receivedList.value,
)

onMounted(() => {
  void loadCoupons()
})

/** 加载可领优惠券列表 */
async function loadCoupons(): Promise<void> {
  loading.value = true
  loadError.value = false
  try {
    coupons.value = await couponApi.listAvailable()
  } catch (e) {
    logger.error('可领优惠券加载失败', e)
    loadError.value = true
  } finally {
    loading.value = false
  }
}

/** 门槛文案：满 N 可用 / 无门槛 */
function thresholdText(coupon: AvailableCouponDto): string {
  return coupon.threshold > 0 ? `满${formatPrice(coupon.threshold)}可用` : '无门槛'
}

/** 面额主文案：满减 ¥N / 包邮 / 折扣 N 折 */
function amountText(coupon: AvailableCouponDto): { symbol: string; num: string } {
  if (coupon.type === 'Shipping') {
    return { symbol: '', num: '包邮' }
  }
  if (coupon.type === 'Discount') {
    return { symbol: '', num: `${(coupon.discount / 10).toFixed(1)}折` }
  }
  return { symbol: '¥', num: formatPrice(coupon.discount) }
}

/** 按钮状态：可领取 / 领取中 / 已领取 / 已抢光 */
function buttonState(coupon: AvailableCouponDto): { text: string; disabled: boolean } {
  if (coupon.received) {
    return { text: '已领取', disabled: true }
  }
  if (coupon.remainCount <= 0) {
    return { text: '已抢光', disabled: true }
  }
  if (receivingIds.value.has(coupon.couponId)) {
    return { text: '领取中...', disabled: true }
  }
  return { text: '立即领取', disabled: false }
}

/** 领取优惠券 */
async function receive(coupon: AvailableCouponDto): Promise<void> {
  if (coupon.received || coupon.remainCount <= 0) return
  if (receivingIds.value.has(coupon.couponId)) return
  receivingIds.value = new Set(receivingIds.value).add(coupon.couponId)
  try {
    await couponApi.receive(coupon.couponId)
    coupon.received = true
    coupon.remainCount = Math.max(0, coupon.remainCount - 1)
    showToast('领取成功')
  } catch (e) {
    logger.warn('领取优惠券失败', e)
    showFailToast(e instanceof Error ? e.message : '领取失败，请稍后重试')
  } finally {
    const next = new Set(receivingIds.value)
    next.delete(coupon.couponId)
    receivingIds.value = next
  }
}

// ---- Tab / 跳转 ----
function switchTab(tab: 'available' | 'received'): void {
  activeTab.value = tab
}

function goBack(): void {
  if (window.history.length > 1) {
    router.back()
  } else {
    router.replace('/')
  }
}

function goHome(): void {
  router.replace('/')
}
</script>

<template>
  <div class="coupons-page">
    <!-- NavBar -->
    <header class="navbar">
      <button class="nav-back" type="button" aria-label="返回" @click="goBack">
        <svg width="20" height="20" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round">
          <path d="M15 18l-6-6 6-6" />
        </svg>
      </button>
      <div class="nav-title">领券中心</div>
    </header>

    <!-- Tab -->
    <nav class="tabs" role="tablist" aria-label="券状态筛选">
      <div
        class="tab"
        :class="{ active: activeTab === 'available' }"
        role="tab"
        :aria-selected="activeTab === 'available'"
        @click="switchTab('available')"
      >
        可领取
      </div>
      <div
        class="tab"
        :class="{ active: activeTab === 'received' }"
        role="tab"
        :aria-selected="activeTab === 'received'"
        @click="switchTab('received')"
      >
        已领取
      </div>
    </nav>

    <!-- 滚动主体 -->
    <main class="body">
      <!-- 骨架屏 -->
      <div v-if="loading" class="coupon-list" aria-label="加载中">
        <div v-for="i in 3" :key="i" class="coupon-card is-skeleton">
          <div class="skeleton-block card-left-sk" />
          <div class="card-right-sk">
            <div class="skeleton-block sk-l1" />
            <div class="skeleton-block sk-l2" />
            <div class="skeleton-block sk-l3" />
          </div>
        </div>
      </div>

      <!-- 错误态 -->
      <ErrorState
        v-else-if="loadError"
        title="优惠券加载失败"
        description="网络异常，请检查网络连接后重试"
        @retry="loadCoupons"
      />

      <!-- 空态 -->
      <EmptyState
        v-else-if="currentList.length === 0"
        :title="activeTab === 'available' ? '暂无可领优惠券' : '暂无已领取优惠券'"
        action-text="去逛逛"
        @action="goHome"
      />

      <!-- 券列表 -->
      <div v-else class="coupon-list">
        <article
          v-for="coupon in currentList"
          :key="coupon.couponId"
          class="coupon-card"
          :class="`coupon-card--${TYPE_META[coupon.type].cls}`"
          role="article"
        >
          <!-- 左侧面额区 -->
          <div class="coupon-card__left" :class="`left--${TYPE_META[coupon.type].cls}`">
            <div
              class="coupon-card__amount"
              :class="[TYPE_META[coupon.type].amountCls, { dim: coupon.remainCount <= 0 && !coupon.received }]"
              :aria-label="`面额${amountText(coupon).num}`"
            >
              <span v-if="amountText(coupon).symbol" class="symbol">{{ amountText(coupon).symbol }}</span>
              <span class="num">{{ amountText(coupon).num }}</span>
            </div>
            <div class="coupon-card__threshold-label" :class="{ dim: coupon.remainCount <= 0 && !coupon.received }">
              {{ thresholdText(coupon) }}
            </div>
          </div>

          <!-- 右侧信息区 -->
          <div class="coupon-card__right">
            <div class="coupon-card__info">
              <div class="coupon-card__name">
                {{ coupon.name }}
                <span class="type-badge" :class="`type-badge--${TYPE_META[coupon.type].cls}`">
                  {{ TYPE_META[coupon.type].badge }}
                </span>
              </div>
              <div class="coupon-card__scope">{{ coupon.scopeText }}</div>
            </div>
            <div class="coupon-card__bottom">
              <div class="coupon-card__validity">
                <template v-if="coupon.remainCount > 0">领取后 {{ coupon.validDays }} 天内有效 · 余 {{ coupon.remainCount }} 张</template>
                <template v-else>领取后 {{ coupon.validDays }} 天内有效</template>
              </div>
              <button
                class="receive-btn"
                :class="{ disabled: buttonState(coupon).disabled }"
                type="button"
                :disabled="buttonState(coupon).disabled"
                :aria-label="`领取 ${coupon.name}`"
                @click="receive(coupon)"
              >
                {{ buttonState(coupon).text }}
              </button>
            </div>
          </div>
        </article>
      </div>
    </main>
  </div>
</template>

<style scoped>
.coupons-page {
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

/* Tab */
.tabs {
  display: flex;
  background: var(--n1);
  border-bottom: 1px solid var(--n3);
  flex-shrink: 0;
}

.tab {
  flex: 1;
  text-align: center;
  padding: var(--s3) 0;
  font-size: var(--fs-base);
  color: var(--n9);
  position: relative;
  cursor: pointer;
}

.tab.active {
  color: var(--c-primary);
  font-weight: var(--fw-medium);
}

.tab.active::after {
  content: "";
  position: absolute;
  bottom: 0;
  left: 50%;
  transform: translateX(-50%);
  width: 24px;
  height: 3px;
  background: var(--c-primary);
  border-radius: 2px;
}

/* 滚动主体 */
.body {
  flex: 1;
  overflow-y: auto;
  padding: var(--s3);
  padding-bottom: calc(var(--s8) + env(safe-area-inset-bottom));
}

/* 券列表 */
.coupon-list {
  display: flex;
  flex-direction: column;
  gap: var(--s3);
}

/* 券卡片 */
.coupon-card {
  display: flex;
  background: var(--n1);
  border-radius: var(--r-lg);
  box-shadow: var(--sh-card);
  overflow: hidden;
  position: relative;
  min-height: 96px;
}

.coupon-card__left {
  width: 104px;
  flex-shrink: 0;
  display: flex;
  flex-direction: column;
  align-items: center;
  justify-content: center;
  padding: var(--s2);
  position: relative;
}

/* 类型差异化：满减（红）/ 包邮（蓝）/ 折扣（橙） */
.left--threshold {
  background: linear-gradient(135deg, #fff1f0 0%, #ffe7e5 100%);
}

.left--shipping {
  background: linear-gradient(135deg, #e6f4ff 0%, #d6ebff 100%);
}

.left--discount {
  background: linear-gradient(135deg, #fff7e6 0%, #ffefcc 100%);
}

/* 打孔缺口 */
.coupon-card__left::before,
.coupon-card__left::after {
  content: "";
  position: absolute;
  right: -4px;
  width: 8px;
  height: 8px;
  border-radius: 50%;
  background: var(--n2);
}

.coupon-card__left::before {
  top: -4px;
}

.coupon-card__left::after {
  bottom: -4px;
}

/* 面额 */
.coupon-card__amount {
  display: flex;
  align-items: baseline;
  font-weight: var(--fw-semibold);
  line-height: 1;
}

.coupon-card__amount .symbol {
  font-size: var(--fs-lg);
  margin-right: 1px;
}

.coupon-card__amount .num {
  font-size: var(--fs-3xl);
}

.amount--threshold {
  color: var(--c-error);
}

.amount--shipping {
  color: var(--c-primary);
}

.amount--discount {
  color: #fa8c16;
}

.coupon-card__threshold-label {
  font-size: var(--fs-sm);
  color: var(--n9);
  margin-top: var(--s1);
}

.left--threshold .coupon-card__threshold-label {
  color: var(--c-error);
  opacity: 0.85;
}

.left--shipping .coupon-card__threshold-label {
  color: var(--c-primary);
  opacity: 0.85;
}

.left--discount .coupon-card__threshold-label {
  color: #fa8c16;
  opacity: 0.85;
}

/* 已抢光置灰 */
.coupon-card__amount.dim,
.coupon-card__threshold-label.dim {
  opacity: 0.45;
}

/* 右侧信息区 */
.coupon-card__right {
  flex: 1;
  padding: var(--s3);
  display: flex;
  flex-direction: column;
  justify-content: space-between;
  min-width: 0;
  border-left: 1px dashed var(--n5);
}

.coupon-card__name {
  font-size: var(--fs-lg);
  font-weight: var(--fw-medium);
  color: var(--n10);
  display: flex;
  align-items: center;
  gap: var(--s2);
}

.type-badge {
  font-size: 10px;
  color: #fff;
  padding: 1px 5px;
  border-radius: 3px;
  font-weight: var(--fw-medium);
  flex-shrink: 0;
}

.type-badge--threshold {
  background: var(--c-error);
}

.type-badge--shipping {
  background: var(--c-primary);
}

.type-badge--discount {
  background: #fa8c16;
}

.coupon-card__scope {
  font-size: var(--fs-sm);
  color: var(--n9);
  margin-top: 2px;
}

.coupon-card__bottom {
  display: flex;
  align-items: center;
  justify-content: space-between;
  margin-top: var(--s2);
  gap: var(--s2);
}

.coupon-card__validity {
  font-size: var(--fs-sm);
  color: var(--n7);
  min-width: 0;
}

.receive-btn {
  background: var(--c-primary);
  color: #fff;
  border: none;
  border-radius: 14px;
  padding: 6px 14px;
  font-size: var(--fs-sm);
  font-weight: var(--fw-medium);
  font-family: inherit;
  cursor: pointer;
  white-space: nowrap;
  flex-shrink: 0;
  transition: opacity var(--d-fast);
}

.receive-btn:active {
  opacity: 0.85;
}

.receive-btn.disabled {
  background: var(--n3);
  color: var(--n7);
  cursor: not-allowed;
}

/* 骨架屏 */
.coupon-card.is-skeleton {
  min-height: 96px;
}

.card-left-sk {
  width: 104px;
  flex-shrink: 0;
  border-radius: 0;
}

.card-right-sk {
  flex: 1;
  padding: var(--s3);
  display: flex;
  flex-direction: column;
  gap: var(--s2);
}

.sk-l1 {
  width: 60%;
  height: 16px;
}

.sk-l2 {
  width: 40%;
  height: 12px;
}

.sk-l3 {
  width: 100%;
  height: 20px;
  margin-top: var(--s1);
}
</style>
