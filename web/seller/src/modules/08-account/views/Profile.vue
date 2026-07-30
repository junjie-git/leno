<script setup lang="ts">
import { computed, onMounted } from 'vue'
import {
  Breadcrumb,
  BreadcrumbItem,
  Card,
  Descriptions,
  DescriptionsItem,
  Tag,
  Avatar,
  Skeleton,
} from 'ant-design-vue'
import { UserOutlined } from '@ant-design/icons-vue'
import { useAuthStore } from '@/shared/auth/auth.store'
import { useShopStore } from '@/shared/shop'
import { StatusTag } from '@/shared/components'

/**
 * 个人资料页（只读）
 *
 * 路由 /account/profile（P0 已注册），权限 account:profile:view
 * 数据来自 authStore.user + shopStore，无新增 API。
 * onMounted 主动刷新 profile + 店铺信息以保证最新。
 */

const authStore = useAuthStore()
const shopStore = useShopStore()

const loading = computed(() => !authStore.user)
const user = computed(() => authStore.user)
const roles = computed(() => authStore.roles)
const permissions = computed(() => authStore.permissions)

onMounted(async () => {
  try {
    await authStore.fetchProfile()
    await shopStore.fetchMyShop()
  } catch {
    // fetchProfile 失败由路由守卫/拦截器统一处理，此处静默
  }
})
</script>

<template>
  <div class="account-profile-page">
    <Breadcrumb class="account-profile-breadcrumb">
      <BreadcrumbItem>个人账号</BreadcrumbItem>
      <BreadcrumbItem>账号信息</BreadcrumbItem>
    </Breadcrumb>

    <Skeleton v-if="loading" active :paragraph="{ rows: 6 }" />
    <div v-else class="account-profile-body">
      <!-- 基本信息 -->
      <Card class="account-profile-card" :bordered="true">
        <template #title>
          <span class="account-profile-title">基本信息</span>
        </template>
        <div class="account-profile-user">
          <Avatar :size="64" :src="user?.avatar || undefined">
            <UserOutlined v-if="!user?.avatar" />
          </Avatar>
          <Descriptions :column="2" bordered size="middle">
            <DescriptionsItem label="用户名">{{ user?.username || '—' }}</DescriptionsItem>
            <DescriptionsItem label="昵称">{{ user?.nickname || '—' }}</DescriptionsItem>
            <DescriptionsItem label="邮箱">{{ user?.email || '—' }}</DescriptionsItem>
            <DescriptionsItem label="手机号">{{ user?.phone || '—' }}</DescriptionsItem>
            <DescriptionsItem label="角色" :span="2">
              <Tag v-for="r in roles" :key="r" color="blue">{{ r }}</Tag>
              <span v-if="roles.length === 0">—</span>
            </DescriptionsItem>
          </Descriptions>
        </div>
      </Card>

      <!-- 店铺信息 -->
      <Card class="account-profile-card" :bordered="true">
        <template #title>
          <span class="account-profile-title">店铺信息</span>
        </template>
        <Descriptions :column="2" bordered size="middle">
          <DescriptionsItem label="店铺名称">
            {{ shopStore.shopName || user?.shopName || '—' }}
          </DescriptionsItem>
          <DescriptionsItem label="店铺状态">
            <StatusTag
              v-if="shopStore.shopStatus || user?.shopStatus"
              type="shop"
              :status="(shopStore.shopStatus || user?.shopStatus) as string"
            />
            <span v-else>—</span>
          </DescriptionsItem>
        </Descriptions>
      </Card>

      <!-- 权限信息 -->
      <Card class="account-profile-card" :bordered="true">
        <template #title>
          <span class="account-profile-title">权限信息</span>
        </template>
        <div class="account-profile-perms">
          <Tag v-for="p in permissions" :key="p" color="geekblue">{{ p }}</Tag>
          <span v-if="permissions.length === 0" class="account-profile-empty">暂无权限</span>
        </div>
      </Card>
    </div>
  </div>
</template>

<style scoped>
.account-profile-page {
  display: flex;
  flex-direction: column;
  gap: 16px;
}
.account-profile-breadcrumb {
  font-size: 14px;
}
.account-profile-body {
  display: flex;
  flex-direction: column;
  gap: 16px;
}
.account-profile-card {
  border-radius: 8px;
}
.account-profile-title {
  font-size: 15px;
  font-weight: 500;
}
.account-profile-user {
  display: flex;
  align-items: flex-start;
  gap: 24px;
}
.account-profile-user :deep(.ant-descriptions) {
  flex: 1;
}
.account-profile-perms {
  display: flex;
  flex-wrap: wrap;
  gap: 8px;
}
.account-profile-empty {
  color: #8c8c8c;
  font-size: 13px;
}
</style>
