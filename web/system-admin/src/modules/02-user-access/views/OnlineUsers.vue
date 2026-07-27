<!-- web/system-admin/src/modules/02-user-access/views/OnlineUsers.vue -->
<template>
  <div class="online-users">
    <!-- 区域 A：顶部统计卡片 -->
    <a-row :gutter="16">
      <a-col :xs="24" :sm="8">
        <StatisticCard
          title="在线总数"
          :value="stats.total"
          :loading="statsLoading"
          status="default"
          suffix="人"
        />
      </a-col>
      <a-col :xs="24" :sm="8">
        <StatisticCard
          title="24h 登录总数"
          :value="stats.logins24h"
          :loading="statsLoading"
          status="success"
          suffix="次"
        />
      </a-col>
      <a-col :xs="24" :sm="8">
        <StatisticCard
          title="异常会话数"
          :value="stats.anomalies"
          :loading="statsLoading"
          :status="stats.anomalies > 0 ? 'danger' : 'success'"
          suffix="个"
        />
      </a-col>
    </a-row>

    <!-- 区域 B：筛选条 -->
    <a-card :bordered="false" class="filter-card">
      <a-form layout="inline">
        <a-form-item label="用户名">
          <a-input
            v-model:value="filters.username"
            placeholder="请输入用户名"
            allow-clear
            style="width: 180px"
            @press-enter="onQuery"
          />
        </a-form-item>
        <a-form-item label="IP 地址">
          <a-input
            v-model:value="filters.ipAddress"
            placeholder="请输入 IP"
            allow-clear
            style="width: 180px"
            @press-enter="onQuery"
          />
        </a-form-item>
        <a-form-item label="登录时间">
          <DateTimeRangePicker
            v-model:value="filters.dateRange"
            show-time
            @change="onDateRangeChange"
          />
        </a-form-item>
        <a-form-item>
          <a-space>
            <a-button type="primary" @click="onQuery">查询</a-button>
            <a-button @click="onReset">重置</a-button>
            <a-button :loading="refreshing" @click="onManualRefresh">
              <template #icon><ReloadOutlined /></template>
              刷新
            </a-button>
          </a-space>
        </a-form-item>
        <a-form-item>
          <a-tooltip :title="autoRefreshEnabled ? '自动刷新（30s）已开启' : '自动刷新已关闭'">
            <a-switch
              v-model:checked="autoRefreshEnabled"
              checked-children="自动"
              un-checked-children="手动"
              @change="onAutoRefreshToggle"
            />
          </a-tooltip>
        </a-form-item>
      </a-form>
    </a-card>

    <!-- 区域 C：主表格 -->
    <a-card :bordered="false" class="table-card">
      <a-table
        :columns="columns"
        :data-source="tableData"
        :loading="loading"
        :pagination="pagination"
        row-key="id"
        :scroll="{ x: 1400 }"
        @change="onTableChange"
      >
        <template #emptyText>
          <EmptyState description="暂无在线用户" action-text="清空筛选条件" @action="onReset" />
        </template>
        <template #bodyCell="{ column, record }">
          <template v-if="column.key === 'username'">
            <span class="user-cell">
              {{ record.username }}
              <StatusTag
                v-if="record.isAnomaly"
                type="onlineUser"
                status="Anomaly"
              />
            </span>
          </template>
          <template v-else-if="column.key === 'roles'">
            <a-tag v-for="r in record.roles" :key="r" color="blue">{{ r }}</a-tag>
            <span v-if="!record.roles || record.roles.length === 0">—</span>
          </template>
          <template v-else-if="column.key === 'ipAddress'">
            <div class="ip-cell">
              <div>{{ record.ipAddress }}</div>
              <div class="geo-text">{{ record.geoLocation || '—' }}</div>
            </div>
          </template>
          <template v-else-if="column.key === 'status'">
            <StatusTag
              type="onlineUser"
              :status="record.isAnomaly ? 'Anomaly' : 'Normal'"
            />
          </template>
          <template v-else-if="column.key === 'loginAt'">
            {{ formatDateTime(record.loginAt) }}
          </template>
          <template v-else-if="column.key === 'lastActivityAt'">
            {{ formatDateTime(record.lastActivityAt) }}
          </template>
          <template v-else-if="column.key === 'sessionDuration'">
            {{ formatDuration(record.sessionDurationMs) }}
          </template>
          <template v-else-if="column.key === 'action'">
            <a-space>
              <a-button type="link" size="small" @click="onView(record)">查看详情</a-button>
              <PermissionGuard permission="online-user:kick">
                <a-button
                  type="link"
                  size="small"
                  danger
                  :loading="kickingId === record.id"
                  @click="onKick(record)"
                >强制下线</a-button>
              </PermissionGuard>
            </a-space>
          </template>
        </template>
      </a-table>
    </a-card>

    <!-- 区域 D：详情抽屉 -->
    <a-drawer
      v-model:open="drawerOpen"
      title="在线会话详情"
      placement="right"
      width="560"
      :destroy-on-close="true"
    >
      <a-spin :spinning="detailLoading">
        <a-descriptions v-if="detail" :column="1" bordered size="small">
          <a-descriptions-item label="用户名">{{ detail.username }}</a-descriptions-item>
          <a-descriptions-item label="用户 ID">{{ detail.userId }}</a-descriptions-item>
          <a-descriptions-item label="角色">
            <a-tag v-for="r in detail.roles" :key="r" color="blue">{{ r }}</a-tag>
            <span v-if="!detail.roles || detail.roles.length === 0">—</span>
          </a-descriptions-item>
          <a-descriptions-item label="会话状态">
            <StatusTag
              type="onlineUser"
              :status="detail.isAnomaly ? 'Anomaly' : 'Normal'"
            />
          </a-descriptions-item>
          <a-descriptions-item label="IP 地址">{{ detail.ipAddress }}</a-descriptions-item>
          <a-descriptions-item label="地理位置">{{ detail.geoLocation || '—' }}</a-descriptions-item>
          <a-descriptions-item label="浏览器">{{ detail.browser || '—' }}</a-descriptions-item>
          <a-descriptions-item label="操作系统">{{ detail.os || '—' }}</a-descriptions-item>
          <a-descriptions-item label="登录时间">{{ formatDateTime(detail.loginAt) }}</a-descriptions-item>
          <a-descriptions-item label="最近活动">{{ formatDateTime(detail.lastActivityAt) }}</a-descriptions-item>
          <a-descriptions-item label="会话时长">{{ formatDuration(detail.sessionDurationMs) }}</a-descriptions-item>
          <a-descriptions-item label="Token 预览">
            <code class="token-preview">{{ tokenPreviewText(detail.tokenPreview) }}</code>
          </a-descriptions-item>
          <a-descriptions-item label="设备指纹">
            <code class="device-fp">{{ detail.deviceFingerprint || '—' }}</code>
          </a-descriptions-item>
          <a-descriptions-item label="会话请求次数">{{ detail.requestCount }} 次</a-descriptions-item>
        </a-descriptions>
        <a-empty v-else-if="!detailLoading" description="无数据" />
      </a-spin>
    </a-drawer>

    <!-- 区域 E：强制下线确认对话框 -->
    <ConfirmDialog
      :open="kickConfirmOpen"
      danger
      title="强制下线"
      :content="kickConfirmContent"
      @confirm="onConfirmKick"
      @cancel="onCancelKick"
    />
  </div>
