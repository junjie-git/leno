<script setup lang="ts">
import { computed, onMounted, onUnmounted, ref, watch } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import { showFailToast } from 'vant'
import { paymentApi } from '@/modules/07-payment/api/payment.api'
import { orderApi } from '@/modules/06-order/api/order.api'
import type { PaymentChannel, PaymentResultDto } from '@/modules/07-payment/types/payment.dto'
import type { OrderDto } from '@/modules/06-order/types/order.dto'
import ErrorState from '@/shared/components/ErrorState.vue'
import { formatDateTime, formatPriceExact } from '@/shared/utils/format'
import { logger } from '@/shared/utils/logger'

/**
 * 支付结果页
 *
 * 页面结构（对齐设计稿 payment-result）：
 * NavBar（返回 + 「支付结果」）→ 滚动主体：
 *   结果头部（状态图标 + 标题 + 说明 + 金额：成功绿 / 失败红 / 取消黄 / 处理中蓝）
 *   → 订单摘要卡片（订单编号 / 支付方式 / 支付时间或失败原因 / 支付金额）
 * → 底部固定操作栏（按状态切换按钮组）
 *
 * 状态机：
 * - 成功（Success）→「查看订单」（/order/:id）+「继续购物」（首页）；
 * - 失败（Failed / Expired / Refunded）→「重新支付」（回收银台）+「返回订单」；
 * - 已取消（订单 Cancelled）→「返回订单列表」+「继续购物」；
 * - 处理中（Pending / Processing）→ 每 3 秒轮询 GET /payments/result/{orderId}，最多 10 次，
 *   期间可手动「刷新状态」；轮询耗尽仍无终态 → 超时提示 +「返回订单」。
 */

const route = useRoute()
const router = useRouter()

// ---- 常量 ----
/** 轮询间隔（ms） */
const POLL_INTERVAL_MS = 3000
/** 最大轮询次数 */
const POLL_MAX_TIMES = 10

/** 支付渠道中文名 */
const channelNameMap: Record<PaymentChannel, string> = {
  Alipay: '支付宝',
  WeChatPay: '微信支付',
  UnionPay: '银联云闪付',
}

// ---- 状态 ----
const loading = ref(true)
const loadError = ref(false)
const result = ref<PaymentResultDto | null>(null)
const order = ref<OrderDto | null>(null)
const refreshing = ref(false)
const pollExhausted = ref(false)

let pollTimer: ReturnType<typeof setTimeout> | null = null
let pollTimes = 0

/** 路由订单号 */
const orderId = computed(() => String(route.params.orderId ?? ''))

// ---- 派生态 ----
/** 页面视图状态 */
type ResultView = 'success' | 'fail' | 'processing' | 'timeout' | 'cancelled'

const view = computed<ResultView>(() => {
  const r = result.value
  if (!r) return 'processing'
  if (r.paymentStatus === 'Success') return 'success'
  if (r.paymentStatus === 'Expired' && r.orderStatus === 'Cancelled') return 'cancelled'
  if (r.paymentStatus === 'Failed' || r.paymentStatus === 'Expired' || r.paymentStatus === 'Refunded') {
    return 'fail'
  }
  if (pollExhausted.value) return 'timeout'
  return 'processing'
})

/** 结果标题 */
const titleText = computed(() => {
  switch (view.value) {
    case 'success':
      return '支付成功'
    case 'fail':
      return '支付失败'
    case 'cancelled':
      return '订单已取消'
    case 'timeout':
      return '支付结果查询超时'
    default:
      return '支付处理中'
  }
})

/** 结果说明文案 */
const reasonText = computed(() => {
  const r = result.value
  switch (view.value) {
    case 'success':
      return '订单支付成功，商品即将为您发出'
    case 'fail':
      if (r?.paymentStatus === 'Refunded') return '该笔支付已退款'
      return r?.failReason ? `支付未通过，原因：${r.failReason}` : '支付未通过，请重新支付'
    case 'cancelled':
      return r?.failReason ?? '订单已取消，如需购买请重新下单'
    case 'timeout':
      return '支付结果查询超时，请稍后在订单列表查看'
    default:
      return '支付结果确认中，系统每 3 秒自动刷新'
  }
})

