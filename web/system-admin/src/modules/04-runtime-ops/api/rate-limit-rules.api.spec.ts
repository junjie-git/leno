// web/system-admin/src/modules/04-runtime-ops/api/rate-limit-rules.api.spec.ts
import { describe, it, expect, vi, beforeEach } from 'vitest'
import { client } from '@/shared/http'
import { rateLimitRuleApi } from './rate-limit-rules.api'
import type { SaveRateLimitRuleDto } from '../types/rate-limit-rule.dto'

vi.mock('@/shared/http', async () => {
  const actual = await vi.importActual<typeof import('@/shared/http')>('@/shared/http')
  return {
    ...actual,
    client: { get: vi.fn(), post: vi.fn(), put: vi.fn(), delete: vi.fn() },
    withIdempotency: actual.withIdempotency,
  }
})

describe('rateLimitRuleApi', () => {
  beforeEach(() => vi.clearAllMocks())

  it('list 使用 /admin/rate-limit-rules + params', async () => {
    ;(client.get as ReturnType<typeof vi.fn>).mockResolvedValue({ data: { items: [], total: 0, page: 1, pageSize: 20 } })
    await rateLimitRuleApi.list({ targetApi: '/api/orders', enabled: true, page: 1, pageSize: 20 })
    expect(client.get).toHaveBeenCalledWith('/admin/rate-limit-rules', {
      params: { targetApi: '/api/orders', enabled: true, page: 1, pageSize: 20 },
    })
  })

  it('get 使用 /admin/rate-limit-rules/{id}', async () => {
    ;(client.get as ReturnType<typeof vi.fn>).mockResolvedValue({ data: {} })
    await rateLimitRuleApi.get('rule-1')
    expect(client.get).toHaveBeenCalledWith('/admin/rate-limit-rules/rule-1')
  })

  it('create 注入 Idempotency-Key', async () => {
    ;(client.post as ReturnType<typeof vi.fn>).mockResolvedValue({ data: {} })
    const body: SaveRateLimitRuleDto = {
      targetApi: '/api/orders', targetContext: 'Order', limit: 100, windowSeconds: 60,
      algorithm: 'SlidingWindow', scope: 'User',
    }
    await rateLimitRuleApi.create(body)
    const [url, payload, config] = (client.post as ReturnType<typeof vi.fn>).mock.calls[0]
    expect(url).toBe('/admin/rate-limit-rules')
    expect(payload).toEqual(body)
    expect(config).toMatchObject({ headers: { 'Idempotency-Key': expect.any(String) } })
  })

  it('update 携带 X-Resource-Version 乐观锁头 + Idempotency-Key', async () => {
    ;(client.put as ReturnType<typeof vi.fn>).mockResolvedValue({ data: {} })
    const body: SaveRateLimitRuleDto = {
      targetApi: '/api/orders', targetContext: 'Order', limit: 200, windowSeconds: 60,
      algorithm: 'SlidingWindow', scope: 'User', version: 3,
    }
    await rateLimitRuleApi.update('rule-1', body)
    const [url, payload, config] = (client.put as ReturnType<typeof vi.fn>).mock.calls[0]
    expect(url).toBe('/admin/rate-limit-rules/rule-1')
    expect(payload).toEqual(body)
    expect(config).toMatchObject({
      headers: { 'X-Resource-Version': 3, 'Idempotency-Key': expect.any(String) },
    })
  })

  it('enable 注入 Idempotency-Key', async () => {
    ;(client.post as ReturnType<typeof vi.fn>).mockResolvedValue({ data: {} })
    await rateLimitRuleApi.enable('rule-1')
    const [url, payload, config] = (client.post as ReturnType<typeof vi.fn>).mock.calls[0]
    expect(url).toBe('/admin/rate-limit-rules/rule-1/enable')
    expect(payload).toBeNull()
    expect(config).toMatchObject({ headers: { 'Idempotency-Key': expect.any(String) } })
  })

  it('disable 注入 Idempotency-Key', async () => {
    ;(client.post as ReturnType<typeof vi.fn>).mockResolvedValue({ data: {} })
    await rateLimitRuleApi.disable('rule-1')
    const [url, , config] = (client.post as ReturnType<typeof vi.fn>).mock.calls[0]
    expect(url).toBe('/admin/rate-limit-rules/rule-1/disable')
    expect(config).toMatchObject({ headers: { 'Idempotency-Key': expect.any(String) } })
  })
})
