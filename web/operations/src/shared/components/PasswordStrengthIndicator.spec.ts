import { describe, it, expect } from 'vitest'
import { mount } from '@vue/test-utils'
import PasswordStrengthIndicator from './PasswordStrengthIndicator.vue'

describe('PasswordStrengthIndicator', () => {
  it('空密码不渲染', () => {
    const wrapper = mount(PasswordStrengthIndicator, { props: { password: '' } })
    expect(wrapper.find('.password-strength').exists()).toBe(false)
  })

  it('长度<8 为弱', () => {
    const wrapper = mount(PasswordStrengthIndicator, { props: { password: 'abc123' } })
    expect(wrapper.text()).toContain('弱')
  })

  it('长度≥8 且含2类字符 为中', () => {
    const wrapper = mount(PasswordStrengthIndicator, { props: { password: 'abcdef12' } })
    expect(wrapper.text()).toContain('中')
  })

  it('长度≥12 且含3类字符 为强', () => {
    const wrapper = mount(PasswordStrengthIndicator, { props: { password: 'Abcdefgh123!' } })
    expect(wrapper.text()).toContain('强')
  })
})
