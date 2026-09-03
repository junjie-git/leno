import { createPinia, setActivePinia } from 'pinia'
import { beforeEach, describe, expect, it, vi } from 'vitest'

const getCartMock = vi.hoisted(() => vi.fn())
const mergeMock = vi.hoisted(() => vi.fn())

vi.mock('@/modules/05-cart/api/cart.api', () => ({
  cartApi: {
    getCart: getCartMock,
    merge: mergeMock,
  },
}))

const { useCartStore } = await import('@/modules/05-cart/stores/cart.store')
const { useAuthStore } = await import('@/shared/auth/auth.store')

describe('useCartStore（购物车角标）', () => {
  beforeEach(() => {
    setActivePinia(createPinia())
    localStorage.clear()
    vi.clearAllMocks()
  })

  it('未登录时角标清零且不发起请求', async () => {
    const cart = useCartStore()
    await cart.refreshBadge()
    expect(cart.badge).toBe(0)
    expect(getCartMock).not.toHaveBeenCalled()
  })

  it('登录后刷新角标为购物车总件数', async () => {
    getCartMock.mockResolvedValue({
      items: [],
      totalCount: 6,
      selectedCount: 4,
      selectedAmount: 88800,
      hasInvalid: false,
    })
    const auth = useAuthStore()
    auth.applyLoginResult({
      token: 'jwt-token',
      user: undefined,
      roles: ['Buyer'],
      permissions: [],
      expiresIn: 3600,
    })

    const cart = useCartStore()
    await cart.refreshBadge()
    expect(cart.badge).toBe(6)
  })

  it('拉取失败时角标安全回退为 0（不抛错）', async () => {
    getCartMock.mockRejectedValue(new Error('server error'))
    const auth = useAuthStore()
    auth.applyLoginResult({
      token: 'jwt-token',
      user: undefined,
      roles: ['Buyer'],
      permissions: [],
      expiresIn: 3600,
    })

    const cart = useCartStore()
    await expect(cart.refreshBadge()).resolves.toBeUndefined()
    expect(cart.badge).toBe(0)
  })

  it('mergeLocalCart 合并匿名车后刷新角标', async () => {
    mergeMock.mockResolvedValue(undefined)
    getCartMock.mockResolvedValue({
      items: [],
      totalCount: 9,
      selectedCount: 9,
      selectedAmount: 12300,
      hasInvalid: false,
    })
    const auth = useAuthStore()
    auth.applyLoginResult({
      token: 'jwt-token',
      user: undefined,
      roles: ['Buyer'],
      permissions: [],
      expiresIn: 3600,
    })

    const cart = useCartStore()
    await cart.mergeLocalCart([{ skuId: 'spu-101-sku1', quantity: 2 }])
    expect(mergeMock).toHaveBeenCalledWith({
      items: [{ skuId: 'spu-101-sku1', quantity: 2 }],
    })
    expect(cart.badge).toBe(9)
  })

  it('空合并列表跳过 merge 直接刷新', async () => {
    const auth = useAuthStore()
    auth.applyLoginResult({
      token: 'jwt-token',
      user: undefined,
      roles: ['Buyer'],
      permissions: [],
      expiresIn: 3600,
    })

    const cart = useCartStore()
    getCartMock.mockResolvedValue({
      items: [],
      totalCount: 3,
      selectedCount: 0,
      selectedAmount: 0,
      hasInvalid: false,
    })
    await cart.mergeLocalCart([])
    expect(mergeMock).not.toHaveBeenCalled()
    expect(cart.badge).toBe(3)
  })
})
