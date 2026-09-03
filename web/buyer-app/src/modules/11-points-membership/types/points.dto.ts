/**
 * 积分域 DTO（Points 域 / 旧 PointsMembership 双轨兜底）
 *
 * 端点契约：
 * - GET  /api/points/account           积分账户（余额/签到状态）
 * - GET  /api/points/ledger            积分流水
 * - POST /api/points/check-in          每日签到
 * - GET  /api/points/tasks             任务中心
 * - POST /api/points/tasks/{taskId}/complete 完成任务
 * - POST /api/points/exchange-coupon   积分兑换优惠券
 */

/** 积分账户 */
export interface PointsAccountDto {
  /** 可用积分 */
  balance: number
  /** 累计获得 */
  totalEarned: number
  /** 累计消耗 */
  totalSpent: number
  /** 即将过期积分 */
  expiringPoints: number
  /** 过期时间 */
  expiringAt?: string
  /** 今日是否已签到 */
  checkedInToday: boolean
  /** 连续签到天数 */
  checkInStreakDays: number
}

/** 积分流水类型 */
export type PointsLedgerType = 'Earn' | 'Spend' | 'Expire' | 'Adjust'

/** 积分流水条目 */
export interface PointsLedgerEntryDto {
  id: string
  type: PointsLedgerType
  /** 变动积分（带符号） */
  points: number
  /** 变动后余额 */
  balanceAfter: number
  description: string
  createdAt: string
}

/** 积分任务 */
export interface PointsTaskDto {
  id: string
  name: string
  description: string
  /** 奖励积分 */
  points: number
  /** Vant 图标名 */
  icon: string
  status: 'Pending' | 'Completed'
  /** 完成动作类型（前端路由跳转语义） */
  action: 'CheckIn' | 'Browse' | 'Search' | 'Share' | 'Review' | 'Order' | 'Profile'
  completedAt?: string
}

/** 积分兑换请求 */
export interface ExchangeCouponRequestDto {
  couponId: string
  /** 兑换消耗积分（服务端校验） */
  points: number
}

/** 积分兑换结果 */
export interface ExchangeCouponResultDto {
  success: boolean
  couponName: string
  validTo: string
  /** 兑换后剩余积分 */
  balanceAfter: number
}

/** 签到结果 */
export interface CheckInResultDto {
  /** 本次获得积分 */
  earnedPoints: number
  /** 连续签到天数 */
  streakDays: number
  /** 签到后余额 */
  balanceAfter: number
}
