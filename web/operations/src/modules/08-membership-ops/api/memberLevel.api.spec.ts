import { describe, expect, it, beforeEach, afterEach } from 'vitest'
import MockAdapter from 'axios-mock-adapter'
import { client } from '@/shared/http'
import { memberLevelApi } from './memberLevel.api'
import type { MemberLevelDto } from '../types/memberLevel.dto'

/**
 * 会员等级 API 单测（axios-mock-adapter 挂载到真实 client 实例）
 *
 * 覆盖点：
 * - list 调用 GET /admin/members/levels 解包数组
 * - create / update 调用管理端写接口，body 正确且自动携带 Idempotency-Key
 * - enable / disable 调用启停端点
 * - 门槛校验冲突（409）与业务错误（code !== 200）时 message 透出
 */
describe('08-membership-ops memberLevelApi', () => {
  let mock: MockAdapter

  const fakeLevels: MemberLevelDto[] = [
    {
      id: 'lv-0001',
      levelNo: 1,
      name: '普通会员',
      growthThreshold: 0,
      discountRate: 1,
      benefits: '基础权益',
      status: 'Active',
      memberCount: 48520,
      createdAt: '2026-01-01T00:00:00.000Z',
    },
    {
      id: 'lv-0002',
      levelNo: 2,
      name: '白银会员',
      growthThreshold: 1000,
      discountRate: 0.98,
      benefits: '98 折 + 积分加速 1.2x',
      status: 'Active',
      memberCount: 12830,
      createdAt: '2026-01-01T00:00:00.000Z',
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

  it('list 调用 GET /admin/members/levels 并解包等级数组', async () => {
    mock.onGet('/admin/members/levels').reply(() => ok(fakeLevels))

    const { data } = await memberLevelApi.list()

    expect(data).toHaveLength(2)
    expect(data[0].levelNo).toBe(1)
    expect(data[1].name).toBe('白银会员')
    expect(mock.history.get.length).toBe(1)
    expect(mock.history.get[0].url).toBe('/admin/members/levels')
  })

  it('create 调用 POST /admin/members/levels，body 正确且携带 Idempotency-Key', async () => {
    mock
      .onPost('/admin/members/levels')
      .reply(() => ok({ ...fakeLevels[1], id: 'lv-0003', levelNo: 3 }))

    const body = {
      name: '黄金会员',
      growthThreshold: 5000,
      discountRate: 0.95,
      benefits: '95 折 + 生日礼',
      status: 'Active' as const,
    }
    const { data } = await memberLevelApi.create(body)

    expect(data.levelNo).toBe(3)
    const req = mock.history.post[0]
    expect(req.url).toBe('/admin/members/levels')
    expect(JSON.parse(req.data as string)).toEqual(body)
    expect(String(req.headers?.['Idempotency-Key'] ?? '')).not.toBe('')
  })

  it('update 调用 PUT /admin/members/levels/{levelId} 并传 UpdateMemberLevelDto', async () => {
    mock
      .onPut('/admin/members/levels/lv-0002')
      .reply(() => ok({ ...fakeLevels[1], growthThreshold: 1200, discountRate: 0.96 }))

    const body = {
      name: '白银会员',
      growthThreshold: 1200,
      discountRate: 0.96,
      benefits: '96 折',
      status: 'Active' as const,
    }
    const { data } = await memberLevelApi.update('lv-0002', body)

    expect(data.growthThreshold).toBe(1200)
    expect(data.discountRate).toBe(0.96)
    const req = mock.history.put[0]
    expect(req.url).toBe('/admin/members/levels/lv-0002')
    expect(JSON.parse(req.data as string)).toEqual(body)
  })

  it('enable 调用 POST /admin/members/levels/{levelId}/enable', async () => {
    mock.onPost('/admin/members/levels/lv-0001/enable').reply(() => ok(null))

    await memberLevelApi.enable('lv-0001')

    expect(mock.history.post.length).toBe(1)
    expect(mock.history.post[0].url).toBe('/admin/members/levels/lv-0001/enable')
  })

  it('disable 调用 POST /admin/members/levels/{levelId}/disable', async () => {
    mock.onPost('/admin/members/levels/lv-0002/disable').reply(() => ok(null))

    await memberLevelApi.disable('lv-0002')

    expect(mock.history.post.length).toBe(1)
    expect(mock.history.post[0].url).toBe('/admin/members/levels/lv-0002/disable')
  })

  it('门槛不递增冲突时返回 409 并透出后端 message', async () => {
    mock
      .onPost('/admin/members/levels')
      .reply(409, { message: '成长值门槛须大于上一等级的 1000' })

    await expect(
      memberLevelApi.create({
        name: '青铜会员',
        growthThreshold: 500,
        discountRate: 0.99,
        status: 'Active',
      }),
    ).rejects.toThrowError('成长值门槛须大于上一等级的 1000')
  })

  it('业务错误（code !== 200）抛出后端 message', async () => {
    mock
      .onPut('/admin/members/levels/lv-0002')
      .reply(200, { code: 40021, message: '折扣率须优于上一等级', data: null })

    await expect(
      memberLevelApi.update('lv-0002', {
        name: '白银会员',
        growthThreshold: 1200,
        discountRate: 0.99,
        status: 'Active',
      }),
    ).rejects.toThrowError('折扣率须优于上一等级')
  })
})
