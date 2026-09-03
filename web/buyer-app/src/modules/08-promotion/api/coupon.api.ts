import { client } from '@/shared/http'
import type { AvailableCouponDto, CouponStatus, MyCouponDto } from '../types/promotion.dto'

/**
 * 优惠券 API（Promotion BC 买家端）
 *
 * - GET  /coupons/available           可领优惠券
 * - POST /coupons/{couponId}/receive  领取优惠券
 * - GET  /coupons/mine                我的优惠券（status 筛选）
 * - GET  /coupons/claimable           积分可兑换券
 *
 * 响应拦截器已解包 ApiResponse.data，调用方拿到的就是业务负载。
 */
export const couponApi = {
  /** 可领优惠券（领券中心） */
  listAvailable(): Promise<AvailableCouponDto[]> {
    return client.get<AvailableCouponDto[]>('/coupons/available').then((r) => r.data)
  },

  /** 领取优惠券 */
  receive(couponId: string): Promise<null> {
    return client.post<null>(`/coupons/${couponId}/receive`).then((r) => r.data)
  },

  /** 我的优惠券 */
  listMine(status?: CouponStatus): Promise<MyCouponDto[]> {
    return client
      .get<MyCouponDto[]>('/coupons/mine', { params: status ? { status } : undefined })
      .then((r) => r.data)
  },

  /** 积分可兑换券（11-points 模块的积分兑换页使用） */
  listClaimable(): Promise<AvailableCouponDto[]> {
    return client.get<AvailableCouponDto[]>('/coupons/claimable').then((r) => r.data)
  },
}
