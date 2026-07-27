// web/system-admin/src/modules/04-runtime-ops/api/health.api.ts
// 健康监控 API：对齐 SystemAdmin BC HealthController 端点
// 只读接口，无写操作不需要 Idempotency-Key

import { client } from '@/shared/http'
import type { HealthAggregationResultDto, ModuleHealthDto } from '../types/health.dto'

export const healthApi = {
  /** 获取聚合健康状态（整体 + 各模块概要） */
  getAggregated: () =>
    client.get<HealthAggregationResultDto>('/admin/health'),

  /** 获取各模块健康详情列表（含依赖项明细） */
  getModules: () =>
    client.get<ModuleHealthDto[]>('/admin/health/modules'),
}
