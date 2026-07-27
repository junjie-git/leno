<!-- web/system-admin/src/modules/05-audit/views/AuditLogs.vue -->
<!-- 审计日志：3 Tab + 筛选 + 表格 + 详情抽屉（敏感字段掩码）+ 导出 CSV，严格只读 -->
<script setup lang="ts">
import { ref, reactive, computed, onMounted } from 'vue'
import { useRoute } from 'vue-router'
import { message } from 'ant-design-vue'
import {
  DownloadOutlined, SearchOutlined, EyeOutlined,
} from '@ant-design/icons-vue'
import dayjs from 'dayjs'
import { auditLogsApi } from '../api/audit-logs.api'
import type {
  AuditLogEntryDto,
  OperationLogDto,
  CrossDomainAuditEntryDto,
  OperatorRole,
} from '../types/audit-log.dto'
import DateTimeRangePicker from '@/shared/components/DateTimeRangePicker.vue'
import PermissionGuard from '@/shared/auth/PermissionGuard.vue'
import EmptyState from '@/shared/components/EmptyState.vue'
import JsonViewer from '@/shared/components/JsonViewer.vue'
import { formatDateTime } from '@/shared/utils/format'

type TabKey = 'audit-logs' | 'operation-logs' | 'cross-domain-entries'

interface AuditFilterState {
  operatorId: string
  resourceType: string
  action: string
  timeRange: [string, string] | null
  page: number
  pageSize: number
}

interface OperationFilterState {
  operatorId: string
  module: string
  timeRange: [string, string] | null
  page: number
  pageSize: number
}

interface EntryFilterState {
  module: string
  action: string
  operatorId: string
  timeRange: [string, string] | null
  page: number
  pageSize: number
}

const route = useRoute()
const activeTab = ref<TabKey>('audit-logs')

// 默认近 24 小时（ISO 8601 UTC）
function defaultRange(): [string, string] {
  return [
    dayjs().subtract(24, 'hour').toISOString(),
    dayjs().toISOString(),
  ]
}

const auditFilter = reactive<AuditFilterState>({
  operatorId: '',
  resourceType: (route.query.resourceType as string) || '',
  action: '',
  timeRange: defaultRange(),
  page: 1,
  pageSize: 20,
})
const operationFilter = reactive<OperationFilterState>({
  operatorId: '',
  module: '',
  timeRange: defaultRange(),
  page: 1,
  pageSize: 20,
})
const entryFilter = reactive<EntryFilterState>({
  module: '',
  action: '',
  operatorId: '',
  timeRange: defaultRange(),
  page: 1,
  pageSize: 20,
})

const loading = ref(false)
const exporting = ref(false)
const auditList = ref<AuditLogEntryDto[]>([])
const auditTotal = ref(0)
const operationList = ref<OperationLogDto[]>([])
const operationTotal = ref(0)
const entryList = ref<CrossDomainAuditEntryDto[]>([])
const entryTotal = ref(0)

// 详情抽屉
const drawerVisible = ref(false)
const drawerLoading = ref(false)
const currentDetail = ref<AuditLogEntryDto | null>(null)

const resourceTypeOptions = [
  'Shop', 'Role', 'User', 'DeadLetter', 'IndexRebuild', 'Reconciliation',
  'RateLimitRule', 'FeatureFlag', 'SystemConfig', 'DataDictionary', 'Announcement',
  'ScheduledTask', 'OAuthClient', 'Operator', 'Outbox', 'Alert',
]
const moduleOptions = [
  'Identity', 'AccessControl', 'UserCenter', 'Points', 'Membership',
  'Review', 'AfterSales', 'Product', 'Order', 'Payment', 'Notification', 'Inventory',
  'SystemAdmin',
]

