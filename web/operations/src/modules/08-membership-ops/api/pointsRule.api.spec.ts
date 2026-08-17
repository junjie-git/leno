import { describe, expect, it, beforeEach, afterEach } from 'vitest'
import MockAdapter from 'axios-mock-adapter'
import { client } from '@/shared/http'
import { pointsRuleApi } from './pointsRule.api'
import type { PointsRuleDto } from '../types/pointsRule.dto'

/**
 * 积分规则 API 单测（axios-mock-adapter 挂载到真实 client 实例）
 *
 * 覆盖点：
 * - list 调用 GET /admin/points/rules 解包规则数组
 * - create / update 调用规则写接口，body 正确且自动携带 Idempotency-Key
 * - enable / disable 调用启停端点
 * - award 调用 POST /admin/points/award，AwardPointsDto 正确
 * - 规则编码重复（409）与用户不存在（业务错误）message 透出
 */
describe('08-membership-ops pointsRuleApi', () => {
  let mock: MockAdapter

  const fakeRules: PointsRuleDto[] = [
    {
      id: 'rule-0001',
      code: 'DAILY_CHECK_IN',
      name: '每日签到',
      actionType: 'DailyCheckIn',
      points: 5,
      dailyLimit: 1,
      status: 'Active',
      updatedAt: '2026-01-01T00:00:00.000Z',
    },
    {
      id: 'rule-0002',
      code: 'ORDER_COMPLETE',
      name: '下单得积分',
      actionType: 'OrderComplete',
      points: 10,
      dailyLimit: 3,
      status: 'Active',
      updatedAt: '2026-01-01T00:00:00.000Z',
    },
  ]

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

  it('list 调用 GET /admin/points/rules 并解包规则数组', async () => {
    mock.onGet('/admin/points/rules').reply(() => ok(fakeRules))

    const { data } = await pointsRuleApi.list()

    expect(data).toHaveLength(2)
    expect(data[0].code).toBe('DAILY_CHECK_IN')
    expect(data[1].actionType).toBe('OrderComplete')
    expect(mock.history.get.length).toBe(1)
    expect(mock.history.get[0].url).toBe('/admin/points/rules')
  })

  it('create 调用 POST /admin/points/rules，body 正确且携带 Idempotency-Key', async () => {
    mock.onPost('/admin/points/rules').reply(() => ok(fakeRules[0]))

    const body = {
      code: 'DAILY_CHECK_IN',
      name: '每日签到',
      actionType: 'DailyCheckIn' as const,
      points: 5,
      dailyLimit: 1,
      status: 'Active' as const,
    }
    const { data } = await pointsRuleApi.create(body)

    expect(data.id).toBe('rule-0001')
    const req = mock.history.post[0]
    expect(req.url).toBe('/admin/points/rules')
    expect(JSON.parse(req.data as string)).toEqual(body)
    expect(String(req.headers?.['Idempotency-Key'] ?? '')).not.toBe('')
  })

  it('update 调用 PUT /admin/points/rules/{ruleId} 并传 UpdatePointsRuleDto', async () => {
    mock
      .onPut('/admin/points/rules/rule-0001')
      .reply(() => ok({ ...fakeRules[0], points: 8 }))

    const body = {
      code: 'DAILY_CHECK_IN',
      name: '每日签到',
      actionType: 'DailyCheckIn' as const,
      points: 8,
      dailyLimit: 1,
      status: 'Active' as const,
    }
    const { data } = await pointsRuleApi.update('rule-0001', body)

    expect(data.points).toBe(8)
    const req = mock.history.put[0]
    expect(req.url).toBe('/admin/points/rules/rule-0001')
    expect(JSON.parse(req.data as string)).toEqual(body)
  })

  it('enable 调用 POST /admin/points/rules/{ruleId}/enable', async () => {
    mock.onPost('/admin/points/rules/rule-0001/enable').reply(() => ok(null))

    await pointsRuleApi.enable('rule-0001')

    expect(mock.history.post.length).toBe(1)
    expect(mock.history.post[0].url).toBe('/admin/points/rules/rule-0001/enable')
  })

  it('disable 调用 POST /admin/points/rules/{ruleId}/disable', async () => {
    mock.onPost('/admin/points/rules/rule-0002/disable').reply(() => ok(null))

    await pointsRuleApi.disable('rule-0002')

    expect(mock.history.post.length).toBe(1)
    expect(mock.history.post[0].url).toBe('/admin/points/rules/rule-0002/disable')
  })

  it('award 调用 POST /admin/points/award 并传 AwardPointsDto', async () => {
    mock.onPost('/admin/points/award').reply(() => ok(null))

    const body = {
      userId: 'U20240156',
      points: 100,
      reason: 'VIP 会员专属活动补偿积分',
    }
    await pointsRuleApi.award(body)

    expect(mock.history.post.length).toBe(1)
    const req = mock.history.post[0]
    expect(req.url).toBe('/admin/points/award')
    expect(JSON.parse(req.data as string)).toEqual(body)
    expect(String(req.headers?.['Idempotency-Key'] ?? '')).not.toBe('')
  })

  it('规则编码重复时返回 409 并透出后端 message', async () => {
    mock.onPost('/admin/points/rules').reply(409, { message: '规则编码已存在' })

    await expect(
      pointsRuleApi.create({
        code: 'DAILY_CHECK_IN',
        name: '每日签到',
        actionType: 'DailyCheckIn',
        points: 5,
        dailyLimit: 1,
        status: 'Active',
      }),
    ).rejects.toThrowError('规则编码已存在')
  })

  it('手动发放用户不存在时抛出后端 message', async () => {
    mock
      .onPost('/admin/points/award')
      .reply(200, { code: 40401, message: '用户不存在', data: null })

    await expect(
      pointsRuleApi.award({ userId: 'U-not-exist', points: 10, reason: '活动补偿积分' }),
    ).rejects.toThrowError('用户不存在')
  })
})
