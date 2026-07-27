import { createApp } from 'vue'
import Antd from 'ant-design-vue'
import { message, Modal } from 'ant-design-vue'
import 'ant-design-vue/dist/reset.css'
import App from './App.vue'
import { pinia } from './app/pinia'
import { router } from './app/router'
import { logger } from './shared/utils/logger'
import { BusinessError, ConcurrencyError, RateLimitedError } from '@/shared/http/errors'
import { client } from '@/shared/http'
import { setupMockAdapter } from '@/shared/http/mock'
import '@/shared/tokens/design-tokens.css'

const app = createApp(App)

app.use(pinia)
app.use(router)
app.use(Antd)

if (import.meta.env.VITE_USE_MOCK === 'true') {
  setupMockAdapter(client)
}

/**
 * 全局错误处理（spec §3.10）
 *
 * - BusinessError → message.error
 * - ConcurrencyError → Modal.confirm 刷新重试
 * - RateLimitedError → message.warning 倒计时
 * - 其它（NetworkError/ServerError）由页面级 ErrorBoundary 兜底
 */
app.config.errorHandler = (err) => {
  logger.error('全局错误捕获', err)
  if (err instanceof BusinessError) {
    message.error(err.message)
  } else if (err instanceof ConcurrencyError) {
    Modal.confirm({
      title: '资源已被他人修改',
      content: `当前版本：v${err.currentVersion}。是否刷新后重试？`,
      okText: '刷新重试',
      cancelText: '取消',
      onOk: () => window.location.reload(),
    })
  } else if (err instanceof RateLimitedError) {
    message.warning(`操作过于频繁，请 ${err.retryAfter}s 后重试`)
  }
}

window.addEventListener('unhandledrejection', (event) => {
  logger.error('未捕获的 Promise 错误', event.reason)
})

app.mount('#app')
