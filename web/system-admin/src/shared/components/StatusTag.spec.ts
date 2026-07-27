import { describe, it, expect } from 'vitest'
import { mount } from '@vue/test-utils'
import StatusTag from './StatusTag.vue'

describe('shared/components/StatusTag', () => {
  it('deadLetter 类型 + Pending 状态渲染黄色 warning tag', () => {
    const wrapper = mount(StatusTag, { props: { type: 'deadLetter', status: 'Pending' } })
    expect(wrapper.html()).toContain('ant-tag')
    expect(wrapper.html()).toContain('待处理')
    expect(wrapper.html()).toContain('ant-tag-warning')
  })

  it('deadLetter 类型 + Retried 状态渲染绿色 success tag', () => {
    const wrapper = mount(StatusTag, { props: { type: 'deadLetter', status: 'Retried' } })
    expect(wrapper.html()).toContain('已重投')
    expect(wrapper.html()).toContain('ant-tag-success')
  })

  it('deadLetter 类型 + Discarded 状态渲染红色 error tag', () => {
    const wrapper = mount(StatusTag, { props: { type: 'deadLetter', status: 'Discarded' } })
    expect(wrapper.html()).toContain('已丢弃')
    expect(wrapper.html()).toContain('ant-tag-error')
  })

  it('orderPayment 类型 + Paid 状态渲染 success tag', () => {
    const wrapper = mount(StatusTag, { props: { type: 'orderPayment', status: 'Paid' } })
    expect(wrapper.html()).toContain('已支付')
    expect(wrapper.html()).toContain('ant-tag-success')
  })

  it('orderPayment 类型 + Pending 状态渲染 warning tag', () => {
    const wrapper = mount(StatusTag, { props: { type: 'orderPayment', status: 'Pending' } })
    expect(wrapper.html()).toContain('待支付')
  })

  it('shop 类型 + Approved 状态渲染 success tag', () => {
    const wrapper = mount(StatusTag, { props: { type: 'shop', status: 'Approved' } })
    expect(wrapper.html()).toContain('已通过')
  })

  it('shop 类型 + Banned 状态渲染 error tag', () => {
    const wrapper = mount(StatusTag, { props: { type: 'shop', status: 'Banned' } })
    expect(wrapper.html()).toContain('已封禁')
  })

  it('未知状态渲染 default 灰色 tag', () => {
    const wrapper = mount(StatusTag, { props: { type: 'deadLetter', status: 'UnknownStatus' } })
    expect(wrapper.html()).toContain('UnknownStatus')
    expect(wrapper.html()).toContain('ant-tag')
  })
})
