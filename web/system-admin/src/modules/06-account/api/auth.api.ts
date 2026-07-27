import { client, withIdempotency } from '@/shared/http'
import type {
  LoginDto,
  LoginResultDto,
  UserProfileResultDto,
  UpdateProfileDto,
  ChangePasswordDto,
} from './types/auth.dto'

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

  /**
   * 更新当前管理员个人资料（email / phone / nickname / avatar / remark）
   *
   * PUT /api/users/me，携带 Idempotency-Key 头防止重复提交。
   */
  updateProfile(body: UpdateProfileDto): Promise<UserProfileResultDto> {
    return client
      .put<UserProfileResultDto>('/users/me', body, withIdempotency())
      .then((r) => r.data)
  },

  /**
   * 修改当前管理员密码
   *
   * PUT /api/users/me/password，携带 Idempotency-Key 头防止重复提交。
   */
  changePassword(body: ChangePasswordDto): Promise<void> {
    return client
      .put<void>('/users/me/password', body, withIdempotency())
      .then((r) => r.data)
  },
}
