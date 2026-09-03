<script setup lang="ts">
import { computed, onMounted, onUnmounted, ref, watch } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import { showFailToast } from 'vant'
import { orderApi } from '@/modules/06-order/api/order.api'
import { paymentApi } from '@/modules/07-payment/api/payment.api'
import type { OrderDto } from '@/modules/06-order/types/order.dto'
import type { PaymentChannel } from '@/modules/07-payment/types/payment.dto'
import ErrorState from '@/shared/components/ErrorState.vue'
import { formatPriceExact } from '@/shared/utils/format'
import { logger } from '@/shared/utils/logger'

/**
 * 收银台（支付发起页）
 *
 * 页面结构（对齐设计稿 payment-initiate）：
 * NavBar（返回 + 「收银台」）→ 滚动主体：
 *   金额卡片（应付金额 + 订单编号 + 支付截止倒计时）
 *   → 支付方式列表（支付宝 / 微信支付 / 银联云闪付，单选，对齐 PaymentChannel 枚举）
 *   → 安全提示条
 * → 底部固定操作栏（「确认支付 ¥xx.xx」，超时后切换为「返回订单」）+ 支付中 Loading 遮罩
 *
 * 数据流：
 * - 进入页面 GET /orders/{orderId} 读取应付金额与支付截止时间，倒计时基于 payDeadline 客户端计算；
 * - 订单已支付（非待支付态）→ 直接跳 /payment/result/:orderId；订单已取消 / 不存在 → 全屏错误态；
 * - 点击「确认支付」→ POST /payments（orderId + channel）→ 发起成功后跳 /payment/result/:orderId；
 * - 倒计时结束 → 标记「订单已超时」并引导返回订单。
 */

const route = useRoute()
const router = useRouter()

// ---- 状态 ----
const loading = ref(true)
const loadError = ref(false)
const notFound = ref(false)
const order = ref<OrderDto | null>(null)
const selectedChannel = ref<PaymentChannel>('Alipay')
const paying = ref(false)

/** 路由订单号 */
const orderId = computed(() => String(route.params.orderId ?? ''))

/** 支付方式选项（与 PaymentChannel 枚举对齐：支付宝 / 微信 / 银联） */
interface ChannelOption {
  channel: PaymentChannel
  name: string
  description: string
  /** 角标文案（可选） */
  tag?: string
  icon: 'alipay' | 'wechat' | 'union'
}

const channelOptions: ChannelOption[] = [
  { channel: 'Alipay', name: '支付宝', description: '跳转支付宝完成支付', tag: '立减优惠', icon: 'alipay' },
  { channel: 'WeChatPay', name: '微信支付', description: '微信内可用，推荐使用', icon: 'wechat' },
  { channel: 'UnionPay', name: '银联云闪付', description: '支持储蓄卡 / 信用卡', icon: 'union' },
]

// ---- 派生态 ----
/** 应付金额（分） */
const payableAmount = computed(() => order.value?.amounts.payableAmount ?? 0)

/** 订单已取消（全屏错误态） */
const orderCancelled = computed(() => order.value?.status === 'Cancelled')

/** 错误态文案 */
const errorTitle = computed(() =>
  notFound.value ? '订单不存在' : orderCancelled.value ? '订单已取消' : '订单信息加载失败',
)
const errorDescription = computed(() => {
  if (notFound.value) return '该订单不存在或已被删除，请返回订单列表查看'
  if (orderCancelled.value) return order.value?.cancelReason ?? '订单已取消，无法继续支付'
  return '网络异常，请检查网络连接后重试'
})
const errorRetryText = computed(() => (loadError.value ? '重新加载' : '返回订单列表'))

// ---- 倒计时 ----
const nowMs = ref(Date.now())
let tickTimer: ReturnType<typeof setInterval> | null = null

/** 剩余支付时间（ms；无 payDeadline 时为 null） */
const remainMs = computed(() => {
  const deadline = order.value?.payDeadline
  if (!deadline) return null
  const t = new Date(deadline).getTime()
  if (Number.isNaN(t)) return null
  return t - nowMs.value
})

/** 支付是否已超时 */
const payExpired = computed(() => remainMs.value !== null && remainMs.value <= 0)

/** 倒计时文案（mm:ss） */
const countdownText = computed(() => {
  if (remainMs.value === null) return ''
  const total = Math.max(0, Math.ceil(remainMs.value / 1000))
  const m = String(Math.floor(total / 60)).padStart(2, '0')
  const s = String(total % 60).padStart(2, '0')
  return `${m}:${s}`
})

