/**
 * 工作台概览 DTO
 */
export interface SellerDashboardDto {
  shopId: string
  shopName: string
  status: string
  productCount: number
  totalOrders: number
  pendingOrders: number
  completedOrders: number
  totalRevenue: number
  todayOrderCount: number
  todaySalesAmount: number
  todaySalesCurrency: string
  todayAvgRating: number
  todayRatingCount: number
  todayRefundCount: number
}

/**
 * 销售趋势条目 DTO
 */
export interface SalesTrendItemDto {
  date: string
  orderCount: number
  salesAmount: number
}

/**
 * 库存预警条目 DTO（后端缺失，mock 兜底）
 */
export interface LowStockItemDto {
  productId: string
  productName: string
  skuId: string
  skuName: string
  stock: number
  threshold: number
}

/**
 * 日期范围参数
 */
export interface DateRangeParams {
  from: string
  to: string
}
