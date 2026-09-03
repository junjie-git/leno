import type MockAdapter from 'axios-mock-adapter'
import type {
  BuyerUserDto,
  ExternalLoginDto,
  LoginRequestDto,
  LoginResultDto,
  OAuthProvider,
} from '@/modules/01-auth/types/auth.dto'
import type { BuyerProfileDto } from '@/modules/13-profile/types/profile.dto'
import {
  DEMO_2FA_ACCOUNT,
  DEMO_2FA_CODE,
  DEMO_ACCOUNT,
  DEMO_PASSWORD,
  runtime,
  seedUser,
} from '../data/seed'
import { fail, ok, parseBody } from './helpers'

/**
 * 认证 + 个人资料 handlers（Identity 域）
 *
 * - POST /account/login（含 2FA 二段）
 * - POST /auth/two-factor/verify
 * - POST /auth/refresh / logout / register / forgot-password / reset-password
 * - GET  /auth/oauth/{provider}/login | callback
 * - GET/POST/DELETE /account/external-logins
 * - GET/PUT /users/me、PUT /users/me/password
 * - POST /users/me/two-factor/enable | confirm | disable
 */

const MOCK_TOKEN = 'mock-token-buyer-1001'
const MOCK_EXPIRES_IN = 7200

/** 构造买家用户视图（实时反映 seedUser 状态） */
function buyerUserView(): BuyerUserDto {
  return {
    id: seedUser.id,
    username: seedUser.username,
    nickname: seedUser.nickname,
    phone: seedUser.phone,
    email: seedUser.email,
    avatar: seedUser.avatar,
    memberLevelName: seedUser.memberLevelName,
    points: seedUser.points,
  }
}

function profileView(): BuyerProfileDto {
  return { ...seedUser }
}

function loginSuccess(): LoginResultDto {
  return {
    token: MOCK_TOKEN,
    expiresIn: MOCK_EXPIRES_IN,
    user: buyerUserView(),
    roles: ['Buyer'],
    permissions: [],
  }
}

/** 已绑定外部登录（演示：微信已绑定） */
const externalLogins: ExternalLoginDto[] = [
  { provider: 'wechat', providerUserId: 'wx-openid-88231004', boundAt: '2025-08-02T10:12:00.000Z' },
]

