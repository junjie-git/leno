<!-- web/system-admin/src/modules/05-audit/views/OutboxMonitor.vue -->
<!-- Outbox 监控：统计条 + 趋势折线 + 按域表格 + 详情抽屉 + 重投/归档确认，每 60s 轮询 -->
<script setup lang="ts">
import { ref, reactive, computed, onMounted, onBeforeUnmount } from 'vue'
import { message, notification } from 'ant-design-vue'
import {
  InboxOutlined, ReloadOutlined, FileZipOutlined, EyeOutlined, WarningOutlined,
} from '@ant-design/icons-vue'
import dayjs from 'dayjs'
import type { EChartsOption } from 'echarts'
import { outboxMonitorApi } from '../api/outbox-monitor.api'
import type { BatchRepublishResultDto } from '../api/outbox-monitor.api'
import type {
  OutboxSummaryDto,
  OutboxTrendPointDto,
  OutboxMessageDto,
  OutboxArchiveHistoryDto,
  OutboxStatus,
  BatchRepublishOutboxDto,
  ArchiveOutboxDto,
} from '../types/outbox.dto'
import ChartLine from '@/shared/components/charts/ChartLine.vue'
import ConfirmDialog from '@/shared/components/ConfirmDialog.vue'
import PermissionGuard from '@/shared/auth/PermissionGuard.vue'
import EmptyState from '@/shared/components/EmptyState.vue'
import JsonViewer from '@/shared/components/JsonViewer.vue'
import { formatDateTime } from '@/shared/utils/format'

const summaryLoading = ref(false)
const summary = ref<OutboxSummaryDto[]>([])

const trendLoading = ref(false)
const trendData = ref<OutboxTrendPointDto[]>([])

// 统计条聚合
const totalPending = computed(() => summary.value.reduce((acc, s) => acc + s.pendingCount, 0))
const backlogContextCount = computed(() => summary.value.filter((s) => s.status !== 'Normal' && s.status !== 'Archived').length)
const maxAgeMinutes = computed(() => summary.value.reduce((acc, s) => Math.max(acc, s.maxAgeMinutes), 0))
const todayRepublishCount = ref(0)

// 严重积压阈值（design-prompt §4 分支流程）
const SEVERE_THRESHOLD = 1000

function statusLabel(s: OutboxStatus): string {
  switch (s) {
    case 'Normal': return '正常'
    case 'Backlog': return '积压'
    case 'Severe': return '严重积压'
    case 'Archived': return '已归档'
    default: return s
  }
}
function statusColor(s: OutboxStatus): 'success' | 'warning' | 'error' | 'default' {
  switch (s) {
    case 'Normal': return 'success'
    case 'Backlog': return 'warning'
    case 'Severe': return 'error'
    case 'Archived': return 'default'
    default: return 'default'
  }
}

// 趋势图数据：将 OutboxTrendPointDto[] 转换为 ChartLine 期望的 series + xAxis
const trendXAxis = computed<string[]>(() => {
  const timestamps = Array.from(new Set(trendData.value.map((p) => p.timestamp))).sort()
  return timestamps.map((ts) => dayjs(ts).format('MM-DD HH:mm'))
})

const trendSeries = computed<EChartsOption['series']>(() => {
  const contexts = Array.from(new Set(trendData.value.map((p) => p.context)))
  const timestamps = Array.from(new Set(trendData.value.map((p) => p.timestamp))).sort()
  return contexts.map((ctx) => {
    const data = timestamps.map((ts) => {
      const point = trendData.value.find((p) => p.timestamp === ts && p.context === ctx)
      return point ? point.pendingCount : 0
    })
    return { name: ctx, type: 'line' as const, data }
  })
})

const hasTrendData = computed(() => Array.isArray(trendSeries.value) && trendSeries.value.length > 0)

// 按积压数倒序
const sortedSummary = computed(() =>
  [...summary.value].sort((a, b) => b.pendingCount - a.pendingCount),
)

