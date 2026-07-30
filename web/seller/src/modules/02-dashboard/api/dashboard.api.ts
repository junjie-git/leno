import { client } from '@/shared/http'
import type { SellerDashboardDto, SalesTrendItemDto, DateRangeParams } from '../types/dashboard.dto'

export const dashboardApi = {
  getDashboard: () =>
    client.get<SellerDashboardDto>('/seller/dashboard').then((r) => r.data),

  getSalesTrend: (params: DateRangeParams) =>
    client.get<SalesTrendItemDto[]>('/seller/sales-trend', { params }).then((r) => r.data),
}
