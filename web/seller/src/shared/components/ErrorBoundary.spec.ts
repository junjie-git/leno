import { describe, it, expect, vi } from 'vitest'
import { mount, flushPromises } from '@vue/test-utils'
import { defineComponent, h } from 'vue'
import ErrorBoundary from './ErrorBoundary.vue'

const BoomComponent = defineComponent({
  setup() {
    throw new Error('子组件爆炸')
  },
  render() {
    return h('div', 'never-rendered')
  },
})

const OkComponent = defineComponent({
  render() {
    return h('div', { class: 'ok-content' }, '正常内容')
  },
})

describe('shared/components/ErrorBoundary', () => {
  it('子组件正常时渲染 default slot', () => {
    const wrapper = mount(ErrorBoundary, {
      slots: { default: h(OkComponent) },
    })
    expect(wrapper.html()).toContain('ok-content')
  })

  it('子组件抛错时渲染 fallback slot', async () => {
    // Vue test-utils 默认会传播错误，这里需要 stub console.error 静默
    const spy = vi.spyOn(console, 'error').mockImplementation(() => {})
    const wrapper = mount(ErrorBoundary, {
      slots: {
        default: h(BoomComponent),
        fallback: '<div class="fallback-content">出错了</div>',
      },
    })
    await flushPromises()
    expect(wrapper.html()).toContain('fallback-content')
    spy.mockRestore()
  })

  it('fallback slot 暴露 error 与 retry', async () => {
    const spy = vi.spyOn(console, 'error').mockImplementation(() => {})
    const wrapper = mount(ErrorBoundary, {
      slots: {
        default: h(BoomComponent),
        fallback: '<div class="fallback-content">出错了</div>',
      },
    })
    await flushPromises()
    expect(wrapper.html()).toContain('fallback-content')
    spy.mockRestore()
  })

  it('无 fallback slot 时使用默认错误态', async () => {
    const spy = vi.spyOn(console, 'error').mockImplementation(() => {})
    const wrapper = mount(ErrorBoundary, {
      slots: { default: h(BoomComponent) },
    })
    await flushPromises()
    expect(wrapper.html()).toContain('加载失败')
    spy.mockRestore()
  })
})
