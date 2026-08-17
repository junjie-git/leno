import type { PageQuery } from '@/shared/types'

/**
 * 02-product-ops 品牌管理 DTO
 *
 * 对接 Product 域 BrandController / AdminBrandsController：
 * - GET  /api/brands                  分页查询品牌（共享字典）
 * - GET  /api/brands/{id}             品牌详情
 * - POST /api/admin/brands            创建品牌
 * - PUT  /api/admin/brands/{id}       更新品牌
 * - POST /api/admin/brands/{id}/enable|disable  启用 / 停用
 *
 * 状态机：Inactive ↔ Active 双向切换；被商品引用的品牌停用时后端返回 409。
 */

/** 品牌状态 */
export type BrandStatus = 'Active' | 'Inactive'

/** 品牌视图 */
export interface BrandDto {
  id: string
  /** 品牌名称（必填，1-50 字） */
  name: string
  /** 英文名，可选 */
  englishName?: string
  /** Logo URL（上传转 base64 data URL 或远程 URL），可选 */
  logoUrl?: string
  /** 品牌简介，可选（≤200 字） */
  description?: string
  /** 排序值，数字越小越靠前 */
  sortOrder: number
  status: BrandStatus
  /** 创建人 */
  createdBy?: string
  /** 创建时间（ISO 8601） */
  createdAt: string
}

/** GET /api/brands 查询参数（BrandQueryDto） */
export interface BrandQueryParams extends PageQuery {
  /** 名称关键词 */
  keyword?: string
  /** 状态筛选 */
  status?: BrandStatus
}

/** 创建 / 更新品牌请求体（CreateBrandDto / UpdateBrandDto 同构） */
export interface SaveBrandDto {
  /** 品牌名称（必填，1-50 字） */
  name: string
  englishName?: string
  logoUrl?: string
  description?: string
  sortOrder: number
  status: BrandStatus
}

/** 创建品牌请求体 */
export type CreateBrandDto = SaveBrandDto

/** 更新品牌请求体 */
export type UpdateBrandDto = SaveBrandDto
