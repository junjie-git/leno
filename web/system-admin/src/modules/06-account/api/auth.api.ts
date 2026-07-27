import { client } from '@/shared/http'
import type { AdminUserDto, LoginDto, LoginResultDto } from '@/shared/auth/auth.store'

/**
 * 后端 profile 响应（/api/users/me）
 */
export interface UserProfileResultDto {
  profile: AdminUserDto
  permissions: string[]
}

/**
 * 鉴权 API
 *
 * 与 Identity 域 AuthController 对接：
 * - POST /api/auth/login
 * - POST /api/auth/logout
 * - GET  /api/users/me
 */
export const authApi = {
  login(body: LoginDto): Promise<LoginResultDto> {
    return client.post<LoginResultDto>('/auth/login', body).then((r) => r.data)
  },
  logout(): Promise<void> {
    return client.post<void>('/auth/logout', null).then(() => undefined)
  },
  getProfile(): Promise<UserProfileResultDto> {
    return client
      .get<{ profile: AdminUserDto; permissions: string[] }>('/users/me')
      .then((r) => r.data)
  },
}
