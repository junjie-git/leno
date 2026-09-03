import { createPinia } from 'pinia'
import piniaPluginPersistedstate from 'pinia-plugin-persistedstate'

/**
 * 全局 Pinia 实例（注册 localStorage 持久化插件，auth/cart store 使用）
 */
export const pinia = createPinia()
pinia.use(piniaPluginPersistedstate)
