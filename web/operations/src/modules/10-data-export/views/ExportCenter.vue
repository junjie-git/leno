<!-- web/operations/src/modules/10-data-export/views/ExportCenter.vue -->
<template>
  <div class="export-center">
    <!-- 区域 A：新建导出任务 -->
    <a-card :bordered="false" class="create-card" title="新建导出任务">
      <a-form layout="inline" class="create-form" @submit.prevent>
        <a-form-item label="业务类型">
          <a-select
            v-model:value="formState.businessType"
            :options="businessTypeOptions"
            style="width: 140px"
            @change="onBusinessTypeChange"
          />
        </a-form-item>
        <a-form-item label="时间范围">
          <a-range-picker
            v-model:value="formState.dateRange"
            :allow-clear="false"
            :disabled-date="disabledFutureDate"
            value-format="YYYY-MM-DD"
            style="width: 260px"
          />
        </a-form-item>
        <a-form-item label="关键词">
          <a-input
            v-model:value="formState.keyword"
            :placeholder="keywordPlaceholder"
            allow-clear
            style="width: 200px"
          />
        </a-form-item>
        <a-form-item label="状态">
          <a-select
            v-model:value="formState.status"
            :options="statusOptions"
            placeholder="全部状态"
            allow-clear
            style="width: 150px"
          />
        </a-form-item>
        <a-form-item>
          <IdempotencyButton type="primary" :loading="exporting" @click="onCreateTask">
            <DownloadOutlined />
            创建导出任务
          </IdempotencyButton>
        </a-form-item>
      </a-form>
      <div class="create-hints">
        <span>单任务上限 {{ formatNumber(EXPORT_MAX_ROWS) }} 行，超限自动截断并提示缩小时间范围；</span>
        <span v-if="!supportsTimeRange" class="hint-warning">
          当前业务类型后端列表端点不支持时间筛选，时间范围仅作为任务记录保留
        </span>
        <span v-else>任务文件保留 {{ EXPORT_RETENTION_DAYS }} 天，过期后需重新创建。</span>
      </div>
    </a-card>

    <!-- 区域 B：导出任务列表 -->
    <a-card :bordered="false" class="table-card">
      <div class="table-toolbar">
        <div class="toolbar-left">
          <span class="toolbar-title">导出任务</span>
          <span class="toolbar-hint">共 {{ tasks.length }} 个任务（本地保留 {{ EXPORT_RETENTION_DAYS }} 天）</span>
        </div>
        <a-button @click="reloadTasks">刷新</a-button>
      </div>

      <a-table
        :columns="columns"
        :data-source="tasks"
        :pagination="{ pageSize: 10, showSizeChanger: false }"
        row-key="id"
      >
        <template #emptyText>
          <EmptyState description="暂无导出任务" action-text="创建导出任务" @action="focusCreate" />
        </template>
        <template #bodyCell="{ column, record }">
          <template v-if="column.key === 'taskName'">
            <span class="task-name">{{ record.taskName }}</span>
          </template>
          <template v-else-if="column.key === 'businessType'">
            <a-tag color="blue" :aria-label="`业务类型 ${businessLabel(record.businessType)}`">
              {{ businessLabel(record.businessType) }}
            </a-tag>
          </template>
          <template v-else-if="column.key === 'range'">
            <span class="range-text">{{ formatDate(record.fromTime) }} ~ {{ formatDate(record.toTime) }}</span>
          </template>
          <template v-else-if="column.key === 'recordCount'">
            <span v-if="record.status === 'Failed'" class="text-muted">—</span>
            <span v-else class="record-count">{{ formatNumber(record.recordCount) }}</span>
          </template>
          <template v-else-if="column.key === 'status'">
            <div class="status-cell">
              <a-tag
                :color="EXPORT_TASK_STATUS_META[record.status as ExportTaskStatus].color"
                :aria-label="`任务状态 ${EXPORT_TASK_STATUS_META[record.status as ExportTaskStatus].label}`"
              >
                {{ EXPORT_TASK_STATUS_META[record.status as ExportTaskStatus].label }}
              </a-tag>
              <a-progress
                v-if="record.status === 'Processing'"
                :percent="record.progress"
                size="small"
                :stroke-color="{ from: '#1677FF', to: '#40A9FF' }"
                :aria-valuenow="record.progress"
              />
            </div>
          </template>
          <template v-else-if="column.key === 'createdAt'">
            <span class="time-text">{{ formatDateTime(record.createdAt) }}</span>
          </template>
          <template v-else-if="column.key === 'action'">
            <a-space>
              <a-button
                type="link"
                size="small"
                :disabled="record.status !== 'Completed'"
                aria-label="下载导出文件"
                @click="onDownload(record)"
              >
                下载
              </a-button>
              <a-button
                type="link"
                size="small"
                aria-label="查看任务详情"
                @click="onOpenDetail(record)"
              >
                详情
              </a-button>
              <a-button
                type="link"
                size="small"
                danger
                aria-label="删除导出任务"
                @click="onDelete(record)"
              >
                删除
              </a-button>
            </a-space>
          </template>
        </template>
      </a-table>
    </a-card>

    <!-- 区域 C：任务详情抽屉 -->
    <a-drawer
      :open="detailOpen"
      width="640"
      :title="detailTask ? `任务详情：${detailTask.taskName}` : '任务详情'"
      @close="detailOpen = false"
    >
      <a-descriptions v-if="detailTask" :column="2" bordered size="small">
        <a-descriptions-item label="任务名称" :span="2">{{ detailTask.taskName }}</a-descriptions-item>
        <a-descriptions-item label="业务类型">
          {{ businessLabel(detailTask.businessType) }}
        </a-descriptions-item>
        <a-descriptions-item label="状态">
          <a-tag :color="EXPORT_TASK_STATUS_META[detailTask.status].color">
            {{ EXPORT_TASK_STATUS_META[detailTask.status].label }}
          </a-tag>
        </a-descriptions-item>
        <a-descriptions-item label="时间范围" :span="2">
          {{ formatDateTime(detailTask.fromTime) }} ~ {{ formatDateTime(detailTask.toTime) }}
        </a-descriptions-item>
        <a-descriptions-item label="筛选关键词">
          {{ detailTask.filters.keyword || '全部' }}
        </a-descriptions-item>
        <a-descriptions-item label="筛选状态">
          {{ detailTask.filters.status || '全部' }}
        </a-descriptions-item>
        <a-descriptions-item label="记录数">
          {{ detailTask.status === 'Failed' ? '—' : formatNumber(detailTask.recordCount) }}
        </a-descriptions-item>
        <a-descriptions-item label="处理进度">
          <a-progress
            :percent="detailTask.progress"
            :status="detailTask.status === 'Failed' ? 'exception' : undefined"
            :aria-valuenow="detailTask.progress"
          />
        </a-descriptions-item>
        <a-descriptions-item label="创建人">{{ detailTask.createdBy }}</a-descriptions-item>
        <a-descriptions-item label="创建时间">{{ formatDateTime(detailTask.createdAt) }}</a-descriptions-item>
        <a-descriptions-item label="完成时间" :span="2">
          {{ detailTask.completedAt ? formatDateTime(detailTask.completedAt) : '—' }}
        </a-descriptions-item>
        <a-descriptions-item
          v-if="detailTask.errorMessage"
          label="失败原因"
          :span="2"
        >
          <span class="error-text">{{ detailTask.errorMessage }}</span>
        </a-descriptions-item>
        <a-descriptions-item label="文件有效期" :span="2">
          文件随任务记录保留 {{ EXPORT_RETENTION_DAYS }} 天；本地存储配额不足或已过期时下载按钮不可用，需重新创建任务。
        </a-descriptions-item>
      </a-descriptions>
    </a-drawer>

    <!-- 删除任务二次确认 -->
    <ConfirmDialog
      :open="deleteConfirmOpen"
      danger
      title="确认删除导出任务"
      :content="`删除「${pendingDelete?.taskName ?? ''}」后，任务记录与导出文件将一并清除，不可恢复。`"
      @confirm="onConfirmDelete"
      @cancel="deleteConfirmOpen = false"
    />
  </div>
