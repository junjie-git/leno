import { describe, expect, it, beforeEach, afterEach } from 'vitest'
import MockAdapter from 'axios-mock-adapter'
import { client } from '@/shared/http'
import { afterSalesApi, countAfterSalesByStatus } from './afterSales.api'
import type { AfterSalesDto } from '../types/afterSales.dto'

/**
 * 售后处理 API 单测（axios-mock-adapter 挂载到真实 client 实例）
 *
 * 覆盖点：
 * - list 调用 GET /admin/after-sales，单号 / 订单 / 买家 / 卖家 / 状态 / 类型 / 时间 / 分页组合传参并解包 data
 * - approve 调用 POST /admin/after-sales/{id}/approve 并传 ApproveAfterSalesDto
 * - reject 调用 POST /admin/after-sales/{id}/reject 并传 RejectAfterSalesDto
 * - countAfterSalesByStatus 按状态并行读取 total 聚合，单状态失败不阻塞整体
 */
describe('05-order-ops afterSalesApi', () => {
  let mock: MockAdapter

  const fakeAfterSales: AfterSalesDto = {
    id: 'as-0001',
    afterSalesNo: 'AS20260801001',
    orderId: 'o-0001',
    orderNo: 'NO202607261523001',
    userId: 'U20240345',
    buyerName: '王雪',
    sellerId: 'SL2024088',
    sellerName: '花西子美妆旗舰店',
    type: 'ReturnRefund',
    status: 'Pending',
    applyAmount: 3999,
    reason: '手机屏幕有划痕，申请退货退款',
    productId: 'p-0002',
    productName: 'iPhone 15 Pro',
    quantity: 1,
    createdAt: '2026-08-01T09:30:00.000Z',
    evidenceImageUrls: ['https://cdn.leno.com/as-0001-1.jpg'],
    negotiationRecords: [
      { role: 'Buyer', action: '发起售后申请', content: '屏幕有划痕', createdAt: '2026-08-01T09:30:00.000Z' },
    ],
  }

  const fakePage = { items: [fakeAfterSales], total: 1, page: 1, pageSize: 20 }

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

  it('list 调用 GET /admin/after-sales 组合查询参数并解包 data', async () => {
    let capturedParams: Record<string, unknown> = {}
    mock.onGet('/admin/after-sales').reply((config) => {
      capturedParams = (config.params ?? {}) as Record<string, unknown>
      return ok(fakePage)
    })

    const { data } = await afterSalesApi.list({
      page: 1,
      pageSize: 20,
      afterSalesNo: 'AS2026',
      orderId: 'o-0001',
      userId: 'U20240345',
      sellerId: 'SL2024088',
      status: 'Pending',
      type: 'ReturnRefund',
      fromTime: '2026-08-01T00:00:00.000Z',
      toTime: '2026-08-31T23:59:59.000Z',
    })

    expect(data.items[0].id).toBe('as-0001')
    expect(data.total).toBe(1)
    expect(mock.history.get.length).toBe(1)
    expect(mock.history.get[0].url).toBe('/admin/after-sales')
    expect(capturedParams).toMatchObject({
      page: 1,
      pageSize: 20,
      afterSalesNo: 'AS2026',
      orderId: 'o-0001',
      userId: 'U20240345',
      sellerId: 'SL2024088',
      status: 'Pending',
      type: 'ReturnRefund',
      fromTime: '2026-08-01T00:00:00.000Z',
      toTime: '2026-08-31T23:59:59.000Z',
    })
  })

  it('list 仅传分页参数时也正常工作', async () => {
    mock.onGet('/admin/after-sales').reply(() => ok(fakePage))

    const { data } = await afterSalesApi.list({ page: 2, pageSize: 10 })

    expect(data.total).toBe(1)
    expect(mock.history.get.length).toBe(1)
  })

  it('approve 调用 POST /admin/after-sales/{id}/approve 并传 ApproveAfterSalesDto', async () => {
    mock.onPost('/admin/after-sales/as-0001/approve').reply(() => ok(null))

    await afterSalesApi.approve('as-0001', { approvedAmount: 3500, remark: '协商部分退款' })

    expect(mock.history.post.length).toBe(1)
    const req = mock.history.post[0]
    expect(req.url).toBe('/admin/after-sales/as-0001/approve')
    expect(JSON.parse(req.data as string)).toEqual({ approvedAmount: 3500, remark: '协商部分退款' })
    expect(String(req.headers?.['Idempotency-Key'] ?? '')).not.toBe('')
  })

  it('approve 缺省金额时仅传 remark（后端按申请金额全额退款）', async () => {
    mock.onPost('/admin/after-sales/as-0001/approve').reply(() => ok(null))

    await afterSalesApi.approve('as-0001', {})

    const req = mock.history.post[0]
    expect(JSON.parse(req.data as string)).toEqual({})
  })

  it('reject 调用 POST /admin/after-sales/{id}/reject 并传 RejectAfterSalesDto', async () => {
    mock.onPost('/admin/after-sales/as-0001/reject').reply(() => ok(null))

    await afterSalesApi.reject('as-0001', { reason: '超过售后时效，证据不足不予支持' })

    expect(mock.history.post.length).toBe(1)
    const req = mock.history.post[0]
    expect(req.url).toBe('/admin/after-sales/as-0001/reject')
    expect(JSON.parse(req.data as string)).toEqual({ reason: '超过售后时效，证据不足不予支持' })
    expect(String(req.headers?.['Idempotency-Key'] ?? '')).not.toBe('')
  })

  it('reject 售后单状态已变更时抛出 409 并透出后端 message', async () => {
    mock
      .onPost('/admin/after-sales/as-0001/reject')
      .reply(409, { message: '售后单状态已变更，请刷新' })

    await expect(
      afterSalesApi.reject('as-0001', { reason: '重复提交的场景' }),
    ).rejects.toThrowError('售后单状态已变更，请刷新')
  })

  it('countAfterSalesByStatus 按状态并行读取 total 聚合，单状态失败记 0', async () => {
    mock.onGet('/admin/after-sales').reply((config) => {
      const params = (config.params ?? {}) as Record<string, unknown>
      if (params.status === 'Pending') {
        return [500, { message: '服务器开小差了' }]
      }
      const totals: Record<string, number> = { Refunded: 156, Rejected: 23 }
      const total = totals[String(params.status)] ?? 0
      return ok({ items: [], total, page: 1, pageSize: 1 })
    })

    const counts = await countAfterSalesByStatus(['Pending', 'Refunded', 'Rejected'])

    expect(counts).toEqual({ Pending: 0, Refunded: 156, Rejected: 23 })
    expect(mock.history.get.length).toBe(3)
  })

  it('list 业务错误（code !== 200）抛出 BusinessError', async () => {
    mock
      .onGet('/admin/after-sales')
      .reply(200, { code: 40301, message: '无售后查询权限', data: null })

    await expect(afterSalesApi.list({ page: 1, pageSize: 20 })).rejects.toThrowError('无售后查询权限')
  })
})
