import type { AdminUserDto } from '@/shared/auth/auth.store'

/**
 * 09-account 模块鉴权相关 DTO 聚合出口
 *
 * - 共享 DTO（AdminUserDto / LoginDto / LoginResultDto）由 shared/auth/auth.store.ts 持有，
 *   本文件透传 re-export，供模块内 api / views 统一引用。
 * - UserProfileResultDto 为模块自有 DTO（GET /api/users/me 响应），在此定义。
 */
export type { AdminUserDto, LoginDto, LoginResultDto } from '@/shared/auth/auth.store'

/**
 * GET /api/users/me 响应体
 *
 * profile 为当前运营人员视图，permissions 为其权限码列表。
 */
export interface UserProfileResultDto {
  profile: AdminUserDto
  permissions: string[]
}
