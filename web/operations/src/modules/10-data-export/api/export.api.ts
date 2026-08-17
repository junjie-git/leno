import { orderApi } from '@/modules/05-order-ops/api/order.api'
import { afterSalesApi } from '@/modules/05-order-ops/api/afterSales.api'
import { reviewApi } from '@/modules/05-order-ops/api/review.api'
import { paymentApi } from '@/modules/06-payment-ops/api/payment.api'
import { refundApi } from '@/modules/06-payment-ops/api/refund.api'
import { productApi } from '@/modules/02-product-ops/api/product.api'
import { shopApi } from '@/modules/04-seller-ops/api/shop.api'
import { recordApi } from '@/modules/07-notification-ops/api/record.api'
import { ORDER_STATUS_META } from '@/modules/05-order-ops/types/order.dto'
import type { OrderStatus } from '@/modules/05-order-ops/types/order.dto'
import {
  AFTER_SALES_STATUS_META,
  AFTER_SALES_TYPE_META,
} from '@/modules/05-order-ops/types/afterSales.dto'
import type { AfterSalesStatus, AfterSalesType } from '@/modules/05-order-ops/types/afterSales.dto'
import { REVIEW_STATUS_META } from '@/modules/05-order-ops/types/review.dto'
import type { ReviewStatus } from '@/modules/05-order-ops/types/review.dto'
import type { PaymentChannelType, PaymentStatus } from '@/modules/06-payment-ops/types/payment.dto'
import type { RefundStatus } from '@/modules/06-payment-ops/types/refund.dto'
import type { ProductStatus } from '@/modules/02-product-ops/types/product.dto'
import type { ShopStatus } from '@/modules/04-seller-ops/types/shop.dto'
import { NOTIFICATION_CHANNEL_META } from '@/modules/07-notification-ops/types/template.dto'
import type { NotificationChannel } from '@/modules/07-notification-ops/types/template.dto'
import { NOTIFICATION_STATUS_META } from '@/modules/07-notification-ops/types/record.dto'
import type {
  NotificationRecordDto,
  NotificationStatus,
} from '@/modules/07-notification-ops/types/record.dto'
import {
  EXPORT_MAX_ROWS,
  EXPORT_PAGE_SIZE,
  EXPORT_RETENTION_DAYS,
  EXPORT_BUSINESS_TYPE_LABELS,
  type ExportBusinessType,
  type ExportFetchResult,
  type ExportFilterParams,
  type ExportTaskRecord,
} from '../types/export.dto'

/**
 * 数据导出 API（降级方案）
 *
 * 后端异步导出端点 /api/admin/data-exports/* 未上线，本模块聚合各业务域
 * 既有列表端点分页同步拉取，前端生成 CSV 下载：
 * - Order      GET /admin/orders
 * - Payment    GET /admin/payments
 * - Refund     GET /admin/refunds
 * - AfterSales GET /admin/after-sales
 * - Product    GET /admin/products/all（后端不支持时间范围，忽略时间筛选）
 * - Notification GET /notifications/records
 * - Review     GET /admin/reviews
 * - Seller     GET /admin/shops（后端不支持时间范围，忽略时间筛选）
 *
 * 单任务上限 EXPORT_MAX_ROWS（10000 行），超限截断并提示缩小时间范围；
 * 任务历史记录持久化 localStorage（保留 7 天）。
 */

// ---------- 各业务展示标签（CSV 序列化用，中文列值） ----------

const PAYMENT_STATUS_LABELS: Record<PaymentStatus, string> = {
  Pending: '待支付',
  Success: '已支付',
  Failed: '支付失败',
  Refunded: '已退款',
}

const PAYMENT_CHANNEL_LABELS: Record<PaymentChannelType, string> = {
  WeChat: '微信支付',
  Alipay: '支付宝',
  Other: '其他',
}

const REFUND_STATUS_LABELS: Record<RefundStatus, string> = {
  Pending: '待退款',
  Refunded: '已退款',
  Failed: '退款失败',
}

const PRODUCT_STATUS_LABELS: Record<ProductStatus, string> = {
  Draft: '草稿',
  PendingAudit: '待审核',
  Active: '已上架',
  Rejected: '已驳回',
  OffShelf: '已下架',
}

