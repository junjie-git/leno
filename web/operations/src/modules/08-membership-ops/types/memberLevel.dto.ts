/**
 * 08-membership-ops 会员等级 DTO
 *
 * 对接 Membership 域（旧域 PointsMembership 双轨兜底）：
 * - GET  /api/admin/members/levels                    查询全部等级（按编号升序）
 * - POST /api/admin/members/levels                    创建等级
 * - PUT  /api/admin/members/levels/{levelId}          更新等级
 * - POST /api/admin/members/levels/{levelId}/enable   启用等级
 * - POST /api/admin/members/levels/{levelId}/disable  停用等级
 *
 * 状态机：Inactive ↔ Active 双向切换；停用不影响已有该等级的会员。
 */

/** 会员等级状态 */
export type MemberLevelStatus = 'Active' | 'Inactive'

/** 会员等级视图 */
export interface MemberLevelDto {
  id: string
  /** 等级编号（自动递增，不可修改） */
  levelNo: number
  /** 等级名称（必填，1-20 字） */
  name: string
  /** 成长值门槛（须大于上一等级、小于下一等级） */
  growthThreshold: number
  /** 折扣率 0-1（如 0.95 表示 95 折，须优于上一等级递减） */
  discountRate: number
  /** 权益说明（≤200 字） */
  benefits?: string
  status: MemberLevelStatus
  /** 当前该等级的会员数 */
  memberCount: number
  /** 创建时间（ISO 8601） */
  createdAt: string
}

/** 创建 / 更新会员等级请求体（CreateMembershipLevelDto / UpdateMembershipLevelDto 同构） */
export interface SaveMemberLevelDto {
  /** 等级名称（必填，1-20 字） */
  name: string
  /** 成长值门槛 */
  growthThreshold: number
  /** 折扣率 0-1 */
  discountRate: number
  /** 权益说明 */
  benefits?: string
  status: MemberLevelStatus
}

/** 创建会员等级请求体 */
export type CreateMemberLevelDto = SaveMemberLevelDto

/** 更新会员等级请求体 */
export type UpdateMemberLevelDto = SaveMemberLevelDto
