/**
 * 商品模块 DTO 类型定义
 *
 * 与后端 ProductController / SkuController / PriceHistoryController 对接。
 * 所有字段命名与后端 JSON 序列化保持一致（camelCase）。
 */

/**
 * 商品状态
 *
 * - Draft: 草稿，仅卖家可见
 * - PendingReview: 待平台审核
 * - Approved: 审核通过已上架，对买家可见
 * - TakenDown: 卖家主动下架，对买家不可见
 * - Rejected: 审核驳回，需修改后重新提交
 */
export type ProductStatus = 'Draft' | 'PendingReview' | 'Approved' | 'TakenDown' | 'Rejected'

/**
 * 商品列表项 DTO
 */
export interface ProductListItemDto {
  id: string
  name: string
  status: ProductStatus
  categoryId: string
  categoryName?: string
  coverImage?: string
  priceRange?: string
  skuCount: number
  totalStock: number
  salesCount: number
  createdAt: string
  updatedAt: string
}

/**
 * SKU DTO
 */
export interface ProductSkuDto {
  id: string
  skuCode: string
  skuName: string
  attributes: Record<string, string>
  price: number
  stock: number
  lowStockThreshold: number
}

/**
 * 商品详情 DTO
 */
export interface ProductDetailDto {
  id: string
  name: string
  description?: string
  status: ProductStatus
  categoryId: string
  categoryName?: string
  coverImage?: string
  images: string[]
  attributes: Array<{ name: string; values: string[] }>
  skus: ProductSkuDto[]
  priceRange?: string
  totalStock: number
  salesCount: number
  version: number
  createdAt: string
  updatedAt: string
  rejectReason?: string
}

/**
 * 创建商品 DTO
 */
export interface CreateProductDto {
  name: string
  description?: string
  categoryId: string
  coverImage?: string
  images?: string[]
  attributes?: Array<{ name: string; values: string[] }>
}

/**
 * 更新商品 DTO
 *
 * version 用于乐观锁，必填。
 */
export interface UpdateProductDto {
  name?: string
  description?: string
  categoryId?: string
  coverImage?: string
  images?: string[]
  attributes?: Array<{ name: string; values: string[] }>
  version: number
}

/**
 * 新增 SKU DTO
 */
export interface AddSkuDto {
  skuCode: string
  skuName: string
  attributes: Record<string, string>
  price: number
  stock: number
  lowStockThreshold?: number
}

/**
 * 调整价格 DTO
 */
export interface AdjustPriceDto {
  newPrice: number
  reason?: string
}

/**
 * 价格变更记录 DTO
 */
export interface PriceChangeRecordDto {
  id: string
  productId: string
  skuId: string
  skuCode: string
  skuName: string
  oldPrice: number
  newPrice: number
  reason?: string
  operator: string
  createdAt: string
}

/**
 * 操作原因 DTO（下架用）
 *
 * version 用于乐观锁，必填。
 */
export interface ActionReasonDto {
  reason: string
  version: number
}

/**
 * 列表查询参数
 */
export interface ListProductsParams {
  keyword?: string
  status?: ProductStatus
  categoryId?: string
  page?: number
  pageSize?: number
}
