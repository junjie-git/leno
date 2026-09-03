/**
 * 通用格式化工具
 */

/**
 * 金额格式化：分 → 元（去掉末尾多余的 0，至少保留 1 位小数时才保留）
 *
 * 后端金额统一为「分」（整数），前端展示统一为元。
 * 例：formatPrice(1990) → "19.9"；formatPrice(100000) → "1000"；formatPrice(1999) → "19.99"
 */
export function formatPrice(cents: number): string {
  if (!Number.isFinite(cents)) return '0'
  const yuan = cents / 100
  // 先去掉小数末尾多余的 0，再去掉孤立的小数点（"19.90"→"19.9"、"1000.00"→"1000"）
  return yuan.toFixed(2).replace(/0+$/, '').replace(/\.$/, '')
}

/**
 * 金额格式化（固定两位小数），用于金额明细、账单场景
 * 例：formatPriceExact(1999) → "19.99"；formatPriceExact(100000) → "1000.00"
 */
export function formatPriceExact(cents: number): string {
  if (!Number.isFinite(cents)) return '0.00'
  return (cents / 100).toFixed(2)
}

/**
 * 千分位数字格式化：12345 → "12,345"
 */
export function formatNumber(value: number): string {
  if (!Number.isFinite(value)) return '0'
  return value.toLocaleString('zh-CN')
}

/**
 * 销量友好展示：32100 → "3.2万"
 */
export function formatSales(value: number): string {
  if (!Number.isFinite(value)) return '0'
  if (value >= 10000) {
    const w = value / 10000
    return `${w >= 10 ? Math.round(w) : Math.round(w * 10) / 10}万`
  }
  return String(value)
}

/**
 * 积分友好展示：12345 → "12,345"（保持千分位，不加「万」）
 */
export function formatPoints(value: number): string {
  return formatNumber(Math.trunc(value))
}

/**
 * 时间格式化（YYYY-MM-DD HH:mm）
 */
export function formatDateTime(iso: string): string {
  const d = new Date(iso)
  if (Number.isNaN(d.getTime())) return iso
  const pad = (n: number) => String(n).padStart(2, '0')
  return `${d.getFullYear()}-${pad(d.getMonth() + 1)}-${pad(d.getDate())} ${pad(d.getHours())}:${pad(d.getMinutes())}`
}

/**
 * 时间格式化（YYYY-MM-DD）
 */
export function formatDate(iso: string): string {
  const d = new Date(iso)
  if (Number.isNaN(d.getTime())) return iso
  const pad = (n: number) => String(n).padStart(2, '0')
  return `${d.getFullYear()}-${pad(d.getMonth() + 1)}-${pad(d.getDate())}`
}

/**
 * 相对时间描述：刚刚 / N 分钟前 / N 小时前 / N 天前 / 超过 7 天回退日期
 */
export function formatRelativeTime(iso: string, now: Date = new Date()): string {
  const t = new Date(iso).getTime()
  if (Number.isNaN(t)) return iso
  const diff = now.getTime() - t
  if (diff < 60_000) return '刚刚'
  if (diff < 3_600_000) return `${Math.floor(diff / 60_000)} 分钟前`
  if (diff < 86_400_000) return `${Math.floor(diff / 3_600_000)} 小时前`
  if (diff < 7 * 86_400_000) return `${Math.floor(diff / 86_400_000)} 天前`
  return formatDate(iso)
}

/**
 * 订单号脱敏分组展示：202609031234567890 → 2026 0903 1234 5678 90（4 位一组空格分隔）
 */
export function formatOrderNo(orderNo: string): string {
  return orderNo.replace(/\s/g, '').replace(/(.{4})/g, '$1 ').trim()
}

/**
 * 手机号脱敏：13812345678 → 138****5678
 */
export function maskPhone(phone: string): string {
  if (phone.length !== 11) return phone
  return `${phone.slice(0, 3)}****${phone.slice(7)}`
}
