import { client, withIdempotency } from '@/shared/http'
import type {
  AccountProfileDto,
  BindExternalLoginDto,
  ChangePasswordDto,
  ExternalLoginDto,
  TwoFactorConfirmDto,
  TwoFactorEnableResultDto,
  UpdateProfileDto,
} from '../types/account.dto'

/**
 * 个人中心 API（Identity 域）
 *
 * 与 UsersController / AccountController 对接：
 * - GET    /api/users/me                          当前账号完整资料（含双因子/外部登录）
 * - PUT    /api/users/me                          修改基础资料
 * - PUT    /api/users/me/password                 修改密码（成功后需重新登录）
 * - POST   /api/users/me/two-factor/enable        启用双因子（生成密钥与二维码 URI）
 * - POST   /api/users/me/two-factor/confirm       确认启用（校验 TOTP 码）
 * - POST   /api/users/me/two-factor/disable       禁用双因子
 * - POST   /api/account/external-logins           绑定外部登录
 * - DELETE /api/account/external-logins/{provider} 解绑外部登录
 *
 * 响应拦截器已解包 ApiResponse.data，调用方拿到的就是业务负载。
 */
export const profileApi = {
  /**
   * 查询当前账号完整资料
   */
  getMyProfile(): Promise<AccountProfileDto> {
    return client.get<AccountProfileDto>('/users/me').then((r) => r.data)
  },

  /**
   * 修改基础资料（姓名/邮箱/手机号/头像）
   *
   * @returns 更新后的资料
   */
  updateProfile(body: UpdateProfileDto): Promise<AccountProfileDto> {
    return client.put<AccountProfileDto>('/users/me', body).then((r) => r.data)
  },

  /**
   * 修改密码：校验旧密码，成功后 token 失效需重新登录
   */
  changePassword(body: ChangePasswordDto): Promise<void> {
    return client
      .put<void>('/users/me/password', body, withIdempotency())
      .then(() => undefined)
  },

  /**
   * 启用双因子：生成二维码 URI 与手动输入密钥
   */
  enableTwoFactor(): Promise<TwoFactorEnableResultDto> {
    return client
      .post<TwoFactorEnableResultDto>('/users/me/two-factor/enable', null, withIdempotency())
      .then((r) => r.data)
  },

  /**
   * 确认启用双因子：验证 Authenticator 生成的 6 位 TOTP 码
   */
  confirmTwoFactor(body: TwoFactorConfirmDto): Promise<void> {
    return client
      .post<void>('/users/me/two-factor/confirm', body, withIdempotency())
      .then(() => undefined)
  },

  /**
   * 禁用双因子（危险操作，页面层需二次确认）
   */
  disableTwoFactor(): Promise<void> {
    return client
      .post<void>('/users/me/two-factor/disable', null, withIdempotency())
      .then(() => undefined)
  },

  /**
   * 绑定外部登录（OAuth 授权码模式）
   *
   * @returns 新增的绑定项
   */
  bindExternalLogin(body: BindExternalLoginDto): Promise<ExternalLoginDto> {
    return client
      .post<ExternalLoginDto>('/account/external-logins', body, withIdempotency())
      .then((r) => r.data)
  },

  /**
   * 解绑外部登录
   *
   * @param provider 提供商标识（Google / GitHub / WeChat）
   */
  unbindExternalLogin(provider: string): Promise<void> {
    return client
      .delete<void>(`/account/external-logins/${encodeURIComponent(provider)}`, withIdempotency())
      .then(() => undefined)
  },
}
