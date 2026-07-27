// 仪表盘报表类型枚举（与后端 ReportType 对齐）
export type ReportType =
  | 'OrderGmv'
  | 'PaymentSuccessRate'
  | 'PointsIssued'
  | 'NotificationDelivery'
  | 'AfterSalesVolume'
  | 'ShopRanking'

// 数据粒度枚举
export type Granularity = 'Hour' | 'Day' | 'Week' | 'Month'

// 单个 Metric 项（后端 Metrics 数组元素，Value 为 unknown 由解析函数转型）
export interface DashboardMetricDto {
  Key: string
  Value: unknown
  Unit?: string
}

// 仪表盘报表 DTO（7 个端点统一返回结构）
export interface DashboardReportDto {
  ReportId: string
  ReportType: ReportType
  PeriodStart: string
  PeriodEnd: string
  Granularity: Granularity
  GeneratedAt: string
  DataVersion?: number
  Metrics: DashboardMetricDto[]
}

// ---- 请求参数 ----
export interface DateRangeParams {
  start: string // ISO 8601 起始时间
  end: string   // ISO 8601 结束时间
}

export interface ReportListParams extends DateRangeParams {
  reportType: ReportType
}

// ---- 运营总览解析数据 ----
export interface OverviewKpi {
  orderCount: number
  gmv: number
  conversionRate: number
  avgOrderAmount: number
}

export interface OverviewKpiChange {
  orderCountChange: number
  gmvChange: number
  conversionRateChange: number
  avgOrderAmountChange: number
}

export interface OverviewDailyTrendPoint {
  date: string
  gmv: number
  orderCount: number
}

export interface SourceDistributionItem {
  source: string
  value: number
}

export interface FunnelStage {
  stage: string
  value: number
}

export interface OverviewData {
  kpi: OverviewKpi
  change: OverviewKpiChange
  dailyTrend: OverviewDailyTrendPoint[]
  sourceDistribution: SourceDistributionItem[]
  funnel: FunnelStage[]
}

// ---- 支付统计解析数据 ----
export interface PaymentKpi {
  totalCount: number
  successRate: number
  avgLatencyMs: number
}

export interface ChannelStat {
  channel: string
  successRate: number
  count: number
}

export interface FailureReason {
  reason: string
  count: number
}

export interface PaymentStatsData {
  kpi: PaymentKpi
  channelStats: ChannelStat[]
  failureReasons: FailureReason[]
}

// ---- 积分统计解析数据 ----
export interface PointsKpi {
  issued: number
  consumed: number
  net: number
}

export interface PointsKpiChange {
  issuedChange: number
  consumedChange: number
  netChange: number
}

export interface PointsDailyTrendPoint {
  date: string
  issued: number
  consumed: number
}

export interface PointsStatsData {
  kpi: PointsKpi
  change: PointsKpiChange
  dailyTrend: PointsDailyTrendPoint[]
  sourceDistribution: SourceDistributionItem[]
}

// ---- 通知送达率解析数据 ----
export interface NotificationChannelStat {
  channel: string
  deliveryRate: number
  totalCount: number
  failedCount: number
}

export interface NotificationFailureReason {
  channel: string
  reason: string
  count: number
  lastOccurredAt: string
}

export interface NotificationDailyTrendPoint {
  date: string
  channel: string
  rate: number
}

export interface NotificationDeliveryData {
  channelStats: NotificationChannelStat[]
  failureReasons: NotificationFailureReason[]
  dailyTrend: NotificationDailyTrendPoint[]
}

// ---- 售后统计解析数据 ----
export interface AfterSalesKpi {
  afterSalesCount: number
  refundAmount: number
  afterSalesRate: number
}

export interface AfterSalesKpiChange {
  afterSalesCountChange: number
  refundAmountChange: number
  afterSalesRateChange: number
}

export interface TypeDistributionItem {
  type: string
  count: number
}

export interface AfterSalesDailyTrendPoint {
  date: string
  count: number
  refundAmount: number
}

export interface TopShopByAfterSales {
  shopId: string
  shopName: string
  afterSalesCount: number
  orderCount: number
  avgProcessHours: number
}

export interface AfterSalesStatsData {
  kpi: AfterSalesKpi
  change: AfterSalesKpiChange
  typeDistribution: TypeDistributionItem[]
  dailyTrend: AfterSalesDailyTrendPoint[]
  topShops: TopShopByAfterSales[]
}

// ---- 店铺排行解析数据 ----
export type ShopStatus = 'Active' | 'Suspended' | 'Closed'

export interface ShopRankingItem {
  shopId: string
  shopName: string
  category: string
  salesAmount: number
  orderCount: number
  avgOrderAmount: number
  growthRate: number
  status: ShopStatus
}

export interface ShopRankingData {
  dimension: string
  items: ShopRankingItem[]
}

// ---- Metric 提取工具函数 ----

