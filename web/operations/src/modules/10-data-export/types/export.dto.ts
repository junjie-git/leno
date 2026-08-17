/**
 * 10-data-export 数据导出 DTO
 *
 * 降级方案（后端异步导出端点 /api/admin/data-exports/* 未上线）：
 * - 前端基于既有业务列表端点分页同步拉取（页大小 200，单任务上限 10000 行）
 * - 前端生成 CSV（BOM + CRLF，Excel 兼容）触发浏览器下载
 * - 导出任务历史持久化 localStorage，保留 7 天自动清理，文件过期提示重建
 *
 * 后续端点上线后替换为异步任务（Queued → Processing → Completed/Failed + 轮询），
 * 状态机字段与规划端点 DataExportTaskDto 对齐，便于平滑迁移。
 */

/** 导出业务类型（与规划端点 BusinessType 枚举对齐） */
export type ExportBusinessType =
  | 'Order'
  | 'Payment'
  | 'Refund'
  | 'AfterSales'
  | 'Product'
  | 'Notification'
  | 'Review'
  | 'Seller'

/** 业务类型 → 中文标签映射 */
export const EXPORT_BUSINESS_TYPE_LABELS: Record<ExportBusinessType, string> = {
  Order: '订单',
  Payment: '支付',
  Refund: '退款',
  AfterSales: '售后',
  Product: '商品',
  Notification: '通知',
  Review: '评价',
  Seller: '卖家',
}

/** 业务类型全量选项（新建任务表单 select 数据源） */
export const EXPORT_BUSINESS_TYPES: ExportBusinessType[] = [
  'Order',
  'Payment',
  'Refund',
  'AfterSales',
  'Product',
  'Notification',
  'Review',
  'Seller',
]

/** 导出任务状态（本地降级：创建即 Processing，拉取完成 Completed / 异常 Failed） */
export type ExportTaskStatus = 'Queued' | 'Processing' | 'Completed' | 'Failed'

/** 任务状态展示元数据（md §6 状态色） */
export const EXPORT_TASK_STATUS_META: Record<ExportTaskStatus, { label: string; color: string }> = {
  Queued: { label: '排队中', color: 'default' },
  Processing: { label: '处理中', color: 'processing' },
  Completed: { label: '已完成', color: 'success' },
  Failed: { label: '失败', color: 'error' },
}

/** 单任务导出行数上限（超限截断并提示缩小时间范围） */
export const EXPORT_MAX_ROWS = 10000

/** 分页同步拉取页大小 */
export const EXPORT_PAGE_SIZE = 200

/** 任务历史保留天数（对齐「文件保留 7 天自动清理」口径） */
export const EXPORT_RETENTION_DAYS = 7

/** 同业务类型同时间范围防重复创建窗口（毫秒，5 分钟） */
export const EXPORT_DEDUPE_WINDOW_MS = 5 * 60 * 1000

/** 导出筛选条件（按业务类型动态取值，status 为各业务状态枚举字符串） */
export interface ExportFilterParams {
  /** 关键词（订单号 / 支付单号 / 退款编号 / 售后单号 / 商品关键词 / 用户 ID / 商品名 / 店铺关键词） */
  keyword?: string
  /** 业务状态筛选（各业务状态枚举值，由视图层按业务类型提供选项） */
  status?: string
}

/** 导出任务本地记录（localStorage 持久化；csv 为空表示文件已过期不可下载） */
export interface ExportTaskRecord {
  id: string
  /** 任务名称（业务类型 + 时间范围摘要） */
  taskName: string
  businessType: ExportBusinessType
  /** 时间范围（ISO 8601 UTC） */
  fromTime: string
  toTime: string
  /** 导出筛选快照 */
  filters: ExportFilterParams
  status: ExportTaskStatus
  /** 已导出记录数 */
  recordCount: number
  /** 处理进度 0-100（Processing 实时更新，Completed 为 100） */
  progress: number
  /** CSV 全文（含 BOM 头）；存储配额不足或超期清理后为空字符串 */
  csv: string
  /** 失败原因（Failed 时展示） */
  errorMessage?: string
  /** 创建人（当前登录用户名，降级记录） */
  createdBy: string
  /** 创建时间（ISO 8601） */
  createdAt: string
  /** 完成时间（ISO 8601） */
  completedAt?: string
}

/** 同步拉取结果（fetchExportRows 返回） */
export interface ExportFetchResult {
  /** CSV 表头（中文列名） */
  header: string[]
  /** 数据行（与 header 对齐） */
  rows: string[][]
  /** 后端命中总数 */
  total: number
  /** 是否因超过上限被截断 */
  truncated: boolean
}
