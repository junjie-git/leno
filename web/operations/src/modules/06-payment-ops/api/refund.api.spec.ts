import { describe, expect, it, beforeEach, afterEach } from 'vitest'
import MockAdapter from 'axios-mock-adapter'
import { client } from '@/shared/http'
import { refundApi } from './refund.api'
import type { RefundListResultDto } from '../types/refund.dto'

/**
 * refundApi 单测（axios-mock-adapter 挂载到真实 client 实例）
 *
 * 覆盖点：
 * - list 调用 GET /admin/refunds，退款编号/订单/状态/时间范围 + 分页参数透传
 * - 响应信封解包：拿到 statusCounts 与 successRate 统计元数据
 * - 仅分页参数时正常工作
 * - 业务错误（code !== 200）抛 BusinessError 并透传后端 message
 */
describe('06-payment-ops refundApi', () => {
  let mock: MockAdapter

  const fakeResult: RefundListResultDto = {
    items: [
      {
        id: 'rf-0001',
        refundNo: 'RF20260816001',
        orderId: 'ord-1001',
        orderNo: 'NO202608161520876',
        userId: 'U10293847',
        userName: '买家A',
        amount: 49,
        channel: 'Alipay',
        status: 'Refunded',
        afterSalesId: 'as-0005',
        afterSalesNo: 'AS20260816005',
        reason: '商品质量问题',
        failReason: undefined,
        requestedAt: '2026-08-16T14:30:22.000Z',
        completedAt: '2026-08-16T14:39:15.890Z',
        channelWriteBack: { refund_id: '5000000820202608', fund_change: 'Y', success: true },
        timeline: [
          {
            status: 'Requested',
            label: '买家发起退款申请',
            description: '关联售后单 AS20260816005',
            occurredAt: '2026-08-16T14:30:22.000Z',
          },
          {
            status: 'Refunded',
            label: '渠道回写成功，退款完成',
            occurredAt: '2026-08-16T14:39:15.890Z',
          },
        ],
      },
      {
        id: 'rf-0002',
        refundNo: 'RF20260816002',
        orderId: 'ord-1003',
        userId: 'U10293849',
        amount: 899,
        channel: 'WeChat',
        status: 'Failed',
        afterSalesNo: 'AS20260816006',
        reason: '未收到商品',
        failReason: '渠道返回：ACQ.TRADE_HAS_SUCCESS_REFUND · 已存在退款单',
        requestedAt: '2026-08-16T14:25:48.000Z',
        completedAt: undefined,
      },
    ],
    total: 2,
    page: 1,
    pageSize: 20,
    statusCounts: { Pending: 3, Refunded: 18, Failed: 1 },
    successRate: 0.818,
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

  it('list 调用 GET /admin/refunds 组合筛选与分页参数并解包 data', async () => {
    let capturedParams: Record<string, unknown> = {}
    mock.onGet('/admin/refunds').reply((config) => {
      capturedParams = (config.params ?? {}) as Record<string, unknown>
      return ok(fakeResult)
    })

    const { data } = await refundApi.list({
      page: 1,
      pageSize: 20,
      refundNo: 'RF20260816001',
      orderId: 'ord-1001',
      status: 'Refunded',
      fromTime: '2026-08-10T00:00:00.000Z',
      toTime: '2026-08-16T23:59:59.000Z',
    })

    expect(data.items).toHaveLength(2)
    expect(data.items[0].refundNo).toBe('RF20260816001')
    expect(data.items[0].channelWriteBack?.refund_id).toBe('5000000820202608')
    expect(data.items[1].failReason).toContain('ACQ.TRADE_HAS_SUCCESS_REFUND')
    expect(data.total).toBe(2)

    expect(mock.history.get.length).toBe(1)
    expect(mock.history.get[0].url).toBe('/admin/refunds')
    expect(capturedParams).toMatchObject({
      page: 1,
      pageSize: 20,
      refundNo: 'RF20260816001',
      orderId: 'ord-1001',
      status: 'Refunded',
      fromTime: '2026-08-10T00:00:00.000Z',
      toTime: '2026-08-16T23:59:59.000Z',
    })
  })

  it('list 解包统计元数据（各状态计数 + 退款成功率）', async () => {
    mock.onGet('/admin/refunds').reply(() => ok(fakeResult))

    const { data } = await refundApi.list({ page: 1, pageSize: 20 })

    expect(data.statusCounts).toEqual({ Pending: 3, Refunded: 18, Failed: 1 })
    expect(data.successRate).toBe(0.818)
  })

  it('list 仅传分页参数时也正常工作', async () => {
    mock.onGet('/admin/refunds').reply(() => ok(fakeResult))

    const { data } = await refundApi.list({ page: 1, pageSize: 10 })

    expect(data.items).toHaveLength(2)
    expect(data.total).toBe(2)
    expect(mock.history.get.length).toBe(1)
  })

  it('list 业务错误（code !== 200）抛出 BusinessError 并透传 message', async () => {
    mock.onGet('/admin/refunds').reply(200, { code: 40301, message: '无退款记录查询权限', data: null })

    await expect(refundApi.list({ page: 1, pageSize: 20 })).rejects.toThrowError('无退款记录查询权限')
  })
})
