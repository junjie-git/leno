<script setup lang="ts">
import { computed, ref } from 'vue'
import { LayoutSider, Menu } from 'ant-design-vue'
import type { RouteRecordRaw } from 'vue-router'
import { useRoute, useRouter } from 'vue-router'
import { useAuthStore } from '@/shared/auth'

/**
 * 侧栏菜单组件
 *
 * - 从 router.options.routes 读取所有带 menuGroup 的子路由
 * - 按 menuGroup 分组渲染
 * - 通过 useAuthStore.hasRole 控制可见
 * - 当前路由通过 menuKey 匹配高亮
 */

interface RouteMeta {
  title?: string
  menuKey?: string
  icon?: string
  roles?: string[]
  menuGroup?: string
}

const props = defineProps<{
  /** 是否折叠 */
  collapsed?: boolean
}>()

const router = useRouter()
const route = useRoute()
const auth = useAuthStore()

// 菜单组显示名映射（运营后台 10 个一级菜单）
const GROUP_TITLES: Record<string, string> = {
  '01-dashboard': '数据看板',
  '02-product-ops': '商品运营',
  '03-promotion-ops': '促销运营',
  '04-seller-ops': '卖家运营',
  '05-order-ops': '订单运营',
  '06-payment-ops': '支付运营',
  '07-notification-ops': '通知运营',
  '08-membership-ops': '会员运营',
  '09-account': '个人中心',
  '10-data-export': '数据导出',
}

// 菜单组排序
const GROUP_ORDER = [
  '01-dashboard',
  '02-product-ops',
  '03-promotion-ops',
  '04-seller-ops',
  '05-order-ops',
  '06-payment-ops',
  '07-notification-ops',
  '08-membership-ops',
  '09-account',
  '10-data-export',
]

interface MenuItem {
  key: string
  title: string
  icon?: string
  path: string
  roles?: string[]
}

const groupedMenus = computed<Record<string, MenuItem[]>>(() => {
  const result: Record<string, MenuItem[]> = {}
  // 从 router 的根路由 '/' 的 children 中读取
  const rootRoute = router.options.routes.find((r) => r.path === '/')
  const children: RouteRecordRaw[] = rootRoute?.children ?? []
  for (const child of children) {
    const meta = (child.meta ?? {}) as RouteMeta
    if (!meta.menuGroup || !meta.menuKey || !meta.title) continue
    // 角色过滤
    if (meta.roles && meta.roles.length > 0 && !auth.hasRole(meta.roles)) continue
    if (!result[meta.menuGroup]) result[meta.menuGroup] = []
    result[meta.menuGroup].push({
      key: meta.menuKey,
      title: meta.title,
      icon: meta.icon,
      // 兼容相对路径（'product-ops/x'）与绝对路径（'/promotion-ops/x'）两种定义
      path: child.path.startsWith('/') ? child.path : `/${child.path}`,
      roles: meta.roles,
    })
  }
  return result
})

const orderedGroups = computed(() => {
  return GROUP_ORDER.filter((g) => groupedMenus.value[g]?.length > 0).map((g) => ({
    key: g,
    title: GROUP_TITLES[g] ?? g,
    items: groupedMenus.value[g],
  }))
})

const selectedKeys = ref<string[]>([])
function updateSelected() {
  const meta = route.meta as RouteMeta
  selectedKeys.value = meta.menuKey ? [meta.menuKey] : []
}
updateSelected()
router.afterEach(() => updateSelected())

function onMenuClick({ key }: { key: string }) {
  // 在所有 group 中查找匹配项
  for (const group of orderedGroups.value) {
    const found = group.items.find((i) => i.key === key)
    if (found) {
      void router.push(found.path)
      return
    }
  }
}
</script>

<template>
  <LayoutSider
    :collapsed="props.collapsed"
    :collapsed-width="80"
    :width="200"
    :trigger="null"
    collapsible
    class="sider-menu"
  >
    <Menu
      mode="inline"
      theme="dark"
      :selected-keys="selectedKeys"
      :open-keys="orderedGroups.map((g) => g.key)"
      @click="onMenuClick"
    >
      <template v-for="group in orderedGroups" :key="group.key">
        <Menu.ItemGroup :title="group.title">
          <Menu.Item v-for="item in group.items" :key="item.key">
            <span>{{ item.title }}</span>
          </Menu.Item>
        </Menu.ItemGroup>
      </template>
    </Menu>
  </LayoutSider>
</template>

<style scoped>
.sider-menu {
  position: fixed;
  left: 0;
  top: 64px;
  bottom: 0;
  z-index: 10;
}
</style>
