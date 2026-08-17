/**
 * 10-data-export 数据导出模块桶导出
 *
 * - 默认导出：模块路由（懒加载视图，由 app/router 聚合到 BasicLayout children）
 * - 具名导出：导出聚合 API、状态筛选项与全部 DTO 类型 / 常量
 */
export { default } from './routes'
export {
  EXPORT_STATUS_OPTIONS,
  fetchExportRows,
  buildCsv,
  csvEscape,
  buildExportFileName,
  loadExportTasks,
  saveExportTasks,
  addExportTask,
  updateExportTask,
  removeExportTask,
  clearExpiredExportTasks,
  hasRecentDuplicate,
  downloadTaskCsv,
  generateExportTaskId,
} from './api/export.api'
export type { ExportFetchOptions } from './api/export.api'
export {
  EXPORT_BUSINESS_TYPES,
  EXPORT_BUSINESS_TYPE_LABELS,
  EXPORT_MAX_ROWS,
  EXPORT_PAGE_SIZE,
  EXPORT_RETENTION_DAYS,
  EXPORT_DEDUPE_WINDOW_MS,
  EXPORT_TASK_STATUS_META,
} from './types/export.dto'
export type {
  ExportBusinessType,
  ExportTaskStatus,
  ExportFilterParams,
  ExportTaskRecord,
  ExportFetchResult,
} from './types/export.dto'
