/**
 * 01-dashboard 数据看板 DTO
 *
 * 字段与 docs/design-prompts/operations/01-dashboard/*.md「数据与 API」对齐：
 * - dashboard 端点统一返回 DashboardReportDto，业务数据在 Metrics 数组中，Key 为下划线风格
 *   （order_count / gmv / conversion_rate / ...）
 * - notifications/statistics 端点返回 DeliveryStatisticsListDto 渠道明细
 * - 解析函数将原始 snake_case 负载转为视图层 camelCase 模型，并对缺失 Metric 做兜底
 */

/** 报表类型枚举（与后端 ReportType 对齐） */
export type ReportType =
  | 'OrderGmv'
  | 'PaymentSuccessRate'
  | 'PointsIssued'
  | 'NotificationDelivery'
  | 'AfterSalesVolume'
  | 'ShopRanking'

/** 数据粒度枚举 */
export type Granularity = 'Hour' | 'Day' | 'Week' | 'Month'

/** 单个 Metric 项（后端 Metrics 数组元素） */
export interface MetricItemDto {
  Key: string
  Value: unknown
  Unit?: string
}

/** 仪表盘报表 DTO（6 个 dashboard 端点统一返回结构） */
export interface DashboardReportDto {
  ReportId: string
  ReportType: ReportType
  PeriodStart: string
  PeriodEnd: string
  Granularity: Granularity
  GeneratedAt: string
  DataVersion?: number
  Metrics: MetricItemDto[]
}

// ---- 请求参数 ----

/** dashboard 端点时间范围参数（ISO 8601 UTC 字符串） */
export interface DateRangeParams {
  start: string
  end: string
}

/** notifications/statistics 端点时间范围参数（from/to 命名） */
export interface NotificationStatisticsParams {
  from: string
  to: string
}

// ---- 运营总览 ----

/** 原始 GMV 趋势点（gmv_trend 数组元素） */
export interface OverviewTrendPointDto {
  date: string
  gmv: number
  order_count: number
}

/** 原始订单来源分布项（order_source_distribution 数组元素） */
export interface SourceDistributionItemDto {
  source: string
  value: number
}

/** 运营总览 KPI */
export interface OverviewKpi {
  orderCount: number
  gmv: number
  conversionRate: number
  avgOrderAmount: number
}

/** 运营总览 KPI 同比变化（百分点 / 百分比数值） */
export interface OverviewKpiChange {
  orderCountChange: number
  gmvChange: number
  conversionRateChange: number
  avgOrderAmountChange: number
}

/** GMV 趋势点（视图模型） */
export interface OverviewTrendPoint {
  date: string
  gmv: number
  orderCount: number
}

/** 订单来源分布项（视图模型） */
export interface SourceDistributionItem {
  source: string
  value: number
}

/** 运营总览解析数据 */
export interface OverviewData {
  kpi: OverviewKpi
  change: OverviewKpiChange
  dailyTrend: OverviewTrendPoint[]
  sourceDistribution: SourceDistributionItem[]
}

// ---- 支付统计 ----

/** 原始渠道分布项（channel_distribution 数组元素） */
export interface PaymentChannelDto {
  channel: string
  count: number
  amount: number
  success_rate: number
  change: number
}

/** 原始失败原因项（failure_reasons 数组元素） */
export interface PaymentFailureReasonDto {
  channel: string
  reason: string
  count: number
  last_occurred_at: string
}

/** 支付统计 KPI */
export interface PaymentKpi {
  successRate: number
  paymentCount: number
  failedCount: number
}

/** 渠道分布项（视图模型） */
export interface PaymentChannelStat {
  channel: string
  count: number
  amount: number
  successRate: number
  /** 环比（百分比数值，正增负减） */
  change: number
}

/** 失败原因项（视图模型） */
export interface PaymentFailureReason {
  channel: string
  reason: string
  count: number
  lastOccurredAt: string
}

/** 支付统计解析数据 */
export interface PaymentStatsData {
  kpi: PaymentKpi
  channelDistribution: PaymentChannelStat[]
  failureReasons: PaymentFailureReason[]
}

// ---- 积分统计 ----

/** 原始积分趋势点（points_trend 数组元素） */
export interface PointsTrendPointDto {
  date: string
  issued: number
  consumed: number
}

