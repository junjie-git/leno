<script setup lang="ts">
import { computed, onBeforeUnmount, onMounted, ref, watch } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import { showConfirmDialog, showFailToast, showToast } from 'vant'
import { orderApi } from '@/modules/06-order/api/order.api'
import type { OrderDto, OrderStatus } from '../types/order.dto'
import PriceText from '@/shared/components/PriceText.vue'
import ErrorState from '@/shared/components/ErrorState.vue'
import { formatDateTime, formatOrderNo, formatPriceExact } from '@/shared/utils/format'
import { logger } from '@/shared/utils/logger'

/**
 * 订单详情页（/order/:id）
 *
 * 结构（对齐设计稿 order-detail）：
 * NavBar（返回 / 订单详情 / 联系客服）→ 状态横幅（渐变底色 + 状态文案 + 副说明，待付款展示剩余时间倒计时）
 * → 收货地址卡 → 商品区（店铺头 + 商品行，行可点跳商品详情 + 售后/评价/查看商品操作）
 * → 订单金额（商品总额/优惠券/积分抵扣/运费/应付总额）
 * → 订单信息（订单编号可复制 / 下单/支付/发货/完成时间 / 物流公司+运单号 / 备注）
 * → 底部固定操作栏（客服/店铺图标 + 按状态渲染：取消/去支付/查看物流/确认收货/去评价/再次购买）
 *
 * 操作流：取消与确认收货均经 showConfirmDialog 二次确认；
 * 待付款倒计时基于 payDeadline 客户端计算，超时提示并支持刷新。
 */

const route = useRoute()
const router = useRouter()

// ---- 页面状态 ----
const loading = ref(true)
const loadError = ref(false)
const notFound = ref(false)
const order = ref<OrderDto | null>(null)
const submitting = ref(false)

// ---- 倒计时 ----
const countdownText = ref('')
let countdownTimer: ReturnType<typeof setInterval> | null = null

// ---- 状态文案与横幅配色 ----
const STATUS_TEXT: Record<OrderStatus, string> = {
  PendingPayment: '待付款',
  Paid: '待发货',
  Shipped: '待收货',
  Completed: '已完成',
  Cancelled: '已取消',
  Refunding: '退款中',
  Refunded: '已退款',
  AfterSales: '售后中',
}

/** 状态副说明（含倒计时/取消原因等动态文案） */
const statusDesc = computed(() => {
  const o = order.value
  if (!o) return ''
  switch (o.status) {
    case 'PendingPayment':
      return o.payDeadline && countdownText.value ? `请尽快完成支付，剩余 ${countdownText.value}` : '请尽快完成支付'
    case 'Paid':
      return '商家正在打包发货，请耐心等待'
    case 'Shipped':
      return '商品已发货，请留意物流动向'
    case 'Completed':
      return '交易完成，感谢您的信任与支持'
    case 'Cancelled':
      return o.cancelReason ? `取消原因：${o.cancelReason}` : '订单已取消'
    case 'Refunding':
      return '退款处理中，请耐心等待'
    case 'Refunded':
      return '退款已完成'
    case 'AfterSales':
      return '售后处理中，请关注处理进度'
    default:
      return ''
  }
})

/** 横幅渐变配色（对齐设计稿状态色） */
const bannerClass = computed(() => {
  const status = order.value?.status
  if (status === 'PendingPayment' || status === 'Refunding') return 'pending'
  if (status === 'Completed') return 'done'
  if (status === 'Cancelled' || status === 'Refunded') return 'cancel'
  return 'process'
})

/** 可申请售后的状态（已支付之后） */
const canAfterSales = computed(() => {
  const status = order.value?.status
  return status === 'Paid' || status === 'Shipped' || status === 'Completed'
})

/** 首个未评价订单行（已完成订单评价入口） */
const firstUnreviewedLine = computed(() => order.value?.items.find((item) => !item.reviewed) ?? null)

