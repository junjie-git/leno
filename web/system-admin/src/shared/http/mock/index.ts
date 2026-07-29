import type { AxiosInstance } from 'axios'
import MockAdapter from 'axios-mock-adapter'
import { ensureSeedData } from './data/seed'
import { registerMenuHandlers } from './handlers/menu'
import { registerOnlineUserHandlers } from './handlers/online-users'
import { registerLoginLogHandlers } from './handlers/login-logs'
import { registerCacheHandlers } from './handlers/cache'
import { registerServerMonitorHandlers } from './handlers/server'

/**
 * 装配 MockAdapter
 *
 * - 启用条件：import.meta.env.VITE_USE_MOCK === 'true'
 * - 命中规则：仅拦截 5 个前缀（/admin/menus、/admin/online-users、/admin/login-logs、/admin/cache、/admin/server-monitor）
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
  mock.onPost('/admin/mock/reset').reply(() => {
    localStorage.removeItem('mock_seed_v1')
    ensureSeedData()
    return [200, { code: 200, message: 'OK', data: { success: true } }]
  })

  registerMenuHandlers(mock)
  registerOnlineUserHandlers(mock)
  registerLoginLogHandlers(mock)
  registerCacheHandlers(mock)
  registerServerMonitorHandlers(mock)

  // 未匹配的请求透传到真实后端
  mock.onAny().passThrough()

  // 启动日志
  console.log('[Mock] 已启用 5 个 handler，共 19 个 endpoint')
}
