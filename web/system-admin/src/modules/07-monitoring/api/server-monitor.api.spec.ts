import { describe, it, expect, beforeEach, vi } from 'vitest'
import { serverMonitorApi } from './server-monitor.api'
import { client } from '@/shared/http'

vi.mock('@/shared/http', () => ({
  client: { get: vi.fn(), post: vi.fn(), put: vi.fn(), delete: vi.fn() },
  withIdempotency: () => ({ headers: { 'Idempotency-Key': 'k' } }),
}))

describe('server-monitor.api', () => {
  beforeEach(() => vi.clearAllMocks())

  it('snapshot: 调 GET /admin/server-monitor/snapshot', async () => {
    const snap = { hostname: 'host-1', cpuUsagePercent: 32.5 }
    vi.mocked(client.get).mockResolvedValueOnce({ data: snap })
    const result = await serverMonitorApi.snapshot()
    expect(client.get).toHaveBeenCalledWith('/admin/server-monitor/snapshot')
    expect(result).toEqual(snap)
  })

  it('history: 调 GET /admin/server-monitor/history?metric=cpu&range=5m', async () => {
    const hist = { metric: 'cpu', points: [{ t: '2026-07-27T00:00:00Z', v: 30 }] }
    vi.mocked(client.get).mockResolvedValueOnce({ data: hist })
    const result = await serverMonitorApi.history('cpu', '5m')
    expect(client.get).toHaveBeenCalledWith('/admin/server-monitor/history', { params: { metric: 'cpu', range: '5m' } })
    expect(result).toEqual(hist)
  })
})
