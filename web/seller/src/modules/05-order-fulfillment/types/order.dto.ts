/**
 * 订单状态
 */
export type OrderStatus =
  | 'PendingShipment'
  | 'Shipped'
  | 'Delivered'
  | 'Completed'
  | 'Cancelled'
  | 'Refunded'

/**
 * 订单列表项 DTO
 */
export interface OrderListItemDto {
  id: string
  orderNo: string
  buyerName: string
  buyerPhone: string
  totalAmount: number
  currency: string
  status: OrderStatus
  itemCount: number
  receiverName: string
  receiverPhone: string
  receiverAddress: string
  createdAt: string
  paidAt?: string
  shippedAt?: string
  completedAt?: string
}

/**
 * 订单商品项 DTO
 */
export interface OrderItemDto {
  id: string
  productId: string
  productName: string
  skuId: string
  skuCode: string
  skuName: string
  coverImage?: string
  price: number
  quantity: number
  subtotal: number
}

/**
 * 订单详情 DTO
 */
export interface OrderDetailDto extends OrderListItemDto {
  items: OrderItemDto[]
  remark?: string
  logisticsCompany?: string
  logisticsNo?: string
  version: number
}

/**
 * 发货 DTO
 */
export interface ShipOrderDto {
  logisticsCompany: string
  logisticsNo: string
  version: number
}

/**
 * 物流轨迹节点 DTO
 */
export interface LogisticsTraceNodeDto {
  time: string
  location?: string
  description: string
  status?: string
}

/**
 * 物流轨迹 DTO
 */
export interface LogisticsTraceDto {
  orderId: string
  orderNo: string
  logisticsCompany: string
  logisticsNo: string
  currentNode?: LogisticsTraceNodeDto
  trace: LogisticsTraceNodeDto[]
}

/**
 * 列表查询参数
 */
export interface ListOrdersParams {
  status?: OrderStatus
  orderNo?: string
  buyerName?: string
  startDate?: string
  endDate?: string
  page?: number    // 后端从 1 起
  pageSize?: number
}