const auditColumns = computed(() => [
  { title: '日志ID', dataIndex: 'logId', key: 'logId', width: 140, ellipsis: true },
  { title: '操作人', key: 'operator', width: 120, customRender: ({ record }: { record: AuditLogEntryDto }) => record.operatorName },
  { title: '角色', key: 'operatorRole', width: 100 },
  { title: '来源上下文', dataIndex: 'sourceContext', key: 'sourceContext', width: 120 },
  { title: '动作', dataIndex: 'action', key: 'action', width: 100 },
  { title: '资源类型', dataIndex: 'resourceType', key: 'resourceType', width: 120 },
  { title: '资源ID', dataIndex: 'resourceId', key: 'resourceId', width: 140, ellipsis: true },
  { title: '响应状态', dataIndex: 'responseStatus', key: 'responseStatus', width: 100, align: 'right' as const },
  { title: 'IP', dataIndex: 'ipAddress', key: 'ipAddress', width: 130, responsive: ['xl'] as const },
  { title: '发生时间', dataIndex: 'occurredAt', key: 'occurredAt', width: 180, customRender: ({ text }: { text: string }) => formatDateTime(text) },
  { title: '操作', key: 'action-col', width: 90, fixed: 'right' as const },
])

const operationColumns = computed(() => [
  { title: '日志ID', dataIndex: 'logId', key: 'logId', width: 140, ellipsis: true },
  { title: '操作人', key: 'operator', width: 120, customRender: ({ record }: { record: OperationLogDto }) => record.operatorName },
  { title: '模块', dataIndex: 'module', key: 'module', width: 120 },
  { title: '动作', dataIndex: 'action', key: 'action', width: 100 },
  { title: '资源类型', dataIndex: 'resourceType', key: 'resourceType', width: 120 },
  { title: '资源ID', dataIndex: 'resourceId', key: 'resourceId', width: 140, ellipsis: true },
  { title: '详情', dataIndex: 'detail', key: 'detail', ellipsis: true },
  { title: '发生时间', dataIndex: 'occurredAt', key: 'occurredAt', width: 180, customRender: ({ text }: { text: string }) => formatDateTime(text) },
])

const entryColumns = computed(() => [
  { title: '条目ID', dataIndex: 'entryId', key: 'entryId', width: 140, ellipsis: true },
  { title: '模块', dataIndex: 'module', key: 'module', width: 120 },
  { title: '动作', dataIndex: 'action', key: 'action', width: 100 },
  { title: '操作人', key: 'operator', width: 120, customRender: ({ record }: { record: CrossDomainAuditEntryDto }) => record.operatorName },
  { title: '资源类型', dataIndex: 'resourceType', key: 'resourceType', width: 120 },
  { title: '资源ID', dataIndex: 'resourceId', key: 'resourceId', width: 140, ellipsis: true },
  { title: 'TraceId', dataIndex: 'traceId', key: 'traceId', width: 160, ellipsis: true },
  { title: '发生时间', dataIndex: 'occurredAt', key: 'occurredAt', width: 180, customRender: ({ text }: { text: string }) => formatDateTime(text) },
])

// 响应状态码颜色：2xx 绿、4xx 黄、5xx 红
function statusColor(status: number): string {
  if (status >= 200 && status < 300) return 'success'
  if (status >= 400 && status < 500) return 'warning'
  if (status >= 500) return 'error'
  return 'default'
}

// 操作人角色颜色（design-prompt §6）
function roleColor(role: OperatorRole): string {
  switch (role) {
    case 'Admin': return 'error'
    case 'Operator': return 'processing'
    case 'Seller': return 'success'
    case 'Buyer': return 'default'
    case 'System': return 'purple'
    default: return 'default'
  }
}

// 敏感字段名正则：匹配 password/token/secret/apiKey/credential/authorization（不区分大小写）
const SENSITIVE_KEY_PATTERN = /(password|token|secret|api[_-]?key|credential|authorization)/i

/** 将任意值替换为掩码占位 */
function maskValue(_value: unknown): string {
  return '******'
}

/** 递归掩码对象中匹配敏感键的字段值 */
function maskSensitive(input: unknown): unknown {
  if (input === null || input === undefined) return input
  if (Array.isArray(input)) return input.map(maskSensitive)
  if (typeof input === 'object') {
    const result: Record<string, unknown> = {}
    for (const [key, value] of Object.entries(input as Record<string, unknown>)) {
      if (SENSITIVE_KEY_PATTERN.test(key)) {
        result[key] = maskValue(value)
      } else if (typeof value === 'object' && value !== null) {
        result[key] = maskSensitive(value)
      } else {
        result[key] = value
      }
    }
    return result
  }
  return input
}

