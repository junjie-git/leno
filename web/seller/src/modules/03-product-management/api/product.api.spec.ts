import { describe, expect, it, vi, beforeEach } from 'vitest'
import { productApi } from './product.api'
import { client, withIdempotency } from '@/shared/http'
import type { AxiosResponse } from 'axios'

/**
 * productApi 单元测试
 *
 * 验证每个 API 函数的 URL / method / params / headers。
 * client 响应拦截器已自动 unwrap ApiResponse.data，
 * 因此 mock client.get/post/put 时返回 AxiosResponse 形态（{ data: 业务对象 }），
 * api 函数内部通过 .then(r => r.data) 解包出业务对象，
 * 与 dashboard.api / auth.api 保持一致。
 */
vi.mock('@/shared/http', () => ({
  client: {
    get: vi.fn(),
    post: vi.fn(),
    put: vi.fn(),
  },
  withIdempotency: vi.fn(() => ({ headers: { 'Idempotency-Key': 'mock-key' } })),
}))

/** 构造 AxiosResponse 形态的 mock 返回值，data 字段即业务对象 */
function mockResponse<T>(data: T): AxiosResponse<T> {
  return { data } as AxiosResponse<T>
}

describe('productApi', () => {
  beforeEach(() => {
    vi.clearAllMocks()
    vi.mocked(withIdempotency).mockReturnValue({ headers: { 'Idempotency-Key': 'mock-key' } })
  })

  describe('list', () => {
    it('调用 GET /products 并透传查询参数', async () => {
      vi.mocked(client.get).mockResolvedValue(
        mockResponse({ items: [], total: 0, page: 1, pageSize: 10 }),
      )
      await productApi.list({ page: 1, pageSize: 10, keyword: 'T恤' })

      expect(client.get).toHaveBeenCalledWith('/products', {
        params: { page: 1, pageSize: 10, keyword: 'T恤' },
      })
    })

    it('透传 status 与 categoryId 筛选参数', async () => {
      vi.mocked(client.get).mockResolvedValue(
        mockResponse({ items: [], total: 0, page: 1, pageSize: 20 }),
      )
      await productApi.list({ status: 'Approved', categoryId: 'cat-1' })

      expect(client.get).toHaveBeenCalledWith('/products', {
        params: { status: 'Approved', categoryId: 'cat-1' },
      })
    })

    it('返回解包后的 PageResult', async () => {
      const page = { items: [{ id: 'p1' }], total: 1, page: 1, pageSize: 10 }
      vi.mocked(client.get).mockResolvedValue(mockResponse(page))
      const result = await productApi.list({})

      expect(result).toEqual(page)
    })
  })

  describe('get', () => {
    it('调用 GET /products/{id}', async () => {
      vi.mocked(client.get).mockResolvedValue(mockResponse({ id: 'p1', name: 'n' }))
      await productApi.get('p1')

      expect(client.get).toHaveBeenCalledWith('/products/p1')
    })
  })

  describe('create', () => {
    it('调用 POST /products 带 Idempotency-Key 头', async () => {
      vi.mocked(client.post).mockResolvedValue(mockResponse({ id: 'p1' }))
      const body = { name: 'T恤', categoryId: 'cat-1' }
      await productApi.create(body)

      expect(client.post).toHaveBeenCalledWith('/products', body, {
        headers: { 'Idempotency-Key': 'mock-key' },
      })
      expect(withIdempotency).toHaveBeenCalled()
    })
  })

  describe('update', () => {
    it('调用 PUT /products/{id} 带 Idempotency-Key 头与 version', async () => {
      vi.mocked(client.put).mockResolvedValue(mockResponse({ id: 'p1', version: 2 }))
      const body = { name: 'T恤2', version: 1 }
      await productApi.update('p1', body)

      expect(client.put).toHaveBeenCalledWith('/products/p1', body, {
        headers: { 'Idempotency-Key': 'mock-key' },
      })
      expect(withIdempotency).toHaveBeenCalled()
    })
  })

  describe('addSku', () => {
    it('调用 POST /products/{id}/skus 带 Idempotency-Key', async () => {
      vi.mocked(client.post).mockResolvedValue(mockResponse({ id: 'p1' }))
      const body = {
        skuCode: 'SKU-001',
        skuName: '白色/L',
        attributes: { 颜色: '白色', 尺码: 'L' },
        price: 29.9,
        stock: 100,
      }
      await productApi.addSku('p1', body)

      expect(client.post).toHaveBeenCalledWith('/products/p1/skus', body, {
        headers: { 'Idempotency-Key': 'mock-key' },
      })
      expect(withIdempotency).toHaveBeenCalled()
    })
  })

  describe('adjustPrice', () => {
    it('调用 POST /products/{id}/skus/{skuId}/price 带 Idempotency-Key', async () => {
      vi.mocked(client.post).mockResolvedValue(mockResponse({ id: 'p1' }))
      const body = { newPrice: 35.9, reason: '夏季促销' }
      await productApi.adjustPrice('p1', 's1', body)

      expect(client.post).toHaveBeenCalledWith(
        '/products/p1/skus/s1/price',
        body,
        { headers: { 'Idempotency-Key': 'mock-key' } },
      )
      expect(withIdempotency).toHaveBeenCalled()
    })
  })

  describe('submitForReview', () => {
    it('调用 POST /products/{id}/submit 带 Idempotency-Key 与 null body', async () => {
      vi.mocked(client.post).mockResolvedValue(mockResponse(null))
      await productApi.submitForReview('p1')

      expect(client.post).toHaveBeenCalledWith('/products/p1/submit', null, {
        headers: { 'Idempotency-Key': 'mock-key' },
      })
      expect(withIdempotency).toHaveBeenCalled()
    })
  })

  describe('takeDown', () => {
    it('调用 POST /products/{id}/take-down 带 Idempotency-Key 与 reason body', async () => {
      vi.mocked(client.post).mockResolvedValue(mockResponse(null))
      const body = { reason: '库存不足', version: 3 }
      await productApi.takeDown('p1', body)

      expect(client.post).toHaveBeenCalledWith('/products/p1/take-down', body, {
        headers: { 'Idempotency-Key': 'mock-key' },
      })
      expect(withIdempotency).toHaveBeenCalled()
    })
  })

  describe('republish', () => {
    it('调用 POST /products/{id}/republish 带 Idempotency-Key 与 null body', async () => {
      vi.mocked(client.post).mockResolvedValue(mockResponse(null))
      await productApi.republish('p1')

      expect(client.post).toHaveBeenCalledWith('/products/p1/republish', null, {
        headers: { 'Idempotency-Key': 'mock-key' },
      })
      expect(withIdempotency).toHaveBeenCalled()
    })
  })

  describe('getPriceHistory', () => {
    it('调用 GET /products/{id}/price-history 不带 skuId 时 params.skuId 为 undefined', async () => {
      vi.mocked(client.get).mockResolvedValue(mockResponse([]))
      await productApi.getPriceHistory('p1')

      expect(client.get).toHaveBeenCalledWith('/products/p1/price-history', {
        params: { skuId: undefined },
      })
    })

    it('调用 GET /products/{id}/price-history 带 skuId 参数', async () => {
      vi.mocked(client.get).mockResolvedValue(mockResponse([]))
      await productApi.getPriceHistory('p1', 's1')

      expect(client.get).toHaveBeenCalledWith('/products/p1/price-history', {
        params: { skuId: 's1' },
      })
    })

    it('返回解包后的价格变更记录数组', async () => {
      const records = [
        {
          id: 'r1',
          productId: 'p1',
          skuId: 's1',
          skuCode: 'SKU-001',
          skuName: '白色/L',
          oldPrice: 29.9,
          newPrice: 35.9,
          operator: '张老板',
          createdAt: '2026-07-26T14:30:00Z',
        },
      ]
      vi.mocked(client.get).mockResolvedValue(mockResponse(records))
      const result = await productApi.getPriceHistory('p1', 's1')

      expect(result).toEqual(records)
    })
  })
})
