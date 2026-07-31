import { http } from '@/shared/http'
import type {
  NotificationListParams,
  NotificationListResultDto,
} from '../types/notification.dto'

/**
 * 通知 API 客户端
 *
 * 与后端 Notification BC 对接（BE-4 已就绪）。响应拦截器已解包 ApiResponse.data。
 * - GET  /notifications            列表
 * - GET  /notifications/unread-count 未读计数
 * - POST /notifications/read        批量标记已读
 * - POST /notifications/read-all    全部标记已读
 */
export const notificationApi = {
  /** 查询通知列表（isRead 可空表示全部） */
  list(params: NotificationListParams): Promise<NotificationListResultDto> {
    const { isRead, page = 1, pageSize = 20 } = params
    return http
      .get<NotificationListResultDto>('/notifications', {
        params: { isRead, page, pageSize },
      })
      .then((r) => r.data)
  },

  /** 获取未读计数 */
  getUnreadCount(): Promise<number> {
    return http.get<number>('/notifications/unread-count').then((r) => r.data)
  },

  /** 批量标记已读 */
  markAsRead(recordIds: string[]): Promise<void> {
    return http
      .post<void>('/notifications/read', { recordIds })
      .then((r) => r.data)
  },

  /** 全部标记已读 */
  markAllAsRead(): Promise<void> {
    return http.post<void>('/notifications/read-all').then((r) => r.data)
  },
}