/** 安全解析 JSON 字符串；解析失败返回原始字符串 */
function safeParseJson(text: string | null | undefined): unknown {
  if (!text) return null
  try {
    return JSON.parse(text)
  } catch {
    return text
  }
}

/** 解析 + 掩码 JSON 快照，返回可交给 JsonViewer 的对象 */
function maskedSnapshot(text: string | null | undefined): unknown {
  return maskSensitive(safeParseJson(text))
}

async function loadAuditLogs(): Promise<void> {
  loading.value = true
  try {
    const params = {
      operatorId: auditFilter.operatorId || undefined,
      resourceType: auditFilter.resourceType || undefined,
      action: auditFilter.action || undefined,
      fromTime: auditFilter.timeRange ? auditFilter.timeRange[0] : undefined,
      toTime: auditFilter.timeRange ? auditFilter.timeRange[1] : undefined,
      page: auditFilter.page,
      pageSize: auditFilter.pageSize,
    }
    const res = await auditLogsApi.list(params)
    auditList.value = res.data.items
    auditTotal.value = res.data.total
  } catch (e: unknown) {
    const msg = e instanceof Error ? e.message : '加载审计日志失败'
    message.error(msg)
  } finally {
    loading.value = false
  }
}

async function loadOperationLogs(): Promise<void> {
  loading.value = true
  try {
    const params = {
      operatorId: operationFilter.operatorId || undefined,
      module: operationFilter.module || undefined,
      fromTime: operationFilter.timeRange ? operationFilter.timeRange[0] : undefined,
      toTime: operationFilter.timeRange ? operationFilter.timeRange[1] : undefined,
      page: operationFilter.page,
      pageSize: operationFilter.pageSize,
    }
    const res = await auditLogsApi.listOperationLogs(params)
    operationList.value = res.data.items
    operationTotal.value = res.data.total
  } catch (e: unknown) {
    const msg = e instanceof Error ? e.message : '加载操作日志失败'
    message.error(msg)
  } finally {
    loading.value = false
  }
}

async function loadEntries(): Promise<void> {
  loading.value = true
  try {
    const params = {
      module: entryFilter.module || undefined,
      action: entryFilter.action || undefined,
      operatorId: entryFilter.operatorId || undefined,
      fromTime: entryFilter.timeRange ? entryFilter.timeRange[0] : undefined,
      toTime: entryFilter.timeRange ? entryFilter.timeRange[1] : undefined,
      page: entryFilter.page,
      pageSize: entryFilter.pageSize,
    }
    const res = await auditLogsApi.listAuditLogEntries(params)
    entryList.value = res.data.items
    entryTotal.value = res.data.total
  } catch (e: unknown) {
    const msg = e instanceof Error ? e.message : '加载跨域审计条目失败'
    message.error(msg)
  } finally {
    loading.value = false
  }
}

function onSearch(): void {
  if (activeTab.value === 'audit-logs') {
    auditFilter.page = 1
    loadAuditLogs()
  } else if (activeTab.value === 'operation-logs') {
    operationFilter.page = 1
    loadOperationLogs()
  } else {
    entryFilter.page = 1
    loadEntries()
  }
}

function onTabChange(key: string): void {
  activeTab.value = key as TabKey
  if (key === 'audit-logs') loadAuditLogs()
  else if (key === 'operation-logs') loadOperationLogs()
  else loadEntries()
}

function clearFilter(): void {
  if (activeTab.value === 'audit-logs') {
    auditFilter.operatorId = ''
    auditFilter.resourceType = ''
    auditFilter.action = ''
    auditFilter.timeRange = defaultRange()
    auditFilter.page = 1
    loadAuditLogs()
  } else if (activeTab.value === 'operation-logs') {
    operationFilter.operatorId = ''
    operationFilter.module = ''
    operationFilter.timeRange = defaultRange()
    operationFilter.page = 1
    loadOperationLogs()
  } else {
    entryFilter.module = ''
    entryFilter.action = ''
    entryFilter.operatorId = ''
    entryFilter.timeRange = defaultRange()
    entryFilter.page = 1
    loadEntries()
  }
}

