import { describe, it, expect, beforeEach, vi } from 'vitest'
import { menuApi } from './menu.api'
import { client } from '@/shared/http'

vi.mock('@/shared/http', () => ({
  client: {
    get: vi.fn(),
    post: vi.fn(),
    put: vi.fn(),
    delete: vi.fn(),
  },
  withIdempotency: () => ({ headers: { 'Idempotency-Key': 'test-key' } }),
}))

describe('menu.api', () => {
  beforeEach(() => {
    vi.clearAllMocks()
  })

  it('getTree: 调 GET /admin/menus/tree 并返回 MenuDto[]', async () => {
    const mockTree = [{ id: 'm-01', name: '仪表盘', children: [] }]
    vi.mocked(client.get).mockResolvedValueOnce({ data: mockTree })
    const result = await menuApi.getTree()
    expect(client.get).toHaveBeenCalledWith('/admin/menus/tree')
    expect(result).toEqual(mockTree)
  })

  it('create: 调 POST /admin/menus 并携带 Idempotency-Key', async () => {
    const body = { parentId: null, name: '新菜单', type: 'Menu' as const, path: '/x', component: null, icon: null, sort: 1, permission: null, roles: ['Admin'], visible: true, cache: false }
    const created = { ...body, id: 'm-new' }
    vi.mocked(client.post).mockResolvedValueOnce({ data: created })
    const result = await menuApi.create(body)
    expect(client.post).toHaveBeenCalledWith('/admin/menus', body, { headers: { 'Idempotency-Key': 'test-key' } })
    expect(result).toEqual(created)
  })

  it('update: 调 PUT /admin/menus/{id}', async () => {
    const updated = { name: '改名' }
    vi.mocked(client.put).mockResolvedValueOnce({ data: { id: 'm-01', name: '改名' } })
    await menuApi.update('m-01', updated)
    expect(client.put).toHaveBeenCalledWith('/admin/menus/m-01', updated, { headers: { 'Idempotency-Key': 'test-key' } })
  })

  it('remove: 调 DELETE /admin/menus/{id}', async () => {
    vi.mocked(client.delete).mockResolvedValueOnce({ data: undefined })
    await menuApi.remove('m-01')
    expect(client.delete).toHaveBeenCalledWith('/admin/menus/m-01', { headers: { 'Idempotency-Key': 'test-key' } })
  })

  it('sort: 调 PUT /admin/menus/sort', async () => {
    const updates = [{ id: 'm-01', parentId: null, sort: 2 }]
    vi.mocked(client.put).mockResolvedValueOnce({ data: undefined })
    await menuApi.sort(updates)
    expect(client.put).toHaveBeenCalledWith('/admin/menus/sort', updates, { headers: { 'Idempotency-Key': 'test-key' } })
  })
})
