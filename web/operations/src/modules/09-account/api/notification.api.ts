import { client, withIdempotency } from '@/shared/http'
import type {
  ListNotificationsParams,
  MarkAsReadDto,
  NotificationListResultDto,
  UnreadCountResultDto,
} from '../types/account.dto'

/**
 * 通知中心 API（Notification 域）
 *
 * - GET  /api/notifications                分页查询我的站内信（isRead/type 筛选）
 * - GET  /api/notifications/unread-count   未读计数
 * - POST /api/notifications/read           按记录标识批量标记已读（幂等）
 * - POST /api/notifications/read-all       全部标记已读（幂等）
 *
 * 响应拦截器已解包 ApiResponse.data，调用方拿到的就是业务负载。
 */
export const notificationApi = {
  /**
   * 分页查询我的站内信
   *
   * @param params page/pageSize/isRead/type
   */
  list(params: ListNotificationsParams): Promise<NotificationListResultDto> {
    return client
      .get<NotificationListResultDto>('/notifications', { params })
      .then((r) => r.data)
  },

  /**
   * 获取未读计数（Header 铃铛与工具栏徽标共用）
   */
  getUnreadCount(): Promise<UnreadCountResultDto> {
    return client.get<UnreadCountResultDto>('/notifications/unread-count').then((r) => r.data)
  },

  /**
   * 批量标记已读（重复调用幂等，无副作用）
   */
  markAsRead(body: MarkAsReadDto): Promise<void> {
    return client
      .post<void>('/notifications/read', body, withIdempotency())
      .then(() => undefined)
  },

  /**
   * 全部标记已读
   */
  markAllAsRead(): Promise<void> {
    return client
      .post<void>('/notifications/read-all', null, withIdempotency())
      .then(() => undefined)
  },
}
