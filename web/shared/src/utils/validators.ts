/**
 * 通用校验器
 *
 * 用于表单与 API 入参前校验。所有函数返回 boolean，不抛异常。
 */

/**
 * 判断是否为非空字符串（trim 后非空）
 */
export function isNonEmptyString(value: unknown): value is string {
  return typeof value === 'string' && value.trim().length > 0
}

const EMAIL_RE = /^[^\s@]+@[^\s@]+\.[^\s@]+$/
/**
 * 判断是否为合法 email
 */
export function isValidEmail(value: unknown): value is string {
  return typeof value === 'string' && EMAIL_RE.test(value)
}

const USERNAME_RE = /^[A-Za-z0-9_]{4,32}$/
/**
 * 判断是否为合法用户名（4-32 位字母数字下划线）
 */
export function isValidUsername(value: unknown): value is string {
  return typeof value === 'string' && USERNAME_RE.test(value)
}

/**
 * 判断是否为合法密码（至少 8 位，含字母与数字）
 */
export function isValidPassword(value: unknown): value is string {
  if (typeof value !== 'string' || value.length < 8) return false
  const hasLetter = /[A-Za-z]/.test(value)
  const hasDigit = /\d/.test(value)
  return hasLetter && hasDigit
}

/**
 * 判断是否为正整数
 */
export function isPositiveInteger(value: unknown): value is number {
  return typeof value === 'number' && Number.isInteger(value) && value > 0
}

/**
 * 判断数字是否在 [min, max] 闭区间
 */
export function isInRange(value: number, min: number, max: number): boolean {
  return value >= min && value <= max
}

const UUID_RE = /^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/i
/**
 * 判断是否为 UUID
 */
export function isUuid(value: unknown): value is string {
  return typeof value === 'string' && UUID_RE.test(value)
}
