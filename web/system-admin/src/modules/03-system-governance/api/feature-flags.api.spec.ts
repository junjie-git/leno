// web/system-admin/src/modules/03-system-governance/api/feature-flags.api.spec.ts

import { describe, it, expect, vi, beforeEach } from 'vitest'
import { client } from '@/shared/http'
import { featureFlagsApi } from './feature-flags.api'
import type { SaveFeatureFlagDto } from '../types/feature-flag.dto'

// 桩 shared/http：client 提供方法桩，withIdempotency 返回固定头便于断言
vi.mock('@/shared/http', () => ({
  client: {
    get: vi.fn(),
    post: vi.fn(),
    put: vi.fn(),
  },
  withIdempotency: vi.fn(() => ({ headers: { 'Idempotency-Key': 'mock-key' } })),
}))

describe('featureFlagsApi', () => {
  beforeEach(() => {
    vi.clearAllMocks()
  })

  it('list 使用 GET /admin/feature-flags 并透传筛选 params', async () => {
    vi.mocked(client.get).mockResolvedValue({
      data: { items: [], total: 0, page: 1, pageSize: 20 },
    })
    await featureFlagsApi.list({
      key: 'flag-1',
      status: ['Enabled'],
      group: 'payment',
      page: 1,
      pageSize: 20,
    })
    expect(client.get).toHaveBeenCalledWith('/admin/feature-flags', {
      params: { key: 'flag-1', status: ['Enabled'], group: 'payment', page: 1, pageSize: 20 },
    })
  })

  it('create 使用 POST /admin/feature-flags 并注入 Idempotency-Key', async () => {
    vi.mocked(client.post).mockResolvedValue({ data: {} })
    const body: SaveFeatureFlagDto = {
      key: 'flag-1',
      description: '测试开关',
      group: 'payment',
      ruleJson: '{}',
      status: 'Disabled',
    }
    await featureFlagsApi.create(body)
    expect(client.post).toHaveBeenCalledWith('/admin/feature-flags', body, {
      headers: { 'Idempotency-Key': 'mock-key' },
    })
  })

  it('update 使用 PUT /admin/feature-flags/{flagId} 并注入 Idempotency-Key', async () => {
    vi.mocked(client.put).mockResolvedValue({ data: {} })
    const body: SaveFeatureFlagDto = {
      key: 'flag-1',
      description: '已更新',
      group: 'payment',
      ruleJson: '{"op":"eq","field":"role","value":"Admin"}',
      status: 'Enabled',
    }
    await featureFlagsApi.update('flag-123', body)
    expect(client.put).toHaveBeenCalledWith('/admin/feature-flags/flag-123', body, {
      headers: { 'Idempotency-Key': 'mock-key' },
    })
  })

  it('enable 使用 POST /admin/feature-flags/{flagId}/enable 并注入 Idempotency-Key', async () => {
    vi.mocked(client.post).mockResolvedValue({ data: {} })
    await featureFlagsApi.enable('flag-123')
    expect(client.post).toHaveBeenCalledWith(
      '/admin/feature-flags/flag-123/enable',
      null,
      { headers: { 'Idempotency-Key': 'mock-key' } },
    )
  })

  it('disable 使用 POST /admin/feature-flags/{flagId}/disable 并注入 Idempotency-Key', async () => {
    vi.mocked(client.post).mockResolvedValue({ data: {} })
    await featureFlagsApi.disable('flag-123')
    expect(client.post).toHaveBeenCalledWith(
      '/admin/feature-flags/flag-123/disable',
      null,
      { headers: { 'Idempotency-Key': 'mock-key' } },
    )
  })

  it('evaluate 使用 POST /admin/feature-flags/evaluate 并传 body + Idempotency-Key', async () => {
    vi.mocked(client.post).mockResolvedValue({ data: { enabled: true, matchedRule: 'role=Admin' } })
    await featureFlagsApi.evaluate({ key: 'flag-1', context: { userId: 'u1', role: 'Admin' } })
    expect(client.post).toHaveBeenCalledWith(
      '/admin/feature-flags/evaluate',
      { key: 'flag-1', context: { userId: 'u1', role: 'Admin' } },
      { headers: { 'Idempotency-Key': 'mock-key' } },
    )
  })
})