</template>

<script setup lang="ts">
import { computed, onMounted, onUnmounted, reactive, ref } from 'vue'
import { message } from 'ant-design-vue'
import { ReloadOutlined } from '@ant-design/icons-vue'
import type { TableColumnsType } from 'ant-design-vue'
import { onlineUsersApi } from '../api/online-users.api'
import type { OnlineUserDto, OnlineUserStatsDto } from '../types/online-user.dto'
import { formatDateTime } from '@/shared/utils/format'
import {
  StatisticCard,
  StatusTag,
  ConfirmDialog,
  EmptyState,
  DateTimeRangePicker,
} from '@/shared/components'
import PermissionGuard from '@/shared/auth/PermissionGuard.vue'

/** 自动刷新间隔（毫秒） */
const AUTO_REFRESH_INTERVAL = 30_000

interface FilterState {
  username: string
  ipAddress: string
  dateRange: [string, string] | null
}

const filters = reactive<FilterState>({
  username: '',
  ipAddress: '',
  dateRange: null,
})

const columns: TableColumnsType = [
  { title: '用户名', dataIndex: 'username', key: 'username', width: 160 },
  { title: '角色', key: 'roles', width: 180 },
  { title: 'IP / 地理位置', dataIndex: 'ipAddress', key: 'ipAddress', width: 200 },
  { title: '浏览器', dataIndex: 'browser', key: 'browser', width: 140, ellipsis: true },
  { title: '操作系统', dataIndex: 'os', key: 'os', width: 140, ellipsis: true },
  { title: '状态', key: 'status', width: 90 },
  { title: '登录时间', key: 'loginAt', width: 170 },
  { title: '最近活动', key: 'lastActivityAt', width: 170 },
  { title: '会话时长', key: 'sessionDuration', width: 120 },
  { title: '操作', key: 'action', width: 180, fixed: 'right' },
]

