import { client } from '@/shared/http'
import type { PageResult } from '@/shared/types'
import { shopApi } from './shop.api'
import type {
  SellerStatsCategoryDto,
  SellerStatsOverviewDto,
  SellerStatsQueryParams,
  SellerStatsSellerRowDto,
  SellerStatsTopShopDto,
  ShopDto,
  ShopQueryParams,
} from '../types/shop.dto'

/**
 * 卖家统计降级聚合 API
 *
 * 后端暂无独立聚合端点（规划中的 GET /api/admin/seller-statistics/overview 未上线），
 * 前端基于两个共享端点二次聚合：
 * - GET /admin/dashboard/shop-ranking（start/end）：Top50 店铺 GMV / 订单排行
 * - GET /admin/shops（pageSize=100 分页循环至 total，上限 5 页防失控）：状态分布 / 评分 / 类目
 *
 * 两个端点 Promise.all 并行请求，任一失败整体抛错，由页面 error 态呈现。
 */

/** 店铺拉取分页大小 */
const SHOPS_PAGE_SIZE = 100
/** 店铺拉取最大页数（防止后端 total 异常导致请求失控，最多聚合 500 家） */
const MAX_SHOPS_PAGES = 5
/** Top 卖家柱状图取前 N 名 */
const TOP_SHOPS_LIMIT = 10
/** 待治理评分阈值（低于该值标记 needsGovernance） */
const GOVERNANCE_RATING_THRESHOLD = 4.0

/** shop_ranking Metric 数组元素（snake_case，与后端 DashboardReportDto 约定一致） */
interface ShopRankingMetricItemDto {
  shop_id: string
  shop_name: string
  seller_account: string
  gmv: number
  order_count: number
}

/** dashboard 报表结构（仅声明本模块用到的字段，避免跨模块依赖） */
interface ShopRankingReportDto {
  ReportType: string
  Metrics: { Key: string; Value: unknown }[]
}

/** 从报表 Metrics 中提取数组（缺失或非数组返回空数组） */
function getArrayMetric<T>(report: ShopRankingReportDto, key: string): T[] {
  const metric = report.Metrics.find((m) => m.Key === key)
  return Array.isArray(metric?.Value) ? (metric.Value as T[]) : []
}

/**
 * 分页拉取全量店铺（状态分布 / 评分 / 类目聚合数据源）
 *
 * pageSize=100 循环至 total，上限 MAX_SHOPS_PAGES 页；任一页失败即抛错。
 */
async function fetchAllShops(category?: string): Promise<PageResult<ShopDto>> {
  const items: ShopDto[] = []
  let total = 0

  for (let page = 1; page <= MAX_SHOPS_PAGES; page += 1) {
    const params: ShopQueryParams = { page, pageSize: SHOPS_PAGE_SIZE }
    if (category) params.category = category

    const { data } = await shopApi.list(params)
    total = data.total
    items.push(...data.items)

    if (data.items.length === 0 || items.length >= total) break
  }

  return { items, total, page: 1, pageSize: SHOPS_PAGE_SIZE }
}

/** 判断时间是否落在 [start, end] 区间（无效时间视为不在区间内） */
function isWithinRange(value: string, start: string, end: string): boolean {
  const time = new Date(value).getTime()
  if (Number.isNaN(time)) return false
  return time >= new Date(start).getTime() && time <= new Date(end).getTime()
}

/** 类目分布聚合（按卖家数降序） */
function aggregateCategoryDistribution(shops: ShopDto[]): SellerStatsCategoryDto[] {
  const counter = new Map<string, number>()
  for (const shop of shops) {
    const category = shop.mainCategory || '未分类'
    counter.set(category, (counter.get(category) ?? 0) + 1)
  }
  return [...counter.entries()]
    .map(([category, count]) => ({ category, count }))
    .sort((a, b) => b.count - a.count)
}

/**
 * 卖家统计总览聚合
 *
 * 输出口径：
 * - 卖家总数：店铺列表 total（类目筛选时为该类目 total）
 * - 活跃卖家数：status === 'Active' 计数
 * - 新增卖家数：createdAt 落在 [start, end] 内计数
 * - 平均评分：店铺 rating 均值（1 位小数；无有效评分返回 0）
 * - Top10 卖家：shop_ranking 按 GMV 降序取前 10
 * - 明细行：店铺列表为基，按 shopId 关联排行 GMV（未上榜为 0），rating < 4.0 标记待治理
 */
export async function fetchSellerStatsOverview(
  params: SellerStatsQueryParams,
): Promise<SellerStatsOverviewDto> {
  const [rankingResponse, shopsPage] = await Promise.all([
    client.get<ShopRankingReportDto>('/admin/dashboard/shop-ranking', {
      params: { start: params.start, end: params.end },
    }),
    fetchAllShops(params.category),
  ])

  const rankingItems = getArrayMetric<ShopRankingMetricItemDto>(rankingResponse.data, 'shop_ranking')
  const shops = shopsPage.items

  const gmvByShopId = new Map<string, ShopRankingMetricItemDto>()
  for (const item of rankingItems) {
    gmvByShopId.set(item.shop_id, item)
  }

  const topShops: SellerStatsTopShopDto[] = rankingItems
    .slice()
    .sort((a, b) => b.gmv - a.gmv)
    .slice(0, TOP_SHOPS_LIMIT)
    .map((item) => ({
      shopId: item.shop_id,
      shopName: item.shop_name,
      sellerAccount: item.seller_account,
      gmv: item.gmv,
      orderCount: item.order_count,
    }))

  const validRatings = shops
    .map((shop) => shop.rating)
    .filter((rating) => typeof rating === 'number' && Number.isFinite(rating))
  const avgRating = validRatings.length
    ? Number((validRatings.reduce((sum, r) => sum + r, 0) / validRatings.length).toFixed(1))
    : 0

  const items: SellerStatsSellerRowDto[] = shops.map((shop) => ({
    shopId: shop.id,
    name: shop.name,
    sellerAccount: shop.sellerAccount,
    category: shop.mainCategory,
    status: shop.status,
    productCount: shop.productCount,
    orderCount: shop.orderCount,
    gmv: gmvByShopId.get(shop.id)?.gmv ?? 0,
    rating: shop.rating,
    needsGovernance: shop.rating < GOVERNANCE_RATING_THRESHOLD,
  }))

  return {
    totalSellers: shopsPage.total,
    activeSellers: shops.filter((shop) => shop.status === 'Active').length,
    newSellers: shops.filter((shop) => isWithinRange(shop.createdAt, params.start, params.end))
      .length,
    avgRating,
    topShops,
    categoryDistribution: aggregateCategoryDistribution(shops),
    items,
  }
}
