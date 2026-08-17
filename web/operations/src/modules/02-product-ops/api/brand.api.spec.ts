import { describe, expect, it, beforeEach, afterEach } from 'vitest'
import MockAdapter from 'axios-mock-adapter'
import { client } from '@/shared/http'
import { brandApi } from './brand.api'
import type { BrandDto } from '../types/brand.dto'

/**
 * 品牌管理 API 单测（axios-mock-adapter 挂载到真实 client 实例）
 *
 * 覆盖点：
 * - list 调用 GET /brands 并组合分页 / 关键词 / 状态参数解包
 * - get 调用 GET /brands/{id} 解包详情
 * - create / update 调用管理端写接口，body 正确且写操作自动携带 Idempotency-Key
 * - enable / disable 调用启停端点；停用被引用品牌时 409 错误 message 透出
 */
describe('02-product-ops brandApi', () => {
  let mock: MockAdapter

  const fakeBrand: BrandDto = {
    id: 'b-0001',
    name: '华为',
    englishName: 'HUAWEI',
    logoUrl: 'https://cdn.leno.com/brands/huawei.png',
    description: '华为终端品牌',
    sortOrder: 1,
    status: 'Active',
    createdBy: 'admin',
    createdAt: '2026-02-01T08:00:00.000Z',
  }

  const fakePage = { items: [fakeBrand], total: 1, page: 1, pageSize: 20 }

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

  it('list 调用 GET /brands 组合查询参数并解包 data', async () => {
    let capturedParams: Record<string, unknown> = {}
    mock.onGet('/brands').reply((config) => {
      capturedParams = (config.params ?? {}) as Record<string, unknown>
      return ok(fakePage)
    })

    const { data } = await brandApi.list({ page: 1, pageSize: 20, keyword: '华', status: 'Active' })

    expect(data.items[0].id).toBe('b-0001')
    expect(mock.history.get.length).toBe(1)
    expect(mock.history.get[0].url).toBe('/brands')
    expect(capturedParams).toMatchObject({ page: 1, pageSize: 20, keyword: '华', status: 'Active' })
  })

  it('get 调用 GET /brands/{id} 并解包详情', async () => {
    mock.onGet('/brands/b-0001').reply(() => ok(fakeBrand))

    const { data } = await brandApi.get('b-0001')

    expect(data.name).toBe('华为')
    expect(data.status).toBe('Active')
    expect(mock.history.get[0].url).toBe('/brands/b-0001')
  })

  it('create 调用 POST /admin/brands，body 正确且携带 Idempotency-Key', async () => {
    mock.onPost('/admin/brands').reply(() => ok(fakeBrand))

    const body = {
      name: '华为',
      englishName: 'HUAWEI',
      logoUrl: 'https://cdn.leno.com/brands/huawei.png',
      description: '华为终端品牌',
      sortOrder: 1,
      status: 'Active' as const,
    }
    const { data } = await brandApi.create(body)

    expect(data.id).toBe('b-0001')
    const req = mock.history.post[0]
    expect(req.url).toBe('/admin/brands')
    expect(JSON.parse(req.data as string)).toEqual(body)
    expect(String(req.headers?.['Idempotency-Key'] ?? '')).not.toBe('')
  })

  it('update 调用 PUT /admin/brands/{id} 并传 UpdateBrandDto', async () => {
    mock.onPut('/admin/brands/b-0001').reply(() => ok({ ...fakeBrand, sortOrder: 5 }))

    const body = {
      name: '华为',
      englishName: 'HUAWEI',
      sortOrder: 5,
      status: 'Active' as const,
    }
    const { data } = await brandApi.update('b-0001', body)

    expect(data.sortOrder).toBe(5)
    const req = mock.history.put[0]
    expect(req.url).toBe('/admin/brands/b-0001')
    expect(JSON.parse(req.data as string)).toEqual(body)
  })

  it('enable 调用 POST /admin/brands/{id}/enable', async () => {
    mock.onPost('/admin/brands/b-0001/enable').reply(() => ok(null))

    await brandApi.enable('b-0001')

    expect(mock.history.post.length).toBe(1)
    expect(mock.history.post[0].url).toBe('/admin/brands/b-0001/enable')
  })

  it('disable 调用 POST /admin/brands/{id}/disable', async () => {
    mock.onPost('/admin/brands/b-0001/disable').reply(() => ok(null))

    await brandApi.disable('b-0001')

    expect(mock.history.post.length).toBe(1)
    expect(mock.history.post[0].url).toBe('/admin/brands/b-0001/disable')
  })

  it('停用被商品引用的品牌返回 409 并透出后端 message', async () => {
    mock
      .onPost('/admin/brands/b-0001/disable')
      .reply(409, { message: '该品牌被 3 个商品引用，无法停用' })

    await expect(brandApi.disable('b-0001')).rejects.toThrowError('该品牌被 3 个商品引用，无法停用')
  })

  it('创建重名品牌抛出业务错误', async () => {
    mock.onPost('/admin/brands').reply(200, { code: 40011, message: '品牌名称已存在', data: null })

    await expect(
      brandApi.create({ name: '华为', sortOrder: 0, status: 'Active' }),
    ).rejects.toThrowError('品牌名称已存在')
  })
})
