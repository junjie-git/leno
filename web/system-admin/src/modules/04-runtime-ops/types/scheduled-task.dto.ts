// web/system-admin/src/modules/04-runtime-ops/types/scheduled-task.dto.ts
// 定时任务 DTO 与枚举，对齐 SystemAdmin BC ScheduledTasksController 契约

/** 任务状态：启用 / 停用 */
export type ScheduledTaskStatus = 'Enabled' | 'Disabled'

/** 定时任务响应 DTO */
export interface ScheduledTaskDto {
  taskId: string
  name: string
  jobType: string
  cronExpression: string
  parameters: Record<string, unknown>
  status: ScheduledTaskStatus
  lastRunAt: string | null
  nextRunAt: string | null
  createdAt: string
}

/** 创建定时任务请求 DTO */
export interface SaveScheduledTaskDto {
  name: string
  jobType: string
  cronExpression: string
  parameters: Record<string, unknown>
}

/** 更新定时任务请求 DTO（jobType 不可变） */
export interface UpdateScheduledTaskDto {
  name: string
  cronExpression: string
  parameters: Record<string, unknown>
}

/** 执行历史条目 */
export interface ScheduledTaskExecutionDto {
  executionId: string
  taskId: string
  startedAt: string
  finishedAt: string | null
  status: 'Running' | 'Succeeded' | 'Failed'
  errorMessage: string | null
}

/** 列表查询参数 */
export interface ListScheduledTasksParams {
  name?: string
  status?: ScheduledTaskStatus[]
  jobType?: string
  page?: number
  pageSize?: number
}
