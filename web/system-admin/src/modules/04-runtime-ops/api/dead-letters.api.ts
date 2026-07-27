// web/system-admin/src/modules/04-runtime-ops/api/dead-letters.api.ts
// 死信队列 API：对齐 SystemAdmin BC DeadLetterController 端点
// 写操作（retry/discard/batchRetry/batchDiscard）均注入 Idempotency-Key 头

import { client, withIdempotency } from '@/shared/http'
import type { PageResult, PageQuery } from '@/shared/types'
import type {
  DeadLetterMessageDto,
  DiscardDeadLetterDto,
  BatchOperationResultDto,
  ListDeadLettersParams,
} from '../types/dead-letter.dto'

/** 死信列表查询参数（合并 PageQuery） */
export type ListDeadLettersRequest = ListDeadLettersParams & PageQuery

export const deadLetterApi = {
  /** 分页查询死信消息 */
  list: (params: ListDeadLettersRequest) =>
    client.get<PageResult<DeadLetterMessageDto>>('/admin/dead-letters', { params }),

  /** 获取死信消息详情 */
  get: (id: string) =>
    client.get<DeadLetterMessageDto>(`/admin/dead-letters/${id}`),

  /** 重投指定死信消息（幂等） */
  retry: (id: string) =>
    client.post<DeadLetterMessageDto>(`/admin/dead-letters/${id}/retry`, null, withIdempotency()),

  /** 丢弃指定死信消息（reason 必填，幂等） */
  discard: (id: string, body: DiscardDeadLetterDto) =>
    client.post<DeadLetterMessageDto>(`/admin/dead-letters/${id}/discard`, body, withIdempotency()),

  /** 批量重投死信消息（幂等） */
  batchRetry: (messageIds: string[]) =>
    client.post<BatchOperationResultDto>('/admin/dead-letters/batch-retry', { messageIds }, withIdempotency()),

  /** 批量丢弃死信消息（reason 必填，幂等） */
  batchDiscard: (messageIds: string[], reason: string) =>
    client.post<BatchOperationResultDto>(
      '/admin/dead-letters/batch-discard',
      { messageIds, discardReason: reason },
      withIdempotency(),
    ),
}
