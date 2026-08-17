import { describe, expect, it, beforeEach, afterEach } from 'vitest'
import MockAdapter from 'axios-mock-adapter'
import { client } from '@/shared/http'
import { rateLimitApi } from './rateLimit.api'
import type { RateLimitConfigDto } from '../types/rate-limit.dto'

/**
 * 通知限流 API 单测（axios-mock-adapter 挂载到真实 client 实例）
 *
 * 覆盖点：
 * - get 调用 GET /admin/notification-rate-limits?channel=x 并解包限流规则与当前用量
 * - update 调用 PUT /admin/notification-rate-limits，body 含四级阈值与状态并携带 Idempotency-Key
 */
describe('07-notification-ops rateLimitApi', () => {
  let mock: MockAdapter

  const fakeLimit: RateLimitConfigDto = {
    channel: 'Sms',
    userDailyLimit: 10,
    userHourlyLimit: 3,
    globalPerMinuteLimit: 100,
    globalHourlyLimit: 1000,
    currentUsage: { todayCount: 560, hourCount: 88, minuteCount: 12 },
    status: 'Active',
    updatedBy: '运营管理员',
    updatedAt: '2026-07-25T10:30:00.000Z',
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

  it('get 调用 GET /admin/notification-rate-limits 并携带 channel 查询参数', async () => {
    let capturedParams: Record<string, unknown> = {}
    mock.onGet('/admin/notification-rate-limits').reply((config) => {
      capturedParams = (config.params ?? {}) as Record<string, unknown>
      return ok(fakeLimit)
    })

    const { data } = await rateLimitApi.get('Sms')

    expect(data.userDailyLimit).toBe(10)
    expect(data.currentUsage.todayCount).toBe(560)
    expect(mock.history.get[0].url).toBe('/admin/notification-rate-limits')
    expect(capturedParams).toEqual({ channel: 'Sms' })
  })

  it('update 调用 PUT /admin/notification-rate-limits 并传四级阈值与状态', async () => {
    mock.onPut('/admin/notification-rate-limits').reply(() => ok(fakeLimit))

    const { data } = await rateLimitApi.update({
      channel: 'Sms',
      userDailyLimit: 10,
      userHourlyLimit: 3,
      globalPerMinuteLimit: 100,
      globalHourlyLimit: 1000,
      status: 'Active',
    })

    expect(data.status).toBe('Active')
    const req = mock.history.put[0]
    expect(req.url).toBe('/admin/notification-rate-limits')
    expect(JSON.parse(req.data as string)).toEqual({
      channel: 'Sms',
      userDailyLimit: 10,
      userHourlyLimit: 3,
      globalPerMinuteLimit: 100,
      globalHourlyLimit: 1000,
      status: 'Active',
    })
    expect(String(req.headers?.['Idempotency-Key'] ?? '')).not.toBe('')
  })

  it('update 关闭限流（status=Inactive）时同样正常提交', async () => {
    mock
      .onPut('/admin/notification-rate-limits')
      .reply(() => ok({ ...fakeLimit, status: 'Inactive' }))

    const { data } = await rateLimitApi.update({
      channel: 'Push',
      userDailyLimit: 20,
      userHourlyLimit: 5,
      globalPerMinuteLimit: 200,
      globalHourlyLimit: 2000,
      status: 'Inactive',
    })

    expect(data.status).toBe('Inactive')
    expect(JSON.parse(mock.history.put[0].data as string)).toMatchObject({
      channel: 'Push',
      status: 'Inactive',
    })
  })

  it('update 业务错误（code !== 200）抛出 BusinessError', async () => {
    mock
      .onPut('/admin/notification-rate-limits')
      .reply(200, { code: 40001, message: '用户级限流不可超过全局级', data: null })

    await expect(
      rateLimitApi.update({
        channel: 'Sms',
        userDailyLimit: 5000,
        userHourlyLimit: 3,
        globalPerMinuteLimit: 100,
        globalHourlyLimit: 1000,
        status: 'Active',
      }),
    ).rejects.toThrowError('用户级限流不可超过全局级')
  })
})
