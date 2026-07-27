<!-- web/system-admin/src/modules/04-runtime-ops/views/AlertManagement.vue -->
<!-- 告警管理：统计+筛选+表格+详情抽屉+确认/静默弹窗+30s 轮询 -->
<script setup lang="ts">
import { ref, reactive, computed, onMounted, onBeforeUnmount } from 'vue'
import { message } from 'ant-design-vue'
import {
  WarningOutlined, ExclamationCircleOutlined, InfoCircleOutlined, BellOutlined,
} from '@ant-design/icons-vue'
import { alertApi, alertSilenceApi } from '../api/alerts.api'
import type {
  AlertDto,
  AlertSeverity,
  AlertStatus,
  SilenceDto,
  CreateSilenceDto,
  AcknowledgeAlertDto,
} from '../types/alert.dto'
import StatusTag from '@/shared/components/StatusTag.vue'
import DateTimeRangePicker from '@/shared/components/DateTimeRangePicker.vue'
import ConfirmDialog from '@/shared/components/ConfirmDialog.vue'
import PermissionGuard from '@/shared/auth/PermissionGuard.vue'
import EmptyState from '@/shared/components/EmptyState.vue'
import JsonViewer from '@/shared/components/JsonViewer.vue'
import { BusinessError } from '@/shared/http/errors'

const moduleOptions = [
  'Identity', 'AccessControl', 'UserCenter', 'Points', 'Membership', 'Review',
  'AfterSales', 'Product', 'Order', 'Payment', 'Notification', 'Inventory', 'SystemAdmin',
]
const severityOptions: { label: string; value: AlertSeverity }[] = [
  { label: 'critical', value: 'critical' },
  { label: 'warning', value: 'warning' },
  { label: 'info', value: 'info' },
]
const statusOptions: { label: string; value: AlertStatus }[] = [
  { label: 'firing', value: 'firing' },
  { label: 'acknowledged', value: 'acknowledged' },
  { label: 'resolved', value: 'resolved' },
]

const loading = ref(false)
const dataList = ref<AlertDto[]>([])
const total = ref(0)
const filter = reactive<{ module: string[]; severity: AlertSeverity[]; status: AlertStatus[]; range: [string, string] | null; page: number; pageSize: number }>({
  module: [],
  severity: [],
  status: ['firing'],
  range: null,
  page: 1,
  pageSize: 20,
})

const stats = reactive({ pending: 0, critical: 0, todayTotal: 0, avgAckDurationSec: 0 })

const detailVisible = ref(false)
const detail = ref<AlertDto | null>(null)

const ackVisible = ref(false)
const ackTarget = ref<AlertDto | null>(null)
const ackComment = ref('')

const silenceVisible = ref(false)
const silenceForm = reactive<CreateSilenceDto>({
  matchers: [{ name: 'module', value: '', isRegex: false }],
  durationMinutes: 60,
  reason: '',
})

const silenceList = ref<SilenceDto[]>([])
const silenceListVisible = ref(false)

const confirmSilence = ref(false)

let pollTimer: ReturnType<typeof setInterval> | null = null

const columns = computed(() => [
  { title: '告警 ID', dataIndex: 'alertId', key: 'alertId', width: 140, ellipsis: true },
  { title: '名称', dataIndex: 'name', key: 'name', width: 160 },
  { title: '模块', dataIndex: 'module', key: 'module', width: 110 },
  { title: '级别', key: 'severity', width: 100 },
  { title: '状态', key: 'status', width: 110 },
  { title: '触发时间', dataIndex: 'triggeredAt', key: 'triggeredAt', width: 160 },
  { title: '持续时长', key: 'duration', width: 110 },
  { title: '操作', key: 'action', width: 220, fixed: 'right' as const },
])

function formatDuration(sec: number): string {
  if (sec < 60) return `${sec}s`
  if (sec < 3600) return `${Math.floor(sec / 60)}m`
  return `${Math.floor(sec / 3600)}h ${Math.floor((sec % 3600) / 60)}m`
}

async function loadList() {
  loading.value = true
  try {
    const params = {
      module: filter.module.length ? filter.module : undefined,
      severity: filter.severity.length ? filter.severity : undefined,
      status: filter.status.length ? filter.status : undefined,
      startTime: filter.range?.[0],
      endTime: filter.range?.[1],
      page: filter.page,
      pageSize: filter.pageSize,
    }
    const res = await alertApi.list(params)
    dataList.value = res.items
    total.value = res.total
    stats.pending = res.items.filter((i) => i.status === 'firing').length
    stats.critical = res.items.filter((i) => i.severity === 'critical').length
    stats.todayTotal = res.total
    stats.avgAckDurationSec = res.items.length > 0
      ? Math.floor(res.items.reduce((s, i) => s + i.durationSeconds, 0) / res.items.length)
      : 0
  } catch (e) {
    if (e instanceof BusinessError) message.error(e.message)
    else message.error('加载告警列表失败')
  } finally {
    loading.value = false
  }
}

