/**
 * 全局共享类型（跨模块复用）
 */

/** 分页请求参数 */
export interface PageParams {
  page?: number
  pageSize?: number
}

/** 分页响应结构（后端 PagedResult<T>） */
export interface PagedResult<T> {
  items: T[]
  total: number
  page: number
  pageSize: number
}

/** 通用操作结果 */
export interface ActionResultDto {
  success: boolean
}
