import dayjs from 'dayjs'

/**
 * 通用格式化工具
 *
 * 与 spec §5.4 表格密度、§3 后端响应字段配套使用。
 */

/** 空值占位符 */
const EMPTY_PLACEHOLDER = '-'

/**
 * 格式化日期时间（yyyy-MM-dd HH:mm:ss）
 *
 * 接受 ISO 字符串、时间戳、Date 对象；空值返回 "-"。
 */
export function formatDateTime(value: string | number | Date | null | undefined): string {
  if (value === null || value === undefined || value === '') return EMPTY_PLACEHOLDER
  const d = dayjs(value)
  if (!d.isValid()) return EMPTY_PLACEHOLDER
  return d.format('YYYY-MM-DD HH:mm:ss')
}

/**
 * 格式化日期（yyyy-MM-dd）
 */
export function formatDate(value: string | number | Date | null | undefined): string {
  if (value === null || value === undefined || value === '') return EMPTY_PLACEHOLDER
  const d = dayjs(value)
  if (!d.isValid()) return EMPTY_PLACEHOLDER
  return d.format('YYYY-MM-DD')
}

/**
 * 格式化金额（默认人民币，2 位小数 + 千分位）
 */
export function formatMoney(
  value: number | string | null | undefined,
  options: { symbol?: string; decimals?: number } = {},
): string {
  if (value === null || value === undefined || value === '') return EMPTY_PLACEHOLDER
  const num = typeof value === 'string' ? Number(value) : value
  if (!Number.isFinite(num)) return EMPTY_PLACEHOLDER
  const symbol = options.symbol ?? '¥'
  const decimals = options.decimals ?? 2
  return `${num < 0 ? '-' : ''}${symbol}${Math.abs(num).toLocaleString('zh-CN', {
    minimumFractionDigits: decimals,
    maximumFractionDigits: decimals,
  })}`
}

/**
 * 格式化百分比（0.12 → 12.00%）
 */
export function formatPercent(
  value: number | null | undefined,
  options: { decimals?: number } = {},
): string {
  if (value === null || value === undefined || !Number.isFinite(value)) return EMPTY_PLACEHOLDER
  const decimals = options.decimals ?? 2
  return `${(value * 100).toFixed(decimals)}%`
}

/**
 * 千分位数字格式化
 */
export function formatNumber(value: number | null | undefined): string {
  if (value === null || value === undefined || !Number.isFinite(value)) return EMPTY_PLACEHOLDER
  return value.toLocaleString('zh-CN')
}
