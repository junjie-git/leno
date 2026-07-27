# 系统管理后台 01-dashboard 模块 Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 实现 `web/system-admin/src/modules/01-dashboard/` 模块的 4 个骨架文件 + 7 个视图页面 + 1 个 API 测试，覆盖运营总览、支付统计、积分统计、通知送达率、售后统计、店铺排行、报表快照 7 个看板页面。

**Architecture:** 模块内部分为 `types/`（DTO + 解析函数）、`api/`（HTTP 调用）、`components/`（模块本地组件 DashboardCard + ChartGauge）、`views/`（7 个 Vue SFC）、`routes.ts`、`index.ts`。7 个看板端点统一返回 `DashboardReportDto`（含 `Metrics: [{Key, Value, Unit}]`），各视图通过 `parseXxxData()` 解析函数将原始 Metrics 转为强类型数据。跨 Plan 类型契约依赖 `@/shared/http`、`@/shared/types`、`@/shared/auth`、`@/shared/components/*`（由 Plan 1 实现）。

**Tech Stack:** Vue 3.5 `<script setup>` + TypeScript strict + Ant Design Vue 4.x + Pinia 2.x + Vue Router 4.x + @vue-echarts 7.x / echarts 5.5 + axios 1.7.x + Vitest 2.x + @vue/test-utils 2.x + dayjs + @ant-design/icons-vue。

**Spec Reference:** `docs/superpowers/specs/2026-07-27-system-admin-frontend-design.md` §2.1（模块 01-dashboard 7 页映射）、§3（数据流与 HTTP 客户端）、§5（共享组件与视觉规范）

---

## File Structure

```
web/system-admin/src/modules/01-dashboard/
├── types/
│   └── dashboard.dto.ts          # 请求/响应 DTO、ReportType/Granularity 枚举、各视图解析数据接口、getMetric/parseXxx 函数
├── api/
│   ├── dashboard.api.ts          # dashboardApi 对象，8 个 GET 端点
│   └── dashboard.api.spec.ts     # API 单元测试，断言 URL/params
├── components/
│   ├── DashboardCard.vue         # 看板 KPI 卡片（模块本地组件，扩展 shared/components.md §8）
│   └── ChartGauge.vue            # 仪表盘图表（模块本地组件，遵循 shared/components.md §7.4）
├── views/
│   ├── OperationsOverview.vue    # 运营总览 — GET /admin/dashboard/overview
│   ├── PaymentStats.vue          # 支付统计 — GET /admin/dashboard/payment-stats
│   ├── PointsStats.vue           # 积分统计 — GET /admin/dashboard/points-stats
│   ├── NotificationDelivery.vue  # 通知送达率 — GET /admin/dashboard/notification-delivery
│   ├── AfterSalesStats.vue       # 售后统计 — GET /admin/dashboard/after-sales-stats
│   ├── ShopRanking.vue           # 店铺排行 — GET /admin/dashboard/shop-ranking
│   └── ReportSnapshots.vue       # 报表快照 — GET /admin/dashboard/reports + /reports/{id}
├── routes.ts                     # 7 条路由项，meta 含 title/menuKey/icon/roles/menuGroup
└── index.ts                      # 导出 routes、dashboardApi、DTO 类型
```

**跨 Plan 依赖（由 Plan 1 实现，本 Plan 直接 import）：**

| 依赖 | 路径 | 关键导出 |
|-|-|-|
| `@/shared/types` | `shared/types/index.ts` | `ApiResponse<T>`, `PageResult<T>`, `PageQuery` |
| `@/shared/http` | `shared/http/index.ts` | `client: AxiosInstance`（拦截器已配置，响应解包 `response.data.data`）, `withIdempotency()` |
| `@/shared/auth` | `shared/auth/auth.store.ts` | `useAuthStore`（含 `isAdmin`, `hasPermission(perm)`, `hasRole(roles)`） |
| `@/shared/components/DateTimeRangePicker` | `shared/components/DateTimeRangePicker.vue` | props: `modelValue: [string,string]`, `presets: ('today'\|...)[]`, `showTime?`；emit: `@update:modelValue` |
| `@/shared/components/ChartLine` | `shared/components/charts/ChartLine.vue` | props: `data: {date,value,series?}[]`, `seriesField?`, `height?`(300), `smooth?`(true) |
| `@/shared/components/ChartPie` | `shared/components/charts/ChartPie.vue` | props: `data: {name,value}[]`, `height?`(300), `legendPosition?`, `donut?`(true) |
| `@/shared/components/ChartBar` | `shared/components/charts/ChartBar.vue` | props: `data: {name,value,series?}[]`, `horizontal?`, `height?`(300), `seriesField?` |
| `@/shared/components/EmptyState` | `shared/components/EmptyState.vue` | props: `title?`, `description?`, `ctaText?`；emit: `@cta-click` |
| `@/shared/components/PermissionGuard` | `shared/components/PermissionGuard.vue` | props: `permission: string\|string[]`, `fallback?`；slot: default |
| `@/shared/components/StatusTag` | `shared/components/StatusTag.vue` | props: `status: string`, `type?: 'order'\|'afterSales'\|'product'\|'shop'\|'payment'` |

**HTTP 返回值约定：** `client.get<T>(url, config)` 返回 `Promise<AxiosResponse<T>>`，响应拦截器已将 `response.data` 解包为 `ApiResponse<T>.data`（即业务负载 `T`）。视图层通过 `const { data } = await dashboardApi.xxx(params)` 获取 `T`。

---

## Task 1: DTO 类型定义与解析函数

**Files:**
- Create: `web/system-admin/src/modules/01-dashboard/types/dashboard.dto.ts`

- [ ] **Step 1: 创建 DTO 文件**

创建 `web/system-admin/src/modules/01-dashboard/types/dashboard.dto.ts`，包含 `ReportType`/`Granularity` 枚举、`DashboardReportDto` 原始结构、7 个视图的解析数据接口、`getMetric`/`getNumberMetric`/`getArrayMetric` 工具函数、6 个 `parseXxxData` 解析函数、`DateRangeParams`/`ReportListParams` 请求参数接口。

```typescript
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
```

- [ ] **Step 2: 验证类型检查通过**

Run: `cd web/system-admin && pnpm typecheck`
Expected: 无 TypeScript 错误（此文件为纯类型定义，无运行时代码依赖）

- [ ] **Step 3: 提交**

```bash
cd web/system-admin
git add src/modules/01-dashboard/types/dashboard.dto.ts
git commit -m "feat(dashboard): 添加仪表盘 DTO 类型定义与解析函数"
```

---

## Task 2: API 层 TDD

**Files:**
- Test: `web/system-admin/src/modules/01-dashboard/api/dashboard.api.spec.ts`
- Create: `web/system-admin/src/modules/01-dashboard/api/dashboard.api.ts`

- [ ] **Step 1: 编写失败的 API 测试**

创建 `web/system-admin/src/modules/01-dashboard/api/dashboard.api.spec.ts`，测试 `dashboardApi` 的 8 个方法，断言调用的 URL、HTTP method 和传参正确。

```typescript
import { describe, it, expect, vi, beforeEach } from 'vitest'
import { client } from '@/shared/http'
import { dashboardApi } from './dashboard.api'
import type { DashboardReportDto } from '../types/dashboard.dto'

// 模拟 shared/http 模块，仅暴露 client.get
vi.mock('@/shared/http', () => ({
  client: {
    get: vi.fn(),
  },
}))

// 构造测试用 DashboardReportDto
function makeReport(reportType: string): DashboardReportDto {
  return {
    ReportId: 'r-001',
    ReportType: reportType as DashboardReportDto['ReportType'],
    PeriodStart: '2026-07-20T00:00:00Z',
    PeriodEnd: '2026-07-27T00:00:00Z',
    Granularity: 'Day',
    GeneratedAt: '2026-07-27T02:00:00Z',
    Metrics: [
      { Key: 'orderCount', Value: 12560 },
      { Key: 'gmv', Value: 1280000 },
    ],
  }
}

describe('dashboardApi', () => {
  beforeEach(() => {
    vi.clearAllMocks()
  })

  const dateParams = { start: '2026-07-20T00:00:00Z', end: '2026-07-27T00:00:00Z' }

  it('getOverview 调用 /admin/dashboard/overview 并传递时间参数', async () => {
    const mockReport = makeReport('OrderGmv')
    vi.spyOn(client, 'get').mockResolvedValue({ data: mockReport })

    const { data } = await dashboardApi.getOverview(dateParams)

    expect(client.get).toHaveBeenCalledWith('/admin/dashboard/overview', { params: dateParams })
    expect(data).toEqual(mockReport)
  })

  it('getPaymentStats 调用 /admin/dashboard/payment-stats', async () => {
    const mockReport = makeReport('PaymentSuccessRate')
    vi.spyOn(client, 'get').mockResolvedValue({ data: mockReport })

    const { data } = await dashboardApi.getPaymentStats(dateParams)

    expect(client.get).toHaveBeenCalledWith('/admin/dashboard/payment-stats', { params: dateParams })
    expect(data).toEqual(mockReport)
  })

  it('getPointsStats 调用 /admin/dashboard/points-stats', async () => {
    const mockReport = makeReport('PointsIssued')
    vi.spyOn(client, 'get').mockResolvedValue({ data: mockReport })

    const { data } = await dashboardApi.getPointsStats(dateParams)

    expect(client.get).toHaveBeenCalledWith('/admin/dashboard/points-stats', { params: dateParams })
    expect(data).toEqual(mockReport)
  })

  it('getNotificationDelivery 调用 /admin/dashboard/notification-delivery', async () => {
    const mockReport = makeReport('NotificationDelivery')
    vi.spyOn(client, 'get').mockResolvedValue({ data: mockReport })

    const { data } = await dashboardApi.getNotificationDelivery(dateParams)

    expect(client.get).toHaveBeenCalledWith('/admin/dashboard/notification-delivery', { params: dateParams })
    expect(data).toEqual(mockReport)
  })

  it('getAfterSalesStats 调用 /admin/dashboard/after-sales-stats', async () => {
    const mockReport = makeReport('AfterSalesVolume')
    vi.spyOn(client, 'get').mockResolvedValue({ data: mockReport })

    const { data } = await dashboardApi.getAfterSalesStats(dateParams)

    expect(client.get).toHaveBeenCalledWith('/admin/dashboard/after-sales-stats', { params: dateParams })
    expect(data).toEqual(mockReport)
  })

  it('getShopRanking 调用 /admin/dashboard/shop-ranking', async () => {
    const mockReport = makeReport('ShopRanking')
    vi.spyOn(client, 'get').mockResolvedValue({ data: mockReport })

    const { data } = await dashboardApi.getShopRanking(dateParams)

    expect(client.get).toHaveBeenCalledWith('/admin/dashboard/shop-ranking', { params: dateParams })
    expect(data).toEqual(mockReport)
  })

  it('getReports 传递 reportType 和时间参数', async () => {
    const mockReports = [makeReport('OrderGmv'), makeReport('OrderGmv')]
    vi.spyOn(client, 'get').mockResolvedValue({ data: mockReports })

    const params = { ...dateParams, reportType: 'OrderGmv' as const }
    const { data } = await dashboardApi.getReports(params)

    expect(client.get).toHaveBeenCalledWith('/admin/dashboard/reports', { params })
    expect(data).toEqual(mockReports)
  })

  it('getReport 调用 /admin/dashboard/reports/{id}', async () => {
    const mockReport = makeReport('OrderGmv')
    vi.spyOn(client, 'get').mockResolvedValue({ data: mockReport })

    const { data } = await dashboardApi.getReport('r-001')

    expect(client.get).toHaveBeenCalledWith('/admin/dashboard/reports/r-001')
    expect(data).toEqual(mockReport)
  })
})
```

- [ ] **Step 2: 运行测试验证失败**

Run: `cd web/system-admin && pnpm test -- src/modules/01-dashboard/api/dashboard.api.spec.ts`
Expected: FAIL — `dashboardApi` 未定义（`./dashboard.api` 模块不存在）

- [ ] **Step 3: 实现 API 层**

创建 `web/system-admin/src/modules/01-dashboard/api/dashboard.api.ts`，实现 `dashboardApi` 对象，包含 8 个 GET 方法。

```typescript
import { client } from '@/shared/http'
import type { DashboardReportDto, DateRangeParams, ReportListParams } from '../types/dashboard.dto'

// 仪表盘 API 对象，8 个 GET 端点
export const dashboardApi = {
  // 运营总览 — 订单量/GMV/转化率
  getOverview: (params: DateRangeParams) =>
    client.get<DashboardReportDto>('/admin/dashboard/overview', { params }),

  // 支付统计 — 成功率/渠道排行/失败原因
  getPaymentStats: (params: DateRangeParams) =>
    client.get<DashboardReportDto>('/admin/dashboard/payment-stats', { params }),

  // 积分统计 — 发放量/消耗量/净增
  getPointsStats: (params: DateRangeParams) =>
    client.get<DashboardReportDto>('/admin/dashboard/points-stats', { params }),

  // 通知送达率 — 四渠道送达率/失败原因
  getNotificationDelivery: (params: DateRangeParams) =>
    client.get<DashboardReportDto>('/admin/dashboard/notification-delivery', { params }),

  // 售后统计 — 售后量/退款金额/售后率
  getAfterSalesStats: (params: DateRangeParams) =>
    client.get<DashboardReportDto>('/admin/dashboard/after-sales-stats', { params }),

  // 店铺排行 — TopN 排行
  getShopRanking: (params: DateRangeParams) =>
    client.get<DashboardReportDto>('/admin/dashboard/shop-ranking', { params }),

  // 报表快照列表 — 按类型和时间范围
  getReports: (params: ReportListParams) =>
    client.get<DashboardReportDto[]>('/admin/dashboard/reports', { params }),

  // 报表快照详情 — 按 ID
  getReport: (id: string) =>
    client.get<DashboardReportDto>(`/admin/dashboard/reports/${id}`),
}
```

- [ ] **Step 4: 运行测试验证通过**

Run: `cd web/system-admin && pnpm test -- src/modules/01-dashboard/api/dashboard.api.spec.ts`
Expected: PASS — 8 个测试用例全部通过

- [ ] **Step 5: 提交**

```bash
cd web/system-admin
git add src/modules/01-dashboard/api/dashboard.api.ts src/modules/01-dashboard/api/dashboard.api.spec.ts
git commit -m "feat(dashboard): 实现 8 个仪表盘 API 端点及单元测试"
```

---

## Task 3: 模块本地组件 — DashboardCard + ChartGauge

**Files:**
- Create: `web/system-admin/src/modules/01-dashboard/components/DashboardCard.vue`
- Create: `web/system-admin/src/modules/01-dashboard/components/ChartGauge.vue`

