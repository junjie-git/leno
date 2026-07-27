import { describe, it, expect, beforeEach, vi } from 'vitest'
import { loginLogsApi } from './login-logs.api'
import { client } from '@/shared/http'

vi.mock('@/shared/http', () => ({
  client: { get: vi.fn(), post: vi.fn(), put: vi.fn(), delete: vi.fn() },
  withIdempotency: () => ({ headers: { 'Idempotency-Key': 'k' } }),
}))

describe('login-logs.api', () => {
  beforeEach(() => vi.clearAllMocks())

  it('list: 调 GET /admin/login-logs 带筛选参数', async () => {
    const page = { items: [], total: 0, page: 1, pageSize: 20 }
    vi.mocked(client.get).mockResolvedValueOnce({ data: page })
    const params = { result: 'Failed' as const, page: 1, pageSize: 20 }
    const result = await loginLogsApi.list(params)
    expect(client.get).toHaveBeenCalledWith('/admin/login-logs', { params })
    expect(result).toEqual(page)
  })

  it('get: 调 GET /admin/login-logs/{id}', async () => {
    const log = { id: 'll-1', username: 'admin' }
    vi.mocked(client.get).mockResolvedValueOnce({ data: log })
    const result = await loginLogsApi.get('ll-1')
    expect(client.get).toHaveBeenCalledWith('/admin/login-logs/ll-1')
    expect(result).toEqual(log)
  })

  it('exportCsv: 调 GET /admin/login-logs/export with responseType=text', async () => {
    const csv = 'id,username\nll-1,admin'
    vi.mocked(client.get).mockResolvedValueOnce({ data: csv })
    const params = { page: 1, pageSize: 100 }
    const result = await loginLogsApi.exportCsv(params)
    expect(client.get).toHaveBeenCalledWith('/admin/login-logs/export', { params, responseType: 'text' })
    expect(result).toBe(csv)
  })
})
