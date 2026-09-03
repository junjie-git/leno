import type MockAdapter from 'axios-mock-adapter'
import type {
  NotificationPreferencesDto,
  NotificationType,
} from '@/modules/12-notification/types/notification.dto'
import { seedNotifications } from '../data/seed'
import { ok, paginate, parseBody, queryParams } from './helpers'

/**
 * 通知 handlers（Notification 域 + UserCenter 偏好）
 *
 * - GET  /notifications（type 筛选 + 分页）
 * - GET  /notifications/unread-count
 * - POST /notifications/read、/notifications/read-all
 * - GET/PUT /users/me/notification-preferences
 */

/** 通知偏好（演示默认值） */
const preferences: NotificationPreferencesDto = {
  inApp: true,
  sms: true,
  email: false,
  order: true,
  logistics: true,
  promotion: true,
  points: true,
  afterSales: true,
  system: true,
}

export function registerNotificationHandlers(mock: MockAdapter): void {
  // 通知列表
  mock.onGet('/notifications').reply((config) => {
    const params = queryParams(config)
    let list = [...seedNotifications]
    if (params.type) {
      list = list.filter((n) => n.type === (params.type as NotificationType))
    }
    return ok(paginate(list, Number(params.page ?? 1), Number(params.pageSize ?? 20)))
  })

  // 未读数
  mock.onGet('/notifications/unread-count').reply(
    () => ok(seedNotifications.filter((n) => !n.isRead).length),
  )

  // 标记已读（单条/批量）
  mock.onPost('/notifications/read').reply((config) => {
    const body = parseBody<{ ids: string[] }>(config.data)
    for (const id of body.ids ?? []) {
      const notification = seedNotifications.find((n) => n.id === id)
      if (notification) {
        notification.isRead = true
      }
    }
    return ok(null)
  })

  // 全部已读
  mock.onPost('/notifications/read-all').reply(() => {
    seedNotifications.forEach((n) => {
      n.isRead = true
    })
    return ok(null)
  })

  // 通知偏好
  mock.onGet('/users/me/notification-preferences').reply(() => ok(preferences))

  // 更新通知偏好
  mock.onPut('/users/me/notification-preferences').reply((config) => {
    const body = parseBody<NotificationPreferencesDto>(config.data)
    Object.assign(preferences, body)
    return ok({ ...preferences })
  })
}
