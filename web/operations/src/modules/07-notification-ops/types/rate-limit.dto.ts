import type { NotificationChannel } from './template.dto'

/**
 * 通知限流域 DTO
 *
 * 与 Notification 域 AdminNotificationRateLimitsController 契约对齐：
 * - 用户级：每用户每日 / 每小时上限
 * - 全局级：全平台每分钟 / 每小时上限
 * - 校验约束：用户每日 ≥ 用户每小时；全局每小时 ≥ 全局每分钟；用户级 ≤ 全局级对应维度
 */

/** 限流配置状态（Active ↔ Inactive；关闭限流为高危操作） */
export type RateLimitStatus = 'Active' | 'Inactive'

/** 限流状态展示元数据（rate-limits.md §6：启用绿点 / 禁用红点） */
export const RATE_LIMIT_STATUS_META: Record<RateLimitStatus, { label: string; color: string }> = {
  Active: { label: '已启用', color: 'success' },
  Inactive: { label: '已禁用', color: 'error' },
}

/** 当前周期用量（详情面板用量进度条数据源） */
export interface RateLimitUsageDto {
  /** 今日累计发送量（对全平台每小时上限的用量进度） */
  todayCount: number
  /** 当前小时累计发送量 */
  hourCount: number
  /** 当前分钟累计发送量 */
  minuteCount: number
}

/** 渠道限流配置 */
export interface RateLimitConfigDto {
  channel: NotificationChannel
  /** 每用户每日上限（1-100 正整数） */
  userDailyLimit: number
  /** 每用户每小时上限（1-20 正整数，且 ≤ 每日上限） */
  userHourlyLimit: number
  /** 全平台每分钟上限（10-10000 正整数） */
  globalPerMinuteLimit: number
  /** 全平台每小时上限（100-100000 正整数，且 ≥ 每分钟上限） */
  globalHourlyLimit: number
  /** 当前用量 */
  currentUsage: RateLimitUsageDto
  status: RateLimitStatus
  updatedBy?: string
  updatedAt?: string
}

/** 更新限流配置请求体（SaveRateLimitConfigDto） */
export interface SaveRateLimitConfigDto {
  channel: NotificationChannel
  userDailyLimit: number
  userHourlyLimit: number
  globalPerMinuteLimit: number
  globalHourlyLimit: number
  status: RateLimitStatus
}
