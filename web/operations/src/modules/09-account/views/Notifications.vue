<script setup lang="ts">
import { computed, onMounted, reactive, ref } from 'vue'
import { useRouter } from 'vue-router'
import { message } from 'ant-design-vue'
import { ReloadOutlined, CheckOutlined, BellOutlined } from '@ant-design/icons-vue'
import { notificationApi } from '../api/notification.api'
import type {
  NotificationRecordDto,
  NotificationType,
} from '../types/account.dto'
import { IdempotencyButton, EmptyState, ConfirmDialog } from '@/shared/components'
import { formatDateTime } from '@/shared/utils/format'

/**
 * 通知中心页（09-account）
 *
 * - 筛选条：已读状态（全部/未读/已读）+ 通知类型（系统/业务/审核）
 * - 工具栏：全部标记已读（二次确认）/ 批量标记已读（勾选）/ 未读徽标 / 刷新
 * - 列表：未读红点置顶（isRead 升序 + 时间倒序），点击单条打开详情抽屉并自动标记已读
 * - 详情抽屉：全文 + 来源 + 时间 + 关联业务跳转按钮（businessRef 站内路径校验）
 * - 三态：loading（a-spin）/ empty（EmptyState）/ error（加载失败重试）
 */

const router = useRouter()

/* ============================== 类型映射 ============================== */

interface TypeMeta {
  label: string
  color: string
}

const typeMetaMap: Record<NotificationType, TypeMeta> = {
  System: { label: '系统', color: 'blue' },
  Business: { label: '业务', color: 'green' },
  Audit: { label: '审核', color: 'orange' },
}

function typeMeta(type: NotificationType): TypeMeta {
  return typeMetaMap[type] ?? { label: type, color: 'default' }
}

/* ============================== 筛选 ============================== */

interface FilterState {
  /** undefined = 全部 */
  isRead: boolean | undefined
  type: NotificationType | undefined
}

const filters = reactive<FilterState>({
  isRead: undefined,
  type: undefined,
})

const readStatusOptions = [
  { label: '全部', value: 'all' },
  { label: '未读', value: 'unread' },
  { label: '已读', value: 'read' },
]

/** 下拉绑定值（all/unread/read），同步到 filters.isRead */
const readStatusValue = ref<string>('all')

function onReadStatusChange(value: string) {
  readStatusValue.value = value
  filters.isRead = value === 'unread' ? false : value === 'read' ? true : undefined
}

const typeOptions = [
  { label: '全部类型', value: 'all' },
  { label: '系统', value: 'System' },
  { label: '业务', value: 'Business' },
  { label: '审核', value: 'Audit' },
]

/** 类型下拉绑定值（all/枚举），同步到 filters.type */
const typeValue = ref<string>('all')

function onTypeChange(value: string) {
  typeValue.value = value
  filters.type = value === 'all' ? undefined : (value as NotificationType)
}

/* ============================== 列表 ============================== */

const records = ref<NotificationRecordDto[]>([])
const loading = ref(false)
const loadError = ref(false)
const unreadCount = ref(0)
const pagination = reactive({
  current: 1,
  pageSize: 20,
  total: 0,
  showSizeChanger: true,
  showTotal: (total: number) => `共 ${total} 条`,
})

/** 未读置顶：isRead 升序（未读在前）→ createdAt 倒序（新在前） */
const sortedRecords = computed(() =>
  [...records.value].sort((a, b) => {
    if (a.isRead !== b.isRead) return a.isRead ? 1 : -1
    return b.createdAt.localeCompare(a.createdAt)
  }),
)

async function fetchList() {
  loading.value = true
  loadError.value = false
  try {
    const result = await notificationApi.list({
      page: pagination.current,
      pageSize: pagination.pageSize,
      isRead: filters.isRead,
      type: filters.type,
    })
    records.value = result.items
    pagination.total = result.total
    unreadCount.value = result.unreadCount
  } catch {
    loadError.value = true
    message.error('加载通知列表失败')
  } finally {
    loading.value = false
  }
}

function onQuery() {
  pagination.current = 1
  fetchList()
}

function onReset() {
  readStatusValue.value = 'all'
  typeValue.value = 'all'
  filters.isRead = undefined
  filters.type = undefined
  onQuery()
}

function onPageChange(page: number, pageSize: number) {
  pagination.current = page
  pagination.pageSize = pageSize
  fetchList()
}

/* ============================== 勾选与批量已读 ============================== */

const selectedIds = ref<string[]>([])

function isSelected(record: NotificationRecordDto): boolean {
  return selectedIds.value.includes(record.id)
}

