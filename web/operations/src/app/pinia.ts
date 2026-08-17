import { createPinia } from 'pinia'
import piniaPluginPersistedstate from 'pinia-plugin-persistedstate'

/**
 * 全局 Pinia 实例
 *
 * 注册持久化插件（localStorage），各 store 通过 `persist` 选项声明持久化字段。
 */
export const pinia = createPinia()
pinia.use(piniaPluginPersistedstate)
