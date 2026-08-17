import { describe, expect, it, beforeEach, afterEach } from 'vitest'
import MockAdapter from 'axios-mock-adapter'
import { client } from '@/shared/http'
import { orderApi, countOrdersByStatus } from './order.api'
import type { OrderDto } from '../types/order.dto'

/**
 * 订单管理 API 单测（axios-mock-adapter 挂载到真实 client 实例）
 *
 * 覆盖点：
 * - list 调用 GET /admin/orders，订单号 / 买家 / 卖家 / 状态 / 时间范围 / 分页组合传参并解包 data
 * - forceCancel 调用 POST /admin/orders/{id}/force-cancel，body 正确且写操作自动携带 Idempotency-Key
 * - countOrdersByStatus 按状态并行读取 total 聚合，单状态失败不阻塞整体
 */
describe('05-order-ops orderApi', () => {
  let mock: MockAdapter

  const fakeOrder: OrderDto = {
    id: 'o-0001',
    orderNo: 'NO202607261523001',
    userId: 'U20240345',
    buyerName: '王雪',
    sellerId: 'SL2024088',
    sellerName: '花西子美妆旗舰店',
    itemSummary: '花西子同心锁口红 x1',
    totalAmount: 189,
    paymentMethod: 'WeChatPay',
    status: 'Paid',
    createdAt: '2026-07-25T20:45:00.000Z',
    lines: [
      {
        id: 'l-0001',
        productId: 'p-0001',
        productName: '花西子同心锁口红',
        skuSpec: 'M316',
        unitPrice: 189,
        quantity: 1,
        subtotal: 189,
      },
    ],
    address: {
      receiver: '王雪',
      phone: '13800000000',
      province: '浙江省',
      city: '杭州市',
      district: '西湖区',
      detail: '文三路 100 号',
    },
    payment: {
      method: 'WeChatPay',
      status: 'Paid',
      transactionNo: 'TX202607250001',
      paidAmount: 189,
      paidAt: '2026-07-25T20:46:00.000Z',
    },
    logisticsTrack: [
      { time: '2026-07-26T09:00:00.000Z', description: '包裹已出库' },
    ],
    statusHistory: [
      { status: 'PendingPayment', operator: '买家', createdAt: '2026-07-25T20:45:00.000Z' },
      { status: 'Paid', operator: '系统', createdAt: '2026-07-25T20:46:00.000Z' },
    ],
  }

  const fakePage = { items: [fakeOrder], total: 1, page: 0, pageSize: 20 }

  function ok<T>(data: T): [number, { code: number; message: string; data: T }] {
    return [200, { code: 200, message: 'OK', data }]
  }

  beforeEach(() => {
    mock = new MockAdapter(client)
    localStorage.clear()
  })

  afterEach(() => {
    mock.restore()
  })

  it('list 调用 GET /admin/orders 组合查询参数并解包 data', async () => {
    let capturedParams: Record<string, unknown> = {}
    mock.onGet('/admin/orders').reply((config) => {
      capturedParams = (config.params ?? {}) as Record<string, unknown>
      return ok(fakePage)
    })

    const { data } = await orderApi.list({
      page: 0,
      pageSize: 20,
      orderNo: 'NO202607',
      userId: 'U20240345',
      sellerId: 'SL2024088',
      status: 'Paid',
      fromTime: '2026-07-01T00:00:00.000Z',
      toTime: '2026-07-31T23:59:59.000Z',
    })

    expect(data.items[0].id).toBe('o-0001')
    expect(data.total).toBe(1)
    expect(mock.history.get.length).toBe(1)
    expect(mock.history.get[0].url).toBe('/admin/orders')
    expect(capturedParams).toMatchObject({
      page: 0,
      pageSize: 20,
      orderNo: 'NO202607',
      userId: 'U20240345',
      sellerId: 'SL2024088',
      status: 'Paid',
      fromTime: '2026-07-01T00:00:00.000Z',
      toTime: '2026-07-31T23:59:59.000Z',
    })
  })

  it('list 仅传分页参数时也正常工作', async () => {
    mock.onGet('/admin/orders').reply(() => ok(fakePage))

    const { data } = await orderApi.list({ page: 2, pageSize: 10 })

    expect(data.total).toBe(1)
    expect(mock.history.get.length).toBe(1)
  })

  it('forceCancel 调用 POST /admin/orders/{id}/force-cancel 并传 ForceCancelOrderDto', async () => {
    mock.onPost('/admin/orders/o-0001/force-cancel').reply(() => ok(null))

    await orderApi.forceCancel('o-0001', { reason: '买家投诉涉嫌欺诈，运营介入强制取消' })

    expect(mock.history.post.length).toBe(1)
    const req = mock.history.post[0]
    expect(req.url).toBe('/admin/orders/o-0001/force-cancel')
    expect(JSON.parse(req.data as string)).toEqual({ reason: '买家投诉涉嫌欺诈，运营介入强制取消' })
    expect(String(req.headers?.['Idempotency-Key'] ?? '')).not.toBe('')
  })

  it('forceCancel 订单状态已变更时抛出 409 并透出后端 message', async () => {
    mock
      .onPost('/admin/orders/o-0001/force-cancel')
      .reply(409, { message: '订单状态已变更，请刷新' })

    await expect(
      orderApi.forceCancel('o-0001', { reason: '重复提交的场景' }),
    ).rejects.toThrowError('订单状态已变更，请刷新')
  })

  it('countOrdersByStatus 按状态并行读取 total 聚合', async () => {
    mock.onGet('/admin/orders').reply((config) => {
      const params = (config.params ?? {}) as Record<string, unknown>
      const status = String(params.status)
      const totals: Record<string, number> = {
        PendingPayment: 12,
        Paid: 56,
        Shipped: 128,
      }
      const total = totals[status] ?? 0
      return ok({ items: [], total, page: 1, pageSize: 1 })
    })

    const counts = await countOrdersByStatus(['PendingPayment', 'Paid', 'Shipped'])

    expect(counts).toEqual({ PendingPayment: 12, Paid: 56, Shipped: 128 })
    expect(mock.history.get.length).toBe(3)
  })

  it('countOrdersByStatus 单状态查询失败时记 0 不阻塞整体', async () => {
    mock.onGet('/admin/orders').reply((config) => {
      const params = (config.params ?? {}) as Record<string, unknown>
      if (params.status === 'Paid') {
        return [500, { message: '服务器开小差了' }]
      }
      return ok({ items: [], total: 7, page: 1, pageSize: 1 })
    })

    const counts = await countOrdersByStatus(['Paid', 'Completed'])

    expect(counts.Paid).toBe(0)
    expect(counts.Completed).toBe(7)
  })

  it('list 业务错误（code !== 200）抛出 BusinessError', async () => {
    mock.onGet('/admin/orders').reply(200, { code: 40301, message: '无订单查询权限', data: null })

    await expect(orderApi.list({ page: 0, pageSize: 20 })).rejects.toThrowError('无订单查询权限')
  })
})
