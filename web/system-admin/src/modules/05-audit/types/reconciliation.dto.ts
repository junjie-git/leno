// web/system-admin/src/modules/05-audit/types/reconciliation.dto.ts
// 对账状态 + 记录 + 差异项 DTO 与枚举，对齐 SystemAdmin BC StatisticsReconciliationService 契约

/** 对账报表类型（design-prompt §2 区域 B） */
export type ReconciliationReportType =
  | 'OrderGmv'
  | 'PaymentSuccessRate'
  | 'PointsIssued'
  | 'NotificationDelivery'
  | 'AfterSalesVolume'
  | 'ShopRanking'
  | 'ConversionRate'

/** 对账状态：一致 / 有差异 / 失败（design-prompt §4 状态机） */
export type ReconciliationStatus = 'Consistent' | 'Discrepancy' | 'Failed'

/** 对账状态汇总 DTO（顶部 4 个统计卡片数据源，design-prompt §3） */
export interface ReconciliationStatusDto {
  /** 是否已执行过对账 */
  hasRun: boolean
  /** 最近一次对账状态 */
  status: ReconciliationStatus | null
  /** 最近一次对账的报表类型（全部对账时为 null） */
  reportType: ReconciliationReportType | null
  /** 最近一次对账时间（ISO 8601 UTC） */
  reconciledAt: string | null
  /** 差异项数量 */
  discrepancyCount: number
  /** 是否一致 */
  isConsistent: boolean
  /** 是否触发告警 */
  alertTriggered: boolean
  /** 是否触发修正 */
  correctionTriggered: boolean
}

/** 对账差异项明细 DTO（详情抽屉列表展示） */
export interface ReconciliationDiscrepancyDto {
  /** 报表类型 */
  reportType: ReconciliationReportType
  /** 指标名（如 OrderGmv/PaymentSuccess/PointsIssued） */
  metricName: string
  /** 期望值 */
  expectedValue: number
  /** 实际值 */
  actualValue: number
  /** 差异值（actual - expected） */
  diffValue: number
}

/** 对账记录响应 DTO（design-prompt §3） */
export interface ReconciliationRecordDto {
  /** 记录 ID */
  recordId: string
  /** 报表类型 */
  reportType: ReconciliationReportType
  /** 对账时间（ISO 8601 UTC） */
  reconciledAt: string
  /** 对账状态 */
  status: ReconciliationStatus
  /** 差异项数量 */
  discrepancyCount: number
  /** 是否触发告警 */
  alertTriggered: boolean
  /** 是否触发修正 */
  correctionTriggered: boolean
  /** 错误信息（对账失败时非空） */
  errorMessage: string | null
  /** 差异项明细列表（详情视图展开时由后端填充，列表查询时可能为空数组） */
  discrepancies: ReconciliationDiscrepancyDto[]
}

/** 触发对账请求参数（query 参数形式，design-prompt §3） */
export interface TriggerReconciliationParams {
  /** 报表类型，未传则对账全部类型 */
  reportType?: ReconciliationReportType
  /** 起始时间（ISO 8601 UTC） */
  start?: string
  /** 结束时间（ISO 8601 UTC） */
  end?: string
}

/** 对账记录列表查询参数 */
export interface ListReconciliationRecordsParams {
  reportType?: ReconciliationReportType
  start?: string
  end?: string
  page?: number
  pageSize?: number
}
