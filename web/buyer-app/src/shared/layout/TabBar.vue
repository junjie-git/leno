<script setup lang="ts">
import { computed, onMounted } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import { useCartStore } from '@/modules/05-cart/stores/cart.store'

/**
 * 底部 Tabbar：首页 / 分类 / 购物车 / 我的
 *
 * - 激活态由当前路由推导（/、/category、/cart、/profile）
 * - 购物车角标展示购物车总件数（登录后拉取）
 * - fixed 布局，适配 safe-area-inset-bottom
 */
const route = useRoute()
const router = useRouter()
const cartStore = useCartStore()

const TAB_PATHS = ['/', '/category', '/cart', '/profile'] as const

const activeIndex = computed(() => {
  const idx = TAB_PATHS.indexOf(route.path as (typeof TAB_PATHS)[number])
  return idx >= 0 ? idx : 0
})

onMounted(() => {
  cartStore.refreshBadge()
})

function go(path: string): void {
  if (route.path !== path) {
    router.push(path)
  }
}
</script>

<template>
  <van-tabbar :model-value="activeIndex" :safe-area-inset-bottom="true" placeholder>
    <van-tabbar-item @click="go('/')">
      <span>首页</span>
      <template #icon="props">
        <van-icon name="wap-home-o" :color="props.active ? '#1677FF' : '#8C8C8C'" size="22" />
      </template>
    </van-tabbar-item>
    <van-tabbar-item @click="go('/category')">
      <span>分类</span>
      <template #icon="props">
        <van-icon name="apps-o" :color="props.active ? '#1677FF' : '#8C8C8C'" size="22" />
      </template>
    </van-tabbar-item>
    <van-tabbar-item @click="go('/cart')">
      <span>购物车</span>
      <template #icon="props">
        <van-badge :content="cartStore.badge" :show-zero="false" max-count="99">
          <van-icon name="cart-o" :color="props.active ? '#1677FF' : '#8C8C8C'" size="22" />
        </van-badge>
      </template>
    </van-tabbar-item>
    <van-tabbar-item @click="go('/profile')">
      <span>我的</span>
      <template #icon="props">
        <van-icon name="user-o" :color="props.active ? '#1677FF' : '#8C8C8C'" size="22" />
      </template>
    </van-tabbar-item>
  </van-tabbar>
</template>