// ---- 数据加载 ----
async function loadDetail(): Promise<void> {
  stopCountdown()
  const id = String(route.params.id ?? '')
  loading.value = true
  loadError.value = false
  notFound.value = false
  try {
    order.value = await orderApi.getDetail(id)
    startCountdown()
  } catch (e) {
    logger.error('订单详情加载失败', e)
    if (e instanceof Error && e.message.includes('不存在')) {
      notFound.value = true
    } else {
      loadError.value = true
    }
  } finally {
    loading.value = false
  }
}

onMounted(() => {
  void loadDetail()
})

// 同组件复用（订单互跳）时重载
watch(
  () => route.params.id,
  (id, prev) => {
    if (id && id !== prev) {
      void loadDetail()
    }
  },
)

onBeforeUnmount(() => {
  stopCountdown()
})

// ---- 倒计时 ----
function formatCountdown(ms: number): string {
  const total = Math.floor(ms / 1000)
  const m = Math.floor(total / 60)
  const s = total % 60
  return `${String(m).padStart(2, '0')}:${String(s).padStart(2, '0')}`
}

function startCountdown(): void {
  stopCountdown()
  const o = order.value
  if (!o || o.status !== 'PendingPayment' || !o.payDeadline) return
  const deadline = new Date(o.payDeadline).getTime()
  if (Number.isNaN(deadline)) return
  countdownText.value = formatCountdown(deadline - Date.now())
  countdownTimer = setInterval(() => {
    const remain = deadline - Date.now()
    if (remain <= 0) {
      countdownText.value = '00:00'
      stopCountdown()
      showToast('订单已超时，请刷新查看最新状态')
      return
    }
    countdownText.value = formatCountdown(remain)
  }, 1000)
}

function stopCountdown(): void {
  if (countdownTimer) {
    clearInterval(countdownTimer)
    countdownTimer = null
  }
  countdownText.value = ''
}

// ---- 复制 ----
async function copyText(text: string, label: string): Promise<void> {
  try {
    await navigator.clipboard.writeText(text)
    showToast(`${label}已复制`)
  } catch {
    showToast('复制失败，请手动复制')
  }
}

// ---- 操作 ----
/** 取消订单（待付款，二次确认） */
async function cancelOrder(): Promise<void> {
  const o = order.value
  if (!o || submitting.value) return
  try {
    await showConfirmDialog({
      title: '确认取消',
      message: '取消后订单将关闭，如需购买请重新下单。此操作不可逆。',
      confirmButtonText: '确认取消',
      confirmButtonColor: '#FF4D4F',
      cancelButtonText: '再想想',
    })
  } catch {
    return
  }
  submitting.value = true
  try {
    await orderApi.cancel(o.id)
    showToast('订单已取消')
    await loadDetail()
  } catch (e) {
    logger.warn('取消订单失败', e)
    showFailToast(e instanceof Error ? e.message : '取消失败，请稍后重试')
  } finally {
    submitting.value = false
  }
}

/** 确认收货（待收货，二次确认） */
async function confirmReceive(): Promise<void> {
  const o = order.value
  if (!o || submitting.value) return
  try {
    await showConfirmDialog({
      title: '确认收货',
      message: '确认收货后交易完成，售后期开始计算。请确认您已收到商品。',
      confirmButtonText: '确认收货',
      confirmButtonColor: '#FF4D4F',
    })
  } catch {
    return
  }
  submitting.value = true
  try {
    order.value = await orderApi.confirm(o.id)
    stopCountdown()
    showToast('已确认收货')
  } catch (e) {
    logger.warn('确认收货失败', e)
    showFailToast(e instanceof Error ? e.message : '操作失败，请稍后重试')
  } finally {
    submitting.value = false
  }
}

// ---- 跳转 ----
function goBack(): void {
  if (window.history.length > 1) {
    router.back()
  } else {
    router.replace('/orders')
  }
}

