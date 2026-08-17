import { describe, expect, it, beforeEach, afterEach } from 'vitest'
import MockAdapter from 'axios-mock-adapter'
import { client } from '@/shared/http'
import { fetchSellerStatsOverview } from './sellerStats.api'
import type { ShopDto } from '../types/shop.dto'

/**
 * 卖家统计降级聚合单测
 *
 * 覆盖点：
 * - 并行请求 shop-ranking 与 /admin/shops，聚合输出总数 / 活跃 / 新增 / 均分 / Top10 / 类目分布 / 明细行
 * - 明细行按 shopId 关联排行 GMV，rating < 4.0 标记待治理
 * - 多页拉取循环至 total；上限 5 页防失控
 * - category 参数透传给店铺列表
 * - 任一端点失败整体抛错
 */
describe('04-seller-ops sellerStatsApi（降级聚合）', () => {
  let mock: MockAdapter

  const START = '2026-07-01T00:00:00.000Z'
  const END = '2026-07-31T23:59:59.000Z'

  function makeShop(overrides: Partial<ShopDto> & { id: string }): ShopDto {
    return {
      name: `店铺${overrides.id}`,
      ownerName: '张伟',
      sellerAccount: `seller-${overrides.id}`,
      contactPhone: '138-0000-0000',
      mainCategory: '数码电器',
      productCount: 100,
      orderCount: 1000,
      rating: 4.5,
      status: 'Active',
      submittedAt: '2026-06-01T00:00:00.000Z',
      createdAt: '2026-06-01T00:00:00.000Z',
      qualifications: undefined,
      ...overrides,
    }
  }

  function okShopsPage(items: ShopDto[], total: number, page: number) {
    return [
      200,
      { code: 200, message: 'OK', data: { items, total, page, pageSize: 100 } },
    ] as const
  }

  function okRanking(
    items: {
      shop_id: string
      shop_name: string
      seller_account: string
      gmv: number
      order_count: number
    }[],
  ) {
    return [
      200,
      {
        code: 200,
        message: 'OK',
        data: {
          ReportId: 'report-1',
          ReportType: 'ShopRanking',
          PeriodStart: START,
          PeriodEnd: END,
          Granularity: 'Day',
          GeneratedAt: '2026-08-01T00:00:00.000Z',
          Metrics: [{ Key: 'shop_ranking', Value: items }],
        },
      },
    ] as const
  }

  beforeEach(() => {
    mock = new MockAdapter(client)
    localStorage.clear()
  })

  afterEach(() => {
    mock.restore()
  })

  it('并行拉取两端点并输出完整聚合结果', async () => {
    const shopA = makeShop({ id: 'shop-a', rating: 4.9, createdAt: '2026-07-10T08:00:00.000Z' })
    const shopB = makeShop({
      id: 'shop-b',
      name: '小米官方旗舰店',
      mainCategory: '数码电器',
      status: 'Suspended',
      rating: 3.6,
      createdAt: '2026-05-01T08:00:00.000Z',
    })
    const shopC = makeShop({ id: 'shop-c', mainCategory: '服饰鞋包', rating: 4.4 })

    mock.onGet('/admin/dashboard/shop-ranking').reply(() =>
      okRanking([
        { shop_id: 'shop-b', shop_name: '小米官方旗舰店', seller_account: 'seller-shop-b', gmv: 900, order_count: 90 },
        { shop_id: 'shop-a', shop_name: '店铺shop-a', seller_account: 'seller-shop-a', gmv: 1200, order_count: 120 },
      ]),
    )
    mock.onGet('/admin/shops').reply((config) => {
      const params = (config.params ?? {}) as Record<string, unknown>
      expect(params.page).toBe(1)
      return okShopsPage([shopA, shopB, shopC], 3, 1)
    })

    const overview = await fetchSellerStatsOverview({ start: START, end: END })

    expect(overview.totalSellers).toBe(3)
    expect(overview.activeSellers).toBe(2)
    // 仅 shopA 的 createdAt 落在 7 月区间
    expect(overview.newSellers).toBe(1)
    // (4.9 + 3.6 + 4.4) / 3 = 4.3
    expect(overview.avgRating).toBe(4.3)
    // Top 按 GMV 降序
    expect(overview.topShops.map((s) => s.shopId)).toEqual(['shop-a', 'shop-b'])
    expect(overview.topShops[0].gmv).toBe(1200)
    // 类目分布降序：数码电器 2、服饰鞋包 1
    expect(overview.categoryDistribution).toEqual([
      { category: '数码电器', count: 2 },
      { category: '服饰鞋包', count: 1 },
    ])
    // 明细行：GMV 关联 + 待治理标记
    expect(overview.items).toHaveLength(3)
    const rowB = overview.items.find((row) => row.shopId === 'shop-b')
    expect(rowB?.gmv).toBe(900)
    expect(rowB?.needsGovernance).toBe(true)
    const rowC = overview.items.find((row) => row.shopId === 'shop-c')
    expect(rowC?.gmv).toBe(0)
    expect(rowC?.needsGovernance).toBe(false)
  })

  it('店铺数超过单页容量时循环拉取至 total', async () => {
    const page1 = Array.from({ length: 100 }, (_, i) => makeShop({ id: `shop-p1-${i}` }))
    const page2 = Array.from({ length: 100 }, (_, i) => makeShop({ id: `shop-p2-${i}` }))
    const page3 = Array.from({ length: 50 }, (_, i) => makeShop({ id: `shop-p3-${i}` }))

    let shopRequestCount = 0
    mock.onGet('/admin/dashboard/shop-ranking').reply(() => okRanking([]))
    mock.onGet('/admin/shops').reply((config) => {
      shopRequestCount += 1
      const params = (config.params ?? {}) as Record<string, unknown>
      if (params.page === 1) return okShopsPage(page1, 250, 1)
      if (params.page === 2) return okShopsPage(page2, 250, 2)
      return okShopsPage(page3, 250, 3)
    })

    const overview = await fetchSellerStatsOverview({ start: START, end: END })

    expect(shopRequestCount).toBe(3)
    expect(overview.totalSellers).toBe(250)
    expect(overview.items).toHaveLength(250)
  })

  it('total 异常放大时最多拉取 5 页防失控', async () => {
    const fullPage = Array.from({ length: 100 }, (_, i) => makeShop({ id: `shop-${i}` }))

    let shopRequestCount = 0
    mock.onGet('/admin/dashboard/shop-ranking').reply(() => okRanking([]))
    mock.onGet('/admin/shops').reply(() => {
      shopRequestCount += 1
      return okShopsPage(fullPage, 999999, shopRequestCount)
    })

    const overview = await fetchSellerStatsOverview({ start: START, end: END })

    expect(shopRequestCount).toBe(5)
    expect(overview.items).toHaveLength(500)
    // total 以后端分页元数据为准
    expect(overview.totalSellers).toBe(999999)
  })

  it('category 参数透传给店铺列表端点', async () => {
    let capturedParams: Record<string, unknown> = {}
    mock.onGet('/admin/dashboard/shop-ranking').reply(() => okRanking([]))
    mock.onGet('/admin/shops').reply((config) => {
      capturedParams = (config.params ?? {}) as Record<string, unknown>
      return okShopsPage([], 0, 1)
    })

    const overview = await fetchSellerStatsOverview({ start: START, end: END, category: '服饰鞋包' })

    expect(capturedParams).toMatchObject({ page: 1, pageSize: 100, category: '服饰鞋包' })
    expect(overview.totalSellers).toBe(0)
    expect(overview.avgRating).toBe(0)
    expect(overview.items).toEqual([])
    expect(overview.categoryDistribution).toEqual([])
  })

  it('shop-ranking 端点失败时整体抛错', async () => {
    mock.onGet('/admin/dashboard/shop-ranking').reply(500, { message: 'internal error' })
    mock.onGet('/admin/shops').reply(() => okShopsPage([], 0, 1))

    await expect(fetchSellerStatsOverview({ start: START, end: END })).rejects.toThrowError()
  })

  it('店铺列表端点失败时整体抛错', async () => {
    mock.onGet('/admin/dashboard/shop-ranking').reply(() => okRanking([]))
    mock.onGet('/admin/shops').reply(200, { code: 40301, message: '无店铺查询权限', data: null })

    await expect(fetchSellerStatsOverview({ start: START, end: END })).rejects.toThrowError(
      '无店铺查询权限',
    )
  })
})
