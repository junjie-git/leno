/* eslint-disable @typescript-eslint/no-explicit-any */
import { describe, expect, it, vi, beforeEach } from 'vitest'
import { notificationApi } from './notification.api'
import { http } from '@/shared/http'

vi.mock('@/shared/http', () => ({
  http: { get: vi.fn(), post: vi.fn() },
  withIdempotency: vi.fn(() => ({ headers: { 'Idempotency-Key': 'mock-key' } })),
}))

describe('notificationApi', () => {
  beforeEach(() => vi.clearAllMocks())

  it('list 调用 GET /notifications 并透传参数', async () => {
    vi.mocked(http.get).mockResolvedValue({
      data: {
        items: [],
        total: 0,
        unreadCount: 0,
        page: 1,
        pageSize: 20,
      },
    } as any)
    await notificationApi.list({ isRead: false, page: 1, pageSize: 20 })
    expect(http.get).toHaveBeenCalledWith('/notifications', {
      params: { isRead: false, page: 1, pageSize: 20 },
    })
  })

  it('list 默认 page=1 pageSize=20', async () => {
    vi.mocked(http.get).mockResolvedValue({
      data: {
        items: [],
        total: 0,
        unreadCount: 0,
        page: 1,
        pageSize: 20,
      },
    } as any)
    await notificationApi.list({})
    expect(http.get).toHaveBeenCalledWith('/notifications', {
      params: expect.objectContaining({ page: 1, pageSize: 20 }),
    })
  })

  it('getUnreadCount 调用 GET /notifications/unread-count', async () => {
    vi.mocked(http.get).mockResolvedValue({ data: 5 } as any)
    const count = await notificationApi.getUnreadCount()
    expect(http.get).toHaveBeenCalledWith('/notifications/unread-count')
    expect(count).toBe(5)
  })

  it('markAsRead 调用 POST /notifications/read 带 recordIds', async () => {
    vi.mocked(http.post).mockResolvedValue({ data: null } as any)
    await notificationApi.markAsRead(['r1', 'r2'])
    expect(http.post).toHaveBeenCalledWith('/notifications/read', { recordIds: ['r1', 'r2'] })
  })

  it('markAllAsRead 调用 POST /notifications/read-all', async () => {
    vi.mocked(http.post).mockResolvedValue({ data: null } as any)
    await notificationApi.markAllAsRead()
    expect(http.post).toHaveBeenCalledWith('/notifications/read-all')
  })
})