const tableData = ref<OnlineUserDto[]>([])
const loading = ref(false)
const refreshing = ref(false)
const kickingId = ref<string | null>(null)

const pagination = reactive({
  current: 1,
  pageSize: 20,
  total: 0,
  showSizeChanger: true,
  showTotal: (total: number) => `共 ${total} 条`,
})

const stats = reactive<OnlineUserStatsDto>({
  total: 0,
  logins24h: 0,
  anomalies: 0,
})
const statsLoading = ref(false)

/** 构造查询参数 */
function buildQuery() {
  const params: {
    page: number
    pageSize: number
    username?: string
    ipAddress?: string
    loginAtFrom?: string
    loginAtTo?: string
  } = {
    page: pagination.current,
    pageSize: pagination.pageSize,
  }
  if (filters.username.trim()) params.username = filters.username.trim()
  if (filters.ipAddress.trim()) params.ipAddress = filters.ipAddress.trim()
  if (filters.dateRange && filters.dateRange.length === 2) {
    params.loginAtFrom = filters.dateRange[0]
    params.loginAtTo = filters.dateRange[1]
  }
  return params
}

/** 拉取在线用户列表 */
async function fetchList() {
  loading.value = true
  try {
    const result = await onlineUsersApi.list(buildQuery())
    tableData.value = result.items
    pagination.total = result.total
  } catch {
    message.error('加载在线用户列表失败')
  } finally {
    loading.value = false
  }
}

/** 拉取统计数据 */
async function fetchStats() {
  statsLoading.value = true
  try {
    const result = await onlineUsersApi.stats()
    stats.total = result.total
    stats.logins24h = result.logins24h
    stats.anomalies = result.anomalies
  } catch {
    // 统计失败不打断主流程，保留上次数据
  } finally {
    statsLoading.value = false
  }
}

/** 同时刷新列表与统计 */
async function refreshAll() {
  refreshing.value = true
  try {
    await Promise.all([fetchList(), fetchStats()])
  } finally {
    refreshing.value = false
  }
}

function onQuery() {
  pagination.current = 1
  void fetchList()
}

function onReset() {
  filters.username = ''
  filters.ipAddress = ''
  filters.dateRange = null
  pagination.current = 1
  void fetchList()
}

function onDateRangeChange(value: [string, string]) {
  filters.dateRange = value
}

function onTableChange(pag: { current: number; pageSize: number }) {
  pagination.current = pag.current
  pagination.pageSize = pag.pageSize
  void fetchList()
}

function onManualRefresh() {
  void refreshAll()
}

