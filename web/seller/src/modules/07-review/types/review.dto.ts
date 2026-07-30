/**
 * 07-review 评价回复 DTO
 *
 * 与后端 ReviewController 对接（新 BC 路径 /api/seller/reviews）：
 * - GET  /api/seller/reviews          评价列表（分页 + 筛选）
 * - GET  /api/seller/reviews/{id}      评价详情
 * - POST /api/seller/reviews/{id}/reply 回复评价（覆盖式编辑，1-500 字，幂等）
 */

/** 评价状态（卖家仅可见 Approved） */
export type ReviewStatus = 'Approved' | 'Hidden'

/** 评价查询参数 */
export interface ReviewQueryParams {
  rating?: number
  replied?: boolean
  productName?: string
  startDate?: string
  endDate?: string
  page: number
  pageSize: number
}

/** 评价列表结果 */
export interface ReviewListResultDto {
  items: ReviewDto[]
  total: number
  page: number
  pageSize: number
}

/** 评价详情 */
export interface ReviewDto {
  reviewId: string
  orderId: string
  orderLineId: string
  spuId: string
  skuId: string
  userId: string
  userMaskedName: string
  rating: number
  content: string
  images: string[]
  status: ReviewStatus
  sellerReplyContent?: string
  sellerReplyBy?: string
  sellerReplyAt?: string
  submittedAt: string
  auditedAt?: string
  productName?: string
  productImage?: string
  skuSpec?: string
}

/** 卖家回复 DTO */
export interface SellerReplyDto {
  content: string
}
