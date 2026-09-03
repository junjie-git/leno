/**
 * 会员域 DTO（Membership 域 / 旧 PointsMembership 双轨兜底）
 *
 * 端点契约：
 * - GET  /api/members/me                          我的会员信息（等级/权益）
 * - GET  /api/membership-packages                 付费会员套餐列表
 * - POST /api/membership-packages/{packageId}/subscribe 订阅会员套餐
 */

/** 会员等级（V1-V6） */
export interface MemberLevelInfoDto {
  level: number
  name: string
  /** 升级所需成长值门槛 */
  threshold: number
  icon: string
  benefits: string[]
}

/** 我的会员信息 */
export interface MemberProfileDto {
  level: number
  levelName: string
  /** 当前成长值（累计积分） */
  points: number
  nextLevelName?: string
  nextLevelPoints?: number
  benefits: string[]
  joinedAt: string
  /** 付费会员到期时间（非付费会员为空） */
  premiumExpireAt?: string
  /** 是否付费会员 */
  isPremium: boolean
}

/** 付费会员套餐 */
export interface MembershipPackageDto {
  id: string
  name: string
  /** 订阅价格（分） */
  price: number
  originalPrice: number
  durationDays: number
  benefits: string[]
  /** 角标（人气/超值等） */
  tag?: string
}

/** 订阅结果 */
export interface SubscribeResultDto {
  success: boolean
  orderId: string
  premiumExpireAt: string
}
