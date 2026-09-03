<script setup lang="ts">
import { computed, onMounted, ref } from 'vue'
import { useRouter } from 'vue-router'
import { couponApi } from '@/modules/08-promotion/api/coupon.api'
import type { CouponStatus, CouponType, MyCouponDto } from '@/modules/08-promotion/types/promotion.dto'
import ErrorState from '@/shared/components/ErrorState.vue'
import EmptyState from '@/shared/components/EmptyState.vue'
import { formatDate, formatPrice } from '@/shared/utils/format'
import { logger } from '@/shared/utils/logger'

/**
 * 我的优惠券页（/coupons/mine）
 *
 * 结构（对齐设计稿 my-coupons）：
 * NavBar（返回 / 我的优惠券 / 领券中心入口）→ 状态 Tab（未使用 / 已使用 / 已过期，CouponStatus 枚举）
 * → 滚动主体（优惠券卡片 + 使用说明；未使用券「去使用」/ 已使用·已过期券置灰 + 状态戳）
 * → 空态（按 Tab 差异化文案；未使用 Tab 提供「去领取」CTA）
 *
 * 交互流：
 * - 进入页面并行拉取三种状态（GET /coupons/mine?status=Usable|Used|Expired），Tab 即切即显；
 * - 未使用券「去使用」→ 券模板无商品关联，统一跳首页选购；
 * - NavBar 右侧「领券中心」→ /coupons/available；
 * - 下拉刷新重新拉取三个状态。
 */

const router = useRouter()

// ---- 状态 ----
const loading = ref(true)
const loadError = ref(false)
const activeTab = ref<CouponStatus>('Usable')
const couponsByStatus = ref<Record<CouponStatus, MyCouponDto[]>>({
  Usable: [],
  Used: [],
  Expired: [],
})

/** Tab 元信息 */
const TABS: Array<{ key: CouponStatus; label: string; emptyText: string }> = [
  { key: 'Usable', label: '未使用', emptyText: '暂无可用优惠券' },
  { key: 'Used', label: '已使用', emptyText: '暂无已使用优惠券' },
  { key: 'Expired', label: '已过期', emptyText: '暂无已过期优惠券' },
]

/** 券类型 → 使用说明 */
const USAGE_TEXT: Record<CouponType, string> = {
  Threshold: '结算时满足门槛金额自动抵扣',
  Shipping: '结算时满足门槛金额自动免除运费',
  Discount: '结算时满足门槛金额按折扣率优惠',
}

/** 当前 Tab 列表 */
const currentList = computed(() => couponsByStatus.value[activeTab.value])

/** 未使用数量（Tab 角标） */
const usableCount = computed(() => couponsByStatus.value.Usable.length)

/** 当前空态文案 */
const currentEmptyText = computed(
  () => TABS.find((t) => t.key === activeTab.value)?.emptyText ?? '暂无优惠券',
)

onMounted(() => {
  void loadAll()
})

/** 并行拉取三种状态优惠券 */
async function loadAll(): Promise<void> {
  loading.value = true
  loadError.value = false
  try {
    const [usable, used, expired] = await Promise.all([
      couponApi.listMine('Usable'),
      couponApi.listMine('Used'),
      couponApi.listMine('Expired'),
    ])
    couponsByStatus.value = { Usable: usable, Used: used, Expired: expired }
  } catch (e) {
    logger.error('我的优惠券加载失败', e)
    loadError.value = true
  } finally {
    loading.value = false
  }
}

/** 门槛文案：满 N 可用 / 无门槛 */
function thresholdText(coupon: MyCouponDto): string {
  return coupon.threshold > 0 ? `满${formatPrice(coupon.threshold)}可用` : '无门槛'
}

/** 面额主文案：满减 ¥N / 包邮 / 折扣 N 折 */
function amountText(coupon: MyCouponDto): { symbol: string; num: string } {
  if (coupon.type === 'Shipping') {
    return { symbol: '', num: '包邮' }
  }
  if (coupon.type === 'Discount') {
    return { symbol: '', num: `${(coupon.discount / 10).toFixed(1)}折` }
  }
  return { symbol: '¥', num: formatPrice(coupon.discount) }
}