/** 原始发放来源分布项（source_distribution 数组元素） */
export interface PointsSourceItemDto {
  source: string
  value: number
}

/** 积分统计 KPI */
export interface PointsKpi {
  issued: number
  consumed: number
  net: number
}

/** 积分统计 KPI 同比变化 */
export interface PointsKpiChange {
  issuedChange: number
  consumedChange: number
  netChange: number
}

/** 积分趋势点（视图模型） */
export interface PointsTrendPoint {
  date: string
  issued: number
  consumed: number
}

/** 积分统计解析数据 */
export interface PointsStatsData {
  kpi: PointsKpi
  change: PointsKpiChange
  dailyTrend: PointsTrendPoint[]
  sourceDistribution: SourceDistributionItem[]
}

// ---- 通知送达率 ----

/** 原始送达率趋势点（delivery_trend 数组元素） */
export interface NotificationTrendPointDto {
  date: string
  channel: string
  rate: number
}

/** 原始失败原因项（failure_reasons 数组元素） */
export interface NotificationFailureReasonDto {
  channel: string
  reason: string
  count: number
  last_occurred_at: string
}

/** 原始渠道投递明细项（DeliveryStatisticsListDto.items 数组元素） */
export interface DeliveryChannelStatDto {
  channel: string
  total_count: number
  delivered_count: number
  failed_count: number
  delivery_rate: number
  avg_latency_ms: number
}

/** notifications/statistics 端点返回结构 */
export interface DeliveryStatisticsListDto {
  items: DeliveryChannelStatDto[]
}

/** 四渠道送达率（百分比数值） */
export interface NotificationChannelRates {
  sms: number
  email: number
  inapp: number
  push: number
}

/** 送达率趋势点（视图模型） */
export interface NotificationTrendPoint {
  date: string
  channel: string
  rate: number
}

/** 通知失败原因项（视图模型） */
export interface NotificationFailureReason {
  channel: string
  reason: string
  count: number
  lastOccurredAt: string
}

/** 渠道投递明细项（视图模型） */
export interface DeliveryChannelStat {
  channel: string
  totalCount: number
  deliveredCount: number
  failedCount: number
  deliveryRate: number
  avgLatencyMs: number
}

/** 通知送达率解析数据（dashboard 端点） */
export interface NotificationDeliveryData {
  channelRates: NotificationChannelRates
  dailyTrend: NotificationTrendPoint[]
  failureReasons: NotificationFailureReason[]
}

/** 通知投递统计解析数据（statistics 端点） */
export interface NotificationStatisticsData {
  items: DeliveryChannelStat[]
}

// ---- 售后统计 ----

/** 原始售后趋势点（after_sales_trend 数组元素） */
export interface AfterSalesTrendPointDto {
  date: string
  count: number
  refund_amount: number
}

/** 原始售后类型分布项（type_distribution 数组元素） */
export interface TypeDistributionItemDto {
  type: string
  count: number
}

/** 原始售后状态分布项（status_distribution 数组元素） */
export interface StatusDistributionItemDto {
  status: string
  count: number
  avg_process_hours: number
  refund_amount: number
}

/** 售后统计 KPI */
export interface AfterSalesKpi {
  afterSalesCount: number
  refundAmount: number
  afterSalesRate: number
  avgProcessHours: number
}

/** 售后统计 KPI 同比变化 */
export interface AfterSalesKpiChange {
  afterSalesCountChange: number
  refundAmountChange: number
  afterSalesRateChange: number
  avgProcessHoursChange: number
}

/** 售后趋势点（视图模型） */
export interface AfterSalesTrendPoint {
  date: string
  count: number
  refundAmount: number
}

/** 售后类型分布项（视图模型） */
export interface TypeDistributionItem {
  type: string
  count: number
}

/** 售后状态分布项（视图模型） */
export interface StatusDistributionItem {
  status: string
  count: number
  avgProcessHours: number
  refundAmount: number
}

/** 售后统计解析数据 */
export interface AfterSalesStatsData {
  kpi: AfterSalesKpi
  change: AfterSalesKpiChange
  typeDistribution: TypeDistributionItem[]
  statusDistribution: StatusDistributionItem[]
  dailyTrend: AfterSalesTrendPoint[]
}

// ---- 店铺排行 ----

