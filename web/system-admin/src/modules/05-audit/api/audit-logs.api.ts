// web/system-admin/src/modules/05-audit/api/audit-logs.api.ts
// 审计日志 API：对齐 SystemAdmin BC AuditLogsController 端点
// 全部只读（GET），无写操作，不注入 Idempotency-Key
// 导出走 responseType: 'blob'，文件名从 Content-Disposition 解析（在视图层完成）

import { client } from '@/shared/http'
import type { PageResult, PageQuery } from '@/shared/types'
import type {
  AuditLogEntryDto,
  OperationLogDto,
  CrossDomainAuditEntryDto,
  ListAuditLogsParams,
  ListOperationLogsParams,
  ListAuditLogEntriesParams,
  ExportAuditLogsParams,
} from '../types/audit-log.dto'

/** 审计日志列表请求（合并分页） */
export type ListAuditLogsRequest = ListAuditLogsParams & PageQuery

/** 操作日志列表请求（合并分页） */
export type ListOperationLogsRequest = ListOperationLogsParams & PageQuery

/** 跨域审计条目列表请求（合并分页） */
export type ListAuditLogEntriesRequest = ListAuditLogEntriesParams & PageQuery

export const auditLogsApi = {
  /** 分页查询审计日志（按操作人/资源类型/动作/时间） */
  list: (params: ListAuditLogsRequest) =>
    client.get<PageResult<AuditLogEntryDto>>('/admin/audit-logs', { params }),

  /** 获取审计日志条目详情（含前后快照 JSON） */
  get: (id: string) =>
    client.get<AuditLogEntryDto>(`/admin/audit-logs/${id}`),

  /** 导出审计日志为 CSV（blob，文件名由视图层从 Content-Disposition 解析） */
  export: (params: ExportAuditLogsParams) =>
    client.get<Blob>('/admin/audit-logs/export', { params, responseType: 'blob' }),

  /** 分页查询操作日志（按操作人/模块/时间） */
  listOperationLogs: (params: ListOperationLogsRequest) =>
    client.get<PageResult<OperationLogDto>>('/admin/operation-logs', { params }),

  /** 分页查询跨域审计条目（按模块/动作/操作人/时间） */
  listAuditLogEntries: (params: ListAuditLogEntriesRequest) =>
    client.get<PageResult<CrossDomainAuditEntryDto>>('/admin/audit-log-entries', { params }),
}
