import { mount } from '@vue/test-utils'
import { describe, expect, it } from 'vitest'
import PriceText from '@/shared/components/PriceText.vue'
import EmptyState from '@/shared/components/EmptyState.vue'
import ErrorState from '@/shared/components/ErrorState.vue'

describe('PriceText（价签排版）', () => {
  it('整数金额只渲染整数部分', () => {
    const wrapper = mount(PriceText, { props: { amount: 9900, size: 16 } })
    expect(wrapper.text()).toContain('99')
    expect(wrapper.find('.dec').exists()).toBe(false)
  })

  it('带小数金额渲染小数部分', () => {
    const wrapper = mount(PriceText, { props: { amount: 1999, size: 16 } })
    expect(wrapper.find('.int').text()).toBe('19')
    expect(wrapper.find('.dec').text()).toBe('.99')
  })

  it('展示划线原价', () => {
    const wrapper = mount(PriceText, { props: { amount: 1999, original: 2599 } })
    expect(wrapper.find('.original').text()).toContain('25.99')
  })

  it('自定义颜色生效', () => {
    const wrapper = mount(PriceText, { props: { amount: 100, color: '#1677FF' } })
    expect(wrapper.attributes('style')).toContain('color: rgb(22, 119, 255)')
  })
})

describe('EmptyState（空状态）', () => {
  it('渲染标题与 CTA 并派发 action 事件', async () => {
    const wrapper = mount(EmptyState, {
      props: { title: '该分类下暂无商品', actionText: '去逛逛' },
    })
    expect(wrapper.text()).toContain('该分类下暂无商品')
    await wrapper.find('.cta').trigger('click')
    expect(wrapper.emitted('action')).toHaveLength(1)
  })

  it('未传 actionText 时不渲染按钮', () => {
    const wrapper = mount(EmptyState, { props: { title: '暂无数据' } })
    expect(wrapper.find('.cta').exists()).toBe(false)
  })
})

describe('ErrorState（错误状态）', () => {
  it('渲染默认文案与重试按钮并派发 retry 事件', async () => {
    const wrapper = mount(ErrorState)
    expect(wrapper.text()).toContain('加载失败')
    expect(wrapper.text()).toContain('网络异常，请检查网络连接后重试')
    await wrapper.find('.retry').trigger('click')
    expect(wrapper.emitted('retry')).toHaveLength(1)
  })

  it('支持自定义标题/描述/按钮文案', () => {
    const wrapper = mount(ErrorState, {
      props: {
        title: '商品已下架',
        description: '该商品不存在或已下架',
        retryText: '返回首页',
      },
    })
    expect(wrapper.text()).toContain('商品已下架')
    expect(wrapper.text()).toContain('返回首页')
  })
})
