import { describe, it, expect, beforeEach } from 'vitest'
import { mount } from '@vue/test-utils'
import { defineComponent } from 'vue'
import { createPinia, setActivePinia } from 'pinia'
import { vPermission } from './permission'
import { useAuthStore } from './auth.store'

const HostComponent = defineComponent({
  props: {
    perm: { type: String, required: true },
  },
  directives: { permission: vPermission },
  template: `
    <div class="host">
      <button class="guarded-btn" v-permission="perm">操作</button>
    </div>
  `,
})

describe('shared/auth/permission (v-permission 指令)', () => {
  beforeEach(() => {
    setActivePinia(createPinia())
  })

  it('有权限时元素可见', async () => {
    const auth = useAuthStore()
    auth.permissions = ['dead-letter:dispose']
    const wrapper = await mount(HostComponent, { props: { perm: 'dead-letter:dispose' } })
    const btn = wrapper.find('.guarded-btn')
    expect(btn.element.style.display).not.toBe('none')
  })

  it('无权限时元素被隐藏（display: none）', async () => {
    const auth = useAuthStore()
    auth.permissions = ['role:read']
    const wrapper = await mount(HostComponent, { props: { perm: 'dead-letter:dispose' } })
    const btn = wrapper.find('.guarded-btn')
    expect(btn.element.style.display).toBe('none')
  })

  it('通配符 * 拥有全部权限', async () => {
    const auth = useAuthStore()
    auth.permissions = ['*']
    const wrapper = await mount(HostComponent, { props: { perm: 'any:thing' } })
    const btn = wrapper.find('.guarded-btn')
    expect(btn.element.style.display).not.toBe('none')
  })

  it('空权限字符串不隐藏元素', async () => {
    const auth = useAuthStore()
    auth.permissions = []
    const wrapper = await mount(HostComponent, { props: { perm: '' } })
    const btn = wrapper.find('.guarded-btn')
    expect(btn.element.style.display).not.toBe('none')
  })
})
