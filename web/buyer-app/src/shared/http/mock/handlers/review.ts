import type MockAdapter from 'axios-mock-adapter'
import type { AppendReviewRequestDto, ReviewDto } from '@/modules/09-review/types/review.dto'
import { seedMyReviews, seedProductReviews, seedProductDetails } from '../data/seed'
import { fail, ok, paginate, parseBody, queryParams } from './helpers'

/**
 * 评价 handlers（Review 域）
 *
 * - GET  /reviews/mine（我的评价）
 * - POST /reviews/{reviewId}/append（追加评价）
 * - GET  /products/{spuId}/reviews（商品评价列表 + 摘要分布，匿名可访问）
 */

/** 评价筛选条件 */
type ReviewFilter = 'all' | 'withImage' | 'good' | 'bad'

/** 计算评分分布 */
function distribution(reviews: ReviewDto[]): Array<{ star: number; count: number }> {
  return [5, 4, 3, 2, 1].map((star) => ({
    star,
    count: reviews.filter((r) => r.rating === star).length,
  }))
}

export function registerReviewHandlers(mock: MockAdapter): void {
  // 我的评价
  mock.onGet('/reviews/mine').reply(() => ok(seedMyReviews))

  // 追加评价
  mock.onPost(/\/reviews\/[\w-]+\/append$/).reply((config) => {
    const reviewId = config.url?.match(/\/reviews\/([\w-]+)\/append$/)?.[1] ?? ''
    const review = seedMyReviews.find((r) => r.id === reviewId)
    if (!review) {
      return fail(40460, '评价不存在')
    }
    if (review.appendContent) {
      return fail(40461, '该评价已追评过，每条评价仅可追评一次')
    }
    const body = parseBody<AppendReviewRequestDto>(config.data)
    if (!body.content || body.content.trim().length < 3) {
      return fail(40462, '追评内容至少 3 个字')
    }
    review.appendContent = body.content
    review.appendAt = new Date().toISOString()
    return ok(review)
  })

  // 商品评价列表（匿名可访问）
  mock.onGet(/\/products\/spu-\d+\/reviews$/).reply((config) => {
    const spuId = config.url?.match(/\/products\/(spu-\d+)\/reviews$/)?.[1] ?? ''
    const detail = seedProductDetails.find((p) => p.id === spuId)
    if (!detail) {
      return fail(40401, '商品不存在或已下架')
    }
    // 商品维度评价 = 公开评价 + 我的评价（该商品）
    const mine = seedMyReviews.filter((r) => r.spuId === spuId)
    const others = seedProductReviews.filter((r) => r.spuId === spuId)
    let list = [...others, ...mine].sort(
      (a, b) => new Date(b.createdAt).getTime() - new Date(a.createdAt).getTime(),
    )

    const params = queryParams(config)
    const filter = (params.filter ?? 'all') as ReviewFilter
    switch (filter) {
      case 'withImage':
        list = list.filter((r) => r.images.length > 0)
        break
      case 'good':
        list = list.filter((r) => r.rating >= 4)
        break
      case 'bad':
        list = list.filter((r) => r.rating <= 2)
        break
      default:
        break
    }

    const count = others.length + mine.length
    const averageRating = count > 0 ? others.concat(mine).reduce((acc, r) => acc + r.rating, 0) / count : 5
    const goodRate = count > 0 ? Math.round((others.concat(mine).filter((r) => r.rating >= 4).length / count) * 100) : 100

    return ok({
      summary: {
        count,
        averageRating: Math.round(averageRating * 10) / 10,
        goodRate,
        distribution: distribution(others.concat(mine)),
      },
      ...paginate(list, Number(params.page ?? 1), Number(params.pageSize ?? 10)),
    })
  })
}
