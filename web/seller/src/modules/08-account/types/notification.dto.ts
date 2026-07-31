/**
 * 08-account 通知 DTO
 *
 * 与后端 Notification BC 对接（BE-4 后端已就绪）：
 * - GET  /api/notifications            通知列表（isRead/page/pageSize）
 * - GET  /api/notifications/unread-count 未读计数
 * - POST /api/notifications/read        批量标记已读（recordIds）
 * - POST /api/notifications/read-all    全部标记已读
 */

/** 通知渠道 */
export type NotificationChannel = 'InApp' | 'Email' | 'Sms'

/** 通知状态 */
export type NotificationStatus = 'Pending' | 'Sent' | 'Failed' | 'DeadLetter'

/** 通知记录 DTO */
export interface NotificationRecordDto {
  recordId: string
  userId: string
  templateCode: string
  channel: NotificationChannel
  title: string
  content: string
  status: NotificationStatus
  isRead: boolean
  sentAt?: string
  createdAt: string
}

/** 通知列表结果（后端 NotificationListResultDto，比 PageResult 多 unreadCount） */
export interface NotificationListResultDto {
  items: NotificationRecordDto[]
  total: number
  unreadCount: number
  page: number
  pageSize: number
}

/** 批量标记已读请求 */
export interface MarkAsReadDto {
  recordIds: string[]
}

/** 列表查询参数 */
export interface NotificationListParams {
  isRead?: boolean
  page?: number
  pageSize?: number
}
