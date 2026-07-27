<script setup lang="ts">
import { computed, ref } from 'vue'
import { LayoutHeader, Breadcrumb, Badge, Dropdown, Input, Modal, Menu as AMenu } from 'ant-design-vue'
import { BellOutlined, SearchOutlined, UserOutlined, LogoutOutlined } from '@ant-design/icons-vue'
import { useRoute, useRouter } from 'vue-router'
import { useAuthStore } from '@/shared/auth'
import { env } from '@/app/env'

/**
 * 顶栏组件
 *
 * 含 Logo + Breadcrumb + 全局搜索 + 通知铃铛 + 用户菜单。
 */

const emit = defineEmits<{
  (e: 'toggle-sider'): void
}>()

const route = useRoute()
const router = useRouter()
const auth = useAuthStore()

// 面包屑：基于 route.matched 的 meta.title
const breadcrumbs = computed(() => {
  return route.matched
    .filter((r) => r.meta?.title)
    .map((r) => ({ title: r.meta.title as string, path: r.path }))
})

// 通知数量（占位，后续 Plan 接入 /api/admin/alerts?status=firing）
const unread = ref(0)

const searchVisible = ref(false)
const searchKeyword = ref('')

function onSearch() {
  // 简单实现：根据关键字跳转到第一个匹配的菜单项
  if (!searchKeyword.value) return
  searchVisible.value = false
  // 后续 Plan 在此对接全局搜索后端
}

function onLogout() {
  void auth.logout().then(() => {
    void router.push('/login')
  })
}

function onProfile() {
  void router.push('/account/profile')
}

const userMenuItems = [
  { key: 'profile', label: '个人中心', icon: UserOutlined },
  { key: 'logout', label: '登出', icon: LogoutOutlined },
]

function onUserMenuClick({ key }: { key: string }) {
  if (key === 'logout') onLogout()
  else if (key === 'profile') onProfile()
}
</script>

<template>
  <LayoutHeader class="header-bar">
    <div class="header-left">
      <span class="header-toggle" @click="emit('toggle-sider')">☰</span>
      <span class="header-logo">Leno 系统管理后台</span>
      <Breadcrumb class="header-breadcrumb">
        <Breadcrumb.Item v-for="crumb in breadcrumbs" :key="crumb.path">
          {{ crumb.title }}
        </Breadcrumb.Item>
      </Breadcrumb>
    </div>
    <div class="header-right">
      <span class="header-action" @click="searchVisible = true">
        <SearchOutlined />
        <span class="header-action-text">搜索</span>
      </span>
      <span class="header-action">
        <Badge :count="unread" :overflow-count="99">
          <BellOutlined style="font-size: 18px" />
        </Badge>
      </span>
      <Dropdown :trigger="['click']">
        <span class="header-action header-user">
          <UserOutlined />
          <span class="header-username">{{ auth.user?.username ?? '未登录' }}</span>
        </span>
        <template #overlay>
          <AMenu @click="onUserMenuClick">
            <AMenu.Item key="profile"><UserOutlined /> 个人中心</AMenu.Item>
            <AMenu.Item key="logout"><LogoutOutlined /> 登出</AMenu.Item>
          </AMenu>
        </template>
      </Dropdown>
    </div>
    <Modal v-model:open="searchVisible" title="全局搜索" @ok="onSearch">
      <Input v-model:value="searchKeyword" placeholder="输入菜单或端点关键词" allow-clear />
    </Modal>
  </LayoutHeader>
</template>

<style scoped>
.header-bar {
  position: fixed;
  top: 0;
  left: 0;
  right: 0;
  height: 64px;
  padding: 0 24px;
  background: #ffffff;
  border-bottom: 1px solid #f0f0f0;
  display: flex;
  align-items: center;
  justify-content: space-between;
  z-index: 100;
}
.header-left {
  display: flex;
  align-items: center;
  gap: 16px;
}
.header-toggle {
  cursor: pointer;
  font-size: 18px;
  padding: 0 8px;
}
.header-logo {
  font-size: 16px;
  font-weight: 600;
  color: #000000d9;
  white-space: nowrap;
}
.header-breadcrumb {
  margin-left: 16px;
}
.header-right {
  display: flex;
  align-items: center;
  gap: 24px;
}
.header-action {
  cursor: pointer;
  display: inline-flex;
  align-items: center;
  gap: 6px;
}
.header-action-text {
  font-size: 13px;
  color: #595959;
}
.header-user {
  gap: 8px;
}
.header-username {
  font-size: 14px;
  color: #000000d9;
}
</style>
