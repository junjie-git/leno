/**
 * 02-product-ops 商品运营模块出口
 *
 * - productOpsRoutes：路由聚合（供 app/router.ts 展开）
 * - productApi / brandApi / categoryApi：模块 API
 * - types：DTO 聚合再导出
 * - views：页面组件（懒加载路由引用，亦支持直接导入）
 */
export { default as productOpsRoutes } from './routes'

export { productApi } from './api/product.api'
export { brandApi } from './api/brand.api'
export { categoryApi } from './api/category.api'

export type {
  ActionReasonDto,
  BatchOperationFailureDto,
  BatchOperationResultDto,
  ProductAuditLogDto,
  ProductDto,
  ProductQueryParams,
  ProductStatus,
  ProductStatusOption,
  ReplenishSkuDto,
  SkuDto,
  UpdateStockDto,
} from './types/product.dto'

export type {
  BrandDto,
  BrandQueryParams,
  BrandStatus,
  CreateBrandDto,
  SaveBrandDto,
  UpdateBrandDto,
} from './types/brand.dto'

export type {
  CategoryDto,
  CategoryStatus,
  CategoryTreeParams,
  CreateCategoryDto,
  SaveCategoryDto,
  UpdateCategoryDto,
} from './types/category.dto'

export { default as ProductAudit } from './views/ProductAudit.vue'
export { default as BrandManagement } from './views/BrandManagement.vue'
export { default as CategoryManagement } from './views/CategoryManagement.vue'
