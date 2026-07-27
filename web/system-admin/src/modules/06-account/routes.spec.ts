import { describe, it, expect } from 'vitest'
import { loginRoute, accountRoutes } from './routes'

describe('modules/06-account/routes', () => {
  it('loginRoute 为 /login 匿名路由', () => {
    expect(loginRoute.path).toBe('/login')
    expect(loginRoute.name).toBe('account.login')
    expect(loginRoute.meta?.anonymous).toBe(true)
    expect(loginRoute.meta?.title).toBe('登录')
  })

  it('accountRoutes 为数组', () => {
    expect(Array.isArray(accountRoutes)).toBe(true)
  })
})
