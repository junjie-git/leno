import { describe, it, expect, beforeEach, vi, afterEach } from 'vitest'
import { mount, flushPromises } from '@vue/test-utils'
import { createPinia, setActivePinia } from 'pinia'
import piniaPluginPersistedstate from 'pinia-plugin-persistedstate'
import { createRouter, createMemoryHistory, type Router } from 'vue-router'
import Login2fa from './Login2fa.vue'
import * as authApiModule from '@/modules/06-account/api/auth.api'
import { UnauthorizedError, RateLimitedError } from '@/shared/http/errors'

vi.mock('ant-design-vue', async () => {
  const actual = await vi.importActual<typeof import('ant-design-vue')>('ant-design-vue')
  return {
    ...actual,
    message: { success: vi.fn(), error: vi.fn(), warning: vi.fn() },
    Modal: { info: vi.fn(), confirm: vi.fn() },
  }
})

function makePinia() {
  const pinia = createPinia()
  pinia.use(piniaPluginPersistedstate)
  setActivePinia(pinia)
  return pinia
}

async function mountLogin(redirect?: string) {
  const pinia = makePinia()
  const router: Router = createRouter({
    history: createMemoryHistory(),
    routes: [
      { path: '/login', component: Login2fa },
      { path: '/dashboard/operations-overview', component: { template: '<div>dashboard</div>' } },
      { path: '/foo', component: { template: '<div>foo</div>' } },
    ],
  })
  await router.push(redirect ? `/login?redirect=${encodeURIComponent(redirect)}` : '/login')
  await router.isReady()
  const wrapper = mount(Login2fa, { global: { plugins: [pinia, router] } })
  return { wrapper, router }
}

describe('modules/06-account/views/Login2fa', () => {
  beforeEach(() => {
    localStorage.clear()
  })
  afterEach(() => {
    vi.restoreAllMocks()
  })

  it('渲染品牌区、登录表单与 2FA 预览角标', async () => {
    const { wrapper } = await mountLogin()
    expect(wrapper.text()).toContain('Leno 系统管理后台')
    expect(wrapper.find('input[placeholder="请输入用户名"]').exists()).toBe(true)
    expect(wrapper.find('input[placeholder="请输入密码"]').exists()).toBe(true)
    expect(wrapper.text()).toContain('2FA 暂未启用')
  })

  it('空表单提交显示校验错误', async () => {
    const { wrapper } = await mountLogin()
    await wrapper.find('form button.ant-btn-primary').trigger('click')
    await flushPromises()
    expect(wrapper.text()).toContain('请输入用户名')
  })

  it('登录成功后跳转 redirect 路径并持久化 token', async () => {
    const fakeResult = {
      token: 'tok-1',
      expiresIn: 3600,
      user: { id: 'u1', username: 'admin', email: 'a@l.com', status: 'Active', roles: ['Admin'] },
      roles: ['Admin'],
      permissions: ['*'],
    }
    const spy = vi.spyOn(authApiModule, 'authApi', 'get').mockReturnValue({
      login: vi.fn().mockResolvedValue(fakeResult),
      logout: vi.fn(),
      getProfile: vi.fn(),
    } as unknown as typeof authApiModule.authApi)
    const { wrapper, router } = await mountLogin('/foo')
    await wrapper.find('input[placeholder="请输入用户名"]').setValue('admin')
    await wrapper.find('input[placeholder="请输入密码"]').setValue('Admin123')
    await wrapper.find('form button.ant-btn-primary').trigger('click')
    await flushPromises()
    await flushPromises()
    expect(router.currentRoute.value.path).toBe('/foo')
    const persisted = JSON.parse(localStorage.getItem('auth') ?? '{}')
    expect(persisted.token).toBe('tok-1')
    spy.mockRestore()
  })

  it('401 显示「账号或密码错误」', async () => {
    const spy = vi.spyOn(authApiModule, 'authApi', 'get').mockReturnValue({
      login: vi.fn().mockRejectedValue(new UnauthorizedError()),
      logout: vi.fn(),
      getProfile: vi.fn(),
    } as unknown as typeof authApiModule.authApi)
    const { wrapper } = await mountLogin()
    await wrapper.find('input[placeholder="请输入用户名"]').setValue('admin')
    await wrapper.find('input[placeholder="请输入密码"]').setValue('wrong1')
    await wrapper.find('form button.ant-btn-primary').trigger('click')
    await flushPromises()
    await flushPromises()
    expect(wrapper.text()).toContain('账号或密码错误')
    spy.mockRestore()
  })

  it('429 显示倒计时文案', async () => {
    const spy = vi.spyOn(authApiModule, 'authApi', 'get').mockReturnValue({
      login: vi.fn().mockRejectedValue(new RateLimitedError('限流', 30)),
      logout: vi.fn(),
      getProfile: vi.fn(),
    } as unknown as typeof authApiModule.authApi)
    const { wrapper } = await mountLogin()
    await wrapper.find('input[placeholder="请输入用户名"]').setValue('admin')
    await wrapper.find('input[placeholder="请输入密码"]').setValue('Admin123')
    await wrapper.find('form button.ant-btn-primary').trigger('click')
    await flushPromises()
    await flushPromises()
    expect(wrapper.text()).toContain('30 秒后重试')
    spy.mockRestore()
  })
})
