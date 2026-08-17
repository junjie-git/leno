import { describe, expect, it, beforeEach, afterEach } from 'vitest'
import MockAdapter from 'axios-mock-adapter'
import { client } from '@/shared/http'
import { logisticsApi } from './logistics.api'
import type { LogisticsCompanyDto } from '../types/logistics.dto'

/**
 * 物流公司管理 API 单测（axios-mock-adapter 挂载到真实 client 实例）
 *
 * 覆盖点：
 * - list 调用 GET /admin/logistics-companies 组合 keyword / status / 分页参数并解包 data
 * - create 调用 POST /admin/logistics-companies，body 正确且携带 Idempotency-Key
 * - update 调用 PUT /admin/logistics-companies/{id} 并传 UpdateLogisticsCompanyDto
 * - enable / disable 调用启停端点；公司代码重复 409 错误 message 透出
 */
describe('05-order-ops logisticsApi', () => {
  let mock: MockAdapter

  const fakeCompany: LogisticsCompanyDto = {
    id: 'lc-0001',
    name: '顺丰速运',
    code: 'SF',
    logoUrl: 'https://cdn.leno.com/logistics/sf.svg',
    phone: '95338',
    website: 'https://www.sf-express.com',
    sortOrder: 1,
    status: 'Active',
    createdAt: '2026-01-15T08:00:00.000Z',
  }

  const fakePage = { items: [fakeCompany], total: 1, page: 1, pageSize: 20 }

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

  it('list 调用 GET /admin/logistics-companies 组合查询参数并解包 data', async () => {
    let capturedParams: Record<string, unknown> = {}
    mock.onGet('/admin/logistics-companies').reply((config) => {
      capturedParams = (config.params ?? {}) as Record<string, unknown>
      return ok(fakePage)
    })

    const { data } = await logisticsApi.list({ page: 1, pageSize: 20, keyword: '顺丰', status: 'Active' })

    expect(data.items[0].id).toBe('lc-0001')
    expect(mock.history.get.length).toBe(1)
    expect(mock.history.get[0].url).toBe('/admin/logistics-companies')
    expect(capturedParams).toMatchObject({ page: 1, pageSize: 20, keyword: '顺丰', status: 'Active' })
  })

  it('list 仅传分页参数时也正常工作', async () => {
    mock.onGet('/admin/logistics-companies').reply(() => ok(fakePage))

    const { data } = await logisticsApi.list({ page: 2, pageSize: 10 })

    expect(data.total).toBe(1)
    expect(mock.history.get.length).toBe(1)
  })

  it('create 调用 POST /admin/logistics-companies，body 正确且携带 Idempotency-Key', async () => {
    mock.onPost('/admin/logistics-companies').reply(() => ok(fakeCompany))

    const body = {
      name: '顺丰速运',
      code: 'SF',
      phone: '95338',
      website: 'https://www.sf-express.com',
      sortOrder: 1,
      status: 'Active' as const,
    }
    const { data } = await logisticsApi.create(body)

    expect(data.id).toBe('lc-0001')
    const req = mock.history.post[0]
    expect(req.url).toBe('/admin/logistics-companies')
    expect(JSON.parse(req.data as string)).toEqual(body)
    expect(String(req.headers?.['Idempotency-Key'] ?? '')).not.toBe('')
  })

  it('update 调用 PUT /admin/logistics-companies/{id} 并传 UpdateLogisticsCompanyDto', async () => {
    mock.onPut('/admin/logistics-companies/lc-0001').reply(() => ok({ ...fakeCompany, sortOrder: 5 }))

    const body = {
      name: '顺丰速运',
      code: 'SF',
      sortOrder: 5,
      status: 'Active' as const,
    }
    const { data } = await logisticsApi.update('lc-0001', body)

    expect(data.sortOrder).toBe(5)
    const req = mock.history.put[0]
    expect(req.url).toBe('/admin/logistics-companies/lc-0001')
    expect(JSON.parse(req.data as string)).toEqual(body)
  })

  it('enable 调用 POST /admin/logistics-companies/{id}/enable', async () => {
    mock.onPost('/admin/logistics-companies/lc-0001/enable').reply(() => ok(null))

    await logisticsApi.enable('lc-0001')

    expect(mock.history.post.length).toBe(1)
    expect(mock.history.post[0].url).toBe('/admin/logistics-companies/lc-0001/enable')
  })

  it('disable 调用 POST /admin/logistics-companies/{id}/disable', async () => {
    mock.onPost('/admin/logistics-companies/lc-0001/disable').reply(() => ok(null))

    await logisticsApi.disable('lc-0001')

    expect(mock.history.post.length).toBe(1)
    expect(mock.history.post[0].url).toBe('/admin/logistics-companies/lc-0001/disable')
  })

  it('创建重复公司代码返回 409 并透出后端 message', async () => {
    mock
      .onPost('/admin/logistics-companies')
      .reply(409, { message: '公司代码已存在' })

    await expect(
      logisticsApi.create({ name: '顺丰速运', code: 'SF', sortOrder: 1, status: 'Active' }),
    ).rejects.toThrowError('公司代码已存在')
  })

  it('list 业务错误（code !== 200）抛出 BusinessError', async () => {
    mock
      .onGet('/admin/logistics-companies')
      .reply(200, { code: 40301, message: '无物流公司查询权限', data: null })

    await expect(logisticsApi.list({ page: 1, pageSize: 20 })).rejects.toThrowError('无物流公司查询权限')
  })
})
