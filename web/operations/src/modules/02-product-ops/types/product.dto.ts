import type { PageQuery } from '@/shared/types'

/**
 * 02-product-ops 商品审核 DTO
 *
 * 对接 Product 域 AdminProductsController：
 * - GET  /api/admin/products/all                          全量商品分页查询（跨店铺）
 * - POST /api/admin/products/{id}/approve                 审核通过并上架
 * - POST /api/admin/products/{id}/reject                  审核驳回（ActionReasonDto）
 * - POST /api/admin/products/{id}/skus/{skuId}/stock      调整 SKU 库存（delta 方式）
 * - POST /api/admin/products/skus/{skuId}/replenish       为指定 SKU 补货
 *
 * 状态机：Draft → PendingAudit → Active / Rejected；Active → OffShelf。
 */

/** 商品状态（与后端 ProductStatus 枚举对齐） */
export type ProductStatus = 'Draft' | 'PendingAudit' | 'Active' | 'Rejected' | 'OffShelf'

/** 商品状态筛选项 */
export interface ProductStatusOption {
  label: string
  value: ProductStatus
}

/** SKU 视图 */
export interface SkuDto {
  id: string
  /** 规格描述，如「黑色 / XL」 */
  spec: string
  /** 售价（元） */
  price: number
  /** 当前库存 */
  stock: number
}

/** 审核历史条目（详情抽屉时间线数据源） */
export interface ProductAuditLogDto {
  id: string
  /** 动作：提交 / 通过 / 驳回 / 下架 / 库存调整 / 补货 */
  action: 'Submitted' | 'Approved' | 'Rejected' | 'OffShelf' | 'StockAdjusted' | 'Replenished'
  /** 操作人（卖家或运营人员） */
  operator: string
  /** 原因（驳回原因 / 调整原因），可选 */
  reason?: string
  /** 操作时间（ISO 8601） */
  createdAt: string
}

/** 商品（SPU）视图，列表行与详情抽屉共用 */
export interface ProductDto {
  id: string
  title: string
  /** 主图 URL（可能缺失，前端用占位） */
  mainImageUrl?: string
  /** 详情图集（含主图，抽屉预览用），可选 */
  imageUrls?: string[]
  status: ProductStatus
  categoryId: string
  categoryName?: string
  brandId?: string
  brandName?: string
  sellerId: string
  sellerName?: string
  /** SKU 列表（后端 Skus: SkuDto[]） */
  skus: SkuDto[]
  /** 提交审核时间（ISO 8601） */
  submittedAt: string
  /** 最近一次驳回原因 */
  rejectReason?: string
  /** 审核历史（后端可选返回，缺失时前端按字段合成） */
  auditLogs?: ProductAuditLogDto[]
}

/** GET /api/admin/products/all 查询参数（ProductQueryDto） */
export interface ProductQueryParams extends PageQuery {
  /** 关键词：商品名称 / SKU 编号 */
  keyword?: string
  /** 卖家 ID */
  sellerId?: string
  /** 商品状态 */
  status?: ProductStatus
  /** 分类 ID */
  categoryId?: string
}

/** 驳回请求体（ActionReasonDto）：Reason 必填，前端限制 5-200 字 */
export interface ActionReasonDto {
  reason: string
}

/** 库存调整请求体（UpdateStockDto）：delta 正数补库存、负数扣库存 */
export interface UpdateStockDto {
  delta: number
  reason?: string
}

/** 补货请求体：数量必须大于 0 */
export interface ReplenishSkuDto {
  quantity: number
}

/** 批量操作失败明细项 */
export interface BatchOperationFailureDto {
  id: string
  reason: string
}

/** 批量操作汇总结果（BatchOperationResultDto） */
export interface BatchOperationResultDto {
  total: number
  succeeded: number
  failed: number
  failures: BatchOperationFailureDto[]
}