const columns = computed(() => [
  { title: '限界上下文', dataIndex: 'context', key: 'context', width: 140 },
  { title: '未发布事件数', dataIndex: 'pendingCount', key: 'pendingCount', width: 130, align: 'right' as const },
  { title: '最早事件时间', dataIndex: 'oldestEventAt', key: 'oldestEventAt', width: 180, customRender: ({ text }: { text: string | null }) => text ? formatDateTime(text) : '—' },
  { title: '最大积压时长(分钟)', dataIndex: 'maxAgeMinutes', key: 'maxAgeMinutes', width: 160, align: 'right' as const },
  { title: '最近归档时间', dataIndex: 'lastArchivedAt', key: 'lastArchivedAt', width: 180, responsive: ['xl'] as const, customRender: ({ text }: { text: string | null }) => text ? formatDateTime(text) : '—' },
  { title: '状态', key: 'status', width: 110 },
  { title: '操作', key: 'action-col', width: 180, fixed: 'right' as const },
])

// 详情抽屉
const drawerVisible = ref(false)
const drawerLoading = ref(false)
const drawerContext = ref<string>('')
const drawerMessages = ref<OutboxMessageDto[]>([])
const drawerArchiveHistory = ref<OutboxArchiveHistoryDto[]>([])

// 重投确认
const republishConfirmVisible = ref(false)
const republishContext = ref<string>('')
const republishing = ref(false)

// 归档确认（含表单：olderThanMinutes + reason），使用 a-modal 以支持 slot
const archiveModalVisible = ref(false)
const archiveContext = ref<string>('')
const archiveForm = reactive<{ olderThanMinutes: number; reason: string }>({
  olderThanMinutes: 60,
  reason: '',
})
const archiving = ref(false)

// 轮询定时器
let pollTimer: ReturnType<typeof setInterval> | null = null

async function loadSummary(): Promise<void> {
  summaryLoading.value = true
  try {
    const res = await outboxMonitorApi.getSummary()
    summary.value = res.data
    // 严重积压自动标红 + 通知（design-prompt §4 分支流程）
    const severe = res.data.find((s) => s.pendingCount > SEVERE_THRESHOLD)
    if (severe) {
      notification.warning({
        message: 'Outbox 严重积压',
        description: `限界上下文 ${severe.context} 积压 ${severe.pendingCount} 条事件，超过阈值 ${SEVERE_THRESHOLD}，请及时处置。`,
        duration: 5,
      })
    }
  } catch {
    // 后端规划中，API 可能 404；静默处理避免轮询刷屏
    summary.value = []
  } finally {
    summaryLoading.value = false
  }
}

async function loadTrend(): Promise<void> {
  trendLoading.value = true
  try {
    const res = await outboxMonitorApi.getTrend({ hours: 24 })
    trendData.value = res.data
  } catch {
    trendData.value = []
  } finally {
    trendLoading.value = false
  }
}

async function loadAll(): Promise<void> {
  await Promise.all([loadSummary(), loadTrend()])
}

async function openDetail(record: OutboxSummaryDto): Promise<void> {
  drawerVisible.value = true
  drawerLoading.value = true
  drawerContext.value = record.context
  drawerMessages.value = []
  drawerArchiveHistory.value = []
  try {
    const [msgRes, historyRes] = await Promise.all([
      outboxMonitorApi.listMessages({ context: record.context, page: 1, pageSize: 50 }),
      outboxMonitorApi.getArchiveHistory(record.context),
    ])
    drawerMessages.value = msgRes.data.items
    drawerArchiveHistory.value = historyRes.data
  } catch (e: unknown) {
    const msg = e instanceof Error ? e.message : '加载积压事件详情失败'
    message.error(msg)
  } finally {
    drawerLoading.value = false
  }
}

function openRepublishConfirm(record: OutboxSummaryDto): void {
  republishContext.value = record.context
  republishConfirmVisible.value = true
}

