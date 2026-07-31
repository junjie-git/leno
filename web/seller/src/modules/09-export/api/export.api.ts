import { http, withIdempotency } from '@/shared/http'
import type {
  CreateExportTaskDto,
  ExportTaskDto,
  ExportTaskListResultDto,
  ExportTaskQueryParams,
} from '../types/export.dto'

/**
 * 数据导出 API 客户端
 *
 * 与后端 ExportController 对接。响应拦截器已解包
 * ApiResponse.data，调用方拿到的就是业务负载：
 * - POST /api/seller/export/sales                创建导出任务（幂等）
 * - GET  /api/seller/export/tasks                查询导出任务列表
 * - GET  /api/seller/export/tasks/{id}/download  下载导出文件
 *
 * 注：getDownloadUrl 为同步辅助方法，仅拼接下载 URL 字符串（含 /api 前缀），
 * 不发起 HTTP 请求。调用方需自行用 http.get 或 window.open 触发下载。
 */
export const exportApi = {
  /** 创建导出任务 */
  createTask(body: CreateExportTaskDto): Promise<ExportTaskDto> {
    return http
      .post<ExportTaskDto>('/seller/export/sales', body, withIdempotency())
      .then((r) => r.data)
  },

  /** 查询导出任务列表 */
  listTasks(
    params: ExportTaskQueryParams,
  ): Promise<ExportTaskListResultDto> {
    return http
      .get<ExportTaskListResultDto>('/seller/export/tasks', { params })
      .then((r) => r.data)
  },

  /**
   * 构造下载导出文件的完整 URL（同步、非 Promise）
   *
   * 返回值含 `/api` 前缀，可直接用于 window.open；
   * 若需经 axios 调用（触发 mock / 走拦截器），请先去掉 `/api` 前缀再传给 http.get。
   */
  getDownloadUrl(taskId: string): string {
    return `/api/seller/export/tasks/${taskId}/download`
  },
}
