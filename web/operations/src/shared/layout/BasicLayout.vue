<script setup lang="ts">
import { ref, computed } from 'vue'
import { Layout, LayoutContent } from 'ant-design-vue'
import HeaderBar from './HeaderBar.vue'
import SiderMenu from './SiderMenu.vue'
import FooterBar from './FooterBar.vue'

/**
 * 全局布局容器
 *
 * 与 spec §5.3 布局结构保持一致：
 * - Header 64px 固定顶部
 * - Sider 200px 固定左侧（可折叠至 80px）
 * - Content padding 24px
 * - Footer 32px
 *
 * 响应式断点：
 * - ≥ 1200px：Sider 全展开
 * - 992-1199px：Sider 自动折叠
 * - < 992px：显示「请使用桌面端访问」提示
 */
const siderCollapsed = ref(false)
const isMobile = ref(false)

function updateResponsive() {
  const width = window.innerWidth
  isMobile.value = width < 992
  if (width < 1200 && width >= 992) {
    siderCollapsed.value = true
  } else if (width >= 1200) {
    siderCollapsed.value = false
  }
}

if (typeof window !== 'undefined') {
  updateResponsive()
  window.addEventListener('resize', updateResponsive)
}

const contentMarginLeft = computed(() => (siderCollapsed.value ? 80 : 200))
</script>

<template>
  <div v-if="isMobile" class="mobile-warn">
    <div class="mobile-warn-content">
      <h2>请使用桌面端访问</h2>
      <p>运营管理后台仅支持桌面浏览器（宽度 ≥ 992px），请切换设备后再访问。</p>
    </div>
  </div>
  <Layout v-else class="basic-layout">
    <HeaderBar @toggle-sider="siderCollapsed = !siderCollapsed" />
    <Layout class="basic-layout-main">
      <SiderMenu :collapsed="siderCollapsed" />
      <LayoutContent class="basic-layout-content" :style="{ marginLeft: `${contentMarginLeft}px` }">
        <slot />
        <FooterBar />
      </LayoutContent>
    </Layout>
  </Layout>
</template>

<style scoped>
.basic-layout {
  min-height: 100vh;
}
.basic-layout-main {
  padding-top: 64px;
}
.basic-layout-content {
  padding: 24px;
  min-height: calc(100vh - 64px - 32px);
  background: #f5f5f5;
}
.mobile-warn {
  position: fixed;
  inset: 0;
  display: flex;
  align-items: center;
  justify-content: center;
  background: #f5f5f5;
  z-index: 1000;
}
.mobile-warn-content {
  text-align: center;
  padding: 24px;
}
.mobile-warn-content h2 {
  font-size: 20px;
  color: #000000d9;
  margin-bottom: 12px;
}
.mobile-warn-content p {
  font-size: 14px;
  color: #595959;
}
</style>