/** 有效期文案 */
function validityText(coupon: MyCouponDto): string {
  if (coupon.status === 'Used') {
    return `已使用 · ${formatDate(coupon.validFrom)} 至 ${formatDate(coupon.validTo)}`
  }
  if (coupon.status === 'Expired') {
    return `已于 ${formatDate(coupon.validTo)} 过期`
  }
  return `${formatDate(coupon.validFrom)} - ${formatDate(coupon.validTo)}`
}

/** 底部按钮文案（仅未使用可点） */
function buttonText(coupon: MyCouponDto): string {
  if (coupon.status === 'Used') return '已使用'
  if (coupon.status === 'Expired') return '已过期'
  return '去使用'
}

// ---- Tab / 跳转 ----
function switchTab(status: CouponStatus): void {
  activeTab.value = status
}

function goBack(): void {
  if (window.history.length > 1) {
    router.back()
  } else {
    router.replace('/')
  }
}

function goCouponCenter(): void {
  router.push('/coupons/available')
}

/** 去使用：券无商品关联 → 跳首页选购 */
function goUse(): void {
  if (activeTab.value !== 'Usable') return
  router.replace('/')
}
</script>

<template>
  <div class="my-coupons-page">
    <!-- NavBar -->
    <header class="navbar">
      <button class="nav-back" type="button" aria-label="返回" @click="goBack">
        <svg width="20" height="20" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round">
          <path d="M15 18l-6-6 6-6" />
        </svg>
      </button>
      <div class="nav-title">我的优惠券</div>
      <button class="nav-right" type="button" aria-label="领券中心" @click="goCouponCenter">
        <svg width="16" height="16" viewBox="0 0 16 16" fill="none" stroke="currentColor" stroke-width="1.3" stroke-linejoin="round">
          <path d="M2 4h12v3a2 2 0 000 4v3H2v-3a2 2 0 000-4V4Z" />
          <path d="M8 4v9" stroke-dasharray="1.5 1.5" />
        </svg>
        <span>领券中心</span>
      </button>
    </header>

    <!-- 状态 Tab -->
    <nav class="tabs" role="tablist" aria-label="券状态筛选">
      <div
        v-for="tab in TABS"
        :key="tab.key"
        class="tab"
        :class="{ active: activeTab === tab.key }"
        role="tab"
        :aria-selected="activeTab === tab.key"
        @click="switchTab(tab.key)"
      >
        {{ tab.label }}<span v-if="tab.key === 'Usable' && usableCount > 0" class="count">{{ usableCount }}</span>
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
        @retry="loadAll"
      />

      <!-- 空态 -->
      <EmptyState
        v-else-if="currentList.length === 0"
        :title="currentEmptyText"
        :action-text="activeTab === 'Usable' ? '去领取' : ''"
        @action="goCouponCenter"
      />

      <!-- 券列表 -->
      <div v-else class="coupon-list">
        <article
          v-for="coupon in currentList"
          :key="coupon.id"
          class="coupon-card"
          :class="{
            'coupon-card--usable': coupon.status === 'Usable',
            'coupon-card--used': coupon.status === 'Used',
            'coupon-card--expired': coupon.status === 'Expired',
          }"
          role="article"
        >
          <!-- 左侧面额区 -->
          <div class="coupon-card__left">
            <div class="coupon-card__amount" :aria-label="`面额${amountText(coupon).num}`">
              <span v-if="amountText(coupon).symbol" class="symbol">{{ amountText(coupon).symbol }}</span>
              <span class="num">{{ amountText(coupon).num }}</span>
            </div>
            <div class="coupon-card__threshold-label">{{ thresholdText(coupon) }}</div>
          </div>

          <!-- 右侧信息区 -->
          <div class="coupon-card__right">
            <span v-if="coupon.status !== 'Usable'" class="status-stamp" :class="`status-stamp--${coupon.status.toLowerCase()}`">
              {{ coupon.status === 'Used' ? '已使用' : '已过期' }}
            </span>
            <div class="coupon-card__info">
              <div class="coupon-card__name">{{ coupon.name }}</div>
              <div class="coupon-card__scope">{{ coupon.scopeText }}</div>
            </div>
            <div class="coupon-card__usage">{{ USAGE_TEXT[coupon.type] }}</div>
            <div class="coupon-card__bottom">
              <div class="coupon-card__validity">{{ validityText(coupon) }}</div>
              <button
                class="use-btn"
                :class="{ disabled: coupon.status !== 'Usable' }"
                type="button"
                :disabled="coupon.status !== 'Usable'"
                :aria-label="buttonText(coupon)"
                @click="goUse"
              >
                {{ buttonText(coupon) }}
              </button>
            </div>
          </div>
        </article>
      </div>
    </main>
  </div>
