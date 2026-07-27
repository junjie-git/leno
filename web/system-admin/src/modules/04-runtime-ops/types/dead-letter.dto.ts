// web/system-admin/src/modules/04-runtime-ops/types/dead-letter.dto.ts
// 死信消息 + 批量结果 + 丢弃 DTO，对齐 SystemAdmin BC DeadLetterController 契约

/** 死信状态：待处理 / 已重投 / 已丢弃 */
export type DeadLetterStatus = 'Pending' | 'Retried' | 'Discarded'

/** 死信消息响应 DTO（spec §3.6 + design-prompt §3） */
export interface DeadLetterMessageDto {
  messageId: string
  originalMessageId: string
  sourceContext: string
  originalTopic: string
  originalQueue: string
  deadLetterQueue: string
  payload: string
  headers: Record<string, unknown>
  errorReason: string
  failedAt: string
  retryCount: number
  status: DeadLetterStatus
  operatorId: string | null
  operatedAt: string | null
  discardReason: string | null
  /** 处置历史，按时间倒序 */
  history: DeadLetterHistoryItemDto[]
}

/** 处置历史条目 */
export interface DeadLetterHistoryItemDto {
  action: 'Retry' | 'Discard' | 'EnterDeadLetter'
  operator: string | null
  operatedAt: string
  result: string
}

/** 丢弃请求 DTO（reason 必填） */
export interface DiscardDeadLetterDto {
  discardReason: string
}

/** 批量操作结果 DTO */
export interface BatchOperationResultDto {
  succeeded: string[]
  failed: { messageId: string; reason: string }[]
}

/** 列表查询参数 */
export interface ListDeadLettersParams {
  sourceContext?: string[]
  status?: DeadLetterStatus[]
  startTime?: string
  endTime?: string
  page?: number
  pageSize?: number
}
