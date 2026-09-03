import { defineStore } from 'pinia'
import { authApi } from '@/modules/01-auth/api/auth.api'
import type { BuyerUserDto, LoginRequestDto, LoginResultDto, TwoFactorVerifyRequestDto } from '@/modules/01-auth/types/auth.dto'
import { logger } from '@/shared/utils/logger'

/**
 * 鉴权状态
 */
export interface AuthState {
  token: string | null
  user: BuyerUserDto | null
  roles: string[]
  permissions: string[]
  loginAt: number | null
  expiresAt: number | null
}

/**
 * 买家端鉴权 Store
 *
 * - 持久化字段：token / user / roles / permissions / expiresAt（localStorage key: auth）
 * - login：POST /api/account/login → 需要 2FA 时返回票据（不落 state），否则直接填充
 * - applyLoginResult：统一落地登录结果（账号密码 / 2FA 二段 / OAuth 回调共用）
 * - verifyTwoFactor：POST /api/auth/two-factor/verify 完成二段登录
 * - fetchProfile：GET /api/users/me → 刷新 user
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
  }),
  getters: {
    isAuthenticated: (s): boolean => !!s.token && (s.expiresAt ?? 0) > Date.now(),
    nickname: (s): string => s.user?.nickname ?? s.user?.username ?? '未登录',
    isBuyer: (s): boolean => s.roles.length === 0 || s.roles.includes('Buyer'),
  },
  actions: {
    /**
     * 账号密码登录
     *
     * 返回原始 LoginResultDto：
     * - requiresTwoFactor = true → 页面携带 twoFactorTicket 跳 /two-factor
     * - 否则 state 已填充，页面按 redirect 跳转
     */
    async login(body: LoginRequestDto): Promise<LoginResultDto> {
      const result = await authApi.login(body)
      if (!result.requiresTwoFactor) {
        this.applyLoginResult(result)
      }
      return result
    },

    /**
     * 2FA 二段验证登录
     */
    async verifyTwoFactor(body: TwoFactorVerifyRequestDto): Promise<LoginResultDto> {
      const result = await authApi.verifyTwoFactor(body)
      this.applyLoginResult(result)
      return result
    },

    /**
     * 统一落地登录结果（token + user + roles）
     */
    applyLoginResult(result: LoginResultDto): void {
      this.token = result.token ?? null
      this.user = result.user ?? null
      this.roles = result.roles ?? ['Buyer']
      this.permissions = result.permissions ?? []
      this.loginAt = Date.now()
      this.expiresAt = result.expiresIn ? Date.now() + result.expiresIn * 1000 : null
    },

    /**
     * 拉取当前用户 profile，刷新 user
     */
    async fetchProfile(): Promise<void> {
      const user = await authApi.getProfile()
      this.user = user
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
    },
  },
  persist: {
    storage: localStorage,
    pick: ['token', 'user', 'roles', 'permissions', 'expiresAt'],
  },
})
