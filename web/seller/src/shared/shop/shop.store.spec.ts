import { setActivePinia, createPinia } from 'pinia'
import { beforeEach, describe, expect, it, vi } from 'vitest'
import { useShopStore } from './shop.store'

vi.mock('@/shared/http', () => ({
  client: {
    get: vi.fn(),
    put: vi.fn(),
  },
}))

import { client } from '@/shared/http'

describe('useShopStore', () => {
  beforeEach(() => {
    setActivePinia(createPinia())
    localStorage.clear()
    vi.clearAllMocks()
  })

  it('fetchMyShop 拉取店铺信息并填充 state', async () => {
    vi.mocked(client.get).mockResolvedValue({
      data: {
        shopId: 'shop-1',
        shopName: '测试店铺',
        status: 'Active',
        qualificationsStatus: {},
      },
    } as any)
    const store = useShopStore()
    await store.fetchMyShop()
    expect(store.shopId).toBe('shop-1')
    expect(store.shopName).toBe('测试店铺')
    expect(store.shopStatus).toBe('Active')
  })

  it('canPublish 返回 true 仅当 status === Active', () => {
    const store = useShopStore()
    store.shopStatus = 'Active'
    expect(store.canPublish).toBe(true)
    store.shopStatus = 'Suspended'
    expect(store.canPublish).toBe(false)
    store.shopStatus = 'PendingReview'
    expect(store.canPublish).toBe(false)
  })

  it('canFulfill 返回 true 仅当 status !== Rejected', () => {
    const store = useShopStore()
    store.shopStatus = 'Active'
    expect(store.canFulfill).toBe(true)
    store.shopStatus = 'Suspended'
    expect(store.canFulfill).toBe(true)
    store.shopStatus = 'Rejected'
    expect(store.canFulfill).toBe(false)
  })

  it('isOnboardingComplete 返回 true 仅当 status === Active', () => {
    const store = useShopStore()
    store.shopStatus = 'Active'
    expect(store.isOnboardingComplete).toBe(true)
    store.shopStatus = 'PendingReview'
    expect(store.isOnboardingComplete).toBe(false)
  })

  it('updateShop 调用 PUT /shops/me', async () => {
    vi.mocked(client.put).mockResolvedValue({} as any)
    const store = useShopStore()
    store.shopId = 'shop-1'
    await store.updateShop({ shopName: '新名称' } as any)
    expect(client.put).toHaveBeenCalledWith('/shops/me', { shopName: '新名称' })
  })
})
