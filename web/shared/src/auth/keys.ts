/**
 * 认证持久化存储键（P1#11：消除 'auth' 魔法字符串）
 *
 * pinia-plugin-persistedstate 默认以 store id 作为 localStorage key，
 * 四端 useAuthStore 统一使用本常量作为 store id，
 * http client 绕过 store 直读 localStorage 时也使用同一常量，
 * 避免两端各自硬编码 'auth' 导致重命名时静默断链。
 */
export const AUTH_STORE_ID = 'auth'

/** 持久化 AuthState 的最小形状（client 直读 localStorage 时依赖） */
export interface PersistedAuthState {
  token?: string | null
  expiresAt?: number | null
}