</template>

<style scoped>
.my-coupons-page {
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

.nav-right {
  margin-left: auto;
  display: flex;
  align-items: center;
  gap: 4px;
  background: none;
  border: none;
  font-family: inherit;
  font-size: var(--fs-sm);
  color: var(--c-primary);
  cursor: pointer;
  padding: var(--s2);
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

.tab .count {
  display: inline-block;
  min-width: 16px;
  height: 16px;
  line-height: 16px;
  padding: 0 4px;
  margin-left: 4px;
  font-size: 10px;
  color: #fff;
  background: var(--c-error);
  border-radius: 8px;
  font-weight: var(--fw-medium);
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
  min-height: 100px;
  border: 1px solid transparent;
}

.coupon-card--usable {
  border-color: rgba(22, 119, 255, 0.25);
}

.coupon-card--used,
.coupon-card--expired {
  opacity: 0.65;
}

/* 左侧面额区 */
.coupon-card__left {
  width: 104px;
  flex-shrink: 0;
  display: flex;
  flex-direction: column;
  align-items: center;
  justify-content: center;
  padding: var(--s2);
  position: relative;
  background: linear-gradient(135deg, #1677ff 0%, #0e5fcc 100%);
  color: #fff;
}

.coupon-card--used .coupon-card__left,
.coupon-card--expired .coupon-card__left {
  background: linear-gradient(135deg, #bfbfbf 0%, #8c8c8c 100%);
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
  background: var(--n1);
}

.coupon-card__left::before {
  top: -4px;
}

.coupon-card__left::after {
  bottom: -4px;
}

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

.coupon-card__threshold-label {
  font-size: var(--fs-sm);
  margin-top: var(--s1);
  opacity: 0.9;
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
  position: relative;
}

.coupon-card__info {
  min-width: 0;
}

.coupon-card__name {
  font-size: var(--fs-lg);
  font-weight: var(--fw-medium);
  color: var(--n10);
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
}

.coupon-card__scope {
  font-size: var(--fs-sm);
  color: var(--n9);
  margin-top: 2px;
}

/* 使用说明 */
.coupon-card__usage {
  font-size: var(--fs-sm);
  color: var(--n7);
  margin-top: var(--s2);
  background: var(--n2);
  border-radius: var(--r-base);
  padding: 4px var(--s2);
  align-self: flex-start;
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

/* 状态戳 */
.status-stamp {
  position: absolute;
  top: var(--s2);
  right: var(--s3);
  font-size: 11px;
  font-weight: var(--fw-semibold);
  padding: 2px var(--s2);
  border-radius: var(--r-base);
  color: var(--n7);
  background: var(--n3);
}

/* 去使用按钮 */
.use-btn {
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

.use-btn:active {
  opacity: 0.85;
}

.use-btn.disabled {
  background: var(--n3);
  color: var(--n7);
  cursor: not-allowed;
}

/* 骨架屏 */
.coupon-card.is-skeleton {
  min-height: 100px;
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
