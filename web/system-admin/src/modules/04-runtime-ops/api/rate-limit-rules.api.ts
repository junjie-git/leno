// web/system-admin/src/modules/04-runtime-ops/api/rate-limit-rules.api.ts
// 限流规则 API：对齐 SystemAdmin BC RateLimitRulesController 端点
// update 携带 X-Resource-Version 乐观锁头；enable/disable/create/update 均注入 Idempotency-Key

import { client, withIdempotency } from '@/shared/http'
import type { PageResult, PageQuery } from '@/shared/types'
import type {
  RateLimitRuleDto,
  SaveRateLimitRuleDto,
  ListRateLimitRulesParams,
} from '../types/rate-limit-rule.dto'

export type ListRateLimitRulesRequest = ListRateLimitRulesParams & PageQuery

export const rateLimitRuleApi = {
  /** 分页查询限流规则 */
  list: (params: ListRateLimitRulesRequest) =>
    client.get<PageResult<RateLimitRuleDto>>('/admin/rate-limit-rules', { params }),

  /** 获取限流规则详情 */
  get: (id: string) =>
    client.get<RateLimitRuleDto>(`/admin/rate-limit-rules/${id}`),

  /** 创建限流规则（幂等） */
  create: (body: SaveRateLimitRuleDto) =>
    client.post<RateLimitRuleDto>('/admin/rate-limit-rules', body, withIdempotency()),

  /** 更新限流规则（乐观锁 + 幂等） */
  update: (id: string, body: SaveRateLimitRuleDto) =>
    client.put<RateLimitRuleDto>(`/admin/rate-limit-rules/${id}`, body, {
      headers: {
        'X-Resource-Version': body.version ?? 0,
        ...withIdempotency().headers,
      },
    }),

  /** 启用限流规则（幂等） */
  enable: (id: string) =>
    client.post<RateLimitRuleDto>(`/admin/rate-limit-rules/${id}/enable`, null, withIdempotency()),

  /** 停用限流规则（幂等） */
  disable: (id: string) =>
    client.post<RateLimitRuleDto>(`/admin/rate-limit-rules/${id}/disable`, null, withIdempotency()),
}
