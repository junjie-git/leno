// web/system-admin/src/modules/02-user-access/types/user.dto.ts

// 用户状态：Active 正常 / Suspended 锁定 / Locked 系统锁定
export type UserStatus = 'Active' | 'Suspended' | 'Locked'

// 用户实体（对应后端 AdminUserDto）
export interface UserDto {
  id: string
  username: string
  email: string
  phone: string | null
  roles: string[]                 // 角色ID列表（用于分配角色穿梭框回填）
  status: UserStatus
  createdAt: string               // ISO 8601
  lastLoginAt: string | null
  lastLoginIp: string | null
}

// 列表查询参数（AdminUserQueryDto）
export interface ListUsersParams {
  keyword?: string                // 用户名/邮箱模糊匹配
  roles?: string[]                // 角色ID多选
  statuses?: UserStatus[]         // 状态多选
  fromTime?: string               // 注册时间起 ISO 8601 UTC
  toTime?: string                 // 注册时间止 ISO 8601 UTC
}

// 分配角色入参（PUT /admin/users/{id}/roles）
export interface AssignUserRolesDto {
  roleIds: string[]
}

// 状态变更入参（PUT /admin/users/{id}/status）
export interface UpdateUserStatusDto {
  status: 'Active' | 'Suspended'  // 仅允许在正常/锁定之间切换
  reason?: string                 // 锁定时必填，恢复时可选
}

// 登录历史条目（详情抽屉展示）
export interface UserLoginHistoryDto {
  loginAt: string
  loginIp: string
  success: boolean
  userAgent: string | null
}