/** 格式化会话时长为可读文本（如 "5分30秒"、"1时2分3秒"） */
function formatDuration(ms: number | null | undefined): string {
  if (ms === null || ms === undefined || !Number.isFinite(ms) || ms < 0) return '—'
  const totalSeconds = Math.floor(ms / 1000)
  if (totalSeconds < 1) return '< 1秒'
  const hours = Math.floor(totalSeconds / 3600)
  const minutes = Math.floor((totalSeconds % 3600) / 60)
  const seconds = totalSeconds % 60
  if (hours > 0) {
    return `${hours}时${minutes}分${seconds}秒`
  }
  if (minutes > 0) {
    return `${minutes}分${seconds}秒`
  }
  return `${seconds}秒`
}

/** Token 预览，仅展示前 8 个字符 */
function tokenPreviewText(token: string | null | undefined): string {
  if (!token) return '—'
  return token.slice(0, 8)
}

// ===== 详情抽屉 =====
const drawerOpen = ref(false)
const detailLoading = ref(false)
const detail = ref<OnlineUserDto | null>(null)

async function onView(record: OnlineUserDto) {
  drawerOpen.value = true
  detailLoading.value = true
  detail.value = null
  try {
    const result = await onlineUsersApi.get(record.id)
    detail.value = result
  } catch {
    message.error('加载会话详情失败')
  } finally {
    detailLoading.value = false
  }
}

// ===== 强制下线 =====
const kickConfirmOpen = ref(false)
const pendingKick = ref<OnlineUserDto | null>(null)

const kickConfirmContent = computed(() => {
  const target = pendingKick.value
  if (!target) return '确定要强制下线该用户吗？'
  return `确定要强制下线用户「${target.username}」（IP: ${target.ipAddress}）吗？该用户当前会话将立即终止，需要重新登录。`
})

function onKick(record: OnlineUserDto) {
  pendingKick.value = record
  kickConfirmOpen.value = true
}

function onCancelKick() {
  kickConfirmOpen.value = false
  pendingKick.value = null
}

async function onConfirmKick() {
  kickConfirmOpen.value = false
  const target = pendingKick.value
  if (!target) return
  kickingId.value = target.id
  try {
    await onlineUsersApi.kick(target.id)
    // 从当前列表中移除该行
    tableData.value = tableData.value.filter((u) => u.id !== target.id)
    pagination.total = Math.max(0, pagination.total - 1)
    message.success(`已下线 ${target.username}`)
    // 同步刷新统计（在线总数减少）
    void fetchStats()
  } catch {
    message.error('强制下线失败，请稍后重试')
  } finally {
    kickingId.value = null
    pendingKick.value = null
  }
}

// ===== 自动刷新 =====
const autoRefreshEnabled = ref(true)
let refreshTimer: ReturnType<typeof setInterval> | null = null

function startAutoRefresh() {
  if (refreshTimer) return
  refreshTimer = setInterval(() => {
    void refreshAll()
  }, AUTO_REFRESH_INTERVAL)
}

function stopAutoRefresh() {
  if (refreshTimer) {
    clearInterval(refreshTimer)
    refreshTimer = null
  }
}

function onAutoRefreshToggle(checked: boolean | string | number) {
  if (checked) {
    startAutoRefresh()
  } else {
    stopAutoRefresh()
  }
}

onMounted(() => {
  void refreshAll()
  if (autoRefreshEnabled.value) {
    startAutoRefresh()
  }
})

onUnmounted(() => {
  stopAutoRefresh()
})
</script>

<style scoped>
.online-users {
  display: flex;
  flex-direction: column;
  gap: 16px;
}
.filter-card :deep(.ant-card-body) {
  padding: 16px 24px;
}
.table-card :deep(.ant-card-body) {
  padding: 0;
}
.user-cell {
  display: inline-flex;
  align-items: center;
  gap: 6px;
}
.ip-cell {
  display: flex;
  flex-direction: column;
}
.geo-text {
  font-size: 12px;
  color: #8c8c8c;
}
.token-preview,
.device-fp {
  font-family: 'SFMono-Regular', Consolas, 'Liberation Mono', Menlo, monospace;
  font-size: 12px;
  background: #f5f5f5;
  padding: 2px 6px;
  border-radius: 3px;
  word-break: break-all;
}
</style>
