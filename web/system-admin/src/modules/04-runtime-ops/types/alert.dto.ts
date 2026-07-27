// web/system-admin/src/modules/04-runtime-ops/types/alert.dto.ts
// 告警 + 静默规则 DTO 与枚举，对齐 SystemAdmin BC AlertsController + AlertSilencesController 契约

/** 告警级别 */
export type AlertSeverity = 'critical' | 'warning' | 'info'

/** 告警状态 */
export type AlertStatus = 'firing' | 'acknowledged' | 'resolved'

/** 告警事件 DTO */
export interface AlertDto {
  alertId: string
  name: string
  module: string
  severity: AlertSeverity
  status: AlertStatus
  triggeredAt: string
  durationSeconds: number
  labels: Record<string, string>
  annotations: Record<string, string>
  summary: string
  description: string
  relatedMetric: string | null
}

/** 静默规则匹配器 */
export interface SilenceMatcherDto {
  name: string
  value: string
  isRegex: boolean
}

/** 静默规则 DTO */
export interface SilenceDto {
  silenceId: string
  matchers: SilenceMatcherDto[]
  startsAt: string
  endsAt: string
  reason: string
  createdBy: string
}

/** 创建静默规则请求 DTO */
export interface CreateSilenceDto {
  matchers: SilenceMatcherDto[]
  durationMinutes: number
  reason: string
}

/** 确认告警请求 DTO */
export interface AcknowledgeAlertDto {
  comment: string
}

/** 列表查询参数 */
export interface ListAlertsParams {
  module?: string[]
  severity?: AlertSeverity[]
  status?: AlertStatus[]
  startTime?: string
  endTime?: string
  page?: number
  pageSize?: number
}