async function onConfirmRepublish(): Promise<void> {
  // 防止对话框停留期间重复点击确认
  if (republishing.value) return
  republishing.value = true
  try {
    const body: BatchRepublishOutboxDto = { maxCount: 100 }
    const res = await outboxMonitorApi.republish(republishContext.value, body)
    republishConfirmVisible.value = false
    const result: BatchRepublishResultDto = res.data
    if (result.failed.length === 0) {
      message.success(`已重投 ${result.succeeded.length} 条积压事件`)
    } else {
      // 部分失败：弹窗显示成功/失败明细（design-prompt §4 分支流程）
      const firstFailure = result.failed[0]
      notification.warning({
        message: '重投部分失败',
        description: `成功 ${result.succeeded.length} 条，失败 ${result.failed.length} 条。失败原因：${firstFailure?.reason ?? '未知'}`,
        duration: 6,
      })
    }
    todayRepublishCount.value += result.succeeded.length
    await loadAll()
  } catch (e: unknown) {
    const msg = e instanceof Error ? e.message : '重投失败'
    message.error(msg)
  } finally {
    republishing.value = false
  }
}

function onCancelRepublish(): void {
  republishConfirmVisible.value = false
}

function openArchiveConfirm(record: OutboxSummaryDto): void {
  archiveContext.value = record.context
  archiveForm.olderThanMinutes = 60
  archiveForm.reason = ''
  archiveModalVisible.value = true
}

async function onConfirmArchive(): Promise<void> {
  // 防止对话框停留期间重复点击确认
  if (archiving.value) return
  if (!archiveForm.reason.trim()) {
    message.warning('请填写归档原因')
    return
  }
  if (archiveForm.olderThanMinutes <= 0) {
    message.warning('归档阈值必须 > 0 分钟')
    return
  }
  archiving.value = true
  try {
    const body: ArchiveOutboxDto = {
      olderThanMinutes: archiveForm.olderThanMinutes,
      reason: archiveForm.reason.trim(),
    }
    const res = await outboxMonitorApi.archive(archiveContext.value, body)
    archiveModalVisible.value = false
    message.success(`已归档 ${res.data.archivedCount} 条陈旧积压事件`)
    await loadAll()
  } catch (e: unknown) {
    const msg = e instanceof Error ? e.message : '归档失败'
    message.error(msg)
  } finally {
    archiving.value = false
  }
}

function onCancelArchive(): void {
  archiveModalVisible.value = false
}

function rowClassName(record: OutboxSummaryDto): string {
  if (record.pendingCount > SEVERE_THRESHOLD) return 'outbox-row-severe'
  if (record.status === 'Backlog') return 'outbox-row-backlog'
  return ''
}

onMounted(() => {
  loadAll()
  // 每 60s 轮询刷新汇总（design-prompt §4 主流程）
  pollTimer = setInterval(() => {
    loadSummary()
  }, 60_000)
})

onBeforeUnmount(() => {
  if (pollTimer) {
    clearInterval(pollTimer)
    pollTimer = null
  }
})
</script>

