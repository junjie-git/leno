<script setup lang="ts">
import { ref, computed, onMounted } from 'vue'
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
  Tag,
  Space,
  Skeleton,
  message,
} from 'ant-design-vue'
import { BellOutlined, CheckOutlined } from '@ant-design/icons-vue'
import { h } from 'vue'
import { EmptyState } from '@/shared/components'
import { notificationApi } from '../api/notification.api'
import type { NotificationRecordDto } from '../types/notification.dto'
import { logger } from '@/shared/utils/logger'
import { formatDateTime } from '@/shared/utils/format'

/**
 * 消息通知页
 *
 * 路由 /account/notifications，权限 notification:list
 * 后端 4 端点已就绪（BE-4 清理），接入真实 API：
 * - 列表 GET /notifications?isRead=&page=&pageSize=
 * - 未读计数 GET /notifications/unread-count
 * - 批量标记已读 POST /notifications/read
 * - 全部标记已读 POST /notifications/read-all
 */

type TabKey = 'all' | 'unread' | 'read'

const activeTab = ref<TabKey>('all')
const loading = ref(true)
const submitting = ref(false)
const notifications = ref<NotificationRecordDto[]>([])
const unreadCount = ref(0)

const filtered = computed<NotificationRecordDto[]>(() => {
  if (activeTab.value === 'unread') return notifications.value.filter((n) => !n.isRead)
  if (activeTab.value === 'read') return notifications.value.filter((n) => n.isRead)
  return notifications.value
})

function isReadParam(tab: TabKey): boolean | undefined {
  if (tab === 'unread') return false
  if (tab === 'read') return true
  return undefined
}

async function loadList(): Promise<void> {
  loading.value = true
  try {
    const res = await notificationApi.list({
      isRead: isReadParam(activeTab.value),
      page: 1,
      pageSize: 50,
    })
    notifications.value = res.items
    unreadCount.value = res.unreadCount
  } catch (e) {
    logger.error('加载通知列表失败', e)
    message.error('加载通知列表失败')
  } finally {
    loading.value = false
  }
}

async function loadUnreadCount(): Promise<void> {
  try {
    unreadCount.value = await notificationApi.getUnreadCount()
  } catch (e) {
    logger.warn('获取未读计数失败', e)
  }
}

async function onMarkAllRead(): Promise<void> {
  submitting.value = true
  try {
    await notificationApi.markAllAsRead()
    message.success('已全部标记为已读')
    await loadList()
    await loadUnreadCount()
  } catch (e) {
    logger.error('标记全部已读失败', e)
    message.error('标记全部已读失败')
  } finally {
    submitting.value = false
  }
}

async function onMarkOneRead(item: NotificationRecordDto): Promise<void> {
  if (item.isRead) return
  try {
    await notificationApi.markAsRead([item.recordId])
    item.isRead = true
    unreadCount.value = Math.max(0, unreadCount.value - 1)
  } catch (e) {
    logger.error('标记已读失败', e)
    message.error('标记已读失败')
  }
}

onMounted(() => {
  void loadList()
  void loadUnreadCount()
})
</script>

<template>
  <div class="account-notifications-page">
    <Breadcrumb class="account-notifications-breadcrumb">
      <BreadcrumbItem>个人账号</BreadcrumbItem>
      <BreadcrumbItem>消息通知</BreadcrumbItem>
    </Breadcrumb>

    <Card class="account-notifications-card" :bordered="true">
      <template #title>
        <Space>
          <BellOutlined />
          <span class="account-notifications-title">消息通知</span>
          <Tag v-if="unreadCount > 0" color="red">{{ unreadCount }} 未读</Tag>
        </Space>
      </template>
      <template #extra>
        <Button
          :icon="h(CheckOutlined)"
          size="small"
          :loading="submitting"
          :disabled="unreadCount === 0"
          @click="onMarkAllRead"
        >
          全部标记已读
        </Button>
      </template>

      <Tabs v-model:active-key="activeTab" @change="loadList">
        <TabPane key="all" tab="全部" />
        <TabPane key="unread" tab="未读" />
        <TabPane key="read" tab="已读" />
      </Tabs>

      <Skeleton v-if="loading" active :paragraph="{ rows: 4 }" />
      <EmptyState v-else-if="filtered.length === 0" description="暂无通知" />
      <List v-else :data-source="filtered" item-layout="horizontal">
        <template #renderItem="{ item }">
          <ListItem>
            <ListItemMeta>
              <template #title>
                <Space>
                  <span>{{ item.title }}</span>
                  <Tag v-if="!item.isRead" color="red">未读</Tag>
                </Space>
              </template>
              <template #description>
                <div class="account-notifications-desc">
                  <span>{{ item.content }}</span>
                  <span class="account-notifications-time">{{ formatDateTime(item.createdAt) }}</span>
                </div>
              </template>
            </ListItemMeta>
            <template #actions>
              <Button
                v-if="!item.isRead"
                type="link"
                size="small"
                @click="onMarkOneRead(item as NotificationRecordDto)"
              >
                标记已读
              </Button>
            </template>
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
.account-notifications-card {
  border-radius: 8px;
}
.account-notifications-title {
  font-size: 15px;
  font-weight: 500;
}
.account-notifications-desc {
  display: flex;
  flex-direction: column;
  gap: 4px;
}
.account-notifications-time {
  font-size: 12px;
  color: #8c8c8c;
}
</style>