async function openDetail(record: AuditLogEntryDto): Promise<void> {
  drawerVisible.value = true
  drawerLoading.value = true
  currentDetail.value = null
  try {
    const res = await auditLogsApi.get(record.logId)
    currentDetail.value = res.data
  } catch (e: unknown) {
    const msg = e instanceof Error ? e.message : '审计日志条目不存在'
    message.error(msg)
    drawerVisible.value = false
  } finally {
    drawerLoading.value = false
  }
}

async function onExport(): Promise<void> {
  if (!auditFilter.timeRange) {
    message.warning('请先选择时间范围')
    return
  }
  exporting.value = true
  try {
    const params = {
      operatorId: auditFilter.operatorId || undefined,
      resourceType: auditFilter.resourceType || undefined,
      action: auditFilter.action || undefined,
      fromTime: auditFilter.timeRange[0],
      toTime: auditFilter.timeRange[1],
    }
    const res = await auditLogsApi.export(params)
    // 注：响应拦截器已解包 data；res.data 为 Blob 实例。文件名按时间戳生成。
    const blob = res.data
    const filename = `audit-logs-${dayjs().format('YYYYMMDDHHmmss')}.csv`
    const url = URL.createObjectURL(blob)
    const link = document.createElement('a')
    link.href = url
    link.download = filename
    document.body.appendChild(link)
    link.click()
    document.body.removeChild(link)
    URL.revokeObjectURL(url)
    message.success(`已导出 ${filename}`)
  } catch (e: unknown) {
    const msg = e instanceof Error ? e.message : '导出失败，请缩小时间范围后重试'
    message.error(msg)
  } finally {
    exporting.value = false
  }
}

// DateTimeRangePicker 仅 emit change（无 update:value），需手动写回 reactive state
function onAuditTimeRangeChange(val: [string, string]): void {
  auditFilter.timeRange = val
}
function onOperationTimeRangeChange(val: [string, string]): void {
  operationFilter.timeRange = val
}
function onEntryTimeRangeChange(val: [string, string]): void {
  entryFilter.timeRange = val
}

function onAuditPageChange(page: number, pageSize: number): void {
  auditFilter.page = page
  auditFilter.pageSize = pageSize
  loadAuditLogs()
}
function onOperationPageChange(page: number, pageSize: number): void {
  operationFilter.page = page
  operationFilter.pageSize = pageSize
  loadOperationLogs()
}
function onEntryPageChange(page: number, pageSize: number): void {
  entryFilter.page = page
  entryFilter.pageSize = pageSize
  loadEntries()
}

onMounted(() => {
  loadAuditLogs()
})
</script>

