import { describe, expect, it, beforeEach, afterEach } from 'vitest'
import MockAdapter from 'axios-mock-adapter'
import { client } from '@/shared/http'
import { paymentApi } from './payment.api'
import type { PaymentListResultDto } from '../types/payment.dto'

/**
 * paymentApi 单测（axios-mock-adapter 挂载到真实 client 实例）
 *
 * 覆盖点：
 * - list 调用 GET /admin/payments，支付单号/订单/用户/渠道/状态/时间范围 + 分页参数透传
 * - 响应信封解包：拿到 statusCounts 与 successRate 统计元数据
 * - 仅分页参数时正常工作
 * - 业务错误（code !== 200）抛 BusinessError 并透传后端 message
 */
describe('06-payment-ops paymentApi', () => {
  let mock: MockAdapter

  const fakeResult: PaymentListResultDto = {
    items: [
      {
        id: 'pay-0001',
        paymentNo: 'PAY202608161523001789',
        orderId: 'ord-1001',
        orderNo: 'NO202608161523001',
        userId: 'U10293847',
        userName: '买家A',
        amount: 299,
        channel: 'WeChat',
        status: 'Success',
        channelTradeNo: '4200002626101523047891',
        createdAt: '2026-08-16T15:23:00.124Z',
        paidAt: '2026-08-16T15:23:05.031Z',
        afterSalesNo: undefined,
        abnormal: false,
        abnormalReason: undefined,
        channelParams: { AppId: 'wx1a2b3c4d5e6f7890', MchId: '1900000109' },
        callbackLogs: [
          {
            id: 'cb-0001',
            event: '渠道回调到达',
            success: true,
            detail: 'return_code=SUCCESS · result_code=SUCCESS',
            payload: { transaction_id: '4200002626101523047891' },
            receivedAt: '2026-08-16T15:23:04.892Z',
          },
        ],
        timeline: [
          {
            status: 'Created',
            label: '发起支付请求',
            description: '统一下单 · prepay_id=wx26152301987654',
            occurredAt: '2026-08-16T15:23:00.124Z',
          },
          {
            status: 'Success',
            label: '支付完成',
            occurredAt: '2026-08-16T15:23:05.031Z',
          },
        ],
      },
      {
        id: 'pay-0002',
        paymentNo: 'PAY202608161520876001',
        orderId: 'ord-1002',
        userId: 'U10293848',
        amount: 49,
        channel: 'Alipay',
        status: 'Refunded',
        createdAt: '2026-08-16T15:20:18.000Z',
        afterSalesNo: 'AS20260816005',
        abnormal: false,
      },
    ],
    total: 2,
    page: 1,
    pageSize: 20,
    statusCounts: { Pending: 12, Success: 1280, Failed: 3, Refunded: 18 },
    successRate: 0.985,
  }

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

  it('list 调用 GET /admin/payments 组合筛选与分页参数并解包 data', async () => {
    let capturedParams: Record<string, unknown> = {}
    mock.onGet('/admin/payments').reply((config) => {
      capturedParams = (config.params ?? {}) as Record<string, unknown>
      return ok(fakeResult)
    })

    const { data } = await paymentApi.list({
      page: 1,
      pageSize: 20,
      paymentNo: 'PAY2026081615',
      orderId: 'ord-1001',
      userId: 'U10293847',
      channel: 'WeChat',
      status: 'Success',
      fromTime: '2026-08-10T00:00:00.000Z',
      toTime: '2026-08-16T23:59:59.000Z',
    })

    expect(data.items).toHaveLength(2)
    expect(data.items[0].paymentNo).toBe('PAY202608161523001789')
    expect(data.items[0].channelParams?.AppId).toBe('wx1a2b3c4d5e6f7890')
    expect(data.items[0].callbackLogs).toHaveLength(1)
    expect(data.items[1].afterSalesNo).toBe('AS20260816005')
    expect(data.total).toBe(2)

    expect(mock.history.get.length).toBe(1)
    expect(mock.history.get[0].url).toBe('/admin/payments')
    expect(capturedParams).toMatchObject({
      page: 1,
      pageSize: 20,
      paymentNo: 'PAY2026081615',
      orderId: 'ord-1001',
      userId: 'U10293847',
      channel: 'WeChat',
      status: 'Success',
      fromTime: '2026-08-10T00:00:00.000Z',
      toTime: '2026-08-16T23:59:59.000Z',
    })
  })

  it('list 解包统计元数据（各状态计数 + 成功率）', async () => {
    mock.onGet('/admin/payments').reply(() => ok(fakeResult))

    const { data } = await paymentApi.list({ page: 1, pageSize: 20 })

    expect(data.statusCounts).toEqual({ Pending: 12, Success: 1280, Failed: 3, Refunded: 18 })
    expect(data.successRate).toBe(0.985)
  })

  it('list 仅传分页参数时也正常工作', async () => {
    mock.onGet('/admin/payments').reply(() => ok(fakeResult))

    const { data } = await paymentApi.list({ page: 2, pageSize: 10 })

    expect(data.page).toBe(1)
    expect(data.items).toHaveLength(2)
    expect(mock.history.get.length).toBe(1)
  })

  it('list 业务错误（code !== 200）抛出 BusinessError 并透传 message', async () => {
    mock.onGet('/admin/payments').reply(200, { code: 40301, message: '无支付记录查询权限', data: null })

    await expect(paymentApi.list({ page: 1, pageSize: 20 })).rejects.toThrowError('无支付记录查询权限')
  })
})