</template>

<script setup lang="ts">
import { computed, onMounted, reactive, ref } from 'vue'
import { message } from 'ant-design-vue'
import type { TableColumnsType } from 'ant-design-vue'
import dayjs, { type Dayjs } from 'dayjs'
import { DownloadOutlined } from '@ant-design/icons-vue'
import { ConfirmDialog, EmptyState, IdempotencyButton } from '@/shared/components'
import { formatDate, formatDateTime, formatNumber } from '@/shared/utils/format'
import { useAuthStore } from '@/shared/auth/auth.store'
import {
  EXPORT_STATUS_OPTIONS,
  addExportTask,
  buildCsv,
  clearExpiredExportTasks,
  downloadTaskCsv,
  fetchExportRows,
  generateExportTaskId,
  hasRecentDuplicate,
  removeExportTask,
  updateExportTask,
} from '../api/export.api'
import {
  EXPORT_BUSINESS_TYPES,
  EXPORT_BUSINESS_TYPE_LABELS,
  EXPORT_MAX_ROWS,
  EXPORT_RETENTION_DAYS,
  EXPORT_TASK_STATUS_META,
  type ExportBusinessType,
  type ExportTaskRecord,
  type ExportTaskStatus,
} from '../types/export.dto'

/**
 * 导出中心（10-data-export）
 *
 * 降级方案（后端异步导出端点未上线）：
 * - 新建任务区：业务类型 / 时间范围（默认近 7 天）/ 动态筛选（关键词 + 状态）
 * - 创建任务 → 基于既有列表端点分页同步拉取（进度实时更新）→ 前端生成 CSV 自动下载
 * - 任务列表：状态 / 进度 / 下载 / 详情 / 删除，localStorage 记录历史（保留 7 天）
 * - 防重复：同业务类型同时间范围 5 分钟内不可重复创建
 * - 上限保护：单任务 10000 行，超限截断并提示缩小时间范围
 */

