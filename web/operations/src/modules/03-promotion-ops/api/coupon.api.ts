import { client, withIdempotency } from '@/shared/http'
import type { PageResult } from '@/shared/types'
import type { CouponDto, ListCouponsParams, SaveCouponDto } from '../types/coupon.dto'

/**
 * 优惠券模板 API
 *
 * 与后端 CouponController（/api/admin/coupons）对接：
 * - GET  /admin/coupons                          分页查询（状态/关键词/类型过滤）
 * - POST /admin/coupons                          创建券模板（草稿态，幂等）
 * - PUT  /admin/coupons/{couponId}               更新券模板（仅草稿可改，幂等）
 * - POST /admin/coupons/{couponId}/publish       发布券模板（Draft → Published，幂等）
 * - POST /admin/coupons/{couponId}/stop          停用券模板（Published → Stopped，幂等）
 * - POST /admin/coupons/{couponId}/issue?quantity=n 批量发放 n 张（幂等；quantity 走 query，body 为空对象）
 *
 * 响应拦截器已解包 ApiResponse.data，调用方拿到的就是业务负载。
 */
export const couponApi = {
  /**
   * 分页查询券模板
   */
  list(params: ListCouponsParams): Promise<PageResult<CouponDto>> {
    return client.get<PageResult<CouponDto>>('/coupons', { params }).then((r) => r.data)
  },

  /**
   * 创建券模板（幂等）
   */
  create(body: SaveCouponDto): Promise<CouponDto> {
    return client.post<CouponDto>('/coupons', body, withIdempotency()).then((r) => r.data)
  },

  /**
   * 更新券模板（幂等，仅草稿态可更新）
   */
  update(couponId: string, body: SaveCouponDto): Promise<CouponDto> {
    return client
      .put<CouponDto>(`/coupons/${couponId}`, body, withIdempotency())
      .then((r) => r.data)
  },

  /**
   * 发布券模板（幂等）：Draft → Published，发布后买家端可见可领取
   */
  publish(couponId: string): Promise<void> {
    return client
      .post<void>(`/coupons/${couponId}/publish`, null, withIdempotency())
      .then((r) => r.data)
  },

  /**
   * 停用券模板（幂等）：Published → Stopped，停用后买家端不可领取，已领取的券仍有效
   */
  stop(couponId: string): Promise<void> {
    return client
      .post<void>(`/coupons/${couponId}/stop`, null, withIdempotency())
      .then((r) => r.data)
  },

  /**
   * 批量发放优惠券（幂等）：quantity 走 query 参数，body 为空对象；
   * 返回发放后的券模板最新视图（已领/剩余列局部更新用）
   */
  issue(couponId: string, quantity: number): Promise<CouponDto> {
    return client
      .post<CouponDto>(`/coupons/${couponId}/issue`, {}, {
        params: { quantity },
        ...withIdempotency(),
      })
      .then((r) => r.data)
  },
}
