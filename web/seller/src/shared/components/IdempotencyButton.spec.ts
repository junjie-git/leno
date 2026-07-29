import { describe, it, expect } from 'vitest'
import { mount } from '@vue/test-utils'
import IdempotencyButton from './IdempotencyButton.vue'

describe('shared/components/IdempotencyButton', () => {
  it('默认 type=primary 渲染 a-button primary', () => {
    const wrapper = mount(IdempotencyButton, {
      props: {},
      slots: { default: '提交' },
    })
    expect(wrapper.text()).toMatch(/提.*交/)
    expect(wrapper.html()).toContain('ant-btn-primary')
  })

  it('danger=true 渲染 danger 样式', () => {
    const wrapper = mount(IdempotencyButton, {
      props: { danger: true },
      slots: { default: '删除' },
    })
    expect(wrapper.html()).toContain('ant-btn-dangerous')
  })

  it('loading=true 禁用并显示 loading', () => {
    const wrapper = mount(IdempotencyButton, {
      props: { loading: true },
      slots: { default: '提交' },
    })
    expect(wrapper.html()).toContain('ant-btn-loading')
  })

  it('点击触发 click 事件', async () => {
    const wrapper = mount(IdempotencyButton, {
      props: {},
      slots: { default: '提交' },
    })
    await wrapper.find('button').trigger('click')
    expect(wrapper.emitted('click')).toBeTruthy()
    expect(wrapper.emitted('click')?.[0]).toBeDefined()
  })

  it('loading 时点击不触发 click', async () => {
    const wrapper = mount(IdempotencyButton, {
      props: { loading: true },
      slots: { default: '提交' },
    })
    await wrapper.find('button').trigger('click')
    expect(wrapper.emitted('click')).toBeFalsy()
  })

  it('size=small 渲染小尺寸', () => {
    const wrapper = mount(IdempotencyButton, {
      props: { size: 'small' },
      slots: { default: '提交' },
    })
    expect(wrapper.html()).toContain('ant-btn-sm')
  })

  it('type=default 渲染默认按钮', () => {
    const wrapper = mount(IdempotencyButton, {
      props: { type: 'default' },
      slots: { default: '取消' },
    })
    expect(wrapper.html()).not.toContain('ant-btn-primary')
  })

  it('disabled=true 禁用', () => {
    const wrapper = mount(IdempotencyButton, {
      props: { disabled: true },
      slots: { default: '提交' },
    })
    expect(wrapper.find('button').attributes('disabled')).toBeDefined()
  })
})
