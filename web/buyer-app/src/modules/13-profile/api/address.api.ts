import { client } from '@/shared/http'
import type { AddressDto, SaveAddressRequestDto } from '../types/profile.dto'

/**
 * 收货地址 API（UserCenter 域接管，旧 UserAuth 双轨兜底）
 *
 * - GET    /users/me/addresses             地址列表
 * - POST   /users/me/addresses             新增地址
 * - PUT    /users/me/addresses/{id}        修改地址
 * - DELETE /users/me/addresses/{id}        删除地址
 * - POST   /users/me/addresses/{id}/default 设为默认
 *
 * 响应拦截器已解包 ApiResponse.data，调用方拿到的就是业务负载。
 */
export const addressApi = {
  /** 地址列表（默认地址在前） */
  list(): Promise<AddressDto[]> {
    return client.get<AddressDto[]>('/users/me/addresses').then((r) => r.data)
  },

  /** 新增地址 */
  create(body: SaveAddressRequestDto): Promise<AddressDto> {
    return client.post<AddressDto>('/users/me/addresses', body).then((r) => r.data)
  },

  /** 修改地址 */
  update(id: string, body: SaveAddressRequestDto): Promise<AddressDto> {
    return client.put<AddressDto>(`/users/me/addresses/${id}`, body).then((r) => r.data)
  },

  /** 删除地址 */
  remove(id: string): Promise<null> {
    return client.delete<null>(`/users/me/addresses/${id}`).then((r) => r.data)
  },

  /** 设为默认地址 */
  setDefault(id: string): Promise<AddressDto> {
    return client.post<AddressDto>(`/users/me/addresses/${id}/default`).then((r) => r.data)
  },
}
