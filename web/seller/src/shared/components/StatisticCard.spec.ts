import { describe, it, expect } from 'vitest'
import { mount } from '@vue/test-utils'
import StatisticCard from './StatisticCard.vue'

describe('StatisticCard', () => {
  it('渲染标题与数值', () => {
    const wrapper = mount(StatisticCard, {
      props: { title: 'CPU', value: 32.5, precision: 1, unit: '%' },
    })
    expect(wrapper.text()).toContain('CPU')
    expect(wrapper.text()).toContain('32.5')
    expect(wrapper.text()).toContain('%')
  })

  it('status=danger 时数值显示红色', () => {
    const wrapper = mount(StatisticCard, {
      props: { title: '错误数', value: 100, status: 'danger' },
    })
    const valueEl = wrapper.find('.ant-statistic-content')
    expect(valueEl.attributes('style')).toContain('255, 77, 79')
  })

  it('loading=true 时显示骨架屏', () => {
    const wrapper = mount(StatisticCard, {
      props: { title: 'X', value: 1, loading: true },
    })
    expect(wrapper.find('.ant-skeleton').exists()).toBe(true)
  })

  it('trend=up 时显示向上箭头', () => {
    const wrapper = mount(StatisticCard, {
      props: { title: 'X', value: 1, trend: 'up', trendValue: 5 },
    })
    expect(wrapper.find('.anticon-arrow-up').exists()).toBe(true)
    expect(wrapper.text()).toContain('5')
  })
})
