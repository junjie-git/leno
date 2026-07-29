import { defineStore } from 'pinia'
import { client } from '@/shared/http'
import { logger } from '@/shared/utils/logger'

/**
 * 店铺状态枚举
 */
export type ShopStatus = 'Active' | 'Suspended' | 'PendingReview' | 'Rejected'

/**
 * 店铺信息 DTO
 */
export interface ShopDto {
  shopId: string
  shopName: string
  status: ShopStatus
  qualificationsStatus: Record<string, 'Approved' | 'Pending' | 'Rejected'>
}

/**
 * 店铺状态
 */
interface ShopState {
  shopId: string | null
  shopName: string | null
  shopStatus: ShopStatus | null
  qualificationsStatus: Record<string, 'Approved' | 'Pending' | 'Rejected'>
}

/**
 * Shop Store — 店铺状态门禁
 *
 * - canPublish: 仅 Active 态可上架商品
 * - canFulfill: 非 Rejected 态可履约既有订单
 * - isOnboardingComplete: Active 表示入驻完成
 */
export const useShopStore = defineStore('shop', {
  state: (): ShopState => ({
    shopId: null,
    shopName: null,
    shopStatus: null,
    qualificationsStatus: {},
  }),
  getters: {
    canPublish: (s): boolean => s.shopStatus === 'Active',
    canFulfill: (s): boolean => s.shopStatus !== 'Rejected',
    isOnboardingComplete: (s): boolean => s.shopStatus === 'Active',
  },
  actions: {
    /**
     * 拉取当前卖家店铺信息
     * GET /api/shops/me
     */
    async fetchMyShop(): Promise<void> {
      try {
        const shop = await client.get<ShopDto>('/shops/me').then((r) => r.data)
        this.shopId = shop.shopId
        this.shopName = shop.shopName
        this.shopStatus = shop.status
        this.qualificationsStatus = shop.qualificationsStatus ?? {}
      } catch (e) {
        logger.warn('fetchMyShop 失败', e)
      }
    },

    /**
     * 更新店铺信息
     * PUT /api/shops/me
     */
    async updateShop(dto: Partial<Pick<ShopDto, 'shopName'>>): Promise<void> {
      await client.put<ShopDto>('/shops/me', dto)
      if (dto.shopName) this.shopName = dto.shopName
    },
  },
  persist: {
    storage: localStorage,
    pick: ['shopId', 'shopName', 'shopStatus'],
  },
})
