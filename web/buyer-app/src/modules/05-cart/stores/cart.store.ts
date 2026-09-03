import { defineStore } from 'pinia'
import { cartApi } from '../api/cart.api'
import { useAuthStore } from '@/shared/auth'
import { logger } from '@/shared/utils/logger'

/**
 * 购物车状态（角标 + 合并）
 *
 * - badge：Tabbar 购物车角标（总件数），登录后拉取，未登录为 0
 * - refreshBadge：登录态变化 / 增删改后刷新
 * - mergeLocalCart：预留匿名车合并入口（当前版本登录后直接拉取服务端车）
 */
export const useCartStore = defineStore('cart', {
  state: () => ({
    badge: 0,
  }),
  actions: {
    /** 刷新购物车角标（未登录直接清零） */
    async refreshBadge(): Promise<void> {
      const auth = useAuthStore()
      if (!auth.isAuthenticated) {
        this.badge = 0
        return
      }
      try {
        const cart = await cartApi.getCart()
        this.badge = cart.totalCount
      } catch (e) {
        logger.warn('刷新购物车角标失败（忽略）', e)
        this.badge = 0
      }
    },

    /** 登录后合并匿名车并刷新角标 */
    async mergeLocalCart(items: Array<{ skuId: string; quantity: number }>): Promise<void> {
      if (items.length === 0) {
        await this.refreshBadge()
        return
      }
      await cartApi.merge({ items })
      await this.refreshBadge()
    },
  },
})
