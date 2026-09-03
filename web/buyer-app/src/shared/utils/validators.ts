/**
 * 表单校验器（与后端校验规则对齐）
 */

/**
 * 用户名：3-32 位，字母/数字/下划线/中文
 */
export function isValidUsername(value: string): boolean {
  return /^[\u4e00-\u9fa5A-Za-z0-9_]{3,32}$/.test(value)
}

/**
 * 手机号：中国大陆 11 位
 */
export function isValidPhone(value: string): boolean {
  return /^1[3-9]\d{9}$/.test(value)
}

/**
 * 邮箱
 */
export function isValidEmail(value: string): boolean {
  return /^[\w.+-]+@[\w-]+(\.[\w-]+)+$/.test(value)
}

/**
 * 登录账号：用户名 / 手机号 / 邮箱任一
 */
export function isValidAccount(value: string): boolean {
  return isValidUsername(value) || isValidPhone(value) || isValidEmail(value)
}

/**
 * 密码：6-32 位，至少包含字母和数字
 */
export function isValidPassword(value: string): boolean {
  return /^(?=.*[A-Za-z])(?=.*\d).{6,32}$/.test(value)
}

/**
 * 短信/双因子验证码：6 位数字
 */
export function isValidVerifyCode(value: string): boolean {
  return /^\d{6}$/.test(value)
}

/**
 * 收货人姓名：2-20 位字符
 */
export function isValidReceiverName(value: string): boolean {
  return value.trim().length >= 2 && value.trim().length <= 20
}

/**
 * 收货地址：详细地址 5-100 位
 */
export function isValidAddressDetail(value: string): boolean {
  return value.trim().length >= 5 && value.trim().length <= 100
}

/**
 * 金额（元）输入合法性：最多两位小数
 */
export function isValidYuanAmount(value: string): boolean {
  return /^\d+(\.\d{1,2})?$/.test(value)
}
