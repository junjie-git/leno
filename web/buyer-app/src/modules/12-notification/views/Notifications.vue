<script setup lang="ts">
import { onMounted, ref } from 'vue'
import { useRouter } from 'vue-router'
import { showFailToast, showToast } from 'vant'
import { notificationApi } from '@/modules/12-notification/api/notification.api'
import type { NotificationDto, NotificationType } from '../types/notification.dto'
import EmptyState from '@/shared/components/EmptyState.vue'
import ErrorState from '@/shared/components/ErrorState.vue'
import { logger } from '@/shared/utils/logger'

/**
 * 消息中心页（/notifications）
 *
 * 结构（对齐设计稿 notifications）：
 * NavBar（返回 / 消息 / 右侧「全部已读」）→ 类型 Tab（全部 + dto 六种类型，带未读角标）
 * → van-pull-refresh + van-list 无限滚动的通知卡片
 * → 卡片（未读红点 + 类型图标 + 标题 + 时间 + 摘要 + 类型标签，未读标题加粗）
 *
 * 交互流：
 * - 点击未读卡片 → POST /notifications/read 乐观标记已读（失败回滚）→ 按 linkUrl 跳转业务页
 * - 「全部已读」→ POST /notifications/read-all → 刷新列表与未读角标
 * - 切换 Tab → 重置分页按 type 重新加载
 */

const router = useRouter()

/** 分页大小 */
const PAGE_SIZE = 20

/** 类型 Tab：全部 + NotificationType 枚举逐项 */
const TABS: Array<{ key: NotificationType | ''; label: string }> = [
  { key: '', label: '全部' },
  { key: 'Order', label: '订单' },
  { key: 'Logistics', label: '物流' },
  { key: 'Promotion', label: '优惠' },
  { key: 'Points', label: '积分' },
  { key: 'AfterSales', label: '售后' },
  { key: 'System', label: '系统' },
]

/** 通知类型 → 图标底色 / 标签配色（对齐设计稿类型配色） */
const TYPE_META: Record<NotificationType, { label: string; iconCls: string; tagCls: string }> = {
  Order: { label: '订单', iconCls: 'ic-order', tagCls: 'tg-order' },
  Logistics: { label: '物流', iconCls: 'ic-logistics', tagCls: 'tg-logistics' },
  Promotion: { label: '优惠', iconCls: 'ic-promo', tagCls: 'tg-promo' },
  Points: { label: '积分', iconCls: 'ic-points', tagCls: 'tg-points' },
  AfterSales: { label: '售后', iconCls: 'ic-aftersales', tagCls: 'tg-aftersales' },
  System: { label: '系统', iconCls: 'ic-system', tagCls: 'tg-system' },
}

/** 各类型未读数缓存（「全部」Tab 列表加载时重建） */
type TypeUnreadMap = Record<NotificationType, number>

function emptyTypeUnread(): TypeUnreadMap {
  return { Order: 0, Logistics: 0, Promotion: 0, Points: 0, AfterSales: 0, System: 0 }
}

// ---- 页面状态 ----
const activeTab = ref<NotificationType | ''>('')
const firstLoading = ref(true)
const notifications = ref<NotificationDto[]>([])
const page = ref(1)
const finished = ref(false)
const listLoading = ref(false)
const listError = ref(false)
const refreshing = ref(false)
const unreadTotal = ref(0)
const typeUnread = ref<TypeUnreadMap>(emptyTypeUnread())
const markingAll = ref(false)

/** 列表请求序号（切换 Tab 时旧响应作废） */
let listSeq = 0

onMounted(() => {
  void reload()
})

function typeMeta(item: NotificationDto): { label: string; iconCls: string; tagCls: string } {
  return TYPE_META[item.type]
}

/** Tab 角标未读数：全部 Tab 用服务端总数，类型 Tab 用缓存计数 */
function tabBadge(key: NotificationType | ''): number {
  if (key === '') {
    return unreadTotal.value
  }
  return typeUnread.value[key] ?? 0
}

