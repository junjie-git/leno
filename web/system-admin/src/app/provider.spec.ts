import { describe, it, expect } from 'vitest'
import { mount } from '@vue/test-utils'
import Provider from './provider.vue'

describe('app/provider', () => {
  it('渲染 slot 内容', () => {
    const wrapper = mount(Provider, {
      slots: { default: '<div class="slot-content">content</div>' },
    })
    expect(wrapper.html()).toContain('slot-content')
  })
})
