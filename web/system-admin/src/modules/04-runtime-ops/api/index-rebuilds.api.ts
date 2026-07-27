// web/system-admin/src/modules/04-runtime-ops/api/index-rebuilds.api.ts
// 索引重建 API：对齐 SystemAdmin BC IndexRebuildController 端点
// trigger/retry 均注入 Idempotency-Key 头

import { client, withIdempotency } from '@/shared/http'
import type { PageResult, PageQuery } from '@/shared/types'
import type {
  IndexRebuildTaskDto,
  TriggerIndexRebuildDto,
  ListIndexRebuildsParams,
} from '../types/index-rebuild.dto'

export type ListIndexRebuildsRequest = ListIndexRebuildsParams & PageQuery

export const indexRebuildApi = {
  /** 分页查询索引重建任务 */
  list: (params: ListIndexRebuildsRequest) =>
    client.get<PageResult<IndexRebuildTaskDto>>('/admin/index-rebuild/tasks', { params }),

  /** 获取任务详情/进度 */
  get: (id: string) =>
    client.get<IndexRebuildTaskDto>(`/admin/index-rebuild/tasks/${id}`),

  /** 触发索引重建（幂等） */
  trigger: (body: TriggerIndexRebuildDto) =>
    client.post<IndexRebuildTaskDto>('/admin/index-rebuild/trigger', body, withIdempotency()),

  /** 重试失败任务（幂等） */
  retry: (id: string) =>
    client.post<IndexRebuildTaskDto>(`/admin/index-rebuild/tasks/${id}/retry`, null, withIdempotency()),
}
