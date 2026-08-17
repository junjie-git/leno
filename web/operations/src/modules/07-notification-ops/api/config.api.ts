import type { AxiosResponse } from 'axios'
import { client, withIdempotency } from '@/shared/http'
import type {
  NotificationConfigDto,
  SaveNotificationConfigDto,
  TestNotificationConfigDto,
  TestSendResultDto,
} from '../types/config.dto'
import type { NotificationChannel } from '../types/template.dto'

/**
 * 通知渠道配置 API
 *
 * 与 Notification 域 AdminNotificationConfigController 对接（baseURL 已含 /api）：
 * - GET 返回敏感字段脱敏值；PUT 敏感项空串 / 缺省表示不修改
 * - POST /test 触发渠道测试发送并返回渠道响应
 */
export const notificationConfigApi = {
  /**
   * 获取指定渠道配置（敏感字段脱敏返回）
   */
  get(channel: NotificationChannel): Promise<AxiosResponse<NotificationConfigDto>> {
    return client.get<NotificationConfigDto>('/admin/notification-config', { params: { channel } })
  },

  /**
   * 更新指定渠道配置（configs 键值对；敏感项空串 / 缺省跳过修改）
   */
  update(body: SaveNotificationConfigDto): Promise<AxiosResponse<NotificationConfigDto>> {
    return client.put<NotificationConfigDto>('/admin/notification-config', body, withIdempotency())
  },

  /**
   * 测试发送验证配置（返回渠道响应，用于展示成功 / 失败详情）
   */
  test(body: TestNotificationConfigDto): Promise<AxiosResponse<TestSendResultDto>> {
    return client.post<TestSendResultDto>('/admin/notification-config/test', body, withIdempotency())
  },
}