/** 店铺状态枚举（后端返回固定 Top50，状态含义见 StatusTag/店铺治理） */
export type ShopStatus = 'Pending' | 'Active' | 'Suspended' | 'Closed' | 'QualificationExpired'

/** 原始店铺排行项（shop_ranking 数组元素） */
export interface ShopRankingItemDto {
  shop_id: string
  shop_name: string
  seller_account: string
  gmv: number
  order_count: number
  avg_order_value: number
  positive_rate: number
  status: ShopStatus
}

/** 店铺排行项（视图模型） */
export interface ShopRankingItem {
  shopId: string
  shopName: string
  sellerAccount: string
  gmv: number
  orderCount: number
  avgOrderValue: number
  positiveRate: number
  status: ShopStatus
}

/** 店铺排行解析数据 */
export interface ShopRankingData {
  items: ShopRankingItem[]
}

// ---- Metric 提取工具函数 ----

/** 从报表中按 Key 提取 Metric 值，未找到返回 undefined */
export function getMetric<T = unknown>(report: DashboardReportDto, key: string): T | undefined {
  const metric = report.Metrics.find((m) => m.Key === key)
  return metric ? (metric.Value as T) : undefined
}

/** 从报表中提取数值型 Metric，未找到或非数值返回 0 */
export function getNumberMetric(report: DashboardReportDto, key: string): number {
  const value = getMetric<number>(report, key)
  return typeof value === 'number' ? value : 0
}

/** 从报表中提取数组型 Metric，未找到或非数组返回空数组 */
export function getArrayMetric<T>(report: DashboardReportDto, key: string): T[] {
  const value = getMetric<T[]>(report, key)
  return Array.isArray(value) ? value : []
}

// ---- 解析函数：将 DashboardReportDto 转为各视图强类型数据 ----

/** 运营总览解析（Metrics: order_count/gmv/conversion_rate/avg_order_value/gmv_trend/order_source_distribution） */
export function parseOverviewData(report: DashboardReportDto): OverviewData {
  return {
    kpi: {
      orderCount: getNumberMetric(report, 'order_count'),
      gmv: getNumberMetric(report, 'gmv'),
      conversionRate: getNumberMetric(report, 'conversion_rate'),
      avgOrderAmount: getNumberMetric(report, 'avg_order_value'),
    },
    change: {
      orderCountChange: getNumberMetric(report, 'order_count_change'),
      gmvChange: getNumberMetric(report, 'gmv_change'),
      conversionRateChange: getNumberMetric(report, 'conversion_rate_change'),
      avgOrderAmountChange: getNumberMetric(report, 'avg_order_value_change'),
    },
    dailyTrend: getArrayMetric<OverviewTrendPointDto>(report, 'gmv_trend').map((p) => ({
      date: p.date,
      gmv: p.gmv,
      orderCount: p.order_count,
    })),
    sourceDistribution: getArrayMetric<SourceDistributionItemDto>(report, 'order_source_distribution').map(
      (item) => ({ source: item.source, value: item.value }),
    ),
  }
}

/** 支付统计解析（Metrics: success_rate/payment_count/failed_count/channel_distribution/failure_reasons） */
export function parsePaymentStatsData(report: DashboardReportDto): PaymentStatsData {
  return {
    kpi: {
      successRate: getNumberMetric(report, 'success_rate'),
      paymentCount: getNumberMetric(report, 'payment_count'),
      failedCount: getNumberMetric(report, 'failed_count'),
    },
    channelDistribution: getArrayMetric<PaymentChannelDto>(report, 'channel_distribution').map((c) => ({
      channel: c.channel,
      count: c.count,
      amount: c.amount,
      successRate: c.success_rate,
      change: c.change,
    })),
    failureReasons: getArrayMetric<PaymentFailureReasonDto>(report, 'failure_reasons').map((r) => ({
      channel: r.channel,
      reason: r.reason,
      count: r.count,
      lastOccurredAt: r.last_occurred_at,
    })),
  }
}

