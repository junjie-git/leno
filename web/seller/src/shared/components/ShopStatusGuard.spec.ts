import { mount } from '@vue/test-utils'
import { setActivePinia, createPinia } from 'pinia'
import { beforeEach, describe, expect, it } from 'vitest'
import ShopStatusGuard from './ShopStatusGuard.vue'
import { useShopStore } from '@/shared/shop'

describe('ShopStatusGuard', () => {
  beforeEach(() => {
    setActivePinia(createPinia())
  })

  it('requires=canPublish + 店铺 Active 时显示 slot 内容', () => {
    const shop = useShopStore()
    shop.shopStatus = 'Active'
    const wrapper = mount(ShopStatusGuard, {
      props: { requires: 'canPublish' },
      slots: { default: '<button>上架</button>' },
    })
    expect(wrapper.html()).toContain('上架')
    expect(wrapper.find('.shop-status-guard-fallback').exists()).toBe(false)
  })

  it('requires=canPublish + 店铺非 Active 时显示 fallbackText', () => {
    const shop = useShopStore()
    shop.shopStatus = 'Suspended'
    const wrapper = mount(ShopStatusGuard, {
      props: { requires: 'canPublish', fallbackText: '店铺暂停中，无法上架' },
      slots: { default: '<button>上架</button>' },
    })
    expect(wrapper.html()).toContain('店铺暂停中，无法上架')
    // fallbackText 自身包含“上架”二字，因此通过判断按钮是否存在来验证 slot 未渲染
    expect(wrapper.find('button').exists()).toBe(false)
  })

  it('requires=canFulfill + 店铺 Rejected 时显示 fallbackText', () => {
    const shop = useShopStore()
    shop.shopStatus = 'Rejected'
    const wrapper = mount(ShopStatusGuard, {
      props: { requires: 'canFulfill', fallbackText: '店铺已驳回' },
      slots: { default: '<button>发货</button>' },
    })
    expect(wrapper.html()).toContain('店铺已驳回')
  })

  it('requires=canFulfill + 店铺 Suspended 时显示 slot 内容（允许履约）', () => {
    const shop = useShopStore()
    shop.shopStatus = 'Suspended'
    const wrapper = mount(ShopStatusGuard, {
      props: { requires: 'canFulfill' },
      slots: { default: '<button>发货</button>' },
    })
    expect(wrapper.html()).toContain('发货')
  })
})
