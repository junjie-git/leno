import type { AxiosResponse } from 'axios'
import { client, withIdempotency } from '@/shared/http'
import type { PageResult } from '@/shared/types'
import type {
  NotificationRecordDto,
  NotificationRecordQueryParams,
  NotificationStatisticsDto,
} from '../types/record.dto'

/**
 * 通知记录 API
 *
 * 与 Notification 域通知记录查询 / 死信重发端点对接（baseURL 已含 /api）：
 * - 列表与详情走 /notifications/records（运营查询端点）
 * - 重发与统计走 /admin/notifications（管理端点，重发需幂等保护）
 */
export const recordApi = {
  /**
   * 多维度分页查询通知记录
   *
   * 支持 userId / channel / status / templateCode / businessRef / 时间范围组合筛选。
   */
  list(
    params: NotificationRecordQueryParams,
  ): Promise<AxiosResponse<PageResult<NotificationRecordDto>>> {
    return client.get<PageResult<NotificationRecordDto>>('/notifications/records', { params })
  },

  /**
   * 获取通知记录详情（含渲染正文、渠道返回与状态时间线）
   */
  detail(id: string): Promise<AxiosResponse<NotificationRecordDto>> {
    return client.get<NotificationRecordDto>(`/notifications/records/${id}`)
  },

  /**
   * 按业务引用查询关联通知记录（如同一订单的全部通知）
   */
  byBusiness(businessRef: string): Promise<AxiosResponse<NotificationRecordDto[]>> {
    return client.get<NotificationRecordDto[]>(`/notifications/records/by-business/${encodeURIComponent(businessRef)}`)
  },

  /**
   * 手工重发死信通知记录（状态重置为 Pending，由 DispatchJob 接管实际发送）
   */
  resend(id: string): Promise<AxiosResponse<void>> {
    return client.post<void>(`/admin/notifications/records/${id}/resend`, null, withIdempotency())
  },

  /**
   * 获取送达率统计（各状态计数 + 送达率）
   */
  statistics(): Promise<AxiosResponse<NotificationStatisticsDto>> {
    return client.get<NotificationStatisticsDto>('/admin/notifications/statistics')
  },
}