<template>
  <div class="audit-logs">
    <div class="page-header">
      <div class="page-title">审计日志</div>
      <div class="page-desc">查询跨域审计日志条目与操作日志，按操作人、模块、资源类型、时间区间筛选，查看详情并导出 CSV 用于合规追溯。</div>
    </div>

    <a-tabs :active-key="activeTab" @change="onTabChange">
      <a-tab-pane key="audit-logs" tab="审计日志" />
      <a-tab-pane key="operation-logs" tab="操作日志" />
      <a-tab-pane key="cross-domain-entries" tab="跨域审计条目" />
    </a-tabs>

    <!-- 筛选条 -->
    <div class="toolbar">
      <template v-if="activeTab === 'audit-logs'">
        <a-input
          v-model:value="auditFilter.operatorId"
          placeholder="操作人 ID"
          allow-clear
          style="width: 180px"
        />
        <a-select
          v-model:value="auditFilter.resourceType"
          placeholder="资源类型"
          allow-clear
          style="width: 180px"
          :options="resourceTypeOptions.map((v) => ({ label: v, value: v }))"
        />
        <a-input
          v-model:value="auditFilter.action"
          placeholder="动作（如 Create）"
          allow-clear
          style="width: 160px"
        />
        <DateTimeRangePicker :value="auditFilter.timeRange ?? undefined" @change="onAuditTimeRangeChange" />
      </template>
      <template v-else-if="activeTab === 'operation-logs'">
        <a-input
          v-model:value="operationFilter.operatorId"
          placeholder="操作人 ID"
          allow-clear
          style="width: 180px"
        />
        <a-select
          v-model:value="operationFilter.module"
          placeholder="模块"
          allow-clear
          style="width: 180px"
          :options="moduleOptions.map((v) => ({ label: v, value: v }))"
        />
        <DateTimeRangePicker :value="operationFilter.timeRange ?? undefined" @change="onOperationTimeRangeChange" />
      </template>
      <template v-else>
        <a-select
          v-model:value="entryFilter.module"
          placeholder="模块"
          allow-clear
          style="width: 180px"
          :options="moduleOptions.map((v) => ({ label: v, value: v }))"
        />
        <a-input
          v-model:value="entryFilter.action"
          placeholder="动作"
          allow-clear
          style="width: 160px"
        />
        <a-input
          v-model:value="entryFilter.operatorId"
          placeholder="操作人 ID"
          allow-clear
          style="width: 180px"
        />
        <DateTimeRangePicker :value="entryFilter.timeRange ?? undefined" @change="onEntryTimeRangeChange" />
      </template>
      <a-button type="primary" @click="onSearch">
        <SearchOutlined />查询
      </a-button>
      <PermissionGuard permission="audit-log:export">
        <a-button v-if="activeTab === 'audit-logs'" :loading="exporting" @click="onExport">
          <DownloadOutlined />导出 CSV
        </a-button>
      </PermissionGuard>
      <div class="spacer" />
      <a-button @click="clearFilter">清空筛选</a-button>
    </div>

    <!-- 审计日志表格 -->
    <a-table
      v-if="activeTab === 'audit-logs'"
      :columns="auditColumns"
      :data-source="auditList"
      :loading="loading"
      row-key="logId"
      size="middle"
      :scroll="{ x: 1300 }"
      :pagination="{
        current: auditFilter.page,
        pageSize: auditFilter.pageSize,
        total: auditTotal,
        showSizeChanger: true,
        onChange: onAuditPageChange,
      }"
    >
      <template #emptyText>
        <EmptyState description="暂无审计日志" action-text="清空筛选条件" @action="clearFilter" />
      </template>
      <template #bodyCell="{ column, record }">
        <template v-if="column.key === 'operatorRole'">
          <a-tag :color="roleColor(record.operatorRole)">{{ record.operatorRole }}</a-tag>
        </template>
        <template v-else-if="column.key === 'responseStatus'">
          <a-tag :color="statusColor(record.responseStatus)">{{ record.responseStatus }}</a-tag>
        </template>
        <template v-else-if="column.key === 'action-col'">
          <a-button type="link" size="small" @click="openDetail(record)">
            <EyeOutlined />详情
          </a-button>
        </template>
      </template>
    </a-table>

    <!-- 操作日志表格 -->
    <a-table
      v-else-if="activeTab === 'operation-logs'"
      :columns="operationColumns"
      :data-source="operationList"
      :loading="loading"
      row-key="logId"
      size="middle"
      :scroll="{ x: 1100 }"
      :pagination="{
        current: operationFilter.page,
        pageSize: operationFilter.pageSize,
        total: operationTotal,
        showSizeChanger: true,
        onChange: onOperationPageChange,
      }"
    >
      <template #emptyText>
        <EmptyState description="暂无操作日志" action-text="清空筛选条件" @action="clearFilter" />
      </template>
    </a-table>

    <!-- 跨域审计条目表格 -->
    <a-table
      v-else
      :columns="entryColumns"
      :data-source="entryList"
      :loading="loading"
      row-key="entryId"
      size="middle"
      :scroll="{ x: 1200 }"
      :pagination="{
        current: entryFilter.page,
        pageSize: entryFilter.pageSize,
        total: entryTotal,
        showSizeChanger: true,
        onChange: onEntryPageChange,
      }"
    >
      <template #emptyText>
        <EmptyState description="暂无跨域审计条目" action-text="清空筛选条件" @action="clearFilter" />
      </template>
    </a-table>

    <!-- 详情抽屉 -->
    <a-drawer
      v-model:open="drawerVisible"
      title="审计日志详情"
      placement="right"
      :width="720"
    >
      <a-spin :spinning="drawerLoading">
        <template v-if="currentDetail">
          <a-descriptions :column="2" bordered size="small">
            <a-descriptions-item label="日志ID">{{ currentDetail.logId }}</a-descriptions-item>
            <a-descriptions-item label="操作人">{{ currentDetail.operatorName }}（{{ currentDetail.operatorId }}）</a-descriptions-item>
            <a-descriptions-item label="角色">
              <a-tag :color="roleColor(currentDetail.operatorRole)">{{ currentDetail.operatorRole }}</a-tag>
            </a-descriptions-item>
            <a-descriptions-item label="来源上下文">{{ currentDetail.sourceContext }}</a-descriptions-item>
            <a-descriptions-item label="动作">{{ currentDetail.action }}</a-descriptions-item>
            <a-descriptions-item label="资源类型">{{ currentDetail.resourceType }}</a-descriptions-item>
            <a-descriptions-item label="资源ID" :span="2">{{ currentDetail.resourceId }}</a-descriptions-item>
            <a-descriptions-item label="响应状态">
              <a-tag :color="statusColor(currentDetail.responseStatus)">{{ currentDetail.responseStatus }}</a-tag>
            </a-descriptions-item>
            <a-descriptions-item label="发生时间">{{ formatDateTime(currentDetail.occurredAt) }}</a-descriptions-item>
            <a-descriptions-item label="IP" :span="2">{{ currentDetail.ipAddress }}</a-descriptions-item>
            <a-descriptions-item label="User-Agent" :span="2">{{ currentDetail.userAgent }}</a-descriptions-item>
            <a-descriptions-item label="TraceId" :span="2">
              <code class="trace-id">{{ currentDetail.traceId }}</code>
            </a-descriptions-item>
          </a-descriptions>

          <div class="snapshot-section">
            <div class="snapshot-title">请求摘要（敏感字段已掩码）</div>
            <JsonViewer :data="maskedSnapshot(currentDetail.requestSummary)" :max-height="200" />
          </div>
          <div class="snapshot-section">
            <div class="snapshot-title">操作前快照（敏感字段已掩码）</div>
            <JsonViewer :data="maskedSnapshot(currentDetail.beforeSnapshot)" :max-height="280" />
          </div>
          <div class="snapshot-section">
            <div class="snapshot-title">操作后快照（敏感字段已掩码）</div>
            <JsonViewer :data="maskedSnapshot(currentDetail.afterSnapshot)" :max-height="280" />
          </div>
        </template>
        <EmptyState v-else-if="!drawerLoading" description="审计日志条目不存在" />
      </a-spin>
    </a-drawer>
  </div>
</template>

<style scoped>
.audit-logs .page-header { background: var(--n1, #fff); border-radius: 8px; padding: 24px; margin-bottom: 16px; box-shadow: 0 1px 2px rgba(0,0,0,.03); }
.audit-logs .page-title { font-size: 20px; font-weight: 600; margin-bottom: 4px; }
.audit-logs .page-desc { color: #8C8C8C; }
.audit-logs .toolbar { display: flex; gap: 12px; margin-bottom: 16px; flex-wrap: wrap; align-items: center; }
.audit-logs .spacer { flex: 1; }
.audit-logs .snapshot-section { margin-top: 16px; }
.audit-logs .snapshot-title { font-size: 14px; font-weight: 500; margin-bottom: 8px; color: #595959; }
.audit-logs .trace-id { font-family: 'SF Mono', 'Cascadia Code', Consolas, monospace; font-size: 12px; word-break: break-all; }
</style>
