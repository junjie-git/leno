/**
 * 通用类型定义
 *
 * 与后端 `docs/contracts/internal-api-contracts.md` 信封格式保持一致。
 * 跨 Plan 共享，所有模块的 API/Store 必须从这里导入。
 */

/**
 * 后端统一响应信封
 *
 * - code: 200 表示成功（与后端 ApiResponse.Success 对齐）；非 200 表示业务错误码
 * - data: 业务负载，可能为 null（如删除操作）
 * - traceId: OpenTelemetry traceId，便于日志关联
 */
export interface ApiResponse<T> {
  code: number
  message: string
  data: T | null
  traceId?: string
}

/**
 * 分页响应结构
 *
 * 与后端分页端点约定的统一返回结构。
 */
export interface PageResult<T> {
  items: T[]
  total: number
  page: number
  pageSize: number
}

/**
 * 分页查询参数
 *
 * 所有列表 API 的查询参数基类。
 */
export interface PageQuery {
  page?: number
  pageSize?: number
}

/**
 * 表格列定义（Ant Design Vue Table 列的子集，按需扩展）
 */
export interface TableColumn {
  title: string
  dataIndex: string
  key?: string
  width?: number | string
  fixed?: 'left' | 'right' | boolean
  align?: 'left' | 'center' | 'right'
  ellipsis?: boolean
  sorter?: boolean | ((a: unknown, b: unknown) => number)
}
