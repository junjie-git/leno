import { describe, expect, it, beforeEach, afterEach } from 'vitest'
import MockAdapter from 'axios-mock-adapter'
import { client } from '@/shared/http'
import { categoryApi } from './category.api'
import type { CategoryDto } from '../types/category.dto'

/**
 * 分类管理 API 单测（axios-mock-adapter 挂载到真实 client 实例）
 *
 * 覆盖点：
 * - tree 调用 GET /categories/tree，keyword 参数透传并解包树形数组
 * - get 调用 GET /categories/{id} 解包详情（含 productCount）
 * - create / update 调用管理端写接口，body 正确且写操作携带 Idempotency-Key
 * - enable / disable 调用启停端点；停用冲突时 409 错误 message 透出
 */
describe('02-product-ops categoryApi', () => {
  let mock: MockAdapter

  const fakeLeaf: CategoryDto = {
    id: 'cat-0102',
    name: '手机',
    parentId: 'cat-01',
    level: 2,
    icon: 'mobile',
    sortOrder: 1,
    status: 'Active',
    children: [],
    productCount: 1256,
  }

  const fakeRoot: CategoryDto = {
    id: 'cat-01',
    name: '数码',
    parentId: null,
    level: 1,
    icon: 'digital',
    sortOrder: 1,
    status: 'Active',
    children: [fakeLeaf],
    productCount: 2106,
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

  it('tree 调用 GET /categories/tree 并解包树形数组', async () => {
    mock.onGet('/categories/tree').reply(() => ok([fakeRoot]))

    const { data } = await categoryApi.tree()

    expect(data.length).toBe(1)
    expect(data[0].children?.[0].id).toBe('cat-0102')
    expect(mock.history.get.length).toBe(1)
    expect(mock.history.get[0].url).toBe('/categories/tree')
  })

  it('tree 透传 keyword 参数（后端返回匹配节点及祖先链）', async () => {
    let capturedParams: Record<string, unknown> = {}
    mock.onGet('/categories/tree').reply((config) => {
      capturedParams = (config.params ?? {}) as Record<string, unknown>
      return ok([fakeRoot])
    })

    const { data } = await categoryApi.tree({ keyword: '手机' })

    expect(data[0].name).toBe('数码')
    expect(capturedParams).toEqual({ keyword: '手机' })
  })

  it('get 调用 GET /categories/{id} 解包详情（含 productCount）', async () => {
    mock.onGet('/categories/cat-0102').reply(() => ok(fakeLeaf))

    const { data } = await categoryApi.get('cat-0102')

    expect(data.name).toBe('手机')
    expect(data.productCount).toBe(1256)
    expect(mock.history.get[0].url).toBe('/categories/cat-0102')
  })

  it('create 调用 POST /admin/categories，body 正确且携带 Idempotency-Key', async () => {
    mock.onPost('/admin/categories').reply(() => ok(fakeLeaf))

    const body = {
      parentId: 'cat-01',
      name: '手机',
      icon: 'mobile',
      sortOrder: 1,
      status: 'Active' as const,
    }
    const { data } = await categoryApi.create(body)

    expect(data.id).toBe('cat-0102')
    const req = mock.history.post[0]
    expect(req.url).toBe('/admin/categories')
    expect(JSON.parse(req.data as string)).toEqual(body)
    expect(String(req.headers?.['Idempotency-Key'] ?? '')).not.toBe('')
  })

  it('update 调用 PUT /admin/categories/{id} 并传 UpdateCategoryDto', async () => {
    mock.onPut('/admin/categories/cat-0102').reply(() => ok({ ...fakeLeaf, name: '智能手机' }))

    const body = {
      parentId: 'cat-01',
      name: '智能手机',
      icon: 'mobile',
      sortOrder: 2,
      status: 'Active' as const,
    }
    const { data } = await categoryApi.update('cat-0102', body)

    expect(data.name).toBe('智能手机')
    const req = mock.history.put[0]
    expect(req.url).toBe('/admin/categories/cat-0102')
    expect(JSON.parse(req.data as string)).toEqual(body)
  })

  it('enable 调用 POST /admin/categories/{id}/enable', async () => {
    mock.onPost('/admin/categories/cat-0102/enable').reply(() => ok(null))

    await categoryApi.enable('cat-0102')

    expect(mock.history.post.length).toBe(1)
    expect(mock.history.post[0].url).toBe('/admin/categories/cat-0102/enable')
  })

  it('disable 调用 POST /admin/categories/{id}/disable', async () => {
    mock.onPost('/admin/categories/cat-0102/disable').reply(() => ok(null))

    await categoryApi.disable('cat-0102')

    expect(mock.history.post.length).toBe(1)
    expect(mock.history.post[0].url).toBe('/admin/categories/cat-0102/disable')
  })

  it('停用含启用子分类的分类返回 409 并透出后端 message', async () => {
    mock.onPost('/admin/categories/cat-01/disable').reply(409, { message: '请先停用或删除子分类' })

    await expect(categoryApi.disable('cat-01')).rejects.toThrowError('请先停用或删除子分类')
  })

  it('停用被商品引用的分类返回 409 并透出后端 message', async () => {
    mock
      .onPost('/admin/categories/cat-0102/disable')
      .reply(409, { message: '该分类被 12 个商品引用，无法停用' })

    await expect(categoryApi.disable('cat-0102')).rejects.toThrowError('该分类被 12 个商品引用，无法停用')
  })

  it('创建同级重名分类抛出业务错误', async () => {
    mock.onPost('/admin/categories').reply(200, { code: 40021, message: '同级下已存在同名分类', data: null })

    await expect(
      categoryApi.create({ parentId: 'cat-01', name: '手机', sortOrder: 0, status: 'Active' }),
    ).rejects.toThrowError('同级下已存在同名分类')
  })
})
