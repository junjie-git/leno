import type { PageQuery } from '@/shared/types'

/**
 * 03-promotion-ops 优惠券模板 DTO
 *
 * 对接后端 CouponController（/api/admin/coupons）：
 * - 券类型三选一：满减券 / 折扣券 / 无门槛券
 * - 状态机：Draft（草稿）→ Published（已发布/启用）→ Stopped（已停用）
 * - 库存三元组：总量 / 已领 / 剩余（剩余 = 总量 - 已领）
 */

/** 优惠券类型：FullReduction=满减券 / Discount=折扣券 / NoThreshold=无门槛券 */
export type CouponType = 'FullReduction' | 'Discount' | 'NoThreshold'

/** 券模板状态机：Draft → Published → Stopped（已停用不可再发布） */
export type CouponTemplateStatus = 'Draft' | 'Published' | 'Stopped'

/** 有效期类型：FixedRange=固定区间 / AfterReceiveDays=领取后 N 天 */
export type CouponValidityType = 'FixedRange' | 'AfterReceiveDays'

/** 券模板视图（GET /admin/coupons 列表项） */
export interface CouponDto {
  id: string
  name: string
  type: CouponType
  /** 面额（元）：FullReduction / NoThreshold 必填，且满减券面额不超门槛 */
  faceValue: number
  /** 折扣率（0-1）：Discount 必填，如 0.9 = 9 折 */
  discountRate?: number
  /** 折扣上限（元）：Discount 可选，单张券最高优惠金额 */
  discountCap?: number
  /** 使用门槛（元）：FullReduction 必填；Discount / NoThreshold 为 0（无门槛） */
  threshold: number
  validityType: CouponValidityType
  /** FixedRange 生效起点（ISO 8601 UTC） */
  validFrom?: string
  /** FixedRange 生效终点（ISO 8601 UTC） */
  validTo?: string
  /** AfterReceiveDays 领取后 N 天有效 */
  validDays?: number
  /** 发放总量 */
  totalQuantity: number
  /** 已领取（含批量发放）数量 */
  issuedQuantity: number
  /** 剩余可发放库存 = totalQuantity - issuedQuantity */
  remainingQuantity: number
  /** 每人限领数量 */
  perUserLimit: number
  status: CouponTemplateStatus
  createdAt: string
}

/** 创建/更新券模板请求体（POST /admin/coupons、PUT /admin/coupons/{couponId}） */
export interface SaveCouponDto {
  name: string
  type: CouponType
  /** 面额（元）：FullReduction / NoThreshold 必填 */
  faceValue?: number
  /** 门槛（元）：FullReduction 必填 */
  threshold?: number
  /** 折扣率（0-1）：Discount 必填 */
  discountRate?: number
  /** 折扣上限（元）：Discount 可选 */
  discountCap?: number
  validityType: CouponValidityType
  /** FixedRange 生效起点（ISO 8601 UTC） */
  validFrom?: string
  /** FixedRange 生效终点（ISO 8601 UTC） */
  validTo?: string
  /** AfterReceiveDays 领取后 N 天有效 */
  validDays?: number
  /** 发放总量（≥1） */
  totalQuantity: number
  /** 每人限领（≥1） */
  perUserLimit: number
}

/** GET /admin/coupons 查询参数 */
export interface ListCouponsParams extends PageQuery {
  /** 券模板状态精确匹配 */
  status?: CouponTemplateStatus
  /** 名称关键词模糊匹配 */
  keyword?: string
  /** 券类型精确匹配 */
  type?: CouponType
}
