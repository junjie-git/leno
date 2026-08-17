/**
 * shared/auth 出口
 */
export { useAuthStore } from './auth.store'
export type { AdminUserDto, LoginDto, LoginResultDto, AuthState } from './auth.store'
export { vPermission } from './permission'
export { default as PermissionGuard } from './PermissionGuard.vue'