/** 剩余不足 1 分钟 → 红色告警 */
const countdownDanger = computed(() => remainMs.value !== null && remainMs.value < 60_000)

// ---- 数据加载 ----
async function loadOrder(): Promise<void> {
  loading.value = true
  loadError.value = false
  notFound.value = false
  try {
    const o = await orderApi.getDetail(orderId.value)
    order.value = o
    if (o.status === 'Cancelled') {
      // 订单已取消 → 全屏错误态
      return
    }
    if (o.status !== 'PendingPayment') {
      // 订单已支付 → 直接跳支付结果页展示成功
      router.replace(`/payment/result/${orderId.value}`)
      return
    }
  } catch (e) {
    logger.error('收银台订单加载失败', e)
    if (e instanceof Error && e.message.includes('不存在')) {
      notFound.value = true
    } else {
      loadError.value = true
    }
  } finally {
    loading.value = false
  }
}

// ---- 发起支付 ----
async function onPay(): Promise<void> {
  if (paying.value) return
  if (payExpired.value) {
    // 订单超时 → 返回订单详情
    router.push(`/order/${orderId.value}`)
    return
  }
  if (!order.value || order.value.status !== 'PendingPayment') return
  paying.value = true
  try {
    await paymentApi.create({ orderId: orderId.value, channel: selectedChannel.value })
    // 支付单创建成功 → 跳支付结果页（由结果页轮询确认支付状态）
    router.replace(`/payment/result/${orderId.value}`)
  } catch (e) {
    logger.error('发起支付失败', e)
    showFailToast(e instanceof Error ? e.message : '支付发起失败，请稍后重试')
  } finally {
    paying.value = false
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

function goOrderList(): void {
  router.push('/orders')
}

function onErrorRetry(): void {
  if (loadError.value) {
    void loadOrder()
  } else {
    goOrderList()
  }
}

// ---- 生命周期 ----
onMounted(() => {
  void loadOrder()
  tickTimer = setInterval(() => {
    nowMs.value = Date.now()
  }, 1000)
})

onUnmounted(() => {
  if (tickTimer !== null) {
    clearInterval(tickTimer)
    tickTimer = null
  }
})

// 同组件复用（订单间跳转）时重载
watch(
  () => route.params.orderId,
  (id, prev) => {
    if (id && id !== prev) {
      void loadOrder()
    }
  },
)
</script>

<template>
  <div class="initiate-page">
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
      <div class="nav-title">收银台</div>
    </header>

    <!-- 加载骨架 -->
    <main v-if="loading" class="body">
      <div class="skeleton-block sk-amount" />
      <div class="skeleton-block sk-section-title" />
      <div class="skeleton-block sk-method" />
      <div class="skeleton-block sk-method" />
      <div class="skeleton-block sk-method" />
    </main>

    <!-- 错误 / 不存在 / 已取消 -->
    <main v-else-if="loadError || notFound || orderCancelled || !order" class="body">
      <ErrorState
        :title="errorTitle"
        :description="errorDescription"
        :retry-text="errorRetryText"
        @retry="onErrorRetry"
      />
    </main>

    <!-- 收银台内容 -->
    <main v-else class="body">
      <!-- 金额卡片 -->
      <section class="amount-card" aria-label="应付金额">
        <div class="amount-card__label">应付金额</div>
        <div class="amount-card__value">
          <span class="symbol">¥</span><span>{{ formatPriceExact(payableAmount) }}</span>
        </div>
        <div class="amount-card__meta">
          <span>订单编号 {{ order.orderNo }}</span>
          <template v-if="countdownText">
            <span class="divider">|</span>
            <span class="countdown" :aria-label="`剩余支付时间 ${countdownText}`">
              剩余支付时间
              <span class="time" :class="{ danger: countdownDanger }">
                {{ payExpired ? '00:00' : countdownText }}
              </span>
            </span>
          </template>
        </div>
      </section>

      <!-- 支付方式 -->
      <h2 class="section-title">选择支付方式</h2>
      <div class="pay-methods" role="radiogroup" aria-label="支付方式">
        <div
          v-for="opt in channelOptions"
          :key="opt.channel"
          class="pay-item"
          :class="{ active: selectedChannel === opt.channel }"
          role="radio"
          :aria-checked="selectedChannel === opt.channel"
          tabindex="0"
          @click="selectedChannel = opt.channel"
          @keydown.enter="selectedChannel = opt.channel"
        >
          <div class="pay-item__icon">
            <!-- 支付宝 -->
            <svg v-if="opt.icon === 'alipay'" width="28" height="28" viewBox="0 0 28 28" fill="none">
              <rect width="28" height="28" rx="7" fill="#1677FF" />
              <path
                d="M20 17.5c-.8.4-1.7.6-2.6.8-.9.1-1.8.2-2.8.2-1.5 0-2.9-.2-4.1-.7-1.2-.4-2.1-1.1-2.8-2-.6-.9-1-2-1-3.3 0-1.2.3-2.3 1-3.2.6-.9 1.6-1.6 2.7-2.1 1.2-.5 2.5-.7 4-.7 1.2 0 2.3.2 3.2.5 1 .4 1.7.9 2.3 1.6.5.7.8 1.5.8 2.5 0 .7-.1 1.3-.4 1.9-.3.5-.6 1-1 1.3-.4.3-.9.5-1.3.5-.4 0-.7-.1-.9-.4-.2-.2-.3-.6-.2-1l.4-2.6c0-.5 0-.9-.2-1.2-.2-.3-.5-.4-1-.4-.5 0-.9.2-1.2.5-.3.4-.5.8-.6 1.4h2.3l-.2 1.2h-2.2l-.2 1.4h2.2l-.3 1.6c-.1.6 0 1 .3 1.3.2.3.6.4 1 .4.5 0 1-.2 1.4-.5.4-.3.8-.8 1-1.4.3-.6.4-1.3.4-2.1 0-.9-.2-1.7-.7-2.4-.5-.7-1.1-1.2-2-1.6-.9-.4-1.9-.6-3.1-.6-1.3 0-2.4.2-3.4.7-1 .4-1.7 1.1-2.3 1.9-.5.8-.8 1.7-.8 2.7 0 1 .3 1.9.8 2.7.5.8 1.3 1.4 2.3 1.8 1 .4 2.1.6 3.4.6.9 0 1.7-.1 2.5-.3.8-.2 1.5-.4 2.2-.8l.6 1.2Z"
                fill="#fff"
              />
            </svg>
            <!-- 微信支付 -->
            <svg v-else-if="opt.icon === 'wechat'" width="28" height="28" viewBox="0 0 28 28" fill="none">
              <rect width="28" height="28" rx="7" fill="#07C160" />
              <path
                d="M11.2 8.4c-3.2 0-5.8 2.1-5.8 4.7 0 1.5.9 2.8 2.2 3.7l-.5 1.6 1.9-1c.7.2 1.4.3 2.2.3.2 0 .4 0 .6 0-.1-.4-.2-.9-.2-1.3 0-2.4 2.3-4.4 5.2-4.4.2 0 .4 0 .6 0-.5-2-2.9-3.6-6.2-3.6Zm-2 1.9c.4 0 .7.3.7.7s-.3.7-.7.7-.7-.3-.7-.7.3-.7.7-.7Zm4.1 0c.4 0 .7.3.7.7s-.3.7-.7.7-.7-.3-.7-.7.3-.7.7-.7Z"
                fill="#fff"
              />
              <path
                d="M23 14.8c0-2.1-2.1-3.9-4.7-3.9s-4.7 1.7-4.7 3.9 2.1 3.9 4.7 3.9c.6 0 1.1-.1 1.6-.2l1.5.8-.4-1.3c1.2-.8 2-2 2-3.2Zm-6.3-.8c-.3 0-.6-.2-.6-.6 0-.3.2-.6.6-.6.3 0 .6.2.6.6 0 .3-.3.6-.6.6Zm3.3 0c-.3 0-.6-.2-.6-.6 0-.3.2-.6.6-.6.3 0 .6.2.6.6 0 .3-.3.6-.6.6Z"
                fill="#fff"
              />
            </svg>
            <!-- 银联云闪付 -->
            <svg v-else width="28" height="28" viewBox="0 0 28 28" fill="none">
              <rect width="28" height="28" rx="7" fill="#595959" />
              <rect x="6" y="8" width="16" height="12" rx="1.5" fill="#fff" />
              <rect x="6" y="10.5" width="16" height="2" fill="#595959" />
              <rect x="9" y="15.5" width="5" height="1.5" rx="0.5" fill="#595959" />
            </svg>
          </div>
          <div class="pay-item__body">
            <div class="pay-item__name">
              {{ opt.name }}
              <span v-if="opt.tag" class="pay-item__tag">{{ opt.tag }}</span>
            </div>
            <div class="pay-item__desc">{{ opt.description }}</div>
          </div>
          <div class="pay-item__radio">
            <svg width="12" height="12" viewBox="0 0 12 12" fill="none">
              <path
                d="M2.5 6.2L4.8 8.5L9.5 3.5"
                stroke="#fff"
                stroke-width="1.8"
                stroke-linecap="round"
                stroke-linejoin="round"
              />
            </svg>
          </div>
        </div>
      </div>

      <!-- 安全提示 -->
      <div class="notice-bar" role="note">
        <svg width="14" height="14" viewBox="0 0 14 14" fill="none">
          <path
            d="M7 1.5L2.5 3.5V7c0 2.9 2 5.3 4.5 6 2.5-.7 4.5-3.1 4.5-6V3.5L7 1.5Z"
            stroke="#52C41A"
            stroke-width="1.2"
            stroke-linejoin="round"
          />
          <path
            d="M5 7L6.5 8.5L9 5.5"
            stroke="#52C41A"
            stroke-width="1.2"
            stroke-linecap="round"
            stroke-linejoin="round"
          />
        </svg>
        <span>支付环境加密保护，请放心支付</span>
      </div>
    </main>

    <!-- 底部支付栏 -->
    <footer v-if="order && order.status === 'PendingPayment'" class="action-bar">
      <button
        class="pay-btn"
        :class="{ timeout: payExpired }"
        type="button"
        :disabled="paying"
        :aria-label="payExpired ? '订单已超时，返回订单' : `确认支付 ${formatPriceExact(payableAmount)} 元`"
        @click="onPay"
      >
        <template v-if="paying">正在支付...</template>
        <template v-else-if="payExpired">订单已超时 · 返回订单</template>
        <template v-else>
          确认支付 <span class="amount">¥{{ formatPriceExact(payableAmount) }}</span>
        </template>
      </button>
    </footer>

    <!-- 支付中 Loading 遮罩 -->
    <div v-if="paying" class="loading-mask" role="alert" aria-live="assertive">
      <div class="loading-box">
        <div class="loading-spinner" />
        <div class="loading-text">支付中</div>
        <div class="loading-sub">请稍候，正在处理</div>
      </div>
    </div>
  </div>
</template>

<style scoped>
.initiate-page {
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
.sk-amount {
  height: 148px;
  border-radius: var(--r-lg);
}

.sk-section-title {
  width: 96px;
  height: 16px;
  margin: var(--s4) 0 var(--s2);
  border-radius: var(--r-base);
}

.sk-method {
  height: 57px;
  border-radius: 0;
  margin-bottom: var(--s1);
}

.sk-method:first-of-type {
  border-radius: var(--r-lg) var(--r-lg) 0 0;
}

.sk-method:last-of-type {
  border-radius: 0 0 var(--r-lg) var(--r-lg);
  margin-bottom: 0;
}

/* 金额卡片 */
.amount-card {
  background: linear-gradient(135deg, #1677ff 0%, #0e5fcc 100%);
  border-radius: var(--r-lg);
  padding: var(--s6) var(--s4);
  color: #fff;
  text-align: center;
  margin-bottom: var(--s3);
  position: relative;
  overflow: hidden;
}

.amount-card::after {
  content: "";
  position: absolute;
  right: -40px;
  top: -40px;
  width: 140px;
  height: 140px;
  border-radius: 50%;
  background: rgba(255, 255, 255, 0.08);
}

.amount-card__label {
  font-size: var(--fs-sm);
  opacity: 0.85;
  margin-bottom: var(--s2);
  position: relative;
  z-index: 1;
}

.amount-card__value {
  font-size: var(--fs-3xl);
  font-weight: var(--fw-semibold);
  line-height: 1.2;
  display: flex;
  align-items: baseline;
  justify-content: center;
  gap: 2px;
  position: relative;
  z-index: 1;
  font-variant-numeric: tabular-nums;
}

.amount-card__value .symbol {
  font-size: var(--fs-xl);
  font-weight: var(--fw-medium);
}

.amount-card__meta {
  display: flex;
  align-items: center;
  justify-content: center;
  flex-wrap: wrap;
  gap: var(--s4);
  margin-top: var(--s3);
  font-size: var(--fs-sm);
  opacity: 0.9;
  position: relative;
  z-index: 1;
}

.amount-card__meta .divider {
  color: rgba(255, 255, 255, 0.4);
}

.countdown {
  display: inline-flex;
  align-items: center;
  gap: 4px;
}

.countdown .time {
  font-variant-numeric: tabular-nums;
  font-weight: var(--fw-semibold);
  color: #ffe08a;
}

.countdown .time.danger {
  color: var(--c-error);
}

/* 区块标题 */
.section-title {
  display: flex;
  align-items: center;
  gap: var(--s2);
  font-size: var(--fs-base);
  font-weight: var(--fw-medium);
  color: var(--n9);
  margin: var(--s4) 0 var(--s2);
  padding-left: var(--s1);
}

/* 支付方式列表 */
.pay-methods {
  background: var(--n1);
  border-radius: var(--r-lg);
  overflow: hidden;
  box-shadow: var(--sh-card);
}

.pay-item {
  display: flex;
  align-items: center;
  padding: var(--s3) var(--s4);
  cursor: pointer;
  border-bottom: 1px solid var(--n3);
  transition: background var(--d-fast) var(--ease-std);
}

.pay-item:last-child {
  border-bottom: none;
}

.pay-item:active {
  background: var(--n2);
}

.pay-item__icon {
  width: 28px;
  height: 28px;
  display: flex;
  align-items: center;
  justify-content: center;
  margin-right: var(--s3);
  flex-shrink: 0;
}

.pay-item__body {
  flex: 1;
  min-width: 0;
}

.pay-item__name {
  font-size: var(--fs-lg);
  font-weight: var(--fw-medium);
  color: var(--n10);
  display: flex;
  align-items: center;
  gap: var(--s2);
}

.pay-item__tag {
  font-size: 11px;
  color: var(--c-warning);
  background: rgba(250, 173, 20, 0.12);
  padding: 1px 6px;
  border-radius: var(--r-base);
  font-weight: var(--fw-medium);
}

.pay-item__desc {
  font-size: var(--fs-sm);
  color: var(--n7);
  margin-top: 2px;
}

.pay-item__radio {
  width: 20px;
  height: 20px;
  border-radius: 50%;
  border: 1.5px solid var(--n5);
  display: flex;
  align-items: center;
  justify-content: center;
  flex-shrink: 0;
  transition: all var(--d-fast) var(--ease-std);
}

.pay-item.active .pay-item__radio {
  border-color: var(--c-primary);
  background: var(--c-primary);
}

.pay-item__radio svg {
  opacity: 0;
  transition: opacity var(--d-fast);
}

.pay-item.active .pay-item__radio svg {
  opacity: 1;
}

/* 安全提示 */
.notice-bar {
  display: flex;
  align-items: center;
  gap: var(--s2);
  background: rgba(82, 196, 26, 0.08);
  border-radius: var(--r-base);
  padding: var(--s2) var(--s3);
  margin-top: var(--s3);
  font-size: var(--fs-sm);
  color: var(--c-success);
}

/* 底部支付栏 */
.action-bar {
  flex-shrink: 0;
  background: var(--n1);
  border-top: 1px solid var(--n3);
  padding: var(--s2) var(--s3);
  padding-bottom: calc(var(--s2) + env(safe-area-inset-bottom, 0px));
}

.pay-btn {
  width: 100%;
  height: 44px;
  background: var(--c-primary);
  color: #fff;
  border: none;
  border-radius: 22px;
  font-size: var(--fs-lg);
  font-weight: var(--fw-semibold);
  font-family: inherit;
  cursor: pointer;
  display: flex;
  align-items: center;
  justify-content: center;
  gap: 4px;
  transition: opacity var(--d-fast) var(--ease-std);
}

.pay-btn:active {
  opacity: 0.85;
}

.pay-btn:disabled {
  background: #9bc4ff;
  cursor: not-allowed;
}

.pay-btn .amount {
  font-variant-numeric: tabular-nums;
}

.pay-btn.timeout {
  background: var(--n1);
  color: var(--n10);
  border: 1px solid var(--n5);
}

/* Loading 遮罩 */
.loading-mask {
  position: fixed;
  inset: 0;
  background: rgba(0, 0, 0, 0.45);
  display: flex;
  align-items: center;
  justify-content: center;
  z-index: 200;
}

.loading-box {
  background: var(--n1);
  border-radius: var(--r-lg);
  padding: var(--s6) var(--s8);
  text-align: center;
  min-width: 140px;
}

.loading-spinner {
  width: 36px;
  height: 36px;
  border: 3px solid var(--n3);
  border-top-color: var(--c-primary);
  border-radius: 50%;
  margin: 0 auto var(--s3);
  animation: spin 0.8s linear infinite;
}

@keyframes spin {
  to {
    transform: rotate(360deg);
  }
}

.loading-text {
  font-size: var(--fs-base);
  color: var(--n10);
  font-weight: var(--fw-medium);
}

.loading-sub {
  font-size: var(--fs-sm);
  color: var(--n7);
  margin-top: var(--s1);
}
</style>
