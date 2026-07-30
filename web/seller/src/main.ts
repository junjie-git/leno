import { createApp } from 'vue'
import Antd from 'ant-design-vue'
import 'ant-design-vue/dist/reset.css'
import EChartsVue from 'vue-echarts'
import 'echarts'
import App from './App.vue'
import pinia from './app/pinia'
import { router } from './app/router'
import { vPermission } from './shared/auth'
import './shared/tokens/design-tokens.css'
import { logger } from './shared/utils/logger'
import { client } from './shared/http'

const app = createApp(App)

app.use(pinia)
app.use(router)
app.use(Antd)
app.component('ECharts', EChartsVue)
app.directive('permission', vPermission)

app.config.errorHandler = (err) => {
  logger.error('Unhandled app error', err)
}

// 开发环境 mock 装配（VITE_USE_MOCK=true 时启用）
// 生产环境不打包 mock 模块（动态 import + 条件守卫双重保护）
if (import.meta.env.DEV && import.meta.env.VITE_USE_MOCK === 'true') {
  const { setupMockAdapter } = await import('./shared/http/mock')
  setupMockAdapter(client)
}

app.mount('#app')
