import { describe, expect, it, beforeEach, afterEach } from 'vitest'
import MockAdapter from 'axios-mock-adapter'
import { client } from '@/shared/http'
import { deadLetterApi } from './deadLetter.api'
import type { DeadLetterRecordDto } from '../types/dead-letter.dto'

/**
 * 死信管理 API 单测（axios-mock-adapter 挂载到真实 client 实例）
 *
 * 覆盖点：
 * - list 调用 GET /admin/dead-letters，渠道 / 模板编码 / 失败时间范围 + 分页传参并解包
 *   （后端固定 Status=DeadLetter，前端不传 status）
 * - batchResend 调用 POST /admin/dead-letters/batch-resend 并携带 Idempotency-Key
 * - batchDiscard 调用 POST /admin/dead-letters/batch-discard，含丢弃原因
 */
describe('07-notification-ops deadLetterApi', () => {
  let mock: MockAdapter

  const fakeDeadLetter: DeadLetterRecordDto = {
    recordId: 'dl-0001',
    userId: 'U100823',
    recipient: '138****1234',
    templateCode: 'ORDER_PAID',
    channel: 'Sms',
    title: '【Leno】您的订单已支付成功',
    content: '【Leno】您于2026-07-26 15:23提交的订单NO202607261523001已支付成功。',
    status: 'DeadLetter',
    retryCount: 5,
    errorCode: 'TIMEOUT',
    errorMessage: '上游短信网关超时，连续 5 次重试失败',
    failedAt: '2026-07-26T14:30:05.000Z',
    createdAt: '2026-07-26T14:20:00.000Z',
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

  it('list 调用 GET /admin/dead-letters 组合查询参数且不传 status', async () => {
    let capturedParams: Record<string, unknown> = {}
    mock.onGet('/admin/dead-letters').reply((config) => {
      capturedParams = (config.params ?? {}) as Record<string, unknown>
      return ok({ items: [fakeDeadLetter], total: 1, page: 1, pageSize: 20 })
    })

    const { data } = await deadLetterApi.list({
      page: 1,
      pageSize: 20,
      channel: 'Sms',
      templateCode: 'ORDER_PAID',
      fromTime: '2026-07-19T00:00:00.000Z',
      toTime: '2026-07-26T23:59:59.000Z',
    })

    expect(data.items[0].recordId).toBe('dl-0001')
    expect(data.total).toBe(1)
    expect(mock.history.get[0].url).toBe('/admin/dead-letters')
    expect(capturedParams).toMatchObject({
      page: 1,
      pageSize: 20,
      channel: 'Sms',
      templateCode: 'ORDER_PAID',
      fromTime: '2026-07-19T00:00:00.000Z',
      toTime: '2026-07-26T23:59:59.000Z',
    })
    expect(capturedParams.status).toBeUndefined()
  })

  it('list 仅传分页参数时也正常工作', async () => {
    mock.onGet('/admin/dead-letters').reply(() => ok({ items: [], total: 0, page: 1, pageSize: 20 }))

    const { data } = await deadLetterApi.list({ page: 1, pageSize: 20 })

    expect(data.total).toBe(0)
    expect(data.items).toEqual([])
  })

  it('batchResend 调用 POST /admin/dead-letters/batch-resend 并携带 Idempotency-Key', async () => {
    const result = { successCount: 2, failureCount: 0, errors: [] }
    mock.onPost('/admin/dead-letters/batch-resend').reply(() => ok(result))

    const { data } = await deadLetterApi.batchResend({ recordIds: ['dl-0001', 'dl-0002'] })

    expect(data.successCount).toBe(2)
    const req = mock.history.post[0]
    expect(req.url).toBe('/admin/dead-letters/batch-resend')
    expect(JSON.parse(req.data as string)).toEqual({ recordIds: ['dl-0001', 'dl-0002'] })
    expect(String(req.headers?.['Idempotency-Key'] ?? '')).not.toBe('')
  })

  it('batchDiscard 调用 POST /admin/dead-letters/batch-discard 并传丢弃原因', async () => {
    const result = {
      successCount: 1,
      failureCount: 1,
      errors: ['记录状态已变更，非死信状态'],
    }
    mock.onPost('/admin/dead-letters/batch-discard').reply(() => ok(result))

    const { data } = await deadLetterApi.batchDiscard({
      recordIds: ['dl-0001', 'dl-0003'],
      discardReason: '人工排查确认为测试数据，无需继续投递',
    })

    expect(data.failureCount).toBe(1)
    expect(data.errors).toEqual(['记录状态已变更，非死信状态'])
    const req = mock.history.post[0]
    expect(req.url).toBe('/admin/dead-letters/batch-discard')
    expect(JSON.parse(req.data as string)).toEqual({
      recordIds: ['dl-0001', 'dl-0003'],
      discardReason: '人工排查确认为测试数据，无需继续投递',
    })
    expect(String(req.headers?.['Idempotency-Key'] ?? '')).not.toBe('')
  })

  it('batchResend 部分失败结果正常解包', async () => {
    const result = {
      successCount: 1,
      failureCount: 1,
      errors: ['记录 dl-0003 状态已变更，非死信状态'],
    }
    mock.onPost('/admin/dead-letters/batch-resend').reply(() => ok(result))

    const { data } = await deadLetterApi.batchResend({ recordIds: ['dl-0001', 'dl-0003'] })

    expect(data.successCount).toBe(1)
    expect(data.errors[0]).toContain('状态已变更')
  })

  it('list 业务错误（code !== 200）抛出 BusinessError', async () => {
    mock
      .onGet('/admin/dead-letters')
      .reply(200, { code: 40301, message: '无死信查询权限', data: null })

    await expect(deadLetterApi.list({ page: 1, pageSize: 20 })).rejects.toThrowError('无死信查询权限')
  })
})
