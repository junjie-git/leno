/**
 * 认证域 DTO（Identity 域 / 旧 UserAuth 域双轨兜底）
 *
 * 端点契约：
 * - POST /api/account/login                    账号密码登录（可能触发 2FA 二段验证）
 * - POST /api/auth/two-factor/verify           双因子验证码校验（完成二段登录）
 * - POST /api/auth/refresh                     刷新访问令牌
 * - POST /api/auth/register                    注册
 * - POST /api/auth/forgot-password             忘记密码（发送重置验证码）
 * - POST /api/auth/reset-password              重置密码
 * - POST /api/auth/logout                      登出
 * - GET  /api/auth/oauth/{provider}/login      获取三方登录跳转地址
 * - GET  /api/auth/oauth/{provider}/callback   三方登录回调换取令牌
 * - GET  /api/account/external-logins          已绑定的外部登录列表
 * - POST /api/account/external-logins          绑定外部登录
 * - DELETE /api/account/external-logins/{provider} 解绑外部登录
 * - POST /api/users/me/two-factor/enable       开启 2FA（返回密钥与二维码）
 * - POST /api/users/me/two-factor/confirm      确认开启 2FA（返回恢复码）
 * - POST /api/users/me/two-factor/disable      关闭 2FA
 */

/** 三方登录提供商标识 */
export type OAuthProvider = 'wechat' | 'alipay'

/** 买家用户视图 */
export interface BuyerUserDto {
  id: string
  username: string
  nickname: string
  phone?: string
  email?: string
  avatar?: string
  /** 会员等级名称（V1-V6），由 Membership 域冗余 */
  memberLevelName?: string
  /** 当前可用积分（只读展示） */
  points?: number
}

/** 登录响应（含 2FA 二段式支持） */
export interface LoginResultDto {
  /** 登录完成时直接下发令牌；需要 2FA 时为空 */
  token?: string
  /** 令牌有效期（秒） */
  expiresIn?: number
  user?: BuyerUserDto
  roles?: string[]
  permissions?: string[]
  /** 是否需要双因子验证（二段登录） */
  requiresTwoFactor?: boolean
  /** 2FA 二段登录凭据（临时票据） */
  twoFactorTicket?: string
}

/** 登录请求体 */
export interface LoginRequestDto {
  /** 用户名 / 手机号 / 邮箱 */
  account: string
  password: string
}

/** 2FA 验证请求体 */
export interface TwoFactorVerifyRequestDto {
  twoFactorTicket: string
  code: string
}

/** 注册请求体 */
export interface RegisterRequestDto {
  username: string
  nickname: string
  phone: string
  email?: string
  password: string
  verifyCode: string
}

/** 忘记密码请求体（发送重置验证码） */
export interface ForgotPasswordRequestDto {
  /** 手机号或邮箱 */
  account: string
}

/** 重置密码请求体 */
export interface ResetPasswordRequestDto {
  account: string
  verifyCode: string
  newPassword: string
}

/** OAuth 跳转地址响应 */
export interface OAuthLoginUrlDto {
  provider: OAuthProvider
  authorizeUrl: string
  state: string
}

/** 已绑定的外部登录 */
export interface ExternalLoginDto {
  provider: OAuthProvider
  providerUserId: string
  boundAt: string
}

/** 开启 2FA 响应 */
export interface TwoFactorEnableResultDto {
  /** TOTP 密钥（base32） */
  secret: string
  /** otpauth:// 二维码内容 */
  qrCodeUri: string
}

/** 确认开启 2FA 响应 */
export interface TwoFactorConfirmResultDto {
  /** 恢复码（一次性） */
  recoveryCodes: string[]
}
