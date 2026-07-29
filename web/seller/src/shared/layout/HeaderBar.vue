<script setup lang="ts">
import { ref } from 'vue'
import { Button, Avatar, Dropdown, Space, Tooltip, Badge, Menu as DropdownMenu } from 'ant-design-vue'
import {
  MenuFoldOutlined, MenuUnfoldOutlined,
  BellOutlined, UserOutlined, LogoutOutlined,
} from '@ant-design/icons-vue'
import { useRouter } from 'vue-router'
import { useAuthStore } from '@/shared/auth'
import { useShopStore } from '@/shared/shop'
import { TodoBadge, StatusTag } from '@/shared/components'

defineProps<{ collapsed: boolean }>()
const emit = defineEmits<{ toggle: [] }>()

const router = useRouter()
const auth = useAuthStore()
const shop = useShopStore()

// 待办徽标计数：dashboard 模块上线前先以 0 作为默认值，
// 待 dashboard store 就绪后切换为实际接口数据。
const pendingShipmentCount = ref(0)
const afterSalesPendingCount = ref(0)

function goToPendingShipment() {
  router.push('/orders/pending-shipment')
}
function goToAfterSales() {
  router.push('/after-sales?status=Pending')
}
function goToNotifications() {
  router.push('/account/notifications')
}
function goToProfile() {
  router.push('/account/profile')
}
async function onLogout() {
  await auth.logout()
  router.push('/login')
}
</script>

<template>
  <div class="header-bar">
    <Button type="text" @click="emit('toggle')">
      <MenuUnfoldOutlined v-if="collapsed" />
      <MenuFoldOutlined v-else />
    </Button>

    <Space class="shop-info" v-if="shop.shopName">
      <span class="shop-name">{{ shop.shopName }}</span>
      <StatusTag v-if="shop.shopStatus" type="shop" :status="shop.shopStatus" />
    </Space>

    <div class="header-right">
      <TodoBadge :count="pendingShipmentCount" label="待发货" @click="goToPendingShipment" />
      <TodoBadge :count="afterSalesPendingCount" label="售后" @click="goToAfterSales" />

      <Tooltip title="消息通知">
        <Badge :count="0" :offset="[-2, 4]">
          <Button type="text" shape="circle" @click="goToNotifications">
            <BellOutlined />
          </Button>
        </Badge>
      </Tooltip>

      <Dropdown>
        <Space class="user-info">
          <Avatar :size="32">
            <UserOutlined v-if="!auth.user?.avatar" />
            <img v-else :src="auth.user.avatar" alt="avatar" />
          </Avatar>
          <span>{{ auth.user?.nickname || auth.user?.username }}</span>
        </Space>
        <template #overlay>
          <DropdownMenu>
            <DropdownMenu.Item key="profile" @click="goToProfile">
              <UserOutlined /> 账号信息
            </DropdownMenu.Item>
            <DropdownMenu.Divider />
            <DropdownMenu.Item key="logout" @click="onLogout">
              <LogoutOutlined /> 退出登录
            </DropdownMenu.Item>
          </DropdownMenu>
        </template>
      </Dropdown>
    </div>
  </div>
</template>

<style scoped>
.header-bar { display: flex; align-items: center; width: 100%; height: 100%; }
.shop-info { margin-left: 16px; }
.shop-name { font-weight: 500; font-size: 14px; }
.header-right { margin-left: auto; display: flex; align-items: center; gap: 16px; }
.user-info { cursor: pointer; }
</style>
