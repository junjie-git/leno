// web/system-admin/src/modules/03-system-governance/types/system-config.dto.ts
// 系统配置 DTO 类型定义（对应后端 SystemConfigsController）

// 配置状态：Enabled 启用 / Disabled 停用
export type SystemConfigStatus = 'Enabled' | 'Disabled'

// 配置值类型：String 字符串 / Int 整数 / Bool 布尔 / Json JSON / Secret 敏感
export type SystemConfigValueType = 'String' | 'Int' | 'Bool' | 'Json' | 'Secret'

// 系统配置响应 DTO（值始终掩码，Secret 类型形如 ****）
export interface SystemConfigDto {
  configId: string
  key: string                    // 配置键，编辑时只读
  group: string                  // 分组，如 payment / notify / cart / search
  valueType: SystemConfigValueType
  valueMasked: string            // 掩码值，Secret 类型为 ****
  description: string
  status: SystemConfigStatus
  updatedAt: string              // ISO 8601
}

// 创建/更新配置请求 DTO（POST/PUT /admin/system-configs[/{configId}]）
export interface SaveSystemConfigDto {
  key: string
  group: string
  valueType: SystemConfigValueType
  value: string                  // 明文值，Secret 类型创建/更新时必填
  description: string
}

// 明文配置响应 DTO（GET /admin/system-configs/by-key/{key}，需 config:reveal 权限）
export interface SystemConfigRevealDto {
  configId: string
  key: string
  value: string                  // 明文值
}

// 列表查询参数（GET /admin/system-configs）
export interface ListSystemConfigsParams {
  key?: string                   // key 模糊搜索
  group?: string                 // 分组精确匹配
  status?: SystemConfigStatus[]  // 状态多选
}

// 分组项（GET /admin/system-configs/groups 返回）
export interface SystemConfigGroupDto {
  group: string
  count: number                  // 该分组下配置数
}
