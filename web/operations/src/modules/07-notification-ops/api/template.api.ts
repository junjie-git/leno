import type { AxiosResponse } from 'axios'
import { client, withIdempotency } from '@/shared/http'
import type { PageResult } from '@/shared/types'
import type {
  NotificationTemplateDto,
  PreviewTemplateDto,
  SaveNotificationTemplateDto,
  TemplatePreviewResultDto,
  TemplateQueryParams,
} from '../types/template.dto'

/**
 * 通知模板 API
 *
 * 与 Notification 域 AdminNotificationTemplatesController 对接（baseURL 已含 /api）。
 * 所有方法返回 AxiosResponse，调用方解构 .data 拿业务负载
 * （响应拦截器已完成 ApiResponse 信封解包）。
 */
export const templateApi = {
  /**
   * 分页查询模板（关键词 / 事件类型 / 渠道 / 状态组合筛选）
   */
  list(params: TemplateQueryParams): Promise<AxiosResponse<PageResult<NotificationTemplateDto>>> {
    return client.get<PageResult<NotificationTemplateDto>>('/admin/notification-templates', { params })
  },

  /**
   * 查询模板详情（编辑回填用）
   */
  detail(templateId: string): Promise<AxiosResponse<NotificationTemplateDto>> {
    return client.get<NotificationTemplateDto>(`/admin/notification-templates/${templateId}`)
  },

  /**
   * 创建模板（编码全局唯一，冲突返回 409）
   */
  create(body: SaveNotificationTemplateDto): Promise<AxiosResponse<NotificationTemplateDto>> {
    return client.post<NotificationTemplateDto>('/admin/notification-templates', body, withIdempotency())
  },

  /**
   * 更新模板（编码不可修改）
   */
  update(
    templateId: string,
    body: SaveNotificationTemplateDto,
  ): Promise<AxiosResponse<NotificationTemplateDto>> {
    return client.put<NotificationTemplateDto>(
      `/admin/notification-templates/${templateId}`,
      body,
      withIdempotency(),
    )
  },

  /**
   * 启用模板（该事件恢复发送通知）
   */
  enable(templateId: string): Promise<AxiosResponse<void>> {
    return client.post<void>(`/admin/notification-templates/${templateId}/enable`, null, withIdempotency())
  },

  /**
   * 停用模板（该事件不再发送通知）
   */
  disable(templateId: string): Promise<AxiosResponse<void>> {
    return client.post<void>(`/admin/notification-templates/${templateId}/disable`, null, withIdempotency())
  },

  /**
   * 预览模板渲染结果（传入变量测试值字典，返回渲染后标题 + 正文）
   */
  preview(
    templateId: string,
    body: PreviewTemplateDto,
  ): Promise<AxiosResponse<TemplatePreviewResultDto>> {
    return client.post<TemplatePreviewResultDto>(
      `/admin/notification-templates/${templateId}/preview`,
      body,
      withIdempotency(),
    )
  },
}
