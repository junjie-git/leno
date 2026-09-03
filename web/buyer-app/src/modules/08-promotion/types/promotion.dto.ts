/**
 * 促销域 DTO（Promotion BC 买家端）
 *
 * 端点契约：
 * - GET  /api/coupons/available                可领优惠券（领券中心）
 * - POST /api/coupons/{couponId}/receive       领取优惠券
 * - GET  /api/coupons/mine                     我的优惠券（状态筛选）
 * - GET  /api/coupons/claimable                积分可兑换券（11-points 模块调用）
 * - GET  /api/seckill/activities               秒杀活动列表（首页入口）
 * - GET  /api/seckill/activities/{activityId}  秒杀活动详情
 * - POST /api/seckill/activities/{activityId}/place 秒杀下单
 */

/** 优惠券类型：满减 / 包邮 / 折扣 */
export type CouponType = 'Threshold' | 'Shipping' | 'Discount'

/** 我的优惠券状态 */
export type CouponStatus = 'Usable' | 'Used' | 'Expired'

/** 领券中心条目（券模板） */
export interface AvailableCouponDto {
  /** 券模板 ID（领取用） */
  couponId: string
  name: string
  type: CouponType
  /** 门槛（分），Shipping 券可为 0 */
  threshold: number
  /** 抵扣额（分）；折扣券为折扣率（如 85 表示 8.5 折） */
  discount: number
  /** 领取后有效天数 */
  validDays: number
  /** 剩余可领数量 */
  remainCount: number
  /** 是否已领取（今日/本次活动内） */
  received: boolean
  /** 适用范围文案 */
  scopeText: string
}

/** 我的优惠券 */
export interface MyCouponDto {
  /** 用户券实例 ID */
  id: string
  /** 券模板 ID */
  couponId: string
  name: string
  type: CouponType
  threshold: number
  discount: number
  status: CouponStatus
  validFrom: string
  validTo: string
  scopeText: string
}

/** 秒杀活动状态 */
export type SeckillActivityStatus = 'Upcoming' | 'Active' | 'Ended'

/** 秒杀场次商品 */
export interface SeckillItemDto {
  skuId: string
  spuId: string
  name: string
  image: string
  specs: string
  /** 秒杀价（分） */
  seckillPrice: number
  /** 原价（分） */
  originalPrice: number
  /** 剩余库存 */
  stock: number
  /** 每人限购 */
  limitPerUser: number
}

/** 秒杀活动（场次） */
export interface SeckillActivityDto {
  id: string
  name: string
  /** 场次开始时间（Upcoming 时用于预告倒计时） */
  startTime: string
  /** 场次结束时间（Active 时用于距结束倒计时） */
  endTime: string
  status: SeckillActivityStatus
  items: SeckillItemDto[]
}

/** 秒杀下单请求 */
export interface SeckillPlaceRequestDto {
  skuId: string
  quantity: number
  addressId: string
}

/** 秒杀下单结果（成功创建秒杀订单） */
export interface SeckillPlaceResultDto {
  orderId: string
  orderNo: string
  payableAmount: number
  /** 支付截止时间 */
  payDeadline: string
}
