import type { AxiosInstance } from 'axios'
import MockAdapter from 'axios-mock-adapter'
import { ensureSeedData, resetSeedData } from './data/seed'
import { registerAuthHandlers } from './handlers/auth'

/**
 * 装配 MockAdapter
 *
 * - 启用条件：main.ts 中 DEV && VITE_USE_MOCK === 'true' 双重守卫后动态 import
 * - 命中规则：仅拦截已注册端点（/auth/login、/auth/logout、/users/me、/mock/reset）
 * - 未匹配请求透传到真实后端（mock.onAny().passThrough()）
 *
 * 生产环境保护：在非 dev 且未显式开启 mock 时直接抛错，避免误启用。
 */
export function setupMockAdapter(client: AxiosInstance): void {
  if (!import.meta.env.DEV && import.meta.env.VITE_USE_MOCK !== 'true') {
    throw new Error('Mock should not be loaded in production')
  }
  ensureSeedData()
  const mock = new MockAdapter(client, { delayResponse: 300 })

  // Mock 重置端点（仅开发联调用）
  mock.onPost('/mock/reset').reply(() => {
    resetSeedData()
    return [200, { code: 200, message: 'OK', data: { success: true } }]
  })

  registerAuthHandlers(mock)

  // 未匹配的请求透传到真实后端
  mock.onAny().passThrough()

  console.log('[Mock] operations 已启用鉴权 handler，覆盖 3 个 endpoint（另含 mock/reset）')
}
