/**
 * 评价域 DTO（Review 域 / 旧 ReviewAfterSales 双轨兜底）
 *
 * 端点契约：
 * - GET  /api/reviews/mine                    我的评价
 * - POST /api/reviews/{reviewId}/append       追加评价
 * - GET  /api/products/{spuId}/reviews        商品评价列表（匿名可访问）
 * - POST /api/orders/{orderId}/reviews        提交订单评价
 */

/** 评价 */
export interface ReviewDto {
  id: string
  orderLineId: string
  spuId: string
  /** 评价展示昵称（匿名时脱敏） */
  nickname: string
  avatar: string
  /** SKU 规格描述 */
  skuSpecs: string
  /** 评分 1-5 */
  rating: number
  content: string
  images: string[]
  /** 追评内容 */
  appendContent?: string
  appendAt?: string
  createdAt: string
  /** 商家回复 */
  reply?: {
    content: string
    repliedAt: string
  }
}

/** 评价分布（按星级） */
export interface RatingDistributionDto {
  star: number
  count: number
}

/** 商品评价摘要（与商品详情内嵌结构一致） */
export interface ProductReviewSummaryDto {
  count: number
  averageRating: number
  goodRate: number
  distribution: RatingDistributionDto[]
}

/** 商品评价列表响应 */
export interface ProductReviewsResultDto {
  summary: ProductReviewSummaryDto
  items: ReviewDto[]
  total: number
  page: number
  pageSize: number
}

/** 提交评价请求（按订单行批量提交） */
export interface SubmitReviewsRequestDto {
  reviews: Array<{
    orderLineId: string
    rating: number
    content: string
    images: string[]
    isAnonymous: boolean
  }>
}

/** 追加评价请求 */
export interface AppendReviewRequestDto {
  content: string
}
