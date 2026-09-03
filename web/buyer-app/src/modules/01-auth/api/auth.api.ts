import { client } from '@/shared/http'
import type {
  BuyerUserDto,
  ExternalLoginDto,
  ForgotPasswordRequestDto,
  LoginRequestDto,
  LoginResultDto,
  OAuthLoginUrlDto,
  OAuthProvider,
  RegisterRequestDto,
  ResetPasswordRequestDto,
  TwoFactorConfirmResultDto,
  TwoFactorEnableResultDto,
  TwoFactorVerifyRequestDto,
} from '../types/auth.dto'

/**
 * 认证 API（Identity 域接管，旧 UserAuth 域双轨兜底）
 *
 * - POST /account/login                    账号密码登录（可能返回 requiresTwoFactor）
 * - POST /auth/two-factor/verify           2FA 验证码校验完成登录
 * - POST /auth/refresh                     刷新令牌
 * - POST /auth/register                    注册
 * - POST /auth/forgot-password             忘记密码（发送重置验证码）
 * - POST /auth/reset-password              重置密码
 * - POST /auth/logout                      登出
 * - GET  /auth/oauth/{provider}/login      三方登录跳转地址
 * - GET  /auth/oauth/{provider}/callback   三方登录回调
 * - GET  /account/external-logins          外部登录绑定列表
 * - POST /account/external-logins          绑定外部登录
 * - DELETE /account/external-logins/{provider} 解绑
 * - POST /users/me/two-factor/enable       开启 2FA
 * - POST /users/me/two-factor/confirm      确认 2FA
 * - POST /users/me/two-factor/disable      关闭 2FA
 *
 * 响应拦截器已解包 ApiResponse.data，调用方拿到的就是业务负载。
 */
export const authApi = {
  /** 账号密码登录（需要 2FA 时返回 twoFactorTicket） */
  login(body: LoginRequestDto): Promise<LoginResultDto> {
    return client.post<LoginResultDto>('/account/login', body).then((r) => r.data)
  },

  /** 2FA 验证码校验，完成二段登录 */
  verifyTwoFactor(body: TwoFactorVerifyRequestDto): Promise<LoginResultDto> {
    return client.post<LoginResultDto>('/auth/two-factor/verify', body).then((r) => r.data)
  },

  /** 刷新访问令牌 */
  refresh(): Promise<LoginResultDto> {
    return client.post<LoginResultDto>('/auth/refresh').then((r) => r.data)
  },

  /** 注册 */
  register(body: RegisterRequestDto): Promise<{ userId: string }> {
    return client.post<{ userId: string }>('/auth/register', body).then((r) => r.data)
  },

  /** 忘记密码：向账号（手机/邮箱）发送重置验证码 */
  forgotPassword(body: ForgotPasswordRequestDto): Promise<{ sentTo: string }> {
    return client.post<{ sentTo: string }>('/auth/forgot-password', body).then((r) => r.data)
  },

  /** 重置密码 */
  resetPassword(body: ResetPasswordRequestDto): Promise<{ success: boolean }> {
    return client.post<{ success: boolean }>('/auth/reset-password', body).then((r) => r.data)
  },

  /** 登出 */
  logout(): Promise<null> {
    return client.post<null>('/auth/logout').then((r) => r.data)
  },

  /** 当前用户信息（路由守卫与 auth store 使用；13-profile 模块的 profileApi 提供完整读写） */
  getProfile(): Promise<BuyerUserDto> {
    return client.get<BuyerUserDto>('/users/me').then((r) => r.data)
  },

  /** 获取三方登录跳转地址 */
  getOAuthLoginUrl(provider: OAuthProvider): Promise<OAuthLoginUrlDto> {
    return client.get<OAuthLoginUrlDto>(`/auth/oauth/${provider}/login`).then((r) => r.data)
  },

  /** 三方登录回调（换取本站令牌） */
  oauthCallback(provider: OAuthProvider, params: { code: string; state: string }): Promise<LoginResultDto> {
    return client
      .get<LoginResultDto>(`/auth/oauth/${provider}/callback`, { params })
      .then((r) => r.data)
  },

  /** 已绑定的外部登录列表 */
  listExternalLogins(): Promise<ExternalLoginDto[]> {
    return client.get<ExternalLoginDto[]>('/account/external-logins').then((r) => r.data)
  },

  /** 绑定外部登录 */
  bindExternalLogin(body: { provider: OAuthProvider; code: string }): Promise<ExternalLoginDto> {
    return client.post<ExternalLoginDto>('/account/external-logins', body).then((r) => r.data)
  },

  /** 解绑外部登录 */
  unbindExternalLogin(provider: OAuthProvider): Promise<null> {
    return client.delete<null>(`/account/external-logins/${provider}`).then((r) => r.data)
  },

  /** 开启 2FA（返回 TOTP 密钥与二维码内容） */
  enableTwoFactor(): Promise<TwoFactorEnableResultDto> {
    return client.post<TwoFactorEnableResultDto>('/users/me/two-factor/enable').then((r) => r.data)
  },

  /** 确认开启 2FA（校验首码并返回恢复码） */
  confirmTwoFactor(body: { code: string }): Promise<TwoFactorConfirmResultDto> {
    return client.post<TwoFactorConfirmResultDto>('/users/me/two-factor/confirm', body).then((r) => r.data)
  },

  /** 关闭 2FA */
  disableTwoFactor(body: { password: string }): Promise<null> {
    return client.post<null>('/users/me/two-factor/disable', body).then((r) => r.data)
  },
}