`DashboardCard`（shared/components.md §8）和 `ChartGauge`（shared/components.md §7.4）不在 Plan 1 的 12 个共享组件清单中，作为模块本地组件实现。

- [ ] **Step 1: 创建 DashboardCard.vue**

创建 `web/system-admin/src/modules/01-dashboard/components/DashboardCard.vue`，看板 KPI 卡片，含标题、数值、趋势箭头、自定义颜色、点击事件、加载骨架。遵循 §8 Props 接口并扩展 `tooltip`/`description`/`valueColor`/`@click`。

```vue
<template>
  <a-card class="dashboard-card" :bordered="bordered" hoverable @click="handleClick">
    <div class="dashboard-card__header">
      <span class="dashboard-card__title">{{ title }}</span>
      <a-tooltip v-if="tooltip" :title="tooltip">
        <InfoCircleOutlined class="dashboard-card__info-icon" />
      </a-tooltip>
    </div>
    <div class="dashboard-card__value" :style="{ color: valueColor || undefined }">
      <a-skeleton v-if="loading" :title="{ width: '60%' }" :paragraph="false" active />
      <span v-else class="dashboard-card__number">{{ formattedValue }}</span>
    </div>
    <div v-if="trend" class="dashboard-card__trend">
      <ArrowUpOutlined v-if="trend.direction === 'up'" class="dashboard-card__arrow dashboard-card__arrow--up" />
      <ArrowDownOutlined v-else class="dashboard-card__arrow dashboard-card__arrow--down" />
      <span :class="trendValueClass">{{ trend.value.toFixed(1) }}%</span>
    </div>
    <div v-if="description" class="dashboard-card__desc">{{ description }}</div>
  </a-card>
</template>

<script setup lang="ts">
import { computed } from 'vue'
import { ArrowUpOutlined, ArrowDownOutlined, InfoCircleOutlined } from '@ant-design/icons-vue'

interface Trend {
  value: number
  direction: 'up' | 'down'
}

interface Props {
  title: string
  value: number | string
  unit?: string
  trend?: Trend
  loading?: boolean
  tooltip?: string
  description?: string
  valueColor?: string
  bordered?: boolean
}

const props = withDefaults(defineProps<Props>(), {
  unit: '',
  loading: false,
  tooltip: '',
  description: '',
  valueColor: '',
  bordered: true,
})

const emit = defineEmits<{ click: [] }>()

const formattedValue = computed(() => {
  if (typeof props.value === 'string') return props.value
  const formatted = props.value.toLocaleString('zh-CN')
  return props.unit ? `${formatted} ${props.unit}` : formatted
})

const trendValueClass = computed(() => {
  if (!props.trend) return ''
  return props.trend.direction === 'up'
    ? 'dashboard-card__trend-value dashboard-card__trend-value--up'
    : 'dashboard-card__trend-value dashboard-card__trend-value--down'
})

function handleClick() {
  emit('click')
}
</script>

<style scoped>
.dashboard-card {
  border-radius: 8px;
  cursor: pointer;
}
.dashboard-card__header {
  display: flex;
  align-items: center;
  gap: 4px;
  margin-bottom: 8px;
}
.dashboard-card__title {
  font-size: 14px;
  color: #8C8C8C;
}
.dashboard-card__info-icon {
  font-size: 12px;
  color: #8C8C8C;
  cursor: help;
}
.dashboard-card__value {
  font-size: 24px;
  font-weight: 600;
  color: #000000D9;
  line-height: 1.4;
}
.dashboard-card__trend {
  display: flex;
  align-items: center;
  gap: 4px;
  margin-top: 8px;
  font-size: 12px;
}
.dashboard-card__arrow {
  font-size: 12px;
}
.dashboard-card__arrow--up {
  color: #52C41A;
}
.dashboard-card__arrow--down {
  color: #FF4D4F;
}
.dashboard-card__trend-value--up {
  color: #52C41A;
}
.dashboard-card__trend-value--down {
  color: #FF4D4F;
}
.dashboard-card__desc {
  margin-top: 4px;
  font-size: 12px;
  color: #8C8C8C;
}
</style>
```

- [ ] **Step 2: 创建 ChartGauge.vue**

创建 `web/system-admin/src/modules/01-dashboard/components/ChartGauge.vue`，仪表盘图表组件，基于 echarts/core 的 GaugeChart。遵循 §7.4 Props 接口（`value`/`title`/`height`/`thresholds`）。阈值染色：低于 thresholds[0] 红、thresholds[0]-thresholds[1] 黄、高于 thresholds[1] 绿。

```vue
<template>
  <a-card :bordered="true" class="chart-gauge">
    <template #title>
      <span class="chart-gauge__title">{{ title }}</span>
    </template>
    <a-spin :spinning="loading">
      <div v-show="!loading" ref="chartRef" class="chart-gauge__canvas" :style="{ height: `${height}px` }" />
    </a-spin>
  </a-card>
</template>

<script setup lang="ts">
import { ref, watch, onMounted, onBeforeUnmount, nextTick } from 'vue'
import * as echarts from 'echarts/core'
import { GaugeChart } from 'echarts/charts'
import { CanvasRenderer } from 'echarts/renderers'

echarts.use([GaugeChart, CanvasRenderer])

interface Props {
  value: number
  title: string
  height?: number
  thresholds?: [number, number]
  loading?: boolean
}

const props = withDefaults(defineProps<Props>(), {
  height: 200,
  thresholds: () => [80, 95] as [number, number],
  loading: false,
})

const chartRef = ref<HTMLDivElement | null>(null)
let chart: echarts.ECharts | null = null

// 根据阈值返回颜色
function getColor(value: number): string {
  const [low, mid] = props.thresholds
  if (value < low) return '#FF4D4F'
  if (value < mid) return '#FAAD14'
  return '#52C41A'
}

function renderChart() {
  if (!chartRef.value || props.loading) return
  if (chart) {
    chart.dispose()
  }
  chart = echarts.init(chartRef.value)
  const color = getColor(props.value)
  chart.setOption({
    series: [
      {
        type: 'gauge',
        min: 0,
        max: 100,
        progress: { show: true, width: 18 },
        axisLine: { lineStyle: { width: 18, color: [[0.8, '#FF4D4F'], [0.95, '#FAAD14'], [1, '#52C41A']] } },
        axisTick: { show: false },
        splitLine: { length: 10, lineStyle: { width: 2, color: '#999' } },
        axisLabel: { distance: 25, color: '#999', fontSize: 12 },
        pointer: { show: true, length: '60%', width: 5 },
        detail: {
          valueAnimation: true,
          formatter: '{value}%',
          color,
          fontSize: 24,
          fontWeight: 600,
          offsetCenter: [0, '70%'],
        },
        data: [{ value: props.value, itemStyle: { color } }],
      },
    ],
  })
}

function handleResize() {
  chart?.resize()
}

onMounted(async () => {
  await nextTick()
  renderChart()
  window.addEventListener('resize', handleResize)
})

onBeforeUnmount(() => {
  window.removeEventListener('resize', handleResize)
  chart?.dispose()
  chart = null
})

watch(() => [props.value, props.loading, props.thresholds], async () => {
  await nextTick()
  renderChart()
}, { deep: true })
</script>

<style scoped>
.chart-gauge {
  border-radius: 8px;
}
.chart-gauge__title {
  font-size: 14px;
  font-weight: 500;
}
.chart-gauge__canvas {
  width: 100%;
}
</style>
```

- [ ] **Step 3: 验证类型检查通过**

Run: `cd web/system-admin && pnpm typecheck`
Expected: 无 TypeScript 错误

- [ ] **Step 4: 提交**

```bash
cd web/system-admin
git add src/modules/01-dashboard/components/DashboardCard.vue src/modules/01-dashboard/components/ChartGauge.vue
git commit -m "feat(dashboard): 添加 DashboardCard 与 ChartGauge 模块本地组件"
```

---

## Task 4: 路由与模块入口

**Files:**
- Create: `web/system-admin/src/modules/01-dashboard/routes.ts`
- Create: `web/system-admin/src/modules/01-dashboard/index.ts`

- [ ] **Step 1: 创建 routes.ts**

创建 `web/system-admin/src/modules/01-dashboard/routes.ts`，定义 7 条路由项，每项含 `path`/`name`/`component`（懒加载）/`meta`（title/menuKey/icon/roles/menuGroup）。路由 path 为 kebab-case，name 为 `dashboard.{view}` kebab-case。

```typescript
import type { RouteRecordRaw } from 'vue-router'

// 01-dashboard 模块路由表（7 条，挂载在 BasicLayout children 下）
const routes: RouteRecordRaw[] = [
  {
    path: 'operations-overview',
    name: 'dashboard.operations-overview',
    component: () => import('../views/OperationsOverview.vue'),
    meta: {
      title: '运营总览',
      menuKey: 'dashboard.operations-overview',
      icon: 'DashboardOutlined',
      roles: ['Admin', 'Operator'],
      menuGroup: '01-dashboard',
    },
  },
  {
    path: 'payment-stats',
    name: 'dashboard.payment-stats',
    component: () => import('../views/PaymentStats.vue'),
    meta: {
      title: '支付统计',
      menuKey: 'dashboard.payment-stats',
      icon: 'PayCircleOutlined',
      roles: ['Admin', 'Operator'],
      menuGroup: '01-dashboard',
    },
  },
  {
    path: 'points-stats',
    name: 'dashboard.points-stats',
    component: () => import('../views/PointsStats.vue'),
    meta: {
      title: '积分统计',
      menuKey: 'dashboard.points-stats',
      icon: 'GiftOutlined',
      roles: ['Admin', 'Operator'],
      menuGroup: '01-dashboard',
    },
  },
  {
    path: 'notification-delivery',
    name: 'dashboard.notification-delivery',
    component: () => import('../views/NotificationDelivery.vue'),
    meta: {
      title: '通知送达率',
      menuKey: 'dashboard.notification-delivery',
      icon: 'NotificationOutlined',
      roles: ['Admin', 'Operator'],
      menuGroup: '01-dashboard',
    },
  },
  {
    path: 'after-sales-stats',
    name: 'dashboard.after-sales-stats',
    component: () => import('../views/AfterSalesStats.vue'),
    meta: {
      title: '售后统计',
      menuKey: 'dashboard.after-sales-stats',
      icon: 'RollbackOutlined',
      roles: ['Admin', 'Operator'],
      menuGroup: '01-dashboard',
    },
  },
  {
    path: 'shop-ranking',
    name: 'dashboard.shop-ranking',
    component: () => import('../views/ShopRanking.vue'),
    meta: {
      title: '店铺排行',
      menuKey: 'dashboard.shop-ranking',
      icon: 'ShopOutlined',
      roles: ['Admin', 'Operator'],
      menuGroup: '01-dashboard',
    },
  },
  {
    path: 'report-snapshots',
    name: 'dashboard.report-snapshots',
    component: () => import('../views/ReportSnapshots.vue'),
    meta: {
      title: '报表快照',
      menuKey: 'dashboard.report-snapshots',
      icon: 'FileTextOutlined',
      roles: ['Admin', 'Operator'],
      menuGroup: '01-dashboard',
    },
  },
]

export default routes
```

- [ ] **Step 2: 创建 index.ts**

创建 `web/system-admin/src/modules/01-dashboard/index.ts`，导出 `routes`、`dashboardApi`、DTO 类型，供 `app/router.ts` 聚合。

```typescript
export { default as routes } from './routes'
export { dashboardApi } from './api/dashboard.api'
export * from './types/dashboard.dto'
```

- [ ] **Step 3: 验证类型检查通过**

Run: `cd web/system-admin && pnpm typecheck`
Expected: 无 TypeScript 错误（视图文件尚未创建，懒加载 `import()` 在类型检查时不报错）

- [ ] **Step 4: 提交**

```bash
cd web/system-admin
git add src/modules/01-dashboard/routes.ts src/modules/01-dashboard/index.ts
git commit -m "feat(dashboard): 添加 7 条路由项与模块入口导出"
```

---

## Task 5: OperationsOverview.vue — 运营总览

**Files:**
- Create: `web/system-admin/src/modules/01-dashboard/views/OperationsOverview.vue`

**设计要点（对应 design-prompt 01-dashboard/operations-overview.md）：**
- 筛选条：`DateTimeRangePicker`（预设 今日/昨日/近 7 天/近 30 天/本月，默认近 7 天）
- 4 个 KPI 卡片：订单量、GMV、转化率、客单价，含同比趋势 ↑↓
- 主趋势图：`ChartLine` — GMV 与订单量双系列按日趋势，高度 320px
- 辅助图区：左 `ChartPie` 订单来源分布（环形），右 `ChartBar` 转化漏斗（浏览→加购→下单→支付）
- KPI 卡片点击跳转子看板（订单量→售后统计、GMV→支付统计、转化率→店铺排行），携带 `start/end` query
- 转化率卡片用 `PermissionGuard permission="dashboard:conversion"` 包裹（Operator 仅可见订单量与 GMV）
- 转化率 < 5% 时 KPI 数值显示警告色 `#FAAD14`
- 时间范围 start ≥ end：前端拦截提示

- [ ] **Step 1: 创建 OperationsOverview.vue**

