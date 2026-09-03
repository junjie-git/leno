import { createApp } from 'vue'
import Vant from 'vant'
import { showFailToast, showNotify } from 'vant'
import 'vant/lib/index.css'
import App from './App.vue'
import { pinia } from './app/pinia'
import { router } from './app/router'
import { logger } from './shared/utils/logger'
import { BusinessError, RateLimitedError, UnauthorizedError } from '@/shared/http/errors'
import { client } from '@/shared/http'
import { useAuthStore } from '@/shared/auth'
import '@/shared/tokens/design-tokens.css'

const app = createApp(App)

app.use(pinia)
app.use(router)
app.use(Vant)

// 开发环境 mock 装配（DEV && VITE_USE_MOCK 双重守卫 + 动态 import）
// 生产环境不打包 mock 模块
if (import.meta.env.DEV && import.meta.env.VITE_USE_MOCK === 'true') {
  const { setupMockAdapter } = await import('@/shared/http/mock')
  setupMockAdapter(client)
}

/**
 * 全局错误处理
 *
 * - BusinessError → showFailToast
 * - UnauthorizedError → 清理登录态并跳登录
 * - RateLimitedError → showNotify 倒计时提示
 * - 其它（NetworkError/ServerError）由页面级 ErrorState 兜底
 */
app.config.errorHandler = (err) => {
  logger.error('全局错误捕获', err)
  if (err instanceof UnauthorizedError) {
    const auth = useAuthStore()
    void auth.logout().finally(() => {
      router.push({ path: '/login', query: { redirect: router.currentRoute.value.fullPath } })
    })
    return
  }
  if (err instanceof BusinessError) {
    showFailToast(err.message)
  } else if (err instanceof RateLimitedError) {
    showNotify({
      type: 'warning',
      message: `操作过于频繁，请 ${err.retryAfter}s 后重试`,
      duration: 2000,
    })
  }
}

window.addEventListener('unhandledrejection', (event) => {
  logger.error('未捕获的 Promise 错误', event.reason)
})

app.mount('#app')
