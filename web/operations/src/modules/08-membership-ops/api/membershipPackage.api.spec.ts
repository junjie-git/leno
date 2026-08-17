import { describe, expect, it, beforeEach, afterEach } from 'vitest'
import MockAdapter from 'axios-mock-adapter'
import { client } from '@/shared/http'
import { membershipPackageApi } from './membershipPackage.api'
import type { MembershipPackageDto } from '../types/membershipPackage.dto'

/**
 * 会员套餐 API 单测（axios-mock-adapter 挂载到真实 client 实例）
 *
 * 覆盖点：
 * - list 调用 GET /membership-packages 并组合 status 过滤参数
 * - create / update 调用管理端写接口，body 正确且自动携带 Idempotency-Key
 * - enable / disable 调用启停端点
 * - 关联等级未启用等业务错误（409 / code !== 200）message 透出
 */
describe('08-membership-ops membershipPackageApi', () => {
  let mock: MockAdapter

  const fakePackage: MembershipPackageDto = {
    id: 'pkg-0001',
    name: '月度会员',
    price: 30,
    durationDays: 30,
    linkedLevelId: 'lv-0002',
    linkedLevelName: '白银会员',
    benefits: ['ExclusiveService', 'Discount'],
    subscriberCount: 156,
    status: 'Active',
    createdAt: '2026-01-01T00:00:00.000Z',
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

  it('list 调用 GET /membership-packages 并透传 status 过滤参数', async () => {
    let capturedParams: Record<string, unknown> = {}
    mock.onGet('/membership-packages').reply((config) => {
      capturedParams = (config.params ?? {}) as Record<string, unknown>
      return ok([fakePackage])
    })

    const { data } = await membershipPackageApi.list({ status: 'Active' })

    expect(data).toHaveLength(1)
    expect(data[0].name).toBe('月度会员')
    expect(mock.history.get[0].url).toBe('/membership-packages')
    expect(capturedParams).toMatchObject({ status: 'Active' })
  })

  it('list 不传参数时 query 为空', async () => {
    mock.onGet('/membership-packages').reply((config) => {
      expect(config.params).toBeUndefined()
      return ok([])
    })

    const { data } = await membershipPackageApi.list()

    expect(data).toEqual([])
  })

  it('create 调用 POST /admin/membership-packages，body 正确且携带 Idempotency-Key', async () => {
    mock.onPost('/admin/membership-packages').reply(() => ok(fakePackage))

    const body = {
      name: '月度会员',
      price: 30,
      durationDays: 30,
      linkedLevelId: 'lv-0002',
      benefits: ['ExclusiveService', 'Discount'] as const,
      status: 'Active' as const,
    }
    const { data } = await membershipPackageApi.create(body)

    expect(data.id).toBe('pkg-0001')
    const req = mock.history.post[0]
    expect(req.url).toBe('/admin/membership-packages')
    expect(JSON.parse(req.data as string)).toEqual({
      name: '月度会员',
      price: 30,
      durationDays: 30,
      linkedLevelId: 'lv-0002',
      benefits: ['ExclusiveService', 'Discount'],
      status: 'Active',
    })
    expect(String(req.headers?.['Idempotency-Key'] ?? '')).not.toBe('')
  })

  it('update 调用 PUT /admin/membership-packages/{packageId} 并传 UpdateMembershipPackageDto', async () => {
    mock
      .onPut('/admin/membership-packages/pkg-0001')
      .reply(() => ok({ ...fakePackage, price: 88, durationDays: 90 }))

    const body = {
      name: '季度会员',
      price: 88,
      durationDays: 90,
      linkedLevelId: 'lv-0002',
      benefits: ['Discount'] as const,
      status: 'Active' as const,
    }
    const { data } = await membershipPackageApi.update('pkg-0001', body)

    expect(data.price).toBe(88)
    expect(data.durationDays).toBe(90)
    const req = mock.history.put[0]
    expect(req.url).toBe('/admin/membership-packages/pkg-0001')
    expect(JSON.parse(req.data as string)).toEqual({
      name: '季度会员',
      price: 88,
      durationDays: 90,
      linkedLevelId: 'lv-0002',
      benefits: ['Discount'],
      status: 'Active',
    })
  })

  it('enable 调用 POST /admin/membership-packages/{packageId}/enable', async () => {
    mock.onPost('/admin/membership-packages/pkg-0001/enable').reply(() => ok(null))

    await membershipPackageApi.enable('pkg-0001')

    expect(mock.history.post.length).toBe(1)
    expect(mock.history.post[0].url).toBe('/admin/membership-packages/pkg-0001/enable')
  })

  it('disable 调用 POST /admin/membership-packages/{packageId}/disable', async () => {
    mock.onPost('/admin/membership-packages/pkg-0001/disable').reply(() => ok(null))

    await membershipPackageApi.disable('pkg-0001')

    expect(mock.history.post.length).toBe(1)
    expect(mock.history.post[0].url).toBe('/admin/membership-packages/pkg-0001/disable')
  })

  it('关联等级未启用时返回 409 并透出后端 message', async () => {
    mock
      .onPost('/admin/membership-packages')
      .reply(409, { message: '关联会员等级未启用' })

    await expect(
      membershipPackageApi.create({
        name: '月度会员',
        price: 30,
        durationDays: 30,
        linkedLevelId: 'lv-0009',
        benefits: ['Discount'],
        status: 'Active',
      }),
    ).rejects.toThrowError('关联会员等级未启用')
  })

  it('价格非法等业务错误（code !== 200）抛出后端 message', async () => {
    mock
      .onPut('/admin/membership-packages/pkg-0001')
      .reply(200, { code: 40031, message: '价格须大于 0', data: null })

    await expect(
      membershipPackageApi.update('pkg-0001', {
        name: '月度会员',
        price: 0,
        durationDays: 30,
        linkedLevelId: 'lv-0002',
        benefits: ['Discount'],
        status: 'Active',
      }),
    ).rejects.toThrowError('价格须大于 0')
  })
})
