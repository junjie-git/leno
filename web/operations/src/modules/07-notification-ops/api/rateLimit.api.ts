import type { AxiosResponse } from 'axios'
import { client, withIdempotency } from '@/shared/http'
import type { RateLimitConfigDto, SaveRateLimitConfigDto } from '../types/rate-limit.dto'
import type { NotificationChannel } from '../types/template.dto'

/**
 * 通知限流 API
 *
 * 与 Notification 域 AdminNotificationRateLimitsController 对接（baseURL 已含 /api）：
 * - GET 按渠道返回限流规则与当前用量
 * - PUT 更新限流规则（阈值校验：用户级不超全局级、每小时不超每日、正整数）
 */
export const rateLimitApi = {
  /**
   * 获取指定渠道频率限制配置（含当前用量）
   */
  get(channel: NotificationChannel): Promise<AxiosResponse<RateLimitConfigDto>> {
    return client.get<RateLimitConfigDto>('/admin/notification-rate-limits', { params: { channel } })
  },

  /**
   * 更新指定渠道频率限制配置
   */
  update(body: SaveRateLimitConfigDto): Promise<AxiosResponse<RateLimitConfigDto>> {
    return client.put<RateLimitConfigDto>('/admin/notification-rate-limits', body, withIdempotency())
  },
}
