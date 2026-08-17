import type MockAdapter from 'axios-mock-adapter'
import type { InternalAxiosRequestConfig } from 'axios'
import {
  loadSeedData,
  saveSeedData,
  nextTokenId,
  findAccountByUsername,
  findSessionByToken,
  addSession,
  removeSession,
} from '../data/seed'
import type { MockAccount } from '../data/types'

/**
 * 鉴权 mock handler
 *
 * 覆盖端点（client.baseURL=/api）：
 * - POST /auth/login   账号密码登录
 * - POST /auth/logout  登出（移除 mock 会话）
 * - GET  /users/me     当前用户 profile 与权限
 *
 * 业务错误统一走 HTTP 200 + code !== 200（由拦截器转换为 BusinessError），
 * 鉴权失败走 HTTP 401（转换为 UnauthorizedError），与真实后端契约一致。
 */

interface LoginBody {
  username?: unknown
  password?: unknown
}

/** 从请求头解析 Bearer token */
function extractBearerToken(config: InternalAxiosRequestConfig): string | null {
  const raw =
    config.headers?.Authorization ??
    config.headers?.authorization ??
    (config.headers as Record<string, unknown> | undefined)?.['Authorization']
  if (typeof raw !== 'string' || !raw.startsWith('Bearer ')) return null
  return raw.slice('Bearer '.length)
}

/** 账号视图（脱去密码） */
function toAccountView(account: MockAccount) {
  return {
    id: account.id,
    username: account.username,
    email: account.email,
    nickname: account.nickname,
    status: account.status,
    roles: account.roles,
  }
}

export function registerAuthHandlers(mock: MockAdapter): void {
  // 登录
  mock.onPost('/auth/login').reply((config) => {
    const body = (typeof config.data === 'string' ? JSON.parse(config.data) : config.data ?? {}) as LoginBody
    const username = typeof body.username === 'string' ? body.username : ''
    const password = typeof body.password === 'string' ? body.password : ''

    if (!username || !password) {
      return [200, { code: 40001, message: '用户名与密码不能为空', data: null }]
    }

    const seed = loadSeedData()
    const account = findAccountByUsername(seed, username)
    if (!account || account.password !== password) {
      return [200, { code: 40001, message: '用户名或密码错误', data: null }]
    }
    if (account.status !== 'Active') {
      return [200, { code: 40003, message: '账号已被禁用', data: null }]
    }

    const token = `mock-token-${account.username}-${nextTokenId(seed)}`
    addSession(seed, token, account.username)
    saveSeedData(seed)

    return [
      200,
      {
        code: 200,
        message: 'OK',
        data: {
          token,
          expiresIn: 7200,
          user: toAccountView(account),
          roles: account.roles,
          permissions: account.permissions,
        },
      },
    ]
  })

  // 登出
  mock.onPost('/auth/logout').reply((config) => {
    const token = extractBearerToken(config)
    if (token) {
      const seed = loadSeedData()
      removeSession(seed, token)
      saveSeedData(seed)
    }
    return [200, { code: 200, message: 'OK', data: null }]
  })

  // 当前用户 profile
  mock.onGet('/users/me').reply((config) => {
    const token = extractBearerToken(config)
    if (!token) {
      return [401, { message: '未登录或登录已过期' }]
    }
    const seed = loadSeedData()
    const hit = findSessionByToken(seed, token)
    if (!hit) {
      return [401, { message: '未登录或登录已过期' }]
    }
    return [
      200,
      {
        code: 200,
        message: 'OK',
        data: {
          profile: toAccountView(hit.account),
          permissions: hit.account.permissions,
        },
      },
    ]
  })
}
