/**
 * shared/http 出口
 *
 * 调用方统一从 `@/shared/http` 引入 client、withIdempotency 与错误类型。
 */
export { client, withIdempotency } from './client'
export { client as http } from './client'
export { withIdempotency as withIdempotencyKey, generateIdempotencyKey } from './idempotency'
export {
  AppError,
  NetworkError,
  BusinessError,
  UnauthorizedError,
  ForbiddenError,
  NotFoundError,
  RateLimitedError,
  ServerError,
  ConcurrencyError,
} from './errors'

export { setupMockAdapter } from './mock'
