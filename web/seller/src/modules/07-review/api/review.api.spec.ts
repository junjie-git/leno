import { describe, expect, it, vi, beforeEach } from 'vitest'
import type { AxiosResponse } from 'axios'
import { reviewApi } from './review.api'
import { http, withIdempotency } from '@/shared/http'

/**
 * reviewApi 单元测试
 *
 * client 响应拦截器已 unwrap ApiResponse.data，故 mock http 方法返回
 * AxiosResponse 形态（{ data: 业务对象 }），api 函数内部 .then(r => r.data) 解包。
 */
vi.mock('@/shared/http', () => ({
  http: {
    get: vi.fn(),
    post: vi.fn(),
  },
  withIdempotency: vi.fn(() => ({ headers: { 'Idempotency-Key': 'mock-key' } })),
}))

function mockResponse<T>(data: T): AxiosResponse<T> {
  return { data } as AxiosResponse<T>
}

describe('reviewApi', () => {
  beforeEach(() => {
    vi.clearAllMocks()
    vi.mocked(withIdempotency).mockReturnValue({
      headers: { 'Idempotency-Key': 'mock-key' },
    })
  })

  describe('list', () => {
    it('调用 GET /seller/reviews 并透传查询参数', async () => {
      vi.mocked(http.get).mockResolvedValue(
        mockResponse({ items: [], total: 0, page: 1, pageSize: 20 }),
      )
      const params = { page: 1, pageSize: 20, rating: 5, replied: false }
      await reviewApi.list(params)

      expect(http.get).toHaveBeenCalledWith('/seller/reviews', { params })
    })

    it('返回解包后的评价列表结果', async () => {
      const result = {
        items: [{ reviewId: 'rev-001', rating: 5, content: '好评', images: [], status: 'Approved' }],
        total: 1,
        page: 1,
        pageSize: 20,
      }
      vi.mocked(http.get).mockResolvedValue(mockResponse(result))
      const res = await reviewApi.list({ page: 1, pageSize: 20 })
      expect(res).toEqual(result)
    })
  })

  describe('get', () => {
    it('调用 GET /seller/reviews/{id}', async () => {
      vi.mocked(http.get).mockResolvedValue(
        mockResponse({ reviewId: 'rev-001', rating: 5, content: '好评', images: [], status: 'Approved' }),
      )
      await reviewApi.get('rev-001')
      expect(http.get).toHaveBeenCalledWith('/seller/reviews/rev-001')
    })
  })

  describe('reply', () => {
    it('调用 POST /seller/reviews/{id}/reply 带 Idempotency-Key', async () => {
      vi.mocked(http.post).mockResolvedValue(
        mockResponse({ reviewId: 'rev-001', rating: 5, content: '好评', images: [], status: 'Approved', sellerReplyContent: '感谢支持' }),
      )
      const body = { content: '感谢支持' }
      await reviewApi.reply('rev-001', body)

      expect(http.post).toHaveBeenCalledWith('/seller/reviews/rev-001/reply', body, {
        headers: { 'Idempotency-Key': 'mock-key' },
      })
      expect(withIdempotency).toHaveBeenCalled()
    })

    it('返回解包后的评价详情（含回复）', async () => {
      const review = {
        reviewId: 'rev-001',
        rating: 5,
        content: '好评',
        images: [],
        status: 'Approved',
        sellerReplyContent: '感谢支持',
        sellerReplyAt: '2026-07-30T10:00:00Z',
      }
      vi.mocked(http.post).mockResolvedValue(mockResponse(review))
      const result = await reviewApi.reply('rev-001', { content: '感谢支持' })
      expect(result).toEqual(review)
    })
  })
})