const SHOP_STATUS_LABELS: Record<ShopStatus, string> = {
  PendingReview: '待审核',
  Active: '已通过',
  Rejected: '已驳回',
  Suspended: '已暂停',
  Closed: '已关闭',
}

/** 各业务类型状态筛选项（新建任务表单动态下拉数据源，与 CSV 序列化共用标签） */
export const EXPORT_STATUS_OPTIONS: Record<ExportBusinessType, { label: string; value: string }[]> = {
  Order: (Object.keys(ORDER_STATUS_META) as OrderStatus[]).map((s) => ({
    label: ORDER_STATUS_META[s].label,
    value: s,
  })),
  Payment: (Object.keys(PAYMENT_STATUS_LABELS) as PaymentStatus[]).map((s) => ({
    label: PAYMENT_STATUS_LABELS[s],
    value: s,
  })),
  Refund: (Object.keys(REFUND_STATUS_LABELS) as RefundStatus[]).map((s) => ({
    label: REFUND_STATUS_LABELS[s],
    value: s,
  })),
  AfterSales: (Object.keys(AFTER_SALES_STATUS_META) as AfterSalesStatus[]).map((s) => ({
    label: AFTER_SALES_STATUS_META[s].label,
    value: s,
  })),
  Product: (Object.keys(PRODUCT_STATUS_LABELS) as ProductStatus[]).map((s) => ({
    label: PRODUCT_STATUS_LABELS[s],
    value: s,
  })),
  Notification: (Object.keys(NOTIFICATION_STATUS_META) as NotificationStatus[]).map((s) => ({
    label: NOTIFICATION_STATUS_META[s].label,
    value: s,
  })),
  Review: (Object.keys(REVIEW_STATUS_META) as ReviewStatus[]).map((s) => ({
    label: REVIEW_STATUS_META[s].label,
    value: s,
  })),
  Seller: (Object.keys(SHOP_STATUS_LABELS) as ShopStatus[]).map((s) => ({
    label: SHOP_STATUS_LABELS[s],
    value: s,
  })),
}

// ---------- 拉取选项与数据源注册 ----------

export interface ExportFetchOptions {
  /** 时间范围下界（ISO 8601 UTC） */
  fromTime: string
  /** 时间范围上界（ISO 8601 UTC） */
  toTime: string
  /** 业务筛选（keyword / status，按业务类型映射到各端点参数） */
  filters?: ExportFilterParams
  /** 进度回调：已拉取行数 / 后端命中总数 */
  onProgress?: (fetched: number, total: number) => void
  /** 行数上限（默认 EXPORT_MAX_ROWS） */
  maxRows?: number
  /** 分页大小（默认 EXPORT_PAGE_SIZE） */
  pageSize?: number
}

/** 单页拉取结果（已序列化为 CSV 行） */
interface PageFetch {
  rows: string[][]
  total: number
}

/** 业务数据源：表头 + 按页拉取并序列化 */
interface ExportSource {
  header: string[]
  fetchPage(page: number, pageSize: number): Promise<PageFetch>
}

/** 金额列格式化：两位小数 */
function yuan(value: number): string {
  return value.toFixed(2)
}

/** 可空文本列兜底 */
function orDash(value: string | undefined | null): string {
  return value && value.length > 0 ? value : '—'
}

