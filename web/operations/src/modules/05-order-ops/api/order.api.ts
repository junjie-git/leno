import type { AxiosResponse } from 'axios'
import { client, withIdempotency } from '@/shared/http'
import type { PageResult } from '@/shared/types'
import type {
  ForceCancelOrderDto,
  OrderDto,
  OrderQueryParams,
  OrderStatus,
} from '../types/order.dto'

/**
 * 订单管理 API
 *
 * 与 Order 域 AdminOrdersController 对接（baseURL 已含 /api）。
 * 所有方法返回 AxiosResponse，调用方解构 .data 拿业务负载
 * （响应拦截器已完成 ApiResponse 信封解包）。
 */
export const orderApi = {
  /**
   * 全平台订单分页查询（走 ES 读模型）
   *
   * 支持订单号 / 买家 / 卖家 / 状态 / 下单时间范围与分页组合筛选。
   */
  list(params: OrderQueryParams): Promise<AxiosResponse<PageResult<OrderDto>>> {
    return client.get<PageResult<OrderDto>>('/admin/orders', { params })
  },

  /**
   * 运营强制取消订单（Admin）
   *
   * 仅待支付 / 已支付 / 已发货态可取消；已支付订单将触发自动退款，
   * 已发货订单需确认库存回写影响。
   */
  forceCancel(id: string, body: ForceCancelOrderDto): Promise<AxiosResponse<void>> {
    return client.post<void>(`/admin/orders/${id}/force-cancel`, body, withIdempotency())
  },
}

/**
 * 按状态统计订单数量（统计概览卡数据源）
 *
 * md 未定义独立统计端点，基于列表端点按状态各取 pageSize=1 读取 total 聚合。
 * 单个状态查询失败时该状态计数记为 0，不阻塞其它状态。
 */
export async function countOrdersByStatus(
  statuses: OrderStatus[],
): Promise<Record<OrderStatus, number>> {
  const results = await Promise.all(
    statuses.map(async (status) => {
      try {
        const { data } = await orderApi.list({ status, page: 1, pageSize: 1 })
        return [status, data.total] as const
      } catch {
        return [status, 0] as const
      }
    }),
  )
  return Object.fromEntries(results) as Record<OrderStatus, number>
}
