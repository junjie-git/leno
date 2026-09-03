<script setup lang="ts">
import { computed, onMounted, ref } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import { showFailToast, showToast } from 'vant'
import type { UploaderFileListItem } from 'vant'
import { afterSalesApi } from '@/modules/10-after-sales/api/afterSales.api'
import { orderApi } from '@/modules/06-order/api/order.api'
import { publicApi } from '@/modules/14-public/api/public.api'
import type { AfterSalesDto, AfterSalesType } from '../types/after-sales.dto'
import type { OrderDto, OrderItemDto } from '@/modules/06-order/types/order.dto'
import type { DictionaryItemDto } from '@/modules/14-public/types/public.dto'
import ErrorState from '@/shared/components/ErrorState.vue'
import PriceText from '@/shared/components/PriceText.vue'
import { formatPriceExact } from '@/shared/utils/format'
import { logger } from '@/shared/utils/logger'

/**
 * 申请售后页（/after-sales/apply/:orderLineId）
 *
 * 结构（对齐设计稿 after-sales-apply）：
 * NavBar（返回 / 申请售后）→ 商品卡（图 + 标题 + 规格 + 单价 + 数量）
 * → 售后类型单选卡（仅退款 / 退货退款）→ 申请信息（原因 Picker + 退款金额 + 最大可退提示）
 * → 问题描述（textarea + 字数统计）→ 凭证图片（van-uploader，最多 5 张）→ 温馨提示
 * → 底部固定提交栏（提交申请 → 跳售后详情）
 *
 * 数据流：按 orderLineId 翻页检索订单列表定位订单行 → 渲染商品卡与默认退款金额；
 * 原因选项来自字典 after-sales-reasons；凭证图经 POST /after-sales/images 上传取回 URL；
 * 提交 POST /after-sales（金额单位分，不超过订单行实付），成功后 replace 售后详情。
 */

const route = useRoute()
const router = useRouter()

// ---- 常量 ----
/** 凭证图上限与单张大小上限 */
const MAX_IMAGE_COUNT = 5
const MAX_IMAGE_SIZE = 5 * 1024 * 1024
/** 允许申请售后的订单状态（已支付之后且未取消） */
const SALEABLE_ORDER_STATUSES = ['Paid', 'Shipped', 'Completed']
/** 订单行检索分页参数（listMine 无行级端点，按订单列表翻页定位） */
const ORDER_PAGE_SIZE = 20
const ORDER_MAX_PAGES = 10

/** 售后类型选项（dto 枚举 RefundOnly / ReturnRefund；Exchange 本页不开放） */
const TYPE_OPTIONS: Array<{
  value: Extract<AfterSalesType, 'RefundOnly' | 'ReturnRefund'>
  title: string
  desc: string
  tag: string
  tagCls: string
}> = [
  { value: 'RefundOnly', title: '仅退款', desc: '未收到货，或与卖家协商一致仅退款', tag: '退款', tagCls: 'tag-refund' },
  { value: 'ReturnRefund', title: '退货退款', desc: '已收到货，需寄回商品并退款', tag: '退货', tagCls: 'tag-return' },
]

// ---- 页面状态 ----
const loading = ref(true)
const loadError = ref(false)
const notFound = ref(false)
const notSaleable = ref(false)
const existing = ref<AfterSalesDto | null>(null)

const order = ref<OrderDto | null>(null)
const line = ref<OrderItemDto | null>(null)
const reasons = ref<DictionaryItemDto[]>([])

// ---- 表单状态 ----
const applyType = ref<'RefundOnly' | 'ReturnRefund'>('RefundOnly')
const reasonLabel = ref('')
const reasonVisible = ref(false)
const amountYuan = ref('')
const description = ref('')
const fileList = ref<UploaderFileListItem[]>([])
const submitting = ref(false)
const uploadingCount = ref(0)

/** 上传成功项 → 服务端 URL 映射（fileList 删除时自动失效） */
const uploadedUrlMap = new WeakMap<UploaderFileListItem, string>()

/** 已上传成功的凭证 URL 列表（提交用） */
const evidenceUrls = computed(() =>
  fileList.value.map((item) => uploadedUrlMap.get(item)).filter((url): url is string => !!url),
)

/** 订单行最大可退金额（分）= 单价 × 数量 */
const maxRefundCents = computed(() => (line.value ? line.value.price * line.value.quantity : 0))

