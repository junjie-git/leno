import type { AxiosResponse } from 'axios'
import { client, withIdempotency } from '@/shared/http'
import type {
  ChannelConfigItemDto,
  UpdateChannelConfigDto,
} from '../types/channel.dto'

/**
 * 支付渠道配置 API
 *
 * 与 Payment 域 AdminPaymentChannelsController 对接（baseURL 已含 /api）：
 * - GET  /admin/payment-channels               获取所有渠道配置项列表
 * - GET  /admin/payment-channels/{id}          获取单个配置项详情
 * - PUT  /admin/payment-channels/{id}          更新配置项值（敏感字段传空字符串跳过）
 * - POST /admin/payment-channels/{id}/enable   启用配置项（幂等）
 * - POST /admin/payment-channels/{id}/disable  禁用配置项（幂等）
 *
 * md 未定义测试连接端点，本模块不提供 test 方法。
 */
export const channelApi = {
  /**
   * 获取所有渠道配置项列表（前端按 channel 分组渲染左侧渠道卡）
   */
  list(): Promise<AxiosResponse<ChannelConfigItemDto[]>> {
    return client.get<ChannelConfigItemDto[]>('/admin/payment-channels')
  },

  /**
   * 获取单个配置项详情
   */
  get(id: string): Promise<AxiosResponse<ChannelConfigItemDto>> {
    return client.get<ChannelConfigItemDto>(`/admin/payment-channels/${id}`)
  },

  /**
   * 更新配置项值（幂等）
   *
   * body.configs 为键值对：敏感字段传空字符串表示不修改原值，
   * 由后端识别空值并跳过；返回更新后的配置项（敏感值仍为脱敏值）。
   */
  update(id: string, body: UpdateChannelConfigDto): Promise<AxiosResponse<ChannelConfigItemDto>> {
    return client.put<ChannelConfigItemDto>(`/admin/payment-channels/${id}`, body, withIdempotency())
  },

  /**
   * 启用配置项（幂等）：Inactive → Active
   */
  enable(id: string): Promise<AxiosResponse<void>> {
    return client.post<void>(`/admin/payment-channels/${id}/enable`, null, withIdempotency())
  },

  /**
   * 禁用配置项（幂等）：Active → Inactive
   */
  disable(id: string): Promise<AxiosResponse<void>> {
    return client.post<void>(`/admin/payment-channels/${id}/disable`, null, withIdempotency())
  },
}