<template>
  <div class="outbox-monitor">
    <div class="page-header">
      <div class="page-title">Outbox 监控</div>
      <div class="page-desc">监控各域 Outbox 发件箱积压情况，按限界上下文查看未发布事件数量与积压时长，触发积压告警处置（重投/归档），保障集成事件最终一致。</div>
    </div>

    <a-alert
      type="info"
      show-icon
      message="后端 Outbox 监控端点规划中"
      description="design-prompt 标记此页端点待后端实现。当前 API 层与视图已按契约完整实现，后端就绪后即可直接使用；端点未就绪时数据为空。"
      style="margin-bottom: 16px"
    />

    <!-- 统计条 -->
    <a-row :gutter="24" class="stats-row">
      <a-col :xs="24" :sm="12" :xl="6">
        <a-card :loading="summaryLoading" class="stat-card">
          <a-statistic
            title="总积压事件数"
            :value="totalPending"
            :value-style="{ color: totalPending > SEVERE_THRESHOLD ? '#FF4D4F' : '#595959' }"
          >
            <template #prefix><InboxOutlined /></template>
          </a-statistic>
        </a-card>
      </a-col>
      <a-col :xs="24" :sm="12" :xl="6">
        <a-card :loading="summaryLoading" class="stat-card">
          <a-statistic title="积压域数量" :value="backlogContextCount" :value-style="{ color: backlogContextCount > 0 ? '#FAAD14' : '#595959' }" />
        </a-card>
      </a-col>
      <a-col :xs="24" :sm="12" :xl="6">
        <a-card :loading="summaryLoading" class="stat-card">
          <a-statistic title="最大积压时长(分钟)" :value="maxAgeMinutes" :value-style="{ color: maxAgeMinutes > 30 ? '#FF4D4F' : '#595959' }" />
        </a-card>
      </a-col>
      <a-col :xs="24" :sm="12" :xl="6">
        <a-card class="stat-card">
          <a-statistic title="今日重投次数" :value="todayRepublishCount" />
        </a-card>
      </a-col>
    </a-row>

    <!-- 积压趋势图 -->
    <a-card title="近 24h 积压趋势" class="trend-card">
      <a-spin :spinning="trendLoading">
        <ChartLine
          v-if="hasTrendData"
          :series="trendSeries"
          :x-axis="trendXAxis"
          :height="300"
        />
        <EmptyState v-else-if="!trendLoading" description="暂无积压趋势数据" action-text="刷新" @action="loadTrend" />
      </a-spin>
    </a-card>

    <!-- 按域分组表格 -->
    <a-card title="按域积压明细" class="table-card">
      <a-table
        :columns="columns"
        :data-source="sortedSummary"
        :loading="summaryLoading"
        row-key="context"
        size="middle"
        :scroll="{ x: 1100 }"
        :row-class-name="rowClassName"
        :pagination="false"
      >
        <template #emptyText>
          <EmptyState description="暂无积压事件，所有域 Outbox 正常" action-text="刷新" @action="loadAll" />
        </template>
        <template #bodyCell="{ column, record }">
          <template v-if="column.key === 'status'">
            <a-tag :color="statusColor(record.status)">
              <WarningOutlined v-if="record.status === 'Severe'" />{{ statusLabel(record.status) }}
            </a-tag>
          </template>
          <template v-else-if="column.key === 'action-col'">
            <a-space>
              <a-button type="link" size="small" @click="openDetail(record)">
                <EyeOutlined />详情
              </a-button>
              <PermissionGuard permission="outbox:manage">
                <a-button type="link" size="small" @click="openRepublishConfirm(record)">
                  <ReloadOutlined />重投
                </a-button>
                <a-button type="link" size="small" danger @click="openArchiveConfirm(record)">
                  <FileZipOutlined />归档
                </a-button>
              </PermissionGuard>
            </a-space>
          </template>
        </template>
      </a-table>
    </a-card>

    <!-- 详情抽屉 -->
    <a-drawer
      v-model:open="drawerVisible"
      :title="`Outbox 积压详情 - ${drawerContext}`"
      placement="right"
      :width="720"
    >
      <a-spin :spinning="drawerLoading">
        <div class="drawer-section">
          <div class="drawer-title">积压事件列表（{{ drawerMessages.length }} 条）</div>
          <a-table
            v-if="drawerMessages.length > 0"
            :columns="[
              { title: '事件ID', dataIndex: 'messageId', key: 'messageId', width: 140, ellipsis: true },
              { title: '聚合ID', dataIndex: 'aggregateId', key: 'aggregateId', width: 140, ellipsis: true },
              { title: '事件类型', dataIndex: 'eventType', key: 'eventType', width: 160 },
              { title: '创建时间', dataIndex: 'createdAt', key: 'createdAt', width: 180, customRender: ({ text }: { text: string }) => formatDateTime(text) },
              { title: '重试次数', dataIndex: 'retryCount', key: 'retryCount', width: 90, align: 'right' as const },
            ]"
            :data-source="drawerMessages"
            row-key="messageId"
            size="small"
            :pagination="{ pageSize: 10 }"
          >
            <template #expandedRowRender="{ record }">
              <div class="payload-section">
                <div class="payload-title">Payload</div>
                <JsonViewer :data="record.payload" :max-height="240" />
              </div>
            </template>
          </a-table>
          <EmptyState v-else description="暂无积压事件" />
        </div>

        <div class="drawer-section">
          <div class="drawer-title">归档历史</div>
          <a-table
            v-if="drawerArchiveHistory.length > 0"
            :columns="[
              { title: '归档时间', dataIndex: 'archivedAt', key: 'archivedAt', width: 180, customRender: ({ text }: { text: string }) => formatDateTime(text) },
              { title: '归档数', dataIndex: 'count', key: 'count', width: 90, align: 'right' as const },
              { title: '原因', dataIndex: 'reason', key: 'reason' },
              { title: '操作人', dataIndex: 'archivedBy', key: 'archivedBy', width: 120 },
            ]"
            :data-source="drawerArchiveHistory"
            row-key="archivedAt"
            size="small"
            :pagination="false"
          />
          <EmptyState v-else description="暂无归档历史" />
        </div>
      </a-spin>
    </a-drawer>

    <!-- 重投确认（ConfirmDialog：仅文案 + 主色确认） -->
    <ConfirmDialog
      :open="republishConfirmVisible"
      :danger="false"
      title="确认重投积压事件"
      content="重投后积压事件将重新发布到事件总线，可能触发重复消费。订阅者需保证幂等。是否继续？"
      @confirm="onConfirmRepublish"
      @cancel="onCancelRepublish"
    />

    <!-- 归档确认（a-modal：danger 红色 + 表单：阈值 + 原因） -->
    <a-modal
      :open="archiveModalVisible"
      title="归档陈旧积压事件"
      ok-text="归档"
      cancel-text="取消"
      :confirm-loading="archiving"
      :ok-button-props="{ danger: true }"
      @ok="onConfirmArchive"
      @cancel="onCancelArchive"
    >
      <a-alert
        type="warning"
        show-icon
        message="归档后陈旧积压事件将从监控视图移除并转入归档存储，不再自动重投。此操作可查询归档历史，但需手动恢复。"
        style="margin-bottom: 16px"
      />
      <a-form layout="vertical">
        <a-form-item label="归档阈值（积压时长超过此分钟数）" required>
          <a-input-number
            v-model:value="archiveForm.olderThanMinutes"
            :min="1"
            :max="10080"
            style="width: 100%"
            placeholder="例如 60 表示归档积压超过 1 小时的事件"
          />
        </a-form-item>
        <a-form-item label="归档原因" required>
          <a-textarea
            v-model:value="archiveForm.reason"
            :rows="3"
            :maxlength="500"
            show-count
            placeholder="请填写归档原因（1-500 字）"
          />
        </a-form-item>
      </a-form>
    </a-modal>
  </div>
