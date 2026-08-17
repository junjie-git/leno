import type { PageQuery } from '@/shared/types'
import type { NotificationChannel } from './template.dto'

/**
 * 通知记录域 DTO
 *
 * 与 Notification 域通知记录查询端点契约对齐：
 * - 列表：/notifications/records 多维度筛选 + 分页
 * - 详情：/notifications/records/{id} 含渲染正文、渠道返回与状态时间线
 * - 统计：/admin/notifications/statistics 各状态计数与送达率
 */

/** 通知状态机：Pending → Sending → Sent → Delivered / Failed → DeadLetter（→ 重发回 Pending / 丢弃 Discarded） */
export type NotificationStatus =
  | 'Pending'
  | 'Sending'
  | 'Sent'
  | 'Delivered'
  | 'Failed'
  | 'DeadLetter'
  | 'Discarded'

/** 通知状态展示元数据（records.md §6 状态色） */
export const NOTIFICATION_STATUS_META: Record<NotificationStatus, { label: string; color: string }> = {
  Pending: { label: '待发送', color: 'default' },
  Sending: { label: '发送中', color: 'processing' },
  Sent: { label: '已发送', color: 'blue' },
  Delivered: { label: '已送达', color: 'success' },
  Failed: { label: '失败', color: 'warning' },
  DeadLetter: { label: '死信', color: 'error' },
  Discarded: { label: '已丢弃', color: 'default' },
}

/** 状态变更时间线节点 */
export interface NotificationStatusTransitionDto {
  /** 变更后状态 */
  status: NotificationStatus | string
  /** 变更时间（ISO 8601 UTC） */
  at: string
  /** 变更说明（如渠道返回 / DispatchJob 接管） */
  detail?: string
}

/** 通知记录（列表项与详情共用；渲染正文 / 渠道返回 / 时间线仅详情返回） */
export interface NotificationRecordDto {
  id: string
  /** 接收用户 ID */
  userId: string
  /** 脱敏接收人（如 138****1234 / a***@example.com） */
  recipient: string
  channel: NotificationChannel
  templateCode: string
  status: NotificationStatus
  /** 业务引用（如订单号），跨端跳转用 */
  businessRef?: string
  /** 渲染后标题（详情） */
  title?: string
  /** 渲染后正文（详情，保留换行） */
  content?: string
  /** 渠道原始返回（详情，JSON 结构） */
  providerResponse?: unknown
  /** 已重试次数（>3 标红提示） */
  retryCount: number
  sentAt?: string
  deliveredAt?: string
  createdAt?: string
  /** 状态变更时间线（详情，按时间倒序） */
  timeline?: NotificationStatusTransitionDto[]
}

/** 通知记录查询参数（userId / channel / status / templateCode / businessRef / 时间范围 + 分页） */
export interface NotificationRecordQueryParams extends PageQuery {
  userId?: string
  channel?: NotificationChannel
  status?: NotificationStatus
  templateCode?: string
  businessRef?: string
  fromTime?: string
  toTime?: string
}

/** 送达率统计（GET /admin/notifications/statistics） */
export interface NotificationStatisticsDto {
  pendingCount: number
  sendingCount: number
  sentCount: number
  deliveredCount: number
  failedCount: number
  deadLetterCount: number
  /** 送达率（0-1 小数，展示时转百分比） */
  deliveryRate: number
}
