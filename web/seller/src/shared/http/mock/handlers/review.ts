/* eslint-disable @typescript-eslint/no-explicit-any */
import type MockAdapter from 'axios-mock-adapter'
import { loadSeedData, saveSeedData } from '../data/seed'

/**
 * 评价 handler 注册
 *
 * 端点（baseURL=/api，故拦截 /seller/reviews/...）：
 * - GET  /seller/reviews          评价列表（分页 + 筛选）
 * - GET  /seller/reviews/{id}      评价详情
 * - POST /seller/reviews/{id}/reply 回复评价（覆盖式编辑）
 */
export function registerReviewHandlers(mock: MockAdapter): void {
  // 评价列表（分页 + 筛选）
  mock.onGet('/seller/reviews').reply((config) => {
    const seed = loadSeedData()
    const params = config.params || {}
    let items = [...(seed.reviews as any[])]

    // 评分筛选
    if (params.rating !== undefined && params.rating !== null && params.rating !== '') {
      items = items.filter((r) => r.rating === Number(params.rating))
    }
    // 回复状态筛选
    if (params.replied !== undefined && params.replied !== null && params.replied !== '') {
      const replied = params.replied === true || params.replied === 'true'
      items = items.filter((r) => !!r.sellerReplyContent === replied)
    }
    // 商品名称筛选（模糊匹配）
    if (params.productName) {
      const kw = String(params.productName).toLowerCase()
      items = items.filter((r) => (r.productName || '').toLowerCase().includes(kw))
    }
    // 时间范围筛选
    if (params.startDate) {
      items = items.filter((r) => new Date(r.submittedAt) >= new Date(params.startDate))
    }
    if (params.endDate) {
      items = items.filter((r) => new Date(r.submittedAt) <= new Date(params.endDate))
    }

    const page = Number(params.page) || 1
    const pageSize = Number(params.pageSize) || 20
    const total = items.length
    const start = (page - 1) * pageSize
    const paged = items.slice(start, start + pageSize)

    return [
      200,
      { code: 200, message: 'OK', data: { items: paged, total, page, pageSize } },
    ]
  })

  // 评价详情
  mock.onGet(/\/seller\/reviews\/[^/]+$/).reply((config) => {
    const id = config.url!.split('/').pop()!
    const seed = loadSeedData()
    const review = (seed.reviews as any[]).find((r) => r.reviewId === id)
    if (!review) {
      return [200, { code: 40400, message: `评价 ${id} 不存在`, data: null }]
    }
    return [200, { code: 200, message: 'OK', data: review }]
  })

  // 回复评价（覆盖式编辑）
  mock.onPost(/\/seller\/reviews\/[^/]+\/reply$/).reply((config) => {
    const id = config.url!.split('/')[3]
    const seed = loadSeedData()
    const review = (seed.reviews as any[]).find((r) => r.reviewId === id)
    if (!review) {
      return [200, { code: 40400, message: `评价 ${id} 不存在`, data: null }]
    }
    const body = JSON.parse(config.data || '{}')
    if (!body.content || body.content.trim().length === 0) {
      return [200, { code: 40001, message: '回复内容不能为空', data: null }]
    }
    if (body.content.length > 500) {
      return [200, { code: 40001, message: '回复内容不超过 500 字', data: null }]
    }
    review.sellerReplyContent = body.content
    review.sellerReplyBy = 'seller-001'
    review.sellerReplyAt = new Date().toISOString()
    saveSeedData(seed)
    return [200, { code: 200, message: 'OK', data: review }]
  })
}
