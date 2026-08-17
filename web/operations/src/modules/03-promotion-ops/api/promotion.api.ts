import { client, withIdempotency } from '@/shared/http'
import type { PageResult } from '@/shared/types'
import type {
  ListPromotionsParams,
  PromotionActivityDto,
  SavePromotionActivityDto,
} from '../types/promotion.dto'

/**
 * 促销活动 API
 *
 * 与后端 PromotionController（/api/admin/promotions）对接：
 * - GET  /admin/promotions                         分页查询（名称模糊/状态精确/时间区间过滤）
 * - GET  /admin/promotions/{activityId}            活动详情（编辑回显）
 * - POST /admin/promotions                         创建活动（待生效态，幂等）
 * - PUT  /admin/promotions/{activityId}            更新活动规则（仅待生效可改，幂等）
 * - POST /admin/promotions/{activityId}/activate   激活 / 恢复（Pending→Active、Paused→Active，幂等）
 * - POST /admin/promotions/{activityId}/pause      暂停（Active→Paused，幂等）
 * - POST /admin/promotions/{activityId}/close      关闭（→Closed 终态，幂等）
 *
 * 响应拦截器已解包 ApiResponse.data，调用方拿到的就是业务负载。
 * 状态流转写操作均带 Idempotency-Key，后端据去重键防重复触发。
 */
export const promotionApi = {
  /**
   * 分页查询促销活动
   */
  list(params: ListPromotionsParams): Promise<PageResult<PromotionActivityDto>> {
    return client
      .get<PageResult<PromotionActivityDto>>('/promotions', { params })
      .then((r) => r.data)
  },

  /**
   * 查询活动详情（编辑抽屉回显）
   */
  get(activityId: string): Promise<PromotionActivityDto> {
    return client.get<PromotionActivityDto>(`/promotions/${activityId}`).then((r) => r.data)
  },

  /**
   * 创建促销活动（幂等）
   */
  create(body: SavePromotionActivityDto): Promise<PromotionActivityDto> {
    return client
      .post<PromotionActivityDto>('/promotions', body, withIdempotency())
      .then((r) => r.data)
  },

  /**
   * 更新活动规则（幂等，仅待生效态可更新）
   */
  update(activityId: string, body: SavePromotionActivityDto): Promise<PromotionActivityDto> {
    return client
      .put<PromotionActivityDto>(`/promotions/${activityId}`, body, withIdempotency())
      .then((r) => r.data)
  },

  /**
   * 激活活动（幂等）：Pending → Active；Paused 恢复亦走本端点（Paused → Active）
   */
  activate(activityId: string): Promise<void> {
    return client
      .post<void>(`/promotions/${activityId}/activate`, null, withIdempotency())
      .then((r) => r.data)
  },

  /**
   * 暂停活动（幂等）：Active → Paused
   */
  pause(activityId: string): Promise<void> {
    return client
      .post<void>(`/promotions/${activityId}/pause`, null, withIdempotency())
      .then((r) => r.data)
  },

  /**
   * 关闭活动（幂等，终态不可逆）：Active/Paused → Closed
   */
  close(activityId: string): Promise<void> {
    return client
      .post<void>(`/promotions/${activityId}/close`, null, withIdempotency())
      .then((r) => r.data)
  },
}
