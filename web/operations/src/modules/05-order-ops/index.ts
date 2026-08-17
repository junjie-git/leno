/**
 * 05-order-ops 订单运营模块出口
 *
 * - orderOpsRoutes：路由聚合（供 app/router.ts 展开）
 * - orderApi / afterSalesApi / reviewApi / logisticsApi：模块 API
 * - types：DTO 聚合再导出
 * - views：页面组件（懒加载路由引用，亦支持直接导入）
 */
export { default as orderOpsRoutes } from './routes'

export { orderApi, countOrdersByStatus } from './api/order.api'
export { afterSalesApi, countAfterSalesByStatus } from './api/afterSales.api'
export { reviewApi } from './api/review.api'
export { logisticsApi } from './api/logistics.api'

export type {
  ForceCancelOrderDto,
  LogisticsTrackNodeDto,
  OrderAddressDto,
  OrderDto,
  OrderLineDto,
  OrderPaymentDto,
  OrderQueryParams,
  OrderStatus,
  OrderStatusCountItem,
  OrderStatusHistoryDto,
} from './types/order.dto'
export { FORCE_CANCELLABLE_STATUSES, ORDER_STATUS_META } from './types/order.dto'

export type {
  AfterSalesDto,
  AfterSalesQueryParams,
  AfterSalesStatus,
  AfterSalesType,
  ApproveAfterSalesDto,
  NegotiationRecordDto,
  RejectAfterSalesDto,
} from './types/afterSales.dto'
export {
  AFTER_SALES_STATUS_META,
  AFTER_SALES_TYPE_META,
  AUDITABLE_AFTER_SALES_STATUSES,
  NEGOTIATION_ROLE_META,
} from './types/afterSales.dto'

export type {
  BatchReviewFailureDto,
  BatchReviewResultDto,
  ModerateReviewDto,
  ReviewDto,
  ReviewQueryParams,
  ReviewReasonCategory,
  ReviewStatus,
} from './types/review.dto'
export { REVIEW_REASON_CATEGORY_META, REVIEW_STATUS_META } from './types/review.dto'

export type {
  CreateLogisticsCompanyDto,
  LogisticsCompanyDto,
  LogisticsCompanyQueryParams,
  LogisticsCompanyStatus,
  SaveLogisticsCompanyDto,
  UpdateLogisticsCompanyDto,
} from './types/logistics.dto'

export { default as OrderManagement } from './views/OrderManagement.vue'
export { default as AfterSales } from './views/AfterSales.vue'
export { default as ReviewAudit } from './views/ReviewAudit.vue'
export { default as LogisticsCompanies } from './views/LogisticsCompanies.vue'