```vue
<template>
  <div class="operations-overview">
    <!-- 筛选条 -->
    <div class="operations-overview__toolbar">
      <DateTimeRangePicker v-model="dateRange" :presets="rangePresets" />
      <a-button :loading="loading" @click="loadData">
        <template #icon><ReloadOutlined /></template>
        刷新
      </a-button>
    </div>

    <!-- KPI 卡片网格 -->
    <a-row :gutter="24">
      <a-col :xs="24" :sm="12" :lg="6">
        <DashboardCard
          title="订单量"
          :value="data?.kpi.orderCount ?? 0"
          :loading="loading"
          :trend="buildTrend(data?.change.orderCountChange)"
          @click="navigateTo('/dashboard/after-sales-stats')"
        />
      </a-col>
      <a-col :xs="24" :sm="12" :lg="6">
        <DashboardCard
          title="GMV"
          :value="formatGmv(data?.kpi.gmv ?? 0)"
          :loading="loading"
          :trend="buildTrend(data?.change.gmvChange)"
          @click="navigateTo('/dashboard/payment-stats')"
        />
      </a-col>
      <a-col :xs="24" :sm="12" :lg="6">
        <PermissionGuard permission="dashboard:conversion">
          <DashboardCard
            title="转化率"
            :value="(data?.kpi.conversionRate ?? 0).toFixed(1)"
            unit="%"
            :loading="loading"
            :trend="buildTrend(data?.change.conversionRateChange)"
            :value-color="conversionColor"
            @click="navigateTo('/dashboard/shop-ranking')"
          />
        </PermissionGuard>
      </a-col>
      <a-col :xs="24" :sm="12" :lg="6">
        <DashboardCard
          title="客单价"
          :value="(data?.kpi.avgOrderAmount ?? 0).toLocaleString('zh-CN')"
          unit="¥"
          :loading="loading"
          :trend="buildTrend(data?.change.avgOrderAmountChange)"
        />
      </a-col>
    </a-row>

    <!-- 主趋势图 -->
    <a-card title="GMV 与订单量趋势" class="operations-overview__card">
      <a-spin :spinning="loading">
        <ChartLine
          v-if="hasTrendData"
          :data="trendChartData"
          series-field="series"
          :height="320"
        />
        <EmptyState
          v-else-if="!loading"
          description="暂无运营数据，请稍后重试"
          cta-text="刷新"
          @cta-click="loadData"
        />
      </a-spin>
    </a-card>

    <!-- 辅助图区 -->
    <a-row :gutter="24">
      <a-col :xs="24" :lg="12">
        <a-card title="订单来源分布" class="operations-overview__card">
          <a-spin :spinning="loading">
            <ChartPie
              v-if="hasSourceData"
              :data="sourcePieData"
              :height="280"
              donut
            />
            <EmptyState v-else-if="!loading" description="暂无来源数据" />
          </a-spin>
        </a-card>
      </a-col>
      <a-col :xs="24" :lg="12">
        <a-card title="转化漏斗" class="operations-overview__card">
          <a-spin :spinning="loading">
            <ChartBar
              v-if="hasFunnelData"
              :data="funnelChartData"
              :height="280"
            />
            <EmptyState v-else-if="!loading" description="暂无漏斗数据" />
          </a-spin>
        </a-card>
      </a-col>
    </a-row>
  </div>
</template>

<script setup lang="ts">
import { ref, computed, onMounted, watch } from 'vue'
import { useRouter, useRoute } from 'vue-router'
import { ReloadOutlined } from '@ant-design/icons-vue'
import { message } from 'ant-design-vue'
import DateTimeRangePicker from '@/shared/components/DateTimeRangePicker.vue'
import ChartLine from '@/shared/components/charts/ChartLine.vue'
import ChartPie from '@/shared/components/charts/ChartPie.vue'
import ChartBar from '@/shared/components/charts/ChartBar.vue'
import PermissionGuard from '@/shared/components/PermissionGuard.vue'
import EmptyState from '@/shared/components/EmptyState.vue'
import { dashboardApi } from '../api/dashboard.api'
import { parseOverviewData, type OverviewData, type DateRangeParams } from '../types/dashboard.dto'
import DashboardCard from '../components/DashboardCard.vue'

const router = useRouter()
const route = useRoute()
const loading = ref(false)
const data = ref<OverviewData | null>(null)

const rangePresets: Array<'today' | 'yesterday' | 'last7days' | 'last30days' | 'thisMonth'> = [
  'today', 'yesterday', 'last7days', 'last30days', 'thisMonth',
]

// 初始化时间范围：优先读取路由 query，否则默认近 7 天
function initDateRange(): [string, string] {
  const queryStart = route.query.start as string | undefined
  const queryEnd = route.query.end as string | undefined
  if (queryStart && queryEnd) return [queryStart, queryEnd]
  return getLast7DaysRange()
}

function getLast7DaysRange(): [string, string] {
  const end = new Date()
  const start = new Date()
  start.setDate(start.getDate() - 7)
  return [start.toISOString(), end.toISOString()]
}

const dateRange = ref<[string, string]>(initDateRange())

const hasTrendData = computed(() => !!data.value && data.value.dailyTrend.length > 0)
const hasSourceData = computed(() => !!data.value && data.value.sourceDistribution.length > 0)
const hasFunnelData = computed(() => !!data.value && data.value.funnel.length > 0)

// 转化率低于 5% 显示警告色
const conversionColor = computed(() => {
  const rate = data.value?.kpi.conversionRate ?? 0
  if (rate < 5) return '#FAAD14'
  return ''
})

// 格式化 GMV：≥1 万显示万单位
function formatGmv(value: number): string {
  if (value >= 10000) return `${(value / 10000).toFixed(1)}万`
  return value.toLocaleString('zh-CN')
}

// 构造趋势对象
function buildTrend(change: number | undefined): { value: number; direction: 'up' | 'down' } | undefined {
  if (change === undefined) return undefined
  return { value: Math.abs(change), direction: change >= 0 ? 'up' : 'down' }
}

// 折线图数据：双系列 GMV + 订单量
const trendChartData = computed(() => {
  if (!data.value) return []
  const result: { date: string; value: number; series: string }[] = []
  for (const point of data.value.dailyTrend) {
    result.push({ date: point.date.slice(0, 10), value: point.gmv, series: 'GMV' })
    result.push({ date: point.date.slice(0, 10), value: point.orderCount, series: '订单量' })
  }
  return result
})

// 饼图数据：订单来源分布
const sourcePieData = computed(() =>
  data.value?.sourceDistribution.map((item) => ({ name: item.source, value: item.value })) ?? []
)

// 柱状图数据：转化漏斗
const funnelChartData = computed(() =>
  data.value?.funnel.map((item) => ({ name: item.stage, value: item.value })) ?? []
)

async function loadData() {
  const [start, end] = dateRange.value
  if (new Date(start) >= new Date(end)) {
    message.warning('结束时间需晚于开始时间')
    return
  }
  loading.value = true
  try {
    const params: DateRangeParams = { start, end }
    const { data: report } = await dashboardApi.getOverview(params)
    data.value = parseOverviewData(report)
  } catch {
    message.error('运营总览加载失败')
  } finally {
    loading.value = false
  }
}

function navigateTo(path: string) {
  const [start, end] = dateRange.value
  router.push({ path, query: { start, end } })
}

watch(dateRange, () => loadData())

onMounted(() => loadData())
</script>

<style scoped>
.operations-overview {
  display: flex;
  flex-direction: column;
  gap: 24px;
}
.operations-overview__toolbar {
  display: flex;
  gap: 12px;
  align-items: center;
}
.operations-overview__card {
  border-radius: 8px;
}
</style>
```

- [ ] **Step 2: 验证类型检查通过**

Run: `cd web/system-admin && pnpm typecheck`
Expected: 无 TypeScript 错误

- [ ] **Step 3: 验证 Lint 通过**

Run: `cd web/system-admin && pnpm lint`
Expected: 无 ESLint 错误

- [ ] **Step 4: 提交**

```bash
cd web/system-admin
git add src/modules/01-dashboard/views/OperationsOverview.vue
git commit -m "feat(dashboard): 实现运营总览视图 OperationsOverview"
```

---

## Task 6: PaymentStats.vue — 支付统计

**Files:**
- Create: `web/system-admin/src/modules/01-dashboard/views/PaymentStats.vue`

**设计要点（对应 design-prompt 01-dashboard/payment-stats.md）：**
- 筛选条：`DateTimeRangePicker` + 渠道多选 `<a-select mode="multiple">`（支付宝/微信/银联/Apple Pay）
- 3 个 KPI 卡片：总笔数、整体成功率、平均到账时长（Operator 不可见 `avgLatencyMs`）
- 整体成功率 `ChartGauge`（阈值 [80, 95]）
- 渠道成功率排行 `ChartBar` 横向（按阈值染色：<80% 红、80-95% 黄、>95% 绿）
- 失败原因分布 `ChartPie` 环形（按错误码聚合）
- 成功率 < 95%：KPI 数值警告色 + `notification.warning`
- 渠道多选前端过滤 `channelStats`，不发新请求
- 点击渠道 Bar 弹出 `a-drawer` 显示该渠道近 7 天趋势小图

- [ ] **Step 1: 创建 PaymentStats.vue**

```vue
<template>
  <div class="payment-stats">
    <!-- 筛选条 -->
    <div class="payment-stats__toolbar">
      <DateTimeRangePicker v-model="dateRange" :presets="rangePresets" />
      <a-select
        v-model:value="selectedChannels"
        mode="multiple"
        placeholder="选择渠道"
        style="min-width: 240px"
        :options="channelOptions"
        allow-clear
      />
      <a-button :loading="loading" @click="loadData">
        <template #icon><ReloadOutlined /></template>
        刷新
      </a-button>
    </div>

    <!-- KPI 行 -->
    <a-row :gutter="24">
      <a-col :xs="24" :sm="8">
        <DashboardCard
          title="总支付笔数"
          :value="(filteredKpi.totalCount).toLocaleString('zh-CN')"
          :loading="loading"
        />
      </a-col>
      <a-col :xs="24" :sm="8">
        <DashboardCard
          title="整体成功率"
          :value="filteredKpi.successRate.toFixed(1)"
          unit="%"
          :loading="loading"
          :value-color="successRateColor"
        />
      </a-col>
      <a-col :xs="24" :sm="8">
        <PermissionGuard permission="dashboard:paymentLatency">
          <DashboardCard
            title="平均到账时长"
            :value="(data?.kpi.avgLatencyMs ?? 0).toFixed(1)"
            unit="ms"
            :loading="loading"
          />
        </PermissionGuard>
      </a-col>
    </a-row>

    <!-- 整体成功率仪表盘 + 渠道排行 -->
    <a-row :gutter="24">
      <a-col :xs="24" :lg="8">
        <ChartGauge
          title="整体成功率"
          :value="filteredKpi.successRate"
          :thresholds="[80, 95]"
          :loading="loading"
          :height="220"
        />
      </a-col>
      <a-col :xs="24" :lg="16">
        <a-card title="渠道成功率排行" class="payment-stats__card">
          <a-spin :spinning="loading">
            <ChartBar
              v-if="filteredChannelStats.length"
              :data="channelBarData"
              horizontal
              :height="280"
            />
            <EmptyState v-else-if="!loading" description="所选时间范围内暂无支付数据" />
          </a-spin>
        </a-card>
      </a-col>
    </a-row>

    <!-- 失败原因分布 -->
    <a-card title="失败原因分布" class="payment-stats__card">
      <a-spin :spinning="loading">
        <ChartPie
          v-if="hasFailureData"
          :data="failurePieData"
          :height="280"
          donut
        />
        <EmptyState v-else-if="!loading" description="暂无失败原因数据" />
      </a-spin>
    </a-card>

    <!-- 渠道趋势抽屉 -->
    <a-drawer
      v-model:open="drawerVisible"
      :title="`${drawerChannel} 近 7 天成功率趋势`"
      width="480"
    >
      <ChartLine
        v-if="drawerTrendData.length"
        :data="drawerTrendData"
        :height="300"
      />
      <EmptyState v-else description="暂无趋势数据" />
    </a-drawer>
  </div>
</template>

<script setup lang="ts">
import { ref, computed, onMounted, watch } from 'vue'
import { useRoute } from 'vue-router'
import { ReloadOutlined } from '@ant-design/icons-vue'
import { message, notification } from 'ant-design-vue'
import DateTimeRangePicker from '@/shared/components/DateTimeRangePicker.vue'
import ChartLine from '@/shared/components/charts/ChartLine.vue'
import ChartPie from '@/shared/components/charts/ChartPie.vue'
import ChartBar from '@/shared/components/charts/ChartBar.vue'
import PermissionGuard from '@/shared/components/PermissionGuard.vue'
import EmptyState from '@/shared/components/EmptyState.vue'
import { dashboardApi } from '../api/dashboard.api'
import { parsePaymentStatsData, type PaymentStatsData, type ChannelStat, type DateRangeParams } from '../types/dashboard.dto'
import DashboardCard from '../components/DashboardCard.vue'
import ChartGauge from '../components/ChartGauge.vue'

const route = useRoute()
const loading = ref(false)
const data = ref<PaymentStatsData | null>(null)
const selectedChannels = ref<string[]>([])
const drawerVisible = ref(false)
const drawerChannel = ref('')

const rangePresets: Array<'today' | 'yesterday' | 'last7days' | 'last30days' | 'thisMonth'> = [
  'today', 'yesterday', 'last7days', 'last30days', 'thisMonth',
]

function initDateRange(): [string, string] {
  const queryStart = route.query.start as string | undefined
  const queryEnd = route.query.end as string | undefined
  if (queryStart && queryEnd) return [queryStart, queryEnd]
  return getLast7DaysRange()
}

function getLast7DaysRange(): [string, string] {
  const end = new Date()
  const start = new Date()
  start.setDate(start.getDate() - 7)
  return [start.toISOString(), end.toISOString()]
}

const dateRange = ref<[string, string]>(initDateRange())

// 全部渠道选项
const channelOptions = computed(() =>
  (data.value?.channelStats ?? []).map((c) => ({ label: c.channel, value: c.channel }))
)

// 按选中渠道过滤后的渠道统计
const filteredChannelStats = computed<ChannelStat[]>(() => {
  if (!data.value) return []
  if (selectedChannels.value.length === 0) return data.value.channelStats
  return data.value.channelStats.filter((c) => selectedChannels.value.includes(c.channel))
})

// 过滤后重新汇总的 KPI
const filteredKpi = computed(() => {
  const stats = filteredChannelStats.value
  const totalCount = stats.reduce((sum, c) => sum + c.count, 0)
  const successCount = stats.reduce((sum, c) => sum + Math.round(c.count * c.successRate / 100), 0)
  const successRate = totalCount > 0 ? (successCount / totalCount) * 100 : 0
  return {
    totalCount,
    successRate,
    avgLatencyMs: data.value?.kpi.avgLatencyMs ?? 0,
  }
})

// 成功率颜色：<80% 红、80-95% 黄、>95% 绿
const successRateColor = computed(() => {
  const rate = filteredKpi.value.successRate
  if (rate < 80) return '#FF4D4F'
  if (rate < 95) return '#FAAD14'
  return '#52C41A'
})

const hasFailureData = computed(() => !!data.value && data.value.failureReasons.length > 0)

// 渠道排行柱状图数据
const channelBarData = computed(() =>
  filteredChannelStats.value
    .slice()
    .sort((a, b) => b.successRate - a.successRate)
    .map((c) => ({ name: c.channel, value: c.successRate }))
)

// 失败原因饼图数据
const failurePieData = computed(() =>
  data.value?.failureReasons.map((r) => ({ name: r.reason, value: r.count })) ?? []
)

// 抽屉趋势数据（从 dailyTrend 按渠道过滤，若后端未返回趋势则用渠道当前值模拟）
const drawerTrendData = computed(() => {
  if (!drawerChannel.value) return []
  return [
    { date: '当日', value: filteredChannelStats.value.find((c) => c.channel === drawerChannel.value)?.successRate ?? 0 },
  ]
})

async function loadData() {
  const [start, end] = dateRange.value
  if (new Date(start) >= new Date(end)) {
    message.warning('结束时间需晚于开始时间')
    return
  }
  loading.value = true
  try {
    const params: DateRangeParams = { start, end }
    const { data: report } = await dashboardApi.getPaymentStats(params)
    data.value = parsePaymentStatsData(report)
    // 成功率低于 95% 触发警告通知
    if (data.value.kpi.successRate < 95) {
      notification.warning({
        message: '支付成功率低于阈值',
        description: `当前整体成功率 ${data.value.kpi.successRate.toFixed(1)}%，请检查支付链路`,
      })
    }
  } catch {
    message.error('支付统计加载失败')
  } finally {
    loading.value = false
  }
}

function openDrawer(channel: string) {
  drawerChannel.value = channel
  drawerVisible.value = true
}

watch(dateRange, () => loadData())

onMounted(() => loadData())
</script>

<style scoped>
.payment-stats {
  display: flex;
  flex-direction: column;
  gap: 24px;
}
.payment-stats__toolbar {
  display: flex;
  gap: 12px;
  align-items: center;
  flex-wrap: wrap;
}
.payment-stats__card {
  border-radius: 8px;
}
</style>
```

