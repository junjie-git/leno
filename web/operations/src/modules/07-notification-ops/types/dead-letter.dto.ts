import type { PageQuery } from '@/shared/types'
import type { NotificationChannel } from './template.dto'
import type { NotificationStatusTransitionDto } from './record.dto'

/**
 * 死信管理域 DTO
 *
 * 与 Notification 域 DeadLettersController 契约对齐：
 * - 列表固定按 Status = DeadLetter 过滤（前端无需传 status）
 * - 批量重发 / 批量丢弃基于记录状态校验，仅 DeadLetter 可操作
 * - 部分失败返回失败原因清单，失败记录保留选中态便于二次操作
 */

/** 死信记录状态（本页列表固定 DeadLetter；丢弃后进入终态 Discarded 离开本视图） */
export type DeadLetterStatus = 'DeadLetter' | 'Discarded'

/** 重试历史节点 */
export interface DeadLetterRetryAttemptDto {
  /** 第几次重试（从 1 开始） */
  attemptNo: number
  /** 重试时间（ISO 8601 UTC） */
  at: string
  /** 该次重试错误码 */
  errorCode?: string
  /** 该次重试错误消息 */
  errorMessage?: string
}

/** 死信记录（列表项与详情共用；正文 / 时间线 / 重试历史详情返回） */
export interface DeadLetterRecordDto {
  recordId: string
  /** 接收用户 ID */
  userId: string
  /** 脱敏接收人（如 138****1234） */
  recipient?: string
  templateCode: string
  channel: NotificationChannel
  /** 渲染后标题 */
  title?: string
  /** 渲染后正文（保留换行） */
  content?: string
  /** 后端固定返回 DeadLetter */
  status: DeadLetterStatus
  /** 已重试次数（≥3 橙色提示） */
  retryCount: number
  /** 最后一次失败错误码（如 TIMEOUT / SMTP_535） */
  errorCode?: string
  /** 最后一次失败错误消息 */
  errorMessage?: string
  /** 最后失败时间（ISO 8601 UTC，列表默认倒序键） */
  failedAt: string
  createdAt?: string
  /** 状态变更时间线（详情，按时间倒序） */
  timeline?: NotificationStatusTransitionDto[]
  /** 重试历史（详情，按时间倒序） */
  retryHistory?: DeadLetterRetryAttemptDto[]
}

/** 死信查询参数（channel / templateCode / 失败时间范围 + 分页；Status 固定 DeadLetter） */
export interface DeadLetterQueryParams extends PageQuery {
  channel?: NotificationChannel
  templateCode?: string
  fromTime?: string
  toTime?: string
}

/** 批量重发请求体（BatchDeadLetterRequestDto） */
export interface BatchDeadLetterResendDto {
  /** 待重发记录标识列表（单次最多 100 条） */
  recordIds: string[]
}

/** 批量丢弃请求体（含丢弃原因，用于审计） */
export interface BatchDeadLetterDiscardDto {
  recordIds: string[]
  /** 丢弃原因（建议必填，最少 10 字符） */
  discardReason: string
}

/** 批量操作结果（BatchOperationResultDto：成功 / 失败计数 + 失败原因清单） */
export interface NotificationBatchResultDto {
  successCount: number
  failureCount: number
  /** 失败原因清单（与 RecordIds 顺序对应） */
  errors: string[]
}
