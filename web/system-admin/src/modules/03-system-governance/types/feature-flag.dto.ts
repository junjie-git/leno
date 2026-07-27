// web/system-admin/src/modules/03-system-governance/types/feature-flag.dto.ts
// 功能开关 DTO 类型定义（对应后端 FeatureFlagsController）

// 开关状态：Enabled 启用 / Disabled 停用
export type FeatureFlagStatus = 'Enabled' | 'Disabled'

// 功能开关响应 DTO（对应后端 FeatureFlagDto，字段 camelCase 由 System.Text.Json 序列化）
export interface FeatureFlagDto {
  flagId: string
  key: string                    // 业务键，新建时可编辑、编辑时只读
  description: string
  group: string                  // 分组，如 payment / order / notify
  status: FeatureFlagStatus
  ruleJson: string               // 规则配置 JSON 字符串
  updatedAt: string              // 最近变更时间 ISO 8601
  updatedBy: string              // 最近变更人
}

// 创建/更新开关请求 DTO（POST/PUT /admin/feature-flags[/{flagId}]）
export interface SaveFeatureFlagDto {
  key: string
  description: string
  group: string
  ruleJson: string
  status: FeatureFlagStatus
}

// 评估开关请求 DTO（POST /admin/feature-flags/evaluate）
export interface EvaluateFlagDto {
  key: string
  context: Record<string, unknown>  // userId / role / shopId 等上下文
}

// 评估开关结果 DTO
export interface EvaluateFlagResultDto {
  enabled: boolean               // 是否生效
  matchedRule: string            // 命中规则描述
}

// 列表查询参数（GET /admin/feature-flags）
export interface ListFeatureFlagsParams {
  key?: string                   // key 模糊搜索
  status?: FeatureFlagStatus[]   // 状态多选
  group?: string                 // 分组精确匹配
}
