import type { AdminUserDto } from '@/shared/auth/auth.store'

/**
 * 06-account 模块鉴权相关 DTO 聚合出口
 *
 * - 共享 DTO（AdminUserDto / LoginDto / LoginResultDto）由 shared/auth/auth.store.ts 持有，
 *   本文件透传 re-export，供模块内 api / views 统一引用。
 * - UserProfileResultDto 为模块自有 DTO（/api/users/me 响应），在此定义。
 */
export type { AdminUserDto, LoginDto, LoginResultDto } from '@/shared/auth/auth.store'

/**
 * GET /api/users/me 响应体
 *
 * profile 为当前管理员视图，permissions 为其权限码列表。
 */
export interface UserProfileResultDto {
  profile: AdminUserDto
  permissions: string[]
}

/**
 * 更新个人资料请求体
 */
export interface UpdateProfileDto {
  email?: string
  phone?: string
  nickname?: string
  avatar?: string
  remark?: string
}

/**
 * 修改密码请求体
 */
export interface ChangePasswordDto {
  oldPassword: string
  newPassword: string
}
