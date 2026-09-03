<script setup lang="ts">
import { computed, onBeforeUnmount, onMounted, ref, watch } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import { showConfirmDialog, showFailToast, showImagePreview, showToast } from 'vant'
import { afterSalesApi } from '@/modules/10-after-sales/api/afterSales.api'
import type { AfterSalesDto, AfterSalesStatus, AfterSalesType, RefundDto } from '../types/after-sales.dto'
import ErrorState from '@/shared/components/ErrorState.vue'
import PriceText from '@/shared/components/PriceText.vue'
import { formatDateTime, formatPriceExact } from '@/shared/utils/format'
import { logger } from '@/shared/utils/logger'

/**
 * 售后详情页（/after-sales/:id）
 *
 * 结构（对齐设计稿 after-sales-detail）：
 * NavBar（返回 / 售后详情）→ 状态头（渐变底色 + 状态文案 + 说明 + 退款金额）
 * → 售后进度时间轴（申请提交 → 卖家审核 → 买家退货（退货退款）→ 退款 → 完成；驳回/撤销分支）
 * → 商品信息 → 申请信息（单号可复制 / 订单号 / 类型 / 原因 / 金额 / 描述 / 时间）
 * → 凭证图片（点击全屏预览）→ 退款信息（渠道 / 金额 / 到账时间）→ 协商记录
 * → 底部固定操作栏（按状态：待审核「撤销申请」/ 待退货「填写物流」/ 已完成「查看订单」/ 已驳回「重新申请」+ 联系客服）
 *
 * 数据流：GET /after-sales/mine 按 id 定位售后单；退款中/已完成时 GET /refunds/{id} 拉取退款进度，
 * 退款中每 5s 轮询一次（最多 12 次），到账后本地推进状态为已完成。
 */

const route = useRoute()
const router = useRouter()

/** 退款轮询参数（对齐设计稿：间隔 5s，最多 12 次） */
const REFUND_POLL_INTERVAL_MS = 5000
const REFUND_POLL_MAX_COUNT = 12

/** 售后状态 → 状态头文案与说明（对齐设计稿状态色） */
const STATUS_META: Record<AfterSalesStatus, { label: string; desc: string; cls: string }> = {
  PendingReview: {
    label: '待卖家审核',
    desc: '卖家将在 48 小时内处理，超时未处理系统将自动同意退款',
    cls: 'hd-pending',
  },
  Approved: {
    label: '卖家已同意，待退货',
    desc: '请在 7 天内寄回商品并填写退货物流单号',
    cls: 'hd-process',
  },
  Returning: {
    label: '退货运输中',
    desc: '卖家确认收到退货后将为您发起退款',
    cls: 'hd-process',
  },
  Refunding: {
    label: '退款中',
    desc: '退款将按原支付渠道退回，预计 1-3 个工作日到账',
    cls: 'hd-process',
  },
  Completed: {
    label: '退款已完成',
    desc: '退款已原路退回，请注意查收',
    cls: 'hd-done',
  },
  Cancelled: {
    label: '售后已撤销',
    desc: '您已撤销该售后申请，如需售后可重新提交',
    cls: 'hd-cancel',
  },
  Rejected: {
    label: '卖家已驳回',
    desc: '如对驳回结果有异议，可联系客服协商处理',
    cls: 'hd-reject',
  },
}

/** 售后类型 → 标签文案与配色 */
const TYPE_META: Record<AfterSalesType, { label: string; cls: string }> = {
  RefundOnly: { label: '仅退款', cls: 'tag-refund' },
  ReturnRefund: { label: '退货退款', cls: 'tag-return' },
  Exchange: { label: '换货', cls: 'tag-return' },
}

/** 退款单状态文案 */
const REFUND_STATUS_TEXT: Record<RefundDto['status'], string> = {
  Processing: '退款处理中',
  Success: '已到账',
  Failed: '退款失败',
}

/** 时间轴节点 */
interface TimelineNode {
  title: string
  time?: string
  desc?: string
  state: 'done' | 'active' | 'pending' | 'error' | 'muted'
}

/** 协商记录条目 */
interface NegoItem {
  role: 'buyer' | 'seller' | 'platform'
  name: string
  time: string
  text: string
}

// ---- 页面状态 ----
const loading = ref(true)
const loadError = ref(false)
const notFound = ref(false)
const detail = ref<AfterSalesDto | null>(null)
const refund = ref<RefundDto | null>(null)
const submitting = ref(false)

// ---- 退货物流弹层 ----
const returnVisible = ref(false)
const returnCompany = ref('')
const returnLogisticsNo = ref('')
const returnSubmitting = ref(false)

// ---- 退款轮询 ----
let refundPollTimer: ReturnType<typeof setInterval> | null = null
let refundPollCount = 0

