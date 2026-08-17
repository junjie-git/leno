import { describe, expect, it, beforeEach, afterEach } from 'vitest'
import MockAdapter from 'axios-mock-adapter'
import { client } from '@/shared/http'
import { reconciliationApi } from './reconciliation.api'
import type { ReconciliationDiffListResultDto } from '../types/reconciliation.dto'

/**
 * reconciliationApi 单测（axios-mock-adapter 挂载到真实 client 实例）
 *
 * 覆盖点：
 * - listDiffs 调用 GET /admin/reconciliation/diffs，账单日期/渠道/差异类型/状态 + 分页参数透传
 * - trigger 携带 billDate 时调用 POST /admin/reconciliation/trigger?billDate=yyyy-MM-dd 并携带 Idempotency-Key
 * - trigger 缺省 billDate 时调用 POST /admin/reconciliation/trigger（后端默认前一天）
 * - 业务错误（code !== 200）抛 BusinessError 并透传后端 message（对账任务进行中提示）
 */
describe('06-payment-ops reconciliationApi', () => {
  let mock: MockAdapter

  const fakeResult: ReconciliationDiffListResultDto = {
    items: [
      {
        id: 'diff-0001',
        billDate: '2026-08-15',
        channel: 'WeChat',
        diffType: 'LongAmount',
        channelTransactionNo: '4200002626151523047891',
        channelAmount: 199,
        channelTransactionTime: '2026-08-15T15:23:04.000Z',
        systemTransactionNo: undefined,
        systemAmount: undefined,
        paymentId: undefined,
        paymentNo: undefined,
        remark: '检查回调日志或确认为测试交易',
        status: 'PendingResolve',
        createdAt: '2026-08-16T02:10:00.000Z',
        resolvedAt: undefined,
        resolvedBy: undefined,
        timeline: [
          {
            status: 'Created',
            label: '对账任务生成差异记录',
            description: '长款：渠道有账但系统无记录',
            occurredAt: '2026-08-16T02:10:00.000Z',
          },
        ],
      },
      {
        id: 'diff-0002',
        billDate: '2026-08-15',
        channel: 'Alipay',
        diffType: 'AmountMismatch',
        channelTransactionNo: '2026081522001480290123',
        channelAmount: 299,
        systemTransactionNo: 'PAY202608151520876001',
        systemAmount: 259,
        paymentId: 'pay-0002',
        paymentNo: 'PAY202608151520876001',
        remark: '人工核对退款流程',
        status: 'Resolved',
        createdAt: '2026-08-16T02:10:00.000Z',
        resolvedAt: '2026-08-16T10:00:00.000Z',
        resolvedBy: '财务管理员',
      },
    ],
    total: 2,
    page: 1,
    pageSize: 20,
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

  it('listDiffs 调用 GET /admin/reconciliation/diffs 组合筛选与分页参数并解包 data', async () => {
    let capturedParams: Record<string, unknown> = {}
    mock.onGet('/admin/reconciliation/diffs').reply((config) => {
      capturedParams = (config.params ?? {}) as Record<string, unknown>
      return ok(fakeResult)
    })

    const { data } = await reconciliationApi.listDiffs({
      page: 1,
      pageSize: 20,
      billDate: '2026-08-15',
      channel: 'WeChat',
      diffType: 'LongAmount',
      status: 'PendingResolve',
    })

    expect(data.items).toHaveLength(2)
    expect(data.items[0].diffType).toBe('LongAmount')
    expect(data.items[0].channelAmount).toBe(199)
    expect(data.items[0].systemAmount).toBeUndefined()
    expect(data.items[1].systemAmount).toBe(259)
    expect(data.total).toBe(2)

    expect(mock.history.get.length).toBe(1)
    expect(mock.history.get[0].url).toBe('/admin/reconciliation/diffs')
    expect(capturedParams).toMatchObject({
      page: 1,
      pageSize: 20,
      billDate: '2026-08-15',
      channel: 'WeChat',
      diffType: 'LongAmount',
      status: 'PendingResolve',
    })
  })

  it('listDiffs 仅传分页参数时也正常工作', async () => {
    mock.onGet('/admin/reconciliation/diffs').reply(() => ok(fakeResult))

    const { data } = await reconciliationApi.listDiffs({ page: 1, pageSize: 20 })

    expect(data.items).toHaveLength(2)
    expect(mock.history.get.length).toBe(1)
  })

  it('trigger 携带 billDate 时调用 POST trigger?billDate=yyyy-MM-dd 并携带 Idempotency-Key', async () => {
    mock.onPost('/admin/reconciliation/trigger?billDate=2026-08-15').reply(() => ok(null))

    await reconciliationApi.trigger('2026-08-15')

    expect(mock.history.post.length).toBe(1)
    const req = mock.history.post[0]
    expect(req.url).toBe('/admin/reconciliation/trigger?billDate=2026-08-15')
    expect(String(req.headers?.['Idempotency-Key'] ?? '')).not.toBe('')
  })

  it('trigger 缺省 billDate 时调用 POST /admin/reconciliation/trigger（后端默认前一天）', async () => {
    mock.onPost('/admin/reconciliation/trigger').reply(() => ok(null))

    await reconciliationApi.trigger()

    expect(mock.history.post.length).toBe(1)
    const req = mock.history.post[0]
    expect(req.url).toBe('/admin/reconciliation/trigger')
    expect(String(req.headers?.['Idempotency-Key'] ?? '')).not.toBe('')
  })

  it('trigger 重复触发（code !== 200）抛出 BusinessError 并透传后端提示', async () => {
    mock
      .onPost('/admin/reconciliation/trigger?billDate=2026-08-15')
      .reply(200, { code: 40901, message: '对账任务进行中，请勿重复触发', data: null })

    await expect(reconciliationApi.trigger('2026-08-15')).rejects.toThrowError(
      '对账任务进行中，请勿重复触发',
    )
  })

  it('listDiffs 网络服务错误（HTTP 500）抛出 ServerError 并透传 message', async () => {
    mock
      .onGet('/admin/reconciliation/diffs')
      .reply(500, { code: 500, message: '渠道账单接口暂不可用，请稍后重试', data: null })

    await expect(reconciliationApi.listDiffs({ page: 1, pageSize: 20 })).rejects.toThrowError(
      '渠道账单接口暂不可用，请稍后重试',
    )
  })
})
