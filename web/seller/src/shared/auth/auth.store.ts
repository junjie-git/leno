import { defineStore } from 'pinia'
import { authApi } from '@/modules/08-account/api/auth.api'
import { logger } from '@/shared/utils/logger'

/**
 * 卖家用户视图（含店铺信息）
 */
export interface SellerUserDto {
  id: string
  username: string
  email: string
  phone?: string
  nickname?: string
  avatar?: string
  shopId?: string
  shopName?: string
  shopStatus?: string
  status: string
  roles: string[]
}

/**
 * 登录请求体
 */
export interface LoginDto {
  username: string
  password: string
}

/**
 * 登录响应体（与后端 AuthController.Login 返回结构对齐）
 */
export interface LoginResultDto {
  token: string
  expiresIn: number
  user: SellerUserDto
  roles: string[]
  permissions: string[]
}

/**
 * 鉴权状态
 */
export interface AuthState {
  token: string | null
  user: SellerUserDto | null
  roles: string[]
  permissions: string[]
  loginAt: number | null
  expiresAt: number | null
  /** 2FA 待处理标志，本次不接通，永远为 false */
  twoFactorPending: boolean
}

/**
 * 鉴权 Store（Seller 角色）
 *
 * - 持久化字段：token / user / roles / permissions / expiresAt
 * - login：POST /api/auth/login → 填充 state
 * - fetchProfile：GET /api/users/me → 刷新 user 与 permissions
 * - logout：best-effort 调用 /api/auth/logout，无论成败都清空 state
 */
export const useAuthStore = defineStore('auth', {
  state: (): AuthState => ({
    token: null,
    user: null,
    roles: [],
    permissions: [],
    loginAt: null,
    expiresAt: null,
    twoFactorPending: false,
  }),
  getters: {
    isAuthenticated: (s): boolean => !!s.token && (s.expiresAt ?? 0) > Date.now(),
    isSeller: (s): boolean => s.roles.includes('Seller'),
    hasPermission: (s) => (perm: string): boolean =>
      s.permissions.includes(perm) || s.permissions.includes('*'),
  },
  actions: {
    /**
     * 登录
     */
    async login(body: LoginDto): Promise<void> {
      const result = await authApi.login(body)
      this.token = result.token
      this.user = result.user
      this.roles = result.roles
      this.permissions = result.permissions
      this.loginAt = Date.now()
      this.expiresAt = Date.now() + result.expiresIn * 1000
      this.twoFactorPending = false
    },

    /**
     * 拉取当前用户 profile，刷新 user/permissions
     */
    async fetchProfile(): Promise<void> {
      const { profile, permissions } = await authApi.getProfile()
      this.user = profile
      this.permissions = permissions
      if (profile.roles && profile.roles.length > 0) {
        this.roles = profile.roles
      }
    },

    /**
     * 登出：best-effort 调用后端 logout，失败不阻塞；最终清空 state
     */
    async logout(): Promise<void> {
      try {
        await authApi.logout()
      } catch (e) {
        logger.warn('authApi.logout 失败（忽略）', e)
      }
      this.token = null
      this.user = null
      this.roles = []
      this.permissions = []
      this.loginAt = null
      this.expiresAt = null
      this.twoFactorPending = false
    },

    /**
     * 角色校验：传入的角色列表与 store.roles 有交集则通过；空数组视为无角色要求，返回 true
     */
    hasRole(roles: string[]): boolean {
      if (roles.length === 0) return true
      return roles.some((r) => this.roles.includes(r))
    },
  },
  persist: {
    storage: localStorage,
    pick: ['token', 'user', 'roles', 'permissions', 'expiresAt'],
  },
})
