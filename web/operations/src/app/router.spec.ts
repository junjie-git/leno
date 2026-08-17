import { describe, it, expect, beforeEach, vi, afterEach } from 'vitest'
import { createPinia, setActivePinia } from 'pinia'
import { createRouter, createMemoryHistory, type Router } from 'vue-router'
import { createAuthGuard, router as appRouter } from './router'
import { useAuthStore } from '@/shared/auth/auth.store'
import * as authApiModule from '@/modules/09-account/api/auth.api'
import type { AdminUserDto } from '@/shared/auth/auth.store'

function makeRouter(): Router {
  return createRouter({
    history: createMemoryHistory(),
    routes: [
      { path: '/login', component: { template: '<div>login</div>' }, meta: { anonymous: true } },
      { path: '/403', component: { template: '<div>403</div>' }, meta: { anonymous: true } },
      { path: '/public', component: { template: '<div>public</div>' }, meta: { anonymous: true } },
      { path: '/protected', component: { template: '<div>protected</div>' } },
      { path: '/admin-only', component: { template: '<div>admin</div>' }, meta: { roles: ['Admin'] } },
      { path: '/perm', component: { template: '<div>perm</div>' }, meta: { permission: 'product:audit' } },
      { path: '/', component: { template: '<div>home</div>' } },
    ],
  })
}

function mkUser(roles: string[]): AdminUserDto {
  return { id: 'u1', username: 'a', email: 'a@leno.com', status: 'Active', roles } as AdminUserDto
}

describe('app/router createAuthGuard', () => {
  beforeEach(() => {
    setActivePinia(createPinia())
    localStorage.clear()
  })
  afterEach(() => vi.restoreAllMocks())

  it('匿名路由直接放行', async () => {
    const router = makeRouter()
    router.beforeEach(createAuthGuard())
    await router.push('/public')
    expect(router.currentRoute.value.path).toBe('/public')
  })

  it('未登录访问受保护路由跳 /login?redirect', async () => {
    const router = makeRouter()
    router.beforeEach(createAuthGuard())
    await router.push('/protected')
    expect(router.currentRoute.value.path).toBe('/login')
    expect(router.currentRoute.value.query.redirect).toBe('/protected')
  })

  it('已登录且 user 已存在时放行受保护路由', async () => {
    const auth = useAuthStore()
    auth.token = 'tok'
    auth.expiresAt = Date.now() + 100_000
    auth.user = mkUser(['Admin'])
    auth.roles = ['Admin']
    const router = makeRouter()
    router.beforeEach(createAuthGuard())
    await router.push('/protected')
    expect(router.currentRoute.value.path).toBe('/protected')
  })

  it('已登录但 user 为空时拉取 profile 后放行', async () => {
    const spy = vi.spyOn(authApiModule, 'authApi', 'get').mockReturnValue({
      login: vi.fn(),
      logout: vi.fn(),
      getProfile: vi.fn().mockResolvedValue({ profile: mkUser(['Admin']), permissions: ['*'] }),
    } as unknown as typeof authApiModule.authApi)
    const auth = useAuthStore()
    auth.token = 'tok'
    auth.expiresAt = Date.now() + 100_000
    const router = makeRouter()
    router.beforeEach(createAuthGuard())
    await router.push('/protected')
    expect(auth.user?.username).toBe('a')
    expect(router.currentRoute.value.path).toBe('/protected')
    spy.mockRestore()
  })

  it('fetchProfile 失败时登出并跳 /login', async () => {
    const spy = vi.spyOn(authApiModule, 'authApi', 'get').mockReturnValue({
      login: vi.fn(),
      logout: vi.fn().mockResolvedValue(undefined),
      getProfile: vi.fn().mockRejectedValue(new Error('network')),
    } as unknown as typeof authApiModule.authApi)
    const auth = useAuthStore()
    auth.token = 'tok'
    auth.expiresAt = Date.now() + 100_000
    const router = makeRouter()
    router.beforeEach(createAuthGuard())
    await router.push('/protected')
    expect(auth.token).toBeNull()
    expect(router.currentRoute.value.path).toBe('/login')
    spy.mockRestore()
  })

  it('角色不足跳 /403', async () => {
    const auth = useAuthStore()
    auth.token = 'tok'
    auth.expiresAt = Date.now() + 100_000
    auth.user = mkUser(['Operator'])
    auth.roles = ['Operator']
    const router = makeRouter()
    router.beforeEach(createAuthGuard())
    await router.push('/admin-only')
    expect(router.currentRoute.value.path).toBe('/403')
  })

  it('权限不足跳 /403（meta.permission 校验）', async () => {
    const auth = useAuthStore()
    auth.token = 'tok'
    auth.expiresAt = Date.now() + 100_000
    auth.user = mkUser(['Operator'])
    auth.roles = ['Operator']
    auth.permissions = ['dashboard:view']
    const router = makeRouter()
    router.beforeEach(createAuthGuard())
    await router.push('/perm')
    expect(router.currentRoute.value.path).toBe('/403')
  })

  it('权限满足时放行（meta.permission 校验）', async () => {
    const auth = useAuthStore()
    auth.token = 'tok'
    auth.expiresAt = Date.now() + 100_000
    auth.user = mkUser(['Operator'])
    auth.roles = ['Operator']
    auth.permissions = ['product:audit']
    const router = makeRouter()
    router.beforeEach(createAuthGuard())
    await router.push('/perm')
    expect(router.currentRoute.value.path).toBe('/perm')
  })

  it('已登录访问 /login 跳首页', async () => {
    const auth = useAuthStore()
    auth.token = 'tok'
    auth.expiresAt = Date.now() + 100_000
    auth.user = mkUser(['Admin'])
    auth.roles = ['Admin']
    const router = makeRouter()
    router.beforeEach(createAuthGuard())
    await router.push('/login')
    expect(router.currentRoute.value.path).toBe('/')
  })

  it('静态路由表包含 5 个框架页与 /login', () => {
    const paths = ['/login', '/403', '/404', '/500', '/maintenance', '/rate-limited']
    const registered = appRouter.getRoutes().map((r) => r.path)
    for (const p of paths) {
      expect(registered).toContain(p)
    }
  })
})
