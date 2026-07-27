// web/system-admin/src/modules/04-runtime-ops/api/dead-letters.api.spec.ts
import { describe, it, expect, vi, beforeEach } from 'vitest'
import { client } from '@/shared/http'
import { deadLetterApi } from './dead-letters.api'
import type { ListDeadLettersParams } from '../types/dead-letter.dto'

// 统一 mock shared/http 模块，client.get/post 替换为 spy
vi.mock('@/shared/http', async () => {
  const actual = await vi.importActual<typeof import('@/shared/http')>('@/shared/http')
  return {
    ...actual,
    client: {
      get: vi.fn(),
      post: vi.fn(),
      put: vi.fn(),
      delete: vi.fn(),
    },
    withIdempotency: actual.withIdempotency,
  }
})

describe('deadLetterApi', () => {
  beforeEach(() => {
    vi.clearAllMocks()
  })

  it('list 使用正确 URL 与 params', async () => {
    ;(client.get as ReturnType<typeof vi.fn>).mockResolvedValue({ data: { items: [], total: 0, page: 1, pageSize: 20 } })
    const params: ListDeadLettersParams = { sourceContext: ['Order'], status: ['Pending'], page: 1, pageSize: 20 }
    await deadLetterApi.list(params)
    expect(client.get).toHaveBeenCalledWith('/admin/dead-letters', { params })
  })

  it('get 使用 /admin/dead-letters/{id} 路径', async () => {
    ;(client.get as ReturnType<typeof vi.fn>).mockResolvedValue({ data: {} })
    await deadLetterApi.get('DLQ-1')
    expect(client.get).toHaveBeenCalledWith('/admin/dead-letters/DLQ-1')
  })

  it('retry 注入 Idempotency-Key 头', async () => {
    ;(client.post as ReturnType<typeof vi.fn>).mockResolvedValue({ data: {} })
    await deadLetterApi.retry('DLQ-1')
    const [, , config] = (client.post as ReturnType<typeof vi.fn>).mock.calls[0]
    expect(config).toMatchObject({ headers: { 'Idempotency-Key': expect.any(String) } })
  })

  it('discard 携带 discardReason body + Idempotency-Key', async () => {
    ;(client.post as ReturnType<typeof vi.fn>).mockResolvedValue({ data: {} })
    await deadLetterApi.discard('DLQ-1', { discardReason: '消息体格式错误' })
    const [url, body, config] = (client.post as ReturnType<typeof vi.fn>).mock.calls[0]
    expect(url).toBe('/admin/dead-letters/DLQ-1/discard')
    expect(body).toEqual({ discardReason: '消息体格式错误' })
    expect(config).toMatchObject({ headers: { 'Idempotency-Key': expect.any(String) } })
  })

  it('batchRetry 提交 messageIds + Idempotency-Key', async () => {
    ;(client.post as ReturnType<typeof vi.fn>).mockResolvedValue({ data: { succeeded: [], failed: [] } })
    await deadLetterApi.batchRetry(['DLQ-1', 'DLQ-2'])
    const [url, body, config] = (client.post as ReturnType<typeof vi.fn>).mock.calls[0]
    expect(url).toBe('/admin/dead-letters/batch-retry')
    expect(body).toEqual({ messageIds: ['DLQ-1', 'DLQ-2'] })
    expect(config).toMatchObject({ headers: { 'Idempotency-Key': expect.any(String) } })
  })

  it('batchDiscard 提交 messageIds + discardReason + Idempotency-Key', async () => {
    ;(client.post as ReturnType<typeof vi.fn>).mockResolvedValue({ data: { succeeded: [], failed: [] } })
    await deadLetterApi.batchDiscard(['DLQ-1'], '批量清理过期消息')
    const [url, body, config] = (client.post as ReturnType<typeof vi.fn>).mock.calls[0]
    expect(url).toBe('/admin/dead-letters/batch-discard')
    expect(body).toEqual({ messageIds: ['DLQ-1'], discardReason: '批量清理过期消息' })
    expect(config).toMatchObject({ headers: { 'Idempotency-Key': expect.any(String) } })
  })
})
