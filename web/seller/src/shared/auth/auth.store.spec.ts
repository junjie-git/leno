import { setActivePinia, createPinia } from 'pinia'
import { beforeEach, describe, expect, it, vi } from 'vitest'
import { useAuthStore } from './auth.store'

// Mock authApi
vi.mock('@/modules/08-account/api/auth.api', () => ({
  authApi: {
    login: vi.fn().mockResolvedValue({
      token: 'test-token',
      expiresIn: 3600,
      user: { id: '1', username: 'seller1', email: 's@test.com', status: 'Active', roles: ['Seller'], shopId: 'shop1', shopName: '测试店铺', shopStatus: 'Active' },
      roles: ['Seller'],
      permissions: ['product:list', 'order:ship'],
    }),
    getProfile: vi.fn().mockResolvedValue({
      profile: { id: '1', username: 'seller1', email: 's@test.com', status: 'Active', roles: ['Seller'] },
      permissions: ['product:list', 'order:ship'],
    }),
    logout: vi.fn().mockResolvedValue(undefined),
  },
}))

describe('useAuthStore', () => {
  beforeEach(() => {
    setActivePinia(createPinia())
    localStorage.clear()
  })

  it('login 成功后填充 token/user/roles/permissions', async () => {
    const store = useAuthStore()
    await store.login({ username: 'seller1', password: 'pass' })
    expect(store.token).toBe('test-token')
    expect(store.user?.shopId).toBe('shop1')
    expect(store.roles).toEqual(['Seller'])
    expect(store.permissions).toContain('product:list')
    expect(store.isAuthenticated).toBe(true)
    expect(store.isSeller).toBe(true)
    expect(store.twoFactorPending).toBe(false)
  })

  it('hasPermission 返回 true 当权限存在', async () => {
    const store = useAuthStore()
    await store.login({ username: 'seller1', password: 'pass' })
    expect(store.hasPermission('product:list')).toBe(true)
    expect(store.hasPermission('product:create')).toBe(false)
  })

  it('hasPermission 返回 true 当有 * 通配权限', async () => {
    const store = useAuthStore()
    await store.login({ username: 'seller1', password: 'pass' })
    store.permissions = ['*']
    expect(store.hasPermission('any:permission')).toBe(true)
  })

  it('hasRole 返回 true 当角色匹配', async () => {
    const store = useAuthStore()
    await store.login({ username: 'seller1', password: 'pass' })
    expect(store.hasRole(['Seller'])).toBe(true)
    expect(store.hasRole(['Admin'])).toBe(false)
  })

  it('hasRole 空数组返回 true（无角色要求）', () => {
    const store = useAuthStore()
    expect(store.hasRole([])).toBe(true)
  })

  it('logout 清空所有状态', async () => {
    const store = useAuthStore()
    await store.login({ username: 'seller1', password: 'pass' })
    expect(store.token).toBe('test-token')
    await store.logout()
    expect(store.token).toBeNull()
    expect(store.user).toBeNull()
    expect(store.roles).toEqual([])
    expect(store.permissions).toEqual([])
  })

  it('isAuthenticated 返回 false 当 token 过期', async () => {
    const store = useAuthStore()
    await store.login({ username: 'seller1', password: 'pass' })
    store.expiresAt = Date.now() - 1000
    expect(store.isAuthenticated).toBe(false)
  })
})
