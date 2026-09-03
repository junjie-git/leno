import { client } from '@/shared/http'
import type {
  BuyerProfileDto,
  ChangePasswordRequestDto,
  UpdateProfileRequestDto,
} from '../types/profile.dto'

/**
 * 个人资料 API（Identity 域接管）
 *
 * - GET  /users/me          个人资料
 * - PUT  /users/me          更新资料
 * - PUT  /users/me/password 修改密码
 *
 * 响应拦截器已解包 ApiResponse.data，调用方拿到的就是业务负载。
 */
export const profileApi = {
  /** 个人资料 */
  getProfile(): Promise<BuyerProfileDto> {
    return client.get<BuyerProfileDto>('/users/me').then((r) => r.data)
  },

  /** 更新资料 */
  updateProfile(body: UpdateProfileRequestDto): Promise<BuyerProfileDto> {
    return client.put<BuyerProfileDto>('/users/me', body).then((r) => r.data)
  },

  /** 修改密码 */
  changePassword(body: ChangePasswordRequestDto): Promise<null> {
    return client.put<null>('/users/me/password', body).then((r) => r.data)
  },
}