/** 通知时间展示：今天 HH:mm / 今年 MM-DD / 跨年 YYYY-MM-DD */
function formatNotifyTime(iso: string): string {
  const d = new Date(iso)
  if (Number.isNaN(d.getTime())) {
    return iso
  }
  const now = new Date()
  const pad = (n: number) => String(n).padStart(2, '0')
  if (d.toDateString() === now.toDateString()) {
    return `${pad(d.getHours())}:${pad(d.getMinutes())}`
  }
  if (d.getFullYear() === now.getFullYear()) {
    return `${pad(d.getMonth() + 1)}-${pad(d.getDate())}`
  }
  return `${d.getFullYear()}-${pad(d.getMonth() + 1)}-${pad(d.getDate())}`
}

// ---- 未读角标 ----
async function refreshUnreadTotal(): Promise<void> {
  try {
    unreadTotal.value = await notificationApi.getUnreadCount()
  } catch (e) {
    logger.warn('未读数获取失败（忽略）', e)
  }
}

/** 由「全部」列表重建各类型未读缓存 */
function rebuildTypeUnread(items: NotificationDto[]): void {
  const counts = emptyTypeUnread()
  for (const n of items) {
    if (!n.isRead) {
      counts[n.type] += 1
    }
  }
  typeUnread.value = counts
}

// ---- 数据加载 ----
/** 重置分页并加载第一页 */
async function reload(): Promise<void> {
  const seq = ++listSeq
  page.value = 1
  finished.value = false
  listError.value = false
  firstLoading.value = true
  try {
    const result = await notificationApi.list({
      type: activeTab.value || undefined,
      page: 1,
      pageSize: PAGE_SIZE,
    })
    if (seq !== listSeq) {
      return
    }
    notifications.value = result.items
    if (result.items.length < PAGE_SIZE) {
      finished.value = true
    }
    if (activeTab.value === '') {
      rebuildTypeUnread(result.items)
    }
    void refreshUnreadTotal()
  } catch (e) {
    if (seq !== listSeq) {
      return
    }
    logger.error('通知列表加载失败', e)
    listError.value = true
  } finally {
    if (seq === listSeq) {
      firstLoading.value = false
      refreshing.value = false
    }
  }
}

/** van-list 无限加载 */
async function onLoad(): Promise<void> {
  if (finished.value || firstLoading.value) {
    return
  }
  const seq = listSeq
  listLoading.value = true
  listError.value = false
  try {
    const next = await notificationApi.list({
      type: activeTab.value || undefined,
      page: page.value + 1,
      pageSize: PAGE_SIZE,
    })
    if (seq !== listSeq) {
      return
    }
    notifications.value.push(...next.items)
    page.value += 1
    if (next.items.length < PAGE_SIZE) {
      finished.value = true
    }
    if (activeTab.value === '') {
      rebuildTypeUnread(notifications.value)
    }
  } catch (e) {
    if (seq !== listSeq) {
      return
    }
    logger.warn('通知列表翻页加载失败', e)
    listError.value = true
  } finally {
    if (seq === listSeq) {
      listLoading.value = false
    }
  }
}

/** 下拉刷新 */
async function onRefresh(): Promise<void> {
  await reload()
}

/** 切换类型 Tab */
function setTab(key: NotificationType | ''): void {
  if (activeTab.value === key) {
    return
  }
  activeTab.value = key
  void reload()
}

// ---- 通知操作 ----
/** 点击通知：未读先乐观标记已读（失败回滚），再按 linkUrl 跳转 */
async function openNotification(item: NotificationDto): Promise<void> {
  if (!item.isRead) {
    item.isRead = true
    unreadTotal.value = Math.max(0, unreadTotal.value - 1)
    typeUnread.value = {
      ...typeUnread.value,
      [item.type]: Math.max(0, (typeUnread.value[item.type] ?? 0) - 1),
    }
    try {
      await notificationApi.markRead([item.id])
    } catch (e) {
      logger.warn('标记已读失败（回滚）', e)
      item.isRead = false
      unreadTotal.value += 1
      typeUnread.value = {
        ...typeUnread.value,
        [item.type]: (typeUnread.value[item.type] ?? 0) + 1,
      }
      showFailToast('标记已读失败，请重试')
      return
    }
    void refreshUnreadTotal()
  }
  if (item.linkUrl && item.linkUrl.startsWith('/')) {
    router.push(item.linkUrl)
  }
}