// 从报表中按 Key 提取 Metric 值，未找到返回 undefined
export function getMetric<T = unknown>(report: DashboardReportDto, key: string): T | undefined {
  const metric = report.Metrics.find((m) => m.Key === key)
  return metric ? (metric.Value as T) : undefined
}

// 从报表中提取数值型 Metric，未找到或非数值返回 0
export function getNumberMetric(report: DashboardReportDto, key: string): number {
  const value = getMetric<number>(report, key)
  return typeof value === 'number' ? value : 0
}

// 从报表中提取数组型 Metric，未找到或非数组返回空数组
export function getArrayMetric<T>(report: DashboardReportDto, key: string): T[] {
  const value = getMetric<T[]>(report, key)
  return Array.isArray(value) ? value : []
}

// ---- 解析函数：将 DashboardReportDto 转为各视图强类型数据 ----

// 运营总览解析
export function parseOverviewData(report: DashboardReportDto): OverviewData {
  return {
    kpi: {
      orderCount: getNumberMetric(report, 'orderCount'),
      gmv: getNumberMetric(report, 'gmv'),
      conversionRate: getNumberMetric(report, 'conversionRate'),
      avgOrderAmount: getNumberMetric(report, 'avgOrderAmount'),
    },
    change: {
      orderCountChange: getNumberMetric(report, 'orderCountChange'),
      gmvChange: getNumberMetric(report, 'gmvChange'),
      conversionRateChange: getNumberMetric(report, 'conversionRateChange'),
      avgOrderAmountChange: getNumberMetric(report, 'avgOrderAmountChange'),
    },
    dailyTrend: getArrayMetric<OverviewDailyTrendPoint>(report, 'dailyTrend'),
    sourceDistribution: getArrayMetric<SourceDistributionItem>(report, 'sourceDistribution'),
    funnel: getArrayMetric<FunnelStage>(report, 'funnel'),
  }
}

// 支付统计解析
export function parsePaymentStatsData(report: DashboardReportDto): PaymentStatsData {
  return {
    kpi: {
      totalCount: getNumberMetric(report, 'totalCount'),
      successRate: getNumberMetric(report, 'successRate'),
      avgLatencyMs: getNumberMetric(report, 'avgLatencyMs'),
    },
    channelStats: getArrayMetric<ChannelStat>(report, 'channelStats'),
    failureReasons: getArrayMetric<FailureReason>(report, 'failureReasons'),
  }
}

// 积分统计解析
export function parsePointsStatsData(report: DashboardReportDto): PointsStatsData {
  return {
    kpi: {
      issued: getNumberMetric(report, 'issued'),
      consumed: getNumberMetric(report, 'consumed'),
      net: getNumberMetric(report, 'net'),
    },
    change: {
      issuedChange: getNumberMetric(report, 'issuedChange'),
      consumedChange: getNumberMetric(report, 'consumedChange'),
      netChange: getNumberMetric(report, 'netChange'),
    },
    dailyTrend: getArrayMetric<PointsDailyTrendPoint>(report, 'dailyTrend'),
    sourceDistribution: getArrayMetric<SourceDistributionItem>(report, 'sourceDistribution'),
  }
}

// 通知送达率解析
export function parseNotificationDeliveryData(report: DashboardReportDto): NotificationDeliveryData {
  return {
    channelStats: getArrayMetric<NotificationChannelStat>(report, 'channelStats'),
    failureReasons: getArrayMetric<NotificationFailureReason>(report, 'failureReasons'),
    dailyTrend: getArrayMetric<NotificationDailyTrendPoint>(report, 'dailyTrend'),
  }
}

// 售后统计解析
export function parseAfterSalesStatsData(report: DashboardReportDto): AfterSalesStatsData {
  return {
    kpi: {
      afterSalesCount: getNumberMetric(report, 'afterSalesCount'),
      refundAmount: getNumberMetric(report, 'refundAmount'),
      afterSalesRate: getNumberMetric(report, 'afterSalesRate'),
    },
    change: {
      afterSalesCountChange: getNumberMetric(report, 'afterSalesCountChange'),
      refundAmountChange: getNumberMetric(report, 'refundAmountChange'),
      afterSalesRateChange: getNumberMetric(report, 'afterSalesRateChange'),
    },
    typeDistribution: getArrayMetric<TypeDistributionItem>(report, 'typeDistribution'),
    dailyTrend: getArrayMetric<AfterSalesDailyTrendPoint>(report, 'dailyTrend'),
    topShops: getArrayMetric<TopShopByAfterSales>(report, 'topShopsByAfterSales'),
  }
}

// 店铺排行解析
export function parseShopRankingData(report: DashboardReportDto): ShopRankingData {
  return {
    dimension: getMetric<string>(report, 'dimension') ?? 'salesAmount',
    items: getArrayMetric<ShopRankingItem>(report, 'items'),
  }
}