const auth = useAuthStore()

// ---------- 新建任务表单 ----------

interface CreateFormState {
  businessType: ExportBusinessType
  dateRange: [string, string]
  keyword: string
  status: string | undefined
}

const formState = reactive<CreateFormState>({
  businessType: 'Order',
  dateRange: [dayjs().subtract(6, 'day').format('YYYY-MM-DD'), dayjs().format('YYYY-MM-DD')],
  keyword: '',
  status: undefined,
})

const businessTypeOptions = EXPORT_BUSINESS_TYPES.map((t) => ({
  label: EXPORT_BUSINESS_TYPE_LABELS[t],
  value: t,
}))

const statusOptions = computed(() => EXPORT_STATUS_OPTIONS[formState.businessType])

/** 商品 / 卖家列表端点不支持时间范围筛选 */
const supportsTimeRange = computed(
  () => formState.businessType !== 'Product' && formState.businessType !== 'Seller',
)

const KEYWORD_PLACEHOLDERS: Record<ExportBusinessType, string> = {
  Order: '订单号',
  Payment: '支付单号',
  Refund: '退款编号',
  AfterSales: '售后单号',
  Product: '商品名称 / SKU',
  Notification: '用户 ID',
  Review: '商品名称',
  Seller: '店铺名称关键词',
}

const keywordPlaceholder = computed(() => `如 ${KEYWORD_PLACEHOLDERS[formState.businessType]}`)

function onBusinessTypeChange() {
  // 业务类型切换后状态枚举不同，清空已选状态避免脏值
  formState.status = undefined
}

function disabledFutureDate(current: Dayjs) {
  return current.isAfter(dayjs(), 'day')
}

function businessLabel(type: ExportBusinessType): string {
  return EXPORT_BUSINESS_TYPE_LABELS[type]
}

// ---------- 任务创建（同步拉取 + CSV 生成 + 自动下载） ----------

const exporting = ref(false)

async function onCreateTask() {
  const [fromDate, toDate] = formState.dateRange
  if (!fromDate || !toDate) {
    message.warning('请选择时间范围')
    return
  }
  // 时间范围转 ISO（起止含全天）
  const fromTime = dayjs(fromDate).startOf('day').toISOString()
  const toTime = dayjs(toDate).endOf('day').toISOString()

  if (hasRecentDuplicate(formState.businessType, fromTime, toTime)) {
    message.warning('同业务类型同时间范围的任务 5 分钟内已创建，请稍后再试或调整范围')
    return
  }

  const now = new Date()
  const record: ExportTaskRecord = {
    id: generateExportTaskId(now),
    taskName: `${businessLabel(formState.businessType)}导出 ${fromDate} ~ ${toDate}`,
    businessType: formState.businessType,
    fromTime,
    toTime,
    filters: {
      keyword: formState.keyword.trim() || undefined,
      status: formState.status,
    },
    status: 'Processing',
    recordCount: 0,
    progress: 0,
    csv: '',
    createdBy: auth.user?.nickname || auth.user?.username || '运营管理员',
    createdAt: now.toISOString(),
  }
  tasks.value = addExportTask(record)
  exporting.value = true

  try {
    const result = await fetchExportRows(formState.businessType, {
      fromTime,
      toTime,
      filters: record.filters,
      onProgress: (fetched, total) => {
        // 更新进度（完成前封顶 99%，避免进度条先到 100 再等 CSV 组装）
        record.recordCount = fetched
        record.progress = total > 0 ? Math.min(99, Math.round((fetched / total) * 100)) : 99
        tasks.value = updateExportTask({ ...record })
      },
    })

    record.status = 'Completed'
    record.recordCount = result.rows.length
    record.progress = 100
    record.csv = buildCsv(result.header, result.rows)
    record.completedAt = new Date().toISOString()
    tasks.value = updateExportTask({ ...record })

    // 自动触发下载（本地配额降级导致 csv 缺失时提示重建）
    if (!downloadTaskCsv(record)) {
      message.warning('导出完成，但本地存储配额不足未能保留文件，请缩小范围重新导出')
    } else if (result.truncated) {
      message.warning(
        `命中 ${formatNumber(result.total)} 条，已导出前 ${formatNumber(result.rows.length)} 行（上限 ${formatNumber(EXPORT_MAX_ROWS)} 行），请缩小时间范围分批导出`,
      )
    } else {
      message.success(`导出完成，共 ${formatNumber(result.rows.length)} 条记录，文件已开始下载`)
    }
  } catch (e) {
    record.status = 'Failed'
    record.errorMessage = e instanceof Error && e.message ? e.message : '导出失败，请重试'
    record.progress = 100
    tasks.value = updateExportTask({ ...record })
    message.error(`导出失败：${record.errorMessage}`)
  } finally {
    exporting.value = false
  }
}

