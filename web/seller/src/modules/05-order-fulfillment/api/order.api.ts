import { client, withIdempotency } from '@/shared/http'
import type { PageResult } from '@/shared/types'
import type {
  OrderListItemDto,
  OrderDetailDto,
  ShipOrderDto,
  LogisticsTraceDto,
  ListOrdersParams,
} from '../types/order.dto'

// TODO(backend): BE-1 待 Order BC 统一 page 从 1 起（当前从 0 起，与 SellerShop/Review 不一致）
//   后端统一后，将下方 page 默认值从 0 改为 1，并移除此 TODO 与调用处的同步标注。
export const orderApi = {
  list: (params: ListOrdersParams) => {
    const { page = 0, pageSize = 20, ...rest } = params
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
