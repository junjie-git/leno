import { describe, expect, it, beforeEach, afterEach } from 'vitest'
import MockAdapter from 'axios-mock-adapter'
import { client } from '@/shared/http'
import { reviewApi } from './review.api'
import type { ReviewDto } from '../types/review.dto'

/**
 * 评价审核 API 单测（axios-mock-adapter 挂载到真实 client 实例）
 *
 * 覆盖点：
 * - list 调用 GET /admin/reviews，商品名 / 状态 / 评分 / 时间 / 分页组合传参并解包 data
 * - approve 调用 POST /admin/reviews/{id}/approve（隐藏可逆，可重新通过）
 * - hide 调用 POST /admin/reviews/{id}/hide 并传 ModerateReviewDto（reasonCategory 枚举）
 * - batchApprove / batchHide 前端串行循环并汇总 BatchReviewResultDto（含失败明细）
 */
describe('05-order-ops reviewApi', () => {
  let mock: MockAdapter

  const fakeReview: ReviewDto = {
    id: 'r-0001',
    content: '质量很好，物流也快，包装完整无破损，值得回购！',
    rating: 5,
    imageUrls: ['https://cdn.leno.com/r-0001-1.jpg'],
    productId: 'p-0001',
    productName: '南极人秋冬保暖内衣套装',
    userId: 'U20240345',
    buyerName: '王雪',
    sellerReply: '感谢您的支持，欢迎再次选购',
    sellerRepliedAt: '2026-08-02T10:00:00.000Z',
    status: 'Pending',
    createdAt: '2026-08-01T11:20:30.000Z',
  }

  const fakePage = { items: [fakeReview], total: 1, page: 1, pageSize: 20 }

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

  it('list 调用 GET /admin/reviews 组合查询参数并解包 data', async () => {
    let capturedParams: Record<string, unknown> = {}
    mock.onGet('/admin/reviews').reply((config) => {
      capturedParams = (config.params ?? {}) as Record<string, unknown>
      return ok(fakePage)
    })

    const { data } = await reviewApi.list({
      page: 1,
      pageSize: 20,
      productName: '南极人',
      status: 'Pending',
      rating: 5,
      fromTime: '2026-08-01T00:00:00.000Z',
      toTime: '2026-08-31T23:59:59.000Z',
    })

    expect(data.items[0].id).toBe('r-0001')
    expect(data.total).toBe(1)
    expect(mock.history.get.length).toBe(1)
    expect(mock.history.get[0].url).toBe('/admin/reviews')
    expect(capturedParams).toMatchObject({
      page: 1,
      pageSize: 20,
      productName: '南极人',
      status: 'Pending',
      rating: 5,
      fromTime: '2026-08-01T00:00:00.000Z',
      toTime: '2026-08-31T23:59:59.000Z',
    })
  })

  it('list 仅传分页参数时也正常工作', async () => {
    mock.onGet('/admin/reviews').reply(() => ok(fakePage))

    const { data } = await reviewApi.list({ page: 2, pageSize: 10 })

    expect(data.total).toBe(1)
    expect(mock.history.get.length).toBe(1)
  })

  it('approve 调用 POST /admin/reviews/{id}/approve 并携带 Idempotency-Key', async () => {
    mock.onPost('/admin/reviews/r-0001/approve').reply(() => ok(null))

    await reviewApi.approve('r-0001')

    expect(mock.history.post.length).toBe(1)
    const req = mock.history.post[0]
    expect(req.url).toBe('/admin/reviews/r-0001/approve')
    expect(String(req.headers?.['Idempotency-Key'] ?? '')).not.toBe('')
  })

  it('hide 调用 POST /admin/reviews/{id}/hide 并传 ModerateReviewDto', async () => {
    mock.onPost('/admin/reviews/r-0001/hide').reply(() => ok(null))

    await reviewApi.hide('r-0001', { reasonCategory: 'Spam', remark: '评价内容含广告链接' })

    expect(mock.history.post.length).toBe(1)
    const req = mock.history.post[0]
    expect(req.url).toBe('/admin/reviews/r-0001/hide')
    expect(JSON.parse(req.data as string)).toEqual({
      reasonCategory: 'Spam',
      remark: '评价内容含广告链接',
    })
    expect(String(req.headers?.['Idempotency-Key'] ?? '')).not.toBe('')
  })

  it('hide 缺省 remark 时仅传 reasonCategory', async () => {
    mock.onPost('/admin/reviews/r-0001/hide').reply(() => ok(null))

    await reviewApi.hide('r-0001', { reasonCategory: 'Other' })

    const req = mock.history.post[0]
    expect(JSON.parse(req.data as string)).toEqual({ reasonCategory: 'Other' })
  })

  it('batchApprove 串行调用单条接口并汇总成功 / 失败结果', async () => {
    mock.onPost('/admin/reviews/r-0001/approve').reply(() => ok(null))
    mock.onPost('/admin/reviews/r-0002/approve').reply(() => ok(null))
    mock
      .onPost('/admin/reviews/r-0003/approve')
      .reply(200, { code: 40901, message: '评价状态已变更，请刷新列表', data: null })

    const result = await reviewApi.batchApprove(['r-0001', 'r-0002', 'r-0003'])

    expect(result.total).toBe(3)
    expect(result.succeeded).toBe(2)
    expect(result.failed).toBe(1)
    expect(result.failures).toEqual([{ id: 'r-0003', reason: '评价状态已变更，请刷新列表' }])
    expect(mock.history.post.length).toBe(3)
  })

  it('batchHide 复用同一隐藏原因串行调用并汇总失败明细', async () => {
    mock.onPost('/admin/reviews/r-0001/hide').reply(() => ok(null))
    mock.onPost('/admin/reviews/r-0002/hide').reply(409, { message: '评价状态已变更，请刷新列表' })

    const result = await reviewApi.batchHide(['r-0001', 'r-0002'], {
      reasonCategory: 'Abuse',
      remark: '评价内容含辱骂言论',
    })

    expect(result.total).toBe(2)
    expect(result.succeeded).toBe(1)
    expect(result.failed).toBe(1)
    expect(result.failures[0].id).toBe('r-0002')
    expect(result.failures[0].reason).toBe('评价状态已变更，请刷新列表')

    const bodies = mock.history.post.map((r) => JSON.parse(r.data as string))
    expect(bodies).toEqual([
      { reasonCategory: 'Abuse', remark: '评价内容含辱骂言论' },
      { reasonCategory: 'Abuse', remark: '评价内容含辱骂言论' },
    ])
  })

  it('list 业务错误（code !== 200）抛出 BusinessError', async () => {
    mock.onGet('/admin/reviews').reply(200, { code: 40301, message: '无评价查询权限', data: null })

    await expect(reviewApi.list({ page: 1, pageSize: 20 })).rejects.toThrowError('无评价查询权限')
  })
})