/** 摘要卡状态角标 */
const tagText = computed(() => {
  switch (view.value) {
    case 'success':
      return '已支付'
    case 'fail':
      return '支付失败'
    case 'cancelled':
      return '已取消'
    case 'timeout':
      return '查询超时'
    default:
      return '处理中'
  }
})

/** 支付金额（分 → 元，两位小数） */
const amountText = computed(() => formatPriceExact(result.value?.amount ?? 0))

/** 支付方式中文名（渠道未回传时为空，隐藏该行） */
const channelName = computed(() => {
  const c = result.value?.channel
  return c ? channelNameMap[c] : ''
})

/** 订单编号（订单摘要拉取失败时降级为订单 id） */
const orderNoText = computed(() => order.value?.orderNo ?? result.value?.orderId ?? '')

/** 支付时间（仅成功态且有 paidAt 时展示） */
const paidAtText = computed(() => (result.value?.paidAt ? formatDateTime(result.value.paidAt) : ''))

// ---- 轮询 ----
/** 是否处于处理中（需继续轮询） */
function isProcessing(r: PaymentResultDto): boolean {
  return r.paymentStatus === 'Pending' || r.paymentStatus === 'Processing'
}

function stopPolling(): void {
  if (pollTimer !== null) {
    clearTimeout(pollTimer)
    pollTimer = null
  }
}

function startPolling(): void {
  stopPolling()
  pollTimes = 0
  pollExhausted.value = false
  scheduleNextPoll()
}

function scheduleNextPoll(): void {
  if (pollTimer !== null) return
  if (pollTimes >= POLL_MAX_TIMES) {
    pollExhausted.value = true
    return
  }
  pollTimer = setTimeout(() => {
    pollTimer = null
    void pollOnce()
  }, POLL_INTERVAL_MS)
}

async function pollOnce(): Promise<void> {
  try {
    result.value = await paymentApi.getResult(orderId.value)
  } catch (e) {
    logger.warn('支付结果轮询失败（将继续重试）', e)
  }
  pollTimes += 1
  if (result.value && isProcessing(result.value)) {
    scheduleNextPoll()
  }
}

// ---- 数据加载 ----
async function load(): Promise<void> {
  loading.value = true
  loadError.value = false
  stopPolling()
  try {
    const r = await paymentApi.getResult(orderId.value)
    result.value = r
    // 订单摘要（订单编号等）补充加载，失败静默降级
    try {
      order.value = await orderApi.getDetail(orderId.value)
    } catch (e) {
      logger.warn('订单摘要加载失败（忽略）', e)
    }
    if (isProcessing(r)) {
      startPolling()
    }
  } catch (e) {
    logger.error('支付结果查询失败', e)
    loadError.value = true
  } finally {
    loading.value = false
  }
}