/** 表单可编辑态（订单行就绪 + 可售后 + 无进行中售后单） */
const formReady = computed(
  () => !!line.value && !notFound.value && !notSaleable.value && !existing.value && !loadError.value,
)

// ---- 订单行检索（按 orderLineId 翻页定位订单与订单行） ----
async function findOrderLine(orderLineId: string): Promise<{ order: OrderDto; line: OrderItemDto } | null> {
  let page = 1
  for (let i = 0; i < ORDER_MAX_PAGES; i += 1) {
    const result = await orderApi.list({ page, pageSize: ORDER_PAGE_SIZE })
    for (const item of result.items) {
      const matched = item.items.find((row) => row.orderLineId === orderLineId)
      if (matched) {
        return { order: item, line: matched }
      }
    }
    if (result.items.length < ORDER_PAGE_SIZE) {
      return null
    }
    page += 1
  }
  return null
}

// ---- 数据加载 ----
async function loadAll(): Promise<void> {
  const orderLineId = String(route.params.orderLineId ?? '')
  loading.value = true
  loadError.value = false
  notFound.value = false
  notSaleable.value = false
  existing.value = null
  order.value = null
  line.value = null
  try {
    const [found, dict] = await Promise.all([
      findOrderLine(orderLineId),
      publicApi.getDictionary('after-sales-reasons'),
    ])
    reasons.value = dict.items
    if (!found) {
      notFound.value = true
      return
    }
    order.value = found.order
    line.value = found.line
    if (!SALEABLE_ORDER_STATUSES.includes(found.order.status)) {
      notSaleable.value = true
      return
    }
    // 进行中售后单检查（失败静默，提交时由服务端兜底校验）
    try {
      const mine = await afterSalesApi.listMine()
      existing.value =
        mine.find((a) => a.orderLineId === orderLineId && a.status !== 'Completed' && a.status !== 'Cancelled') ??
        null
    } catch (e) {
      logger.warn('进行中售后检查失败（忽略）', e)
    }
    if (existing.value) {
      return
    }
    // 默认退款金额 = 最大可退金额
    amountYuan.value = (maxRefundCents.value / 100).toFixed(2)
  } catch (e) {
    logger.error('售后申请页加载失败', e)
    loadError.value = true
  } finally {
    loading.value = false
  }
}

onMounted(() => {
  void loadAll()
})

// ---- 原因选择 ----
const reasonColumns = computed(() =>
  reasons.value.map((item) => ({ text: item.label, value: item.value })),
)

function onReasonConfirm(params: { selectedOptions: Array<{ text?: string | number } | undefined> }): void {
  reasonLabel.value = String(params.selectedOptions[0]?.text ?? '')
  reasonVisible.value = false
}

// ---- 金额输入 ----
/** 失焦归一：保留两位小数并夹取到 (0, 最大可退] */
function normalizeAmount(): void {
  const n = Number(amountYuan.value)
  if (!Number.isFinite(n) || n <= 0) {
    return
  }
  let cents = Math.round(n * 100)
  if (cents > maxRefundCents.value) {
    cents = maxRefundCents.value
  }
  amountYuan.value = (cents / 100).toFixed(2)
}

/** 解析退款金额（分）；非法或超限返回 null */
function parseAmountCents(): number | null {
  const n = Number(amountYuan.value)
  if (!Number.isFinite(n) || n <= 0) {
    return null
  }
  const cents = Math.round(n * 100)
  if (cents > maxRefundCents.value) {
    return null
  }
  return cents
}

// ---- 凭证上传 ----
/** 读取前置校验：仅 JPG / PNG / WebP */
function validateImageFile(file: File): boolean {
  const okType = ['image/jpeg', 'image/png', 'image/webp'].includes(file.type)
  if (!okType) {
    showFailToast('仅支持 JPG / PNG / WebP 图片')
    return false
  }
  return true
}

function onBeforeRead(file: File | File[]): boolean {
  if (Array.isArray(file)) {
    return file.every(validateImageFile)
  }
  return validateImageFile(file)
}

function onOversize(): void {
  showFailToast('单张图片不能超过 5MB')
}

