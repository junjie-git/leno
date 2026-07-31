/* eslint-disable @typescript-eslint/no-explicit-any */
import { describe, expect, it, vi, beforeEach } from 'vitest'
import { orderApi } from './order.api'
import { client, withIdempotency } from '@/shared/http'
import type { ShipOrderDto } from '../types/order.dto'

/**
 * 由于 client 响应拦截器已自动 unwrap `.data`，mock 时返回业务对象本身即可。
 */
vi.mock('@/shared/http', () => ({
  client: { get: vi.fn(), post: vi.fn() },
  withIdempotency: vi.fn(() => ({ headers: { 'Idempotency-Key': 'mock-key' } })),
}))

describe('orderApi', () => {
  beforeEach(() => {
    vi.clearAllMocks()
    vi.mocked(withIdempotency).mockReturnValue({
      headers: { 'Idempotency-Key': 'mock-key' },
    })
  })

  it('list 调用 GET /seller/orders 并透传筛选参数', async () => {
    vi.mocked(client.get).mockResolvedValue({
      items: [],
      total: 0,
      page: 1,
      pageSize: 20,
    } as any)
    await orderApi.list({
      status: 'PendingShipment',
      orderNo: 'NO001',
      page: 1,
      pageSize: 20,
    })
    expect(client.get).toHaveBeenCalledWith('/seller/orders', {
      params: expect.objectContaining({
        status: 'PendingShipment',
        orderNo: 'NO001',
        page: 1,
        pageSize: 20,
      }),
    })
  })

  it('list 默认 page=1', async () => {
    vi.mocked(client.get).mockResolvedValue({
      items: [],
      total: 0,
      page: 1,
      pageSize: 20,
    } as any)
    await orderApi.list({ pageSize: 20 }) // 不传 page，使用默认 1
    expect(client.get).toHaveBeenCalledWith('/seller/orders', {
      params: expect.objectContaining({ page: 1, pageSize: 20 }),
    })
  })

  it('list 显式传 page 时透传该值', async () => {
    vi.mocked(client.get).mockResolvedValue({
      items: [],
      total: 0,
      page: 2,
      pageSize: 20,
    } as any)
    await orderApi.list({ page: 2, pageSize: 20 })
    expect(client.get).toHaveBeenCalledWith('/seller/orders', {
      params: expect.objectContaining({ page: 2, pageSize: 20 }),
    })
  })

  it('list 不传 page 时默认 1', async () => {
    vi.mocked(client.get).mockResolvedValue({
      items: [],
      total: 0,
      page: 1,
      pageSize: 20,
    } as any)
    await orderApi.list({ status: 'Shipped' })
    expect(client.get).toHaveBeenCalledWith('/seller/orders', {
      params: expect.objectContaining({ page: 1, pageSize: 20 }),
    })
  })

  it('get 调用 GET /seller/orders/{id}', async () => {
    vi.mocked(client.get).mockResolvedValue({ id: 'o1', orderNo: 'NO1' } as any)
    await orderApi.get('o1')
    expect(client.get).toHaveBeenCalledWith('/seller/orders/o1')
  })

  it('ship 调用 POST /seller/orders/{id}/ship 带 Idempotency-Key 头与 body', async () => {
    vi.mocked(client.post).mockResolvedValue({ id: 'o1', status: 'Shipped' } as any)
    const body: ShipOrderDto = {
      logisticsCompany: '顺丰速运',
      logisticsNo: 'SF1234567890',
      version: 3,
    }
    await orderApi.ship('o1', body)
    expect(client.post).toHaveBeenCalledWith('/seller/orders/o1/ship', body, {
      headers: { 'Idempotency-Key': 'mock-key' },
    })
    expect(withIdempotency).toHaveBeenCalled()
  })

  it('getLogisticsTrace 调用 GET /orders/{id}/logistics-trace（非 /seller/orders/）', async () => {
    vi.mocked(client.get).mockResolvedValue({
      orderId: 'o1',
      orderNo: 'NO1',
      logisticsCompany: 'SF',
      logisticsNo: 'SF123',
      trace: [],
    } as any)
    await orderApi.getLogisticsTrace('o1')
    expect(client.get).toHaveBeenCalledWith('/orders/o1/logistics-trace')
  })
})
