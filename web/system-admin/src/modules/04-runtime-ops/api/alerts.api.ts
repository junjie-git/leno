// web/system-admin/src/modules/04-runtime-ops/api/alerts.api.ts
// 告警管理 API：对齐 SystemAdmin BC AlertsController + AlertSilencesController 端点
// acknowledge/create silence 均注入 Idempotency-Key 头

import { client, withIdempotency } from '@/shared/http'
import type { PageResult, PageQuery } from '@/shared/types'
import type {
  AlertDto,
  SilenceDto,
  CreateSilenceDto,
  AcknowledgeAlertDto,
  ListAlertsParams,
} from '../types/alert.dto'

export type ListAlertsRequest = ListAlertsParams & PageQuery

/** 告警事件 API */
export const alertApi = {
  /** 分页查询告警事件 */
  list: (params: ListAlertsRequest) =>
    client.get<PageResult<AlertDto>>('/admin/alerts', { params }),

  /** 获取告警详情 */
  get: (id: string) =>
    client.get<AlertDto>(`/admin/alerts/${id}`),

  /** 确认告警（幂等） */
  acknowledge: (id: string, body: AcknowledgeAlertDto) =>
    client.post<AlertDto>(`/admin/alerts/${id}/acknowledge`, body, withIdempotency()),
}

/** 静默规则 API */
export const alertSilenceApi = {
  /** 查询静默规则列表 */
  list: () =>
    client.get<SilenceDto[]>('/admin/alerts/silences'),

  /** 创建静默规则（幂等） */
  create: (body: CreateSilenceDto) =>
    client.post<SilenceDto>('/admin/alerts/silences', body, withIdempotency()),

  /** 删除静默规则 */
  remove: (id: string) =>
    client.delete<void>(`/admin/alerts/silences/${id}`),
}