async function onAfterRead(items: UploaderFileListItem | UploaderFileListItem[]): Promise<void> {
  const list = Array.isArray(items) ? items : [items]
  await Promise.all(
    list.map(async (item) => {
      if (!item.file) {
        return
      }
      item.status = 'uploading'
      item.message = '上传中'
      uploadingCount.value += 1
      try {
        const urls = await afterSalesApi.uploadImages([item.file])
        const url = urls[0]
        if (!url) {
          throw new Error('凭证图上传未返回地址')
        }
        uploadedUrlMap.set(item, url)
        item.status = 'done'
        item.message = ''
      } catch (e) {
        logger.warn('凭证图上传失败', e)
        item.status = 'failed'
        item.message = '上传失败'
        showFailToast('图片上传失败，可删除后重试')
      } finally {
        uploadingCount.value -= 1
      }
    }),
  )
}

// ---- 提交 ----
async function submit(): Promise<void> {
  if (submitting.value || !formReady.value) {
    return
  }
  if (!reasonLabel.value) {
    showToast('请选择申请原因')
    return
  }
  const cents = parseAmountCents()
  if (cents == null) {
    showFailToast(`退款金额需大于 0 且不超过 ¥${formatPriceExact(maxRefundCents.value)}`)
    return
  }
  const desc = description.value.trim()
  if (desc.length < 10) {
    showToast('问题描述至少输入 10 个字')
    return
  }
  if (uploadingCount.value > 0) {
    showToast('凭证图片上传中，请稍候')
    return
  }
  submitting.value = true
  try {
    const created = await afterSalesApi.apply({
      orderLineId: String(route.params.orderLineId ?? ''),
      type: applyType.value,
      reason: reasonLabel.value,
      description: desc,
      images: evidenceUrls.value,
      refundAmount: cents,
    })
    showToast('提交成功，等待卖家审核')
    router.replace(`/after-sales/${created.id}`)
  } catch (e) {
    logger.warn('售后申请提交失败', e)
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
    router.replace('/orders')
  }
}

function goOrder(): void {
  if (order.value) {
    router.replace(`/order/${order.value.id}`)
  } else {
    router.replace('/orders')
  }
}

function goExistingDetail(): void {
  if (existing.value) {
    router.replace(`/after-sales/${existing.value.id}`)
  }
}
</script>

