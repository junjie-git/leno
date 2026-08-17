import { client } from '@/shared/http'
import type {
  DashboardReportDto,
  DateRangeParams,
  DeliveryStatisticsListDto,
  NotificationStatisticsParams,
} from '../types/dashboard.dto'

/**
 * 数据看板 API
 *
 * 与 Dashboard 域 DashboardController / Notifications 域统计端点对接：
 * - GET /api/admin/dashboard/overview            运营总览（订单量/GMV/转化率/客单价）
 * - GET /api/admin/dashboard/payment-stats       支付统计（成功率/渠道分布/失败原因）
 * - GET /api/admin/dashboard/points-stats        积分统计（发放/消耗/净增）
 * - GET /api/admin/dashboard/notification-delivery 通知送达率（四渠道/趋势/失败原因）
 * - GET /api/admin/notifications/statistics      通知投递统计（渠道明细，from/to 参数）
 * - GET /api/admin/dashboard/after-sales-stats   售后统计（单量/退款/售后率/时效）
 * - GET /api/admin/dashboard/shop-ranking        店铺排行（后端固定返回 Top50）
 *
 * 响应拦截器已解包 ApiResponse.data，调用方拿到的就是业务负载。
 */
export const dashboardApi = {
  /** 运营总览 */
  getOverview(params: DateRangeParams): Promise<DashboardReportDto> {
    return client.get<DashboardReportDto>('/admin/dashboard/overview', { params }).then((r) => r.data)
  },

  /** 支付统计 */
  getPaymentStats(params: DateRangeParams): Promise<DashboardReportDto> {
    return client.get<DashboardReportDto>('/admin/dashboard/payment-stats', { params }).then((r) => r.data)
  },

  /** 积分统计 */
  getPointsStats(params: DateRangeParams): Promise<DashboardReportDto> {
    return client.get<DashboardReportDto>('/admin/dashboard/points-stats', { params }).then((r) => r.data)
  },

  /** 通知送达率 */
  getNotificationDelivery(params: DateRangeParams): Promise<DashboardReportDto> {
    return client.get<DashboardReportDto>('/admin/dashboard/notification-delivery', { params }).then((r) => r.data)
  },

  /** 通知投递统计（渠道明细，时间参数为 from/to） */
  getNotificationStatistics(params: NotificationStatisticsParams): Promise<DeliveryStatisticsListDto> {
    return client.get<DeliveryStatisticsListDto>('/admin/notifications/statistics', { params }).then((r) => r.data)
  },

  /** 售后统计 */
  getAfterSalesStats(params: DateRangeParams): Promise<DashboardReportDto> {
    return client.get<DashboardReportDto>('/admin/dashboard/after-sales-stats', { params }).then((r) => r.data)
  },

  /** 店铺排行（后端返回固定 Top50，TopN 由前端切片） */
  getShopRanking(params: DateRangeParams): Promise<DashboardReportDto> {
    return client.get<DashboardReportDto>('/admin/dashboard/shop-ranking', { params }).then((r) => r.data)
  },
}