function goOrders(): void {
  router.replace('/orders')
}

function goPay(): void {
  if (order.value) {
    router.push(`/payment/initiate/${order.value.id}`)
  }
}

function goLogistics(): void {
  if (order.value) {
    router.push(`/order/${order.value.id}/logistics`)
  }
}

function goShop(): void {
  if (order.value) {
    router.push(`/shop/${order.value.shopId}`)
  }
}

function goProduct(spuId: string): void {
  router.push(`/product/${spuId}`)
}

function goFirstProduct(): void {
  const spuId = order.value?.items[0]?.spuId
  if (spuId) {
    router.push(`/product/${spuId}`)
  }
}

function goAfterSales(): void {
  const lineId = order.value?.items[0]?.orderLineId
  if (lineId) {
    router.push(`/after-sales/apply/${lineId}`)
  }
}

function goReviewFirst(): void {
  const line = firstUnreviewedLine.value
  if (line) {
    router.push(`/review/submit/${line.orderLineId}`)
  }
}

function goRepurchase(): void {
  const spuId = order.value?.items[0]?.spuId
  if (spuId) {
    router.push(`/product/${spuId}`)
  }
}

function goService(): void {
  showToast('客服功能即将上线')
}
</script>

<template>
  <div class="detail-page">
    <!-- NavBar -->
    <header class="navbar">
      <button class="nav-back" type="button" aria-label="返回" @click="goBack">
        <svg width="20" height="20" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round">
          <path d="M15 18l-6-6 6-6" />
        </svg>
      </button>
      <div class="nav-title">订单详情</div>
      <button class="nav-right" type="button" aria-label="联系客服" @click="goService">
        <svg width="20" height="20" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round">
          <path d="M21 15a2 2 0 0 1-2 2H7l-4 4V5a2 2 0 0 1 2-2h14a2 2 0 0 1 2 2z" />
        </svg>
      </button>
    </header>

    <!-- 骨架屏 -->
    <main v-if="loading" class="body">
      <div class="skeleton-block sk-banner" />
      <div class="skeleton-block sk-address" />
      <div class="skeleton-block sk-goods" />
      <div class="skeleton-block sk-amount" />
    </main>

    <!-- 错误 / 不存在 -->
    <main v-else-if="loadError || notFound || !order" class="body">
      <ErrorState
        :title="notFound ? '订单不存在' : '订单详情加载失败'"
        :description="notFound ? '订单可能已被删除或不存在于当前账号' : '网络异常，请稍后重试'"
        :retry-text="notFound ? '返回订单列表' : '重新加载'"
        @retry="notFound ? goOrders() : loadDetail()"
      />
    </main>

    <!-- 内容 -->
    <main v-else class="body">
      <!-- 状态横幅 -->
      <section class="status-banner" :class="bannerClass">
        <svg class="status-icon" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round">
          <circle cx="12" cy="12" r="10" />
          <path d="M12 6v6l4 2" />
        </svg>
        <div class="status-text">{{ STATUS_TEXT[order.status] }}</div>
        <div class="status-desc">{{ statusDesc }}</div>
      </section>

      <!-- 收货地址 -->
      <section class="address-card">
        <svg class="addr-icon" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round">
          <path d="M21 10c0 7-9 13-9 13s-9-6-9-13a9 9 0 0 1 18 0z" />
          <circle cx="12" cy="10" r="3" />
        </svg>
        <div class="addr-content">
          <div class="addr-head">
            <span class="recipient">{{ order.address.receiver }}</span>
            <span class="phone">{{ order.address.phone }}</span>
          </div>
          <div class="addr-detail">{{ order.address.fullAddress }}</div>
        </div>
      </section>

      <!-- 商品区 -->
      <section class="section">
        <button class="section-title" type="button" @click="goShop">
          <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round">
            <path d="M3 9l1-5h16l1 5" />
            <path d="M4 9v11a1 1 0 0 0 1 1h14a1 1 0 0 0 1-1V9" />
          </svg>
          {{ order.shopName }}
          <svg class="title-arrow" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round">
            <path d="M9 6l6 6-6 6" />
          </svg>
        </button>
        <div
          v-for="item in order.items"
          :key="item.orderLineId"
          class="goods-item"
          role="button"
          aria-label="查看商品详情"
          @click="goProduct(item.spuId)"
        >
          <img class="goods-img" :src="item.image" :alt="item.name" loading="lazy">
          <div class="goods-info">
            <div class="goods-title">{{ item.name }}</div>
            <div class="goods-spec">{{ item.specs }}</div>
            <div class="goods-bottom">
              <span class="goods-price">¥{{ formatPriceExact(item.price) }}</span>
              <span class="goods-qty">x{{ item.quantity }}</span>
            </div>
          </div>
        </div>
        <div class="goods-action">
          <button v-if="canAfterSales" class="mini-btn" type="button" @click="goAfterSales">申请售后</button>
          <button v-if="order.status === 'Completed' && firstUnreviewedLine" class="mini-btn" type="button" @click="goReviewFirst">
            去评价
          </button>
          <button class="mini-btn" type="button" @click="goFirstProduct">查看商品</button>
        </div>
      </section>

      <!-- 订单金额 -->
      <section class="section">
        <div class="section-title">订单金额</div>
        <div class="amount-list">
          <div class="amount-row">
            <span>商品总额</span>
            <span class="val">¥{{ formatPriceExact(order.amounts.goodsAmount) }}</span>
          </div>
          <div v-if="order.amounts.couponDiscount > 0" class="amount-row discount">
            <span>优惠券抵扣</span>
            <span class="val">-¥{{ formatPriceExact(order.amounts.couponDiscount) }}</span>
          </div>
          <div v-if="order.amounts.pointsDiscount > 0" class="amount-row discount">
            <span>积分抵扣</span>
            <span class="val">-¥{{ formatPriceExact(order.amounts.pointsDiscount) }}</span>
          </div>
          <div class="amount-row">
            <span>运费</span>
            <span class="val">¥{{ formatPriceExact(order.amounts.freight) }}</span>
          </div>
          <div class="amount-row total">
            <span class="label">应付总额</span>
            <PriceText :amount="order.amounts.payableAmount" :size="16" />
          </div>
        </div>
      </section>

      <!-- 订单信息 -->
      <section class="section">
        <div class="section-title">订单信息</div>
        <div class="info-list">
          <div class="info-row">
            <span class="info-label">订单编号</span>
            <span class="info-val mono">
              {{ formatOrderNo(order.orderNo) }}
              <button class="copy-btn" type="button" @click="copyText(order.orderNo, '订单号')">
                <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round">
                  <rect x="9" y="9" width="13" height="13" rx="2" ry="2" />
                  <path d="M5 15H4a2 2 0 0 1-2-2V4a2 2 0 0 1 2-2h9a2 2 0 0 1 2 2v1" />
                </svg>
                复制
              </button>
            </span>
          </div>
          <div class="info-row">
            <span class="info-label">下单时间</span>
            <span class="info-val">{{ formatDateTime(order.createdAt) }}</span>
          </div>
          <div v-if="order.payDeadline && order.status === 'PendingPayment'" class="info-row">
            <span class="info-label">支付截止</span>
            <span class="info-val">{{ formatDateTime(order.payDeadline) }}</span>
          </div>
          <div v-if="order.paidAt" class="info-row">
            <span class="info-label">支付时间</span>
            <span class="info-val">{{ formatDateTime(order.paidAt) }}</span>
          </div>
          <div v-if="order.shippedAt" class="info-row">
            <span class="info-label">发货时间</span>
            <span class="info-val">{{ formatDateTime(order.shippedAt) }}</span>
          </div>
          <div v-if="order.completedAt" class="info-row">
            <span class="info-label">完成时间</span>
            <span class="info-val">{{ formatDateTime(order.completedAt) }}</span>
          </div>
          <div v-if="order.cancelledAt" class="info-row">
            <span class="info-label">取消时间</span>
            <span class="info-val">{{ formatDateTime(order.cancelledAt) }}</span>
          </div>
          <div v-if="order.logisticsCompany" class="info-row">
            <span class="info-label">物流公司</span>
            <span class="info-val">{{ order.logisticsCompany }}</span>
          </div>
          <div v-if="order.logisticsNo" class="info-row">
            <span class="info-label">物流单号</span>
            <span class="info-val mono">{{ order.logisticsNo }}</span>
          </div>
          <div v-if="order.remark" class="info-row">
            <span class="info-label">订单备注</span>
            <span class="info-val">{{ order.remark }}</span>
          </div>
        </div>
      </section>
    </main>

    <!-- 底部操作栏 -->
    <footer v-if="order" class="action-bar">
      <button class="act-icon" type="button" aria-label="联系客服" @click="goService">
        <span class="ic">
          <svg width="20" height="20" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.8" stroke-linecap="round" stroke-linejoin="round">
            <path d="M21 15a2 2 0 0 1-2 2H7l-4 4V5a2 2 0 0 1 2-2h14a2 2 0 0 1 2 2z" />
          </svg>
        </span>
        <span>客服</span>
      </button>
      <button class="act-icon" type="button" aria-label="进入店铺" @click="goShop">
        <span class="ic">
          <svg width="20" height="20" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.8" stroke-linecap="round" stroke-linejoin="round">
            <path d="M3 9l1-5h16l1 5" />
            <path d="M4 9v11a1 1 0 0 0 1 1h14a1 1 0 0 0 1-1V9" />
            <path d="M9 13h6" />
          </svg>
        </span>
        <span>店铺</span>
      </button>
      <div class="action-spacer" />
      <template v-if="order.status === 'PendingPayment'">
        <button class="bar-btn" type="button" :disabled="submitting" @click="cancelOrder">取消订单</button>
        <button class="bar-btn bar-btn-primary" type="button" :disabled="submitting" @click="goPay">去支付</button>
      </template>
      <template v-else-if="order.status === 'Shipped'">
        <button class="bar-btn" type="button" @click="goLogistics">查看物流</button>
        <button class="bar-btn bar-btn-primary" type="button" :disabled="submitting" @click="confirmReceive">确认收货</button>
      </template>
      <template v-else-if="order.status === 'Completed'">
        <button v-if="firstUnreviewedLine" class="bar-btn" type="button" @click="goReviewFirst">去评价</button>
        <button class="bar-btn bar-btn-primary" type="button" @click="goRepurchase">再次购买</button>
      </template>
    </footer>
  </div>
