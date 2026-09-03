/**
 * 通知域 DTO（Notification 域 + UserCenter 域偏好端点）
 *
 * 端点契约：
 * - GET  /api/notifications                 通知列表（type 筛选 + 分页）
 * - GET  /api/notifications/unread-count    未读数（首页铃铛角标）
 * - POST /api/notifications/read            标记已读（单条/批量）
 * - POST /api/notifications/read-all        全部已读
 * - GET  /api/users/me/notification-preferences  通知偏好
 * - PUT  /api/users/me/notification-preferences  更新通知偏好
 */

/** 通知类型 */
export type NotificationType = 'Order' | 'Logistics' | 'Promotion' | 'Points' | 'AfterSales' | 'System'

/** 通知条目 */
export interface NotificationDto {
  id: string
  type: NotificationType
  title: string
  content: string
  isRead: boolean
  createdAt: string
  /** 点击跳转相对路径（如 /order/xxx），为空则不跳转 */
  linkUrl?: string
}

/** 通知偏好（渠道开关 + 分类开关） */
export interface NotificationPreferencesDto {
  /** 站内通知渠道 */
  inApp: boolean
  /** 短信渠道 */
  sms: boolean
  /** 邮件渠道 */
  email: boolean
  /** 分类开关 */
  order: boolean
  logistics: boolean
  promotion: boolean
  points: boolean
  afterSales: boolean
  system: boolean
}