</template>

<style scoped>
.outbox-monitor .page-header { background: var(--n1, #fff); border-radius: 8px; padding: 24px; margin-bottom: 16px; box-shadow: 0 1px 2px rgba(0,0,0,.03); }
.outbox-monitor .page-title { font-size: 20px; font-weight: 600; margin-bottom: 4px; }
.outbox-monitor .page-desc { color: #8C8C8C; }
.outbox-monitor .stats-row { margin-bottom: 16px; }
.outbox-monitor .stat-card { border-radius: 8px; }
.outbox-monitor .trend-card { margin-bottom: 16px; border-radius: 8px; }
.outbox-monitor .table-card { border-radius: 8px; }
.outbox-monitor .drawer-section { margin-bottom: 24px; }
.outbox-monitor .drawer-title { font-size: 14px; font-weight: 500; margin-bottom: 12px; color: #595959; }
.outbox-monitor .payload-section { background: #FAFAFA; padding: 12px; border-radius: 4px; }
.outbox-monitor .payload-title { font-size: 12px; color: #8C8C8C; margin-bottom: 8px; }
.outbox-monitor :deep(.outbox-row-severe) { background-color: #FFF1F0; }
.outbox-monitor :deep(.outbox-row-severe:hover) > td { background-color: #FFCCC7 !important; }
.outbox-monitor :deep(.outbox-row-backlog) { background-color: #FFFBE6; }
.outbox-monitor :deep(.outbox-row-backlog:hover) > td { background-color: #FFF1B8 !important; }
</style>