function onSearch() {
  filter.page = 1
  loadList()
}

async function openDetail(record: AlertDto) {
  detail.value = record
  detailVisible.value = true
  try {
    detail.value = await alertApi.get(record.alertId)
  } catch (e) {
    if (e instanceof BusinessError) message.error(e.message)
  }
}

function openAck(record: AlertDto) {
  ackTarget.value = record
  ackComment.value = ''
  ackVisible.value = true
}

async function onSubmitAck() {
  if (!ackTarget.value) return
  if (!ackComment.value.trim()) return message.error('注释必填')
  try {
    const body: AcknowledgeAlertDto = { comment: ackComment.value.trim() }
    await alertApi.acknowledge(ackTarget.value.alertId, body)
    message.success('已确认告警')
    ackVisible.value = false
    loadList()
  } catch (e) {
    if (e instanceof BusinessError) message.error(e.message)
    else message.error('确认失败')
  }
}

function openSilence() {
  Object.assign(silenceForm, {
    matchers: [{ name: 'module', value: '', isRegex: false }],
    durationMinutes: 60,
    reason: '',
  })
  silenceVisible.value = true
}

function addMatcher() {
  silenceForm.matchers.push({ name: '', value: '', isRegex: false })
}
function removeMatcher(idx: number) {
  if (silenceForm.matchers.length <= 1) return message.warning('至少保留一个匹配器')
  silenceForm.matchers.splice(idx, 1)
}

function askConfirmSilence() {
  if (!silenceForm.reason.trim()) return message.error('静默原因必填')
  if (silenceForm.matchers.some((m) => !m.name.trim() || !m.value.trim())) {
    return message.error('匹配器 name/value 必填')
  }
  confirmSilence.value = true
}

async function onSubmitSilence() {
  try {
    await alertSilenceApi.create({
      matchers: silenceForm.matchers.map((m) => ({ name: m.name.trim(), value: m.value.trim(), isRegex: m.isRegex })),
      durationMinutes: silenceForm.durationMinutes,
      reason: silenceForm.reason.trim(),
    })
    message.success('静默规则已创建')
    confirmSilence.value = false
    silenceVisible.value = false
  } catch (e) {
    if (e instanceof BusinessError) message.error(e.message)
    else message.error('创建静默规则失败')
  }
}

async function openSilenceList() {
  silenceListVisible.value = true
  try {
    silenceList.value = await alertSilenceApi.list()
  } catch (e) {
    if (e instanceof BusinessError) message.error(e.message)
    silenceList.value = []
  }
}

async function deleteSilence(id: string) {
  try {
    await alertSilenceApi.remove(id)
    message.success('已删除静默规则')
    silenceList.value = await alertSilenceApi.list()
  } catch (e) {
    if (e instanceof BusinessError) message.error(e.message)
  }
}

function severityIcon(sev: AlertSeverity) {
  return sev === 'critical' ? WarningOutlined : sev === 'warning' ? ExclamationCircleOutlined : InfoCircleOutlined
}
function severityColor(sev: AlertSeverity): string {
  return sev === 'critical' ? '#FF4D4F' : sev === 'warning' ? '#FAAD14' : '#1677FF'
}

function onPageChange(page: number, pageSize: number) {
  filter.page = page
  filter.pageSize = pageSize
  loadList()
}

onMounted(() => {
  loadList()
  pollTimer = setInterval(loadList, 30_000)
})
onBeforeUnmount(() => {
  if (pollTimer) clearInterval(pollTimer)
})
</script>

