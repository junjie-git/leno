import { client } from '@/shared/http'
import type { BrowseHistoryDto } from '../types/profile.dto'

/**
 * 浏览历史 API（UserCenter 域接管，旧 UserAuth 双轨兜底）
 *
 * - GET    /users/me/browse-history              历史列表
 * - POST   /users/me/browse-history              上报浏览
 * - DELETE /users/me/browse-history/{id}         删除单条
 * - POST   /users/me/browse-history/batch-delete 批量删除
 * - DELETE /users/me/browse-history              清空
 *
 * 响应拦截器已解包 ApiResponse.data，调用方拿到的就是业务负载。
 */
export const historyApi = {
  /** 浏览历史列表（倒序） */
  list(): Promise<BrowseHistoryDto[]> {
    return client.get<BrowseHistoryDto[]>('/users/me/browse-history').then((r) => r.data)
  },

  /** 上报浏览（商品详情进入时调用，幂等去重） */
  report(spuId: string): Promise<null> {
    return client.post<null>('/users/me/browse-history', { spuId }).then((r) => r.data)
  },

  /** 删除单条历史 */
  remove(id: string): Promise<null> {
    return client.delete<null>(`/users/me/browse-history/${id}`).then((r) => r.data)
  },

  /** 批量删除历史 */
  batchRemove(ids: string[]): Promise<null> {
    return client.post<null>('/users/me/browse-history/batch-delete', { ids }).then((r) => r.data)
  },

  /** 清空历史 */
  clear(): Promise<null> {
    return client.delete<null>('/users/me/browse-history').then((r) => r.data)
  },
}