/** 全部已读 */
async function markAllRead(): Promise<void> {
  if (markingAll.value) {
    return
  }
  if (unreadTotal.value === 0 && !notifications.value.some((n) => !n.isRead)) {
    showToast('暂无未读消息')
    return
  }
  markingAll.value = true
  try {
    await notificationApi.markAllRead()
    notifications.value.forEach((n) => {
      n.isRead = true
    })
    typeUnread.value = emptyTypeUnread()
    unreadTotal.value = 0
    showToast('已全部标记为已读')
  } catch (e) {
    logger.warn('全部已读失败', e)
    showFailToast('操作失败，请稍后重试')
  } finally {
    markingAll.value = false
  }
}

// ---- 跳转 ----
function goPreferences(): void {
  router.push('/notifications/preferences')
}

function goHome(): void {
  router.replace('/')
}

function goBack(): void {
  if (window.history.length > 1) {
    router.back()
  } else {
    router.replace('/')
  }
}
</script>

<template>
  <div class="noti-page">
    <!-- NavBar -->
    <header class="navbar">
      <button class="nav-back" type="button" aria-label="返回" @click="goBack">
        <svg width="20" height="20" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round">
          <path d="M15 18l-6-6 6-6" />
        </svg>
      </button>
      <div class="nav-title">消息</div>
      <div class="nav-right">
        <button class="text-btn read-all" type="button" :disabled="markingAll" @click="markAllRead">
          {{ markingAll ? '处理中...' : '全部已读' }}
        </button>
        <button class="icon-btn" type="button" aria-label="通知偏好设置" @click="goPreferences">
          <svg width="20" height="20" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.8" stroke-linecap="round" stroke-linejoin="round">
            <circle cx="12" cy="12" r="3" />
            <path d="M19.4 15a1.65 1.65 0 0 0 .33 1.82l.06.06a2 2 0 1 1-2.83 2.83l-.06-.06a1.65 1.65 0 0 0-1.82-.33 1.65 1.65 0 0 0-1 1.51V21a2 2 0 1 1-4 0v-.09a1.65 1.65 0 0 0-1-1.51 1.65 1.65 0 0 0-1.82.33l-.06.06a2 2 0 1 1-2.83-2.83l.06-.06a1.65 1.65 0 0 0 .33-1.82 1.65 1.65 0 0 0-1.51-1H3a2 2 0 1 1 0-4h.09a1.65 1.65 0 0 0 1.51-1 1.65 1.65 0 0 0-.33-1.82l-.06-.06a2 2 0 1 1 2.83-2.83l.06.06a1.65 1.65 0 0 0 1.82.33h.01a1.65 1.65 0 0 0 1-1.51V3a2 2 0 1 1 4 0v.09a1.65 1.65 0 0 0 1 1.51h.01a1.65 1.65 0 0 0 1.82-.33l.06-.06a2 2 0 1 1 2.83 2.83l-.06.06a1.65 1.65 0 0 0-.33 1.82v.01a1.65 1.65 0 0 0 1.51 1H21a2 2 0 1 1 0 4h-.09a1.65 1.65 0 0 0-1.51 1z" />
          </svg>
        </button>
      </div>
    </header>

    <!-- 类型 Tab -->
    <nav class="tabs" role="tablist" aria-label="通知类型筛选">
      <div
        v-for="tab in TABS"
        :key="tab.key || 'all'"
        class="tab"
        :class="{ active: activeTab === tab.key }"
        role="tab"
        :aria-selected="activeTab === tab.key"
        @click="setTab(tab.key)"
      >
        <span class="tab-label">
          {{ tab.label }}
          <span v-if="tabBadge(tab.key) > 0" class="tab-badge" aria-label="未读">
            {{ tabBadge(tab.key) > 99 ? '99+' : tabBadge(tab.key) }}
          </span>
        </span>
      </div>
    </nav>

    <!-- 列表区 -->
    <div class="list-wrap">
      <!-- 首屏骨架 -->
      <div v-if="firstLoading" class="skeleton-list">
        <div v-for="i in 5" :key="i" class="sk-card">
          <div class="skeleton-block sk-icon" />
          <div class="sk-lines">
            <div class="skeleton-block sk-l1" />
            <div class="skeleton-block sk-l2" />
          </div>
        </div>
      </div>

      <!-- 错误态 -->
      <ErrorState
        v-else-if="listError && notifications.length === 0"
        title="消息加载失败"
        description="网络不给力，请检查网络后重试"
        @retry="reload"
      />

      <!-- 空态 -->
      <EmptyState
        v-else-if="notifications.length === 0"
        :title="activeTab === '' ? '暂无消息' : '暂无相关消息'"
        action-text="去逛逛"
        @action="goHome"
      />

      <!-- 通知列表 -->
      <van-pull-refresh v-else v-model="refreshing" success-text="刷新成功" @refresh="onRefresh">
        <van-list
          v-model:loading="listLoading"
          :finished="finished"
          :error="listError"
          error-text="加载失败，点击重试"
          finished-text="没有更多了"
          loading-text="加载中..."
          @load="onLoad"
        >
          <article
            v-for="item in notifications"
            :key="item.id"
            class="noti-card"
            :class="item.isRead ? 'read' : 'unread'"
            role="article"
            :aria-label="`${typeMeta(item).label}通知：${item.title}`"
            @click="openNotification(item)"
          >
            <span v-if="!item.isRead" class="unread-dot" aria-label="未读" />
            <span class="noti-icon" :class="typeMeta(item).iconCls">
              <!-- 订单：购物车 -->
              <svg v-if="item.type === 'Order'" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round">
                <circle cx="9" cy="21" r="1" />
                <circle cx="20" cy="21" r="1" />
                <path d="M1 1h4l2.68 13.39a2 2 0 0 0 2 1.61h9.72a2 2 0 0 0 2-1.61L23 6H6" />
              </svg>
              <!-- 物流：卡车 -->
              <svg v-else-if="item.type === 'Logistics'" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round">
                <rect x="1" y="5" width="13" height="11" rx="1" />
                <path d="M14 9h4l4 4v3h-8z" />
                <circle cx="6" cy="18.5" r="1.8" />
                <circle cx="17.5" cy="18.5" r="1.8" />
              </svg>
              <!-- 优惠：礼盒 -->
              <svg v-else-if="item.type === 'Promotion'" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round">
                <polyline points="20 12 20 22 4 22 4 12" />
                <rect x="2" y="7" width="20" height="5" />
                <line x1="12" y1="22" x2="12" y2="7" />
                <path d="M12 7H7.5a2.5 2.5 0 0 1 0-5C11 2 12 7 12 7z" />
                <path d="M12 7h4.5a2.5 2.5 0 0 0 0-5C13 2 12 7 12 7z" />
              </svg>
              <!-- 积分：金币 -->
              <svg v-else-if="item.type === 'Points'" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round">
                <circle cx="12" cy="12" r="9" />
                <path d="M9 8l3 4 3-4M12 12v5M9.5 13.5h5M9.5 15.5h5" />
              </svg>
              <!-- 售后：退款 -->
              <svg v-else-if="item.type === 'AfterSales'" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round">
                <polyline points="1 4 1 10 7 10" />
                <path d="M3.51 15a9 9 0 1 0 2.13-9.36L1 10" />
              </svg>
              <!-- 系统：铃铛 -->
              <svg v-else viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round">
                <path d="M18 8A6 6 0 0 0 6 8c0 7-3 9-3 9h18s-3-2-3-9" />
                <path d="M13.73 21a2 2 0 0 1-3.46 0" />
              </svg>
            </span>
            <div class="noti-body">
              <div class="noti-title-row">
                <span class="noti-title">{{ item.title }}</span>
                <span class="noti-time">{{ formatNotifyTime(item.createdAt) }}</span>
              </div>
              <p class="noti-summary">{{ item.content }}</p>
              <div class="noti-meta">
                <span class="noti-tag" :class="typeMeta(item).tagCls">{{ typeMeta(item).label }}</span>
              </div>
            </div>
            <span v-if="item.linkUrl && item.linkUrl.startsWith('/')" class="noti-arrow">
              <svg width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round">
                <polyline points="9 18 15 12 9 6" />
              </svg>
            </span>
          </article>
        </van-list>
      </van-pull-refresh>
    </div>
  </div>
