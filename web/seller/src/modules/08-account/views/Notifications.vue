<script setup lang="ts">
import { ref, computed, h } from 'vue'
import {
  Breadcrumb,
  BreadcrumbItem,
  Card,
  Tabs,
  TabPane,
  List,
  ListItem,
  ListItemMeta,
  Button,
  Alert,
  Tag,
  Space,
  message,
} from 'ant-design-vue'
import { BellOutlined, CheckOutlined } from '@ant-design/icons-vue'
import { EmptyState } from '@/shared/components'

/**
 * 消息通知页
 *
 * 路由 /account/notifications（P0 已注册），权限 notification:list
 * 后端通知端点待确认（BE-4），本页采用"仅 UI + BE-4 标记"策略：
 * 完整 UI 但不调用 API，展示空列表与 BE-4 提示。
 */

interface NotificationItem {
  id: string
  title: string
  content: string
  read: boolean
  createdAt: string
}

const activeTab = ref<'all' | 'unread' | 'read'>('all')
const notifications = ref<NotificationItem[]>([])

const filtered = computed(() => {
  if (activeTab.value === 'unread') return notifications.value.filter((n) => !n.read)
  if (activeTab.value === 'read') return notifications.value.filter((n) => n.read)
  return notifications.value
})

const unreadCount = computed(() => notifications.value.filter((n) => !n.read).length)

function onMarkAllRead(): void {
  // BE-4：后端未就绪，仅提示
  message.warning('后端通知接口未就绪（BE-4），暂无法标记已读')
}
</script>

<template>
  <div class="account-notifications-page">
    <Breadcrumb class="account-notifications-breadcrumb">
      <BreadcrumbItem>个人账号</BreadcrumbItem>
      <BreadcrumbItem>消息通知</BreadcrumbItem>
    </Breadcrumb>

    <Alert
      type="warning"
      show-icon
      message="后端通知接口未就绪（BE-4）"
      description="通知端点待后端确认，当前展示空列表占位。后端就绪后将自动接入真实数据。"
      class="account-notifications-alert"
    />

    <Card class="account-notifications-card" :bordered="true">
      <template #title>
        <Space>
          <BellOutlined />
          <span class="account-notifications-title">消息通知</span>
          <Tag v-if="unreadCount > 0" color="red">{{ unreadCount }} 未读</Tag>
        </Space>
      </template>
      <template #extra>
        <Button :icon="h(CheckOutlined)" size="small" @click="onMarkAllRead">
          全部标记已读
        </Button>
      </template>

      <Tabs v-model:active-key="activeTab">
        <TabPane key="all" tab="全部" />
        <TabPane key="unread" tab="未读" />
        <TabPane key="read" tab="已读" />
      </Tabs>

      <EmptyState
        v-if="filtered.length === 0"
        description="暂无通知"
      />
      <List v-else :data-source="filtered" item-layout="horizontal">
        <template #renderItem="{ item }">
          <ListItem>
            <ListItemMeta>
              <template #title>
                <Space>
                  <span>{{ item.title }}</span>
                  <Tag v-if="!item.read" color="red">未读</Tag>
                </Space>
              </template>
              <template #description>{{ item.content }}</template>
            </ListItemMeta>
          </ListItem>
        </template>
      </List>
    </Card>
  </div>
</template>

<style scoped>
.account-notifications-page {
  display: flex;
  flex-direction: column;
  gap: 16px;
}
.account-notifications-breadcrumb {
  font-size: 14px;
}
.account-notifications-alert {
  border-radius: 8px;
}
.account-notifications-card {
  border-radius: 8px;
}
.account-notifications-title {
  font-size: 15px;
  font-weight: 500;
}
</style>
