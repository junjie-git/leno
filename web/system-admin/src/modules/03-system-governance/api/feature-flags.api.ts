// web/system-admin/src/modules/03-system-governance/api/feature-flags.api.ts
// 功能开关管理 API（SystemAdmin 域 FeatureFlagsController）

import { client, withIdempotency } from '@/shared/http'
import type { PageResult, PageQuery } from '@/shared/types'
import type {
  FeatureFlagDto,
  SaveFeatureFlagDto,
  EvaluateFlagDto,
  EvaluateFlagResultDto,
  ListFeatureFlagsParams,
} from '../types/feature-flag.dto'

// 功能开关 API：list/create/update/enable/disable/evaluate
export const featureFlagsApi = {
  // 分页查询功能开关
  list: (params: ListFeatureFlagsParams & PageQuery): Promise<PageResult<FeatureFlagDto>> =>
    client.get<PageResult<FeatureFlagDto>>('/admin/feature-flags', { params }).then((r) => r.data),

  // 创建功能开关（幂等）
  create: (body: SaveFeatureFlagDto): Promise<FeatureFlagDto> =>
    client.post<FeatureFlagDto>('/admin/feature-flags', body, withIdempotency()).then((r) => r.data),

  // 更新功能开关（key 不可变，幂等）
  update: (flagId: string, body: SaveFeatureFlagDto): Promise<FeatureFlagDto> =>
    client.put<FeatureFlagDto>(`/admin/feature-flags/${flagId}`, body, withIdempotency()).then((r) => r.data),

  // 启用开关（幂等）
  enable: (flagId: string): Promise<FeatureFlagDto> =>
    client.post<FeatureFlagDto>(`/admin/feature-flags/${flagId}/enable`, null, withIdempotency()).then((r) => r.data),

  // 停用开关（幂等）
  disable: (flagId: string): Promise<FeatureFlagDto> =>
    client.post<FeatureFlagDto>(`/admin/feature-flags/${flagId}/disable`, null, withIdempotency()).then((r) => r.data),

  // 按上下文评估开关是否生效（幂等）
  evaluate: (body: EvaluateFlagDto): Promise<EvaluateFlagResultDto> =>
    client.post<EvaluateFlagResultDto>('/admin/feature-flags/evaluate', body, withIdempotency()).then((r) => r.data),
}
