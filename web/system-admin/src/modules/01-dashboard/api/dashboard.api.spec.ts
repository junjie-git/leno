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
