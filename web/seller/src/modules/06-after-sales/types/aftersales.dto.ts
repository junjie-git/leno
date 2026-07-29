/**
 * 售后状态
 */
export type AfterSalesStatus =
  | 'Pending'
  | 'Approved'
  | 'Rejected'
  | 'ReturnInProgress'
  | 'Refunded'
  | 'Closed'

/**
 * 售后类型
 */
export type AfterSalesType =
  | 'RefundOnly'      // 仅退款
  | 'ReturnRefund'    // 退货退款
  | 'Exchange'        // 换货

/**
 * 售后列表项 DTO
 */
export interface AfterSalesListItemDto {
  id: string
  afterSalesNo: string
  orderId: string
  orderNo: string
  buyerName: string
  buyerPhone: string
  type: AfterSalesType
  status: AfterSalesStatus
  amount: number
  currency: string
  reason: string
  description?: string
  productName: string
  productId: string
  skuId: string
  skuName: string
  quantity: number
  createdAt: string
  updatedAt: string
}

/**
 * 售后详情 DTO
 */
export interface AfterSalesDetailDto extends AfterSalesListItemDto {
  images: string[]
  rejectReason?: string
  logisticsCompany?: string
  logisticsNo?: string
  returnLogisticsCompany?: string
  returnLogisticsNo?: string
  refundTime?: string
  version: number
}

/**
 * 拒绝售后 DTO
 */
export interface RejectAfterSalesDto {
  reason: string
  version: number
}

/**
 * 列表查询参数
 */
export interface ListAfterSalesParams {
  status?: AfterSalesStatus
  afterSalesNo?: string
  orderNo?: string
  buyerName?: string
  type?: AfterSalesType
  startDate?: string
  endDate?: string
  page?: number    // 从 1 起
  pageSize?: number
}
