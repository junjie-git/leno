import { describe, expect, it, beforeEach, afterEach } from 'vitest'
import MockAdapter from 'axios-mock-adapter'
import { client } from '@/shared/http'
import { authApi } from './auth.api'
import type { AdminUserDto, LoginResultDto, UserProfileResultDto } from '../types/auth.dto'

/**
 * 鉴权 API 单测（axios-mock-adapter 挂载到真实 client 实例）
 *
 * 覆盖点：
 * - login 调用 POST /auth/login 并解包 ApiResponse.data
 * - getProfile 调用 GET /users/me 并解包
 * - logout 调用 POST /auth/logout
 * - 写操作（POST）自动携带 Idempotency-Key（由 client 请求拦截器注入）
 */
describe('09-account authApi', () => {
  let mock: MockAdapter

  const fakeUser: AdminUserDto = {
    id: 'u-0001',
    username: 'admin',
    email: 'admin@leno.com',
    nickname: '运营管理员',
    status: 'Active',
    roles: ['Admin'],
  }

  const fakeLoginResult: LoginResultDto = {
    token: 'mock-token-admin-1001',
    expiresIn: 7200,
    user: fakeUser,
    roles: ['Admin'],
    permissions: ['*'],
  }

  beforeEach(() => {
    mock = new MockAdapter(client)
    localStorage.clear()
  })

  afterEach(() => {
    mock.restore()
  })

  it('login 调用 POST /auth/login 并解包 data', async () => {
    mock
      .onPost('/auth/login')
      .reply(200, { code: 200, message: 'OK', data: fakeLoginResult })

    const result = await authApi.login({ username: 'admin', password: 'Admin123!' })

    expect(result.token).toBe('mock-token-admin-1001')
    expect(result.expiresIn).toBe(7200)
    expect(result.user.username).toBe('admin')
    expect(result.roles).toEqual(['Admin'])
    expect(result.permissions).toEqual(['*'])

    expect(mock.history.post.length).toBe(1)
    const req = mock.history.post[0]
    expect(req.url).toBe('/auth/login')
    expect(JSON.parse(req.data as string)).toEqual({ username: 'admin', password: 'Admin123!' })
  })

  it('login 写操作自动携带 Idempotency-Key', async () => {
    mock
      .onPost('/auth/login')
      .reply(200, { code: 200, message: 'OK', data: fakeLoginResult })

    await authApi.login({ username: 'admin', password: 'Admin123!' })

    const headers = mock.history.post[0].headers ?? {}
    expect(String(headers['Idempotency-Key'] ?? '')).not.toBe('')
  })

  it('login 业务错误（code !== 200）抛 BusinessError', async () => {
    mock.onPost('/auth/login').reply(200, { code: 40001, message: '用户名或密码错误', data: null })

    await expect(authApi.login({ username: 'admin', password: 'wrong' })).rejects.toThrowError(
      '用户名或密码错误',
    )
  })

  it('getProfile 调用 GET /users/me 并解包 data', async () => {
    const fakeProfile: UserProfileResultDto = {
      profile: fakeUser,
      permissions: ['product:audit', 'promotion:manage'],
    }
    mock.onGet('/users/me').reply(200, { code: 200, message: 'OK', data: fakeProfile })

    const result = await authApi.getProfile()

    expect(result.profile.username).toBe('admin')
    expect(result.permissions).toEqual(['product:audit', 'promotion:manage'])
    expect(mock.history.get.length).toBe(1)
    expect(mock.history.get[0].url).toBe('/users/me')
  })

  it('logout 调用 POST /auth/logout', async () => {
    mock.onPost('/auth/logout').reply(200, { code: 200, message: 'OK', data: null })

    await authApi.logout()

    expect(mock.history.post.length).toBe(1)
    expect(mock.history.post[0].url).toBe('/auth/logout')
  })
})
