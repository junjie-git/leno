import type { PageQuery } from '@/shared/types'

/**
 * 03-promotion-ops 促销活动 DTO
 *
 * 对接后端 PromotionController（/api/admin/promotions）：
 * - 活动类型三选一：满减 / 满折 / 满赠
 * - 状态机：Pending（待生效）→ Active（进行中）↔ Paused（已暂停）→ Closed（已关闭，终态）
 * - 阶梯规则：门槛 + 优惠（优惠额 / 折扣率 / 赠品 SKU）
 */

/** 促销活动类型：FullReduction=满减 / FullDiscount=满折 / FullGift=满赠 */
export type PromotionType = 'FullReduction' | 'FullDiscount' | 'FullGift'

/** 促销活动状态机：Pending → Active ↔ Paused → Closed（终态） */
export type PromotionStatus = 'Pending' | 'Active' | 'Paused' | 'Closed'

/** 适用范围：All=全平台 / Category=指定分类 / Product=指定商品 */
export type PromotionScopeType = 'All' | 'Category' | 'Product'

/**
 * 阶梯规则
 *
 * - threshold：门槛金额（满 X 元）
 * - discountValue：优惠值，语义随活动类型切换
 *   - FullReduction → 优惠金额（元），且不超门槛
 *   - FullDiscount → 折扣率（0-1 开区间，如 0.85 = 八五折）
 *   - FullGift → 固定 0，优惠以赠品 SKU 表达
 */
export interface PromotionRuleDto {
  /** 门槛金额（元） */
  threshold: number
  /** 优惠值：优惠额或折扣率（语义随类型切换） */
  discountValue: number
  /** 赠品 SKU ID（FullGift 必填） */
  giftSkuId?: string
  /** 赠品 SKU 名称（展示用） */
  giftSkuName?: string
  /** 赠品数量（FullGift，默认 1） */
  giftQuantity?: number
}

/** 促销活动视图（GET /admin/promotions 列表项与详情） */
export interface PromotionActivityDto {
  id: string
  name: string
  type: PromotionType
  status: PromotionStatus
  /** ISO 8601 UTC 字符串 */
  startTime: string
  /** ISO 8601 UTC 字符串 */
  endTime: string
  /** 阶梯规则（按门槛升序） */
  rules: PromotionRuleDto[]
  scope: PromotionScopeType
  /** 指定分类/商品 ID 列表（scope=All 时为空数组） */
  scopeIds: string[]
  createdBy: string
  createdAt: string
}

/** 创建/更新促销活动请求体（POST /admin/promotions、PUT /admin/promotions/{activityId}） */
export interface SavePromotionActivityDto {
  name: string
  type: PromotionType
  startTime: string
  endTime: string
  rules: PromotionRuleDto[]
  scope: PromotionScopeType
  scopeIds: string[]
}

/** GET /admin/promotions 查询参数 */
export interface ListPromotionsParams extends PageQuery {
  /** 活动名称模糊匹配 */
  name?: string
  /** 活动状态精确匹配 */
  status?: PromotionStatus
  /** 活动开始时间下界（ISO 8601 UTC） */
  startTime?: string
  /** 活动结束时间上界（ISO 8601 UTC） */
  endTime?: string
}
