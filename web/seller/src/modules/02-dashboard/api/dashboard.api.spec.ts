import { describe, expect, it, vi, beforeEach } from 'vitest'
import { dashboardApi } from './dashboard.api'
import { client } from '@/shared/http'

vi.mock('@/shared/http', () => ({
  client: { get: vi.fn() },
}))

describe('dashboardApi', () => {
  beforeEach(() => vi.clearAllMocks())

  it('getDashboard 调用 GET /seller/dashboard', async () => {
    vi.mocked(client.get).mockResolvedValue({ data: { shopId: 's1' } } as any)
    await dashboardApi.getDashboard()
    expect(client.get).toHaveBeenCalledWith('/seller/dashboard')
  })

  it('getSalesTrend 调用 GET /seller/sales-trend 带日期参数', async () => {
    vi.mocked(client.get).mockResolvedValue({ data: [] } as any)
    await dashboardApi.getSalesTrend({ from: '2026-07-01', to: '2026-07-07' })
    expect(client.get).toHaveBeenCalledWith('/seller/sales-trend', {
      params: { from: '2026-07-01', to: '2026-07-07' },
    })
  })
})
