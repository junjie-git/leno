import { client } from '@/shared/http'
import type { AnnouncementDto, DictionaryDto } from '../types/public.dto'

/**
 * 公共 API（SystemAdmin BC 公开端点）
 *
 * - GET /announcements      公告列表
 * - GET /dictionaries/{code} 数据字典
 *
 * 响应拦截器已解包 ApiResponse.data，调用方拿到的就是业务负载。
 */
export const publicApi = {
  /** 公告列表（置顶在前，倒序） */
  listAnnouncements(): Promise<AnnouncementDto[]> {
    return client.get<AnnouncementDto[]>('/announcements').then((r) => r.data)
  },

  /** 按编码查询数据字典 */
  getDictionary(code: string): Promise<DictionaryDto> {
    return client.get<DictionaryDto>(`/dictionaries/${code}`).then((r) => r.data)
  },
}
