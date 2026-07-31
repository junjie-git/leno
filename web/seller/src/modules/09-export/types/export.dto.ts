/**
 * 09-export 数据导出 DTO
 *
 * 与后端 ExportController 对接：
 * - POST /api/seller/export/sales              创建导出任务（幂等）
 * - GET  /api/seller/export/tasks              查询导出任务列表
 * - GET  /api/seller/export/tasks/{id}/download 下载导出文件
 */

/** 报表类型 */
export type ReportType = 'SalesSummary' | 'OrderDetail' | 'ProductSales'

/** 导出格式 */
export type ExportFormat = 'Excel' | 'CSV'

/** 任务状态 */
export type ExportTaskStatus = 'Processing' | 'Completed' | 'Failed'

/** 创建导出任务 */
export interface CreateExportTaskDto {
  reportType: ReportType
  startDate: string
  endDate: string
  format: ExportFormat
}

/** 导出任务 */
export interface ExportTaskDto {
  id: string
  reportType: ReportType
  startDate: string
  endDate: string
  format: ExportFormat
  status: ExportTaskStatus
  recordCount?: number
  fileSize?: number
  downloadUrl?: string
  errorMessage?: string
  createdAt: string
  completedAt?: string
}

/** 任务查询参数 */
export interface ExportTaskQueryParams {
  page: number
  pageSize: number
  status?: ExportTaskStatus
}

/** 任务列表结果 */
export interface ExportTaskListResultDto {
  items: ExportTaskDto[]
  total: number
}