/** 处理中手动刷新（不回骨架屏，按钮转圈反馈） */
async function refreshStatus(): Promise<void> {
  if (refreshing.value) return
  refreshing.value = true
  try {
    const r = await paymentApi.getResult(orderId.value)
    result.value = r
    if (isProcessing(r)) {
      startPolling()
    } else {
      stopPolling()
    }
  } catch (e) {
    logger.warn('手动刷新支付状态失败', e)
    showFailToast('查询失败，请稍后重试')
  } finally {
    refreshing.value = false
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

function goOrderDetail(): void {
  router.push(`/order/${orderId.value}`)
}

function goOrderList(): void {
  router.push('/orders')
}

function goHome(): void {
  router.replace('/')
}

function retryPay(): void {
  router.push(`/payment/initiate/${orderId.value}`)
}

// ---- 生命周期 ----
onMounted(() => {
  void load()
})

onUnmounted(() => {
  stopPolling()
})

// 同组件复用（订单间跳转）时重载
watch(
  () => route.params.orderId,
  (id, prev) => {
    if (id && id !== prev) {
      stopPolling()
      result.value = null
      order.value = null
      pollExhausted.value = false
      void load()
    }
  },
)
</script>

<template>
  <div class="result-page">
    <!-- NavBar -->
    <header class="navbar">
      <button class="nav-back" type="button" aria-label="返回" @click="goBack">
        <svg width="20" height="20" viewBox="0 0 20 20" fill="none">
          <path
            d="M12.5 4L7 10l5.5 6"
            stroke="currentColor"
            stroke-width="1.6"
            stroke-linecap="round"
            stroke-linejoin="round"
          />
        </svg>
      </button>
      <div class="nav-title">支付结果</div>
    </header>

    <!-- 加载骨架 -->
    <main v-if="loading" class="body">
      <div class="sk-result">
        <div class="skeleton-block sk-icon" />
        <div class="skeleton-block sk-title" />
        <div class="skeleton-block sk-sub" />
      </div>
      <div class="skeleton-block sk-card" />
    </main>

    <!-- 查询失败 -->
    <main v-else-if="loadError || !result" class="body">
      <ErrorState
        title="支付结果查询失败"
        description="网络异常，请检查网络连接后重试"
        retry-text="重新查询"
        @retry="load"
      />
    </main>

    <!-- 结果内容 -->
    <main v-else class="body">
      <!-- 结果头部 -->
      <section class="result-header" :aria-label="titleText">
        <div class="result-icon" :class="`result-icon--${view}`">
          <!-- 成功 -->
          <svg v-if="view === 'success'" width="40" height="40" viewBox="0 0 40 40" fill="none">
            <circle cx="20" cy="20" r="20" fill="#52C41A" />
            <path
              d="M12 20.5L17.5 26L28.5 14.5"
              stroke="#fff"
              stroke-width="2.6"
              stroke-linecap="round"
              stroke-linejoin="round"
            />
          </svg>
          <!-- 失败 / 已取消 -->
          <svg
            v-else-if="view === 'fail' || view === 'cancelled'"
            width="40"
            height="40"
            viewBox="0 0 40 40"
            fill="none"
          >
            <circle cx="20" cy="20" r="20" :fill="view === 'cancelled' ? '#FAAD14' : '#FF4D4F'" />
            <path d="M20 11V22" stroke="#fff" stroke-width="2.8" stroke-linecap="round" />
            <circle cx="20" cy="28.5" r="1.8" fill="#fff" />
          </svg>
          <!-- 处理中 / 查询超时 -->
          <svg v-else width="40" height="40" viewBox="0 0 40 40" fill="none">
            <circle cx="20" cy="20" r="20" fill="#1677FF" />
            <path
              d="M20 10.5V20l6.5 4"
              stroke="#fff"
              stroke-width="2.6"
              stroke-linecap="round"
              stroke-linejoin="round"
            />
          </svg>
        </div>
        <div class="result-title" :class="`result-title--${view}`">{{ titleText }}</div>
        <div class="result-reason">{{ reasonText }}</div>
        <div v-if="view === 'success' || view === 'fail'" class="result-amount">
          <span class="symbol">¥</span><span class="num">{{ amountText }}</span>
        </div>
      </section>

      <!-- 订单摘要 -->
      <section class="summary-card" role="region" aria-label="订单摘要">
        <div class="summary-card__header">
          <span class="summary-card__title">订单信息</span>
          <span class="summary-card__tag" :class="`summary-card__tag--${view}`">{{ tagText }}</span>
        </div>
        <div class="summary-row">
          <span class="summary-row__label">订单编号</span>
          <span class="summary-row__value summary-row__value--mono">{{ orderNoText }}</span>
        </div>
        <div v-if="channelName" class="summary-row">
          <span class="summary-row__label">支付方式</span>
          <span class="summary-row__value">{{ channelName }}</span>
        </div>
        <div v-if="view === 'success' && paidAtText" class="summary-row">
          <span class="summary-row__label">支付时间</span>
          <span class="summary-row__value summary-row__value--mono">{{ paidAtText }}</span>
        </div>
        <div v-if="view === 'fail' && result.failReason" class="summary-row">
          <span class="summary-row__label">失败原因</span>
          <span class="summary-row__value">{{ result.failReason }}</span>
        </div>
        <div class="summary-row">
          <span class="summary-row__label">{{ view === 'success' ? '支付金额' : '应付金额' }}</span>
          <span class="summary-row__value summary-row__value--amount">¥{{ amountText }}</span>
        </div>
      </section>
    </main>

    <!-- 底部操作栏 -->
    <footer v-if="!loading && !loadError && result" class="action-bar">
      <!-- 成功 -->
      <template v-if="view === 'success'">
        <button class="btn btn--primary" type="button" aria-label="查看订单" @click="goOrderDetail">
          查看订单
        </button>
        <button class="btn btn--ghost" type="button" aria-label="继续购物" @click="goHome">
          继续购物
        </button>
      </template>
      <!-- 失败 -->
      <template v-else-if="view === 'fail'">
        <button class="btn btn--primary" type="button" aria-label="重新支付" @click="retryPay">
          重新支付
        </button>
        <button class="btn btn--ghost" type="button" aria-label="返回订单" @click="goOrderDetail">
          返回订单
        </button>
      </template>
      <!-- 已取消 -->
      <template v-else-if="view === 'cancelled'">
        <button class="btn btn--primary" type="button" aria-label="返回订单列表" @click="goOrderList">
          返回订单列表
        </button>
        <button class="btn btn--ghost" type="button" aria-label="继续购物" @click="goHome">
          继续购物
        </button>
      </template>
      <!-- 处理中 -->
      <template v-else-if="view === 'processing'">
        <button
          class="btn btn--primary"
          type="button"
          aria-label="刷新状态"
          :disabled="refreshing"
          @click="refreshStatus"
        >
          <span v-if="refreshing" class="spinner" />
          {{ refreshing ? '正在查询...' : '刷新状态' }}
        </button>
        <button class="btn btn--ghost" type="button" aria-label="返回订单" @click="goOrderDetail">
          返回订单
        </button>
      </template>
      <!-- 轮询超时 -->
      <template v-else>
        <button class="btn btn--primary" type="button" aria-label="返回订单" @click="goOrderDetail">
          返回订单
        </button>
        <button class="btn btn--ghost" type="button" aria-label="查看订单列表" @click="goOrderList">
          查看订单列表
        </button>
      </template>
    </footer>
  </div>
</template>

<style scoped>
.result-page {
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
  justify-content: center;
  width: 32px;
  height: 32px;
  color: var(--n10);
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
  padding: var(--s3);
  padding-bottom: calc(var(--s6) + env(safe-area-inset-bottom));
}

/* 骨架屏 */
.sk-result {
  display: flex;
  flex-direction: column;
  align-items: center;
  padding: var(--s8) 0 var(--s6);
}

.sk-icon {
  width: 72px;
  height: 72px;
  border-radius: 50%;
}

.sk-title {
  width: 120px;
  height: 22px;
  margin-top: var(--s4);
}

.sk-sub {
  width: 200px;
  height: 14px;
  margin-top: var(--s1);
}

.sk-card {
  height: 240px;
  margin-top: var(--s4);
  border-radius: var(--r-lg);
}

/* 结果头部 */
.result-header {
  text-align: center;
  padding: var(--s8) 0 var(--s6);
}

.result-icon {
  width: 72px;
  height: 72px;
  border-radius: 50%;
  display: inline-flex;
  align-items: center;
  justify-content: center;
  margin-bottom: var(--s4);
  animation: pop var(--d-slow) var(--ease-std);
}

.result-icon--success {
  background: rgba(82, 196, 26, 0.12);
}

.result-icon--fail {
  background: rgba(255, 77, 79, 0.12);
}

.result-icon--cancelled {
  background: rgba(250, 173, 20, 0.12);
}

.result-icon--processing,
.result-icon--timeout {
  background: rgba(22, 119, 255, 0.1);
}

@keyframes pop {
  0% {
    transform: scale(0.6);
    opacity: 0;
  }

  100% {
    transform: scale(1);
    opacity: 1;
  }
}

.result-title {
  font-size: var(--fs-xl);
  font-weight: var(--fw-semibold);
  color: var(--n10);
  margin-bottom: var(--s1);
}

.result-title--success {
  color: var(--c-success);
}

.result-title--fail {
  color: var(--c-error);
}

.result-title--cancelled {
  color: var(--c-warning);
}

.result-title--processing,
.result-title--timeout {
  color: var(--c-primary);
}

.result-reason {
  font-size: var(--fs-base);
  color: var(--n7);
  margin-top: var(--s1);
  padding: 0 var(--s4);
}

.result-amount {
  margin-top: var(--s4);
  display: flex;
  align-items: baseline;
  justify-content: center;
  gap: 2px;
}

.result-amount .symbol {
  font-size: var(--fs-lg);
  font-weight: var(--fw-medium);
  color: var(--c-error);
}

.result-amount .num {
  font-size: var(--fs-2xl);
  font-weight: var(--fw-semibold);
  color: var(--c-error);
  font-variant-numeric: tabular-nums;
}

/* 订单摘要卡片 */
.summary-card {
  background: var(--n1);
  border-radius: var(--r-lg);
  box-shadow: var(--sh-card);
  overflow: hidden;
  margin-top: var(--s4);
}

.summary-card__header {
  display: flex;
  align-items: center;
  justify-content: space-between;
  padding: var(--s3) var(--s4);
  border-bottom: 1px solid var(--n3);
}

.summary-card__title {
  font-size: var(--fs-base);
  font-weight: var(--fw-medium);
  color: var(--n10);
}

.summary-card__tag {
  font-size: var(--fs-sm);
  padding: 2px var(--s2);
  border-radius: var(--r-base);
}

.summary-card__tag--success {
  color: var(--c-success);
  background: rgba(82, 196, 26, 0.1);
}

.summary-card__tag--fail {
  color: var(--c-error);
  background: rgba(255, 77, 79, 0.1);
}

.summary-card__tag--cancelled {
  color: var(--c-warning);
  background: rgba(250, 173, 20, 0.1);
}

.summary-card__tag--processing,
.summary-card__tag--timeout {
  color: var(--c-primary);
  background: rgba(22, 119, 255, 0.1);
}

.summary-row {
  display: flex;
  justify-content: space-between;
  align-items: center;
  padding: var(--s3) var(--s4);
  font-size: var(--fs-base);
  gap: var(--s3);
}

.summary-row + .summary-row {
  border-top: 1px solid var(--n3);
}

.summary-row__label {
  color: var(--n9);
  flex-shrink: 0;
}

.summary-row__value {
  color: var(--n10);
  font-weight: var(--fw-medium);
  text-align: right;
  word-break: break-all;
}

.summary-row__value--amount {
  color: var(--c-error);
  font-size: var(--fs-lg);
  font-weight: var(--fw-semibold);
  font-variant-numeric: tabular-nums;
}

.summary-row__value--mono {
  font-variant-numeric: tabular-nums;
  font-size: var(--fs-sm);
}

/* 底部操作栏 */
.action-bar {
  flex-shrink: 0;
  background: var(--n1);
  border-top: 1px solid var(--n3);
  padding: var(--s3);
  padding-bottom: calc(var(--s3) + env(safe-area-inset-bottom, 0px));
  display: flex;
  flex-direction: column;
  gap: var(--s3);
}

.btn {
  width: 100%;
  height: 44px;
  border-radius: 22px;
  font-size: var(--fs-lg);
  font-weight: var(--fw-medium);
  font-family: inherit;
  cursor: pointer;
  display: flex;
  align-items: center;
  justify-content: center;
  gap: var(--s1);
  transition: opacity var(--d-fast) var(--ease-std);
  border: none;
}

.btn:active {
  opacity: 0.85;
}

.btn:disabled {
  opacity: 0.7;
  cursor: not-allowed;
}

.btn--primary {
  background: var(--c-primary);
  color: #fff;
}

.btn--ghost {
  background: var(--n1);
  color: var(--n10);
  border: 1px solid var(--n5);
}

.spinner {
  width: 16px;
  height: 16px;
  border: 2px solid rgba(255, 255, 255, 0.4);
  border-top-color: #fff;
  border-radius: 50%;
  animation: spin 0.7s linear infinite;
}

@keyframes spin {
  to {
    transform: rotate(360deg);
  }
}
</style>