export function registerAuthHandlers(mock: MockAdapter): void {
  // 账号密码登录（演示账号见 Login 页提示；demo2fa 走双因子二段）
  mock.onPost('/account/login').reply((config) => {
    const body = parseBody<LoginRequestDto>(config.data)
    if (!body.account || !body.password) {
      return fail(40001, '请输入账号与密码')
    }
    if (body.account === DEMO_2FA_ACCOUNT) {
      const ticket = `2fa-ticket-${Date.now()}`
      runtime.twoFactorTickets.set(ticket, body.account)
      return ok({ requiresTwoFactor: true, twoFactorTicket: ticket })
    }
    if (body.account !== DEMO_ACCOUNT && body.account !== seedUser.phone && body.account !== seedUser.email) {
      return fail(40001, '账号不存在，请先注册')
    }
    if (body.password !== DEMO_PASSWORD) {
      return fail(40001, '账号或密码错误')
    }
    return ok(loginSuccess())
  })

  // 2FA 验证码校验（演示验证码 123456）
  mock.onPost('/auth/two-factor/verify').reply((config) => {
    const body = parseBody<{ twoFactorTicket: string; code: string }>(config.data)
    const account = runtime.twoFactorTickets.get(body.twoFactorTicket ?? '')
    if (!account) {
      return fail(40101, '登录会话已过期，请重新登录')
    }
    if (body.code !== DEMO_2FA_CODE) {
      return fail(40102, '验证码错误，请重新输入')
    }
    runtime.twoFactorTickets.delete(body.twoFactorTicket)
    return ok(loginSuccess())
  })

  // 刷新令牌
  mock.onPost('/auth/refresh').reply(() => ok(loginSuccess()))

  // 登出
  mock.onPost('/auth/logout').reply(() => ok(null))

  // 注册（演示：任意合法输入均成功）
  mock.onPost('/auth/register').reply((config) => {
    const body = parseBody<{
      username: string
      nickname: string
      phone: string
      password: string
      verifyCode: string
    }>(config.data)
    if (!body.username || !body.phone || !body.password) {
      return fail(40010, '请完整填写注册信息')
    }
    if (body.verifyCode !== '123456') {
      return fail(40011, '短信验证码错误（演示验证码 123456）')
    }
    return ok({ userId: 'u-new-1002' })
  })

  // 忘记密码（发送重置验证码）
  mock.onPost('/auth/forgot-password').reply((config) => {
    const body = parseBody<{ account: string }>(config.data)
    if (!body.account) {
      return fail(40020, '请输入注册手机号或邮箱')
    }
    const masked = body.account.includes('@')
      ? body.account.replace(/^(.).*(@.*)$/, '$1****$2')
      : `${body.account.slice(0, 3)}****${body.account.slice(7)}`
    return ok({ sentTo: masked })
  })

  // 重置密码
  mock.onPost('/auth/reset-password').reply((config) => {
    const body = parseBody<{ account: string; verifyCode: string; newPassword: string }>(config.data)
    if (body.verifyCode !== '123456') {
      return fail(40021, '验证码错误（演示验证码 123456）')
    }
    if (!body.newPassword || body.newPassword.length < 6) {
      return fail(40022, '新密码长度需 6-32 位')
    }
    return ok({ success: true })
  })

  // OAuth 跳转地址
  mock.onGet(/\/auth\/oauth\/(wechat|alipay)\/login$/).reply((config) => {
    const provider = (config.url?.match(/\/auth\/oauth\/(wechat|alipay)\/login$/) ?? [])[1] as OAuthProvider
    const state = `state-${Date.now()}`
    return ok({
      provider,
      authorizeUrl: `https://oauth.leno.mock/${provider}/authorize?state=${state}`,
      state,
    })
  })

  // OAuth 回调（演示：code 任意非空即成功）
  mock.onGet(/\/auth\/oauth\/(wechat|alipay)\/callback$/).reply((config) => {
    const url = config.url ?? ''
    const code = url.match(/code=([^&]+)/)?.[1]
    if (!code) {
      return fail(40110, '三方授权失败：缺少授权码')
    }
    return ok(loginSuccess())
  })

  // 外部登录绑定列表
  mock.onGet('/account/external-logins').reply(() => ok(externalLogins))

  // 绑定外部登录
  mock.onPost('/account/external-logins').reply((config) => {
    const body = parseBody<{ provider: OAuthProvider; code: string }>(config.data)
    if (!body.code) {
      return fail(40111, '三方授权失败：缺少授权码')
    }
    const exists = externalLogins.find((e) => e.provider === body.provider)
    if (exists) {
      return fail(40112, '该账号已绑定，无需重复绑定')
    }
    const bound: ExternalLoginDto = {
      provider: body.provider,
      providerUserId: `${body.provider}-openid-${Date.now()}`,
      boundAt: new Date().toISOString(),
    }
    externalLogins.push(bound)
    return ok(bound)
  })

  // 解绑外部登录
  mock.onDelete(/\/account\/external-logins\/(wechat|alipay)$/).reply((config) => {
    const provider = (config.url?.match(/\/account\/external-logins\/(wechat|alipay)$/) ?? [])[1]
    const idx = externalLogins.findIndex((e) => e.provider === provider)
    if (idx < 0) {
      return fail(40113, '该账号未绑定此前置登录方式')
    }
    externalLogins.splice(idx, 1)
    return ok(null)
  })

  // 当前用户（完整资料）
  mock.onGet('/users/me').reply(() => ok(profileView()))

  // 更新资料
  mock.onPut('/users/me').reply((config) => {
    const body = parseBody<{
      nickname?: string
      avatar?: string
      gender?: 'Male' | 'Female' | 'Unknown'
      birthday?: string
      email?: string
    }>(config.data)
    if (body.nickname !== undefined) {
      if (!body.nickname || body.nickname.length > 20) {
        return fail(40030, '昵称需 1-20 个字符')
      }
      seedUser.nickname = body.nickname
    }
    if (body.gender !== undefined) seedUser.gender = body.gender
    if (body.birthday !== undefined) seedUser.birthday = body.birthday
    if (body.email !== undefined) seedUser.email = body.email
    if (body.avatar !== undefined) seedUser.avatar = body.avatar
    return ok(profileView())
  })

  // 修改密码
  mock.onPut('/users/me/password').reply((config) => {
    const body = parseBody<{ oldPassword: string; newPassword: string }>(config.data)
    if (body.oldPassword !== DEMO_PASSWORD) {
      return fail(40031, '原密码错误')
    }
    if (!body.newPassword || body.newPassword.length < 6) {
      return fail(40032, '新密码需 6-32 位且包含字母和数字')
    }
    return ok(null)
  })

  // 开启 2FA
  mock.onPost('/users/me/two-factor/enable').reply(() => {
    const secret = 'JBSWY3DPEHPK3PXP'
    return ok({
      secret,
      qrCodeUri: `otpauth://totp/Leno:${seedUser.username}?secret=${secret}&issuer=Leno`,
    })
  })

  // 确认开启 2FA（演示验证码 123456）
  mock.onPost('/users/me/two-factor/confirm').reply((config) => {
    const body = parseBody<{ code: string }>(config.data)
    if (body.code !== DEMO_2FA_CODE) {
      return fail(40103, '验证码错误（演示验证码 123456）')
    }
    seedUser.twoFactorEnabled = true
    return ok({ recoveryCodes: ['A1B2C3D4', 'E5F6G7H8', 'I9J0K1L2'] })
  })

  // 关闭 2FA
  mock.onPost('/users/me/two-factor/disable').reply((config) => {
    const body = parseBody<{ password: string }>(config.data)
    if (body.password !== DEMO_PASSWORD) {
      return fail(40031, '密码错误，无法关闭双因子')
    }
    seedUser.twoFactorEnabled = false
    return ok(null)
  })
}