<template>
  <div class="runtime-ops-alert">
    <div class="page-header">
      <div class="page-title">告警管理</div>
      <div class="page-desc">查看 Alertmanager 告警事件，按模块与严重级别筛选，处置告警（确认/静默/转工单），追踪闭环。firing 状态每 30s 自动刷新。</div>
    </div>

    <a-alert
      type="info"
      message="告警管理功能规划中，API 待 SystemAdmin BC 实现 Alertmanager 集成"
      show-icon
      style="margin-bottom: 16px"
    />

    <div class="stats-row">
      <a-card size="small"><a-statistic title="待处置告警" :value="stats.pending" :value-style="{ color: '#FAAD14' }" /></a-card>
      <a-card size="small"><a-statistic title="严重告警" :value="stats.critical" :value-style="{ color: '#FF4D4F' }" /></a-card>
      <a-card size="small"><a-statistic title="今日告警总数" :value="stats.todayTotal" /></a-card>
      <a-card size="small"><a-statistic title="平均处置时长" :value="formatDuration(stats.avgAckDurationSec)" /></a-card>
    </div>

    <div class="toolbar">
      <a-select
        v-model:value="filter.module"
        mode="multiple"
        placeholder="模块"
        allow-clear
        style="min-width: 220px"
        :options="moduleOptions.map((v) => ({ label: v, value: v }))"
      />
      <a-select
        v-model:value="filter.severity"
        mode="multiple"
        placeholder="级别"
        allow-clear
        style="min-width: 180px"
        :options="severityOptions"
      />
      <a-select
        v-model:value="filter.status"
        mode="multiple"
        placeholder="状态"
        allow-clear
        style="min-width: 200px"
        :options="statusOptions"
      />
      <DateTimeRangePicker v-model="filter.range" />
      <a-button type="primary" @click="onSearch">筛选</a-button>
      <div class="spacer" />
      <PermissionGuard permission="alert:manage">
        <a-button @click="openSilenceList">
          <BellOutlined />查看静默规则
        </a-button>
      </PermissionGuard>
      <PermissionGuard permission="alert:manage">
        <a-button type="primary" @click="openSilence">
          <BellOutlined />创建静默规则
        </a-button>
      </PermissionGuard>
    </div>

    <a-table
      :columns="columns"
      :data-source="dataList"
      :loading="loading"
      row-key="alertId"
      size="middle"
      :pagination="{
        current: filter.page,
        pageSize: filter.pageSize,
        total,
        showSizeChanger: true,
        onChange: onPageChange,
      }"
    >
      <template #emptyText>
        <EmptyState description="暂无告警" action-text="刷新" @action="loadList" />
      </template>
      <template #bodyCell="{ column, record }">
        <template v-if="column.key === 'module'">
          <a-tag>{{ record.module }}</a-tag>
        </template>
        <template v-else-if="column.key === 'severity'">
          <component :is="severityIcon(record.severity)" :style="{ color: severityColor(record.severity), marginRight: '4px' }" />
          <span :style="{ color: severityColor(record.severity), fontWeight: 500 }">{{ record.severity }}</span>
        </template>
        <template v-else-if="column.key === 'status'">
          <StatusTag type="alert" :status="record.status" />
        </template>
        <template v-else-if="column.key === 'duration'">
          {{ formatDuration(record.durationSeconds) }}
        </template>
        <template v-else-if="column.key === 'action'">
          <a-space size="small">
            <a-button type="link" size="small" @click="openDetail(record)">详情</a-button>
            <PermissionGuard permission="alert:manage">
              <a-button
                type="link"
                size="small"
                :disabled="record.status === 'resolved'"
                @click="openAck(record)"
              >
                确认
              </a-button>
            </PermissionGuard>
            <PermissionGuard permission="alert:manage">
              <a-button
                type="link"
                size="small"
                :disabled="record.status === 'resolved'"
                @click="openSilence"
              >
                静默
              </a-button>
            </PermissionGuard>
          </a-space>
        </template>
      </template>
    </a-table>

    <a-drawer
      v-model:open="detailVisible"
      title="告警详情"
      width="720"
      placement="right"
    >
      <template v-if="detail">
        <a-descriptions :column="1" bordered size="small">
          <a-descriptions-item label="告警 ID"><span class="mono">{{ detail.alertId }}</span></a-descriptions-item>
          <a-descriptions-item label="名称">{{ detail.name }}</a-descriptions-item>
          <a-descriptions-item label="模块">{{ detail.module }}</a-descriptions-item>
          <a-descriptions-item label="级别">
            <component :is="severityIcon(detail.severity)" :style="{ color: severityColor(detail.severity), marginRight: '4px' }" />
            {{ detail.severity }}
          </a-descriptions-item>
          <a-descriptions-item label="状态"><StatusTag type="alert" :status="detail.status" /></a-descriptions-item>
          <a-descriptions-item label="触发时间">{{ detail.triggeredAt }}</a-descriptions-item>
          <a-descriptions-item label="持续时长">{{ formatDuration(detail.durationSeconds) }}</a-descriptions-item>
          <a-descriptions-item label="摘要">{{ detail.summary }}</a-descriptions-item>
          <a-descriptions-item label="描述">{{ detail.description }}</a-descriptions-item>
          <a-descriptions-item v-if="detail.relatedMetric" label="关联指标">
            <span class="mono">{{ detail.relatedMetric }}</span>
          </a-descriptions-item>
        </a-descriptions>

        <div class="section-title">标签（Labels）</div>
        <JsonViewer :data="detail.labels" :max-height="200" />

        <div class="section-title">注释（Annotations）</div>
        <JsonViewer :data="detail.annotations" :max-height="200" />
      </template>
    </a-drawer>

    <a-modal
      v-model:open="ackVisible"
      title="确认告警"
      width="480"
      ok-text="确认"
      cancel-text="取消"
      @ok="onSubmitAck"
    >
      <a-alert
        type="info"
        message="确认后告警状态变为已确认，不再触发通知（除非再次变为 firing）。"
        show-icon
        style="margin-bottom: 16px"
      />
      <a-form layout="vertical">
        <a-form-item label="注释" required>
          <a-textarea v-model:value="ackComment" :rows="4" placeholder="请输入确认注释，将记录至审计日志" />
        </a-form-item>
      </a-form>
    </a-modal>

    <a-modal
      v-model:open="silenceVisible"
      title="创建静默规则"
      width="560"
      ok-text="提交"
      cancel-text="取消"
      @ok="askConfirmSilence"
    >
      <a-form layout="vertical">
        <div class="section-title">匹配器</div>
        <div v-for="(m, idx) in silenceForm.matchers" :key="idx" class="matcher-row">
          <a-input v-model:value="m.name" placeholder="name（如 module）" style="width: 130px" />
          <a-input v-model:value="m.value" placeholder="value（如 Payment）" style="width: 180px" />
          <a-checkbox v-model:checked="m.isRegex">正则</a-checkbox>
          <a-button type="link" danger size="small" @click="removeMatcher(idx)">删除</a-button>
        </div>
        <a-button type="dashed" size="small" @click="addMatcher">+ 新增匹配器</a-button>

        <a-form-item label="持续时长（分钟）" required style="margin-top: 16px">
          <a-input-number v-model:value="silenceForm.durationMinutes" :min="1" :max="1440" style="width: 100%" />
        </a-form-item>
        <a-form-item label="原因" required>
          <a-textarea v-model:value="silenceForm.reason" :rows="3" placeholder="请填写静默原因" />
        </a-form-item>
      </a-form>
    </a-modal>

    <ConfirmDialog
      v-model:open="confirmSilence"
      title="确认创建静默规则"
      content="静默期间匹配的告警将不再通知，可能遗漏关键事件。请确认静默时长。"
      :danger="true"
      ok-text="确认静默"
      cancel-text="取消"
      @confirm="onSubmitSilence"
    />

    <a-drawer
      v-model:open="silenceListVisible"
      title="静默规则列表"
      width="640"
      placement="right"
    >
      <a-empty v-if="silenceList.length === 0" description="暂无静默规则" />
      <a-list v-else :data-source="silenceList" item-layout="horizontal">
        <template #renderItem="{ item }">
          <a-list-item>
            <a-list-item-meta>
              <template #title>
                <span class="mono">{{ item.matchers.map((m) => `${m.name}=${m.value}`).join(', ') }}</span>
              </template>
              <template #description>
                持续 {{ item.startsAt }} ~ {{ item.endsAt }}<br />
                原因：{{ item.reason }}<br />
                创建人：{{ item.createdBy }}
              </template>
            </a-list-item-meta>
            <template #actions>
              <a-button type="link" danger size="small" @click="deleteSilence(item.silenceId)">删除</a-button>
            </template>
          </a-list-item>
        </template>
      </a-list>
    </a-drawer>
  </div>
</template>

<style scoped>
.runtime-ops-alert .page-header { background: var(--n1, #fff); border-radius: 8px; padding: 24px; margin-bottom: 16px; box-shadow: 0 1px 2px rgba(0,0,0,.03); }
.runtime-ops-alert .page-title { font-size: 20px; font-weight: 600; margin-bottom: 4px; }
.runtime-ops-alert .page-desc { color: #8C8C8C; }
.runtime-ops-alert .stats-row { display: grid; grid-template-columns: repeat(4, 1fr); gap: 12px; margin-bottom: 16px; }
.runtime-ops-alert .toolbar { display: flex; gap: 12px; margin-bottom: 16px; flex-wrap: wrap; align-items: center; }
.runtime-ops-alert .spacer { flex: 1; }
.runtime-ops-alert .section-title { font-size: 14px; font-weight: 500; margin: 16px 0 8px; }
.runtime-ops-alert .mono { font-family: "SF Mono","Cascadia Code",Consolas,monospace; font-size: 12px; }
.runtime-ops-alert .matcher-row { display: flex; gap: 8px; align-items: center; margin-bottom: 8px; }
</style>
