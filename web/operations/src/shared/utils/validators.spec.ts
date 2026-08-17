import { describe, it, expect } from 'vitest'
import {
  isNonEmptyString,
  isValidEmail,
  isValidUsername,
  isValidPassword,
  isPositiveInteger,
  isInRange,
  isUuid,
} from './validators'

describe('shared/utils/validators', () => {
  it('isNonEmptyString', () => {
    expect(isNonEmptyString('abc')).toBe(true)
    expect(isNonEmptyString('  ')).toBe(false)
    expect(isNonEmptyString('')).toBe(false)
    expect(isNonEmptyString(null)).toBe(false)
    expect(isNonEmptyString(undefined)).toBe(false)
  })

  it('isValidEmail', () => {
    expect(isValidEmail('admin@leno.com')).toBe(true)
    expect(isValidEmail('a.b+c@sub.leno.cn')).toBe(true)
    expect(isValidEmail('admin@leno')).toBe(false)
    expect(isValidEmail('admin.leno.com')).toBe(false)
    expect(isValidEmail('')).toBe(false)
  })

  it('isValidUsername: 4-32 位字母数字下划线', () => {
    expect(isValidUsername('admin')).toBe(true)
    expect(isValidUsername('user_01')).toBe(true)
    expect(isValidUsername('a')).toBe(false)
    expect(isValidUsername('a'.repeat(33))).toBe(false)
    expect(isValidUsername('用户名')).toBe(false)
    expect(isValidUsername('user-name')).toBe(false)
  })

  it('isValidPassword: 至少 8 位含字母与数字', () => {
    expect(isValidPassword('Admin123')).toBe(true)
    expect(isValidPassword('admin123')).toBe(true)
    expect(isValidPassword('ADMIN123')).toBe(true)
    expect(isValidPassword('12345678')).toBe(false)
    expect(isValidPassword('aaaaaaaa')).toBe(false)
    expect(isValidPassword('Adm1')).toBe(false)
  })

  it('isPositiveInteger', () => {
    expect(isPositiveInteger(1)).toBe(true)
    expect(isPositiveInteger(100)).toBe(true)
    expect(isPositiveInteger(0)).toBe(false)
    expect(isPositiveInteger(-1)).toBe(false)
    expect(isPositiveInteger(1.5)).toBe(false)
    expect(isPositiveInteger('1')).toBe(false)
  })

  it('isInRange', () => {
    expect(isInRange(5, 1, 10)).toBe(true)
    expect(isInRange(1, 1, 10)).toBe(true)
    expect(isInRange(10, 1, 10)).toBe(true)
    expect(isInRange(0, 1, 10)).toBe(false)
    expect(isInRange(11, 1, 10)).toBe(false)
  })

  it('isUuid', () => {
    expect(isUuid('550e8400-e29b-41d4-a716-446655440000')).toBe(true)
    expect(isUuid('not-a-uuid')).toBe(false)
    expect(isUuid('')).toBe(false)
  })
})
