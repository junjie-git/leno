// web/system-admin/src/modules/04-runtime-ops/types/health.dto.ts
// 健康聚合 + 模块健康 + 依赖项 DTO，对齐 SystemAdmin BC HealthController 契约

/** 整体健康状态：健康 / 降级 / 不健康 */
export type OverallStatus = 'Healthy' | 'Degraded' | 'Unhealthy'

/** 单依赖项状态 */
export type DependencyStatus = 'Healthy' | 'Degraded' | 'Unhealthy'

/** 依赖项 DTO */
export interface DependencyHealthDto {
  name: string
  status: DependencyStatus
  latencyMs: number
  error: string | null
  lastCheckedAt: string
}

/** 模块健康 DTO */
export interface ModuleHealthDto {
  moduleName: string
  status: DependencyStatus
  latencyMs: number
  dependencies: DependencyHealthDto[]
}

/** 聚合健康结果 DTO */
export interface HealthAggregationResultDto {
  overallStatus: OverallStatus
  checkedAt: string
  modules: ModuleHealthDto[]
}
