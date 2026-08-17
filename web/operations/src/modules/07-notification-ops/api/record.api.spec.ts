import { describe, expect, it, beforeEach, afterEach } from 'vitest'
import MockAdapter from 'axios-mock-adapter'
import { client } from '@/shared/http'
import { recordApi } from './record.api'
import type { NotificationRecordDto } from '../types/record.dto'

/**
 * 通知记录 API 单测（axios-mock-adapter 挂载到真实 client 实例）
 *
 * 覆盖点：
 * - list 调用 GET /notifications/records，多维度筛选 + 时间范围 + 分页传参并解包
 * - detail 调用 GET /notifications/records/{id}
 * - byBusiness 调用 GET /notifications/records/by-business/{businessRef}（含编码转义）
 * - resend 调用 POST /admin/notifications/records/{id}/resend 并携带 Idempotency-Key
 * - statistics 调用 GET /admin/notifications/statistics 并解包送达率
 */
describe('07-notification-ops recordApi', () => {
  let mock: MockAdapter

  const fakeRecord: NotificationRecordDto = {
    id: 'ntf-0001',
    userId: 'U100823',
    recipient: '138****1234',
    channel: 'Sms',
    templateCode: 'ORDER_PAID',
    status: 'Delivered',
    businessRef: 'NO202607261523001',
    retryCount: 0,
    sentAt: '2026-07-26T14:30:05.000Z',
    deliveredAt: '2026-07-26T14:30:06.000Z',
    createdAt: '2026-07-26T14:30:03.000Z',
  }

  const fakeDetail: NotificationRecordDto = {
    ...fakeRecord,
    title: '【Leno】您的订单已支付成功',
    content: '【Leno】您于2026-07-26 15:23提交的订单NO202607261523001已支付成功。',
    providerResponse: { Code: 'OK', BizId: '9080199798765', RequestId: '4ABD-2026' },
    timeline: [
      { status: 'Delivered', at: '2026-07-26T14:30:06.000Z', detail: '渠道回调确认送达' },
      { status: 'Pending', at: '2026-07-26T14:30:03.000Z', detail: 'DispatchJob 接管' },
    ],
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

  it('list 调用 GET /notifications/records 组合查询参数并解包 data', async () => {
    let capturedParams: Record<string, unknown> = {}
    mock.onGet('/notifications/records').reply((config) => {
      capturedParams = (config.params ?? {}) as Record<string, unknown>
      return ok({ items: [fakeRecord], total: 1, page: 1, pageSize: 20 })
    })

    const { data } = await recordApi.list({
      page: 1,
      pageSize: 20,
      userId: 'U100823',
      channel: 'Sms',
      status: 'DeadLetter',
      templateCode: 'ORDER_PAID',
      businessRef: 'NO202607261523001',
      fromTime: '2026-07-19T00:00:00.000Z',
      toTime: '2026-07-26T23:59:59.000Z',
    })

    expect(data.items[0].id).toBe('ntf-0001')
    expect(data.total).toBe(1)
    expect(mock.history.get[0].url).toBe('/notifications/records')
    expect(capturedParams).toMatchObject({
      page: 1,
      pageSize: 20,
      userId: 'U100823',
      channel: 'Sms',
      status: 'DeadLetter',
      templateCode: 'ORDER_PAID',
      businessRef: 'NO202607261523001',
      fromTime: '2026-07-19T00:00:00.000Z',
      toTime: '2026-07-26T23:59:59.000Z',
    })
  })

  it('detail 调用 GET /notifications/records/{id} 并解包渲染正文与时间线', async () => {
    mock.onGet('/notifications/records/ntf-0001').reply(() => ok(fakeDetail))

    const { data } = await recordApi.detail('ntf-0001')

    expect(data.content).toContain('已支付成功')
    expect(data.timeline).toHaveLength(2)
    expect(mock.history.get[0].url).toBe('/notifications/records/ntf-0001')
  })

  it('byBusiness 调用 GET /notifications/records/by-business/{businessRef} 并编码特殊字符', async () => {
    mock.onGet('/notifications/records/by-business/NO%2F2026%2F001').reply(() => ok([fakeRecord]))

    const { data } = await recordApi.byBusiness('NO/2026/001')

    expect(data).toHaveLength(1)
    expect(data[0].businessRef).toBe('NO202607261523001')
    expect(mock.history.get[0].url).toBe('/notifications/records/by-business/NO%2F2026%2F001')
  })

  it('resend 调用 POST /admin/notifications/records/{id}/resend 并携带 Idempotency-Key', async () => {
    mock.onPost('/admin/notifications/records/ntf-0001/resend').reply(() => ok(null))

    await recordApi.resend('ntf-0001')

    const req = mock.history.post[0]
    expect(req.url).toBe('/admin/notifications/records/ntf-0001/resend')
    expect(String(req.headers?.['Idempotency-Key'] ?? '')).not.toBe('')
  })

  it('statistics 调用 GET /admin/notifications/statistics 并解包各状态计数与送达率', async () => {
    const stats = {
      pendingCount: 12,
      sendingCount: 4,
      sentCount: 260,
      deliveredCount: 1280,
      failedCount: 18,
      deadLetterCount: 3,
      deliveryRate: 0.985,
    }
    mock.onGet('/admin/notifications/statistics').reply(() => ok(stats))

    const { data } = await recordApi.statistics()

    expect(data.deliveredCount).toBe(1280)
    expect(data.deadLetterCount).toBe(3)
    expect(data.deliveryRate).toBeCloseTo(0.985)
    expect(mock.history.get[0].url).toBe('/admin/notifications/statistics')
  })

  it('resend 业务错误（code !== 200）抛出 BusinessError', async () => {
    mock
      .onPost('/admin/notifications/records/ntf-0001/resend')
      .reply(200, { code: 40901, message: '记录状态已变更，非死信状态', data: null })

    await expect(recordApi.resend('ntf-0001')).rejects.toThrowError('记录状态已变更，非死信状态')
  })
})
