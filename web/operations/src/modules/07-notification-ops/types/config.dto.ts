import type { NotificationChannel } from './template.dto'

/**
 * 通知渠道配置域 DTO
 *
 * 与 Notification 域 AdminNotificationConfigController 契约对齐：
 * - GET 返回敏感字段脱敏值（如 LTAI**** / ********）
 * - PUT 提交配置项键值对，敏感项留空（空串 / 缺省）表示不修改
 * - POST /test 触发渠道测试发送并返回渠道响应
 */

/** 渠道配置项（Value 为脱敏展示值） */
export interface NotificationConfigItemDto {
  /** 配置键（如 AccessKeyId / SignName / SmtpHost） */
  key: string
  /** 配置值（敏感项后端脱敏返回，如 LTAI****） */
  value: string
  /** 是否敏感项（编辑时留空表示不修改） */
  isSensitive: boolean
  /** 配置项说明（右侧详情展示） */
  description?: string
}

/** 渠道配置详情 */
export interface NotificationConfigDto {
  channel: NotificationChannel
  /** 配置项列表（空列表视为「未配置」） */
  configs: NotificationConfigItemDto[]
  updatedBy?: string
  updatedAt?: string
}

/** 更新渠道配置请求体（configs 为配置项键值对；敏感项空串 / 缺省表示不修改） */
export interface SaveNotificationConfigDto {
  channel: NotificationChannel
  configs: Record<string, string>
}

/** 测试发送请求体（TestSendRequestDto） */
export interface TestNotificationConfigDto {
  channel: NotificationChannel
  /** 测试接收人（手机号 / 邮箱 / 用户标识） */
  recipient: string
  /** 测试内容 */
  content: string
}

/** 测试发送结果（TestSendResultDto） */
export interface TestSendResultDto {
  success: boolean
  /** 结果说明（成功 / 失败原因，如 API Key 无效） */
  message: string
  /** 渠道原始响应（JSON 结构） */
  providerResponse?: unknown
}
