import { client } from '@/shared/http'
import type {
  NotificationDto,
  NotificationPreferencesDto,
  NotificationType,
} from '../types/notification.dto'
import type { PagedResult } from '@/shared/types'

/**
 * 通知 API（Notification 域列表 + UserCenter 域偏好）
 *
 * - GET  /notifications                 通知列表
 * - GET  /notifications/unread-count    未读数
 * - POST /notifications/read            标记已读
 * - POST /notifications/read-all        全部已读
 * - GET  /users/me/notification-preferences  通知偏好
 * - PUT  /users/me/notification-preferences  更新偏好
 *
 * 响应拦截器已解包 ApiResponse.data，调用方拿到的就是业务负载。
 */
export const notificationApi = {
  /** 通知列表（type 筛选 + 分页） */
  list(params?: { type?: NotificationType; page?: number; pageSize?: number }): Promise<PagedResult<NotificationDto>> {
    return client.get<PagedResult<NotificationDto>>('/notifications', { params }).then((r) => r.data)
  },

  /** 未读数（首页铃铛角标） */
  getUnreadCount(): Promise<number> {
    return client.get<number>('/notifications/unread-count').then((r) => r.data)
  },

  /** 标记已读（单条/批量） */
  markRead(ids: string[]): Promise<null> {
    return client.post<null>('/notifications/read', { ids }).then((r) => r.data)
  },

  /** 全部已读 */
  markAllRead(): Promise<null> {
    return client.post<null>('/notifications/read-all').then((r) => r.data)
  },

  /** 通知偏好 */
  getPreferences(): Promise<NotificationPreferencesDto> {
    return client.get<NotificationPreferencesDto>('/users/me/notification-preferences').then((r) => r.data)
  },

  /** 更新通知偏好 */
  updatePreferences(body: NotificationPreferencesDto): Promise<NotificationPreferencesDto> {
    return client
      .put<NotificationPreferencesDto>('/users/me/notification-preferences', body)
      .then((r) => r.data)
  },
}
