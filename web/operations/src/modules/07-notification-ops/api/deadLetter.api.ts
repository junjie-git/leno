import type { AxiosResponse } from 'axios'
import { client, withIdempotency } from '@/shared/http'
import type { PageResult } from '@/shared/types'
import type {
  BatchDeadLetterDiscardDto,
  BatchDeadLetterResendDto,
  DeadLetterQueryParams,
  DeadLetterRecordDto,
  NotificationBatchResultDto,
} from '../types/dead-letter.dto'

/**
 * 死信管理 API
 *
 * 与 Notification 域 DeadLettersController 对接（baseURL 已含 /api）：
 * - 列表固定按 Status = DeadLetter 过滤（前端无需传 status）
 * - 批量重发 / 批量丢弃单次最多 100 条，部分失败返回失败原因清单
 */
export const deadLetterApi = {
  /**
   * 分页查询死信列表（按 FailedAt 倒序，渠道 / 模板编码 / 失败时间范围筛选）
   */
  list(params: DeadLetterQueryParams): Promise<AxiosResponse<PageResult<DeadLetterRecordDto>>> {
    return client.get<PageResult<DeadLetterRecordDto>>('/admin/dead-letters', { params })
  },

  /**
   * 批量重发死信（触发再次投递，可能产生渠道费用）
   */
  batchResend(body: BatchDeadLetterResendDto): Promise<AxiosResponse<NotificationBatchResultDto>> {
    return client.post<NotificationBatchResultDto>('/admin/dead-letters/batch-resend', body, withIdempotency())
  },

  /**
   * 批量丢弃死信（终态不可恢复，丢弃原因用于审计）
   */
  batchDiscard(
    body: BatchDeadLetterDiscardDto,
  ): Promise<AxiosResponse<NotificationBatchResultDto>> {
    return client.post<NotificationBatchResultDto>('/admin/dead-letters/batch-discard', body, withIdempotency())
  },
}
