import type { PageQuery, PageResult } from '@/shared/types'
import type { PaymentChannelType } from './payment.dto'

/**
 * 06-payment-ops 退款记录 DTO
 *
 * 对接 Payment 域 AdminRefundsController：
 * - GET /api/admin/refunds 运营端分页查询全平台退款记录
 *
 * 状态机：Pending（待退款）→ Refunded（已退款）/ Failed（退款失败）。
 * md 未定义退款重试端点，失败退款需人工处理，前端不提供重试入口。
 */

/** 退款状态（与后端 RefundStatus 枚举对齐） */
export type RefundStatus = 'Pending' | 'Refunded' | 'Failed'

/** 渠道回写信息键值对（如 refund_id / fund_change / 回写耗时） */
export type RefundChannelWriteBack = Record<string, unknown>

/** 退款状态时间线条目（详情抽屉 a-timeline 数据源） */
export interface RefundTimelineItemDto {
  /** 节点状态（可为 RefundStatus 或扩展标记，如 Requested） */
  status: string
  /** 节点标题 */
  label: string
  /** 节点描述（可选） */
  description?: string
  /** 发生时间（ISO 8601） */
  occurredAt: string
}

/** 退款记录视图，列表行与详情抽屉共用 */
export interface RefundDto {
  id: string
  /** 退款编号 */
  refundNo: string
  /** 订单 ID */
  orderId: string
  /** 订单编号（展示用） */
  orderNo?: string
  /** 买家用户 ID */
  userId: string
  /** 买家昵称（展示用） */
  userName?: string
  /** 退款金额（元） */
  amount: number
  /** 退款渠道（原路退回渠道） */
  channel: PaymentChannelType
  /** 退款状态 */
  status: RefundStatus
  /** 关联售后单 ID */
  afterSalesId?: string
  /** 关联售后单号（链接跳转售后处理） */
  afterSalesNo?: string
  /** 退款原因 */
  reason?: string
  /** 退款失败原因（Failed 时详情抽屉展示） */
  failReason?: string
  /** 申请时间（ISO 8601） */
  requestedAt: string
  /** 退款完成时间 */
  completedAt?: string
  /** 渠道回写信息（详情抽屉 JsonViewer 展示） */
  channelWriteBack?: RefundChannelWriteBack
  /** 状态时间线（详情抽屉展示；后端缺失时前端按时间字段合成） */
  timeline?: RefundTimelineItemDto[]
}

/** GET /api/admin/refunds 查询参数 */
export interface RefundQueryParams extends PageQuery {
  /** 退款编号（精确 / 模糊匹配） */
  refundNo?: string
  /** 订单 ID / 订单编号 */
  orderId?: string
  /** 退款状态 */
  status?: RefundStatus
  /** 申请时间下界（ISO 8601） */
  fromTime?: string
  /** 申请时间上界（ISO 8601） */
  toTime?: string
}

/** GET /api/admin/refunds 响应（RefundListResultDto） */
export interface RefundListResultDto extends PageResult<RefundDto> {
  /** 各状态计数（统计概览卡数据源） */
  statusCounts: Record<RefundStatus, number>
  /** 退款成功率（0-1 小数，如 0.818 表示 81.8%） */
  successRate: number
}
