/**
 * 03-promotion-ops 促销运营模块桶导出
 *
 * - 默认导出：模块路由（懒加载视图，由 app/router 聚合到 BasicLayout children）
 * - 具名导出：三个域的 API 与全部 DTO 类型
 */
export { default } from './routes'
export { promotionApi } from './api/promotion.api'
export { couponApi } from './api/coupon.api'
export { seckillApi } from './api/seckill.api'
export type {
  PromotionType,
  PromotionStatus,
  PromotionScopeType,
  PromotionRuleDto,
  PromotionActivityDto,
  SavePromotionActivityDto,
  ListPromotionsParams,
} from './types/promotion.dto'
export type {
  CouponType,
  CouponTemplateStatus,
  CouponValidityType,
  CouponDto,
  SaveCouponDto,
  ListCouponsParams,
} from './types/coupon.dto'
export type {
  SeckillStatus,
  SeckillSkuConfigDto,
  SeckillItemDto,
  SeckillActivityDto,
  CreateSeckillActivityDto,
  ListSeckillActivitiesParams,
} from './types/seckill.dto'
