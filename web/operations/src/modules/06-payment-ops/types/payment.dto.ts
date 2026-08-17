import type { PageQuery, PageResult } from '@/shared/types'

/**
 * 06-payment-ops 支付记录 DTO
 *
 * 对接 Payment 域 AdminPaymentsController：
 * - GET /api/admin/payments 运营端分页查询全平台支付记录
 *
 * 状态机：Pending（待支付）→ Success（已支付）/ Failed（支付失败）；
 * Success → Refunded（已退款）。
 */

/** 支付状态（与后端 PaymentStatus 枚举对齐） */
export type PaymentStatus = 'Pending' | 'Success' | 'Failed' | 'Refunded'

/** 支付渠道枚举（与后端 PaymentChannel 枚举对齐） */
export type PaymentChannelType = 'WeChat' | 'Alipay' | 'Other'

/** 渠道参数快照键值对（如 AppId / MchId / OpenId / PrepayId） */
export type PaymentChannelParams = Record<string, string>

/** 渠道回调记录条目（详情抽屉回调记录列表数据源） */
export interface PaymentCallbackLogDto {
  id: string
  /** 事件名，如「渠道回调到达」「回调处理完成」 */
  event: string
  /** 是否处理成功 */
  success: boolean
  /** 处理说明（含渠道返回码等） */
  detail?: string
  /** 渠道原始报文（JsonViewer 展示） */
  payload?: Record<string, unknown>
  /** 接收时间（ISO 8601） */
  receivedAt: string
}

/** 支付状态时间线条目（详情抽屉 a-timeline 数据源） */
export interface PaymentTimelineItemDto {
  /** 节点状态（可为 PaymentStatus 或扩展标记，如 Created） */
  status: string
  /** 节点标题 */
  label: string
  /** 节点描述（可选） */
  description?: string
  /** 发生时间（ISO 8601） */
  occurredAt: string
}

/** 支付记录视图，列表行与详情抽屉共用 */
export interface PaymentDto {
  id: string
  /** 支付单号 */
  paymentNo: string
  /** 订单 ID */
  orderId: string
  /** 订单编号（展示用） */
  orderNo?: string
  /** 买家用户 ID */
  userId: string
  /** 买家昵称（展示用） */
  userName?: string
  /** 支付金额（元） */
  amount: number
  /** 支付渠道 */
  channel: PaymentChannelType
  /** 支付状态 */
  status: PaymentStatus
  /** 渠道流水号 */
  channelTradeNo?: string
  /** 创建时间（ISO 8601） */
  createdAt: string
  /** 支付完成时间 */
  paidAt?: string
  /** 已退款时关联的售后单号（用于跳转售后处理） */
  afterSalesNo?: string
  /**
   * 异常标记：已支付（Success）但订单状态未变更 / 回调超时未处理等，
   * 为 true 时列表行标红并提供排查入口
   */
  abnormal: boolean
  /** 异常原因说明 */
  abnormalReason?: string
  /** 渠道参数快照（详情抽屉 JsonViewer 展示） */
  channelParams?: PaymentChannelParams
  /** 回调记录列表（详情抽屉展示） */
  callbackLogs?: PaymentCallbackLogDto[]
  /** 状态时间线（详情抽屉展示；后端缺失时前端按时间字段合成） */
  timeline?: PaymentTimelineItemDto[]
}

/** GET /api/admin/payments 查询参数 */
export interface PaymentQueryParams extends PageQuery {
  /** 支付单号（精确 / 模糊匹配） */
  paymentNo?: string
  /** 订单 ID / 订单编号 */
  orderId?: string
  /** 买家用户 ID */
  userId?: string
  /** 支付渠道 */
  channel?: PaymentChannelType
  /** 支付状态 */
  status?: PaymentStatus
  /** 创建时间下界（ISO 8601） */
  fromTime?: string
  /** 创建时间上界（ISO 8601） */
  toTime?: string
}

/** GET /api/admin/payments 响应（PaymentListResultDto） */
export interface PaymentListResultDto extends PageResult<PaymentDto> {
  /** 各状态计数（统计概览卡数据源） */
  statusCounts: Record<PaymentStatus, number>
  /** 支付成功率（0-1 小数，如 0.985 表示 98.5%） */
  successRate: number
}