<template>
  <div class="apply-page">
    <!-- NavBar -->
    <header class="navbar">
      <button class="nav-back" type="button" aria-label="返回" @click="goBack">
        <svg width="20" height="20" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round">
          <path d="M15 18l-6-6 6-6" />
        </svg>
      </button>
      <div class="nav-title">申请售后</div>
    </header>

    <!-- 骨架屏 -->
    <main v-if="loading" class="body">
      <div class="card sk-product">
        <div class="skeleton-block sk-img" />
        <div class="sk-lines">
          <div class="skeleton-block sk-l1" />
          <div class="skeleton-block sk-l2" />
          <div class="skeleton-block sk-l3" />
        </div>
      </div>
      <div class="skeleton-block sk-block" />
      <div class="skeleton-block sk-block" />
      <div class="skeleton-block sk-block tall" />
    </main>

    <!-- 加载失败 -->
    <main v-else-if="loadError" class="body">
      <ErrorState title="加载失败" description="网络异常，请检查网络连接后重试" @retry="loadAll" />
    </main>

    <!-- 订单行不存在 -->
    <main v-else-if="notFound" class="body">
      <ErrorState
        title="售后商品不存在"
        description="未找到该订单行，可能订单已被删除或链接失效"
        retry-text="返回订单"
        @retry="goOrder"
      />
    </main>

    <!-- 订单不可售后 -->
    <main v-else-if="notSaleable" class="body">
      <ErrorState
        title="订单不可售后"
        description="该订单状态不支持申请售后，仅已支付、已发货或已完成的订单可申请"
        retry-text="返回订单"
        @retry="goOrder"
      />
    </main>

    <!-- 已有进行中售后单 -->
    <main v-else-if="existing" class="body">
      <ErrorState
        title="该商品已申请售后"
        description="同一商品存在进行中的售后单，请查看售后进度"
        retry-text="查看售后单"
        @retry="goExistingDetail"
      />
    </main>

    <!-- 表单 -->
    <main v-else class="body">
      <!-- 商品卡 -->
      <section v-if="line" class="card product-card">
        <img class="product-img" :src="line.image" :alt="line.name" loading="lazy">
        <div class="product-info">
          <div class="product-name">{{ line.name }}</div>
          <div class="product-sku">规格：{{ line.specs }}</div>
          <div class="product-bottom">
            <PriceText :amount="line.price" :size="15" />
            <span class="product-qty">×{{ line.quantity }}</span>
          </div>
        </div>
      </section>

      <!-- 售后类型 -->
      <section class="section">
        <div class="section-title">售后类型</div>
        <div class="card">
          <div class="radio-group" role="radiogroup" aria-label="售后类型">
            <button
              v-for="option in TYPE_OPTIONS"
              :key="option.value"
              class="radio-card"
              :class="{ selected: applyType === option.value }"
              type="button"
              role="radio"
              :aria-checked="applyType === option.value"
              @click="applyType = option.value"
            >
              <span class="radio-icon" />
              <span class="radio-content">
                <span class="radio-title">{{ option.title }}</span>
                <span class="radio-desc">{{ option.desc }}</span>
              </span>
              <span class="radio-tag" :class="option.tagCls">{{ option.tag }}</span>
            </button>
          </div>
          <div v-if="applyType === 'ReturnRefund'" class="return-tip">
            退货退款需在卖家同意后，于售后详情页填写退货物流单号寄回商品
          </div>
        </div>
      </section>

      <!-- 申请信息 -->
      <section class="section">
        <div class="section-title">申请信息</div>
        <div class="card">
          <button class="form-cell" type="button" aria-label="选择申请原因" @click="reasonVisible = true">
            <span class="form-label"><span class="required">*</span>申请原因</span>
            <span class="form-value">
              <span :class="reasonLabel ? 'value-text' : 'value-placeholder'">
                {{ reasonLabel || '请选择' }}
              </span>
              <svg width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round">
                <path d="M9 6l6 6-6 6" />
              </svg>
            </span>
          </button>
          <div class="form-cell static">
            <span class="form-label"><span class="required">*</span>退款金额</span>
            <span class="form-value">
              <span class="currency">¥</span>
              <input
                v-model="amountYuan"
                class="amount-input"
                type="text"
                inputmode="decimal"
                placeholder="0.00"
                aria-label="退款金额（元）"
                @blur="normalizeAmount"
              >
            </span>
          </div>
          <div class="amount-hint">
            <svg width="14" height="14" viewBox="0 0 24 24" fill="currentColor">
              <path d="M12 2C6.5 2 2 6.5 2 12s4.5 10 10 10 10-4.5 10-10S17.5 2 12 2zm-1 15l-5-5 1.4-1.4L11 14.2l5.6-5.6L18 10l-7 7z" />
            </svg>
            <span>最多可退 ¥{{ formatPriceExact(maxRefundCents) }}，退款将原路退回</span>
          </div>
        </div>
      </section>

      <!-- 问题描述 -->
      <section class="section">
        <div class="section-title">问题描述</div>
        <div class="card">
          <textarea
            v-model="description"
            class="textarea"
            maxlength="200"
            placeholder="请详细描述遇到的问题，以便卖家快速处理（至少 10 个字）"
            aria-label="问题描述"
          />
          <div class="textarea-counter">{{ description.length }}/200</div>
        </div>
      </section>

      <!-- 凭证图片 -->
      <section class="section">
        <div class="section-title">凭证图片</div>
        <div class="card">
          <van-uploader
            v-model="fileList"
            class="uploader"
            multiple
            accept="image/jpeg,image/png,image/webp"
            :max-count="MAX_IMAGE_COUNT"
            :max-size="MAX_IMAGE_SIZE"
            preview-size="96px"
            upload-text="上传凭证"
            :before-read="onBeforeRead"
            :after-read="onAfterRead"
            @oversize="onOversize"
          />
          <div class="uploader-tips">最多 {{ MAX_IMAGE_COUNT }} 张，支持 JPG / PNG / WebP，每张不超过 5MB</div>
        </div>
      </section>

      <!-- 温馨提示 -->
      <div class="tips-box">
        <svg class="tips-icon" width="16" height="16" viewBox="0 0 24 24" fill="currentColor">
          <path d="M12 2C6.5 2 2 6.5 2 12s4.5 10 10 10 10-4.5 10-10S17.5 2 12 2zm1 15h-2v-2h2v2zm0-4h-2V7h2v6z" />
        </svg>
        <p class="tips-text">
          提交后卖家将在 48 小时内处理审核，如超时未处理系统将自动同意退款。退货退款需在卖家同意后填写退货物流单号。
        </p>
      </div>
    </main>

    <!-- 底部提交栏 -->
    <footer v-if="formReady" class="submit-bar">
      <button
        class="submit-btn"
        :class="{ loading: submitting }"
        type="button"
        :disabled="submitting"
        aria-label="提交售后申请"
        @click="submit"
      >
        <span v-if="submitting" class="spinner" />
        {{ submitting ? '提交中...' : '提交申请' }}
      </button>
    </footer>

    <!-- 原因选择弹层 -->
    <van-popup
      v-model:show="reasonVisible"
      position="bottom"
      round
      role="dialog"
      aria-label="选择申请原因"
    >
      <van-picker
        title="申请原因"
        :columns="reasonColumns"
        @confirm="onReasonConfirm"
        @cancel="reasonVisible = false"
      />
    </van-popup>
  </div>
