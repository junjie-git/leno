import { client } from '@/shared/http'
import type { FavoriteDto } from '../types/profile.dto'

/**
 * 收藏 API（UserCenter 域接管，旧 UserAuth 双轨兜底）
 *
 * - GET    /users/me/favorites             收藏列表
 * - POST   /users/me/favorites             新增收藏
 * - DELETE /users/me/favorites/{spuId}     取消收藏
 * - POST   /users/me/favorites/batch-delete 批量取消收藏
 * - GET    /users/me/favorites/count       收藏数量
 *
 * 响应拦截器已解包 ApiResponse.data，调用方拿到的就是业务负载。
 */
export const favoriteApi = {
  /** 收藏列表 */
  list(): Promise<FavoriteDto[]> {
    return client.get<FavoriteDto[]>('/users/me/favorites').then((r) => r.data)
  },

  /** 新增收藏（重复收藏幂等） */
  add(spuId: string): Promise<null> {
    return client.post<null>('/users/me/favorites', { spuId }).then((r) => r.data)
  },

  /** 取消收藏 */
  remove(spuId: string): Promise<null> {
    return client.delete<null>(`/users/me/favorites/${spuId}`).then((r) => r.data)
  },

  /** 批量取消收藏 */
  batchRemove(spuIds: string[]): Promise<null> {
    return client.post<null>('/users/me/favorites/batch-delete', { spuIds }).then((r) => r.data)
  },

  /** 收藏数量 */
  count(): Promise<number> {
    return client.get<number>('/users/me/favorites/count').then((r) => r.data)
  },
}
