<script setup lang="ts">
import { computed } from 'vue'
import { useRoute } from 'vue-router'
import TabBar from './TabBar.vue'

/**
 * 买家端全局布局
 *
 * 结构：app-shell（375px 基准 / ≥768px 居中 480px）
 *   ├── app-page：RouterView（Tabbar 四页 KeepAlive 缓存）
 *   └── TabBar（仅 meta.tabbar 页显示；秒杀/支付等聚焦任务页隐藏）
 */
const route = useRoute()
const showTabBar = computed(() => route.meta.tabbar === true)
</script>

<template>
  <div class="app-shell">
    <div class="app-page">
      <RouterView v-slot="{ Component }">
        <KeepAlive include="HomeFeed, CategoryNav, Cart, Profile">
          <component :is="Component" />
        </KeepAlive>
      </RouterView>
    </div>
    <TabBar v-if="showTabBar" />
  </div>
</template>