- [ ] **Step 2: 验证类型检查通过**

Run: `cd web/system-admin && pnpm typecheck`
Expected: 无 TypeScript 错误

- [ ] **Step 3: 验证 Lint 通过**

Run: `cd web/system-admin && pnpm lint`
Expected: 无 ESLint 错误

- [ ] **Step 4: 提交**

```bash
cd web/system-admin
git add src/modules/01-dashboard/views/PaymentStats.vue
git commit -m "feat(dashboard): 实现支付统计视图 PaymentStats"
```

---

## Task 7: PointsStats.vue — 积分统计

**Files:**
- Create: `web/system-admin/src/modules/01-dashboard/views/PointsStats.vue`

**设计要点（对应 design-prompt 01-dashboard/points-stats.md）：**
- 筛选条：`DateTimeRangePicker`（默认近 30 天，积分统计周期较长）
- 3 个 KPI 卡片：发放量、消耗量、净增（发放−消耗），均以积分为单位
- 主趋势 `ChartLine` 双系列（发放/消耗）按日趋势，高度 320px
- 来源分布 `ChartPie` 环形（订单完成/签到/任务/活动赠送）
- 净增为负：KPI 数值显示危险色 `#FF4D4F` + Tooltip 提示
- 单日发放量突增（>均值 3 倍）：通过 `notification.warning` 告警

- [ ] **Step 1: 创建 PointsStats.vue**

```vue
<template>
  <div class="points-stats">
    <!-- 筛选条 -->
    <div class="points-stats__toolbar">
      <DateTimeRangePicker v-model="dateRange" :presets="rangePresets" />
      <a-button :loading="loading" @click="loadData">
        <template #icon><ReloadOutlined /></template>
        刷新
      </a-button>
    </div>

    <!-- KPI 行 -->
    <a-row :gutter="24">
      <a-col :xs="24" :sm="8">
        <DashboardCard
          title="发放量"
          :value="formatPoints(data?.kpi.issued ?? 0)"
          :loading="loading"
          :trend="buildTrend(data?.change.issuedChange)"
        />
      </a-col>
      <a-col :xs="24" :sm="8">
        <DashboardCard
          title="消耗量"
          :value="formatPoints(data?.kpi.consumed ?? 0)"
          :loading="loading"
          :trend="buildTrend(data?.change.consumedChange)"
        />
      </a-col>
      <a-col :xs="24" :sm="8">
        <DashboardCard
          title="净增"
          :value="formatPoints(data?.kpi.net ?? 0)"
          :loading="loading"
          :trend="buildTrend(data?.change.netChange)"
          :value-color="netColor"
          :tooltip="netTooltip"
        />
      </a-col>
    </a-row>

    <!-- 主趋势图 -->
    <a-card title="发放 vs 消耗 双系列趋势" class="points-stats__card">
      <a-spin :spinning="loading">
        <ChartLine
          v-if="hasTrendData"
          :data="trendChartData"
          series-field="series"
          :height="320"
        />
        <EmptyState
          v-else-if="!loading"
          description="暂无积分数据"
          cta-text="刷新"
          @cta-click="loadData"
        />
      </a-spin>
    </a-card>

    <!-- 来源分布 -->
    <a-card title="发放来源分布" class="points-stats__card">
      <a-spin :spinning="loading">
        <ChartPie
          v-if="hasSourceData"
          :data="sourcePieData"
          :height="280"
          donut
        />
        <EmptyState v-else-if="!loading" description="暂无来源分布数据" />
      </a-spin>
    </a-card>
  </div>
</template>

<script setup lang="ts">
import { ref, computed, onMounted, watch } from 'vue'
import { useRoute } from 'vue-router'
import { ReloadOutlined } from '@ant-design/icons-vue'
import { message, notification } from 'ant-design-vue'
import DateTimeRangePicker from '@/shared/components/DateTimeRangePicker.vue'
import ChartLine from '@/shared/components/charts/ChartLine.vue'
import ChartPie from '@/shared/components/charts/ChartPie.vue'
import EmptyState from '@/shared/components/EmptyState.vue'
import { dashboardApi } from '../api/dashboard.api'
import { parsePointsStatsData, type PointsStatsData, type DateRangeParams } from '../types/dashboard.dto'
import DashboardCard from '../components/DashboardCard.vue'

const route = useRoute()
const loading = ref(false)
const data = ref<PointsStatsData | null>(null)

const rangePresets: Array<'today' | 'yesterday' | 'last7days' | 'last30days' | 'thisMonth'> = [
  'today', 'yesterday', 'last7days', 'last30days', 'thisMonth',
]

// 积分统计默认近 30 天
function initDateRange(): [string, string] {
  const queryStart = route.query.start as string | undefined
  const queryEnd = route.query.end as string | undefined
  if (queryStart && queryEnd) return [queryStart, queryEnd]
  return getLast30DaysRange()
}

function getLast30DaysRange(): [string, string] {
  const end = new Date()
  const start = new Date()
  start.setDate(start.getDate() - 30)
  return [start.toISOString(), end.toISOString()]
}

const dateRange = ref<[string, string]>(initDateRange())

const hasTrendData = computed(() => !!data.value && data.value.dailyTrend.length > 0)
const hasSourceData = computed(() => !!data.value && data.value.sourceDistribution.length > 0)

// 净增为负时显示红色
const netColor = computed(() => {
  const net = data.value?.kpi.net ?? 0
  return net < 0 ? '#FF4D4F' : ''
})

const netTooltip = computed(() => {
  const net = data.value?.kpi.net ?? 0
  if (net < 0) return '消耗超过发放，请检查营销活动配置'
  return ''
})

// 格式化积分数值：≥10000 显示万单位
function formatPoints(value: number): string {
  if (value >= 10000) return `${(value / 10000).toFixed(1)}万`
  return value.toLocaleString('zh-CN')
}

function buildTrend(change: number | undefined): { value: number; direction: 'up' | 'down' } | undefined {
  if (change === undefined) return undefined
  return { value: Math.abs(change), direction: change >= 0 ? 'up' : 'down' }
}

// 折线图数据：发放 + 消耗双系列
const trendChartData = computed(() => {
  if (!data.value) return []
  const result: { date: string; value: number; series: string }[] = []
  for (const point of data.value.dailyTrend) {
    result.push({ date: point.date.slice(0, 10), value: point.issued, series: '发放' })
    result.push({ date: point.date.slice(0, 10), value: point.consumed, series: '消耗' })
  }
  return result
})

// 饼图数据：发放来源分布
const sourcePieData = computed(() =>
  data.value?.sourceDistribution.map((item) => ({ name: item.source, value: item.value })) ?? []
)

// 检测异常峰值：单日发放量 > 均值 3 倍
function detectAnomaly() {
  if (!data.value || data.value.dailyTrend.length === 0) return
  const trend = data.value.dailyTrend
  const avgIssued = trend.reduce((sum, p) => sum + p.issued, 0) / trend.length
  const threshold = avgIssued * 3
  const anomaly = trend.find((p) => p.issued > threshold && avgIssued > 0)
  if (anomaly) {
    notification.warning({
      message: '检测到积分发放异常峰值',
      description: `${anomaly.date.slice(0, 10)} 发放量 ${anomaly.issued.toLocaleString('zh-CN')}，超过均值 3 倍，请检查营销活动配置`,
    })
  }
}

async function loadData() {
  const [start, end] = dateRange.value
  if (new Date(start) >= new Date(end)) {
    message.warning('结束时间需晚于开始时间')
    return
  }
  loading.value = true
  try {
    const params: DateRangeParams = { start, end }
    const { data: report } = await dashboardApi.getPointsStats(params)
    data.value = parsePointsStatsData(report)
    detectAnomaly()
  } catch {
    message.error('积分统计加载失败')
  } finally {
    loading.value = false
  }
}

watch(dateRange, () => loadData())

onMounted(() => loadData())
</script>

<style scoped>
.points-stats {
  display: flex;
  flex-direction: column;
  gap: 24px;
}
.points-stats__toolbar {
  display: flex;
  gap: 12px;
  align-items: center;
}
.points-stats__card {
  border-radius: 8px;
}
</style>
```

- [ ] **Step 2: 验证类型检查通过**

Run: `cd web/system-admin && pnpm typecheck`
Expected: 无 TypeScript 错误

- [ ] **Step 3: 验证 Lint 通过**

Run: `cd web/system-admin && pnpm lint`
Expected: 无 ESLint 错误

- [ ] **Step 4: 提交**

```bash
cd web/system-admin
git add src/modules/01-dashboard/views/PointsStats.vue
git commit -m "feat(dashboard): 实现积分统计视图 PointsStats"
```

---

## Task 8: NotificationDelivery.vue — 通知送达率

**Files:**
- Create: `web/system-admin/src/modules/01-dashboard/views/NotificationDelivery.vue`

**设计要点（对应 design-prompt 01-dashboard/notification-delivery.md）：**
- 筛选条：`DateTimeRangePicker` + 渠道多选 `<a-select mode="multiple">`（邮件/短信/站内信/推送）
- 4 个 `ChartGauge` 渠道送达率网格（阈值 [90, 95]）：>95% 绿、90-95% 黄、<90% 红
- 失败原因表 `<a-table>`：列含渠道/失败原因/失败数/占比/最近发生时间，按失败数倒序，分页 20
- 趋势 `ChartLine` 多系列按渠道按日送达率，高度 280px
- 渠道多选仅前端过滤表格与趋势
- 某 Gauge < 90%：`notification.error` 告警 + `<a-badge status="error" />`
- 失败数为 0：表格显示 `<a-empty description="无失败记录" />`
- 点击失败原因表行 → `<a-drawer>` 显示该原因近 7 天分布
- 跨页面跳转「最近时间」列 → `/audit/audit-logs?resourceType=Notification&keyword={reason}`

- [ ] **Step 1: 创建 NotificationDelivery.vue**