/** 按业务类型构造数据源（时间范围 + 筛选映射到各列表端点参数） */
function createSource(
  businessType: ExportBusinessType,
  options: ExportFetchOptions,
): ExportSource {
  const { fromTime, toTime } = options
  const keyword = options.filters?.keyword?.trim() || undefined
  const status = options.filters?.status || undefined

  switch (businessType) {
    case 'Order':
      return {
        header: ['订单号', '买家ID', '卖家ID', '商品摘要', '总金额(元)', '支付方式', '状态', '下单时间'],
        async fetchPage(page, pageSize) {
          const { data } = await orderApi.list({
            page,
            pageSize,
            orderNo: keyword,
            status: status as OrderStatus | undefined,
            fromTime,
            toTime,
          })
          return {
            total: data.total,
            rows: data.items.map((o) => [
              o.orderNo,
              o.userId,
              o.sellerId,
              o.itemSummary,
              yuan(o.totalAmount),
              orDash(o.paymentMethod),
              ORDER_STATUS_META[o.status].label,
              o.createdAt,
            ]),
          }
        },
      }
    case 'Payment':
      return {
        header: ['支付单号', '订单号', '用户ID', '金额(元)', '渠道', '状态', '创建时间'],
        async fetchPage(page, pageSize) {
          const { data } = await paymentApi.list({
            page,
            pageSize,
            paymentNo: keyword,
            status: status as PaymentStatus | undefined,
            fromTime,
            toTime,
          })
          return {
            total: data.total,
            rows: data.items.map((p) => [
              p.paymentNo,
              orDash(p.orderNo ?? p.orderId),
              p.userId,
              yuan(p.amount),
              PAYMENT_CHANNEL_LABELS[p.channel],
              PAYMENT_STATUS_LABELS[p.status],
              p.createdAt,
            ]),
          }
        },
      }
    case 'Refund':
      return {
        header: ['退款编号', '订单号', '用户ID', '退款金额(元)', '渠道', '状态', '申请时间'],
        async fetchPage(page, pageSize) {
          const { data } = await refundApi.list({
            page,
            pageSize,
            refundNo: keyword,
            status: status as RefundStatus | undefined,
            fromTime,
            toTime,
          })
          return {
            total: data.total,
            rows: data.items.map((r) => [
              r.refundNo,
              orDash(r.orderNo ?? r.orderId),
              r.userId,
              yuan(r.amount),
              PAYMENT_CHANNEL_LABELS[r.channel],
              REFUND_STATUS_LABELS[r.status],
              r.requestedAt,
            ]),
          }
        },
      }
    case 'AfterSales':
      return {
        header: ['售后单号', '订单号', '买家ID', '卖家ID', '类型', '状态', '申请金额(元)', '申请时间'],
        async fetchPage(page, pageSize) {
          const { data } = await afterSalesApi.list({
            page,
            pageSize,
            afterSalesNo: keyword,
            status: status as AfterSalesStatus | undefined,
            fromTime,
            toTime,
          })
          return {
            total: data.total,
            rows: data.items.map((a) => [
              a.afterSalesNo,
              orDash(a.orderNo ?? a.orderId),
              a.userId,
              a.sellerId,
              AFTER_SALES_TYPE_META[a.type as AfterSalesType].label,
              AFTER_SALES_STATUS_META[a.status].label,
              yuan(a.applyAmount),
              a.createdAt,
            ]),
          }
        },
      }
    case 'Product':
      // 商品列表端点不支持时间范围，时间筛选仅作为任务记录快照保留
      return {
        header: ['商品ID', '商品名称', '状态', '分类', '品牌', '卖家', 'SKU数', '提交审核时间'],
        async fetchPage(page, pageSize) {
          const { data } = await productApi.list({
            page,
            pageSize,
            keyword,
            status: status as ProductStatus | undefined,
          })
          return {
            total: data.total,
            rows: data.items.map((p) => [
              p.id,
              p.title,
              PRODUCT_STATUS_LABELS[p.status],
              orDash(p.categoryName ?? p.categoryId),
              orDash(p.brandName),
              orDash(p.sellerName ?? p.sellerId),
              String(p.skus?.length ?? 0),
              p.submittedAt,
            ]),
          }
        },
      }
    case 'Notification':
      return {
        header: ['记录ID', '用户ID', '接收人', '渠道', '模板编码', '状态', '业务引用', '重试次数', '创建时间'],
        async fetchPage(page, pageSize) {
          const { data } = await recordApi.list({
            page,
            pageSize,
            userId: keyword,
            status: status as NotificationStatus | undefined,
            fromTime,
            toTime,
          })
          return {
            total: data.total,
            rows: data.items.map((n: NotificationRecordDto) => [
              n.id,
              n.userId,
              n.recipient,
              NOTIFICATION_CHANNEL_META[n.channel as NotificationChannel].label,
              n.templateCode,
              NOTIFICATION_STATUS_META[n.status as NotificationStatus].label,
              orDash(n.businessRef),
              String(n.retryCount ?? 0),
              orDash(n.createdAt),
            ]),
          }
        },
      }
    case 'Review':
      return {
        header: ['评价ID', '商品名称', '买家ID', '评分', '状态', '评价内容', '评价时间'],
        async fetchPage(page, pageSize) {
          const { data } = await reviewApi.list({
            page,
            pageSize,
            productName: keyword,
            status: status as ReviewStatus | undefined,
            fromTime,
            toTime,
          })
          return {
            total: data.total,
            rows: data.items.map((r) => [
              r.id,
              r.productName,
              r.userId,
              String(r.rating),
              REVIEW_STATUS_META[r.status].label,
              r.content,
              r.createdAt,
            ]),
          }
        },
      }
    case 'Seller':
      // 店铺列表端点不支持时间范围，时间筛选仅作为任务记录快照保留
      return {
        header: ['店铺ID', '店铺名称', '申请人', '主营类目', '在售商品数', '累计订单数', '评分', '状态', '提交时间'],
        async fetchPage(page, pageSize) {
          const { data } = await shopApi.list({
            page,
            pageSize,
            keyword,
            status: status as ShopStatus | undefined,
          })
          return {
            total: data.total,
            rows: data.items.map((s) => [
              s.id,
              s.name,
              s.ownerName,
              s.mainCategory,
              String(s.productCount),
              String(s.orderCount),
              s.rating.toFixed(1),
              SHOP_STATUS_LABELS[s.status],
              s.submittedAt,
            ]),
          }
        },
      }
    default: {
      // 类型穷举兜底：未知业务类型直接失败（不可能到达，TS 穷举保护）
      const neverType = businessType as never
      throw new Error(`不支持的导出业务类型：${String(neverType)}`)
    }
  }
}

