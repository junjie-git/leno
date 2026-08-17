/**
 * 06-payment-ops 支付运营模块出口
 *
 * - paymentOpsRoutes：路由聚合（供 app/router.ts 展开）
 * - paymentApi / refundApi / channelApi / reconciliationApi：模块 API
 * - types：DTO 聚合再导出
 * - views：页面组件（懒加载路由引用，亦支持直接导入）
 */
export { default as paymentOpsRoutes } from './routes'

export { paymentApi } from './api/payment.api'
export { refundApi } from './api/refund.api'
export { channelApi } from './api/channel.api'
export { reconciliationApi } from './api/reconciliation.api'

export type {
  PaymentCallbackLogDto,
  PaymentChannelParams,
  PaymentChannelType,
  PaymentDto,
  PaymentListResultDto,
  PaymentQueryParams,
  PaymentStatus,
  PaymentTimelineItemDto,
} from './types/payment.dto'

export type {
  RefundChannelWriteBack,
  RefundDto,
  RefundListResultDto,
  RefundQueryParams,
  RefundStatus,
  RefundTimelineItemDto,
} from './types/refund.dto'

export type {
  ChannelConfigItemDto,
  ChannelConfigStatus,
  UpdateChannelConfigDto,
} from './types/channel.dto'

export type {
  DiffTimelineItemDto,
  ReconciliationDiffDto,
  ReconciliationDiffListResultDto,
  ReconciliationDiffQueryParams,
  ReconciliationDiffStatus,
  ReconciliationDiffType,
} from './types/reconciliation.dto'

export { default as PaymentRecords } from './views/PaymentRecords.vue'
export { default as RefundRecords } from './views/RefundRecords.vue'
export { default as PaymentChannels } from './views/PaymentChannels.vue'
export { default as Reconciliation } from './views/Reconciliation.vue'