function toggleSelect(record: NotificationRecordDto) {
  if (isSelected(record)) {
    selectedIds.value = selectedIds.value.filter((id) => id !== record.id)
  } else {
    selectedIds.value = [...selectedIds.value, record.id]
  }
}

/** 选中项中的未读记录 id（已读的无需重复标记） */
const selectedUnreadIds = computed(() =>
  selectedIds.value.filter((id) => {
    const record = records.value.find((r) => r.id === id)
    return record ? !record.isRead : false
  }),
)

const batchMarking = ref(false)

async function onBatchMarkRead() {
  if (selectedUnreadIds.value.length === 0) {
    message.info('选中项均已是已读状态')
    return
  }
  batchMarking.value = true
  try {
    await notificationApi.markAsRead({ recordIds: selectedUnreadIds.value })
    message.success(`已批量标记 ${selectedUnreadIds.value.length} 条已读`)
    selectedIds.value = []
    await fetchList()
  } catch {
    message.error('操作失败，请重试')
  } finally {
    batchMarking.value = false
  }
}

/* ============================== 全部已读 ============================== */

const markAllConfirmOpen = ref(false)
const markAllLoading = ref(false)

function onMarkAllRead() {
  if (unreadCount.value === 0) {
    message.info('当前没有未读通知')
    return
  }
  markAllConfirmOpen.value = true
}

async function onConfirmMarkAll() {
  markAllConfirmOpen.value = false
  markAllLoading.value = true
  try {
    await notificationApi.markAllAsRead()
    message.success('已全部标记为已读')
    selectedIds.value = []
    await fetchList()
  } catch {
    message.error('操作失败，请重试')
  } finally {
    markAllLoading.value = false
  }
}

/* ============================== 单条已读与详情抽屉 ============================== */

const detailOpen = ref(false)
const detail = ref<NotificationRecordDto | null>(null)
const marking = ref(false)

/** 本地将记录置为已读并同步未读计数 */
function markLocalRead(record: NotificationRecordDto) {
  if (record.isRead) return
  record.isRead = true
  unreadCount.value = Math.max(0, unreadCount.value - 1)
}

async function markRead(record: NotificationRecordDto) {
  if (record.isRead) return
  marking.value = true
  try {
    await notificationApi.markAsRead({ recordIds: [record.id] })
    markLocalRead(record)
    message.success('已标记为已读')
  } catch {
    message.error('操作失败，请重试')
  } finally {
    marking.value = false
  }
}

/** 点击单条：打开抽屉 + 自动标记已读 */
async function openDetail(record: NotificationRecordDto) {
  detail.value = record
  detailOpen.value = true
  if (!record.isRead) {
    try {
      await notificationApi.markAsRead({ recordIds: [record.id] })
      markLocalRead(record)
    } catch {
      // 静默失败：不打断详情查看，下次刷新会重新同步
    }
  }
}

/** 关联业务跳转：仅接受站内路径，防开放重定向 */
function goBusiness() {
  const ref = detail.value?.businessRef
  if (!ref) return
  if (ref.startsWith('/') && !ref.startsWith('//')) {
    detailOpen.value = false
    void router.push(ref)
  } else {
    message.warning('关联业务链接无效')
  }
}

const businessLinkText = computed(() => {
  switch (detail.value?.type) {
    case 'Audit':
      return '前往处理审核 →'
    case 'Business':
      return '查看关联业务详情 →'
    default:
      return '查看关联业务 →'
  }
})

/* ============================== 生命周期 ============================== */

onMounted(() => {
  fetchList()
})
</script>

