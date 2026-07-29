<script setup lang="ts">
import { Menu } from 'ant-design-vue'
import { computed } from 'vue'
import { useRouter, useRoute } from 'vue-router'
import {
  DashboardOutlined, ShopOutlined, TruckOutlined,
  ProfileOutlined, CustomerServiceOutlined, CommentOutlined,
  SettingOutlined, ExportOutlined, UserOutlined,
} from '@ant-design/icons-vue'
import { useAuthStore } from '@/shared/auth'
import type { Component } from 'vue'

defineProps<{ collapsed: boolean }>()

const router = useRouter()
const route = useRoute()
const auth = useAuthStore()

const iconMap: Record<string, Component> = {
  DashboardOutlined, ShopOutlined, TruckOutlined,
  ProfileOutlined, CustomerServiceOutlined, CommentOutlined,
  SettingOutlined, ExportOutlined, UserOutlined,
}

interface MenuChild {
  key: string
  label: string
  path: string
  permission?: string
}
interface MenuGroup {
  key: string
  label: string
  icon: string
  children: MenuChild[]
}

const menuGroups: MenuGroup[] = [
  {
    key: '02-dashboard', label: '工作台', icon: 'DashboardOutlined',
    children: [
      { key: 'dashboard.overview', label: '经营概览', path: '/dashboard/overview', permission: 'dashboard:view' },
      { key: 'dashboard.sales-trend', label: '销售趋势', path: '/dashboard/sales-trend', permission: 'dashboard:sales-trend' },
      { key: 'dashboard.low-stock', label: '库存预警', path: '/dashboard/low-stock', permission: 'dashboard:low-stock' },
    ],
  },
  {
    key: '03-product-management', label: '商品管理', icon: 'ShopOutlined',
    children: [
      { key: 'product.list', label: '商品列表', path: '/products', permission: 'product:list' },
    ],
  },
  {
    key: '04-logistics', label: '物流管理', icon: 'TruckOutlined',
    children: [
      { key: 'freight-template.list', label: '运费模板', path: '/logistics/freight-templates', permission: 'freight-template:list' },
      { key: 'logistics-company.list', label: '物流公司', path: '/logistics/companies', permission: 'logistics-company:list' },
    ],
  },
  {
    key: '05-order-fulfillment', label: '订单履约', icon: 'ProfileOutlined',
    children: [
      { key: 'order.pending-shipment', label: '待发货', path: '/orders/pending-shipment', permission: 'order:list' },
      { key: 'order.list', label: '订单列表', path: '/orders', permission: 'order:list' },
    ],
  },
  {
    key: '06-after-sales', label: '售后处理', icon: 'CustomerServiceOutlined',
    children: [
      { key: 'aftersales.list', label: '售后列表', path: '/after-sales', permission: 'aftersales:list' },
    ],
  },
  {
    key: '07-review', label: '评价管理', icon: 'CommentOutlined',
    children: [
      { key: 'review.list', label: '评价回复', path: '/reviews', permission: 'review:list' },
    ],
  },
  {
    key: '01-onboarding', label: '店铺设置', icon: 'SettingOutlined',
    children: [
      { key: 'shop.application', label: '入驻申请', path: '/shop/application', permission: 'shop:application:submit' },
      { key: 'shop.profile', label: '店铺信息', path: '/shop/profile', permission: 'shop:profile:view' },
      { key: 'shop.qualifications', label: '资质管理', path: '/shop/qualifications', permission: 'shop:qualification:upload' },
    ],
  },
  {
    key: '09-export', label: '报表导出', icon: 'ExportOutlined',
    children: [
      { key: 'export.sales', label: '销售报表', path: '/export/sales', permission: 'export:sales' },
    ],
  },
  {
    key: '08-account', label: '个人中心', icon: 'UserOutlined',
    children: [
      { key: 'account.profile', label: '账号信息', path: '/account/profile', permission: 'account:profile:view' },
      { key: 'account.notifications', label: '消息通知', path: '/account/notifications', permission: 'notification:list' },
    ],
  },
]

const visibleGroups = computed(() => {
  return menuGroups
    .map(group => ({
      ...group,
      children: group.children.filter(child =>
        !child.permission || auth.hasPermission(child.permission),
      ),
    }))
    .filter(group => group.children.length > 0)
})

const selectedKeys = computed(() => {
  const matched = route.meta.menuKey
  return matched ? [matched as string] : []
})
const openKeys = computed(() => visibleGroups.value.map(g => g.key))

function onMenuClick({ key }: { key: string }) {
  for (const group of menuGroups) {
    const item = group.children.find(c => c.key === key)
    if (item) {
      router.push(item.path)
      return
    }
  }
}
</script>

<template>
  <div class="sider-logo" v-if="!collapsed">
    <h1>Leno 卖家</h1>
  </div>
  <div class="sider-logo-mini" v-else>
    <span>L</span>
  </div>
  <Menu
    mode="inline"
    theme="dark"
    :selected-keys="selectedKeys"
    :default-open-keys="openKeys"
    @click="onMenuClick"
  >
    <Menu.ItemGroup v-for="group in visibleGroups" :key="group.key" :title="group.label">
      <template #icon>
        <component :is="iconMap[group.icon]" />
      </template>
      <Menu.Item v-for="child in group.children" :key="child.key">
        {{ child.label }}
      </Menu.Item>
    </Menu.ItemGroup>
  </Menu>
</template>

<style scoped>
.sider-logo {
  height: 64px; display: flex; align-items: center; justify-content: center;
  background: #001529; color: #fff;
}
.sider-logo h1 { margin: 0; font-size: 18px; font-weight: 600; }
.sider-logo-mini {
  height: 64px; display: flex; align-items: center; justify-content: center;
  background: #001529; color: #fff; font-size: 24px; font-weight: 700;
}
</style>
