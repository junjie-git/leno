// web/system-admin/src/modules/03-system-governance/api/announcements.api.spec.ts

import { describe, it, expect, vi, beforeEach } from 'vitest'
import { client } from '@/shared/http'
import { announcementsApi } from './announcements.api'
import type { SaveAnnouncementDto } from '../types/announcement.dto'

vi.mock('@/shared/http', () => ({
  client: {
    get: vi.fn(),
    post: vi.fn(),
    put: vi.fn(),
  },
  withIdempotency: vi.fn(() => ({ headers: { 'Idempotency-Key': 'mock-key' } })),
}))

describe('announcementsApi', () => {
  beforeEach(() => {
    vi.clearAllMocks()
  })

  it('list 使用 GET /admin/announcements 并透传筛选 params', async () => {
    vi.mocked(client.get).mockResolvedValue({
      data: { items: [], total: 0, page: 1, pageSize: 20 },
    })
    await announcementsApi.list({
      type: ['Urgent'],
      status: ['Published'],
      page: 1,
      pageSize: 20,
    })
    expect(client.get).toHaveBeenCalledWith('/admin/announcements', {
      params: { type: ['Urgent'], status: ['Published'], page: 1, pageSize: 20 },
    })
  })

  it('create 使用 POST /admin/announcements 并注入 Idempotency-Key', async () => {
    vi.mocked(client.post).mockResolvedValue({ data: {} })
    const body: SaveAnnouncementDto = {
      title: '系统维护通知',
      type: 'SystemMaintenance',
      audiences: ['Buyer', 'Seller'],
      effectiveFrom: '2026-07-27T00:00:00Z',
      effectiveTo: '2026-07-28T00:00:00Z',
      content: '系统将于 07-27 凌晨维护',
      isPinned: false,
    }
    await announcementsApi.create(body)
    expect(client.post).toHaveBeenCalledWith('/admin/announcements', body, {
      headers: { 'Idempotency-Key': 'mock-key' },
    })
  })

  it('update 使用 PUT /admin/announcements/{id} 并注入 Idempotency-Key', async () => {
    vi.mocked(client.put).mockResolvedValue({ data: {} })
    const body: SaveAnnouncementDto = {
      title: '已更新标题',
      type: 'Urgent',
      audiences: ['Operator'],
      effectiveFrom: '2026-07-27T00:00:00Z',
      effectiveTo: '2026-07-28T00:00:00Z',
      content: '已更新正文',
      isPinned: true,
    }
    await announcementsApi.update('ann-123', body)
    expect(client.put).toHaveBeenCalledWith('/admin/announcements/ann-123', body, {
      headers: { 'Idempotency-Key': 'mock-key' },
    })
  })

  it('publish 使用 POST /admin/announcements/{id}/publish 并注入 Idempotency-Key', async () => {
    vi.mocked(client.post).mockResolvedValue({ data: {} })
    await announcementsApi.publish('ann-123')
    expect(client.post).toHaveBeenCalledWith(
      '/admin/announcements/ann-123/publish',
      null,
      { headers: { 'Idempotency-Key': 'mock-key' } },
    )
  })

  it('unpublish 使用 POST /admin/announcements/{id}/unpublish 并注入 Idempotency-Key', async () => {
    vi.mocked(client.post).mockResolvedValue({ data: {} })
    await announcementsApi.unpublish('ann-123')
    expect(client.post).toHaveBeenCalledWith(
      '/admin/announcements/ann-123/unpublish',
      null,
      { headers: { 'Idempotency-Key': 'mock-key' } },
    )
  })
})