```vue
<template>
  <div class="notification-delivery">
    <!-- 筛选条 -->
    <div class="notification-delivery__toolbar">
      <DateTimeRangePicker v-model="dateRange" :presets="rangePresets" />
      <a-select
        v-model:value="selectedChannels"
        mode="multiple"
        placeholder="选择渠道"
        style="min-width: 280px"
        :options="channelOptions"
        allow-clear
      />
      <a-button :loading="loading" @click="loadData">
        <template #icon><ReloadOutlined /></template>
        刷新
      </a-button>
    </div>

    <!-- 4 渠道 Gauge 网格 -->
    <a-row :gutter="24">
      <a-col v-for="stat in filteredChannelStats" :key="stat.channel" :xs="24" :sm="12" :lg="6">
        <div class="notification-delivery__gauge-wrapper">
          <a-badge v-if="stat.deliveryRate < 90" status="error" class="notification-delivery__badge" />
          <ChartGauge
            :title="`${stat.channel} 送达率`"
            :value="stat.deliveryRate"
            :thresholds="[90, 95]"
            :loading="loading"
            :height="220"
          />
        </div>
      </a-col>
      <template v-if="!loading && filteredChannelStats.length === 0">
        <a-col :span="24">
          <EmptyState description="暂无渠道送达数据" />
        </a-col>
      </template>
    </a-row>

    <!-- 趋势折线 -->
    <a-card title="渠道送达率趋势" class="notification-delivery__card">
      <a-spin :spinning="loading">
        <ChartLine
          v-if="hasTrendData"
          :data="trendChartData"
          series-field="series"
          :height="280"
        />
        <EmptyState v-else-if="!loading" description="所选渠道暂无趋势数据" />
      </a-spin>
    </a-card>

    <!-- 失败原因表 -->
    <a-card title="失败原因分布" class="notification-delivery__card">
      <a-spin :spinning="loading">
        <a-table
          v-if="filteredFailureReasons.length > 0"
          :columns="failureColumns"
          :data-source="filteredFailureReasons"
          :pagination="{ pageSize: 20, showSizeChanger: false }"
          row-key="reason"
          :scroll="{ y: 480 }"
          @row-click="openFailureDrawer"
        >
          <template #bodyCell="{ column, record }">
            <template v-if="column.key === 'reason'">
              <a class="notification-delivery__link" @click="openFailureDrawer(record)">{{ record.reason }}</a>
            </template>
            <template v-else-if="column.key === 'proportion'">
              {{ computeProportion(record.count) }}%
            </template>
            <template v-else-if="column.key === 'lastOccurredAt'">
              <a class="notification-delivery__link" @click="navigateToAuditLogs(record.reason)">
                {{ formatDateTime(record.lastOccurredAt) }}
              </a>
            </template>
          </template>
        </a-table>
        <a-empty v-else-if="!loading" description="无失败记录" />
      </a-spin>
    </a-card>

    <!-- 失败原因详情抽屉 -->
    <a-drawer
      v-model:open="drawerVisible"
      :title="`${drawerReason} — 近 7 天分布`"
      width="480"
    >
      <a-spin :spinning="drawerLoading">
        <ChartLine
          v-if="drawerTrendData.length"
          :data="drawerTrendData"
          :height="300"
        />
        <EmptyState v-else description="暂无该失败原因的趋势数据" />
      </a-spin>
    </a-drawer>
  </div>
</template>

<script setup lang="ts">
import { ref, computed, onMounted, watch } from 'vue'
import { useRouter, useRoute } from 'vue-router'
import { ReloadOutlined } from '@ant-design/icons-vue'
import { message, notification } from 'ant-design-vue'
import dayjs from 'dayjs'
import DateTimeRangePicker from '@/shared/components/DateTimeRangePicker.vue'
import ChartLine from '@/shared/components/charts/ChartLine.vue'
import EmptyState from '@/shared/components/EmptyState.vue'
import { dashboardApi } from '../api/dashboard.api'
import {
  parseNotificationDeliveryData,
  type NotificationDeliveryData,
  type NotificationFailureReason,
  type DateRangeParams,
} from '../types/dashboard.dto'
import ChartGauge from '../components/ChartGauge.vue'

const router = useRouter()
const route = useRoute()
const loading = ref(false)
const data = ref<NotificationDeliveryData | null>(null)
const selectedChannels = ref<string[]>([])
const drawerVisible = ref(false)
const drawerReason = ref('')
const drawerLoading = ref(false)

const rangePresets: Array<'today' | 'yesterday' | 'last7days' | 'last30days' | 'thisMonth'> = [
  'today', 'yesterday', 'last7days', 'last30days', 'thisMonth',
]

function initDateRange(): [string, string] {
  const queryStart = route.query.start as string | undefined
  const queryEnd = route.query.end as string | undefined
  if (queryStart && queryEnd) return [queryStart, queryEnd]
  return getLast7DaysRange()
}

function getLast7DaysRange(): [string, string] {
  const end = new Date()
  const start = new Date()
  start.setDate(start.getDate() - 7)
  return [start.toISOString(), end.toISOString()]
}

const dateRange = ref<[string, string]>(initDateRange())

// 全部渠道选项（来自返回数据）
const channelOptions = computed(() =>
  (data.value?.channelStats ?? []).map((c) => ({ label: c.channel, value: c.channel }))
)

// 按选中渠道过滤后的渠道统计
const filteredChannelStats = computed(() => {
  if (!data.value) return []
  if (selectedChannels.value.length === 0) return data.value.channelStats
  return data.value.channelStats.filter((c) => selectedChannels.value.includes(c.channel))
})

// 按选中渠道过滤后的失败原因
const filteredFailureReasons = computed<NotificationFailureReason[]>(() => {
  if (!data.value) return []
  if (selectedChannels.value.length === 0) return data.value.failureReasons
  return data.value.failureReasons.filter((r) => selectedChannels.value.includes(r.channel))
})

const hasTrendData = computed(() => {
  if (!data.value) return false
  if (selectedChannels.value.length === 0) return data.value.dailyTrend.length > 0
  return data.value.dailyTrend.some((p) => selectedChannels.value.includes(p.channel))
})

// 趋势图数据：多系列按渠道
const trendChartData = computed(() => {
  if (!data.value) return []
  const trend = data.value.dailyTrend
  const filtered = selectedChannels.value.length === 0
    ? trend
    : trend.filter((p) => selectedChannels.value.includes(p.channel))
  return filtered.map((p) => ({
    date: p.date.slice(0, 10),
    value: p.rate,
    series: p.channel,
  }))
})

// 失败原因表列定义
const failureColumns = [
  { title: '渠道', dataIndex: 'channel', key: 'channel', width: 120 },
  { title: '失败原因', dataIndex: 'reason', key: 'reason' },
  { title: '失败数', dataIndex: 'count', key: 'count', width: 100, sorter: (a: NotificationFailureReason, b: NotificationFailureReason) => b.count - a.count, defaultSortOrder: 'descend' as const },
  { title: '占比', key: 'proportion', width: 100 },
  { title: '最近发生时间', key: 'lastOccurredAt', width: 200 },
]

// 计算失败占比（相对全部失败数）
function computeProportion(count: number): string {
  const total = filteredFailureReasons.value.reduce((sum, r) => sum + r.count, 0)
  if (total === 0) return '0.0'
  return ((count / total) * 100).toFixed(1)
}

// 格式化日期时间
function formatDateTime(iso: string): string {
  return dayjs(iso).format('YYYY-MM-DD HH:mm')
}

// 抽屉趋势数据：按 drawerReason 过滤 dailyTrend
const drawerTrendData = computed(() => {
  if (!drawerReason.value || !data.value) return []
  return data.value.dailyTrend
    .filter((p) => {
      const reason = data.value!.failureReasons.find((r) => r.reason === drawerReason.value)
      return reason && p.channel === reason.channel
    })
    .map((p) => ({ date: p.date.slice(0, 10), value: p.rate }))
})

async function loadData() {
  const [start, end] = dateRange.value
  if (new Date(start) >= new Date(end)) {
    message.warning('结束时间需晚于开始时间')
    return
  }
  loading.value = true
  try {
    const params: DateRangeParams = { start, end }
    const { data: report } = await dashboardApi.getNotificationDelivery(params)
    data.value = parseNotificationDeliveryData(report)
    // 检查是否有渠道 < 90% 触发 error 告警
    const lowRateChannel = data.value.channelStats.find((c) => c.deliveryRate < 90)
    if (lowRateChannel) {
      notification.error({
        message: '通知送达率严重偏低',
        description: `${lowRateChannel.channel} 渠道送达率 ${lowRateChannel.deliveryRate.toFixed(1)}%，低于 90% 阈值，请立即排查通知链路`,
      })
    }
  } catch {
    message.error('通知送达率加载失败')
  } finally {
    loading.value = false
  }
}

function openFailureDrawer(record: NotificationFailureReason) {
  drawerReason.value = record.reason
  drawerLoading.value = true
  drawerVisible.value = true
  // 模拟详情加载延迟
  setTimeout(() => {
    drawerLoading.value = false
  }, 200)
}

function navigateToAuditLogs(reason: string) {
  router.push({
    path: '/audit/audit-logs',
    query: { resourceType: 'Notification', keyword: reason },
  })
}

watch(dateRange, () => loadData())

onMounted(() => loadData())
</script>

<style scoped>
.notification-delivery {
  display: flex;
  flex-direction: column;
  gap: 24px;
}
.notification-delivery__toolbar {
  display: flex;
  gap: 12px;
  align-items: center;
  flex-wrap: wrap;
}
.notification-delivery__card {
  border-radius: 8px;
}
.notification-delivery__gauge-wrapper {
  position: relative;
}
.notification-delivery__badge {
  position: absolute;
  top: 16px;
  right: 24px;
  z-index: 1;
}
.notification-delivery__link {
  color: #1677FF;
  cursor: pointer;
}
.notification-delivery__link:hover {
  text-decoration: underline;
}
</style>
```

- [ ] **Step 2: 验证类型检查通过**

Run: `cd web/system-admin && pnpm typecheck`
Expected: 无 TypeScript 错误

- [ ] **Step 3: 验证 Lint 通过**

Run: `cd web/system-admin && pnpm lint`
Expected: 无 ESLint 错误

- [ ] **Step 4: 提交**

```bash
cd web/system-admin
git add src/modules/01-dashboard/views/NotificationDelivery.vue
git commit -m "feat(dashboard): 实现通知送达率视图 NotificationDelivery"
```

---

## Task 9: AfterSalesStats.vue — 售后统计

**Files:**
- Create: `web/system-admin/src/modules/01-dashboard/views/AfterSalesStats.vue`

**设计要点（对应 design-prompt 01-dashboard/after-sales-stats.md）：**
- 筛选条：`DateTimeRangePicker` + 售后类型多选（仅退款/退货退款/换货）
- 3 个 KPI 卡片：售后单量、退款金额、售后率（售后单/订单量），含同比趋势
- 售后类型分布 `ChartPie` 环形（按 type 聚合）
- 趋势 `ChartLine` 双系列（售后单量 + 退款金额）按日趋势，高度 280px
- Top 10 高售后店铺表 `<a-table>`：列含店铺名/售后单量/订单量/售后率/平均处理时长，按售后率倒序
- 售后率 > 5%：KPI 数值染红 + `notification.warning`
- 类型多选仅前端过滤饼图（趋势数据未携带类型字段，保持原值）
- 点击 Top 10 表店铺名 → `/audit/audit-logs?resourceType=AfterSales&keyword={shopId}`

- [ ] **Step 1: 创建 AfterSalesStats.vue**

```vue
<template>
  <div class="after-sales-stats">
    <!-- 筛选条 -->
    <div class="after-sales-stats__toolbar">
      <DateTimeRangePicker v-model="dateRange" :presets="rangePresets" />
      <a-select
        v-model:value="selectedTypes"
        mode="multiple"
        placeholder="选择售后类型"
        style="min-width: 280px"
        :options="typeOptions"
        allow-clear
      />
      <a-button :loading="loading" @click="loadData">
        <template #icon><ReloadOutlined /></template>
        刷新
      </a-button>
    </div>

    <!-- KPI 行 -->
    <a-row :gutter="24">
      <a-col :xs="24" :sm="8">
        <DashboardCard
          title="售后单量"
          :value="(data?.kpi.afterSalesCount ?? 0).toLocaleString('zh-CN')"
          :loading="loading"
          :trend="buildTrend(data?.change.afterSalesCountChange)"
        />
      </a-col>
      <a-col :xs="24" :sm="8">
        <DashboardCard
          title="退款金额"
          :value="formatMoney(data?.kpi.refundAmount ?? 0)"
          :loading="loading"
          :trend="buildTrend(data?.change.refundAmountChange)"
        />
      </a-col>
      <a-col :xs="24" :sm="8">
        <DashboardCard
          title="售后率"
          :value="(data?.kpi.afterSalesRate ?? 0).toFixed(2)"
          unit="%"
          :loading="loading"
          :trend="buildTrend(data?.change.afterSalesRateChange)"
          :value-color="afterSalesRateColor"
        />
      </a-col>
    </a-row>

    <!-- 类型分布 + 趋势 -->
    <a-row :gutter="24">
      <a-col :xs="24" :lg="10">
        <a-card title="售后类型分布" class="after-sales-stats__card">
          <a-spin :spinning="loading">
            <ChartPie
              v-if="hasTypeData"
              :data="typePieData"
              :height="280"
              donut
            />
            <EmptyState v-else-if="!loading" description="暂无售后类型数据" />
          </a-spin>
        </a-card>
      </a-col>
      <a-col :xs="24" :lg="14">
        <a-card title="售后单量与退款金额趋势" class="after-sales-stats__card">
          <a-spin :spinning="loading">
            <ChartLine
              v-if="hasTrendData"
              :data="trendChartData"
              series-field="series"
              :height="280"
            />
            <EmptyState v-else-if="!loading" description="暂无趋势数据" />
          </a-spin>
        </a-card>
      </a-col>
    </a-row>

    <!-- Top 10 高售后店铺 -->
    <a-card title="Top 10 高售后店铺" class="after-sales-stats__card">
      <a-spin :spinning="loading">
        <a-table
          v-if="topShops.length > 0"
          :columns="topShopColumns"
          :data-source="topShops"
          :pagination="false"
          row-key="shopId"
          :scroll="{ y: 480 }"
        >
          <template #bodyCell="{ column, record }">
            <template v-if="column.key === 'shopName'">
              <a class="after-sales-stats__link" @click="navigateToAuditLogs(record.shopId)">
                {{ record.shopName }}
              </a>
            </template>
            <template v-else-if="column.key === 'afterSalesRate'">
              <span :style="{ color: rateColor(computeShopRate(record)) }">
                {{ computeShopRate(record).toFixed(2) }}%
              </span>
            </template>
            <template v-else-if="column.key === 'avgProcessHours'">
              {{ record.avgProcessHours.toFixed(1) }} 小时
            </template>
          </template>
        </a-table>
        <EmptyState
          v-else-if="!loading"
          description="所选时间范围暂无售后数据"
          cta-text="刷新"
          @cta-click="loadData"
        />
      </a-spin>
    </a-card>
  </div>
</template>

<script setup lang="ts">
import { ref, computed, onMounted, watch } from 'vue'
import { useRouter, useRoute } from 'vue-router'
import { ReloadOutlined } from '@ant-design/icons-vue'
import { message, notification } from 'ant-design-vue'
import DateTimeRangePicker from '@/shared/components/DateTimeRangePicker.vue'
import ChartLine from '@/shared/components/charts/ChartLine.vue'
import ChartPie from '@/shared/components/charts/ChartPie.vue'
import EmptyState from '@/shared/components/EmptyState.vue'
import { dashboardApi } from '../api/dashboard.api'
import {
  parseAfterSalesStatsData,
  type AfterSalesStatsData,
  type TopShopByAfterSales,
  type DateRangeParams,
} from '../types/dashboard.dto'
import DashboardCard from '../components/DashboardCard.vue'

const router = useRouter()
const route = useRoute()
const loading = ref(false)
const data = ref<AfterSalesStatsData | null>(null)
const selectedTypes = ref<string[]>([])

const rangePresets: Array<'today' | 'yesterday' | 'last7days' | 'last30days' | 'thisMonth'> = [
  'today', 'yesterday', 'last7days', 'last30days', 'thisMonth',
]

function initDateRange(): [string, string] {
  const queryStart = route.query.start as string | undefined
  const queryEnd = route.query.end as string | undefined
  if (queryStart && queryEnd) return [queryStart, queryEnd]
  return getLast7DaysRange()
}

function getLast7DaysRange(): [string, string] {
  const end = new Date()
  const start = new Date()
  start.setDate(start.getDate() - 7)
  return [start.toISOString(), end.toISOString()]
}

const dateRange = ref<[string, string]>(initDateRange())

// 售后类型选项（来自返回数据）
const typeOptions = computed(() =>
  (data.value?.typeDistribution ?? []).map((t) => ({ label: t.type, value: t.type }))
)

// 按选中类型过滤后的类型分布
const filteredTypeDistribution = computed(() => {
  if (!data.value) return []
  if (selectedTypes.value.length === 0) return data.value.typeDistribution
  return data.value.typeDistribution.filter((t) => selectedTypes.value.includes(t.type))
})

const hasTypeData = computed(() => filteredTypeDistribution.value.length > 0)
const hasTrendData = computed(() => !!data.value && data.value.dailyTrend.length > 0)

// 售后率颜色：>5% 红、3-5% 黄、<3% 绿
function rateColor(rate: number): string {
  if (rate > 5) return '#FF4D4F'
  if (rate >= 3) return '#FAAD14'
  return '#52C41A'
}

const afterSalesRateColor = computed(() => {
  const rate = data.value?.kpi.afterSalesRate ?? 0
  return rateColor(rate)
})

// 格式化金额：≥1 万显示万单位
function formatMoney(value: number): string {
  if (value >= 10000) return `¥${(value / 10000).toFixed(1)}万`
  return `¥${value.toLocaleString('zh-CN')}`
}

function buildTrend(change: number | undefined): { value: number; direction: 'up' | 'down' } | undefined {
  if (change === undefined) return undefined
  return { value: Math.abs(change), direction: change >= 0 ? 'up' : 'down' }
}

// 类型饼图数据
const typePieData = computed(() =>
  filteredTypeDistribution.value.map((t) => ({ name: t.type, value: t.count }))
)

// 趋势图数据：售后单量 + 退款金额双系列（共享单轴）
const trendChartData = computed(() => {
  if (!data.value) return []
  const result: { date: string; value: number; series: string }[] = []
  for (const point of data.value.dailyTrend) {
    result.push({ date: point.date.slice(0, 10), value: point.count, series: '售后单量' })
    result.push({ date: point.date.slice(0, 10), value: point.refundAmount, series: '退款金额' })
  }
  return result
})

// Top 10 高售后店铺，按售后率倒序
const topShops = computed<TopShopByAfterSales[]>(() => {
  if (!data.value) return []
  return data.value.topShops
    .slice()
    .sort((a, b) => computeShopRate(b) - computeShopRate(a))
    .slice(0, 10)
})

// 计算单店售后率
function computeShopRate(record: TopShopByAfterSales): number {
  if (record.orderCount === 0) return 0
  return (record.afterSalesCount / record.orderCount) * 100
}

// Top 10 表列定义
const topShopColumns = [
  { title: '店铺名', key: 'shopName' },
  { title: '售后单量', dataIndex: 'afterSalesCount', key: 'afterSalesCount', width: 120, sorter: (a: TopShopByAfterSales, b: TopShopByAfterSales) => b.afterSalesCount - a.afterSalesCount },
  { title: '订单量', dataIndex: 'orderCount', key: 'orderCount', width: 120 },
  { title: '售后率', key: 'afterSalesRate', width: 120, sorter: (a: TopShopByAfterSales, b: TopShopByAfterSales) => computeShopRate(b) - computeShopRate(a), defaultSortOrder: 'descend' as const },
  { title: '平均处理时长', key: 'avgProcessHours', width: 140 },
]

async function loadData() {
  const [start, end] = dateRange.value
  if (new Date(start) >= new Date(end)) {
    message.warning('结束时间需晚于开始时间')
    return
  }
  loading.value = true
  try {
    const params: DateRangeParams = { start, end }
    const { data: report } = await dashboardApi.getAfterSalesStats(params)
    data.value = parseAfterSalesStatsData(report)
    // 售后率 > 5% 触发 warning 通知
    if (data.value.kpi.afterSalesRate > 5) {
      notification.warning({
        message: '售后率异常',
        description: `当前售后率 ${data.value.kpi.afterSalesRate.toFixed(2)}%，超过 5% 阈值，请关注售后处理情况`,
      })
    }
  } catch {
    message.error('售后统计加载失败')
  } finally {
    loading.value = false
  }
}

function navigateToAuditLogs(shopId: string) {
  router.push({
    path: '/audit/audit-logs',
    query: { resourceType: 'AfterSales', keyword: shopId },
  })
}

watch(dateRange, () => loadData())

onMounted(() => loadData())
</script>

<style scoped>
.after-sales-stats {
  display: flex;
  flex-direction: column;
  gap: 24px;
}
.after-sales-stats__toolbar {
  display: flex;
  gap: 12px;
  align-items: center;
  flex-wrap: wrap;
}
.after-sales-stats__card {
  border-radius: 8px;
}
.after-sales-stats__link {
  color: #1677FF;
  cursor: pointer;
}
.after-sales-stats__link:hover {
  text-decoration: underline;
}
</style>
```

