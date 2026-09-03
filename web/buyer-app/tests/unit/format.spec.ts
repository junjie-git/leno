import { describe, expect, it } from 'vitest'
import {
  formatDate,
  formatDateTime,
  formatNumber,
  formatOrderNo,
  formatPoints,
  formatPrice,
  formatPriceExact,
  formatRelativeTime,
  formatSales,
  maskPhone,
} from '@/shared/utils/format'

describe('formatPrice（分 → 元，去尾零）', () => {
  it('常规金额保留有效小数', () => {
    expect(formatPrice(1990)).toBe('19.9')
    expect(formatPrice(1999)).toBe('19.99')
    expect(formatPrice(100)).toBe('1')
    expect(formatPrice(100000)).toBe('1000')
  })

  it('边界值', () => {
    expect(formatPrice(0)).toBe('0')
    expect(formatPrice(1)).toBe('0.01')
    expect(formatPrice(999999)).toBe('9999.99')
  })

  it('非有限数值返回 0', () => {
    expect(formatPrice(Number.NaN)).toBe('0')
    expect(formatPrice(Number.POSITIVE_INFINITY)).toBe('0')
  })
})

describe('formatPriceExact（固定两位小数）', () => {
  it('保留两位小数', () => {
    expect(formatPriceExact(1999)).toBe('19.99')
    expect(formatPriceExact(100000)).toBe('1000.00')
    expect(formatPriceExact(0)).toBe('0.00')
  })
})

describe('formatNumber / formatPoints', () => {
  it('千分位', () => {
    expect(formatNumber(12345)).toBe('12,345')
    expect(formatNumber(0)).toBe('0')
  })

  it('积分取整千分位', () => {
    expect(formatPoints(12345.6)).toBe('12,345')
  })
})

describe('formatSales（万级缩写）', () => {
  it('一万以下原样输出', () => {
    expect(formatSales(0)).toBe('0')
    expect(formatSales(8800)).toBe('8800')
  })

  it('万级保留一位小数', () => {
    expect(formatSales(12000)).toBe('1.2万')
    expect(formatSales(52000)).toBe('5.2万')
    expect(formatSales(345678)).toBe('35万')
  })

  it('十万级以上取整', () => {
    expect(formatSales(120000)).toBe('12万')
  })
})

describe('日期时间格式化', () => {
  const iso = '2026-09-01T08:05:00.000Z'

  it('formatDateTime 输出 YYYY-MM-DD HH:mm', () => {
    expect(formatDateTime(iso)).toMatch(/^\d{4}-\d{2}-\d{2} \d{2}:\d{2}$/)
  })

  it('formatDate 输出 YYYY-MM-DD', () => {
    expect(formatDate(iso)).toMatch(/^\d{4}-\d{2}-\d{2}$/)
  })

  it('非法时间原样返回', () => {
    expect(formatDate('not-a-date')).toBe('not-a-date')
    expect(formatDateTime('not-a-date')).toBe('not-a-date')
  })
})

describe('formatRelativeTime', () => {
  const now = new Date('2026-09-03T12:00:00.000Z')

  it('一分钟内 → 刚刚', () => {
    expect(formatRelativeTime('2026-09-03T11:59:30.000Z', now)).toBe('刚刚')
  })

  it('一小时内 → N 分钟前', () => {
    expect(formatRelativeTime('2026-09-03T11:30:00.000Z', now)).toBe('30 分钟前')
  })

  it('一天内 → N 小时前', () => {
    expect(formatRelativeTime('2026-09-03T06:00:00.000Z', now)).toBe('6 小时前')
  })

  it('七天内 → N 天前', () => {
    expect(formatRelativeTime('2026-09-01T12:00:00.000Z', now)).toBe('2 天前')
  })

  it('超过七天回退日期', () => {
    expect(formatRelativeTime('2026-08-20T12:00:00.000Z', now)).toMatch(/^\d{4}-\d{2}-\d{2}$/)
  })
})

describe('formatOrderNo（4 位一组空格分组）', () => {
  it('分组展示', () => {
    expect(formatOrderNo('202609031234567890')).toBe('2026 0903 1234 5678 90')
  })

  it('已含空格先去重再分组', () => {
    expect(formatOrderNo('2026 0903 1234')).toBe('2026 0903 1234')
  })
})

describe('maskPhone（手机号脱敏）', () => {
  it('11 位手机号中间四位打码', () => {
    expect(maskPhone('13812345678')).toBe('138****5678')
  })

  it('非 11 位原样返回', () => {
    expect(maskPhone('12345')).toBe('12345')
  })
})
