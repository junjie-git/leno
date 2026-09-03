/**
 * import.meta.env 类型化封装
 *
 * 集中声明所有环境变量，避免散落在各处。Vite 注入 import.meta.env。
 */

interface AppEnv {
  /** API 基础路径，dev 下为 /api（经 Vite proxy） */
  readonly apiBase: string
  /** 应用版本号 */
  readonly appVersion: string
  /** 后端 API target，仅 dev 使用（proxy 转发目标） */
  readonly apiTarget?: string
  /** 是否启用 axios-mock-adapter（仅 dev 生效） */
  readonly useMock: boolean
}

function parseBoolean(value: string | undefined, defaultValue = false): boolean {
  if (value === undefined) return defaultValue
  return value === 'true' || value === '1' || value === 'yes'
}

export const env: AppEnv = {
  apiBase: import.meta.env.VITE_API_BASE ?? '/api',
  appVersion: import.meta.env.VITE_APP_VERSION ?? 'dev',
  apiTarget: import.meta.env.VITE_API_TARGET,
  useMock: parseBoolean(import.meta.env.VITE_USE_MOCK, false),
} as const
