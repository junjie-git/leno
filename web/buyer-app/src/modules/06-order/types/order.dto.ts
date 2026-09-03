import type { CheckoutPreviewDto } from '@/modules/05-cart/types/cart.dto'

/**
 * 订单交易域 DTO（Order BC 买家端）
 *
 * 端点契约：
 * - GET  /api/orders                       订单列表（状态筛选 + 分页）
 * - GET  /api/orders/{id}                  订单详情
 * - POST /api/orders                       从购物车创建订单
 * - POST /api/orders/buy-now               立即购买下单
 * - POST /api/orders/preview               下单前预览（与 /cart/preview 同构）
 * - POST /api/orders/{id}/cancel           取消订单
 * - POST /api/orders/{id}/confirm          确认收货
 * - GET  /api/orders/{id}/logistics        物流轨迹
 * - GET  /api/addresses                    下单可用地址列表（/users/me/addresses 别名）
 * - POST /api/orders/{orderId}/reviews     提交订单评价（09-review 模块调用）
 */

/** 订单状态（与后端 OrderStatus 枚举一致） */
export type OrderStatus =
  | 'PendingPayment'
  | 'Paid'
  | 'Shipped'
  | 'Completed'
  | 'Cancelled'
  | 'Refunding'
  | 'Refunded'
  | 'AfterSales'

/** 订单条目 */
export interface OrderItemDto {
  orderLineId: string
  spuId: string
  skuId: string
  name: string
  image: string
  specs: string
  /** 成交单价（分） */
  price: number
  quantity: number
  /** 是否已评价 */
  reviewed: boolean
}

/** 订单金额明细 */
export interface OrderAmountsDto {
  goodsAmount: number
  freight: number
  couponDiscount: number
  pointsDiscount: number
  payableAmount: number
}

/** 订单地址快照 */
export interface OrderAddressDto {
  receiver: string
  phone: string
  fullAddress: string
}

/** 订单 */
export interface OrderDto {
  id: string
  orderNo: string
  status: OrderStatus
  items: OrderItemDto[]
  shopId: string
  shopName: string
  amounts: OrderAmountsDto
  address: OrderAddressDto
  createdAt: string
  /** 待支付截止时间 */
  payDeadline?: string
  paidAt?: string
  shippedAt?: string
  completedAt?: string
  cancelledAt?: string
  cancelReason?: string
  logisticsCompany?: string
  logisticsNo?: string
  remark?: string
}

/** 订单列表筛选状态（订单聚合入口） */
export type OrderListTab = 'PendingPayment' | 'Paid' | 'Shipped' | 'Completed' | 'AfterSales'

/** 订单列表请求参数 */
export interface OrderQueryParams {
  status?: OrderListTab
  page?: number
  pageSize?: number
}

/** 物流轨迹节点 */
export interface LogisticsTraceNodeDto {
  time: string
  description: string
  /** 节点状态：已揽收 / 运输中 / 派送中 / 已签收等 */
  status: string
}

/** 物流轨迹 */
export interface LogisticsTraceDto {
  logisticsCompany: string
  logisticsNo: string
  /** 按时间倒序 */
  traces: LogisticsTraceNodeDto[]
}

/** 创建订单请求（购物车结算） */
export interface CreateOrderRequestDto {
  addressId: string
  /** 使用的优惠券 */
  couponId?: string | null
  /** 是否使用积分抵扣 */
  usePoints: boolean
  remark?: string
}

/** 立即购买请求 */
export interface BuyNowRequestDto {
  skuId: string
  quantity: number
  addressId: string
  couponId?: string | null
  usePoints: boolean
  remark?: string
}

/** 下单预览请求（与创建同构，返回结算预览） */
export interface OrderPreviewRequestDto extends CreateOrderRequestDto {
  /** 预览来源：购物车勾选项 / 立即购买 */
  from?: 'cart' | 'buyNow'
  skuId?: string
  quantity?: number
}

/** 下单预览响应（复用结算预览结构） */
export type OrderPreviewResultDto = CheckoutPreviewDto