</template>

<style scoped>
.noti-page {
  height: 100%;
  display: flex;
  flex-direction: column;
  background: var(--n3);
}

/* NavBar */
.navbar {
  height: 46px;
  background: var(--n1);
  border-bottom: 1px solid var(--n3);
  display: flex;
  align-items: center;
  justify-content: space-between;
  padding: 0 var(--s3);
  flex-shrink: 0;
}

.nav-back {
  display: flex;
  align-items: center;
  justify-content: center;
  width: 32px;
  height: 32px;
  color: var(--n10);
  background: none;
  border: none;
  padding: 0;
  cursor: pointer;
}

.nav-title {
  font-size: var(--fs-lg);
  font-weight: var(--fw-medium);
  color: var(--n10);
}

.nav-right {
  display: flex;
  align-items: center;
  gap: var(--s1);
}

.text-btn {
  border: none;
  background: transparent;
  font-size: var(--fs-base);
  color: var(--c-primary);
  cursor: pointer;
  padding: 6px var(--s1);
  font-family: inherit;
}

.text-btn:active {
  opacity: 0.6;
}

.text-btn:disabled {
  color: var(--n7);
  cursor: not-allowed;
}

.icon-btn {
  width: 32px;
  height: 32px;
  display: flex;
  align-items: center;
  justify-content: center;
  border: none;
  background: transparent;
  cursor: pointer;
  color: var(--n10);
  padding: 0;
}

