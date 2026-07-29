/**
 * shared/auth 出口
 */
export { useAuthStore } from './auth.store'
export type { AuthState, SellerUserDto, LoginDto, LoginResultDto } from './auth.store'
export { vPermission } from './permission'
export { default as PermissionGuard } from './PermissionGuard.vue'
