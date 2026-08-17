import { describe, expect, it, beforeEach, afterEach } from 'vitest'
import MockAdapter from 'axios-mock-adapter'
import { client } from '@/shared/http'
import { productApi } from './product.api'
import type { ProductDto } from '../types/product.dto'

/**
 * 商品审核 API 单测（axios-mock-adapter 挂载到真实 client 实例）
 *
 * 覆盖点：
 * - list 调用 GET /admin/products/all，PageQuery 与业务筛选组合传参并解包 data
 * - approve / reject 调用对应 POST 端点，body 正确且自动携带 Idempotency-Key
 * - updateSkuStock / replenishSku 调用库存端点并解包 SKU 数据
 * - batchApprove / batchReject 前端串行循环并汇总 BatchOperationResultDto（含失败明细）
 */
describe('02-product-ops productApi', () => {
  let mock: MockAdapter

  const fakeSku = { id: 'sku-0001', spec: '黑色 / XL', price: 129.0, stock: 40 }

  const fakeProduct: ProductDto = {
    id: 'p-0001',
    title: '南极人秋冬保暖内衣套装',
    mainImageUrl: 'https://cdn.leno.com/p-0001-main.jpg',
    imageUrls: ['https://cdn.leno.com/p-0001-main.jpg'],
    status: 'PendingAudit',
    categoryId: 'cat-01',
    categoryName: '服饰',
    sellerId: 'SL2024088',
    sellerName: '南极人旗舰店',
    skus: [fakeSku],
    submittedAt: '2026-08-01T10:20:30.000Z',
    rejectReason: undefined,
    auditLogs: [
      { id: 'log-1', action: 'Submitted', operator: '南极人旗舰店', createdAt: '2026-08-01T10:20:30.000Z' },
    ],
  }

  const fakePage = { items: [fakeProduct], total: 1, page: 1, pageSize: 20 }

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

  it('list 调用 GET /admin/products/all 组合查询参数并解包 data', async () => {
    let capturedParams: Record<string, unknown> = {}
    mock.onGet('/admin/products/all').reply((config) => {
      capturedParams = (config.params ?? {}) as Record<string, unknown>
      return ok(fakePage)
    })

    const { data } = await productApi.list({
      page: 1,
      pageSize: 20,
      keyword: '南极人',
      sellerId: 'SL2024088',
      status: 'PendingAudit',
      categoryId: 'cat-01',
    })

    expect(data.items[0].id).toBe('p-0001')
    expect(data.total).toBe(1)
    expect(mock.history.get.length).toBe(1)
    expect(mock.history.get[0].url).toBe('/admin/products/all')
    expect(capturedParams).toMatchObject({
      page: 1,
      pageSize: 20,
      keyword: '南极人',
      sellerId: 'SL2024088',
      status: 'PendingAudit',
      categoryId: 'cat-01',
    })
  })

  it('list 仅传分页参数时也正常工作', async () => {
    mock.onGet('/admin/products/all').reply(() => ok(fakePage))

    const { data } = await productApi.list({ page: 2, pageSize: 10 })

    expect(data.page).toBe(1)
    expect(mock.history.get.length).toBe(1)
  })

  it('approve 调用 POST /admin/products/{id}/approve 并携带 Idempotency-Key', async () => {
    mock.onPost('/admin/products/p-0001/approve').reply(() => ok(null))

    await productApi.approve('p-0001')

    expect(mock.history.post.length).toBe(1)
    const req = mock.history.post[0]
    expect(req.url).toBe('/admin/products/p-0001/approve')
    expect(String(req.headers?.['Idempotency-Key'] ?? '')).not.toBe('')
  })

  it('reject 调用 POST /admin/products/{id}/reject 并传 ActionReasonDto', async () => {
    mock.onPost('/admin/products/p-0001/reject').reply(() => ok(null))

    await productApi.reject('p-0001', { reason: '主图涉嫌盗用他人素材' })

    expect(mock.history.post.length).toBe(1)
    const req = mock.history.post[0]
    expect(req.url).toBe('/admin/products/p-0001/reject')
    expect(JSON.parse(req.data as string)).toEqual({ reason: '主图涉嫌盗用他人素材' })
    expect(String(req.headers?.['Idempotency-Key'] ?? '')).not.toBe('')
  })

  it('updateSkuStock 调用 POST /admin/products/{id}/skus/{skuId}/stock 并解包 SKU', async () => {
    const updatedSku = { ...fakeSku, stock: 30 }
    mock.onPost('/admin/products/p-0001/skus/sku-0001/stock').reply(() => ok(updatedSku))

    const { data } = await productApi.updateSkuStock('p-0001', 'sku-0001', {
      delta: -10,
      reason: '运营后台人工调整',
    })

    expect(data?.stock).toBe(30)
    const req = mock.history.post[0]
    expect(req.url).toBe('/admin/products/p-0001/skus/sku-0001/stock')
    expect(JSON.parse(req.data as string)).toEqual({ delta: -10, reason: '运营后台人工调整' })
  })

  it('replenishSku 调用 POST /admin/products/skus/{skuId}/replenish 并解包 SKU', async () => {
    const updatedSku = { ...fakeSku, stock: 140 }
    mock.onPost('/admin/products/skus/sku-0001/replenish').reply(() => ok(updatedSku))

    const { data } = await productApi.replenishSku('sku-0001', { quantity: 100 })

    expect(data?.stock).toBe(140)
    const req = mock.history.post[0]
    expect(req.url).toBe('/admin/products/skus/sku-0001/replenish')
    expect(JSON.parse(req.data as string)).toEqual({ quantity: 100 })
  })

  it('batchApprove 串行调用单条接口并汇总成功结果', async () => {
    mock.onPost('/admin/products/p-0001/approve').reply(() => ok(null))
    mock.onPost('/admin/products/p-0002/approve').reply(() => ok(null))
    mock
      .onPost('/admin/products/p-0003/approve')
      .reply(200, { code: 40901, message: '商品状态已变更，请刷新列表', data: null })

    const result = await productApi.batchApprove(['p-0001', 'p-0002', 'p-0003'])

    expect(result.total).toBe(3)
    expect(result.succeeded).toBe(2)
    expect(result.failed).toBe(1)
    expect(result.failures).toEqual([{ id: 'p-0003', reason: '商品状态已变更，请刷新列表' }])
    expect(mock.history.post.length).toBe(3)
  })

  it('batchReject 复用同一驳回原因串行调用并汇总失败明细', async () => {
    mock.onPost('/admin/products/p-0001/reject').reply(() => ok(null))
    mock.onPost('/admin/products/p-0002/reject').reply(409, { message: '商品状态已变更，请刷新列表' })

    const result = await productApi.batchReject(['p-0001', 'p-0002'], { reason: '资质材料不完整，请补充后重新提交' })

    expect(result.total).toBe(2)
    expect(result.succeeded).toBe(1)
    expect(result.failed).toBe(1)
    expect(result.failures[0].id).toBe('p-0002')
    expect(result.failures[0].reason).toBe('商品状态已变更，请刷新列表')

    const bodies = mock.history.post.map((r) => JSON.parse(r.data as string))
    expect(bodies).toEqual([
      { reason: '资质材料不完整，请补充后重新提交' },
      { reason: '资质材料不完整，请补充后重新提交' },
    ])
  })

  it('list 业务错误（code !== 200）抛出 BusinessError', async () => {
    mock.onGet('/admin/products/all').reply(200, { code: 40301, message: '无商品查询权限', data: null })

    await expect(productApi.list({ page: 1, pageSize: 20 })).rejects.toThrowError('无商品查询权限')
  })
})
