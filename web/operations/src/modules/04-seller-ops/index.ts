/**
 * 04-seller-ops 卖家运营模块出口
 *
 * - sellerOpsRoutes：路由聚合（供 app/router.ts 展开）
 * - shopApi / fetchSellerStatsOverview：模块 API
 * - types：DTO 聚合再导出
 * - views：页面组件（懒加载路由引用，亦支持直接导入）
 * - components：模块内自建图表组件
 */
export { default as sellerOpsRoutes } from './routes'

export { shopApi } from './api/shop.api'
export { fetchSellerStatsOverview } from './api/sellerStats.api'

export type {
  ActionReasonDto,
  QualificationDto,
  QualificationStatus,
  QualificationType,
  SellerStatsCategoryDto,
  SellerStatsOverviewDto,
  SellerStatsQueryParams,
  SellerStatsSellerRowDto,
  SellerStatsTopShopDto,
  ShopDto,
  ShopQueryParams,
  ShopStatus,
} from './types/shop.dto'

export { default as ApplicationAudit } from './views/ApplicationAudit.vue'
export { default as ShopGovernance } from './views/ShopGovernance.vue'
export { default as SellerStatistics } from './views/SellerStatistics.vue'

export { default as ChartBarHorizontal } from './components/ChartBarHorizontal.vue'
export { default as ChartDonut } from './components/ChartDonut.vue'
