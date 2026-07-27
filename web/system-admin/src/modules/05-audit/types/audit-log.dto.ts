// web/system-admin/src/modules/05-audit/types/audit-log.dto.ts
// 审计日志 + 操作日志 + 跨域审计条目 DTO，对齐 SystemAdmin BC AuditLogsController 契约
// 审计日志只读，不可编辑；详情含 BeforeSnapshot/AfterSnapshot/RequestSummary，前端对敏感键掩码展示

/** 操作人角色（用于行/详情着色） */
export type OperatorRole = 'Admin' | 'Operator' | 'Seller' | 'Buyer' | 'System'

/** 审计日志条目响应 DTO（design-prompt §3） */
export interface AuditLogEntryDto {
  /** 日志 ID */
  logId: string
  /** 操作人 ID */
  operatorId: string
  /** 操作人名称 */
  operatorName: string
  /** 操作人角色 */
  operatorRole: OperatorRole
  /** 来源上下文（限界上下文名，如 Order/Payment） */
  sourceContext: string
  /** 操作动作（如 Create/Update/Delete/Login/Export） */
  action: string
  /** 资源类型（如 Shop/Role/DeadLetter/Reconciliation） */
  resourceType: string
  /** 资源 ID */
  resourceId: string
  /** 请求摘要（含 path/method/query，可能含敏感参数，前端掩码展示） */
  requestSummary: string
  /** HTTP 响应状态码（200/403/500 等） */
  responseStatus: number
  /** 客户端 IP */
  ipAddress: string
  /** User-Agent */
  userAgent: string
  /** 链路追踪 ID */
  traceId: string
  /** 操作前快照（JSON 字符串，可能含敏感字段，前端掩码展示） */
  beforeSnapshot: string | null
  /** 操作后快照（JSON 字符串，可能含敏感字段，前端掩码展示） */
  afterSnapshot: string | null
  /** 发生时间（ISO 8601 UTC） */
  occurredAt: string
}

/** 操作日志条目响应 DTO（design-prompt §3 operation-logs） */
export interface OperationLogDto {
  /** 日志 ID */
  logId: string
  /** 操作人 ID */
  operatorId: string
  /** 操作人名称 */
  operatorName: string
  /** 操作人角色 */
  operatorRole: OperatorRole
  /** 所属模块（如 Order/Payment/Identity） */
  module: string
  /** 操作动作 */
  action: string
  /** 资源类型 */
  resourceType: string
  /** 资源 ID */
  resourceId: string
  /** 操作详情（人类可读） */
  detail: string
  /** IP 地址 */
  ipAddress: string
  /** 链路追踪 ID */
  traceId: string
  /** 发生时间（ISO 8601 UTC） */
  occurredAt: string
}

/** 跨域审计条目响应 DTO（design-prompt §3 audit-log-entries） */
export interface CrossDomainAuditEntryDto {
  /** 条目 ID */
  entryId: string
  /** 限界上下文/模块 */
  module: string
  /** 操作动作 */
  action: string
  /** 操作人 ID */
  operatorId: string
  /** 操作人名称 */
  operatorName: string
  /** 资源类型 */
  resourceType: string
  /** 资源 ID */
  resourceId: string
  /** 链路追踪 ID */
  traceId: string
  /** 发生时间（ISO 8601 UTC） */
  occurredAt: string
}

/** 审计日志列表查询参数（design-prompt §3 请求参数） */
export interface ListAuditLogsParams {
  operatorId?: string
  resourceType?: string
  action?: string
  fromTime?: string
  toTime?: string
  page?: number
  pageSize?: number
}

/** 操作日志列表查询参数 */
export interface ListOperationLogsParams {
  operatorId?: string
  module?: string
  fromTime?: string
  toTime?: string
  page?: number
  pageSize?: number
}

/** 跨域审计条目列表查询参数 */
export interface ListAuditLogEntriesParams {
  module?: string
  action?: string
  operatorId?: string
  fromTime?: string
  toTime?: string
  page?: number
  pageSize?: number
}

/** 导出审计日志查询参数（不分页） */
export interface ExportAuditLogsParams {
  operatorId?: string
  resourceType?: string
  action?: string
  fromTime?: string
  toTime?: string
}
