import { describe, it, expect, vi } from 'vitest'
import { mount } from '@vue/test-utils'
import { defineComponent, h } from 'vue'

// Mock vue-echarts：jsdom 下 ECharts 无 canvas 渲染器无法实际渲染，
// 用占位组件替代；图表 spec 仅验证外层容器与状态切换。
vi.mock('vue-echarts', () => ({
  default: defineComponent({
    name: 'VChart',
    props: ['option', 'autoresize'],
    setup() {
      return () => h('div', { class: 'v-chart-stub' })
    },
  }),
}))

import ChartLine from './ChartLine.vue'

describe('shared/components/charts/ChartLine', () => {
  it('传入 series 与 xAxis 渲染 echarts 容器', () => {
    const wrapper = mount(ChartLine, {
      props: {
        series: [{ name: '订单', type: 'line', data: [1, 2, 3] }],
        xAxis: ['2026-07-25', '2026-07-26', '2026-07-27'],
      },
    })
    expect(wrapper.html()).toContain('chart-line')
  })

  it('loading=true 显示加载态', () => {
    const wrapper = mount(ChartLine, {
      props: {
        series: [],
        xAxis: [],
        loading: true,
      },
    })
    expect(wrapper.html()).toContain('ant-spin')
  })

  it('未传 series 时显示空态', () => {
    const wrapper = mount(ChartLine, {
      props: { series: [], xAxis: [] },
    })
    expect(wrapper.html()).toContain('ant-empty')
  })
})
