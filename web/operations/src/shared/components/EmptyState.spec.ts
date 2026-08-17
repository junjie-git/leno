import { describe, it, expect } from 'vitest'
import { mount } from '@vue/test-utils'
import EmptyState from './EmptyState.vue'

describe('shared/components/EmptyState', () => {
  it('渲染 description 文本', () => {
    const wrapper = mount(EmptyState, {
      props: { description: '暂无数据' },
    })
    expect(wrapper.html()).toContain('暂无数据')
  })

  it('未提供 actionText 时不渲染按钮', () => {
    const wrapper = mount(EmptyState, {
      props: { description: '空' },
    })
    expect(wrapper.find('button').exists()).toBe(false)
  })

  it('提供 actionText 时渲染 CTA 按钮', () => {
    const wrapper = mount(EmptyState, {
      props: { description: '空', actionText: '刷新' },
    })
    expect(wrapper.text()).toMatch(/刷.*新/)
    expect(wrapper.find('button').exists()).toBe(true)
  })

  it('点击按钮触发 action 事件', async () => {
    const wrapper = mount(EmptyState, {
      props: { description: '空', actionText: '刷新' },
    })
    await wrapper.find('button').trigger('click')
    expect(wrapper.emitted('action')).toBeTruthy()
    expect(wrapper.emitted('action')?.length).toBe(1)
  })

  it('渲染 antd Empty 图标', () => {
    const wrapper = mount(EmptyState, {
      props: { description: '空' },
    })
    expect(wrapper.html()).toContain('ant-empty')
  })
})