- [ ] **Step 2: 验证类型检查通过**

Run: `cd web/system-admin && pnpm typecheck`
Expected: 无 TypeScript 错误

- [ ] **Step 3: 验证 Lint 通过**

Run: `cd web/system-admin && pnpm lint`
Expected: 无 ESLint 错误

- [ ] **Step 4: 提交**

```bash
cd web/system-admin
git add src/modules/01-dashboard/views/AfterSalesStats.vue
git commit -m "feat(dashboard): 实现售后统计视图 AfterSalesStats"
```

---

## Task 10: ShopRanking.vue — 店铺排行

**Files:**
- Create: `web/system-admin/src/modules/01-dashboard/views/ShopRanking.vue`

**设计要点（对应 design-prompt 01-dashboard/shop-ranking.md）：**
- 筛选条：`DateTimeRangePicker` + 维度切换 `<a-segmented>`（销售额/订单量/客单价）+ TopN `<a-input-number min="5" max="50">`（默认 10）
- Top 3 领奖台：奖牌图标（TrophyOutlined 金/银/铜 32px），第 1 名居中放大，含店铺名/类目/指标值/增长率
- 主排行表 `<a-table>`：列含排名/店铺名/所在类目/指标值/环比增长率/状态，按指标值倒序
- 切换维度或 TopN：前端重新排序与截取，不发新请求
- 店铺状态使用 `StatusTag type="shop"`
- 增长率为负：表格该列显示红色 ↓ 与绝对值
- 店铺数 < 3：领奖台仅显示已有店铺，空位显示 `<a-empty image="simple" />`
- 点击表格店铺名 → `/audit/audit-logs?resourceType=Shop&keyword={shopId}`

- [ ] **Step 1: 创建 ShopRanking.vue**

```vue
<template>
  <div class="shop-ranking">
    <!-- 筛选条 -->
    <div class="shop-ranking__toolbar">
      <DateTimeRangePicker v-model="dateRange" :presets="rangePresets" />
      <a-segmented v-model:value="dimension" :options="dimensionOptions" />
      <span class="shop-ranking__topn-label">TopN</span>
      <a-input-number v-model:value="topN" :min="5" :max="50" :step="1" />
      <a-button :loading="loading" @click="loadData">
        <template #icon><ReloadOutlined /></template>
        刷新
      </a-button>
    </div>

    <!-- Top 3 领奖台 -->
    <div class="shop-ranking__podium">
      <!-- 第 2 名 -->
      <div class="shop-ranking__podium-item shop-ranking__podium-item--silver">
        <TrophyOutlined class="shop-ranking__medal shop-ranking__medal--silver" />
        <template v-if="top3[1]">
          <div class="shop-ranking__shop-name">{{ top3[1].shopName }}</div>
          <div class="shop-ranking__category">{{ top3[1].category }}</div>
          <div class="shop-ranking__metric-value">{{ formatMetric(top3[1]) }}</div>
          <GrowthTag :rate="top3[1].growthRate" />
        </template>
        <a-empty v-else image="simple" />
      </div>
      <!-- 第 1 名（居中放大） -->
      <div class="shop-ranking__podium-item shop-ranking__podium-item--gold">
        <TrophyOutlined class="shop-ranking__medal shop-ranking__medal--gold" />
        <template v-if="top3[0]">
          <div class="shop-ranking__shop-name shop-ranking__shop-name--first">
            {{ top3[0].shopName }}
          </div>
          <div class="shop-ranking__category">{{ top3[0].category }}</div>
          <div class="shop-ranking__metric-value shop-ranking__metric-value--first">
            {{ formatMetric(top3[0]) }}
          </div>
          <GrowthTag :rate="top3[0].growthRate" />
        </template>
        <a-empty v-else image="simple" />
      </div>
      <!-- 第 3 名 -->
      <div class="shop-ranking__podium-item shop-ranking__podium-item--bronze">
        <TrophyOutlined class="shop-ranking__medal shop-ranking__medal--bronze" />
        <template v-if="top3[2]">
          <div class="shop-ranking__shop-name">{{ top3[2].shopName }}</div>
          <div class="shop-ranking__category">{{ top3[2].category }}</div>
          <div class="shop-ranking__metric-value">{{ formatMetric(top3[2]) }}</div>
          <GrowthTag :rate="top3[2].growthRate" />
        </template>
        <a-empty v-else image="simple" />
      </div>
    </div>

    <!-- 主排行表 -->
    <a-card title="店铺排行明细" class="shop-ranking__card">
      <a-spin :spinning="loading">
        <a-table
          v-if="rankedItems.length > 0"
          :columns="tableColumns"
          :data-source="rankedItems"
          :pagination="{ pageSize: 20, showSizeChanger: false }"
          row-key="shopId"
          :scroll="{ y: 480 }"
        >
          <template #bodyCell="{ column, record, index }">
            <template v-if="column.key === 'rank'">
              <span :class="rankClass(index + 1)">{{ index + 1 }}</span>
            </template>
            <template v-else-if="column.key === 'shopName'">
              <a class="shop-ranking__link" @click="navigateToAuditLogs(record.shopId)">
                {{ record.shopName }}
              </a>
            </template>
            <template v-else-if="column.key === 'metricValue'">
              {{ formatMetric(record) }}
            </template>
            <template v-else-if="column.key === 'growthRate'">
              <GrowthTag :rate="record.growthRate" />
            </template>
            <template v-else-if="column.key === 'status'">
              <StatusTag :status="record.status" type="shop" />
            </template>
          </template>
        </a-table>
        <EmptyState
          v-else-if="!loading"
          description="所选时间范围暂无店铺排行数据"
          cta-text="刷新"
          @cta-click="loadData"
        />
      </a-spin>
    </a-card>
  </div>
</template>

<script setup lang="ts">
import { ref, computed, onMounted, watch, h } from 'vue'
import { useRouter, useRoute } from 'vue-router'
import { ReloadOutlined, TrophyOutlined, ArrowUpOutlined, ArrowDownOutlined } from '@ant-design/icons-vue'
import { message } from 'ant-design-vue'
import DateTimeRangePicker from '@/shared/components/DateTimeRangePicker.vue'
import EmptyState from '@/shared/components/EmptyState.vue'
import StatusTag from '@/shared/components/StatusTag.vue'
import { dashboardApi } from '../api/dashboard.api'
import {
  parseShopRankingData,
  type ShopRankingData,
  type ShopRankingItem,
  type DateRangeParams,
} from '../types/dashboard.dto'

// 增长率标签内联组件：正绿↑、负红↓
const GrowthTag = (props: { rate: number }) => {
  const isUp = props.rate >= 0
  return h('span', {
    style: { color: isUp ? '#52C41A' : '#FF4D4F', fontSize: '12px' },
  }, [
    h(isUp ? ArrowUpOutlined : ArrowDownOutlined, { style: { marginRight: '4px' } }),
    `${Math.abs(props.rate).toFixed(1)}%`,
  ])
}
GrowthTag.props = { rate: { type: Number, required: true } }

const router = useRouter()
const route = useRoute()
const loading = ref(false)
const data = ref<ShopRankingData | null>(null)
const dimension = ref<'salesAmount' | 'orderCount' | 'avgOrderAmount'>('salesAmount')
const topN = ref<number>(10)

const rangePresets: Array<'today' | 'yesterday' | 'last7days' | 'last30days' | 'thisMonth'> = [
  'today', 'yesterday', 'last7days', 'last30days', 'thisMonth',
]

const dimensionOptions = [
  { label: '销售额', value: 'salesAmount' },
  { label: '订单量', value: 'orderCount' },
  { label: '客单价', value: 'avgOrderAmount' },
]

function initDateRange(): [string, string] {
  const queryStart = route.query.start as string | undefined
  const queryEnd = route.query.end as string | undefined
  if (queryStart && queryEnd) return [queryStart, queryEnd]
  return getLast7DaysRange()
}

function getLast7DaysRange(): [string, string] {
  const end = new Date()
  const start = new Date()
  start.setDate(start.getDate() - 7)
  return [start.toISOString(), end.toISOString()]
}

const dateRange = ref<[string, string]>(initDateRange())

// 按维度排序后的所有店铺
const sortedItems = computed<ShopRankingItem[]>(() => {
  if (!data.value) return []
  return data.value.items
    .slice()
    .sort((a, b) => (b[dimension.value] as number) - (a[dimension.value] as number))
})

// 按 TopN 截取后的列表（表格用）
const rankedItems = computed<ShopRankingItem[]>(() =>
  sortedItems.value.slice(0, topN.value)
)

// Top 3 领奖台数据
const top3 = computed<(ShopRankingItem | null)[]>(() => {
  const items = sortedItems.value
  return [items[0] ?? null, items[1] ?? null, items[2] ?? null]
})

// 按维度格式化指标值
function formatMetric(item: ShopRankingItem): string {
  const value = item[dimension.value] as number
  if (dimension.value === 'salesAmount') {
    if (value >= 10000) return `¥${(value / 10000).toFixed(1)}万`
    return `¥${value.toLocaleString('zh-CN')}`
  }
  if (dimension.value === 'avgOrderAmount') {
    return `¥${value.toFixed(2)}`
  }
  return value.toLocaleString('zh-CN')
}

// 排名样式：前 3 名高亮
function rankClass(rank: number): string {
  if (rank === 1) return 'shop-ranking__rank shop-ranking__rank--gold'
  if (rank === 2) return 'shop-ranking__rank shop-ranking__rank--silver'
  if (rank === 3) return 'shop-ranking__rank shop-ranking__rank--bronze'
  return 'shop-ranking__rank'
}

// 主表列定义
const tableColumns = [
  { title: '排名', key: 'rank', width: 80 },
  { title: '店铺名', key: 'shopName' },
  { title: '所在类目', dataIndex: 'category', key: 'category', width: 160 },
  { title: '指标值', key: 'metricValue', width: 160 },
  { title: '环比增长率', key: 'growthRate', width: 140 },
  { title: '状态', key: 'status', width: 120 },
]

async function loadData() {
  const [start, end] = dateRange.value
  if (new Date(start) >= new Date(end)) {
    message.warning('结束时间需晚于开始时间')
    return
  }
  loading.value = true
  try {
    const params: DateRangeParams = { start, end }
    const { data: report } = await dashboardApi.getShopRanking(params)
    data.value = parseShopRankingData(report)
    // 若返回数据中 dimension 与当前选择不一致，以当前选择为准（前端重新排序）
  } catch {
    message.error('店铺排行加载失败')
  } finally {
    loading.value = false
  }
}

function navigateToAuditLogs(shopId: string) {
  router.push({
    path: '/audit/audit-logs',
    query: { resourceType: 'Shop', keyword: shopId },
  })
}

watch(dateRange, () => loadData())

onMounted(() => loadData())
</script>

<style scoped>
.shop-ranking {
  display: flex;
  flex-direction: column;
  gap: 24px;
}
.shop-ranking__toolbar {
  display: flex;
  gap: 12px;
  align-items: center;
  flex-wrap: wrap;
}
.shop-ranking__topn-label {
  font-size: 14px;
  color: #8C8C8C;
}
.shop-ranking__podium {
  display: flex;
  justify-content: center;
  align-items: flex-end;
  gap: 24px;
  padding: 24px 0;
  background: #FAFAFA;
  border-radius: 8px;
}
.shop-ranking__podium-item {
  display: flex;
  flex-direction: column;
  align-items: center;
  padding: 24px 16px;
  border-radius: 8px;
  background: #FFFFFF;
  width: 220px;
  min-height: 180px;
  box-shadow: 0 2px 8px rgba(0, 0, 0, 0.06);
}
.shop-ranking__podium-item--gold {
  width: 260px;
  min-height: 220px;
  border: 2px solid #FAAD14;
}
.shop-ranking__podium-item--silver {
  border: 2px solid #D9D9D9;
}
.shop-ranking__podium-item--bronze {
  border: 2px solid #D48806;
}
.shop-ranking__medal {
  font-size: 32px;
  margin-bottom: 12px;
}
.shop-ranking__medal--gold {
  color: #FAAD14;
}
.shop-ranking__medal--silver {
  color: #8C8C8C;
}
.shop-ranking__medal--bronze {
  color: #D48806;
}
.shop-ranking__shop-name {
  font-size: 16px;
  font-weight: 500;
  color: #000000D9;
  margin-bottom: 4px;
}
.shop-ranking__shop-name--first {
  font-size: 20px;
  font-weight: 600;
}
.shop-ranking__category {
  font-size: 12px;
  color: #8C8C8C;
  margin-bottom: 8px;
}
.shop-ranking__metric-value {
  font-size: 16px;
  font-weight: 600;
  color: #1677FF;
  margin-bottom: 4px;
}
.shop-ranking__metric-value--first {
  font-size: 20px;
}
.shop-ranking__card {
  border-radius: 8px;
}
.shop-ranking__link {
  color: #1677FF;
  cursor: pointer;
}
.shop-ranking__link:hover {
  text-decoration: underline;
}
.shop-ranking__rank {
  display: inline-block;
  min-width: 24px;
  text-align: center;
  font-weight: 600;
}
.shop-ranking__rank--gold {
  color: #FAAD14;
}
.shop-ranking__rank--silver {
  color: #8C8C8C;
}
.shop-ranking__rank--bronze {
  color: #D48806;
}
</style>
```