.icon-btn:active {
  opacity: 0.6;
}

/* 类型 Tab */
.tabs {
  background: var(--n1);
  display: flex;
  height: 44px;
  border-bottom: 1px solid var(--n3);
  overflow-x: auto;
  -webkit-overflow-scrolling: touch;
  flex-shrink: 0;
  padding: 0 var(--s1);
}

.tabs::-webkit-scrollbar {
  display: none;
}

.tab {
  flex: 1 0 auto;
  min-width: 58px;
  display: flex;
  align-items: center;
  justify-content: center;
  font-size: var(--fs-base);
  color: var(--n9);
  position: relative;
  cursor: pointer;
  white-space: nowrap;
  padding: 0 var(--s2);
  transition: color var(--d-mid) var(--ease-std);
}

.tab.active {
  color: var(--c-primary);
  font-weight: var(--fw-medium);
}

.tab.active::after {
  content: "";
  position: absolute;
  bottom: 0;
  left: 50%;
  transform: translateX(-50%);
  width: 20px;
  height: 2px;
  background: var(--c-primary);
  border-radius: 1px;
}

.tab-label {
  display: inline-flex;
  align-items: center;
  gap: 4px;
}

.tab-badge {
  min-width: 16px;
  height: 16px;
  padding: 0 4px;
  border-radius: 8px;
  background: var(--c-error);
  color: #fff;
  font-size: 10px;
  line-height: 16px;
  text-align: center;
  font-weight: var(--fw-medium);
}

/* 列表区 */
.list-wrap {
  flex: 1;
  overflow-y: auto;
  padding: var(--s3);
  padding-bottom: calc(var(--s6) + env(safe-area-inset-bottom));
  background: var(--n3);
}

