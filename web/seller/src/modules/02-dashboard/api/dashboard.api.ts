import { client } from '@/shared/http'
import type {
  SellerDashboardDto,
  SalesTrendItemDto,
  DateRangeParams,
  LowStockItemDto,
} from '../types/dashboard.dto'

export const dashboardApi = {
  getDashboard: () =>
    client.get<SellerDashboardDto>('/seller/dashboard').then((r) => r.data),

  getSalesTrend: (params: DateRangeParams) =>
    client.get<SalesTrendItemDto[]>('/seller/sales-trend', { params }).then((r) => r.data),

  getLowStock: (threshold: number) =>
    client
      .get<LowStockItemDto[]>('/seller/dashboard/low-stock', {
        params: { threshold },
      })
      .then((r) => r.data),
}
