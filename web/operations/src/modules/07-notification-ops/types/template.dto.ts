import type { PageQuery } from '@/shared/types'

/**
 * 通知模板域 DTO
 *
 * 与 Notification 域 AdminNotificationTemplatesController 契约对齐：
 * - 渠道枚举 NotificationChannel：Sms / Email / InApp / Push
 * - 模板编码 Code 全局唯一，创建后不可修改
 * - 模板标题 / 正文支持 {{变量}} 插值，变量须在 Variables 列表中定义
 */

/** 通知渠道（后端 NotificationChannel 枚举） */
export type NotificationChannel = 'Sms' | 'Email' | 'InApp' | 'Push'

/** 渠道展示元数据（md §6 渠道色：短信绿 / 邮件蓝 / 站内信紫 / 推送金） */
export const NOTIFICATION_CHANNEL_META: Record<
  NotificationChannel,
  { label: string; color: string }
> = {
  Sms: { label: '短信', color: 'green' },
  Email: { label: '邮件', color: 'blue' },
  InApp: { label: '站内信', color: 'purple' },
  Push: { label: 'Push', color: 'gold' },
}

/** 全部通知渠道（左侧渠道列表 / 筛选选项共用顺序） */
export const NOTIFICATION_CHANNELS: NotificationChannel[] = ['Sms', 'Email', 'InApp', 'Push']

/** 通知事件类型（筛选与模板表单共用） */
export type NotificationEventType = 'Order' | 'Payment' | 'Refund' | 'AfterSales' | 'Marketing'

/** 事件类型展示元数据（templates.html 事件标签配色） */
export const NOTIFICATION_EVENT_TYPE_META: Record<
  NotificationEventType,
  { label: string; color: string }
> = {
  Order: { label: '订单', color: 'blue' },
  Payment: { label: '支付', color: 'green' },
  Refund: { label: '退款', color: 'gold' },
  AfterSales: { label: '售后', color: 'red' },
  Marketing: { label: '营销', color: 'purple' },
}

/** 模板状态（Inactive ↔ Active 双向切换） */
export type NotificationTemplateStatus = 'Active' | 'Inactive'

/** 模板状态展示元数据 */
export const NOTIFICATION_TEMPLATE_STATUS_META: Record<
  NotificationTemplateStatus,
  { label: string; color: string }
> = {
  Active: { label: '启用', color: 'success' },
  Inactive: { label: '禁用', color: 'default' },
}

/** 模板变量定义（变量名 + 描述 + 示例值） */
export interface TemplateVariableDto {
  /** 变量名（模板中以 {{变量名}} 插值） */
  name: string
  /** 变量描述（运营可读） */
  description: string
  /** 示例值（预览默认填充） */
  example?: string
}

/** 通知模板（列表项与详情共用，详情含完整模板内容） */
export interface NotificationTemplateDto {
  templateId: string
  /** 模板编码（全局唯一，创建后不可修改，大写字母与下划线） */
  code: string
  name: string
  eventType: NotificationEventType
  channel: NotificationChannel
  /** 变量定义列表 */
  variables: TemplateVariableDto[]
  /** 标题模板（支持 {{变量}} 插值） */
  titleTemplate: string
  /** 正文模板（支持 {{变量}} 插值；短信渠道渲染后限 70 字） */
  bodyTemplate: string
  status: NotificationTemplateStatus
  updatedBy?: string
  updatedAt?: string
}

/** 创建 / 更新模板请求体（SaveNotificationTemplateDto） */
export interface SaveNotificationTemplateDto {
  code: string
  name: string
  eventType: NotificationEventType
  channel: NotificationChannel
  variables: TemplateVariableDto[]
  titleTemplate: string
  bodyTemplate: string
  status: NotificationTemplateStatus
}

/** 模板分页查询参数（eventType / channel / status / keyword + 分页） */
export interface TemplateQueryParams extends PageQuery {
  keyword?: string
  eventType?: NotificationEventType
  channel?: NotificationChannel
  status?: NotificationTemplateStatus
}

/** 模板预览请求体（PreviewTemplateDto：变量测试值字典） */
export interface PreviewTemplateDto {
  variables: Record<string, string>
}

/** 模板预览渲染结果（渲染后标题 + 正文） */
export interface TemplatePreviewResultDto {
  title: string
  body: string
}
