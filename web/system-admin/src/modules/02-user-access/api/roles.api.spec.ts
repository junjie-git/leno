// web/system-admin/src/modules/02-user-access/api/roles.api.spec.ts

import { describe, it, expect, vi, beforeEach } from 'vitest'
import { client } from '@/shared/http'
import { rolesApi } from './roles.api'
import type { ListRolesParams, SaveRoleDto, UpdateRolePermissionsDto } from '../types/role.dto'
import type { PageQuery } from '@/shared/types'

vi.mock('@/shared/http', () => ({
  client: { get: vi.fn(), put: vi.fn(), post: vi.fn(), delete: vi.fn() },
  withIdempotency: vi.fn(() => ({ headers: { 'Idempotency-Key': 'mock-key' } })),
}))

describe('rolesApi', () => {
  beforeEach(() => vi.clearAllMocks())

  it('list 使用 GET /admin/roles 并透传 keyword', async () => {
    vi.mocked(client.get).mockResolvedValue({
      data: { items: [], total: 0, page: 1, pageSize: 20 },
    })
    const params: ListRolesParams & PageQuery = { keyword: '运营', page: 1, pageSize: 20 }
    await rolesApi.list(params)
    expect(client.get).toHaveBeenCalledWith('/admin/roles', { params })
  })

  it('get 使用 GET /admin/roles/{id}', async () => {
    vi.mocked(client.get).mockResolvedValue({ data: {} })
    await rolesApi.get('r-1')
    expect(client.get).toHaveBeenCalledWith('/admin/roles/r-1')
  })

  it('create 使用 POST /admin/roles 并注入 Idempotency-Key', async () => {
    vi.mocked(client.post).mockResolvedValue({ data: {} })
    const body: SaveRoleDto = { name: '运营经理', description: '负责日常运营' }
    await rolesApi.create(body)
    expect(client.post).toHaveBeenCalledWith(
      '/admin/roles',
      body,
      expect.objectContaining({
        headers: expect.objectContaining({ 'Idempotency-Key': expect.any(String) }),
      }),
    )
  })

  it('update 使用 PUT /admin/roles/{id} 并注入 Idempotency-Key', async () => {
    vi.mocked(client.put).mockResolvedValue({ data: {} })
    const body: SaveRoleDto = { name: '运营经理', description: '修改描述' }
    await rolesApi.update('r-1', body)
    expect(client.put).toHaveBeenCalledWith(
      '/admin/roles/r-1',
      body,
      expect.objectContaining({
        headers: expect.objectContaining({ 'Idempotency-Key': expect.any(String) }),
      }),
    )
  })

  it('remove 使用 DELETE /admin/roles/{id}', async () => {
    vi.mocked(client.delete).mockResolvedValue({ data: undefined })
    await rolesApi.remove('r-1')
    expect(client.delete).toHaveBeenCalledWith('/admin/roles/r-1')
  })

  it('getPermissions 使用 GET /admin/roles/{id}/permissions', async () => {
    vi.mocked(client.get).mockResolvedValue({ data: [] })
    await rolesApi.getPermissions('r-1')
    expect(client.get).toHaveBeenCalledWith('/admin/roles/r-1/permissions')
  })

  it('getPermissionCatalog 使用 GET /admin/roles/permissions/catalog', async () => {
    vi.mocked(client.get).mockResolvedValue({ data: [] })
    await rolesApi.getPermissionCatalog()
    expect(client.get).toHaveBeenCalledWith('/admin/roles/permissions/catalog')
  })

  it('updatePermissions 使用 PUT /admin/roles/{id}/permissions 并注入 Idempotency-Key', async () => {
    vi.mocked(client.put).mockResolvedValue({ data: undefined })
    const body: UpdateRolePermissionsDto = { permissions: ['user:read', 'role:write'] }
    await rolesApi.updatePermissions('r-1', body)
    expect(client.put).toHaveBeenCalledWith(
      '/admin/roles/r-1/permissions',
      body,
      expect.objectContaining({
        headers: expect.objectContaining({ 'Idempotency-Key': expect.any(String) }),
      }),
    )
  })
})
