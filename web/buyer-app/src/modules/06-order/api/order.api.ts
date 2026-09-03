import { client } from '@/shared/http'
import type { AddressDto } from '@/modules/13-profile/types/profile.dto'
import type {
  BuyNowRequestDto,
  CreateOrderRequestDto,
  LogisticsTraceDto,
  OrderDto,
  OrderPreviewRequestDto,
  OrderPreviewResultDto,
  OrderQueryParams,
} from '../types/order.dto'
import type { PagedResult } from '@/shared/types'

/**
 * 订单 API（Order BC 买家端）
 *
 * - GET  /orders                  订单列表
 * - GET  /orders/{id}             订单详情
 * - POST /orders                  从购物车创建订单
 * - POST /orders/buy-now          立即购买下单
 * - POST /orders/preview          下单前预览
 * - POST /orders/{id}/cancel      取消订单
 * - POST /orders/{id}/confirm     确认收货
 * - GET  /orders/{id}/logistics   物流轨迹
 * - GET  /addresses               下单可用地址（别名端点）
 *
 * 响应拦截器已解包 ApiResponse.data，调用方拿到的就是业务负载。
 */
export const orderApi = {
  /** 订单列表（状态筛选 + 分页） */
  list(params: OrderQueryParams): Promise<PagedResult<OrderDto>> {
    return client.get<PagedResult<OrderDto>>('/orders', { params }).then((r) => r.data)
  },

  /** 订单详情 */
  getDetail(id: string): Promise<OrderDto> {
    return client.get<OrderDto>(`/orders/${id}`).then((r) => r.data)
  },

  /** 从购物车创建订单 */
  create(body: CreateOrderRequestDto): Promise<OrderDto> {
    return client.post<OrderDto>('/orders', body).then((r) => r.data)
  },

  /** 立即购买下单 */
  buyNow(body: BuyNowRequestDto): Promise<OrderDto> {
    return client.post<OrderDto>('/orders/buy-now', body).then((r) => r.data)
  },

  /** 下单前预览 */
  preview(body: OrderPreviewRequestDto): Promise<OrderPreviewResultDto> {
    return client.post<OrderPreviewResultDto>('/orders/preview', body).then((r) => r.data)
  },

  /** 取消订单 */
  cancel(id: string): Promise<null> {
    return client.post<null>(`/orders/${id}/cancel`).then((r) => r.data)
  },

  /** 确认收货 */
  confirm(id: string): Promise<OrderDto> {
    return client.post<OrderDto>(`/orders/${id}/confirm`).then((r) => r.data)
  },

  /** 物流轨迹 */
  getLogistics(id: string): Promise<LogisticsTraceDto> {
    return client.get<LogisticsTraceDto>(`/orders/${id}/logistics`).then((r) => r.data)
  },

  /** 下单可用地址列表（/users/me/addresses 别名端点，与 13-profile 的 addressApi 等价） */
  getAddresses(): Promise<AddressDto[]> {
    return client.get<AddressDto[]>('/addresses').then((r) => r.data)
  },
}
