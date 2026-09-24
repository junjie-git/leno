/**
 * @leno/shared http 出口
 *
 * 调用方统一从 `@leno/shared/http` 引入 client、withIdempotency 与错误类型。
 */
export { client } from './client'
export {
  withIdempotency,
  withIdempotency as withIdempotencyKey,
  generateIdempotencyKey,
} from './idempotency'
export {
  AppError,
  BusinessError,
  ConcurrencyError,
  ForbiddenError,
  NetworkError,
  NotFoundError,
  RateLimitedError,
  ServerError,
  UnauthorizedError,
} from './errors'
