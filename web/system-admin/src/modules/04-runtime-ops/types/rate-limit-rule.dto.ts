// web/system-admin/src/modules/04-runtime-ops/types/rate-limit-rule.dto.ts
// 限流规则 DTO 与枚举，对齐 SystemAdmin BC RateLimitRulesController 契约

/** 限流算法 */
export type RateLimitAlgorithm = 'SlidingWindow' | 'TokenBucket' | 'FixedWindow'

/** 限流维度 */
export type RateLimitScope = 'IP' | 'User' | 'Global' | 'Shop'

/** 限流规则响应 DTO（spec §3.8 含 Version 字段用于乐观锁） */
export interface RateLimitRuleDto {
  ruleId: string
  targetApi: string
  targetContext: string
  limit: number
  windowSeconds: number
  algorithm: RateLimitAlgorithm
  scope: RateLimitScope
  enabled: boolean
  updatedBy: string
  updatedAt: string
  version: number
}

/** 创建/更新限流规则请求 DTO */
export interface SaveRateLimitRuleDto {
  targetApi: string
  targetContext: string
  limit: number
  windowSeconds: number
  algorithm: RateLimitAlgorithm
  scope: RateLimitScope
  /** 编辑时携带，用于乐观锁；新建时省略 */
  version?: number
}

/** 列表查询参数 */
export interface ListRateLimitRulesParams {
  targetApi?: string
  enabled?: boolean
  targetContext?: string[]
  page?: number
  pageSize?: number
}
