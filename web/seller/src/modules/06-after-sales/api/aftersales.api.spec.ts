import { describe, expect, it, vi, beforeEach } from 'vitest'
import { aftersalesApi } from './aftersales.api'
import { client, withIdempotency } from '@/shared/http'

vi.mock('@/shared/http', () => ({
  client: { get: vi.fn(), post: vi.fn() },
  withIdempotency: vi.fn(() => ({ headers: { 'Idempotency-Key': 'mock-key' } })),
}))

describe('aftersalesApi', () => {
  beforeEach(() => {
    vi.clearAllMocks()
    vi.mocked(withIdempotency).mockReturnValue({ headers: { 'Idempotency-Key': 'mock-key' } })
  })

  // ===== list =====
  it('list 调用 GET /seller/after-sales', async () => {
    vi.mocked(client.get).mockResolvedValue({ items: [], total: 0, page: 1, pageSize: 20 } as any)
    await aftersalesApi.list({})
    expect(client.get).toHaveBeenCalledWith('/seller/after-sales', expect.anything())
  })

  it('list 默认 page=1（与 Order BC 的 0 起不同）', async () => {
    vi.mocked(client.get).mockResolvedValue({ items: [], total: 0, page: 1, pageSize: 20 } as any)
    await aftersalesApi.list({ pageSize: 20 })
    expect(client.get).toHaveBeenCalledWith(
      '/seller/after-sales',
      { params: expect.objectContaining({ page: 1, pageSize: 20 }) },
    )
  })

  it('list 默认 pageSize=20', async () => {
    vi.mocked(client.get).mockResolvedValue({ items: [], total: 0, page: 1, pageSize: 20 } as any)
    await aftersalesApi.list({ page: 2 })
    expect(client.get).toHaveBeenCalledWith(
      '/seller/after-sales',
      { params: expect.objectContaining({ page: 2, pageSize: 20 }) },
    )
  })

  it('list 透传筛选参数', async () => {
    vi.mocked(client.get).mockResolvedValue({ items: [], total: 0, page: 1, pageSize: 20 } as any)
    await aftersalesApi.list({
      status: 'Pending',
      afterSalesNo: 'AS20260726001',
      orderNo: 'NO20260726001',
      buyerName: '张三',
      type: 'RefundOnly',
      startDate: '2026-07-01T00:00:00.000Z',
      endDate: '2026-07-31T23:59:59.000Z',
      page: 2,
      pageSize: 50,
    })
    expect(client.get).toHaveBeenCalledWith(
      '/seller/after-sales',
      {
        params: expect.objectContaining({
          status: 'Pending',
          afterSalesNo: 'AS20260726001',
          orderNo: 'NO20260726001',
          buyerName: '张三',
          type: 'RefundOnly',
          startDate: '2026-07-01T00:00:00.000Z',
          endDate: '2026-07-31T23:59:59.000Z',
          page: 2,
          pageSize: 50,
        }),
      },
    )
  })

  // ===== get =====
  it('get 调用 GET /seller/after-sales/{id}', async () => {
    vi.mocked(client.get).mockResolvedValue({ id: 'a1' } as any)
    await aftersalesApi.get('a1')
    expect(client.get).toHaveBeenCalledWith('/seller/after-sales/a1')
  })

  // ===== approve =====
  it('approve 调用 POST /seller/after-sales/{id}/approve 带 Idempotency-Key 头 + body { version }', async () => {
    vi.mocked(client.post).mockResolvedValue({ id: 'a1', status: 'Approved' } as any)
    await aftersalesApi.approve('a1', 3)
    expect(withIdempotency).toHaveBeenCalledTimes(1)
    expect(client.post).toHaveBeenCalledWith(
      '/seller/after-sales/a1/approve',
      { version: 3 },
      { headers: { 'Idempotency-Key': 'mock-key' } },
    )
  })

  // ===== reject =====
  it('reject 调用 POST /seller/after-sales/{id}/reject 带 Idempotency-Key 头 + body { reason, version }', async () => {
    vi.mocked(client.post).mockResolvedValue({ id: 'a1', status: 'Rejected' } as any)
    await aftersalesApi.reject('a1', { reason: '商品无质量问题', version: 3 })
    expect(withIdempotency).toHaveBeenCalledTimes(1)
    expect(client.post).toHaveBeenCalledWith(
      '/seller/after-sales/a1/reject',
      { reason: '商品无质量问题', version: 3 },
      { headers: { 'Idempotency-Key': 'mock-key' } },
    )
  })

  // ===== confirmReturn =====
  it('confirmReturn 调用 POST /seller/after-sales/{id}/confirm-return 带 Idempotency-Key 头 + body { version }', async () => {
    vi.mocked(client.post).mockResolvedValue({ id: 'a1', status: 'Refunded' } as any)
    await aftersalesApi.confirmReturn('a1', 5)
    expect(withIdempotency).toHaveBeenCalledTimes(1)
    expect(client.post).toHaveBeenCalledWith(
      '/seller/after-sales/a1/confirm-return',
      { version: 5 },
      { headers: { 'Idempotency-Key': 'mock-key' } },
    )
  })
})
