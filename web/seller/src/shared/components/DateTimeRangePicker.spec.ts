import { describe, it, expect } from 'vitest'
import { mount } from '@vue/test-utils'
import DateTimeRangePicker from './DateTimeRangePicker.vue'

describe('shared/components/DateTimeRangePicker', () => {
  it('未传 value 时不报错', () => {
    const wrapper = mount(DateTimeRangePicker, { props: {} })
    expect(wrapper.html()).toContain('ant-picker')
  })

  it('传入 value 时渲染日期', () => {
    const wrapper = mount(DateTimeRangePicker, {
      props: { value: ['2026-07-27T00:00:00Z', '2026-07-28T00:00:00Z'] },
    })
    expect(wrapper.html()).toContain('ant-picker')
  })

  it('change 事件输出 ISO 8601 UTC 字符串数组', async () => {
    const wrapper = mount(DateTimeRangePicker, { props: {} })
    const input = wrapper.find('input')
    expect(input.exists()).toBe(true)
    // 验证组件 expose 的 onChange 方法
    const vm = wrapper.vm as unknown as { onChange: (val: [Date, Date]) => void }
    vm.onChange([new Date(Date.UTC(2026, 6, 27, 0, 0, 0)), new Date(Date.UTC(2026, 6, 28, 0, 0, 0))])
    const events = wrapper.emitted('change')
    expect(events).toBeTruthy()
    const payload = events?.[0]?.[0] as [string, string]
    expect(payload[0]).toMatch(/^2026-07-27T\d{2}:\d{2}:\d{2}.\d{3}Z$/)
    expect(payload[1]).toMatch(/^2026-07-28T\d{2}:\d{2}:\d{2}.\d{3}Z$/)
  })

  it('showTime=true 时显示时间选择', () => {
    const wrapper = mount(DateTimeRangePicker, {
      props: { showTime: true },
    })
    expect(wrapper.html()).toContain('ant-picker')
  })
})
