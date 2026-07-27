// web/system-admin/src/modules/05-audit/types/outbox.dto.ts
// Outbox 汇总 + 趋势 + 消息 + 归档 DTO 与枚举，对齐 SystemAdmin BC OutboxMonitorController 契约
// 注：design-prompt 标 🚧 规划中，端点待后端实现；DTO 与 API 层先按 design-prompt §3 完整定义

/** Outbox 域状态：正常 / 积压 / 严重积压 / 已归档（design-prompt §4 状态机） */
export type OutboxStatus = 'Normal' | 'Backlog' | 'Severe' | 'Archived'

/** Outbox 域积压汇总 DTO（按域分组表格数据源，design-prompt §3） */
export interface OutboxSummaryDto {
  /** 限界上下文（如 Order/Payment/Notification） */
  context: string
  /** 未发布事件数 */
  pendingCount: number
  /** 最早事件时间（ISO 8601 UTC） */
  oldestEventAt: string | null
  /** 最大积压时长（分钟） */
  maxAgeMinutes: number
  /** 最近归档时间（ISO 8601 UTC） */
  lastArchivedAt: string | null
  /** 域状态 */
  status: OutboxStatus
}

/** Outbox 积压趋势点 DTO（按时间×上下文，design-prompt §3） */
export interface OutboxTrendPointDto {
  /** 时间戳（ISO 8601 UTC） */
  timestamp: string
  /** 限界上下文 */
  context: string
  /** 该时刻积压事件数 */
  pendingCount: number
}

/** Outbox 积压事件消息 DTO（详情抽屉列表，design-prompt §3） */
export interface OutboxMessageDto {
  /** 事件 ID */
  messageId: string
  /** 聚合 ID */
  aggregateId: string
  /** 事件类型 */
  eventType: string
  /** 事件 Payload（JSON 字符串） */
  payload: string
  /** 创建时间（ISO 8601 UTC） */
  createdAt: string
  /** 重试次数 */
  retryCount: number
  /** 消息状态 */
  status: OutboxStatus
}

/** Outbox 归档历史条目 DTO */
export interface OutboxArchiveHistoryDto {
  /** 归档时间（ISO 8601 UTC） */
  archivedAt: string
  /** 归档事件数 */
  count: number
  /** 归档原因 */
  reason: string
  /** 操作人 */
  archivedBy: string
}

/** 批量重投请求 DTO（design-prompt §3 BatchRepublishDto） */
export interface BatchRepublishOutboxDto {
  /** 指定消息 ID 列表；为空则重投该域全部积压 */
  messageIds?: string[]
  /** 最大重投条数（不指定 messageIds 时生效） */
  maxCount?: number
}

/** 归档请求 DTO（design-prompt §3 ArchiveDto） */
export interface ArchiveOutboxDto {
  /** 归档阈值：积压时长超过此分钟数的事件归档 */
  olderThanMinutes: number
  /** 归档原因（必填） */
  reason: string
}

/** Outbox 积压趋势查询参数 */
export interface GetOutboxTrendParams {
  /** 趋势时间窗口（小时，默认 24） */
  hours?: number
}

/** Outbox 消息列表查询参数 */
export interface ListOutboxMessagesParams {
  /** 限界上下文（路径参数） */
  context: string
  page?: number
  pageSize?: number
}
