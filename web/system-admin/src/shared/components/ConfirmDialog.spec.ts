import { describe, it, expect } from 'vitest'
import { mount } from '@vue/test-utils'
import ConfirmDialog from './ConfirmDialog.vue'

describe('shared/components/ConfirmDialog', () => {
  it('open=false 时不渲染对话框', () => {
    const wrapper = mount(ConfirmDialog, {
      props: { open: false, title: '确认', content: '是否继续？' },
    })
    expect(wrapper.find('.ant-modal').exists()).toBe(false)
  })

  it('open=true 时渲染 title 与 content', async () => {
    const wrapper = mount(ConfirmDialog, {
      props: { open: true, title: '删除规则', content: '此操作不可撤销，是否继续？' },
    })
    await wrapper.vm.$nextTick()
    expect(wrapper.html()).toContain('删除规则')
    expect(wrapper.html()).toContain('此操作不可撤销')
  })

  it('danger=true 时确认按钮含 danger 样式', async () => {
    const wrapper = mount(ConfirmDialog, {
      props: { open: true, danger: true, title: '删除', content: '确认删除？' },
    })
    await wrapper.vm.$nextTick()
    expect(wrapper.html()).toContain('ant-btn-dangerous')
  })

  it('点击取消触发 cancel 事件', async () => {
    const wrapper = mount(ConfirmDialog, {
      props: { open: true, title: '确认', content: '继续？' },
    })
    await wrapper.vm.$nextTick()
    const cancelBtn = wrapper.findAll('button').find((b) => b.text().includes('取消'))
    expect(cancelBtn).toBeDefined()
    await cancelBtn!.trigger('click')
    expect(wrapper.emitted('cancel')).toBeTruthy()
  })

  it('requireInput 配置时未达最小长度禁用确认', async () => {
    const wrapper = mount(ConfirmDialog, {
      props: {
        open: true,
        title: '丢弃原因',
        content: '请填写丢弃原因',
        requireInput: { label: '丢弃原因', min: 5, max: 500 },
      },
    })
    await wrapper.vm.$nextTick()
    const okBtn = wrapper.findAll('button').find((b) => b.text().includes('确认'))
    expect(okBtn?.attributes('disabled')).toBeDefined()
  })

  it('requireInput 配置时达到最小长度启用确认', async () => {
    const wrapper = mount(ConfirmDialog, {
      props: {
        open: true,
        title: '丢弃原因',
        content: '请填写丢弃原因',
        requireInput: { label: '丢弃原因', min: 5, max: 500 },
      },
    })
    await wrapper.vm.$nextTick()
    const input = wrapper.find('input, textarea')
    await input.setValue('这是一段足够长的丢弃原因说明')
    const okBtn = wrapper.findAll('button').find((b) => b.text().includes('确认'))
    expect(okBtn?.attributes('disabled')).toBeUndefined()
  })

  it('点击确认（无 requireInput）触发 confirm 事件', async () => {
    const wrapper = mount(ConfirmDialog, {
      props: { open: true, title: '确认', content: '继续？' },
    })
    await wrapper.vm.$nextTick()
    const okBtn = wrapper.findAll('button').find((b) => b.text().includes('确认'))
    await okBtn!.trigger('click')
    expect(wrapper.emitted('confirm')).toBeTruthy()
  })

  it('requireInput 时 confirm 事件携带输入值', async () => {
    const wrapper = mount(ConfirmDialog, {
      props: {
        open: true,
        title: '丢弃',
        content: '原因？',
        requireInput: { label: '丢弃原因', min: 1, max: 100 },
      },
    })
    await wrapper.vm.$nextTick()
    const input = wrapper.find('input, textarea')
    await input.setValue('测试原因')
    const okBtn = wrapper.findAll('button').find((b) => b.text().includes('确认'))
    await okBtn!.trigger('click')
    const confirmEvents = wrapper.emitted('confirm')
    expect(confirmEvents).toBeTruthy()
    expect(confirmEvents?.[0]?.[0]).toBe('测试原因')
  })
})