<template>
  <div class="notifications-page">
    <div class="page-header">
      <h1>通知中心</h1>
      <p class="sub">查询站内信通知，支持按已读状态筛选、批量标记已读与全部已读。</p>
    </div>

    <!-- 区域 A：筛选条 -->
    <a-card :bordered="false" class="filter-card">
      <a-form layout="inline">
        <a-form-item label="已读状态">
          <a-select
            :value="readStatusValue"
            style="width: 120px"
            :options="readStatusOptions"
            @change="onReadStatusChange"
          />
        </a-form-item>
        <a-form-item label="通知类型">
          <a-select
            :value="typeValue"
            style="width: 140px"
            :options="typeOptions"
            @change="onTypeChange"
          />
        </a-form-item>
        <a-form-item>
          <a-button type="primary" @click="onQuery">查询</a-button>
          <a-button style="margin-left: 8px" @click="onReset">重置</a-button>
        </a-form-item>
      </a-form>
    </a-card>

    <a-card :bordered="false" class="list-card">
      <!-- 区域 B：工具栏 -->
      <div class="toolbar">
        <div class="toolbar-left">
          <IdempotencyButton
            type="primary"
            :loading="markAllLoading"
            :disabled="unreadCount === 0"
            @click="onMarkAllRead"
          >
            <CheckOutlined />
            全部标记已读
          </IdempotencyButton>
          <IdempotencyButton
            :loading="batchMarking"
            :disabled="selectedUnreadIds.length === 0"
            @click="onBatchMarkRead"
          >
            批量标记已读{{ selectedUnreadIds.length > 0 ? `（${selectedUnreadIds.length}）` : '' }}
          </IdempotencyButton>
        </div>
        <div class="toolbar-right">
          <a-badge :count="unreadCount" :offset="[6, 0]" aria-label="未读通知数">
            <span class="unread-hint"><BellOutlined /> 未读通知</span>
          </a-badge>
          <a-button :loading="loading" @click="fetchList">
            <ReloadOutlined />
            刷新
          </a-button>
        </div>
      </div>

      <!-- 区域 C：通知列表 -->
      <a-spin :spinning="loading">
        <div v-if="loadError" class="state-block">
          <EmptyState description="加载通知列表失败" action-text="重新加载" @action="fetchList" />
        </div>
        <EmptyState
          v-else-if="sortedRecords.length === 0"
          :description="filters.isRead === false ? '无未读通知' : '暂无通知'"
        />
        <ul v-else class="notif-list">
          <li
            v-for="record in sortedRecords"
            :key="record.id"
            :class="['notif-item', { unread: !record.isRead }]"
            :aria-label="`${record.isRead ? '' : '未读 '}${record.title}`"
            @click="openDetail(record)"
          >
            <a-checkbox
              :checked="isSelected(record)"
              class="notif-checkbox"
              aria-label="勾选通知"
              @click.stop
              @change="toggleSelect(record)"
            />
            <span :class="['notif-dot', { unread: !record.isRead }]" aria-hidden="true" />
            <div class="notif-body">
              <div class="notif-header">
                <div :class="['notif-title', { unread: !record.isRead }]">{{ record.title }}</div>
                <div class="notif-time">{{ formatDateTime(record.createdAt) }}</div>
              </div>
              <div class="notif-summary">{{ record.summary ?? '—' }}</div>
              <div class="notif-footer">
                <a-tag :color="typeMeta(record.type).color">{{ typeMeta(record.type).label }}</a-tag>
                <div class="notif-actions">
                  <a-button type="link" size="small" @click.stop="openDetail(record)">查看</a-button>
                  <a-button
                    v-if="!record.isRead"
                    type="link"
                    size="small"
                    :loading="marking"
                    @click.stop="markRead(record)"
                  >
                    标记已读
                  </a-button>
                </div>
              </div>
            </div>
          </li>
        </ul>

        <!-- 分页 -->
        <div v-if="!loadError && pagination.total > 0" class="pagination-wrap">
          <a-pagination
            :current="pagination.current"
            :page-size="pagination.pageSize"
            :total="pagination.total"
            :show-size-changer="pagination.showSizeChanger"
            :show-total="pagination.showTotal"
            @change="onPageChange"
            @showSizeChange="onPageChange"
          />
        </div>
      </a-spin>
    </a-card>

    <!-- 区域 D：详情抽屉 -->
    <a-drawer
      v-model:open="detailOpen"
      title="通知详情"
      placement="right"
      width="480"
      :destroy-on-close="true"
    >
      <div v-if="detail" class="detail-body">
        <div class="detail-section">
          <div class="detail-section__title">标题</div>
          <div :class="['detail-section__content', { 'detail-title--unread': !detail.isRead }]" aria-live="polite">
            {{ detail.title }}
          </div>
        </div>
        <div class="detail-section">
          <div class="detail-section__title">基本信息</div>
          <div class="detail-meta">
            <div class="detail-meta__row">
              <span class="meta-label">类型</span>
              <span class="meta-value">
                <a-tag :color="typeMeta(detail.type).color">{{ typeMeta(detail.type).label }}</a-tag>
              </span>
            </div>
            <div class="detail-meta__row">
              <span class="meta-label">来源</span>
              <span class="meta-value">{{ detail.source ?? '—' }}</span>
            </div>
            <div class="detail-meta__row">
              <span class="meta-label">时间</span>
              <span class="meta-value">{{ formatDateTime(detail.createdAt) }}</span>
            </div>
            <div class="detail-meta__row">
              <span class="meta-label">状态</span>
              <span class="meta-value">{{ detail.isRead ? '已读' : '未读' }}</span>
            </div>
          </div>
        </div>
        <div class="detail-section">
          <div class="detail-section__title">通知内容</div>
          <div class="detail-section__content detail-content">{{ detail.content ?? detail.summary ?? '—' }}</div>
        </div>
        <div v-if="detail.businessRef" class="detail-section">
          <a-button type="primary" block @click="goBusiness">{{ businessLinkText }}</a-button>
        </div>
      </div>
      <template #footer>
        <div class="drawer-footer">
          <a-button @click="detailOpen = false">关闭</a-button>
          <IdempotencyButton
            v-if="detail && !detail.isRead"
            type="primary"
            :loading="marking"
            @click="detail && markRead(detail)"
          >
            标记已读
          </IdempotencyButton>
        </div>
      </template>
    </a-drawer>

    <!-- 全部标记已读二次确认 -->
    <ConfirmDialog
      :open="markAllConfirmOpen"
      title="全部标记已读"
      :content="`将把当前 ${unreadCount} 条未读通知全部标记为已读，未读状态提示将被清除。确定继续吗？`"
      ok-text="全部已读"
      @confirm="onConfirmMarkAll"
      @cancel="markAllConfirmOpen = false"
    />
  </div>