/* 骨架屏 */
.skeleton-list {
  display: flex;
  flex-direction: column;
  gap: var(--s2);
}

.sk-card {
  background: var(--n1);
  border-radius: var(--r-lg);
  box-shadow: var(--sh-card);
  padding: var(--s3);
  display: flex;
  gap: var(--s3);
}

.sk-icon {
  width: 40px;
  height: 40px;
  border-radius: 50%;
  flex-shrink: 0;
}

.sk-lines {
  flex: 1;
  display: flex;
  flex-direction: column;
  gap: var(--s2);
  padding-top: 2px;
}

.sk-l1 {
  width: 80%;
  height: 14px;
}

.sk-l2 {
  width: 50%;
  height: 12px;
}

/* 通知卡片 */
.noti-card {
  background: var(--n1);
  border-radius: var(--r-lg);
  box-shadow: var(--sh-card);
  padding: var(--s3);
  display: flex;
  gap: var(--s3);
  position: relative;
  cursor: pointer;
  margin-bottom: var(--s2);
  transition: background var(--d-fast) var(--ease-std);
}

.noti-card:active {
  background: var(--n2);
}

.noti-card.unread::before {
  content: "";
  position: absolute;
  left: 6px;
  top: 14px;
  width: 8px;
  height: 8px;
  border-radius: 50%;
  background: var(--c-error);
  box-shadow: 0 0 0 2px var(--n1);
}

.noti-icon {
  flex-shrink: 0;
  width: 40px;
  height: 40px;
  border-radius: 50%;
  display: flex;
  align-items: center;
  justify-content: center;
}

.noti-icon svg {
  width: 22px;
  height: 22px;
}

.ic-order {
  background: #e6f0ff;
  color: var(--c-primary);
}

.ic-logistics {
  background: rgba(82, 196, 26, 0.12);
  color: var(--c-success);
}

.ic-promo {
  background: #fff7e6;
  color: var(--c-warning);
}

.ic-points {
  background: rgba(114, 46, 209, 0.1);
  color: var(--c-buyer);
}

.ic-aftersales {
  background: #fff1f0;
  color: var(--c-error);
}

.ic-system {
  background: #f0f0f0;
  color: var(--n7);
}

.noti-body {
  flex: 1;
  min-width: 0;
}

.noti-title-row {
  display: flex;
  align-items: center;
  justify-content: space-between;
  gap: var(--s2);
}

.noti-title {
  font-size: var(--fs-base);
  color: var(--n10);
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
}

.noti-card.unread .noti-title {
  font-weight: var(--fw-semibold);
}

.noti-card.read .noti-title {
  color: var(--n7);
}

.noti-time {
  font-size: var(--fs-sm);
  color: var(--n7);
  flex-shrink: 0;
}

.noti-summary {
  font-size: 13px;
  color: var(--n9);
  margin-top: var(--s1);
  line-height: 1.5;
  display: -webkit-box;
  -webkit-line-clamp: 2;
  -webkit-box-orient: vertical;
  overflow: hidden;
}

.noti-card.read .noti-summary {
  color: var(--n7);
}

.noti-meta {
  display: flex;
  align-items: center;
  gap: 6px;
  margin-top: 6px;
}

.noti-tag {
  font-size: 11px;
  padding: 1px 6px;
  border-radius: var(--r-base);
  font-weight: var(--fw-medium);
}

.tg-order {
  background: #e6f0ff;
  color: var(--c-primary);
}

.tg-logistics {
  background: rgba(82, 196, 26, 0.12);
  color: var(--c-success);
}

.tg-promo {
  background: #fff7e6;
  color: var(--c-warning);
}

.tg-points {
  background: rgba(114, 46, 209, 0.1);
  color: var(--c-buyer);
}

.tg-aftersales {
  background: #fff1f0;
  color: var(--c-error);
}

.tg-system {
  background: #f0f0f0;
  color: var(--n7);
}

.noti-arrow {
  color: var(--n5);
  display: flex;
  align-items: center;
  flex-shrink: 0;
}
</style>
