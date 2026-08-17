import type { PageQuery } from '@/shared/types'

/**
 * 05-order-ops 售后处理 DTO
 *
 * 对接 AfterSales 域 AdminAfterSalesController（旧域 ReviewAfterSales 双轨兜底，端点不变）：
 * - GET  /api/admin/after-sales                    分页查询全平台售后单
 * - POST /api/admin/after-sales/{id}/approve       运营审核通过（触发退款流程）
 * - POST /api/admin/after-sales/{id}/reject        运营驳回（原因必填）
 *
 * 状态机：Pending（待审核）→ SellerApproved（卖家同意）→ ReturnShipping（退货中）
 *         → SellerReceived（卖家收货）→ 运营介入 AdminApproved / AdminRejected
 *         → Refunded（已退款）/ Rejected（已驳回）。
 */

/** 售后类型：仅退款 / 退货退款 / 换货 */
export type AfterSalesType = 'RefundOnly' | 'ReturnRefund' | 'Exchange'

/** 售后类型展示元数据 */
export const AFTER_SALES_TYPE_META: Record<AfterSalesType, { label: string; color: string }> = {
  RefundOnly: { label: '仅退款', color: 'warning' },
  ReturnRefund: { label: '退货退款', color: 'processing' },
  Exchange: { label: '换货', color: 'purple' },
}

/** 售后状态（与后端 AfterSalesStatus 枚举对齐） */
export type AfterSalesStatus =
  | 'Pending'
  | 'SellerApproved'
  | 'ReturnShipping'
  | 'SellerReceived'
  | 'AdminApproved'
  | 'AdminRejected'
  | 'Refunded'
  | 'Rejected'

/** 售后状态展示元数据（md §6 状态色：待审核橙 / 待介入紫 / 已退款绿 / 已驳回红） */
export const AFTER_SALES_STATUS_META: Record<AfterSalesStatus, { label: string; color: string }> = {
  Pending: { label: '待审核', color: 'warning' },
  SellerApproved: { label: '卖家已同意', color: 'processing' },
  ReturnShipping: { label: '退货中', color: 'processing' },
  SellerReceived: { label: '卖家已收货', color: 'geekblue' },
  AdminApproved: { label: '运营已通过', color: 'success' },
  AdminRejected: { label: '运营已驳回', color: 'error' },
  Refunded: { label: '已退款', color: 'success' },
  Rejected: { label: '已驳回', color: 'error' },
}

/** 可运营介入（通过 / 驳回）的售后状态：未终态前均可介入 */
export const AUDITABLE_AFTER_SALES_STATUSES: AfterSalesStatus[] = [
  'Pending',
  'SellerApproved',
  'ReturnShipping',
  'SellerReceived',
]

/** 协商记录节点（详情抽屉时间线数据源） */
export interface NegotiationRecordDto {
  /** 角色：Buyer 买家 / Seller 卖家 / Operator 运营 / System 系统 */
  role: 'Buyer' | 'Seller' | 'Operator' | 'System'
  /** 动作，如「发起售后申请」「同意退货」「运营介入通过」 */
  action: string
  /** 详细说明，可选 */
  content?: string
  /** 操作时间（ISO 8601） */
  createdAt: string
}

/** 协商角色展示元数据 */
export const NEGOTIATION_ROLE_META: Record<NegotiationRecordDto['role'], { label: string; color: string }> = {
  Buyer: { label: '买家', color: 'blue' },
  Seller: { label: '卖家', color: 'purple' },
  Operator: { label: '运营', color: 'orange' },
  System: { label: '系统', color: 'gray' },
}

/**
 * 售后单视图（列表行与详情抽屉共用）
 *
 * 列表端点保证基础字段；凭证图片 / 协商记录为详情扩展字段。
 */
export interface AfterSalesDto {
  id: string
  /** 售后单号（AS 前缀，mono 展示） */
  afterSalesNo: string
  /** 关联订单 ID */
  orderId: string
  /** 关联订单号，可选 */
  orderNo?: string
  /** 买家用户 ID */
  userId: string
  /** 买家昵称，可选 */
  buyerName?: string
  /** 卖家 ID */
  sellerId: string
  /** 店铺名称，可选 */
  sellerName?: string
  /** 售后类型 */
  type: AfterSalesType
  status: AfterSalesStatus
  /** 申请金额（元） */
  applyAmount: number
  /** 申请原因 */
  reason: string
  /** 售后商品 ID，可选 */
  productId?: string
  /** 售后商品名称，可选 */
  productName?: string
  /** 售后数量，可选 */
  quantity?: number
  /** 申请时间（ISO 8601） */
  createdAt: string
  /** 凭证图片 URL 列表（详情扩展字段） */
  evidenceImageUrls?: string[]
  /** 协商记录（详情扩展字段） */
  negotiationRecords?: NegotiationRecordDto[]
}

/** GET /api/admin/after-sales 查询参数 */
export interface AfterSalesQueryParams extends PageQuery {
  /** 售后单号模糊匹配 */
  afterSalesNo?: string
  /** 关联订单 ID */
  orderId?: string
  /** 买家用户 ID */
  userId?: string
  /** 卖家 ID */
  sellerId?: string
  /** 售后状态 */
  status?: AfterSalesStatus
  /** 售后类型 */
  type?: AfterSalesType
  /** 申请时间下界（ISO 8601 UTC） */
  fromTime?: string
  /** 申请时间上界（ISO 8601 UTC） */
  toTime?: string
}

/** 审核通过请求体（ApproveAfterSalesDto）：approvedAmount 默认为申请金额，0 < 金额 ≤ 申请金额 */
export interface ApproveAfterSalesDto {
  /** 审核金额（元），缺省按申请金额全额退款 */
  approvedAmount?: number
  /** 备注，可选 */
  remark?: string
}

/** 驳回请求体（RejectAfterSalesDto）：reason 必填（≥5 字） */
export interface RejectAfterSalesDto {
  reason: string
}
