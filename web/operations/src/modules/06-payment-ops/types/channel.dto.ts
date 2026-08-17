import type { PaymentChannelType } from './payment.dto'

/**
 * 06-payment-ops 支付渠道配置 DTO
 *
 * 对接 Payment 域 AdminPaymentChannelsController：
 * - GET  /api/admin/payment-channels            获取所有渠道配置项列表
 * - GET  /api/admin/payment-channels/{id}       获取单个配置项详情
 * - PUT  /api/admin/payment-channels/{id}       更新配置项值（敏感字段传空字符串表示不修改）
 * - POST /api/admin/payment-channels/{id}/enable  启用配置项
 * - POST /api/admin/payment-channels/{id}/disable 禁用配置项
 *
 * 状态机：Inactive ↔ Active（启用 / 禁用双向切换）。
 */

/** 渠道配置项状态 */
export type ChannelConfigStatus = 'Active' | 'Inactive'

/** 渠道配置项视图（列表与详情共用） */
export interface ChannelConfigItemDto {
  id: string
  /** 所属渠道 */
  channel: PaymentChannelType
  /** 配置键（如 AppId / MchId / ApiKey / NotifyUrl） */
  key: string
  /** 配置值（敏感字段为脱敏值，如 ••••1234） */
  value: string
  /** 是否敏感字段（脱敏展示，编辑留空表示不修改） */
  isSensitive: boolean
  /** 是否启用 */
  enabled: boolean
  /** 配置项说明 */
  description?: string
  /** 最后更新人 */
  updatedBy?: string
  /** 最后更新时间（ISO 8601） */
  updatedAt?: string
}

/**
 * PUT /api/admin/payment-channels/{id} 请求体（UpdatePaymentChannelConfigDto）
 *
 * configs 为配置项键值对：{ AppId: 'wx1234', ApiKey: '' }。
 * 敏感字段传空字符串表示不修改原值（后端跳过空值）。
 */
export interface UpdateChannelConfigDto {
  configs: Record<string, string>
  /** 可选：同步更新配置项说明 */
  description?: string
}
