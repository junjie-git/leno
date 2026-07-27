// web/system-admin/src/modules/04-runtime-ops/types/index-rebuild.dto.ts
// 索引重建任务 DTO 与枚举，对齐 SystemAdmin BC IndexRebuildController 契约

/** 任务状态：待执行 / 执行中 / 成功 / 失败 */
export type IndexRebuildStatus = 'Pending' | 'Running' | 'Succeeded' | 'Failed'

/** 索引重建任务响应 DTO */
export interface IndexRebuildTaskDto {
  taskId: string
  targetContext: string
  indexName: string
  status: IndexRebuildStatus
  triggeredBy: string
  triggeredAt: string
  startedAt: string | null
  finishedAt: string | null
  totalDocs: number
  processedDocs: number
  errorMessage: string | null
  retryCount: number
  esTaskId: string | null
}

/** 触发重建请求 DTO */
export interface TriggerIndexRebuildDto {
  targetContext: string
  indexName: string
}

/** 列表查询参数 */
export interface ListIndexRebuildsParams {
  targetContext?: string[]
  status?: IndexRebuildStatus[]
  page?: number
  pageSize?: number
}
