// web/system-admin/src/modules/02-user-access/api/users.api.spec.ts

import { describe, it, expect, vi, beforeEach } from 'vitest'
import { client } from '@/shared/http'
import { usersApi } from './users.api'
import type { ListUsersParams } from '../types/user.dto'
import type { PageQuery } from '@/shared/types'

// 桩 shared/http：client 提供方法桩，withIdempotency 返回固定头
vi.mock('@/shared/http', () => ({
  client: {
    get: vi.fn(),
    put: vi.fn(),
    post: vi.fn(),
    delete: vi.fn(),
  },
  withIdempotency: vi.fn(() => ({ headers: { 'Idempotency-Key': 'mock-key' } })),
}))

describe('usersApi', () => {
  beforeEach(() => {
    vi.clearAllMocks()
  })

  it('list 使用 GET /admin/users 并透传筛选 params', async () => {
    vi.mocked(client.get).mockResolvedValue({
      data: { items: [], total: 0, page: 1, pageSize: 20 },
    })
    const params: ListUsersParams & PageQuery = {
      keyword: 'jack',
      roles: ['r-1'],
      statuses: ['Active'],
      fromTime: '2026-01-01T00:00:00Z',
      toTime: '2026-07-27T00:00:00Z',
      page: 1,
      pageSize: 20,
    }
    await usersApi.list(params)
    expect(client.get).toHaveBeenCalledWith('/admin/users', { params })
  })

  it('get 使用 GET /admin/users/{id}', async () => {
    vi.mocked(client.get).mockResolvedValue({ data: {} })
    await usersApi.get('u-1')
    expect(client.get).toHaveBeenCalledWith('/admin/users/u-1')
  })

  it('assignRoles 使用 PUT /admin/users/{id}/roles 并注入 Idempotency-Key', async () => {
    vi.mocked(client.put).mockResolvedValue({ data: {} })
    await usersApi.assignRoles('u-1', { roleIds: ['r-1', 'r-2'] })
    expect(client.put).toHaveBeenCalledWith(
      '/admin/users/u-1/roles',
      { roleIds: ['r-1', 'r-2'] },
      expect.objectContaining({
        headers: expect.objectContaining({ 'Idempotency-Key': expect.any(String) }),
      }),
    )
  })

  it('updateStatus 使用 PUT /admin/users/{id}/status 并注入 Idempotency-Key', async () => {
    vi.mocked(client.put).mockResolvedValue({ data: {} })
    await usersApi.updateStatus('u-1', { status: 'Suspended', reason: '违规操作' })
    expect(client.put).toHaveBeenCalledWith(
      '/admin/users/u-1/status',
      { status: 'Suspended', reason: '违规操作' },
      expect.objectContaining({
        headers: expect.objectContaining({ 'Idempotency-Key': expect.any(String) }),
      }),
    )
  })
})