/** 积分统计解析（Metrics: points_issued/points_consumed/points_net/points_trend/source_distribution） */
export function parsePointsStatsData(report: DashboardReportDto): PointsStatsData {
  return {
    kpi: {
      issued: getNumberMetric(report, 'points_issued'),
      consumed: getNumberMetric(report, 'points_consumed'),
      net: getNumberMetric(report, 'points_net'),
    },
    change: {
      issuedChange: getNumberMetric(report, 'issued_change'),
      consumedChange: getNumberMetric(report, 'consumed_change'),
      netChange: getNumberMetric(report, 'net_change'),
    },
    dailyTrend: getArrayMetric<PointsTrendPointDto>(report, 'points_trend').map((p) => ({
      date: p.date,
      issued: p.issued,
      consumed: p.consumed,
    })),
    sourceDistribution: getArrayMetric<PointsSourceItemDto>(report, 'source_distribution').map((item) => ({
      source: item.source,
      value: item.value,
    })),
  }
}

/** 通知送达率解析（Metrics: sms_rate/email_rate/inapp_rate/push_rate/delivery_trend/failure_reasons） */
export function parseNotificationDeliveryData(report: DashboardReportDto): NotificationDeliveryData {
  return {
    channelRates: {
      sms: getNumberMetric(report, 'sms_rate'),
      email: getNumberMetric(report, 'email_rate'),
      inapp: getNumberMetric(report, 'inapp_rate'),
      push: getNumberMetric(report, 'push_rate'),
    },
    dailyTrend: getArrayMetric<NotificationTrendPointDto>(report, 'delivery_trend').map((p) => ({
      date: p.date,
      channel: p.channel,
      rate: p.rate,
    })),
    failureReasons: getArrayMetric<NotificationFailureReasonDto>(report, 'failure_reasons').map((r) => ({
      channel: r.channel,
      reason: r.reason,
      count: r.count,
      lastOccurredAt: r.last_occurred_at,
    })),
  }
}

/** 通知投递统计解析（statistics 端点，渠道明细） */
export function parseNotificationStatisticsData(dto: DeliveryStatisticsListDto): NotificationStatisticsData {
  return {
    items: (Array.isArray(dto.items) ? dto.items : []).map((item) => ({
      channel: item.channel,
      totalCount: item.total_count,
      deliveredCount: item.delivered_count,
      failedCount: item.failed_count,
      deliveryRate: item.delivery_rate,
      avgLatencyMs: item.avg_latency_ms,
    })),
  }
}

/** 售后统计解析（Metrics: after_sales_count/refund_amount/after_sales_rate/avg_process_hours/after_sales_trend/type_distribution/status_distribution） */
export function parseAfterSalesStatsData(report: DashboardReportDto): AfterSalesStatsData {
  return {
    kpi: {
      afterSalesCount: getNumberMetric(report, 'after_sales_count'),
      refundAmount: getNumberMetric(report, 'refund_amount'),
      afterSalesRate: getNumberMetric(report, 'after_sales_rate'),
      avgProcessHours: getNumberMetric(report, 'avg_process_hours'),
    },
    change: {
      afterSalesCountChange: getNumberMetric(report, 'after_sales_count_change'),
      refundAmountChange: getNumberMetric(report, 'refund_amount_change'),
      afterSalesRateChange: getNumberMetric(report, 'after_sales_rate_change'),
      avgProcessHoursChange: getNumberMetric(report, 'avg_process_hours_change'),
    },
    typeDistribution: getArrayMetric<TypeDistributionItemDto>(report, 'type_distribution').map((t) => ({
      type: t.type,
      count: t.count,
    })),
    statusDistribution: getArrayMetric<StatusDistributionItemDto>(report, 'status_distribution').map((s) => ({
      status: s.status,
      count: s.count,
      avgProcessHours: s.avg_process_hours,
      refundAmount: s.refund_amount,
    })),
    dailyTrend: getArrayMetric<AfterSalesTrendPointDto>(report, 'after_sales_trend').map((p) => ({
      date: p.date,
      count: p.count,
      refundAmount: p.refund_amount,
    })),
  }
}

/** 店铺排行解析（Metrics: shop_ranking，后端返回固定 Top50） */
export function parseShopRankingData(report: DashboardReportDto): ShopRankingData {
  return {
    items: getArrayMetric<ShopRankingItemDto>(report, 'shop_ranking').map((item) => ({
      shopId: item.shop_id,
      shopName: item.shop_name,
      sellerAccount: item.seller_account,
      gmv: item.gmv,
      orderCount: item.order_count,
      avgOrderValue: item.avg_order_value,
      positiveRate: item.positive_rate,
      status: item.status,
    })),
  }
}
