/**
 * 幂等键工具
 *
 * 调用方在写操作（POST/PUT/DELETE）时通过 `withIdempotency()` 包装 config，
 * 拦截器会自动注入 `Idempotency-Key` 头，后端据此去重。
 */

/**
 * 生成 UUID v4 字符串
 *
 * 优先使用原生 crypto.randomUUID（Node ≥ 19、现代浏览器均支持）；
 * 降级使用 Math.random 拼装，保证 jsdom 等老环境可用。
 */
export function generateIdempotencyKey(): string {
  if (typeof crypto !== 'undefined' && typeof crypto.randomUUID === 'function') {
    return crypto.randomUUID()
  }
  // 降级方案：按 RFC4122 v4 拼装
  const bytes = new Uint8Array(16)
  if (typeof crypto !== 'undefined' && typeof crypto.getRandomValues === 'function') {
    crypto.getRandomValues(bytes)
  } else {
    for (let i = 0; i < 16; i++) bytes[i] = Math.floor(Math.random() * 256)
  }
  bytes[6] = (bytes[6] & 0x0f) | 0x40
  bytes[8] = (bytes[8] & 0x3f) | 0x80
  const hex = Array.from(bytes, (b) => b.toString(16).padStart(2, '0'))
  return `${hex.slice(0, 4).join('')}-${hex.slice(4, 6).join('')}-${hex.slice(6, 8).join('')}-${hex.slice(8, 10).join('')}-${hex.slice(10, 16).join('')}`
}

/**
 * 构造携带 Idempotency-Key 头的 axios config 片段
 *
 * 用法：
 * ```ts
 * client.post('/admin/dead-letters/1/retry', null, withIdempotency())
 * ```
 */
export function withIdempotency(): { headers: { 'Idempotency-Key': string } } {
  return {
    headers: {
      'Idempotency-Key': generateIdempotencyKey(),
    },
  }
}
