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

const app = createApp(App)

app.use(pinia)
app.use(router)
app.use(Antd)
app.component('ECharts', EChartsVue)
app.directive('permission', vPermission)

app.config.errorHandler = (err) => {
  logger.error('Unhandled app error', err)
}

app.mount('#app')
