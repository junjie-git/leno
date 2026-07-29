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

  // ===== 卖家 shop 状态映射（替换 system-admin 的 shop 映射）=====
  it('shop 类型 + Active 状态渲染 success tag（正常）', () => {
    const wrapper = mount(StatusTag, { props: { type: 'shop', status: 'Active' } })
    expect(wrapper.html()).toContain('正常')
    expect(wrapper.html()).toContain('ant-tag-success')
  })

  it('shop 类型 + PendingReview 状态渲染 warning tag（审核中）', () => {
    const wrapper = mount(StatusTag, { props: { type: 'shop', status: 'PendingReview' } })
    expect(wrapper.html()).toContain('审核中')
    expect(wrapper.html()).toContain('ant-tag-warning')
  })

  it('shop 类型 + Suspended 状态渲染 error tag（暂停）', () => {
    const wrapper = mount(StatusTag, { props: { type: 'shop', status: 'Suspended' } })
    expect(wrapper.html()).toContain('暂停')
    expect(wrapper.html()).toContain('ant-tag-error')
  })

  it('shop 类型 + Rejected 状态渲染 error tag（已驳回）', () => {
    const wrapper = mount(StatusTag, { props: { type: 'shop', status: 'Rejected' } })
    expect(wrapper.html()).toContain('已驳回')
    expect(wrapper.html()).toContain('ant-tag-error')
  })

  // ===== product 商品状态映射 =====
  it('product 类型 + Draft 状态渲染 default tag（草稿）', () => {
    const wrapper = mount(StatusTag, { props: { type: 'product', status: 'Draft' } })
    expect(wrapper.html()).toContain('草稿')
  })

  it('product 类型 + PendingReview 状态渲染 warning tag（待审核）', () => {
    const wrapper = mount(StatusTag, { props: { type: 'product', status: 'PendingReview' } })
    expect(wrapper.html()).toContain('待审核')
    expect(wrapper.html()).toContain('ant-tag-warning')
  })

  it('product 类型 + Approved 状态渲染 success tag（已上架）', () => {
    const wrapper = mount(StatusTag, { props: { type: 'product', status: 'Approved' } })
    expect(wrapper.html()).toContain('已上架')
    expect(wrapper.html()).toContain('ant-tag-success')
  })

  it('product 类型 + TakenDown 状态渲染 default tag（已下架）', () => {
    const wrapper = mount(StatusTag, { props: { type: 'product', status: 'TakenDown' } })
    expect(wrapper.html()).toContain('已下架')
  })

  it('product 类型 + Rejected 状态渲染 error tag（已驳回）', () => {
    const wrapper = mount(StatusTag, { props: { type: 'product', status: 'Rejected' } })
    expect(wrapper.html()).toContain('已驳回')
    expect(wrapper.html()).toContain('ant-tag-error')
  })

  // ===== order 卖家订单状态映射 =====
  it('order 类型 + PendingShipment 状态渲染 warning tag（待发货）', () => {
    const wrapper = mount(StatusTag, { props: { type: 'order', status: 'PendingShipment' } })
    expect(wrapper.html()).toContain('待发货')
    expect(wrapper.html()).toContain('ant-tag-warning')
  })

  it('order 类型 + Shipped 状态渲染 processing tag（已发货）', () => {
    const wrapper = mount(StatusTag, { props: { type: 'order', status: 'Shipped' } })
    expect(wrapper.html()).toContain('已发货')
    expect(wrapper.html()).toContain('ant-tag-processing')
  })

  it('order 类型 + Delivered 状态渲染 processing tag（已送达）', () => {
    const wrapper = mount(StatusTag, { props: { type: 'order', status: 'Delivered' } })
    expect(wrapper.html()).toContain('已送达')
    expect(wrapper.html()).toContain('ant-tag-processing')
  })

  it('order 类型 + Completed 状态渲染 success tag（已完成）', () => {
    const wrapper = mount(StatusTag, { props: { type: 'order', status: 'Completed' } })
    expect(wrapper.html()).toContain('已完成')
    expect(wrapper.html()).toContain('ant-tag-success')
  })

  it('order 类型 + Cancelled 状态渲染 default tag（已取消）', () => {
    const wrapper = mount(StatusTag, { props: { type: 'order', status: 'Cancelled' } })
    expect(wrapper.html()).toContain('已取消')
  })

  it('order 类型 + Refunded 状态渲染 default tag（已退款）', () => {
    const wrapper = mount(StatusTag, { props: { type: 'order', status: 'Refunded' } })
    expect(wrapper.html()).toContain('已退款')
  })

  // ===== aftersales 售后单状态映射 =====
  it('aftersales 类型 + Pending 状态渲染 warning tag（待处理）', () => {
    const wrapper = mount(StatusTag, { props: { type: 'aftersales', status: 'Pending' } })
    expect(wrapper.html()).toContain('待处理')
    expect(wrapper.html()).toContain('ant-tag-warning')
  })

  it('aftersales 类型 + Approved 状态渲染 processing tag（已同意）', () => {
    const wrapper = mount(StatusTag, { props: { type: 'aftersales', status: 'Approved' } })
    expect(wrapper.html()).toContain('已同意')
    expect(wrapper.html()).toContain('ant-tag-processing')
  })

  it('aftersales 类型 + Rejected 状态渲染 error tag（已拒绝）', () => {
    const wrapper = mount(StatusTag, { props: { type: 'aftersales', status: 'Rejected' } })
    expect(wrapper.html()).toContain('已拒绝')
    expect(wrapper.html()).toContain('ant-tag-error')
  })

  it('aftersales 类型 + ReturnInProgress 状态渲染 processing tag（退货中）', () => {
    const wrapper = mount(StatusTag, { props: { type: 'aftersales', status: 'ReturnInProgress' } })
    expect(wrapper.html()).toContain('退货中')
    expect(wrapper.html()).toContain('ant-tag-processing')
  })

  it('aftersales 类型 + Refunded 状态渲染 success tag（已退款）', () => {
    const wrapper = mount(StatusTag, { props: { type: 'aftersales', status: 'Refunded' } })
    expect(wrapper.html()).toContain('已退款')
    expect(wrapper.html()).toContain('ant-tag-success')
  })

  it('aftersales 类型 + Closed 状态渲染 default tag（已关闭）', () => {
    const wrapper = mount(StatusTag, { props: { type: 'aftersales', status: 'Closed' } })
    expect(wrapper.html()).toContain('已关闭')
  })

  // ===== freightTemplate 运费模板状态映射 =====
  it('freightTemplate 类型 + Enabled 状态渲染 success tag（启用）', () => {
    const wrapper = mount(StatusTag, { props: { type: 'freightTemplate', status: 'Enabled' } })
    expect(wrapper.html()).toContain('启用')
    expect(wrapper.html()).toContain('ant-tag-success')
  })

  it('freightTemplate 类型 + Disabled 状态渲染 default tag（禁用）', () => {
    const wrapper = mount(StatusTag, { props: { type: 'freightTemplate', status: 'Disabled' } })
    expect(wrapper.html()).toContain('禁用')
  })

  it('未知状态渲染 default 灰色 tag', () => {
    const wrapper = mount(StatusTag, { props: { type: 'deadLetter', status: 'UnknownStatus' } })
    expect(wrapper.html()).toContain('UnknownStatus')
    expect(wrapper.html()).toContain('ant-tag')
  })
})
