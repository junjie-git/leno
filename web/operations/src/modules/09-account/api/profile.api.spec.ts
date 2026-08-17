import { describe, expect, it, beforeEach, afterEach } from 'vitest'
import MockAdapter from 'axios-mock-adapter'
import { client } from '@/shared/http'
import { profileApi } from './profile.api'
import type { AccountProfileDto } from '../types/account.dto'

/**
 * 个人中心 API 单测（axios-mock-adapter 挂载到真实 client 实例）
 *
 * 覆盖点：
 * - 资料查询/修改（GET/PUT /users/me）
 * - 修改密码（PUT /users/me/password）
 * - 双因子启用/确认/禁用（POST /users/me/two-factor/*）
 * - 外部登录绑定（POST /account/external-logins）与解绑（DELETE /account/external-logins/{provider}）
 * - 写操作自动携带 Idempotency-Key
 * - 业务错误（code !== 200）抛出带 message 的错误
 */
describe('09-account profileApi', () => {
  let mock: MockAdapter

  const fakeProfile: AccountProfileDto = {
    id: 'u-0001',
    username: 'zhangyy',
    fullName: '张运营',
    email: 'zhangsan@leno.com',
    phone: '138****8888',
    avatarUrl: null,
    roles: ['Operator'],
    hasPassword: true,
    twoFactorEnabled: false,
    twoFactorEnabledAt: null,
    externalLogins: [
      { provider: 'Google', externalUserName: 'zhangsan@gmail.com', boundAt: '2026-06-20T10:00:00Z' },
    ],
  }

  function ok<T>(data: T) {
    return [200, { code: 200, message: 'OK', data }]
  }

  beforeEach(() => {
    mock = new MockAdapter(client)
    localStorage.clear()
  })

  afterEach(() => {
    mock.restore()
  })

  it('getMyProfile 调用 GET /users/me 并解包 data', async () => {
    mock.onGet('/users/me').reply(...ok(fakeProfile))

    const result = await profileApi.getMyProfile()

    expect(result.username).toBe('zhangyy')
    expect(result.twoFactorEnabled).toBe(false)
    expect(result.externalLogins).toHaveLength(1)
    expect(result.externalLogins[0].provider).toBe('Google')

    expect(mock.history.get.length).toBe(1)
    expect(mock.history.get[0].url).toBe('/users/me')
  })

  it('updateProfile 调用 PUT /users/me 并回传更新后资料', async () => {
    const updated = { ...fakeProfile, fullName: '张三丰' }
    mock.onPut('/users/me').reply(...ok(updated))

    const result = await profileApi.updateProfile({
      fullName: '张三丰',
      email: 'zhangsan@leno.com',
      phone: '138****8888',
      avatarUrl: null,
    })

    expect(result.fullName).toBe('张三丰')
    expect(mock.history.put.length).toBe(1)

    const req = mock.history.put[0]
    expect(req.url).toBe('/users/me')
    expect(JSON.parse(req.data as string)).toEqual({
      fullName: '张三丰',
      email: 'zhangsan@leno.com',
      phone: '138****8888',
      avatarUrl: null,
    })
    // PUT 写操作自动携带幂等键
    expect(String(req.headers?.['Idempotency-Key'] ?? '')).not.toBe('')
  })

  it('changePassword 调用 PUT /users/me/password 并携带请求体', async () => {
    mock.onPut('/users/me/password').reply(...ok(null))

    await profileApi.changePassword({ oldPassword: 'Old1234!', newPassword: 'New1234!' })

    const req = mock.history.put[0]
    expect(req.url).toBe('/users/me/password')
    expect(JSON.parse(req.data as string)).toEqual({ oldPassword: 'Old1234!', newPassword: 'New1234!' })
    expect(String(req.headers?.['Idempotency-Key'] ?? '')).not.toBe('')
  })

  it('changePassword 原密码错误时抛业务错误', async () => {
    mock
      .onPut('/users/me/password')
      .reply(200, { code: 40002, message: '原密码错误', data: null })

    await expect(
      profileApi.changePassword({ oldPassword: 'wrong', newPassword: 'New1234!' }),
    ).rejects.toThrowError('原密码错误')
  })

  it('enableTwoFactor 调用 POST /users/me/two-factor/enable 并返回密钥', async () => {
    mock
      .onPost('/users/me/two-factor/enable')
      .reply(...ok({ qrCodeUri: 'otpauth://totp/Leno:zhangyy?secret=JBSWY3DP', manualEntryKey: 'JBSWY3DP' }))

    const result = await profileApi.enableTwoFactor()

    expect(result.qrCodeUri).toContain('otpauth://totp')
    expect(result.manualEntryKey).toBe('JBSWY3DP')

    const req = mock.history.post[0]
    expect(req.url).toBe('/users/me/two-factor/enable')
    expect(String(req.headers?.['Idempotency-Key'] ?? '')).not.toBe('')
  })

  it('confirmTwoFactor 调用 POST /users/me/two-factor/confirm 传 TOTP 码', async () => {
    mock.onPost('/users/me/two-factor/confirm').reply(...ok(null))

    await profileApi.confirmTwoFactor({ totpCode: '123456' })

    const req = mock.history.post[0]
    expect(req.url).toBe('/users/me/two-factor/confirm')
    expect(JSON.parse(req.data as string)).toEqual({ totpCode: '123456' })
  })

  it('confirmTwoFactor TOTP 码错误时抛业务错误', async () => {
    mock
      .onPost('/users/me/two-factor/confirm')
      .reply(200, { code: 40010, message: 'TOTP 码错误', data: null })

    await expect(profileApi.confirmTwoFactor({ totpCode: '000000' })).rejects.toThrowError(
      'TOTP 码错误',
    )
  })

  it('disableTwoFactor 调用 POST /users/me/two-factor/disable', async () => {
    mock.onPost('/users/me/two-factor/disable').reply(...ok(null))

    await profileApi.disableTwoFactor()

    expect(mock.history.post.length).toBe(1)
    expect(mock.history.post[0].url).toBe('/users/me/two-factor/disable')
  })

  it('bindExternalLogin 调用 POST /account/external-logins 并返回绑定项', async () => {
    const bound = { provider: 'GitHub', externalUserName: 'zhangyy', boundAt: '2026-07-01T08:00:00Z' }
    mock.onPost('/account/external-logins').reply(...ok(bound))

    const result = await profileApi.bindExternalLogin({
      provider: 'GitHub',
      authorizationCode: 'oauth-code-001',
    })

    expect(result.provider).toBe('GitHub')

    const req = mock.history.post[0]
    expect(req.url).toBe('/account/external-logins')
    expect(JSON.parse(req.data as string)).toEqual({
      provider: 'GitHub',
      authorizationCode: 'oauth-code-001',
    })
    expect(String(req.headers?.['Idempotency-Key'] ?? '')).not.toBe('')
  })

  it('unbindExternalLogin 调用 DELETE /account/external-logins/{provider}', async () => {
    mock.onDelete('/account/external-logins/Google').reply(...ok(null))

    await profileApi.unbindExternalLogin('Google')

    expect(mock.history.delete.length).toBe(1)
    const req = mock.history.delete[0]
    expect(req.url).toBe('/account/external-logins/Google')
    // delete 同样通过 withIdempotency 配置携带幂等键
    expect(String(req.headers?.['Idempotency-Key'] ?? '')).not.toBe('')
  })

  it('unbindExternalLogin 对 provider 做 URL 编码', async () => {
    mock.onDelete(/\/account\/external-logins\/.+/).reply(...ok(null))

    await profileApi.unbindExternalLogin('We Chat')

    expect(mock.history.delete[0].url).toBe('/account/external-logins/We%20Chat')
  })
})
