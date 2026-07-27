// web/system-admin/src/modules/05-audit/api/outbox-monitor.api.ts
// Outbox 监控 API：对齐 SystemAdmin BC OutboxMonitorController 端点
// design-prompt 标 🚧 规划中，端点待后端实现；API 层先按 design-prompt §3 完整定义
// 重投（republish）与归档（archive）注入 Idempotency-Key 头

import { client, withIdempotency } from '@/shared/http'
import type { PageResult, PageQuery } from '@/shared/types'
import type {
  OutboxSummaryDto,
  OutboxTrendPointDto,
  OutboxMessageDto,
  OutboxArchiveHistoryDto,
  BatchRepublishOutboxDto,
  ArchiveOutboxDto,
  GetOutboxTrendParams,
  ListOutboxMessagesParams,
} from '../types/outbox.dto'

/** Outbox 消息列表请求（合并分页） */
export type ListOutboxMessagesRequest = ListOutboxMessagesParams & PageQuery

/** 批量重投结果 DTO */
export interface BatchRepublishResultDto {
  /** 成功重投的消息 ID 列表 */
  succeeded: string[]
  /** 失败明细 */
  failed: { messageId: string; reason: string }[]
}

/** 归档结果 DTO */
export interface ArchiveOutboxResultDto {
  /** 实际归档事件数 */
  archivedCount: number
  /** 归档时间（ISO 8601 UTC） */
  archivedAt: string
}

export const outboxMonitorApi = {
  /** 获取各域 Outbox 积压汇总（按域分组表格数据源） */
  getSummary: () =>
    client.get<OutboxSummaryDto[]>('/admin/outbox/summary'),

  /** 获取近 N 小时积压趋势（按域分系列，默认 24 小时） */
  getTrend: (params: GetOutboxTrendParams) =>
    client.get<OutboxTrendPointDto[]>('/admin/outbox/trend', { params }),

  /** 分页查询指定域积压事件详情（详情抽屉列表） */
  listMessages: (params: ListOutboxMessagesRequest) =>
    client.get<PageResult<OutboxMessageDto>>(`/admin/outbox/${params.context}/messages`, {
      params: { page: params.page, pageSize: params.pageSize },
    }),

  /** 批量重投指定域积压事件（幂等） */
  republish: (context: string, body: BatchRepublishOutboxDto) =>
    client.post<BatchRepublishResultDto>(
      `/admin/outbox/${context}/republish`,
      body,
      withIdempotency(),
    ),

  /** 归档指定域陈旧积压事件（幂等） */
  archive: (context: string, body: ArchiveOutboxDto) =>
    client.post<ArchiveOutboxResultDto>(
      `/admin/outbox/${context}/archive`,
      body,
      withIdempotency(),
    ),

  /** 查询指定域归档历史 */
  getArchiveHistory: (context: string) =>
    client.get<OutboxArchiveHistoryDto[]>(`/admin/outbox/${context}/archive-history`),
}
