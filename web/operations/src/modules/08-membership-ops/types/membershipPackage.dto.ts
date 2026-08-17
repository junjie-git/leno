/**
 * 08-membership-ops 会员套餐 DTO
 *
 * 对接 Membership 域（旧域 PointsMembership 双轨兜底）：
 * - GET  /api/membership-packages                            共享字典（买家端复用，运营可过滤启用）
 * - POST /api/admin/membership-packages                      创建套餐
 * - PUT  /api/admin/membership-packages/{packageId}          更新套餐
 * - POST /api/admin/membership-packages/{packageId}/enable   启用套餐
 * - POST /api/admin/membership-packages/{packageId}/disable  停用套餐
 *
 * 状态机：Inactive ↔ Active 双向切换；停用不影响已订阅用户权益。
 */

/** 会员套餐状态 */
export type MembershipPackageStatus = 'Active' | 'Inactive'

/** 会员权益枚举（五项固定权益） */
export type MembershipBenefit =
  | 'ExclusiveService'
  | 'BirthdayGift'
  | 'Discount'
  | 'PointsAccelerator'
  | 'FreeReturn'

/** 权益码 → 中文标签映射 */
export const MEMBERSHIP_BENEFIT_LABELS: Record<MembershipBenefit, string> = {
  ExclusiveService: '专属客服',
  BirthdayGift: '生日礼包',
  Discount: '购物折扣',
  PointsAccelerator: '积分加速',
  FreeReturn: '免费退换',
}

/** 权益全量选项（表单 checkbox-group 与摘要展示共用） */
export const MEMBERSHIP_BENEFITS: MembershipBenefit[] = [
  'ExclusiveService',
  'BirthdayGift',
  'Discount',
  'PointsAccelerator',
  'FreeReturn',
]

/** 会员套餐视图 */
export interface MembershipPackageDto {
  id: string
  /** 套餐名称（必填） */
  name: string
  /** 价格（元，> 0，两位小数） */
  price: number
  /** 时长天数（30 / 90 / 365） */
  durationDays: number
  /** 关联会员等级 ID（须为已启用等级） */
  linkedLevelId: string
  /** 关联等级名称（后端冗余返回，列表展示用） */
  linkedLevelName?: string
  /** 权益码列表 */
  benefits: MembershipBenefit[]
  /** 当前订阅数 */
  subscriberCount: number
  status: MembershipPackageStatus
  /** 创建时间（ISO 8601） */
  createdAt: string
}

/** GET /api/membership-packages 查询参数 */
export interface MembershipPackageQueryParams {
  /** 状态筛选（运营按需过滤启用） */
  status?: MembershipPackageStatus
}

/** 创建 / 更新会员套餐请求体（CreateMembershipPackageDto / UpdateMembershipPackageDto 同构） */
export interface SaveMembershipPackageDto {
  /** 套餐名称 */
  name: string
  /** 价格（元） */
  price: number
  /** 时长天数 */
  durationDays: number
  /** 关联会员等级 ID */
  linkedLevelId: string
  /** 权益码列表 */
  benefits: MembershipBenefit[]
  status: MembershipPackageStatus
}

/** 创建会员套餐请求体 */
export type CreateMembershipPackageDto = SaveMembershipPackageDto

/** 更新会员套餐请求体 */
export type UpdateMembershipPackageDto = SaveMembershipPackageDto
