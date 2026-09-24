/**
 * @leno/shared 门面：实现已单源化到 @leno/shared/http（2026-09-24 提取）。
 * 保留本文件以兼容既有深路径导入（'@/shared/http/errors'）。
 */
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
} from '@leno/shared/http'
