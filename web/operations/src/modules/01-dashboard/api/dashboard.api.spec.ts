import { describe, expect, it, beforeEach, afterEach } from 'vitest'
import MockAdapter from 'axios-mock-adapter'
import { client } from '@/shared/http'
import { dashboardApi } from './dashboard.api'
import type {
  DashboardReportDto,
  DeliveryStatisticsListDto,
} from '../types/dashboard.dto'

/**
 * 数据看板 API 单测（axios-mock-adapter 挂载到真实 client 实例）
 *
 * 覆盖点：
 * - 7 个 GET 端点的 URL 与查询参数（start/end，statistics 为 from/to）
 * - 成功分支解包 ApiResponse.data（信封 code=200）
 * - 业务错误分支（HTTP 200 但 code !== 200）抛出 BusinessError，错误 message 透传
 */
describe('01-dashboard dashboardApi', () => {
  let mock: MockAdapter

  const dateParams = { start: '2026-07-20T00:00:00Z', end: '2026-07-27T00:00:00Z' }
  const statisticsParams = { from: '2026-07-20T00:00:00Z', to: '2026-07-27T00:00:00Z' }

  function makeReport(reportType: DashboardReportDto['ReportType']): DashboardReportDto {
    return {
      ReportId: 'r-001',
      ReportType: reportType,
      PeriodStart: dateParams.start,
      PeriodEnd: dateParams.end,
      Granularity: 'Day',
      GeneratedAt: '2026-07-27T02:00:00Z',
      Metrics: [
        { Key: 'order_count', Value: 1286 },
        { Key: 'gmv', Value: 128560 },
      ],
    }
  }

  const statisticsDto: DeliveryStatisticsListDto = {
    items: [
      {
        channel: 'Sms',
        total_count: 84560,
        delivered_count: 83000,
        failed_count: 1560,
        delivery_rate: 98.2,
        avg_latency_ms: 1800,
      },
      {
        channel: 'Email',
        total_count: 36200,
        delivered_count: 35880,
        failed_count: 320,
        delivery_rate: 99.1,
        avg_latency_ms: 2400,
      },
    ],
  }

  beforeEach(() => {
    mock = new MockAdapter(client)
    localStorage.clear()
  })

  afterEach(() => {
    mock.restore()
  })

  it('getOverview 调用 /admin/dashboard/overview 并传递 start/end，解包 data', async () => {
    const report = makeReport('OrderGmv')
    mock
      .onGet('/admin/dashboard/overview')
      .reply(200, { code: 200, message: 'OK', data: report })

    const result = await dashboardApi.getOverview(dateParams)

    expect(result.ReportId).toBe('r-001')
    expect(result.ReportType).toBe('OrderGmv')
    expect(result.Metrics).toHaveLength(2)
    expect(mock.history.get.length).toBe(1)
    expect(mock.history.get[0].url).toBe('/admin/dashboard/overview')
    expect(mock.history.get[0].params).toEqual(dateParams)
  })

  it('getOverview 业务错误（code !== 200）抛出 BusinessError', async () => {
    mock
      .onGet('/admin/dashboard/overview')
      .reply(200, { code: 50001, message: '运营报表数据不可用', data: null })

    await expect(dashboardApi.getOverview(dateParams)).rejects.toThrowError('运营报表数据不可用')
  })

  it('getPaymentStats 调用 /admin/dashboard/payment-stats 并传递时间参数', async () => {
    const report = makeReport('PaymentSuccessRate')
    mock
      .onGet('/admin/dashboard/payment-stats')
      .reply(200, { code: 200, message: 'OK', data: report })

    const result = await dashboardApi.getPaymentStats(dateParams)

    expect(result.ReportType).toBe('PaymentSuccessRate')
    expect(mock.history.get[0].url).toBe('/admin/dashboard/payment-stats')
    expect(mock.history.get[0].params).toEqual(dateParams)
  })

  it('getPaymentStats 业务错误抛出后端 message', async () => {
    mock
      .onGet('/admin/dashboard/payment-stats')
      .reply(200, { code: 50002, message: '支付统计数据不可用', data: null })

    await expect(dashboardApi.getPaymentStats(dateParams)).rejects.toThrowError('支付统计数据不可用')
  })

  it('getPointsStats 调用 /admin/dashboard/points-stats 并传递时间参数', async () => {
    const report = makeReport('PointsIssued')
    mock
      .onGet('/admin/dashboard/points-stats')
      .reply(200, { code: 200, message: 'OK', data: report })

    const result = await dashboardApi.getPointsStats(dateParams)

    expect(result.ReportType).toBe('PointsIssued')
    expect(mock.history.get[0].url).toBe('/admin/dashboard/points-stats')
    expect(mock.history.get[0].params).toEqual(dateParams)
  })

  it('getPointsStats 业务错误抛出后端 message', async () => {
    mock
      .onGet('/admin/dashboard/points-stats')
      .reply(200, { code: 50003, message: '积分统计数据不可用', data: null })

    await expect(dashboardApi.getPointsStats(dateParams)).rejects.toThrowError('积分统计数据不可用')
  })

  it('getNotificationDelivery 调用 /admin/dashboard/notification-delivery 并传递时间参数', async () => {
    const report = makeReport('NotificationDelivery')
    mock
      .onGet('/admin/dashboard/notification-delivery')
      .reply(200, { code: 200, message: 'OK', data: report })

    const result = await dashboardApi.getNotificationDelivery(dateParams)

    expect(result.ReportType).toBe('NotificationDelivery')
    expect(mock.history.get[0].url).toBe('/admin/dashboard/notification-delivery')
    expect(mock.history.get[0].params).toEqual(dateParams)
  })

  it('getNotificationDelivery 业务错误抛出后端 message', async () => {
    mock
      .onGet('/admin/dashboard/notification-delivery')
      .reply(200, { code: 50004, message: '通知送达率数据不可用', data: null })

    await expect(dashboardApi.getNotificationDelivery(dateParams)).rejects.toThrowError('通知送达率数据不可用')
  })

  it('getNotificationStatistics 调用 /admin/notifications/statistics 并传递 from/to，解包 data', async () => {
    mock
      .onGet('/admin/notifications/statistics')
      .reply(200, { code: 200, message: 'OK', data: statisticsDto })

    const result = await dashboardApi.getNotificationStatistics(statisticsParams)

    expect(result.items).toHaveLength(2)
    expect(result.items[0].channel).toBe('Sms')
    expect(result.items[0].total_count).toBe(84560)
    expect(mock.history.get[0].url).toBe('/admin/notifications/statistics')
    expect(mock.history.get[0].params).toEqual(statisticsParams)
  })

  it('getNotificationStatistics 业务错误抛出后端 message', async () => {
    mock
      .onGet('/admin/notifications/statistics')
      .reply(200, { code: 50005, message: '通知统计明细不可用', data: null })

    await expect(dashboardApi.getNotificationStatistics(statisticsParams)).rejects.toThrowError('通知统计明细不可用')
  })

  it('getAfterSalesStats 调用 /admin/dashboard/after-sales-stats 并传递时间参数', async () => {
    const report = makeReport('AfterSalesVolume')
    mock
      .onGet('/admin/dashboard/after-sales-stats')
      .reply(200, { code: 200, message: 'OK', data: report })

    const result = await dashboardApi.getAfterSalesStats(dateParams)

    expect(result.ReportType).toBe('AfterSalesVolume')
    expect(mock.history.get[0].url).toBe('/admin/dashboard/after-sales-stats')
    expect(mock.history.get[0].params).toEqual(dateParams)
  })

  it('getAfterSalesStats 业务错误抛出后端 message', async () => {
    mock
      .onGet('/admin/dashboard/after-sales-stats')
      .reply(200, { code: 50006, message: '售后统计数据不可用', data: null })

    await expect(dashboardApi.getAfterSalesStats(dateParams)).rejects.toThrowError('售后统计数据不可用')
  })

  it('getShopRanking 调用 /admin/dashboard/shop-ranking 并传递时间参数', async () => {
    const report = makeReport('ShopRanking')
    mock
      .onGet('/admin/dashboard/shop-ranking')
      .reply(200, { code: 200, message: 'OK', data: report })

    const result = await dashboardApi.getShopRanking(dateParams)

    expect(result.ReportType).toBe('ShopRanking')
    expect(mock.history.get[0].url).toBe('/admin/dashboard/shop-ranking')
    expect(mock.history.get[0].params).toEqual(dateParams)
  })

  it('getShopRanking 业务错误抛出后端 message', async () => {
    mock
      .onGet('/admin/dashboard/shop-ranking')
      .reply(200, { code: 50007, message: '店铺排行数据不可用', data: null })

    await expect(dashboardApi.getShopRanking(dateParams)).rejects.toThrowError('店铺排行数据不可用')
  })
})