</template>

<style scoped>
.detail-page {
  height: 100%;
  display: flex;
  flex-direction: column;
  background: var(--n3);
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

.nav-back,
.nav-right {
  display: flex;
  align-items: center;
  justify-content: center;
  color: var(--n10);
  background: none;
  border: none;
  padding: 0;
  width: 32px;
  height: 32px;
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
}

/* 滚动主体 */
.body {
  flex: 1;
  overflow-y: auto;
  background: var(--n3);
  padding-bottom: calc(var(--s6) + env(safe-area-inset-bottom));
}

/* 骨架屏 */
.sk-banner {
  height: 96px;
  margin: var(--s3);
  border-radius: var(--r-lg);
}

.sk-address {
  height: 84px;
  margin: 0 var(--s3) var(--s3);
  border-radius: var(--r-lg);
}

.sk-goods {
  height: 240px;
  margin: 0 var(--s3) var(--s3);
  border-radius: var(--r-lg);
}

.sk-amount {
  height: 180px;
  margin: 0 var(--s3) var(--s3);
  border-radius: var(--r-lg);
}

/* 状态横幅 */
.status-banner {
  padding: var(--s6) var(--s4);
  color: #fff;
  position: relative;
  overflow: hidden;
}

.status-banner.pending {
  background: linear-gradient(135deg, #faad14 0%, #ffc53d 100%);
}

.status-banner.process {
  background: linear-gradient(135deg, #1677ff 0%, #4096ff 100%);
}

.status-banner.done {
  background: linear-gradient(135deg, #52c41a 0%, #73d13d 100%);
}

.status-banner.cancel {
  background: linear-gradient(135deg, #8c8c8c 0%, #bfbfbf 100%);
}

.status-banner .status-icon {
  width: 32px;
  height: 32px;
  position: absolute;
  right: var(--s4);
  top: var(--s4);
  opacity: 0.9;
}

.status-banner .status-text {
  font-size: var(--fs-xl);
  font-weight: var(--fw-semibold);
  margin-bottom: 4px;
}

.status-banner .status-desc {
  font-size: var(--fs-sm);
  opacity: 0.95;
  line-height: 1.6;
}

/* 收货地址卡 */
.address-card {
  margin: var(--s3);
  background: var(--n1);
  border-radius: var(--r-lg);
  box-shadow: var(--sh-card);
  padding: var(--s3);
  position: relative;
  overflow: hidden;
  display: flex;
  align-items: flex-start;
  gap: var(--s2);
}

.address-card::before {
  content: "";
  position: absolute;
  left: 0;
  top: 0;
  bottom: 0;
  width: 3px;
  background: var(--c-primary);
}

.addr-icon {
  width: 20px;
  height: 20px;
  color: var(--c-primary);
  flex-shrink: 0;
  margin-top: 2px;
}

.addr-content {
  flex: 1;
  min-width: 0;
}

.addr-head {
  display: flex;
  align-items: center;
  gap: var(--s2);
  margin-bottom: var(--s1);
}

.recipient {
  font-size: var(--fs-base);
  font-weight: var(--fw-semibold);
  color: var(--n10);
}

.phone {
  font-size: var(--fs-base);
  color: var(--n9);
}

.addr-detail {
  font-size: var(--fs-sm);
  color: var(--n9);
  line-height: 1.6;
}

/* 卡片区块 */
.section {
  margin: var(--s3);
  background: var(--n1);
  border-radius: var(--r-lg);
  box-shadow: var(--sh-card);
  overflow: hidden;
}

.section-title {
  width: 100%;
  padding: var(--s3);
  font-size: var(--fs-base);
  font-weight: var(--fw-medium);
  color: var(--n10);
  display: flex;
  align-items: center;
  gap: 6px;
  cursor: pointer;
  font-family: inherit;
  text-align: left;
  background: var(--n1);
  border: none;
}

.section-title svg {
  width: 16px;
  height: 16px;
  color: var(--c-primary);
  flex-shrink: 0;
}

.section-title .title-arrow {
  width: 14px;
  height: 14px;
  color: var(--n7);
  margin-left: auto;
}

/* 商品行 */
.goods-item {
  display: flex;
  gap: var(--s2);
  padding: var(--s2) var(--s3);
  cursor: pointer;
  border-top: 1px solid var(--n3);
}

.goods-img {
  width: 72px;
  height: 72px;
  border-radius: var(--r-base);
  background: var(--n3);
  flex-shrink: 0;
  object-fit: cover;
}

.goods-info {
  flex: 1;
  min-width: 0;
  display: flex;
  flex-direction: column;
  justify-content: space-between;
}

.goods-title {
  font-size: var(--fs-base);
  color: var(--n10);
  line-height: 1.4;
  display: -webkit-box;
  -webkit-line-clamp: 2;
  -webkit-box-orient: vertical;
  overflow: hidden;
}

.goods-spec {
  font-size: var(--fs-sm);
  color: var(--n7);
  background: var(--n3);
  padding: 2px 6px;
  border-radius: var(--r-base);
  align-self: flex-start;
  max-width: 100%;
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
}

.goods-bottom {
  display: flex;
  justify-content: space-between;
  align-items: flex-end;
}

.goods-price {
  font-size: var(--fs-base);
  color: var(--n10);
}

.goods-qty {
  font-size: var(--fs-sm);
  color: var(--n7);
}

.goods-action {
  display: flex;
  gap: var(--s2);
  justify-content: flex-end;
  padding: var(--s2) var(--s3) var(--s3);
  border-top: 1px solid var(--n3);
}

.mini-btn {
  height: 26px;
  padding: 0 10px;
  border-radius: var(--r-base);
  font-size: var(--fs-sm);
  border: 1px solid var(--n5);
  background: var(--n1);
  color: var(--n9);
  cursor: pointer;
  font-family: inherit;
}

/* 金额明细 */
.amount-list {
  padding: var(--s2) var(--s3) var(--s3);
}

.amount-row {
  display: flex;
  justify-content: space-between;
  align-items: center;
  padding: 8px 0;
  font-size: var(--fs-base);
  color: var(--n9);
}

.amount-row .val {
  color: var(--n10);
}

.amount-row.discount .val {
  color: var(--c-success);
}

.amount-row.total {
  border-top: 1px dashed var(--n5);
  margin-top: var(--s2);
  padding-top: var(--s3);
}

.amount-row.total .label {
  color: var(--n10);
  font-weight: var(--fw-medium);
}

/* 订单信息 */
.info-list {
  padding: var(--s2) var(--s3) var(--s3);
}

.info-row {
  display: flex;
  justify-content: space-between;
  align-items: center;
  padding: 8px 0;
  font-size: var(--fs-sm);
  color: var(--n9);
}

.info-row .info-label {
  color: var(--n7);
  flex-shrink: 0;
}

.info-row .info-val {
  color: var(--n9);
  text-align: right;
  display: flex;
  align-items: center;
  gap: var(--s1);
  min-width: 0;
  word-break: break-all;
}

.info-row .info-val.mono {
  font-family: var(--ff-mono);
}

.copy-btn {
  color: var(--c-primary);
  cursor: pointer;
  display: inline-flex;
  align-items: center;
  gap: 2px;
  background: none;
  border: none;
  padding: 0;
  font-size: var(--fs-sm);
  font-family: inherit;
  flex-shrink: 0;
}

.copy-btn svg {
  width: 12px;
  height: 12px;
}

/* 底部操作栏 */
.action-bar {
  height: 50px;
  background: var(--n1);
  border-top: 1px solid var(--n3);
  flex-shrink: 0;
  display: flex;
  align-items: center;
  padding: 0 var(--s3);
  gap: var(--s3);
  padding-bottom: env(safe-area-inset-bottom);
}

.act-icon {
  display: flex;
  flex-direction: column;
  align-items: center;
  justify-content: center;
  gap: 2px;
  color: var(--n9);
  font-size: 10px;
  cursor: pointer;
  width: 44px;
  flex-shrink: 0;
  background: none;
  border: none;
  font-family: inherit;
  padding: 0;
}

.act-icon .ic {
  display: flex;
  align-items: center;
  height: 20px;
}

.action-spacer {
  flex: 1;
}

.bar-btn {
  height: 36px;
  padding: 0 20px;
  border-radius: 18px;
  font-size: var(--fs-base);
  border: 1px solid var(--n5);
  background: var(--n1);
  color: var(--n9);
  cursor: pointer;
  display: inline-flex;
  align-items: center;
  justify-content: center;
  font-family: inherit;
  flex-shrink: 0;
}

.bar-btn:disabled {
  opacity: 0.6;
  pointer-events: none;
}

.bar-btn-primary {
  background: var(--c-primary);
  border-color: var(--c-primary);
  color: #fff;
  font-weight: var(--fw-medium);
}
</style>