// ---------- 任务列表 ----------

const columns: TableColumnsType = [
  { title: '任务名称', key: 'taskName', width: 220, ellipsis: true },
  { title: '业务类型', key: 'businessType', width: 100 },
  { title: '时间范围', key: 'range', width: 200 },
  { title: '记录数', key: 'recordCount', width: 90, align: 'right' },
  { title: '状态 / 进度', key: 'status', width: 200 },
  { title: '创建时间', key: 'createdAt', width: 170 },
  { title: '操作', key: 'action', width: 160, fixed: 'right' },
]

const tasks = ref<ExportTaskRecord[]>([])

/** 重新读取任务列表：先清理过期任务（7 天口径），返回按创建时间倒序的存活任务 */
function reloadTasks() {
  tasks.value = clearExpiredExportTasks()
}

function focusCreate() {
  // 空态 CTA：聚焦业务类型选择，引导新建任务
  const el = document.querySelector('.create-card .ant-select-selector') as HTMLElement | null
  el?.focus()
}

// ---------- 下载 / 详情 / 删除 ----------

function onDownload(record: ExportTaskRecord) {
  if (record.status !== 'Completed') return
  if (!downloadTaskCsv(record)) {
    message.warning('文件已过期（保留 7 天），请重新创建导出任务')
  }
}

const detailOpen = ref(false)
const detailTask = ref<ExportTaskRecord | null>(null)

function onOpenDetail(record: ExportTaskRecord) {
  // 展示抽屉时用列表内最新快照（进度可能仍在更新）
  detailTask.value = tasks.value.find((t) => t.id === record.id) ?? record
  detailOpen.value = true
}

const deleteConfirmOpen = ref(false)
const pendingDelete = ref<ExportTaskRecord | null>(null)

function onDelete(record: ExportTaskRecord) {
  pendingDelete.value = record
  deleteConfirmOpen.value = true
}

function onConfirmDelete() {
  deleteConfirmOpen.value = false
  const target = pendingDelete.value
  if (!target) return

  tasks.value = removeExportTask(target.id)
  message.success('导出任务已删除')
  pendingDelete.value = null
}

onMounted(() => {
  // 读取历史任务前先清理过期记录（7 天口径）
  reloadTasks()
})
</script>

<style scoped>
.export-center {
  display: flex;
  flex-direction: column;
  gap: 16px;
}

.create-card :deep(.ant-card-body),
.table-card :deep(.ant-card-body) {
  padding: 16px;
}

.create-form {
  row-gap: 12px;
}

.create-hints {
  margin-top: 12px;
  font-size: 12px;
  color: #8c8c8c;
  display: flex;
  flex-direction: column;
  gap: 4px;
}

.hint-warning {
  color: #faad14;
}

.table-toolbar {
  display: flex;
  align-items: center;
  justify-content: space-between;
  margin-bottom: 16px;
}

.toolbar-left {
  display: flex;
  align-items: center;
  gap: 12px;
}

.toolbar-title {
  font-size: 16px;
  font-weight: 600;
  color: rgba(0, 0, 0, 0.85);
}

.toolbar-hint {
  font-size: 12px;
  color: #595959;
}

.task-name {
  font-weight: 500;
  color: rgba(0, 0, 0, 0.85);
}

.range-text,
.time-text {
  font-size: 12px;
  color: #8c8c8c;
}

.record-count {
  font-variant-numeric: tabular-nums;
  color: rgba(0, 0, 0, 0.85);
}

.status-cell {
  display: flex;
  flex-direction: column;
  gap: 4px;
}

.text-muted {
  color: #8c8c8c;
}

.error-text {
  color: #ff4d4f;
}
</style>
