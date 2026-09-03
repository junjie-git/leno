import type { PagedResult } from '@/shared/types'

/**
 * 商品目录域 DTO（Product BC 买家端）
 *
 * 端点契约：
 * - GET /api/products/search          商品搜索/列表（关键词、分类、品牌、排序、分页）
 * - GET /api/products/{id}            商品详情（含 SKU、属性、价格历史、评价摘要）
 * - GET /api/products/{id}/price-history 价格历史（详情内嵌返回，独立端点同构）
 * - GET /api/categories/tree          分类树（一级 + 二级）
 * - GET /api/brands                   品牌列表
 */

/** 商品搜索排序方式 */
export type ProductSort = 'default' | 'sales' | 'priceAsc' | 'priceDesc' | 'newest'

/** 商品搜索请求参数 */
export interface ProductQueryParams {
  keyword?: string
  categoryId?: string
  brandId?: string
  shopId?: string
  sort?: ProductSort
  page?: number
  pageSize?: number
}

/** 商品卡片摘要（推荐流 / 搜索结果 / 分类列表共用） */
export interface ProductSummaryDto {
  id: string
  name: string
  mainImage: string
  /** SKU 最低价（分） */
  priceMin: number
  /** SKU 最高价（分） */
  priceMax: number
  /** 月销量 */
  sales: number
  /** 营销标签（秒杀/包邮/满减/新品等） */
  tags: string[]
  shopId: string
  shopName: string
  categoryId: string
}

/** 商品 SKU */
export interface ProductSkuDto {
  id: string
  spuId: string
  /** 规格组合描述，如「颜色:白色;尺码:M」 */
  specs: string
  /** 售价（分） */
  price: number
  /** 划线原价（分） */
  originalPrice: number
  stock: number
  image: string
}

/** 价格历史点 */
export interface PriceHistoryPointDto {
  date: string
  price: number
}

/** 评价摘要 */
export interface ReviewSummaryDto {
  count: number
  averageRating: number
  goodRate: number
}

/** 商品属性（规格以外的展示属性） */
export interface ProductAttributeDto {
  name: string
  value: string
}

/** 商品详情 */
export interface ProductDetailDto {
  id: string
  name: string
  subtitle: string
  mainImage: string
  images: string[]
  categoryId: string
  categoryName: string
  brandId: string
  brandName: string
  shopId: string
  shopName: string
  description: string
  priceMin: number
  priceMax: number
  sales: number
  stock: number
  tags: string[]
  skus: ProductSkuDto[]
  attributes: ProductAttributeDto[]
  priceHistory: PriceHistoryPointDto[]
  reviewSummary: ReviewSummaryDto
}

/** 分类节点 */
export interface CategoryDto {
  id: string
  name: string
  /** 分类入口图标（SVG data URI） */
  icon?: string
  children: CategoryDto[]
}

/** 品牌 */
export interface BrandDto {
  id: string
  name: string
  logo: string
}

/** 商品搜索分页结果 */
export type ProductSearchResult = PagedResult<ProductSummaryDto>