/**
 * 分页同步拉取指定业务类型数据并序列化为 CSV 行
 *
 * - 按页循环拉取直到取完 total 或达到 maxRows（截断，truncated=true）
 * - 每页回调 onProgress(fetched, total) 供任务进度条更新
 */
export async function fetchExportRows(
  businessType: ExportBusinessType,
  options: ExportFetchOptions,
): Promise<ExportFetchResult> {
  const maxRows = options.maxRows ?? EXPORT_MAX_ROWS
  const pageSize = options.pageSize ?? EXPORT_PAGE_SIZE
  const source = createSource(businessType, options)

  const rows: string[][] = []
  let total = 0
  let truncated = false
  let page = 1

  for (;;) {
    const result = await source.fetchPage(page, pageSize)
    total = result.total
    if (result.rows.length === 0) break

    for (const row of result.rows) {
      if (rows.length >= maxRows) {
        truncated = true
        break
      }
      rows.push(row)
    }
    options.onProgress?.(rows.length, total)

    if (truncated) break
    if (rows.length >= total) break
    page += 1
  }

  return { header: source.header, rows, total, truncated }
}

// ---------- CSV 构建 ----------

/** CSV 单元格转义：含引号 / 逗号 / 换行时包裹双引号，内部双引号翻倍 */
export function csvEscape(value: string): string {
  const escaped = value.replace(/"/g, '""')
  return /[",\n\r]/.test(escaped) ? `"${escaped}"` : escaped
}

/** 组装 CSV 全文（表头 + 数据行，BOM 头 + CRLF 换行，Excel 兼容） */
export function buildCsv(header: string[], rows: string[][]): string {
  const lines = [header, ...rows].map((row) => row.map(csvEscape).join(','))
  return `\uFEFF${lines.join('\r\n')}`
}

/** 任务记录导出的默认文件名（业务类型 + 时间戳） */
export function buildExportFileName(record: ExportTaskRecord): string {
  const label = EXPORT_BUSINESS_TYPE_LABELS[record.businessType]
  return `${label}导出_${record.createdAt.replace(/[:.]/g, '-')}.csv`
}

// ---------- 任务历史 localStorage 持久化 ----------

const EXPORT_TASKS_STORAGE_KEY = 'operations.data-export.tasks'

/** 解析 localStorage 中 JSON 数组（损坏时返回空数组并清理） */
function parseStoredTasks(): ExportTaskRecord[] {
  try {
    const raw = localStorage.getItem(EXPORT_TASKS_STORAGE_KEY)
    if (!raw) return []
    const parsed = JSON.parse(raw) as unknown
    if (!Array.isArray(parsed)) return []
    return parsed.filter(
      (t): t is ExportTaskRecord =>
        !!t && typeof t === 'object' && 'id' in t && 'businessType' in t && 'status' in t,
    )
  } catch {
    return []
  }
}

/** 读取全部任务（按创建时间倒序，最近在前） */
export function loadExportTasks(): ExportTaskRecord[] {
  return parseStoredTasks().sort((a, b) => b.createdAt.localeCompare(a.createdAt))
}

/**
 * 持久化任务列表
 *
 * 存储配额不足时降级：剔除 csv 大字段后重试，仍失败则放弃本次持久化
 * （内存态任务列表仍可用，刷新后历史任务丢失可接受）。
 */
export function saveExportTasks(tasks: ExportTaskRecord[]): void {
  try {
    localStorage.setItem(EXPORT_TASKS_STORAGE_KEY, JSON.stringify(tasks))
  } catch {
    const slimmed = tasks.map((t) => ({ ...t, csv: '' }))
    try {
      localStorage.setItem(EXPORT_TASKS_STORAGE_KEY, JSON.stringify(slimmed))
    } catch {
      // 配额仍不足：放弃持久化，保留内存态
    }
  }
}

/** 新增任务记录（置于列表头部后持久化） */
export function addExportTask(record: ExportTaskRecord): ExportTaskRecord[] {
  const next = [record, ...parseStoredTasks()]
  saveExportTasks(next)
  return next.sort((a, b) => b.createdAt.localeCompare(a.createdAt))
}

/** 更新任务记录（按 id 原位替换后持久化） */
export function updateExportTask(record: ExportTaskRecord): ExportTaskRecord[] {
  const next = parseStoredTasks().map((t) => (t.id === record.id ? record : t))
  saveExportTasks(next)
  return next.sort((a, b) => b.createdAt.localeCompare(a.createdAt))
}

/** 删除任务记录（含其导出文件内容） */
export function removeExportTask(id: string): ExportTaskRecord[] {
  const next = parseStoredTasks().filter((t) => t.id !== id)
  saveExportTasks(next)
  return next.sort((a, b) => b.createdAt.localeCompare(a.createdAt))
}

/**
 * 清理过期任务（创建时间早于保留天数，文件 7 天过期口径）
 *
 * 返回清理后的存活任务列表（创建时间倒序）；有过期任务被清除时同步持久化。
 */
export function clearExpiredExportTasks(now: Date = new Date()): ExportTaskRecord[] {
  const expiresBefore = now.getTime() - EXPORT_RETENTION_DAYS * 24 * 60 * 60 * 1000
  const tasks = parseStoredTasks()
  const survivors = tasks.filter((t) => new Date(t.createdAt).getTime() >= expiresBefore)
  if (survivors.length !== tasks.length) {
    saveExportTasks(survivors)
  }
  return survivors.sort((a, b) => b.createdAt.localeCompare(a.createdAt))
}

/** 同业务类型同时间范围 5 分钟内是否已存在任务（防重复创建） */
export function hasRecentDuplicate(
  businessType: ExportBusinessType,
  fromTime: string,
  toTime: string,
  now: Date = new Date(),
): boolean {
  const windowStart = now.getTime() - 5 * 60 * 1000
  return parseStoredTasks().some(
    (t) =>
      t.businessType === businessType &&
      t.fromTime === fromTime &&
      t.toTime === toTime &&
      new Date(t.createdAt).getTime() >= windowStart,
  )
}

/**
 * 触发浏览器下载 CSV 文件
 *
 * csv 为空（过期 / 配额降级）时不触发下载并返回 false，由调用方提示重建任务。
 */
export function downloadTaskCsv(record: ExportTaskRecord): boolean {
  if (!record.csv || record.csv.length === 0) return false

  const blob = new Blob([record.csv], { type: 'text/csv;charset=utf-8;' })
  const url = URL.createObjectURL(blob)
  const link = document.createElement('a')
  link.href = url
  link.download = buildExportFileName(record)
  document.body.appendChild(link)
  link.click()
  document.body.removeChild(link)
  URL.revokeObjectURL(url)
  return true
}

/**
 * 生成任务 ID：EXP- 前缀 + 时间戳 + 随机段（本地记录用，无后端全局唯一要求）
 */
export function generateExportTaskId(now: Date = new Date()): string {
  const stamp = now.getTime().toString(36)
  const rand = Math.floor(Math.random() * 0xfffff).toString(36).padStart(4, '0')
  return `EXP-${stamp}-${rand}`
}
