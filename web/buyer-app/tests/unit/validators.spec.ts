import { describe, expect, it } from 'vitest'
import {
  isValidAccount,
  isValidAddressDetail,
  isValidEmail,
  isValidPassword,
  isValidPhone,
  isValidReceiverName,
  isValidUsername,
  isValidVerifyCode,
  isValidYuanAmount,
} from '@/shared/utils/validators'

describe('isValidUsername', () => {
  it('合法：字母/数字/下划线/中文，3-32 位', () => {
    expect(isValidUsername('zhangsan')).toBe(true)
    expect(isValidUsername('张小雅')).toBe(true)
    expect(isValidUsername('user_01')).toBe(true)
  })

  it('非法：过短/过长/特殊字符', () => {
    expect(isValidUsername('ab')).toBe(false)
    expect(isValidUsername('a'.repeat(33))).toBe(false)
    expect(isValidUsername('user@01')).toBe(false)
    expect(isValidUsername('')).toBe(false)
  })
})

describe('isValidPhone', () => {
  it('合法中国大陆手机号', () => {
    expect(isValidPhone('13812345678')).toBe(true)
    expect(isValidPhone('19912345678')).toBe(true)
  })

  it('非法号段与位数', () => {
    expect(isValidPhone('12812345678')).toBe(false)
    expect(isValidPhone('1381234567')).toBe(false)
    expect(isValidPhone('138123456789')).toBe(false)
  })
})

describe('isValidEmail', () => {
  it('合法邮箱', () => {
    expect(isValidEmail('user@example.com')).toBe(true)
    expect(isValidEmail('first.last+tag@sub.domain.cn')).toBe(true)
  })

  it('非法邮箱', () => {
    expect(isValidEmail('user@')).toBe(false)
    expect(isValidEmail('@example.com')).toBe(false)
    expect(isValidEmail('user@@example.com')).toBe(false)
  })
})

describe('isValidAccount（用户名/手机号/邮箱任一）', () => {
  it('三种形态均通过', () => {
    expect(isValidAccount('zhangsan')).toBe(true)
    expect(isValidAccount('13812345678')).toBe(true)
    expect(isValidAccount('user@example.com')).toBe(true)
  })

  it('均不满足时失败', () => {
    expect(isValidAccount('no')).toBe(false)
    expect(isValidAccount('bad@@mail')).toBe(false)
  })
})

describe('isValidPassword', () => {
  it('至少包含字母和数字，6-32 位', () => {
    expect(isValidPassword('Zhang123456')).toBe(true)
    expect(isValidPassword('abc123')).toBe(true)
  })

  it('纯字母/纯数字/过短均拒绝', () => {
    expect(isValidPassword('abcdef')).toBe(false)
    expect(isValidPassword('123456')).toBe(false)
    expect(isValidPassword('a1')).toBe(false)
    expect(isValidPassword('a'.repeat(32) + '1')).toBe(false)
  })
})

describe('isValidVerifyCode', () => {
  it('6 位数字', () => {
    expect(isValidVerifyCode('123456')).toBe(true)
    expect(isValidVerifyCode('12345')).toBe(false)
    expect(isValidVerifyCode('1234567')).toBe(false)
    expect(isValidVerifyCode('12a456')).toBe(false)
  })
})

describe('isValidReceiverName', () => {
  it('2-20 位字符（含首尾空白）', () => {
    expect(isValidReceiverName('张三')).toBe(true)
    expect(isValidReceiverName('  张三  ')).toBe(true)
    expect(isValidReceiverName('张')).toBe(false)
    expect(isValidReceiverName('张'.repeat(21))).toBe(false)
  })
})

describe('isValidAddressDetail', () => {
  it('5-100 位', () => {
    expect(isValidAddressDetail('福建省福州市鼓楼区软件大道 89 号')).toBe(true)
    expect(isValidAddressDetail('福州市')).toBe(false)
    expect(isValidAddressDetail('a'.repeat(101))).toBe(false)
  })
})

describe('isValidYuanAmount', () => {
  it('整数或最多两位小数', () => {
    expect(isValidYuanAmount('199')).toBe(true)
    expect(isValidYuanAmount('19.9')).toBe(true)
    expect(isValidYuanAmount('19.99')).toBe(true)
  })

  it('非法格式', () => {
    expect(isValidYuanAmount('19.999')).toBe(false)
    expect(isValidYuanAmount('.5')).toBe(false)
    expect(isValidYuanAmount('-1')).toBe(false)
    expect(isValidYuanAmount('')).toBe(false)
  })
})