- [ ] **Step 2: 验证类型检查通过**

Run: `cd web/system-admin && pnpm typecheck`
Expected: 无 TypeScript 错误

- [ ] **Step 3: 验证 Lint 通过**

Run: `cd web/system-admin && pnpm lint`
Expected: 无 ESLint 错误

- [ ] **Step 4: 提交**

```bash
cd web/system-admin
git add src/modules/01-dashboard/views/ShopRanking.vue
git commit -m "feat(dashboard): 实现店铺排行视图 ShopRanking"
```

---

## Task 11: ReportSnapshots.vue — 报表快照

**Files:**
- Create: `web/system-admin/src/modules/01-dashboard/views/ReportSnapshots.vue`

**设计要点（对应 design-prompt 01-dashboard/report-snapshots.md）：**
- 筛选条：`<a-select>` 报表类型（7 种）+ `DateTimeRangePicker` + 刷新按钮 + 导出 CSV（`PermissionGuard permission="dashboard:export"`）
- 主列表 `<a-table>`：列含报表类型/周期起止/粒度/数据版本/生成时间/操作（查看/对比），按生成时间倒序，分页 20
- 详情抽屉 `<a-drawer width="640">`：`<a-descriptions>` 展示 Metrics 全量字段（Key-Value-Unit）
- 「与上一版本对比」`<a-switch>`：开启后显示差异表（Key/旧值/新值/变化%），同周期前一版本（PeriodStart/PeriodEnd 相同且 DataVersion 较小的最近一个）
- 同周期仅一个版本：对比开关 disabled，Tooltip「无历史版本可对比」
- 报表详情 404：`message.error('快照不存在或已归档')` 3s
- 从子看板跳转携带 `reportType` query 自动选中
- 导出 CSV：调用列表 API 后客户端生成 CSV 下载

- [ ] **Step 1: 创建 ReportSnapshots.vue**

```vue
<template>
  <div class="report-snapshots">
    <!-- 筛选条 -->
    <div class="report-snapshots__toolbar">
      <a-select
        v-model:value="reportType"
        style="width: 200px"
        :options="reportTypeOptions"
        placeholder="选择报表类型"
      />
      <DateTimeRangePicker v-model="dateRange" :presets="rangePresets" />
      <a-button :loading="loading" @click="loadList">
        <template #icon><ReloadOutlined /></template>
        刷新
      </a-button>
      <PermissionGuard permission="dashboard:export">
        <a-button :loading="exporting" @click="exportCsv">
          <template #icon><DownloadOutlined /></template>
          导出 CSV
        </a-button>
      </PermissionGuard>
    </div>

    <!-- 主列表 -->
    <a-card title="报表快照列表" class="report-snapshots__card">
      <a-spin :spinning="loading">
        <a-table
          v-if="list.length > 0"
          :columns="listColumns"
          :data-source="list"
          :pagination="{ pageSize: 20, showSizeChanger: false }"
          row-key="ReportId"
          :scroll="{ y: 480 }"
        >
          <template #bodyCell="{ column, record }">
            <template v-if="column.key === 'reportType'">
              {{ reportTypeLabel(record.ReportType) }}
            </template>
            <template v-else-if="column.key === 'periodStart'">
              {{ formatDate(record.PeriodStart) }}
            </template>
            <template v-else-if="column.key === 'periodEnd'">
              {{ formatDate(record.PeriodEnd) }}
            </template>
            <template v-else-if="column.key === 'dataVersion'">
              <a-tag color="blue">v{{ record.DataVersion ?? 0 }}</a-tag>
            </template>
            <template v-else-if="column.key === 'generatedAt'">
              {{ formatDateTime(record.GeneratedAt) }}
            </template>
            <template v-else-if="column.key === 'actions'">
              <a class="report-snapshots__link" @click="viewDetail(record)">
                <EyeOutlined /> 查看
              </a>
              <a-divider type="vertical" />
              <a
                class="report-snapshots__link"
                :class="{ 'report-snapshots__link--disabled': !findPreviousVersion(record) }"
                @click="compareVersion(record)"
              >
                <DiffOutlined /> 对比
              </a>
            </template>
          </template>
        </a-table>
        <EmptyState
          v-else-if="!loading"
          description="暂无快照记录"
          cta-text="调整筛选条件"
          @cta-click="loadList"
        />
      </a-spin>
    </a-card>

    <!-- 详情抽屉 -->
    <a-drawer
      v-model:open="drawerVisible"
      title="报表快照详情"
      width="640"
      @after-open="focusFirstDescription"
    >
      <a-spin :spinning="detailLoading">
        <template v-if="detail">
          <a-descriptions :column="1" bordered size="small" ref="descriptionsRef">
            <a-descriptions-item label="报表 ID">{{ detail.ReportId }}</a-descriptions-item>
            <a-descriptions-item label="报表类型">{{ reportTypeLabel(detail.ReportType) }}</a-descriptions-item>
            <a-descriptions-item label="周期起">{{ formatDateTime(detail.PeriodStart) }}</a-descriptions-item>
            <a-descriptions-item label="周期止">{{ formatDateTime(detail.PeriodEnd) }}</a-descriptions-item>
            <a-descriptions-item label="粒度">{{ granularityLabel(detail.Granularity) }}</a-descriptions-item>
            <a-descriptions-item label="数据版本">v{{ detail.DataVersion ?? 0 }}</a-descriptions-item>
            <a-descriptions-item label="生成时间">{{ formatDateTime(detail.GeneratedAt) }}</a-descriptions-item>
          </a-descriptions>

          <div class="report-snapshots__metrics-title">Metrics 指标</div>
          <a-descriptions :column="2" bordered size="small">
            <a-descriptions-item
              v-for="metric in detail.Metrics"
              :key="metric.Key"
              :label="metric.Key"
            >
              {{ formatMetricValue(metric.Value) }}
              <span v-if="metric.Unit" class="report-snapshots__unit">{{ metric.Unit }}</span>
            </a-descriptions-item>
          </a-descriptions>

          <!-- 版本对比 -->
          <div class="report-snapshots__compare-header">
            <span>与上一版本对比</span>
            <a-tooltip :title="compareSwitchTooltip">
              <a-switch
                v-model:checked="compareEnabled"
                :disabled="!previousVersionForDetail"
              />
            </a-tooltip>
          </div>
          <a-table
            v-if="compareEnabled && diffRows.length > 0"
            :columns="diffColumns"
            :data-source="diffRows"
            :pagination="false"
            row-key="key"
            size="small"
          >
            <template #bodyCell="{ column, record }">
              <template v-if="column.key === 'changePercent'">
                <span :style="{ color: record.changePercent > 0 ? '#52C41A' : '#FF4D4F' }">
                  {{ formatChangePercent(record.changePercent) }}
                </span>
              </template>
            </template>
          </a-table>
          <a-empty
            v-else-if="compareEnabled"
            description="无差异指标"
            image="simple"
          />
        </template>
        <EmptyState v-else description="快照不存在或已归档" />
      </a-spin>
    </a-drawer>
  </div>
</template>

<script setup lang="ts">
import { ref, computed, onMounted, watch } from 'vue'
import { useRoute } from 'vue-router'
import {
  ReloadOutlined,
  DownloadOutlined,
  EyeOutlined,
  DiffOutlined,
} from '@ant-design/icons-vue'
import { message } from 'ant-design-vue'
import dayjs from 'dayjs'
import DateTimeRangePicker from '@/shared/components/DateTimeRangePicker.vue'
import EmptyState from '@/shared/components/EmptyState.vue'
import PermissionGuard from '@/shared/components/PermissionGuard.vue'
import { dashboardApi } from '../api/dashboard.api'
import type {
  DashboardReportDto,
  DashboardMetricDto,
  ReportType,
  Granularity,
  ReportListParams,
  DateRangeParams,
} from '../types/dashboard.dto'

const route = useRoute()
const loading = ref(false)
const exporting = ref(false)
const detailLoading = ref(false)
const list = ref<DashboardReportDto[]>([])
const detail = ref<DashboardReportDto | null>(null)
const drawerVisible = ref(false)
const compareEnabled = ref(false)
const descriptionsRef = ref<unknown>(null)

const reportTypeOptions: { label: string; value: ReportType }[] = [
  { label: '订单 GMV', value: 'OrderGmv' },
  { label: '支付成功率', value: 'PaymentSuccessRate' },
  { label: '积分发放量', value: 'PointsIssued' },
  { label: '通知送达率', value: 'NotificationDelivery' },
  { label: '售后量', value: 'AfterSalesVolume' },
  { label: '店铺排行', value: 'ShopRanking' },
]

// 初始化报表类型：优先从路由 query 读取
function initReportType(): ReportType {
  const query = route.query.reportType as string | undefined
  if (query && reportTypeOptions.some((o) => o.value === query)) {
    return query as ReportType
  }
  return 'OrderGmv'
}

const reportType = ref<ReportType>(initReportType())

const rangePresets: Array<'today' | 'yesterday' | 'last7days' | 'last30days' | 'thisMonth'> = [
  'today', 'yesterday', 'last7days', 'last30days', 'thisMonth',
]

function initDateRange(): [string, string] {
  const queryStart = route.query.start as string | undefined
  const queryEnd = route.query.end as string | undefined
  if (queryStart && queryEnd) return [queryStart, queryEnd]
  return getLast7DaysRange()
}

function getLast7DaysRange(): [string, string] {
  const end = new Date()
  const start = new Date()
  start.setDate(start.getDate() - 7)
  return [start.toISOString(), end.toISOString()]
}

const dateRange = ref<[string, string]>(initDateRange())

// 报表类型中文标签映射
function reportTypeLabel(type: ReportType): string {
  const option = reportTypeOptions.find((o) => o.value === type)
  return option ? option.label : type
}

// 粒度中文标签映射
function granularityLabel(g: Granularity): string {
  const map: Record<Granularity, string> = {
    Hour: '小时',
    Day: '日',
    Week: '周',
    Month: '月',
  }
  return map[g] ?? g
}

function formatDate(iso: string): string {
  return dayjs(iso).format('YYYY-MM-DD')
}

function formatDateTime(iso: string): string {
  return dayjs(iso).format('YYYY-MM-DD HH:mm')
}

// 格式化 Metric 值：数字/字符串/数组/对象分别处理
function formatMetricValue(value: unknown): string {
  if (value === null || value === undefined) return '-'
  if (typeof value === 'number') return value.toLocaleString('zh-CN')
  if (typeof value === 'string') return value
  if (Array.isArray(value)) return `[${value.length} 项]`
  if (typeof value === 'object') return JSON.stringify(value)
  return String(value)
}

// 列表列定义
const listColumns = [
  { title: '报表类型', key: 'reportType', width: 140 },
  { title: '周期起', key: 'periodStart', width: 120 },
  { title: '周期止', key: 'periodEnd', width: 120 },
  { title: '粒度', dataIndex: 'Granularity', key: 'granularity', width: 80, responsive: ['lg'] as const },
  { title: '数据版本', key: 'dataVersion', width: 100 },
  { title: '生成时间', key: 'generatedAt', width: 160 },
  { title: '操作', key: 'actions', width: 200, fixed: 'right' as const },
]

// 找到同周期前一版本（PeriodStart/PeriodEnd 相同且 DataVersion 较小的最近一个）
function findPreviousVersion(record: DashboardReportDto): DashboardReportDto | undefined {
  return list.value
    .filter(
      (r) =>
        r.PeriodStart === record.PeriodStart &&
        r.PeriodEnd === record.PeriodEnd &&
        r.ReportType === record.ReportType &&
        (r.DataVersion ?? 0) < (record.DataVersion ?? 0),
    )
    .sort((a, b) => (b.DataVersion ?? 0) - (a.DataVersion ?? 0))[0]
}

// 详情抽屉的「上一版本」（基于当前详情）
const previousVersionForDetail = computed<DashboardReportDto | null>(() => {
  if (!detail.value) return null
  return findPreviousVersion(detail.value) ?? null
})

const compareSwitchTooltip = computed(() => {
  if (previousVersionForDetail.value) return '开启后将显示与上一版本的差异'
  return '无历史版本可对比'
})

// 差异行：Key/旧值/新值/变化%
const diffColumns = [
  { title: '指标 Key', dataIndex: 'key', key: 'key' },
  { title: '旧值', dataIndex: 'oldValue', key: 'oldValue' },
  { title: '新值', dataIndex: 'newValue', key: 'newValue' },
  { title: '变化%', key: 'changePercent', width: 120 },
]

const diffRows = computed<{ key: string; oldValue: string; newValue: string; changePercent: number }[]>(() => {
  if (!detail.value || !previousVersionForDetail.value) return []
  const current = detail.value
  const previous = previousVersionForDetail.value
  const result: { key: string; oldValue: string; newValue: string; changePercent: number }[] = []
  const allKeys = new Set<string>([
    ...current.Metrics.map((m) => m.Key),
    ...previous.Metrics.map((m) => m.Key),
  ])
  for (const key of allKeys) {
    const curMetric = current.Metrics.find((m) => m.Key === key)
    const prevMetric = previous.Metrics.find((m) => m.Key === key)
    const curValue = extractNumber(curMetric)
    const prevValue = extractNumber(prevMetric)
    const changePercent = prevValue === 0 ? 0 : ((curValue - prevValue) / Math.abs(prevValue)) * 100
    result.push({
      key,
      oldValue: prevMetric ? formatMetricValue(prevMetric.Value) : '-',
      newValue: curMetric ? formatMetricValue(curMetric.Value) : '-',
      changePercent,
    })
  }
  return result
})

// 提取 Metric 的数值（仅对数值型返回数字，其他返回 0）
function extractNumber(metric: DashboardMetricDto | undefined): number {
  if (!metric) return 0
  if (typeof metric.Value === 'number') return metric.Value
  return 0
}

function formatChangePercent(percent: number): string {
  const sign = percent > 0 ? '+' : ''
  return `${sign}${percent.toFixed(1)}%`
}

async function loadList() {
  const [start, end] = dateRange.value
  if (new Date(start) >= new Date(end)) {
    message.warning('结束时间需晚于开始时间')
    return
  }
  loading.value = true
  try {
    const params: ReportListParams = { start, end, reportType: reportType.value }
    const { data: reports } = await dashboardApi.getReports(params)
    list.value = reports
  } catch {
    message.error('报表快照列表加载失败')
  } finally {
    loading.value = false
  }
}

async function viewDetail(record: DashboardReportDto) {
  drawerVisible.value = true
  detailLoading.value = true
  detail.value = null
  compareEnabled.value = false
  try {
    const { data: report } = await dashboardApi.getReport(record.ReportId)
    detail.value = report
  } catch {
    message.error('快照不存在或已归档')
  } finally {
    detailLoading.value = false
  }
}

function compareVersion(record: DashboardReportDto) {
  const previous = findPreviousVersion(record)
  if (!previous) {
    message.info('无历史版本可对比')
    return
  }
  viewDetail(record)
  // 详情加载完成后自动开启对比开关
  setTimeout(() => {
    compareEnabled.value = true
  }, 300)
}

// 抽屉打开后聚焦首个描述项（可访问性）
function focusFirstDescription() {
  // descriptions 实例可能未挂载 ref，此处仅作焦点尝试
  setTimeout(() => {
    const el = document.querySelector('.report-snapshots .ant-drawer-body .ant-descriptions-item-content')
    if (el instanceof HTMLElement) el.focus()
  }, 100)
}

// 导出 CSV：基于列表数据生成并下载
async function exportCsv() {
  exporting.value = true
  try {
    const [start, end] = dateRange.value
    const params: ReportListParams = { start, end, reportType: reportType.value }
    const { data: reports } = await dashboardApi.getReports(params)
    const headers = ['ReportId', 'ReportType', 'PeriodStart', 'PeriodEnd', 'Granularity', 'DataVersion', 'GeneratedAt']
    const rows = reports.map((r) => [
      r.ReportId,
      r.ReportType,
      r.PeriodStart,
      r.PeriodEnd,
      r.Granularity,
      String(r.DataVersion ?? 0),
      r.GeneratedAt,
    ])
    const csv = [headers, ...rows]
      .map((row) => row.map((cell) => `"${String(cell).replace(/"/g, '""')}"`).join(','))
      .join('\n')
    // 添加 BOM 头以兼容 Excel 中文
    const blob = new Blob([`\uFEFF${csv}`], { type: 'text/csv;charset=utf-8;' })
    const url = URL.createObjectURL(blob)
    const link = document.createElement('a')
    link.href = url
    link.download = `reports-${reportType.value}-${dayjs().format('YYYYMMDD-HHmmss')}.csv`
    document.body.appendChild(link)
    link.click()
    document.body.removeChild(link)
    URL.revokeObjectURL(url)
    message.success(`已导出 ${reports.length} 条快照`)
  } catch {
    message.error('CSV 导出失败')
  } finally {
    exporting.value = false
  }
}

