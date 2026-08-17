import { describe, it, expect, beforeEach } from 'vitest'
import { withIdempotency, generateIdempotencyKey } from './idempotency'

describe('shared/http/idempotency', () => {
  beforeEach(() => {
    sessionStorage.clear()
  })

  it('generateIdempotencyKey 返回 UUID v4 格式', () => {
    const key = generateIdempotencyKey()
    expect(key).toMatch(/^[0-9a-f]{8}-[0-9a-f]{4}-4[0-9a-f]{3}-[89ab][0-9a-f]{3}-[0-9a-f]{12}$/i)
  })

  it('withIdempotency 返回包含 Idempotency-Key 的 headers 对象', () => {
    const result = withIdempotency()
    expect(result.headers).toHaveProperty('Idempotency-Key')
    expect(typeof result.headers['Idempotency-Key']).toBe('string')
    expect(result.headers['Idempotency-Key']).toHaveLength(36)
  })

  it('每次调用 withIdempotency 生成不同的 key', () => {
    const a = withIdempotency()
    const b = withIdempotency()
    expect(a.headers['Idempotency-Key']).not.toBe(b.headers['Idempotency-Key'])
  })
})
