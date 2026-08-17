/**
 * 应用错误类型层级
 *
 * 所有 HTTP 调用最终被转换为本层错误，调用方用 instanceof 精细化处理。
 * 与 spec §3.5 保持一致。
 */

/**
 * 应用错误基类
 *
 * 所有具体错误均继承此类，提供统一的 kind/message/traceId 字段。
 */
export abstract class AppError extends Error {
  /** 错误类别标识，用于序列化与日志 */
  abstract readonly kind: string
  /** OpenTelemetry traceId，便于前后端日志关联 */
  traceId?: string

  constructor(message: string, traceId?: string) {
    super(message)
    this.name = new.target.name
    this.traceId = traceId
    // 维持原型链（ES5 继承 Error 的标准修复）
    Object.setPrototypeOf(this, new.target.prototype)
  }
}

/**
 * 网络错误：超时、断网、DNS 失败等
 */
export class NetworkError extends AppError {
  readonly kind = 'NetworkError'
  constructor(message = '网络异常，请检查连接', traceId?: string) {
    super(message, traceId)
  }
}

/**
 * 业务错误：HTTP 200 但 code !== 0
 */
export class BusinessError extends AppError {
  readonly kind = 'BusinessError'
  readonly code: number
  constructor(code: number, message: string, traceId?: string) {
    super(message, traceId)
    this.code = code
  }
}

/**
 * 未登录或登录已过期（HTTP 401）
 */
export class UnauthorizedError extends AppError {
  readonly kind = 'UnauthorizedError'
  constructor(message = '未登录或登录已过期', traceId?: string) {
    super(message, traceId)
  }
}

/**
 * 无权访问（HTTP 403）
 */
export class ForbiddenError extends AppError {
  readonly kind = 'ForbiddenError'
  constructor(message = '无权访问', traceId?: string) {
    super(message, traceId)
  }
}

/**
 * 资源不存在（HTTP 404）
 */
export class NotFoundError extends AppError {
  readonly kind = 'NotFoundError'
  constructor(message = '资源不存在', traceId?: string) {
    super(message, traceId)
  }
}

/**
 * 限流（HTTP 429），携带重试等待秒数
 */
export class RateLimitedError extends AppError {
  readonly kind = 'RateLimitedError'
  readonly retryAfter: number
  constructor(message = '操作过于频繁', retryAfter = 0, traceId?: string) {
    super(message, traceId)
    this.retryAfter = retryAfter
  }
}

/**
 * 服务器错误（HTTP 5xx）
 */
export class ServerError extends AppError {
  readonly kind = 'ServerError'
  constructor(message = '服务器异常，请稍后重试', traceId?: string) {
    super(message, traceId)
  }
}

/**
 * 乐观锁冲突（HTTP 409），携带当前版本号
 */
export class ConcurrencyError extends AppError {
  readonly kind = 'ConcurrencyError'
  readonly currentVersion: number
  constructor(message = '资源已被他人修改', currentVersion = 0, traceId?: string) {
    super(message, traceId)
    this.currentVersion = currentVersion
  }
}
