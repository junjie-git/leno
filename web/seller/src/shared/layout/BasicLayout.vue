<script setup lang="ts">
import { Layout } from 'ant-design-vue'
import { ref } from 'vue'
import SiderMenu from './SiderMenu.vue'
import HeaderBar from './HeaderBar.vue'
import FooterBar from './FooterBar.vue'

const { Sider, Header, Content } = Layout

const collapsed = ref(false)
const isDesktop = ref(window.innerWidth >= 992)

window.addEventListener('resize', () => {
  isDesktop.value = window.innerWidth >= 992
  if (window.innerWidth < 1200) collapsed.value = true
})
</script>

<template>
  <div v-if="!isDesktop" class="desktop-only-notice">
    <p>请使用桌面端访问卖家管理后台</p>
    <p class="hint">建议屏幕宽度 ≥ 992px</p>
  </div>
  <Layout v-else class="basic-layout">
    <Sider
      v-model:collapsed="collapsed"
      :trigger="null"
      collapsible
      :width="200"
      :collapsed-width="80"
      class="basic-sider"
    >
      <SiderMenu :collapsed="collapsed" />
    </Sider>
    <Layout>
      <Header class="basic-header" :class="{ 'is-collapsed': collapsed }">
        <HeaderBar :collapsed="collapsed" @toggle="collapsed = !collapsed" />
      </Header>
      <Content class="basic-content" :class="{ 'is-collapsed': collapsed }">
        <RouterView />
      </Content>
      <FooterBar />
    </Layout>
  </Layout>
</template>

<style scoped>
.basic-layout { min-height: 100vh; }
.basic-sider { position: fixed; left: 0; top: 0; bottom: 0; z-index: 100; }
.basic-header {
  position: fixed; top: 0; right: 0; left: 200px; z-index: 99;
  height: 64px; padding: 0 24px; background: #fff; box-shadow: 0 1px 4px rgba(0,0,0,0.08);
  display: flex; align-items: center; transition: left 0.2s;
}
.basic-header.is-collapsed { left: 80px; }
.basic-content {
  margin-left: 200px; margin-top: 64px; padding: 24px; min-height: calc(100vh - 64px - 32px);
  transition: margin-left 0.2s;
}
.basic-content.is-collapsed { margin-left: 80px; }
.desktop-only-notice {
  display: flex; flex-direction: column; align-items: center; justify-content: center;
  min-height: 100vh; font-size: 18px; color: #595959;
}
.desktop-only-notice .hint { font-size: 14px; color: #8C8C8C; margin-top: 8px; }
</style>
