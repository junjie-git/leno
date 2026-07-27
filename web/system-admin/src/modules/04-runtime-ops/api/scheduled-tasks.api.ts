// web/system-admin/src/modules/04-runtime-ops/api/scheduled-tasks.api.ts
// 定时任务 API：对齐 SystemAdmin BC ScheduledTasksController 端点
// create/update/enable/disable/runNow 均注入 Idempotency-Key 头

import { client, withIdempotency } from '@/shared/http'
import type { PageResult, PageQuery } from '@/shared/types'
import type {
  ScheduledTaskDto,
  SaveScheduledTaskDto,
  UpdateScheduledTaskDto,
  ScheduledTaskExecutionDto,
  ListScheduledTasksParams,
} from '../types/scheduled-task.dto'

export type ListScheduledTasksRequest = ListScheduledTasksParams & PageQuery

export const scheduledTaskApi = {
  /** 分页查询定时任务 */
  list: (params: ListScheduledTasksRequest) =>
    client.get<PageResult<ScheduledTaskDto>>('/admin/scheduled-tasks', { params }),

  /** 获取定时任务详情 */
  get: (taskId: string) =>
    client.get<ScheduledTaskDto>(`/admin/scheduled-tasks/${taskId}`),

  /** 创建定时任务（初始停用态，幂等） */
  create: (body: SaveScheduledTaskDto) =>
    client.post<ScheduledTaskDto>('/admin/scheduled-tasks', body, withIdempotency()),

  /** 更新定时任务（jobType 不可变，幂等） */
  update: (taskId: string, body: UpdateScheduledTaskDto) =>
    client.put<ScheduledTaskDto>(`/admin/scheduled-tasks/${taskId}`, body, withIdempotency()),

  /** 启用任务并向调度器注册（幂等） */
  enable: (taskId: string) =>
    client.post<ScheduledTaskDto>(`/admin/scheduled-tasks/${taskId}/enable`, null, withIdempotency()),

  /** 停用任务并从调度器注销（幂等） */
  disable: (taskId: string) =>
    client.post<ScheduledTaskDto>(`/admin/scheduled-tasks/${taskId}/disable`, null, withIdempotency()),

  /** 立即触发任务执行（幂等） */
  runNow: (taskId: string) =>
    client.post<ScheduledTaskDto>(`/admin/scheduled-tasks/${taskId}/run-now`, null, withIdempotency()),

  /** 查询执行历史（最近 20 次） */
  getHistory: (taskId: string) =>
    client.get<ScheduledTaskExecutionDto[]>(`/admin/scheduled-tasks/${taskId}/executions`),
}