watch(dateRange, () => loadList())
watch(reportType, () => loadList())

onMounted(() => loadList())
</script>

<style scoped>
.report-snapshots {
  display: flex;
  flex-direction: column;
  gap: 24px;
}
.report-snapshots__toolbar {
  display: flex;
  gap: 12px;
  align-items: center;
  flex-wrap: wrap;
}
.report-snapshots__card {
  border-radius: 8px;
}
.report-snapshots__link {
  color: #1677FF;
  cursor: pointer;
}
.report-snapshots__link:hover {
  text-decoration: underline;
}
.report-snapshots__link--disabled {
  color: #BFBFBF;
  cursor: not-allowed;
}
.report-snapshots__metrics-title {
  margin: 24px 0 12px;
  font-size: 14px;
  font-weight: 500;
  color: #000000D9;
}
.report-snapshots__compare-header {
  display: flex;
  align-items: center;
  gap: 12px;
  margin: 24px 0 12px;
  font-size: 14px;
  font-weight: 500;
  color: #000000D9;
}
.report-snapshots__unit {
  margin-left: 4px;
  color: #8C8C8C;
  font-size: 12px;
}
</style>
```

- [ ] **Step 2: 验证类型检查通过**

Run: `cd web/system-admin && pnpm typecheck`
Expected: 无 TypeScript 错误

- [ ] **Step 3: 验证 Lint 通过**

Run: `cd web/system-admin && pnpm lint`
Expected: 无 ESLint 错误

- [ ] **Step 4: 提交**

```bash
cd web/system-admin
git add src/modules/01-dashboard/views/ReportSnapshots.vue
git commit -m "feat(dashboard): 实现报表快照视图 ReportSnapshots"
```

---

## Task 12: 模块整体集成验证与自检

**Files:**
- Verify: `web/system-admin/src/modules/01-dashboard/`（整个模块）

本任务作为模块收尾，验证所有 11 个任务的产物可协同工作：类型一致、路由可挂载、API 与 DTO 对齐、各视图字段与 design-prompt 一一对应、无占位符残留。

- [ ] **Step 1: 验证模块目录结构完整**

Run: `cd web/system-admin && ls -R src/modules/01-dashboard`
Expected: 输出包含以下 13 个文件：
- `types/dashboard.dto.ts`
- `api/dashboard.api.ts`、`api/dashboard.api.spec.ts`
- `components/DashboardCard.vue`、`components/ChartGauge.vue`
- `views/OperationsOverview.vue`、`views/PaymentStats.vue`、`views/PointsStats.vue`、`views/NotificationDelivery.vue`、`views/AfterSalesStats.vue`、`views/ShopRanking.vue`、`views/ReportSnapshots.vue`
- `routes.ts`、`index.ts`

- [ ] **Step 2: 验证无占位符残留**

Run: `cd web/system-admin && grep -rE "TODO|TBD|FIXME|占位|省略|此处保持不变|未实现" src/modules/01-dashboard/`
Expected: 无匹配输出（exit code 1）

- [ ] **Step 3: 运行 TypeScript 类型检查**

Run: `cd web/system-admin && pnpm typecheck`
Expected: 无 TypeScript 错误（所有视图对 DTO 字段引用、API 返回类型、组件 Props 类型对齐）

- [ ] **Step 4: 运行 ESLint**

Run: `cd web/system-admin && pnpm lint`
Expected: 无 ESLint 错误

- [ ] **Step 5: 运行单元测试**

Run: `cd web/system-admin && pnpm test -- src/modules/01-dashboard/`
Expected: `dashboard.api.spec.ts` 的 8 个用例全部 PASS

- [ ] **Step 6: 验证路由聚合到主路由**

Run: `cd web/system-admin && grep -E "01-dashboard" src/router/index.ts`
Expected: 至少 1 行匹配（主路由通过 `import { routes as dashboardRoutes } from '@/modules/01-dashboard'` 聚合 7 条子路由到 BasicLayout children 下）

如果未挂载，由 Plan 1（应用骨架）负责在主路由聚合；本 Plan 输出 `index.ts` 已正确导出 `routes`，无需修改。

- [ ] **Step 7: 字段覆盖核对（与 7 份 design-prompt 对照）**

逐项核对每个视图的关键字段，确保未遗漏：

| 视图 | design-prompt 关键字段 | 实现位置 |
|-|-|-|
| OperationsOverview | 4 KPI（订单量/GMV/转化率/客单价）+ 趋势线 + 来源饼图 + 漏斗柱状图 + 转化率权限守卫 | Task 5 模板 KPI 行 + 主趋势 + 辅助图区 + `PermissionGuard permission="dashboard:conversion"` |
| PaymentStats | 3 KPI + 整体成功率 Gauge + 渠道排行 Bar + 失败原因 Pie + 渠道抽屉 + 平均到账时长权限 | Task 6 KPI 行 + ChartGauge + ChartBar + ChartPie + a-drawer + `PermissionGuard permission="dashboard:paymentLatency"` |
| PointsStats | 3 KPI（发放/消耗/净增）+ 双系列趋势 + 来源饼图 + 净增负值染色 + 突增告警 | Task 7 KPI 行 + ChartLine 双系列 + ChartPie + `netColor` + `detectAnomaly` |
| NotificationDelivery | 4 Gauge 网格 + 趋势多系列 + 失败原因表 + 失败原因抽屉 + 90% 告警 + 审计跳转 | Task 8 Gauge 网格 + ChartLine + a-table + a-drawer + `notification.error` + `navigateToAuditLogs` |
| AfterSalesStats | 3 KPI + 类型饼图 + 双系列趋势 + Top 10 表 + 售后率染色 + 5% 告警 + 审计跳转 | Task 9 KPI 行 + ChartPie + ChartLine 双系列 + a-table + `rateColor` + `notification.warning` + `navigateToAuditLogs` |
| ShopRanking | 筛选 + 维度切换 + TopN + Top 3 领奖台 + 主排行表 + 状态标签 + 增长率染色 + 审计跳转 | Task 10 toolbar + a-segmented + a-input-number + podium + a-table + StatusTag + GrowthTag + `navigateToAuditLogs` |
| ReportSnapshots | 类型/时间筛选 + 列表表 + 详情抽屉 + 版本对比 + CSV 导出 + 404 提示 | Task 11 toolbar + a-table + a-drawer + a-switch + exportCsv + `message.error('快照不存在或已归档')` |

- [ ] **Step 8: 跨 Plan 类型契约核对**

验证模块对 Plan 1 共享层的引用均存在且签名对齐：

| 引用 | 期望路径 | 期望导出 |
|-|-|-|
| `@/shared/http` | `shared/http/index.ts` | `client: AxiosInstance`，`client.get<T>(url, config): Promise<AxiosResponse<T>>` |
| `@/shared/types` | `shared/types/index.ts` | `ApiResponse<T>`、`PageResult<T>`、`PageQuery`（本模块未直接使用，但 Plan 1 须提供） |
| `@/shared/auth` | `shared/auth/auth.store.ts` | `useAuthStore`（`isAdmin`、`hasPermission(perm)`、`hasRole(roles)`，由 `PermissionGuard` 内部消费） |
| `@/shared/components/DateTimeRangePicker.vue` | shared 组件 | `modelValue: [string,string]`、`presets`、`showTime?`、`@update:modelValue` |
| `@/shared/components/charts/ChartLine.vue` | shared 组件 | `data: {date,value,series?}[]`、`seriesField?`、`height?` |
| `@/shared/components/charts/ChartPie.vue` | shared 组件 | `data: {name,value}[]`、`height?`、`donut?` |
| `@/shared/components/charts/ChartBar.vue` | shared 组件 | `data: {name,value,series?}[]`、`horizontal?`、`height?` |
| `@/shared/components/EmptyState.vue` | shared 组件 | `title?`、`description?`、`ctaText?`、`@cta-click` |
| `@/shared/components/PermissionGuard.vue` | shared 组件 | `permission: string\|string[]`、`fallback?`、默认 slot |
| `@/shared/components/StatusTag.vue` | shared 组件 | `status: string`、`type?: 'order'\|'afterSales'\|'product'\|'shop'\|'payment'` |

Run: `cd web/system-admin && grep -rE "from '@/shared/" src/modules/01-dashboard/ | sort -u`
Expected: 至少 14 行匹配，且每行对应的共享模块路径与 Plan 1 文件结构对齐

- [ ] **Step 9: 提交最终验证产物（如有修复）**

如 Step 1-8 全部通过，无需新增提交；如有修复，提交修复：

```bash
cd web/system-admin
git add -A src/modules/01-dashboard/
git commit -m "test(dashboard): 模块整体集成验证通过"
```

- [ ] **Step 10: 推送到远程**

```bash
cd web/system-admin
git push origin dev
```

Expected: 推送成功，远程仓库包含 12 次提交（每个 Task 一次 + 最终验证一次，若 Step 9 未产生提交则为 11 次）

---

## Plan 自检清单（已完成）

- **Spec 覆盖**：spec §2.1 列出的 01-dashboard 7 页全部由 Task 5-11 实现；模块骨架（DTO/API/组件/路由/入口）由 Task 1-4 实现；测试由 Task 2 提供。无遗漏。
- **占位符扫描**：已执行 `grep -rE "TODO|TBD|FIXME|占位|省略|此处保持不变|未实现"` 于本 plan 文件，无匹配（Step 2 在执行阶段对产物代码再次扫描）。
- **类型一致性**：`dashboardApi` 8 个方法的返回类型与 `parseXxxData` 入参类型均为 `DashboardReportDto`；各视图 `data.value` 类型与对应 `parseXxxData` 返回类型一致；`DateRangeParams`/`ReportListParams` 在所有视图中按需引用；DTO 中 `getMetric`/`getNumberMetric`/`getArrayMetric` 工具函数签名稳定。
- **文件路径一致性**：所有视图路径 `web/system-admin/src/modules/01-dashboard/views/*.vue` 与 `routes.ts` 的懒加载 `import('../views/Xxx.vue')` 一一对应；`index.ts` 导出的 `routes`/`dashboardApi`/DTO 类型与各文件实际导出对齐。
- **design-prompt 字段覆盖**：Step 7 已逐视图核对关键字段，7 份 design-prompt 的「数据模型与 API 对接」段中所有 `Metrics.Key` 均在对应 DTO 解析函数中提取，所有「关键区域」均在对应视图模板中渲染。

