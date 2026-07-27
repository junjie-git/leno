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
