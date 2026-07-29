import { describe, it, expect } from 'vitest'
import { formatDateTime, formatDate, formatMoney, formatPercent, formatNumber } from './format'

describe('shared/utils/format', () => {
  it('formatDateTime 格式化 ISO 字符串为 yyyy-MM-dd HH:mm:ss', () => {
    expect(formatDateTime('2026-07-27T08:30:00Z')).toMatch(/^\d{4}-\d{2}-\d{2} \d{2}:\d{2}:\d{2}$/)
  })

  it('formatDateTime 接受时间戳', () => {
    const ts = Date.UTC(2026, 6, 27, 8, 30, 0)
    expect(formatDateTime(ts)).toMatch(/^\d{4}-\d{2}-\d{2} \d{2}:\d{2}:\d{2}$/)
  })

  it('formatDateTime 空值返回 "-"', () => {
    expect(formatDateTime(null)).toBe('-')
    expect(formatDateTime(undefined)).toBe('-')
    expect(formatDateTime('')).toBe('-')
  })

  it('formatDate 格式化为 yyyy-MM-dd', () => {
    expect(formatDate('2026-07-27T08:30:00Z')).toMatch(/^\d{4}-\d{2}-\d{2}$/)
  })

  it('formatMoney 默认人民币 2 位小数', () => {
    expect(formatMoney(1234.5)).toBe('¥1,234.50')
    expect(formatMoney(0)).toBe('¥0.00')
    expect(formatMoney(-99.9)).toBe('-¥99.90')
  })

  it('formatMoney 支持自定义货币符号', () => {
    expect(formatMoney(1234.5, { symbol: '$' })).toBe('$1,234.50')
  })

  it('formatMoney 空值返回 "-"', () => {
    expect(formatMoney(null)).toBe('-')
    expect(formatMoney(undefined)).toBe('-')
  })

  it('formatPercent 默认 2 位小数 + %', () => {
    expect(formatPercent(0.1234)).toBe('12.34%')
    expect(formatPercent(1)).toBe('100.00%')
  })

  it('formatPercent 支持自定义小数位', () => {
    expect(formatPercent(0.1234, { decimals: 0 })).toBe('12%')
    expect(formatPercent(0.1234, { decimals: 4 })).toBe('12.3400%')
  })

  it('formatNumber 千分位分隔', () => {
    expect(formatNumber(1234567)).toBe('1,234,567')
    expect(formatNumber(1234.5)).toBe('1,234.5')
  })
})
