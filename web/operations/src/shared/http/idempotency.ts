/**
 * @leno/shared 门面：实现已单源化到 @leno/shared/http（2026-09-24 提取）。
 * 保留本文件以兼容既有深路径导入（'@/shared/http/idempotency'）。
 */
export { generateIdempotencyKey, withIdempotency } from '@leno/shared/http'
