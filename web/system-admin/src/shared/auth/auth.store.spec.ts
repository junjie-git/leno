import { describe, it, expect, beforeEach, vi, afterEach } from 'vitest'
import { setActivePinia, createPinia } from 'pinia'
import { useAuthStore } from './auth.store'
import * as authApiModule from '@/modules/06-account/api/auth.api'

describe('shared/auth/auth.store', () => {
  beforeEach(() => {
    setActivePinia(createPinia())
    localStorage.clear()
  })

  afterEach(() => {
    vi.restoreAllMocks()
  })

  it('初始状态：未登录', () => {
    const auth = useAuthStore()
    expect(auth.token).toBeNull()
    expect(auth.user).toBeNull()
    expect(auth.roles).toEqual([])
    expect(auth.permissions).toEqual([])
    expect(auth.loginAt).toBeNull()
    expect(auth.expiresAt).toBeNull()
    expect(auth.twoFactorPending).toBe(false)
  })

  it('isAuthenticated：无 token 时为 false', () => {
    const auth = useAuthStore()
    expect(auth.isAuthenticated).toBe(false)
  })

  it('isAuthenticated：有 token 但过期时为 false', () => {
    const auth = useAuthStore()
    auth.token = 'tok'
    auth.expiresAt = Date.now() - 1_000
    expect(auth.isAuthenticated).toBe(false)
  })

  it('isAuthenticated：有 token 且未过期时为 true', () => {
    const auth = useAuthStore()
    auth.token = 'tok'
    auth.expiresAt = Date.now() + 10_000
    expect(auth.isAuthenticated).toBe(true)
  })

  it('isAdmin：roles 含 Admin 时为 true', () => {
    const auth = useAuthStore()
    auth.roles = ['Admin']
    expect(auth.isAdmin).toBe(true)
  })

  it('isAdmin：roles 不含 Admin 时为 false', () => {
    const auth = useAuthStore()
    auth.roles = ['Operator']
    expect(auth.isAdmin).toBe(false)
  })

  it('hasPermission：permissions 含目标权限时为 true', () => {
    const auth = useAuthStore()
    auth.permissions = ['dead-letter:dispose', 'role:read']
    expect(auth.hasPermission('dead-letter:dispose')).toBe(true)
  })

  it('hasPermission：permissions 含通配符 * 时为 true', () => {
    const auth = useAuthStore()
    auth.permissions = ['*']
    expect(auth.hasPermission('any:thing')).toBe(true)
  })

  it('hasPermission：permissions 不含目标时为 false', () => {
    const auth = useAuthStore()
    auth.permissions = ['role:read']
    expect(auth.hasPermission('dead-letter:dispose')).toBe(false)
  })

  it('hasRole：传入的角色与 store.roles 有交集时为 true', () => {
    const auth = useAuthStore()
    auth.roles = ['Admin', 'Operator']
    expect(auth.hasRole(['Admin'])).toBe(true)
    expect(auth.hasRole(['Operator'])).toBe(true)
    expect(auth.hasRole(['Admin', 'Operator'])).toBe(true)
    expect(auth.hasRole(['Auditor'])).toBe(false)
    expect(auth.hasRole([])).toBe(false)
  })

  it('login：调用 authApi.login 并填充 state', async () => {
    const fakeResult = {
      token: 'tok-123',
      expiresIn: 3600,
      user: { id: 'u1', username: 'admin', email: 'admin@leno.com', status: 'Active', roles: ['Admin'] },
      roles: ['Admin'],
      permissions: ['dead-letter:dispose', '*'],
    }
    const spy = vi.spyOn(authApiModule, 'authApi', 'get').mockReturnValue({
      login: vi.fn().mockResolvedValue(fakeResult),
      logout: vi.fn().mockResolvedValue(undefined),
      getProfile: vi.fn().mockResolvedValue(undefined),
    } as unknown as typeof authApiModule.authApi)
    const auth = useAuthStore()
    await auth.login({ username: 'admin', password: 'Admin123' })
    expect(auth.token).toBe('tok-123')
    expect(auth.user?.username).toBe('admin')
    expect(auth.roles).toEqual(['Admin'])
    expect(auth.permissions).toContain('*')
    expect(auth.loginAt).toBeTypeOf('number')
    expect(auth.expiresAt).toBeTypeOf('number')
    expect(auth.expiresAt! - auth.loginAt!).toBeGreaterThan(3_500_000)
    spy.mockRestore()
  })

  it('fetchProfile：调用 authApi.getProfile 并刷新 user/permissions', async () => {
    const fakeProfile = {
      id: 'u1',
      username: 'admin',
      email: 'admin@leno.com',
      status: 'Active',
      roles: ['Admin'],
    }
    const fakePerms = ['role:read', 'role:write']
    const spy = vi.spyOn(authApiModule, 'authApi', 'get').mockReturnValue({
      login: vi.fn(),
      logout: vi.fn(),
      getProfile: vi.fn().mockResolvedValue({ profile: fakeProfile, permissions: fakePerms }),
    } as unknown as typeof authApiModule.authApi)
    const auth = useAuthStore()
    auth.token = 'tok'
    await auth.fetchProfile()
    expect(auth.user?.username).toBe('admin')
    expect(auth.permissions).toEqual(fakePerms)
    spy.mockRestore()
  })

  it('logout：清空 state（best-effort 调用 authApi.logout）', async () => {
    const logoutMock = vi.fn().mockResolvedValue(undefined)
    const spy = vi.spyOn(authApiModule, 'authApi', 'get').mockReturnValue({
      login: vi.fn(),
      logout: logoutMock,
      getProfile: vi.fn(),
    } as unknown as typeof authApiModule.authApi)
    const auth = useAuthStore()
    auth.token = 'tok'
    auth.user = { id: 'u1', username: 'admin', email: 'a@l.com', status: 'Active', roles: ['Admin'] }
    auth.roles = ['Admin']
    auth.permissions = ['*']
    auth.loginAt = Date.now()
    auth.expiresAt = Date.now() + 100_000
    await auth.logout()
    expect(auth.token).toBeNull()
    expect(auth.user).toBeNull()
    expect(auth.roles).toEqual([])
    expect(auth.permissions).toEqual([])
    expect(auth.loginAt).toBeNull()
    expect(auth.expiresAt).toBeNull()
    expect(logoutMock).toHaveBeenCalled()
    spy.mockRestore()
  })

  it('logout：即使 authApi.logout 失败也清空 state', async () => {
    const spy = vi.spyOn(authApiModule, 'authApi', 'get').mockReturnValue({
      login: vi.fn(),
      logout: vi.fn().mockRejectedValue(new Error('network')),
      getProfile: vi.fn(),
    } as unknown as typeof authApiModule.authApi)
    const auth = useAuthStore()
    auth.token = 'tok'
    await auth.logout()
    expect(auth.token).toBeNull()
    spy.mockRestore()
  })
})
