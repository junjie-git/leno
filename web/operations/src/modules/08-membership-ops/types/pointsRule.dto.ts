/**
 * 08-membership-ops 积分规则 DTO
 *
 * 对接 Points 域（旧域 PointsMembership 双轨兜底）：
 * - GET  /api/admin/points/rules                   查询全部积分规则
 * - POST /api/admin/points/rules                   创建规则
 * - PUT  /api/admin/points/rules/{ruleId}          更新规则
 * - POST /api/admin/points/rules/{ruleId}/enable   启用规则
 * - POST /api/admin/points/rules/{ruleId}/disable  停用规则
 * - POST /api/admin/points/award                   运营手动发放积分
 *
 * 状态机：Inactive ↔ Active 双向切换；规则编码唯一，创建后不可修改。
 */

/** 积分规则状态 */
export type PointsRuleStatus = 'Active' | 'Inactive'

/** 积分行为类型 */
export type PointsActionType =
  | 'SignUp'
  | 'DailyCheckIn'
  | 'OrderComplete'
  | 'ReviewSubmit'
  | 'ShareProduct'
  | 'ProfileComplete'

/** 行为类型 → 中文标签映射 */
export const POINTS_ACTION_TYPE_LABELS: Record<PointsActionType, string> = {
  SignUp: '注册',
  DailyCheckIn: '签到',
  OrderComplete: '下单',
  ReviewSubmit: '评价',
  ShareProduct: '分享',
  ProfileComplete: '完善资料',
}

/** 行为类型全量选项（表单 select 与表格展示共用） */
export const POINTS_ACTION_TYPES: PointsActionType[] = [
  'SignUp',
  'DailyCheckIn',
  'OrderComplete',
  'ReviewSubmit',
  'ShareProduct',
  'ProfileComplete',
]

/** 积分规则视图 */
export interface PointsRuleDto {
  id: string
  /** 规则编码（唯一，创建后不可修改，大写字母 + 下划线） */
  code: string
  /** 规则名称 */
  name: string
  /** 行为类型 */
  actionType: PointsActionType
  /** 积分值（-1000 ~ 1000 非 0，正数发放 / 负数扣减） */
  points: number
  /** 每日上限（1-100 次 / 日） */
  dailyLimit: number
  status: PointsRuleStatus
  /** 更新时间（ISO 8601） */
  updatedAt: string
}

/** 创建 / 更新积分规则请求体（CreatePointsRuleDto / UpdatePointsRuleDto 同构） */
export interface SavePointsRuleDto {
  /** 规则编码 */
  code: string
  /** 规则名称 */
  name: string
  /** 行为类型 */
  actionType: PointsActionType
  /** 积分值 */
  points: number
  /** 每日上限 */
  dailyLimit: number
  status: PointsRuleStatus
}

/** 创建积分规则请求体 */
export type CreatePointsRuleDto = SavePointsRuleDto

/** 更新积分规则请求体 */
export type UpdatePointsRuleDto = SavePointsRuleDto

/** 手动发放积分请求体（AwardPointsDto） */
export interface AwardPointsDto {
  /** 目标用户 ID */
  userId: string
  /** 发放积分值（正整数） */
  points: number
  /** 发放原因（≥5 字） */
  reason: string
}