onMounted(() => {
  void loadDetail()
})

watch(
  () => route.params.id,
  (id, prev) => {
    if (id && id !== prev) {
      void loadDetail()
    }
  },
)

onBeforeUnmount(() => {
  stopRefundPolling()
})

// ---- 计算属性 ----
function statusMeta(): { label: string; desc: string; cls: string } {
  const a = detail.value
  return a ? STATUS_META[a.status] : { label: '', desc: '', cls: '' }
}

function typeMeta(): { label: string; cls: string } {
  const a = detail.value
  return a ? TYPE_META[a.type] : { label: '', cls: '' }
}

/** 售后进度时间轴（按类型与状态推导，含驳回/撤销分支） */
const timeline = computed<TimelineNode[]>(() => {
  const a = detail.value
  if (!a) {
    return []
  }
  const nodes: TimelineNode[] = [
    {
      title: '申请提交',
      time: formatDateTime(a.applyAt),
      desc: `您已提交${TYPE_META[a.type].label}申请，等待卖家处理`,
      state: 'done',
    },
  ]
  if (a.status === 'Cancelled') {
    nodes.push({ title: '申请已撤销', desc: '您已撤销该售后申请', state: 'muted' })
    return nodes
  }
  if (a.status === 'PendingReview') {
    nodes.push({
      title: '卖家审核中',
      desc: '卖家将在 48 小时内处理，超时未处理系统将自动同意',
      state: 'active',
    })
  } else if (a.status === 'Rejected') {
    nodes.push({
      title: '卖家驳回',
      time: a.handleAt ? formatDateTime(a.handleAt) : undefined,
      desc: a.rejectReason ? `驳回原因：${a.rejectReason}` : '卖家驳回了您的售后申请',
      state: 'error',
    })
    return nodes
  } else {
    nodes.push({
      title: '卖家审核',
      time: a.handleAt ? formatDateTime(a.handleAt) : undefined,
      desc: a.type === 'ReturnRefund' ? '卖家已同意退货申请' : '卖家已同意退款申请',
      state: 'done',
    })
  }
  if (a.type === 'ReturnRefund') {
    if (a.status === 'Approved') {
      nodes.push({ title: '待买家退货', desc: '请在 7 天内寄回商品并填写物流单号', state: 'active' })
    } else if (a.returnLogistics) {
      nodes.push({
        title: '买家退货',
        time: formatDateTime(a.returnLogistics.shippedAt),
        desc: `${a.returnLogistics.company} ${a.returnLogistics.logisticsNo}`,
        state: 'done',
      })
    } else {
      nodes.push({ title: '买家退货', time: '待处理', state: 'pending' })
    }
  }
  if (a.status === 'Refunding') {
    nodes.push({ title: '退款中', desc: '退款将按原支付渠道退回，请耐心等待', state: 'active' })
    nodes.push({ title: '退款完成', time: '待处理', state: 'pending' })
  } else if (a.status === 'Completed') {
    nodes.push({
      title: '退款中',
      desc: refund.value ? `退款渠道：${refund.value.channel}` : '退款已发起',
      state: 'done',
    })
    nodes.push({
      title: '退款完成',
      time: refund.value?.refundedAt ? formatDateTime(refund.value.refundedAt) : undefined,
      desc: '退款已原路退回，请注意查收',
      state: 'done',
    })
  } else {
    nodes.push({ title: '退款中', time: '待处理', state: 'pending' })
    nodes.push({ title: '退款完成', time: '待处理', state: 'pending' })
  }
  return nodes
})

/** 协商记录（由售后单事件按时间顺序推导） */
const negotiation = computed<NegoItem[]>(() => {
  const a = detail.value
  if (!a) {
    return []
  }
  const items: NegoItem[] = []
  items.push({
    role: 'buyer',
    name: '买家',
    time: formatDateTime(a.applyAt),
    text: a.description || `发起${TYPE_META[a.type].label}申请：${a.reason}`,
  })
  items.push({
    role: 'platform',
    name: '平台客服',
    time: formatDateTime(a.applyAt),
    text: '您的售后申请已提交，已通知卖家在 48 小时内处理。如超时未处理，系统将自动同意退款。',
  })
  if (a.status === 'Rejected') {
    items.push({
      role: 'seller',
      name: '卖家',
      time: formatDateTime(a.handleAt ?? a.applyAt),
      text: a.rejectReason
        ? `很抱歉给您带来不便，驳回原因：${a.rejectReason}`
        : '卖家驳回了您的售后申请',
    })
  } else if (['Approved', 'Returning', 'Refunding', 'Completed'].includes(a.status)) {
    items.push({
      role: 'seller',
      name: '卖家',
      time: formatDateTime(a.handleAt ?? a.applyAt),
      text:
        a.type === 'ReturnRefund'
          ? '已同意您的退货退款申请，请尽快寄回商品并填写物流单号。'
          : '已同意您的退款申请，退款将按原支付渠道退回。',
    })
  }
  if (a.returnLogistics) {
    items.push({
      role: 'buyer',
      name: '买家',
      time: formatDateTime(a.returnLogistics.shippedAt),
      text: `已寄回商品：${a.returnLogistics.company}，物流单号 ${a.returnLogistics.logisticsNo}`,
    })
  }
  if (a.status === 'Completed') {
    items.push({
      role: 'platform',
      name: '平台客服',
      time: formatDateTime(refund.value?.refundedAt ?? a.applyAt),
      text: `退款 ¥${formatPriceExact(a.refundAmount)} 已原路退回（${
        refund.value?.channel ?? '原支付渠道'
      }），请查收。`,
    })
  }
  return items
})

