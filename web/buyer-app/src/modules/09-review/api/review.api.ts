import { client } from '@/shared/http'
import type {
  AppendReviewRequestDto,
  ProductReviewsResultDto,
  ReviewDto,
  SubmitReviewsRequestDto,
} from '../types/review.dto'

/**
 * 评价 API（Review 域 / 旧 ReviewAfterSales 双轨兜底）
 *
 * - GET  /reviews/mine                  我的评价
 * - POST /reviews/{reviewId}/append     追加评价
 * - GET  /products/{spuId}/reviews      商品评价列表
 * - POST /orders/{orderId}/reviews      提交订单评价
 *
 * 响应拦截器已解包 ApiResponse.data，调用方拿到的就是业务负载。
 */
export const reviewApi = {
  /** 我的评价列表 */
  listMine(): Promise<ReviewDto[]> {
    return client.get<ReviewDto[]>('/reviews/mine').then((r) => r.data)
  },

  /** 追加评价 */
  append(reviewId: string, body: AppendReviewRequestDto): Promise<ReviewDto> {
    return client.post<ReviewDto>(`/reviews/${reviewId}/append`, body).then((r) => r.data)
  },

  /** 商品评价列表（含摘要与分布，匿名可访问） */
  listProductReviews(
    spuId: string,
    params: { page?: number; pageSize?: number; filter?: 'all' | 'withImage' | 'good' | 'bad' },
  ): Promise<ProductReviewsResultDto> {
    return client.get<ProductReviewsResultDto>(`/products/${spuId}/reviews`, { params }).then((r) => r.data)
  },

  /** 提交订单评价（按订单行批量） */
  submitOrderReviews(orderId: string, body: SubmitReviewsRequestDto): Promise<ReviewDto[]> {
    return client.post<ReviewDto[]>(`/orders/${orderId}/reviews`, body).then((r) => r.data)
  },
}
