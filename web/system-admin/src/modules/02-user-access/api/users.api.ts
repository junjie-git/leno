// web/system-admin/src/modules/02-user-access/api/users.api.ts

import { client, withIdempotency } from '@/shared/http'
import type { PageResult, PageQuery } from '@/shared/types'
import type {
  UserDto,
  ListUsersParams,
  AssignUserRolesDto,
  UpdateUserStatusDto,
} from '../types/user.dto'

// 用户管理 API（Identity 域 AdminUsersController）
export const usersApi = {
  // 分页查询用户列表
  list: (params: ListUsersParams & PageQuery) =>
    client.get<PageResult<UserDto>>('/admin/users', { params }),

  // 查询单个用户详情
  get: (id: string) =>
    client.get<UserDto>(`/admin/users/${id}`),

  // 为用户分配角色（幂等，全量替换）
  assignRoles: (id: string, body: AssignUserRolesDto) =>
    client.put<UserDto>(`/admin/users/${id}/roles`, body, withIdempotency()),

  // 锁定/恢复用户账户（幂等）
  updateStatus: (id: string, body: UpdateUserStatusDto) =>
    client.put<UserDto>(`/admin/users/${id}/status`, body, withIdempotency()),
}
