import { client } from '@/shared/http'
import type { LoginDto, LoginResultDto, UserProfileResultDto } from './types/auth.dto'

/**
 * 鉴权 API
 *
 * 与 Identity 域 AuthController / UsersController 对接：
 * - POST /api/auth/login    账号密码登录
 * - POST /api/auth/logout   登出（best-effort）
 * - GET  /api/users/me      当前管理员 profile 与权限
 *
 * 响应拦截器已解包 ApiResponse.data，调用方拿到的就是业务负载。
 */
export const authApi = {
  /**
   * 账号密码登录
   *
   * @param body 用户名 + 密码
   * @returns token / expiresIn / user / roles / permissions
   */
  login(body: LoginDto): Promise<LoginResultDto> {
    return client.post<LoginResultDto>('/auth/login', body).then((r) => r.data)
  },

  /**
   * 登出（best-effort，失败由 store 吞掉）
   */
  logout(): Promise<void> {
    return client.post<void>('/auth/logout', null).then(() => undefined)
  },

  /**
   * 拉取当前管理员 profile 与权限
   */
  getProfile(): Promise<UserProfileResultDto> {
    return client.get<UserProfileResultDto>('/users/me').then((r) => r.data)
  },
}
