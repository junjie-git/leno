import { mount } from '@vue/test-utils'
import { describe, expect, it } from 'vitest'
import TodoBadge from './TodoBadge.vue'

describe('TodoBadge', () => {
  it('count > 0 时显示徽标', () => {
    const wrapper = mount(TodoBadge, {
      props: { count: 5, label: '待发货' },
    })
    expect(wrapper.text()).toContain('待发货')
  })

  it('count === 0 时不显示数字徽标', () => {
    const wrapper = mount(TodoBadge, {
      props: { count: 0, label: '待发货' },
    })
    expect(wrapper.text()).toContain('待发货')
  })

  it('点击触发 click 事件', async () => {
    const wrapper = mount(TodoBadge, {
      props: { count: 3, label: '售后' },
    })
    // click 绑定在内部 .todo-badge-label span 上，需定位到该元素触发
    await wrapper.find('.todo-badge-label').trigger('click')
    expect(wrapper.emitted('click')).toBeTruthy()
  })
})
