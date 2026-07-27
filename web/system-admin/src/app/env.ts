/**
 * import.meta.env 类型化封装
 *
 * 集中声明所有环境变量，避免散落在各处。Vite 注入 import.meta.env。
 */

interface AppEnv {
  /** API 基础路径，dev 下为 /api（经 Vite proxy） */
  readonly apiBase: string
  /** 是否强制 2FA（默认 false，仅账号密码登录） */
  readonly require2FA: boolean
  /** 应用版本号 */
  readonly appVersion: string
  /** 后端 API target，仅 dev 使用（proxy 转发目标） */
  readonly apiTarget?: string
}

function parseBoolean(value: string | undefined, defaultValue = false): boolean {
  if (value === undefined) return defaultValue
  return value === 'true' || value === '1' || value === 'yes'
}

export const env: AppEnv = {
  apiBase: import.meta.env.VITE_API_BASE ?? '/api',
  require2FA: parseBoolean(import.meta.env.VITE_REQUIRE_2FA, false),
  appVersion: import.meta.env.VITE_APP_VERSION ?? 'dev',
  apiTarget: import.meta.env.VITE_API_TARGET,
} as const
