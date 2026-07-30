import { http, withIdempotency } from '@/shared/http'
import type {
  ReviewDto,
  ReviewListResultDto,
  ReviewQueryParams,
  SellerReplyDto,
} from '../types/review.dto'

/**
 * 评价 API 客户端（新 BC 路径 /api/seller/reviews）
 *
 * 与后端 ReviewController 对接（响应拦截器已解包 ApiResponse.data，
 * 调用方拿到的就是业务负载）：
 * - GET  /seller/reviews          评价列表（分页 + 筛选）
 * - GET  /seller/reviews/{id}      评价详情
 * - POST /seller/reviews/{id}/reply 回复评价（覆盖式编辑，1-500 字，幂等）
 */
export const reviewApi = {
  /** 查询卖家评价列表 */
  list(params: ReviewQueryParams): Promise<ReviewListResultDto> {
    return http
      .get<ReviewListResultDto>('/seller/reviews', { params })
      .then((r) => r.data)
  },

  /** 查询评价详情 */
  get(id: string): Promise<ReviewDto> {
    return http.get<ReviewDto>(`/seller/reviews/${id}`).then((r) => r.data)
  },

  /** 回复评价（覆盖式编辑，1-500 字） */
  reply(id: string, body: SellerReplyDto): Promise<ReviewDto> {
    return http
      .post<ReviewDto>(`/seller/reviews/${id}/reply`, body, withIdempotency())
      .then((r) => r.data)
  },
}
