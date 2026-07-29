import { describe, it, expect, beforeEach, vi } from 'vitest'
import { onlineUsersApi } from './online-users.api'
import { client } from '@/shared/http'

vi.mock('@/shared/http', () => ({
  client: { get: vi.fn(), post: vi.fn(), put: vi.fn(), delete: vi.fn() },
  withIdempotency: () => ({ headers: { 'Idempotency-Key': 'k' } }),
}))

describe('online-users.api', () => {
  beforeEach(() => vi.clearAllMocks())

  it('list: 调 GET /admin/online-users 带筛选参数', async () => {
    const page = { items: [], total: 0, page: 1, pageSize: 20 }
    vi.mocked(client.get).mockResolvedValueOnce({ data: page })
    const params = { username: 'admin', page: 1, pageSize: 20 }
    const result = await onlineUsersApi.list(params)
    expect(client.get).toHaveBeenCalledWith('/admin/online-users', { params })
    expect(result).toEqual(page)
  })

  it('get: 调 GET /admin/online-users/{id}', async () => {
    const user = { sessionId: 'ou-1', username: 'admin' }
    vi.mocked(client.get).mockResolvedValueOnce({ data: user })
    const result = await onlineUsersApi.get('ou-1')
    expect(client.get).toHaveBeenCalledWith('/admin/online-users/ou-1')
    expect(result).toEqual(user)
  })

  it('kick: 调 DELETE /admin/online-users/{id} 携带幂等键', async () => {
    vi.mocked(client.delete).mockResolvedValueOnce({ data: undefined })
    await onlineUsersApi.kick('ou-1')
    expect(client.delete).toHaveBeenCalledWith('/admin/online-users/ou-1', { headers: { 'Idempotency-Key': 'k' } })
  })

  it('stats: 调 GET /admin/online-users/stats', async () => {
    const stats = { total: 12, logins24h: 45, anomalies: 2 }
    vi.mocked(client.get).mockResolvedValueOnce({ data: stats })
    const result = await onlineUsersApi.stats()
    expect(client.get).toHaveBeenCalledWith('/admin/online-users/stats')
    expect(result).toEqual(stats)
  })
})
