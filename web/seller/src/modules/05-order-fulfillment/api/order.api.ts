import { client, withIdempotency } from '@/shared/http'
import type { PageResult } from '@/shared/types'
import type {
  OrderListItemDto,
  OrderDetailDto,
  ShipOrderDto,
  LogisticsTraceDto,
  ListOrdersParams,
} from '../types/order.dto'

export const orderApi = {
  list: (params: ListOrdersParams) => {
    const { page = 1, pageSize = 20, ...rest } = params
    return client.get<PageResult<OrderListItemDto>>('/seller/orders', {
      params: { ...rest, page, pageSize },
    })
  },

  get: (id: string) => client.get<OrderDetailDto>(`/seller/orders/${id}`),

  ship: (id: string, body: ShipOrderDto) =>
    client.post<OrderDetailDto>(`/seller/orders/${id}/ship`, body, withIdempotency()),

  getLogisticsTrace: (orderId: string) =>
    client.get<LogisticsTraceDto>(`/orders/${orderId}/logistics-trace`),
}
