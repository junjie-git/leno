import type { PageQuery } from '@/shared/types'

/**
 * 05-order-ops 物流公司管理 DTO
 *
 * 对接 Logistics 域 AdminLogisticsCompaniesController：
 * - GET  /api/admin/logistics-companies                 分页查询物流公司（keyword / status）
 * - POST /api/admin/logistics-companies                 创建物流公司（代码唯一，409 透出）
 * - PUT  /api/admin/logistics-companies/{id}            更新可编辑字段
 * - POST /api/admin/logistics-companies/{id}/enable     启用
 * - POST /api/admin/logistics-companies/{id}/disable    停用（历史订单不受影响）
 *
 * 状态机：Inactive ↔ Active 双向切换；列表按 SortOrder 升序展示。
 */

/** 物流公司状态 */
export type LogisticsCompanyStatus = 'Active' | 'Inactive'

/** 物流公司视图 */
export interface LogisticsCompanyDto {
  id: string
  /** 公司名称（必填，1-50 字） */
  name: string
  /** 公司代码（必填，全局唯一，如 SF / ZTO；重复后端返回 409） */
  code: string
  /** Logo URL（上传转 base64 data URL 或远程 URL），可选 */
  logoUrl?: string
  /** 官方电话，可选 */
  phone?: string
  /** 官网链接，可选 */
  website?: string
  /** 排序值，数字越小越靠前 */
  sortOrder: number
  status: LogisticsCompanyStatus
  /** 创建时间（ISO 8601） */
  createdAt: string
}

/** GET /api/admin/logistics-companies 查询参数 */
export interface LogisticsCompanyQueryParams extends PageQuery {
  /** 名称 / 代码模糊匹配关键词 */
  keyword?: string
  /** 状态筛选 */
  status?: LogisticsCompanyStatus
}

/** 创建 / 更新物流公司请求体（CreateLogisticsCompanyDto / UpdateLogisticsCompanyDto 同构） */
export interface SaveLogisticsCompanyDto {
  name: string
  code: string
  /** base64 data URL 或远程 URL */
  logoUrl?: string
  phone?: string
  website?: string
  sortOrder: number
  status: LogisticsCompanyStatus
}

/** 创建物流公司请求体 */
export type CreateLogisticsCompanyDto = SaveLogisticsCompanyDto

/** 更新物流公司请求体 */
export type UpdateLogisticsCompanyDto = SaveLogisticsCompanyDto