/** 是否展示退款信息卡（退款中 / 已完成） */
const showRefundCard = computed(() => {
  const a = detail.value
  return !!a && (a.status === 'Refunding' || a.status === 'Completed') && !!refund.value
})

// ---- 数据加载 ----
async function loadDetail(): Promise<void> {
  stopRefundPolling()
  const id = String(route.params.id ?? '')
  loading.value = true
  loadError.value = false
  notFound.value = false
  refund.value = null
  try {
    const list = await afterSalesApi.listMine()
    const found = list.find((item) => item.id === id)
    if (!found) {
      notFound.value = true
      return
    }
    detail.value = found
    if (found.status === 'Refunding' || found.status === 'Completed') {
      try {
        refund.value = await afterSalesApi.getRefund(found.id)
      } catch (e) {
        logger.warn('退款信息加载失败（忽略）', e)
      }
    }
    if (found.status === 'Refunding') {
      startRefundPolling()
    }
  } catch (e) {
    logger.error('售后详情加载失败', e)
    loadError.value = true
  } finally {
    loading.value = false
  }
}

// ---- 退款轮询 ----
function stopRefundPolling(): void {
  if (refundPollTimer) {
    clearInterval(refundPollTimer)
    refundPollTimer = null
  }
  refundPollCount = 0
}

function startRefundPolling(): void {
  stopRefundPolling()
  refundPollTimer = setInterval(() => {
    refundPollCount += 1
    if (refundPollCount > REFUND_POLL_MAX_COUNT) {
      stopRefundPolling()
      return
    }
    void pollRefundOnce()
  }, REFUND_POLL_INTERVAL_MS)
}

async function pollRefundOnce(): Promise<void> {
  const a = detail.value
  if (!a || a.status !== 'Refunding') {
    stopRefundPolling()
    return
  }
  try {
    const r = await afterSalesApi.getRefund(a.id)
    refund.value = r
    if (r.status === 'Success') {
      detail.value = { ...a, status: 'Completed' }
      stopRefundPolling()
      showToast('退款已到账')
    }
  } catch (e) {
    logger.warn('退款进度轮询失败（忽略，下轮重试）', e)
  }
}

// ---- 凭证预览 ----
function previewEvidence(index: number): void {
  const images = detail.value?.images ?? []
  if (images.length === 0) {
    return
  }
  showImagePreview({ images, startPosition: index })
}

// ---- 复制 ----
async function copyAfterSalesNo(): Promise<void> {
  const a = detail.value
  if (!a) {
    return
  }
  try {
    await navigator.clipboard.writeText(a.id)
    showToast('售后单号已复制')
  } catch {
    showToast('复制失败，请手动复制')
  }
}

// ---- 操作 ----
/** 撤销售后（待审核，二次确认） */
async function cancelAfterSales(): Promise<void> {
  const a = detail.value
  if (!a || submitting.value) {
    return
  }
  try {
    await showConfirmDialog({
      title: '确认撤销',
      message: '撤销后无法恢复，确认撤销该售后申请吗？',
      confirmButtonText: '确认撤销',
      confirmButtonColor: '#FF4D4F',
      cancelButtonText: '再想想',
    })
  } catch {
    return
  }
  submitting.value = true
  try {
    await afterSalesApi.cancel(a.id)
    showToast('撤销成功')
    await loadDetail()
  } catch (e) {
    logger.warn('撤销售后失败', e)
    showFailToast(e instanceof Error ? e.message : '撤销失败，请稍后重试')
  } finally {
    submitting.value = false
  }
}