</template>

<style scoped>
.notifications-page {
  display: flex;
  flex-direction: column;
  gap: 16px;
}

.page-header h1 {
  margin: 0;
  font-size: 20px;
  font-weight: 600;
  color: #000000d9;
}

.page-header .sub {
  margin: 4px 0 0;
  font-size: 12px;
  color: #8c8c8c;
}

.filter-card :deep(.ant-card-body) {
  padding: 16px 24px;
}

.list-card :deep(.ant-card-body) {
  padding: 16px 24px 24px;
}

/* 工具栏 */
.toolbar {
  display: flex;
  align-items: center;
  justify-content: space-between;
  gap: 12px;
  margin-bottom: 16px;
}

.toolbar-left {
  display: flex;
  gap: 8px;
}

.toolbar-right {
  display: flex;
  align-items: center;
  gap: 16px;
}

.unread-hint {
  font-size: 13px;
  color: #595959;
  display: inline-flex;
  align-items: center;
  gap: 4px;
}

/* 通知列表 */
.notif-list {
  list-style: none;
  margin: 0;
  padding: 0;
  display: flex;
  flex-direction: column;
  gap: 12px;
}

.notif-item {
  display: flex;
  align-items: flex-start;
  gap: 12px;
  padding: 12px 16px;
  border: 1px solid #f0f0f0;
  border-radius: 6px;
  cursor: pointer;
  transition: border-color 0.15s, background-color 0.15s;
}

.notif-item:hover {
  border-color: #1677ff;
  background: #fafcff;
}

.notif-checkbox {
  margin-top: 4px;
}

.notif-dot {
  width: 6px;
  height: 6px;
  border-radius: 50%;
  background: #8c8c8c;
  margin-top: 10px;
  flex-shrink: 0;
}

.notif-dot.unread {
  background: #ff4d4f;
}

.notif-body {
  flex: 1;
  min-width: 0;
}

.notif-header {
  display: flex;
  align-items: center;
  justify-content: space-between;
  gap: 12px;
}

.notif-title {
  font-size: 14px;
  color: #000000d9;
  min-width: 0;
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
}

.notif-title.unread {
  font-weight: 600;
}

.notif-time {
  font-size: 12px;
  color: #8c8c8c;
  flex-shrink: 0;
}

.notif-summary {
  font-size: 13px;
  color: #595959;
  margin-top: 4px;
  display: -webkit-box;
  -webkit-line-clamp: 2;
  -webkit-box-orient: vertical;
  overflow: hidden;
}

.notif-footer {
  display: flex;
  align-items: center;
  justify-content: space-between;
  margin-top: 8px;
}

.notif-actions {
  display: flex;
  gap: 4px;
}

.state-block {
  padding: 32px 0;
}

.pagination-wrap {
  display: flex;
  justify-content: flex-end;
  margin-top: 16px;
}

/* 详情抽屉 */
.detail-body {
  display: flex;
  flex-direction: column;
  gap: 24px;
}

.detail-section__title {
  font-size: 13px;
  font-weight: 600;
  color: #8c8c8c;
  margin-bottom: 8px;
}

.detail-section__content {
  font-size: 14px;
  color: #000000d9;
}

.detail-title--unread {
  font-weight: 600;
}

.detail-content {
  line-height: 1.8;
  white-space: pre-wrap;
  word-break: break-all;
}

.detail-meta {
  display: flex;
  flex-direction: column;
  gap: 8px;
}

.detail-meta__row {
  display: flex;
  align-items: center;
  gap: 12px;
}

.meta-label {
  width: 56px;
  font-size: 13px;
  color: #8c8c8c;
  flex-shrink: 0;
}

.meta-value {
  font-size: 13px;
  color: #000000d9;
}

.drawer-footer {
  display: flex;
  justify-content: flex-end;
  gap: 8px;
}
</style>
