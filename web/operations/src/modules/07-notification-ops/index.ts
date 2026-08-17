/**
 * 07-notification-ops 通知运营模块出口
 *
 * - notificationOpsRoutes：路由聚合（供 app/router.ts 展开）
 * - templateApi / recordApi / notificationConfigApi / rateLimitApi / deadLetterApi：模块 API
 * - types：DTO 聚合再导出
 * - views：页面组件（懒加载路由引用，亦支持直接导入）
 */
export { default as notificationOpsRoutes } from './routes'

export { templateApi } from './api/template.api'
export { recordApi } from './api/record.api'
export { notificationConfigApi } from './api/config.api'
export { rateLimitApi } from './api/rateLimit.api'
export { deadLetterApi } from './api/deadLetter.api'

export type {
  NotificationChannel,
  NotificationEventType,
  NotificationTemplateDto,
  NotificationTemplateStatus,
  PreviewTemplateDto,
  SaveNotificationTemplateDto,
  TemplatePreviewResultDto,
  TemplateQueryParams,
  TemplateVariableDto,
} from './types/template.dto'

export type {
  NotificationRecordDto,
  NotificationRecordQueryParams,
  NotificationStatisticsDto,
  NotificationStatus,
  NotificationStatusTransitionDto,
} from './types/record.dto'

export type {
  NotificationConfigDto,
  NotificationConfigItemDto,
  SaveNotificationConfigDto,
  TestNotificationConfigDto,
  TestSendResultDto,
} from './types/config.dto'

export type {
  RateLimitConfigDto,
  RateLimitStatus,
  RateLimitUsageDto,
  SaveRateLimitConfigDto,
} from './types/rate-limit.dto'

export type {
  BatchDeadLetterDiscardDto,
  BatchDeadLetterResendDto,
  DeadLetterQueryParams,
  DeadLetterRecordDto,
  DeadLetterRetryAttemptDto,
  DeadLetterStatus,
  NotificationBatchResultDto,
} from './types/dead-letter.dto'

export { default as NotificationTemplates } from './views/Templates.vue'
export { default as NotificationRecords } from './views/Records.vue'
export { default as NotificationConfig } from './views/Config.vue'
export { default as NotificationRateLimits } from './views/RateLimits.vue'
export { default as DeadLetters } from './views/DeadLetters.vue'