/** 提交退货物流（待退货） */
async function submitReturnLogistics(): Promise<void> {
  const a = detail.value
  if (!a || returnSubmitting.value) {
    return
  }
  const company = returnCompany.value.trim()
  const logisticsNo = returnLogisticsNo.value.trim()
  if (!company) {
    showToast('请填写快递公司')
    return
  }
  if (!logisticsNo) {
    showToast('请填写物流单号')
    return
  }
  returnSubmitting.value = true
  try {
    await afterSalesApi.submitReturnLogistics(a.id, { company, logisticsNo })
    returnVisible.value = false
    showToast('提交成功，等待卖家确认收货')
    await loadDetail()
  } catch (e) {
    logger.warn('提交退货物流失败', e)
    showFailToast(e instanceof Error ? e.message : '提交失败，请稍后重试')
  } finally {
    returnSubmitting.value = false
  }
}

// ---- 跳转 ----
function goBack(): void {
  if (window.history.length > 1) {
    router.back()
  } else {
    router.replace('/after-sales/mine')
  }
}

function goMine(): void {
  router.replace('/after-sales/mine')
}

function goOrder(): void {
  if (detail.value) {
    router.push(`/order/${detail.value.orderId}`)
  }
}

function goReApply(): void {
  if (detail.value) {
    router.push(`/after-sales/apply/${detail.value.orderLineId}`)
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
      <div class="nav-title">售后详情</div>
    </header>

    <!-- 骨架屏 -->
    <main v-if="loading" class="body">
      <div class="skeleton-block sk-header" />
      <div class="skeleton-block sk-card" />
      <div class="skeleton-block sk-card" />
      <div class="skeleton-block sk-card tall" />
    </main>

    <!-- 加载失败 / 售后单不存在 -->
    <main v-else-if="loadError || notFound || !detail" class="body">
      <ErrorState
        :title="notFound ? '售后单不存在' : '售后详情加载失败'"
        :description="notFound ? '该售后单可能已被删除，或您无权查看' : '网络异常，请稍后重试'"
        :retry-text="notFound ? '返回列表' : '重新加载'"
        @retry="notFound ? goMine() : loadDetail()"
      />
    </main>

    <!-- 详情内容 -->
    <main v-else class="body">
      <!-- 状态头 -->
      <section class="status-header" :class="statusMeta().cls">
        <div class="status-title">
          <svg width="22" height="22" viewBox="0 0 24 24" fill="currentColor">
            <path d="M12 2C6.5 2 2 6.5 2 12s4.5 10 10 10 10-4.5 10-10S17.5 2 12 2zm0 18c-4.4 0-8-3.6-8-8s3.6-8 8-8 8 3.6 8 8-3.6 8-8 8zm-1-13h2v6h-2zm0 8h2v2h-2z" />
          </svg>
          {{ statusMeta().label }}
        </div>
        <div class="status-desc">{{ statusMeta().desc }}</div>
        <div class="status-amount">
          <span class="amount-label">退款金额</span>
          <span class="amount-value">¥{{ formatPriceExact(detail.refundAmount) }}</span>
        </div>
      </section>

      <!-- 售后进度 -->
      <section class="card">
        <div class="card-title">售后进度</div>
        <div class="steps" role="list" aria-label="售后进度时间轴">
          <div v-for="(node, index) in timeline" :key="index" class="step" role="listitem">
            <span class="step-line" :class="{ active: index < timeline.length - 1 && timeline[index + 1].state !== 'pending' }" />
            <span class="step-dot" :class="node.state">
              <svg v-if="node.state === 'done'" width="12" height="12" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="3" stroke-linecap="round" stroke-linejoin="round">
                <path d="M5 12l5 5L20 7" />
              </svg>
              <svg v-else-if="node.state === 'error'" width="12" height="12" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="3" stroke-linecap="round">
                <path d="M6 6l12 12M18 6L6 18" />
              </svg>
            </span>
            <span class="step-content">
              <span class="step-title" :class="node.state">{{ node.title }}</span>
              <span v-if="node.time" class="step-time">{{ node.time }}</span>
              <span v-if="node.desc" class="step-desc">{{ node.desc }}</span>
            </span>
          </div>
        </div>
      </section>

      <!-- 商品信息 -->
      <section class="card">
        <div class="card-title">商品信息</div>
        <div class="product-row">
          <img class="product-img" :src="detail.image" :alt="detail.name" loading="lazy">
          <div class="product-info">
            <div class="product-name">{{ detail.name }}</div>
            <div class="product-bottom">
              <span class="product-sku">规格：{{ detail.specs }}</span>
              <span class="product-price">¥{{ formatPriceExact(detail.price) }} ×{{ detail.quantity }}</span>
            </div>
          </div>
        </div>
      </section>

      <!-- 申请信息 -->
      <section class="card">
        <div class="card-title">申请信息</div>
        <div class="info-row">
          <span class="info-label">售后单号</span>
          <button class="info-value copy" type="button" aria-label="复制售后单号" @click="copyAfterSalesNo">
            {{ detail.id }}
            <svg width="14" height="14" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2">
              <rect x="8" y="8" width="12" height="12" rx="2" />
              <path d="M16 8V6a2 2 0 0 0-2-2H6a2 2 0 0 0-2 2v8a2 2 0 0 0 2 2h2" />
            </svg>
          </button>
        </div>
        <div class="info-row">
          <span class="info-label">订单编号</span>
          <span class="info-value">{{ detail.orderNo }}</span>
        </div>
        <div class="info-row">
          <span class="info-label">售后类型</span>
          <span class="info-value"><span class="type-tag" :class="typeMeta().cls">{{ typeMeta().label }}</span></span>
        </div>
        <div class="info-row">
          <span class="info-label">申请原因</span>
          <span class="info-value">{{ detail.reason }}</span>
        </div>
        <div class="info-row">
          <span class="info-label">退款金额</span>
          <span class="info-value amount">
            <PriceText :amount="detail.refundAmount" :size="16" />
          </span>
        </div>
        <div v-if="detail.description" class="info-row">
          <span class="info-label">问题描述</span>
          <span class="info-value text">{{ detail.description }}</span>
        </div>
        <div class="info-row">
          <span class="info-label">申请时间</span>
          <span class="info-value">{{ formatDateTime(detail.applyAt) }}</span>
        </div>
        <div v-if="detail.returnLogistics" class="info-row">
          <span class="info-label">退货物流</span>
          <span class="info-value">{{ detail.returnLogistics.company }} {{ detail.returnLogistics.logisticsNo }}</span>
        </div>
      </section>

      <!-- 凭证图片 -->
      <section v-if="detail.images.length > 0" class="card">
        <div class="card-title">凭证图片</div>
        <div class="evidence-grid">
          <img
            v-for="(img, index) in detail.images"
            :key="index"
            class="evidence-item"
            :src="img"
            :alt="`凭证图片 ${index + 1}`"
            loading="lazy"
            @click="previewEvidence(index)"
          >
        </div>
      </section>

      <!-- 退款信息 -->
      <section v-if="showRefundCard && refund" class="card">
        <div class="card-title">退款信息</div>
        <div class="info-row">
          <span class="info-label">退款状态</span>
          <span class="info-value" :class="{ success: refund.status === 'Success', error: refund.status === 'Failed' }">
            {{ REFUND_STATUS_TEXT[refund.status] }}
          </span>
        </div>
        <div class="info-row">
          <span class="info-label">退款渠道</span>
          <span class="info-value">{{ refund.channel }}</span>
        </div>
        <div class="info-row">
          <span class="info-label">退款金额</span>
          <span class="info-value amount">
            <PriceText :amount="refund.amount" :size="16" />
          </span>
        </div>
        <div class="info-row">
          <span class="info-label">申请时间</span>
          <span class="info-value">{{ formatDateTime(refund.appliedAt) }}</span>
        </div>
        <div v-if="refund.refundedAt" class="info-row">
          <span class="info-label">到账时间</span>
          <span class="info-value">{{ formatDateTime(refund.refundedAt) }}</span>
        </div>
      </section>

      <!-- 协商记录 -->
      <section class="card">
        <div class="card-title">协商记录</div>
        <div class="negotiation">
          <div v-for="(item, index) in negotiation" :key="index" class="nego-item">
            <span class="nego-avatar" :class="item.role">{{ item.role === 'buyer' ? '我' : item.role === 'seller' ? '卖' : '客' }}</span>
            <div class="nego-body">
              <div class="nego-head">
                <span class="nego-name">{{ item.name }}</span>
                <span class="nego-role" :class="item.role">
                  {{ item.role === 'buyer' ? '本人' : item.role === 'seller' ? '卖家' : '系统' }}
                </span>
                <span class="nego-time">{{ item.time }}</span>
              </div>
              <p class="nego-bubble" :class="item.role">{{ item.text }}</p>
            </div>
          </div>
        </div>
      </section>
    </main>

    <!-- 底部操作栏 -->
    <footer v-if="detail" class="action-bar">
      <template v-if="detail.status === 'PendingReview'">
        <button class="action-btn danger" type="button" :disabled="submitting" @click="cancelAfterSales">
          {{ submitting ? '撤销中...' : '撤销申请' }}
        </button>
        <button class="action-btn outline" type="button" @click="goService">联系客服</button>
      </template>
      <template v-else-if="detail.status === 'Approved'">
        <button class="action-btn primary" type="button" @click="returnVisible = true">填写物流</button>
        <button class="action-btn outline" type="button" @click="goService">联系客服</button>
      </template>
      <template v-else-if="detail.status === 'Completed'">
        <button class="action-btn outline" type="button" @click="goService">联系客服</button>
        <button class="action-btn primary" type="button" @click="goOrder">查看订单</button>
      </template>
      <template v-else-if="detail.status === 'Rejected'">
        <button class="action-btn outline" type="button" @click="goService">联系客服</button>
        <button class="action-btn primary" type="button" @click="goReApply">重新申请</button>
      </template>
      <template v-else>
        <button class="action-btn outline" type="button" @click="goService">联系客服</button>
        <button class="action-btn primary" type="button" @click="goMine">我的售后</button>
      </template>
    </footer>

    <!-- 退货物流弹层 -->
    <van-popup
      v-model:show="returnVisible"
      position="bottom"
      round
      role="dialog"
      aria-label="填写退货物流"
    >
      <div class="return-panel">
        <div class="panel-head">
          <span class="panel-title">填写退货物流</span>
          <van-icon name="cross" size="18" color="#8C8C8C" @click="returnVisible = false" />
        </div>
        <div class="panel-body">
          <div class="panel-field">
            <label class="field-label" for="detail-return-company">快递公司</label>
            <input
              id="detail-return-company"
              v-model="returnCompany"
              class="field-input"
              type="text"
              maxlength="20"
              placeholder="如：顺丰速运"
            >
          </div>
          <div class="panel-field">
            <label class="field-label" for="detail-return-no">物流单号</label>
            <input
              id="detail-return-no"
              v-model="returnLogisticsNo"
              class="field-input"
              type="text"
              maxlength="32"
              placeholder="请输入退货物流单号"
            >
          </div>
          <p class="panel-tip">请先与卖家确认退货地址后再寄回商品，并如实填写物流信息</p>
        </div>
        <div class="panel-foot">
          <button
            class="panel-submit"
            :class="{ loading: returnSubmitting }"
            type="button"
            :disabled="returnSubmitting"
            @click="submitReturnLogistics"
          >
            {{ returnSubmitting ? '提交中...' : '提交物流信息' }}
          </button>
        </div>
      </div>
    </van-popup>
  </div>
</template>

<style scoped>
.detail-page {
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
  padding-bottom: calc(var(--s6) + env(safe-area-inset-bottom));
  display: flex;
  flex-direction: column;
  gap: var(--s3);
}

/* 骨架屏 */
.sk-header {
  height: 130px;
  border-radius: var(--r-lg);
}

.sk-card {
  height: 150px;
  border-radius: var(--r-lg);
}

.sk-card.tall {
  height: 200px;
}

/* 状态头 */
.status-header {
  border-radius: var(--r-lg);
  padding: var(--s4);
  color: #fff;
  box-shadow: var(--sh-card);
}

.hd-pending {
  background: linear-gradient(135deg, #faad14 0%, #f0a800 100%);
}

.hd-process {
  background: linear-gradient(135deg, #1677ff 0%, #0e5fd8 100%);
}

.hd-done {
  background: linear-gradient(135deg, #52c41a 0%, #3da612 100%);
}

.hd-cancel {
  background: linear-gradient(135deg, #8c8c8c 0%, #595959 100%);
}

.hd-reject {
  background: linear-gradient(135deg, #ff4d4f 0%, #d9363e 100%);
}

.status-title {
  font-size: var(--fs-xl);
  font-weight: var(--fw-semibold);
  display: flex;
  align-items: center;
  gap: var(--s2);
}

.status-desc {
  font-size: var(--fs-sm);
  opacity: 0.9;
  margin-top: var(--s1);
  line-height: 1.6;
}

.status-amount {
  display: flex;
  align-items: baseline;
  gap: var(--s1);
  margin-top: var(--s3);
  padding-top: var(--s3);
  border-top: 1px solid rgba(255, 255, 255, 0.2);
}

.amount-label {
  font-size: var(--fs-sm);
  opacity: 0.9;
}

.amount-value {
  font-size: var(--fs-2xl);
  font-weight: var(--fw-semibold);
}

/* 通用卡片 */
.card {
  background: var(--n1);
  border-radius: var(--r-lg);
  box-shadow: var(--sh-card);
  padding: var(--s3);
}

.card-title {
  font-size: var(--fs-base);
  color: var(--n10);
  font-weight: var(--fw-medium);
  margin-bottom: var(--s3);
  display: flex;
  align-items: center;
  gap: var(--s2);
}

.card-title::before {
  content: "";
  width: 3px;
  height: 14px;
  background: var(--c-primary);
  border-radius: 2px;
}

/* 进度时间轴 */
.steps {
  display: flex;
  flex-direction: column;
}

.step {
  display: flex;
  gap: var(--s3);
  position: relative;
  padding-bottom: var(--s4);
}

.step:last-child {
  padding-bottom: 0;
}

.step-line {
  position: absolute;
  left: 9px;
  top: 24px;
  bottom: 0;
  width: 2px;
  background: var(--n3);
}

.step:last-child .step-line {
  display: none;
}

.step-line.active {
  background: var(--c-primary);
}

.step-dot {
  width: 20px;
  height: 20px;
  border-radius: 50%;
  flex-shrink: 0;
  display: flex;
  align-items: center;
  justify-content: center;
  z-index: 1;
  background: var(--n1);
  border: 2px solid var(--n5);
  position: relative;
}

.step-dot.done {
  border-color: var(--c-primary);
  background: var(--c-primary);
  color: #fff;
}

.step-dot.active {
  border-color: var(--c-warning);
  background: var(--c-warning);
}

.step-dot.active::after {
  content: "";
  width: 8px;
  height: 8px;
  border-radius: 50%;
  background: #fff;
  position: absolute;
}

.step-dot.error {
  border-color: var(--c-error);
  background: var(--c-error);
  color: #fff;
}

.step-dot.muted {
  border-color: var(--n5);
  background: var(--n5);
}

.step-content {
  flex: 1;
  min-width: 0;
  display: flex;
  flex-direction: column;
}

.step-title {
  font-size: var(--fs-base);
  color: var(--n9);
  font-weight: var(--fw-medium);
}

.step-title.done {
  color: var(--n10);
}

.step-title.active {
  color: var(--c-warning);
}

.step-title.error {
  color: var(--c-error);
}

.step-title.muted {
  color: var(--n7);
}

.step-time {
  font-size: var(--fs-sm);
  color: var(--n7);
  margin-top: 2px;
}

.step-desc {
  font-size: var(--fs-sm);
  color: var(--n7);
  margin-top: 2px;
  line-height: 1.5;
}

/* 商品信息 */
.product-row {
  display: flex;
  gap: var(--s2);
}

.product-img {
  width: 64px;
  height: 64px;
  border-radius: var(--r-card);
  object-fit: cover;
  background: var(--n3);
  flex-shrink: 0;
}

.product-info {
  flex: 1;
  min-width: 0;
  display: flex;
  flex-direction: column;
  justify-content: space-between;
}

.product-name {
  font-size: var(--fs-base);
  color: var(--n10);
  line-height: 1.4;
  display: -webkit-box;
  -webkit-line-clamp: 2;
  -webkit-box-orient: vertical;
  overflow: hidden;
}

.product-bottom {
  display: flex;
  justify-content: space-between;
  align-items: center;
  gap: var(--s2);
}

.product-sku {
  font-size: var(--fs-sm);
  color: var(--n7);
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
}

.product-price {
  font-size: var(--fs-sm);
  color: var(--c-error);
  font-weight: var(--fw-medium);
  flex-shrink: 0;
}

/* 申请信息 */
.info-row {
  display: flex;
  align-items: flex-start;
  justify-content: space-between;
  gap: var(--s2);
  padding: 10px 0;
  border-bottom: 1px solid var(--n3);
}

.info-row:last-child {
  border-bottom: none;
}

.info-label {
  font-size: var(--fs-base);
  color: var(--n7);
  min-width: 72px;
  flex-shrink: 0;
}

.info-value {
  flex: 1;
  font-size: var(--fs-base);
  color: var(--n10);
  text-align: right;
  word-break: break-all;
}

.info-value.text {
  text-align: left;
  line-height: 1.5;
}

.info-value.amount {
  display: flex;
  justify-content: flex-end;
}

.info-value.success {
  color: var(--c-success);
  font-weight: var(--fw-medium);
}

.info-value.error {
  color: var(--c-error);
  font-weight: var(--fw-medium);
}

.info-value.copy {
  display: inline-flex;
  align-items: center;
  justify-content: flex-end;
  gap: var(--s1);
  cursor: pointer;
  background: none;
  border: none;
  font-family: inherit;
  padding: 0;
}

.info-value.copy svg {
  color: var(--c-primary);
  flex-shrink: 0;
}

.type-tag {
  display: inline-block;
  padding: 2px var(--s2);
  border-radius: var(--r-base);
  font-size: var(--fs-sm);
  font-weight: var(--fw-medium);
}

.tag-refund {
  background: rgba(22, 119, 255, 0.1);
  color: var(--c-primary);
}

.tag-return {
  background: rgba(250, 173, 20, 0.1);
  color: var(--c-warning);
}

/* 凭证图片 */
.evidence-grid {
  display: flex;
  flex-wrap: wrap;
  gap: var(--s2);
}

.evidence-item {
  width: 72px;
  height: 72px;
  border-radius: var(--r-card);
  object-fit: cover;
  background: var(--n3);
  cursor: pointer;
}

/* 协商记录 */
.negotiation {
  display: flex;
  flex-direction: column;
  gap: var(--s4);
}

.nego-item {
  display: flex;
  gap: var(--s2);
}

.nego-avatar {
  width: 32px;
  height: 32px;
  border-radius: 50%;
  flex-shrink: 0;
  display: flex;
  align-items: center;
  justify-content: center;
  color: #fff;
  font-size: var(--fs-sm);
  font-weight: var(--fw-medium);
}

.nego-avatar.buyer {
  background: var(--c-primary);
}

.nego-avatar.seller {
  background: var(--c-success);
}

.nego-avatar.platform {
  background: var(--c-warning);
}

.nego-body {
  flex: 1;
  min-width: 0;
}

.nego-head {
  display: flex;
  align-items: center;
  gap: var(--s1);
  margin-bottom: var(--s1);
  flex-wrap: wrap;
}

.nego-name {
  font-size: var(--fs-sm);
  color: var(--n9);
  font-weight: var(--fw-medium);
}

.nego-role {
  font-size: 10px;
  padding: 1px 6px;
  border-radius: var(--r-base);
}

.nego-role.buyer {
  background: rgba(22, 119, 255, 0.1);
  color: var(--c-primary);
}

.nego-role.seller {
  background: rgba(82, 196, 26, 0.1);
  color: var(--c-success);
}

.nego-role.platform {
  background: rgba(140, 140, 140, 0.1);
  color: var(--n7);
}

.nego-time {
  font-size: var(--fs-sm);
  color: var(--n7);
}

.nego-bubble {
  background: var(--n2);
  border-radius: var(--r-card);
  padding: var(--s2) var(--s3);
  font-size: var(--fs-sm);
  color: var(--n9);
  line-height: 1.6;
  word-break: break-all;
}

.nego-bubble.seller {
  background: rgba(82, 196, 26, 0.06);
}

.nego-bubble.platform {
  background: rgba(250, 173, 20, 0.06);
}

/* 底部操作栏 */
.action-bar {
  flex-shrink: 0;
  background: var(--n1);
  border-top: 1px solid var(--n3);
  padding: var(--s2) var(--s3);
  padding-bottom: calc(var(--s2) + env(safe-area-inset-bottom));
  box-shadow: 0 -2px 8px rgba(0, 0, 0, 0.04);
  display: flex;
  gap: var(--s3);
}

.action-btn {
  flex: 1;
  height: 44px;
  border-radius: var(--r-base);
  font-size: var(--fs-lg);
  font-weight: var(--fw-semibold);
  font-family: inherit;
  cursor: pointer;
  display: flex;
  align-items: center;
  justify-content: center;
  gap: var(--s2);
  transition: opacity var(--d-fast) var(--ease-std);
}

.action-btn:active {
  opacity: 0.85;
}

.action-btn:disabled {
  opacity: 0.5;
  cursor: not-allowed;
}

.action-btn.primary {
  background: var(--c-primary);
  color: #fff;
  border: none;
}

.action-btn.outline {
  background: var(--n1);
  color: var(--c-primary);
  border: 1px solid var(--c-primary);
}

.action-btn.danger {
  background: var(--n1);
  color: var(--c-error);
  border: 1px solid var(--c-error);
}

/* 退货物流弹层 */
.return-panel {
  padding: var(--s4) var(--s4) calc(var(--s4) + env(safe-area-inset-bottom));
  display: flex;
  flex-direction: column;
}

.panel-head {
  display: flex;
  align-items: center;
  justify-content: space-between;
  margin-bottom: var(--s3);
}

.panel-title {
  font-size: var(--fs-lg);
  font-weight: var(--fw-medium);
  color: var(--n10);
}

.panel-body {
  display: flex;
  flex-direction: column;
  gap: var(--s3);
}

.panel-field {
  display: flex;
  flex-direction: column;
  gap: var(--s1);
}

.field-label {
  font-size: var(--fs-sm);
  color: var(--n9);
}

.field-input {
  height: 42px;
  border: 1px solid var(--n3);
  border-radius: var(--r-card);
  padding: 0 var(--s3);
  font-size: var(--fs-base);
  font-family: inherit;
  color: var(--n10);
  outline: none;
  transition: border-color var(--d-mid) var(--ease-std);
  background: var(--n1);
}

.field-input:focus {
  border-color: var(--c-primary);
}

.field-input::placeholder {
  color: var(--n7);
}

.panel-tip {
  font-size: var(--fs-sm);
  color: var(--n7);
  line-height: 1.6;
}

.panel-foot {
  margin-top: var(--s4);
}

.panel-submit {
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
  transition: opacity var(--d-fast) var(--ease-std);
}

.panel-submit:active {
  opacity: 0.85;
}

.panel-submit.loading {
  opacity: 0.7;
}
</style>
