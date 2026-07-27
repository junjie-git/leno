// web/system-admin/src/modules/02-user-access/api/roles.api.ts

import { client, withIdempotency } from '@/shared/http'
import type { PageResult, PageQuery } from '@/shared/types'
import type {
  RoleDto,
  ListRolesParams,
  SaveRoleDto,
  UpdateRolePermissionsDto,
  PermissionGroupDto,
} from '../types/role.dto'

// 角色管理 API（AccessControl 域 AdminRolesController）
export const rolesApi = {
  // 分页查询角色列表
  list: (params: ListRolesParams & PageQuery) =>
    client.get<PageResult<RoleDto>>('/admin/roles', { params }),

  // 查询角色详情
  get: (id: string) =>
    client.get<RoleDto>(`/admin/roles/${id}`),

  // 创建角色（幂等）
  create: (body: SaveRoleDto) =>
    client.post<RoleDto>('/admin/roles', body, withIdempotency()),

  // 编辑角色（幂等）
  update: (id: string, body: SaveRoleDto) =>
    client.put<RoleDto>(`/admin/roles/${id}`, body, withIdempotency()),

  // 删除角色（内置角色后端拒绝）
  remove: (id: string) =>
    client.delete<void>(`/admin/roles/${id}`),

  // 查看角色已分配的权限码列表
  getPermissions: (id: string) =>
    client.get<string[]>(`/admin/roles/${id}/permissions`),

  // 获取全量权限目录（按模块分组，用于权限树渲染）
  getPermissionCatalog: () =>
    client.get<PermissionGroupDto[]>('/admin/roles/permissions/catalog'),

  // 全量替换角色权限（幂等）
  updatePermissions: (id: string, body: UpdateRolePermissionsDto) =>
    client.put<void>(`/admin/roles/${id}/permissions`, body, withIdempotency()),
}
