import { describe, it, expect, beforeEach } from 'vitest'
import { mount } from '@vue/test-utils'
import { createPinia, setActivePinia } from 'pinia'
import PermissionGuard from './PermissionGuard.vue'
import { useAuthStore } from './auth.store'

describe('shared/auth/PermissionGuard', () => {
  beforeEach(() => {
    setActivePinia(createPinia())
  })

  it('有权限时渲染 slot 内容', () => {
    const auth = useAuthStore()
    auth.permissions = ['role:write']
    const wrapper = mount(PermissionGuard, {
      props: { permission: 'role:write' },
      slots: { default: '<button class="ok">编辑</button>' },
    })
    expect(wrapper.html()).toContain('class="ok"')
  })

  it('无权限时不渲染 slot 内容', () => {
    const auth = useAuthStore()
    auth.permissions = ['role:read']
    const wrapper = mount(PermissionGuard, {
      props: { permission: 'role:write' },
      slots: { default: '<button class="ok">编辑</button>' },
    })
    expect(wrapper.html()).not.toContain('class="ok"')
  })

  it('通配符 * 通过任意 permission', () => {
    const auth = useAuthStore()
    auth.permissions = ['*']
    const wrapper = mount(PermissionGuard, {
      props: { permission: 'something:else' },
      slots: { default: '<span>任意</span>' },
    })
    expect(wrapper.html()).toContain('任意')
  })
})