</template>

<style scoped>
.apply-page {
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

/* 通用卡片 */
.card {
  background: var(--n1);
  border-radius: var(--r-lg);
  box-shadow: var(--sh-card);
  padding: var(--s3);
}

.section-title {
  font-size: var(--fs-base);
  color: var(--n9);
  font-weight: var(--fw-medium);
  margin-bottom: var(--s2);
  display: flex;
  align-items: center;
  gap: var(--s2);
}

.section-title::before {
  content: "";
  width: 3px;
  height: 14px;
  background: var(--c-primary);
  border-radius: 2px;
}

/* 骨架屏 */
.sk-product {
  display: flex;
  gap: var(--s3);
}

.sk-img {
  width: 72px;
  height: 72px;
  flex-shrink: 0;
}

.sk-lines {
  flex: 1;
  display: flex;
  flex-direction: column;
  gap: var(--s2);
  justify-content: center;
}

.sk-l1 {
  width: 90%;
  height: 15px;
}

.sk-l2 {
  width: 50%;
  height: 12px;
}

.sk-l3 {
  width: 35%;
  height: 14px;
}

.sk-block {
  height: 150px;
}

.sk-block.tall {
  height: 190px;
}

/* 商品卡 */
.product-card {
  display: flex;
  gap: var(--s3);
}

.product-img {
  width: 72px;
  height: 72px;
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

.product-sku {
  font-size: var(--fs-sm);
  color: var(--n7);
  margin-top: 2px;
}

.product-bottom {
  display: flex;
  align-items: center;
  justify-content: space-between;
}

.product-qty {
  font-size: var(--fs-sm);
  color: var(--n7);
}

/* 售后类型 */
.radio-group {
  display: flex;
  flex-direction: column;
  gap: var(--s2);
}

.radio-card {
  display: flex;
  align-items: center;
  gap: var(--s3);
  padding: var(--s3);
  border: 2px solid var(--n3);
  border-radius: var(--r-card);
  background: var(--n1);
  cursor: pointer;
  transition: border-color var(--d-mid) var(--ease-std), background var(--d-mid) var(--ease-std);
  text-align: left;
  font-family: inherit;
}

.radio-card.selected {
  border-color: var(--c-primary);
  background: rgba(22, 119, 255, 0.03);
}

.radio-icon {
  width: 20px;
  height: 20px;
  border-radius: 50%;
  border: 2px solid var(--n5);
  flex-shrink: 0;
  display: flex;
  align-items: center;
  justify-content: center;
  transition: border-color var(--d-mid) var(--ease-std);
}

.radio-card.selected .radio-icon {
  border-color: var(--c-primary);
}

.radio-card.selected .radio-icon::after {
  content: "";
  width: 10px;
  height: 10px;
  border-radius: 50%;
  background: var(--c-primary);
}

.radio-content {
  flex: 1;
  min-width: 0;
}

.radio-title {
  display: block;
  font-size: var(--fs-base);
  color: var(--n10);
  font-weight: var(--fw-medium);
}

.radio-desc {
  display: block;
  font-size: var(--fs-sm);
  color: var(--n7);
  margin-top: 2px;
}

.radio-tag {
  font-size: var(--fs-sm);
  padding: 2px var(--s2);
  border-radius: var(--r-base);
  font-weight: var(--fw-medium);
  flex-shrink: 0;
}

.tag-refund {
  background: rgba(22, 119, 255, 0.1);
  color: var(--c-primary);
}

.tag-return {
  background: rgba(250, 173, 20, 0.1);
  color: var(--c-warning);
}

.return-tip {
  margin-top: var(--s2);
  padding: var(--s2) var(--s3);
  background: rgba(250, 173, 20, 0.06);
  border-radius: var(--r-base);
  font-size: var(--fs-sm);
  color: var(--n9);
  line-height: 1.6;
}

/* 申请信息 */
.form-cell {
  display: flex;
  align-items: center;
  justify-content: space-between;
  gap: var(--s2);
  width: 100%;
  padding: var(--s3) 0;
  border: none;
  border-bottom: 1px solid var(--n3);
  background: none;
  cursor: pointer;
  font-family: inherit;
}

.form-cell.static {
  cursor: default;
}

.form-cell:last-of-type {
  border-bottom: none;
}

.form-label {
  font-size: var(--fs-base);
  color: var(--n10);
  flex-shrink: 0;
}

.required {
  color: var(--c-error);
  margin-right: 2px;
}

.form-value {
  flex: 1;
  display: flex;
  align-items: center;
  justify-content: flex-end;
  gap: var(--s1);
  color: var(--n7);
  min-width: 0;
}

.value-text {
  font-size: var(--fs-base);
  color: var(--n10);
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
}

.value-placeholder {
  font-size: var(--fs-base);
  color: var(--n7);
}

.currency {
  font-size: var(--fs-base);
  color: var(--n9);
  margin-right: 2px;
}

.amount-input {
  width: 110px;
  border: none;
  outline: none;
  text-align: right;
  font-size: var(--fs-lg);
  font-weight: var(--fw-semibold);
  font-family: inherit;
  color: var(--n10);
  background: transparent;
}

.amount-input::placeholder {
  color: var(--n7);
  font-weight: var(--fw-normal);
}

.amount-hint {
  margin-top: var(--s2);
  display: flex;
  align-items: center;
  gap: var(--s1);
  padding: var(--s2);
  background: rgba(82, 196, 26, 0.06);
  border-radius: var(--r-base);
  font-size: var(--fs-sm);
  color: var(--c-success);
  line-height: 1.5;
}

.amount-hint svg {
  flex-shrink: 0;
}

/* 问题描述 */
.textarea {
  width: 100%;
  min-height: 100px;
  border: 1px solid var(--n3);
  border-radius: var(--r-card);
  padding: var(--s3);
  font-size: var(--fs-base);
  font-family: inherit;
  color: var(--n10);
  resize: none;
  line-height: 1.6;
  outline: none;
  transition: border-color var(--d-mid) var(--ease-std);
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

/* 凭证上传 */
.uploader-tips {
  font-size: var(--fs-sm);
  color: var(--n7);
  margin-top: var(--s2);
}

/* 温馨提示 */
.tips-box {
  background: rgba(250, 173, 20, 0.06);
  border-radius: var(--r-card);
  padding: var(--s3);
  display: flex;
  gap: var(--s2);
}

.tips-icon {
  color: var(--c-warning);
  flex-shrink: 0;
  margin-top: 2px;
}

.tips-text {
  font-size: var(--fs-sm);
  color: var(--n9);
  line-height: 1.6;
}

/* 底部提交栏 */
.submit-bar {
  flex-shrink: 0;
  background: var(--n1);
  border-top: 1px solid var(--n3);
  padding: var(--s2) var(--s3);
  padding-bottom: calc(var(--s2) + env(safe-area-inset-bottom));
  box-shadow: 0 -2px 8px rgba(0, 0, 0, 0.04);
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
  transition: opacity var(--d-fast) var(--ease-std);
}

.submit-btn:active {
  opacity: 0.85;
}

.submit-btn.loading {
  opacity: 0.7;
}

.spinner {
  width: 16px;
  height: 16px;
  border: 2px solid rgba(255, 255, 255, 0.4);
  border-top-color: #fff;
  border-radius: 50%;
  animation: spin 0.8s linear infinite;
}

@keyframes spin {
  to {
    transform: rotate(360deg);
  }
}
</style>
