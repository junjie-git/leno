import { describe, it, expect, beforeEach, vi } from 'vitest'
import { cacheApi } from './cache.api'
import { client } from '@/shared/http'

vi.mock('@/shared/http', () => ({
  client: { get: vi.fn(), post: vi.fn(), put: vi.fn(), delete: vi.fn() },
  withIdempotency: () => ({ headers: { 'Idempotency-Key': 'k' } }),
}))

describe('cache.api', () => {
  beforeEach(() => vi.clearAllMocks())

  it('info: 调 GET /admin/cache/info', async () => {
    const info = { redisVersion: '7.2.3' }
    vi.mocked(client.get).mockResolvedValueOnce({ data: info })
    const result = await cacheApi.info()
    expect(client.get).toHaveBeenCalledWith('/admin/cache/info')
    expect(result).toEqual(info)
  })

  it('keyspaces: 调 GET /admin/cache/keyspaces', async () => {
    const ks = [{ db: 0, keys: 1243, expires: 120, avgTtl: 3600000 }]
    vi.mocked(client.get).mockResolvedValueOnce({ data: ks })
    const result = await cacheApi.keyspaces()
    expect(client.get).toHaveBeenCalledWith('/admin/cache/keyspaces')
    expect(result).toEqual(ks)
  })

  it('listKeys: 调 GET /admin/cache/keys 带 query', async () => {
    const page = { items: [], total: 0, page: 1, pageSize: 20 }
    vi.mocked(client.get).mockResolvedValueOnce({ data: page })
    const params = { db: 0, pattern: 'user:*', page: 1, pageSize: 20 }
    const result = await cacheApi.listKeys(params)
    expect(client.get).toHaveBeenCalledWith('/admin/cache/keys', { params })
    expect(result).toEqual(page)
  })

  it('getKey: 调 GET /admin/cache/keys/{key}?db=0（key 需 URL 编码）', async () => {
    const detail = { key: 'user:0001', type: 'string' as const, value: 'v', ttl: 3600, size: 1, db: 0 }
    vi.mocked(client.get).mockResolvedValueOnce({ data: detail })
    const result = await cacheApi.getKey('user:0001', 0)
    expect(client.get).toHaveBeenCalledWith('/admin/cache/keys/user%3A0001', { params: { db: 0 } })
    expect(result).toEqual(detail)
  })

  it('deleteKey: 调 DELETE /admin/cache/keys/{key}?db=0 携带幂等键', async () => {
    vi.mocked(client.delete).mockResolvedValueOnce({ data: undefined })
    await cacheApi.deleteKey('user:0001', 0)
    expect(client.delete).toHaveBeenCalledWith('/admin/cache/keys/user%3A0001', { params: { db: 0 }, headers: { 'Idempotency-Key': 'k' } })
  })
})
