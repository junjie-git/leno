<!-- web/system-admin/src/modules/04-runtime-ops/views/DeadLetterQueue.vue -->
<!-- 死信队列：统计+筛选+批量操作+表格+详情抽屉+重投/丢弃确认 -->
<script setup lang="ts">
import { ref, reactive, computed, onMounted } from 'vue'
import { message } from 'ant-design-vue'
import {
  ReloadOutlined, DeleteOutlined, EyeOutlined,
} from '@ant-design/icons-vue'
import dayjs from 'dayjs'
import { deadLetterApi } from '../api/dead-letters.api'
import type {
  DeadLetterMessageDto,
  DeadLetterStatus,
  BatchOperationResultDto,
} from '../types/dead-letter.dto'
import StatusTag from '@/shared/components/StatusTag.vue'
import ConfirmDialog from '@/shared/components/ConfirmDialog.vue'
import DateTimeRangePicker from '@/shared/components/DateTimeRangePicker.vue'
import PermissionGuard from '@/shared/auth/PermissionGuard.vue'
import EmptyState from '@/shared/components/EmptyState.vue'
import JsonViewer from '@/shared/components/JsonViewer.vue'
import { BusinessError } from '@/shared/http/errors'

const sourceOptions = ['Order', 'Payment', 'Notification', 'Inventory', 'Review', 'AfterSales', 'Points', 'Membership']
const statusOptions: { label: string; value: DeadLetterStatus }[] = [
  { label: '待处理', value: 'Pending' },
  { label: '已重投', value: 'Retried' },
  { label: '已丢弃', value: 'Discarded' },
]

const loading = ref(false)
const dataList = ref<DeadLetterMessageDto[]>([])
const total = ref(0)
const selectedRowKeys = ref<string[]>([])
const filter = reactive<{ sourceContext: string[]; status: DeadLetterStatus[]; range: [string, string] | null; page: number; pageSize: number }>({
  sourceContext: [],
  status: ['Pending'],
  range: null,
  page: 1,
  pageSize: 20,
})

const stats = reactive({ pending: 0, retriedToday: 0, discardedToday: 0, backlogQueues: 0 })

const detailVisible = ref(false)
const detailLoading = ref(false)
const detail = ref<DeadLetterMessageDto | null>(null)

const retryConfirm = ref(false)
const discardConfirm = ref(false)
const discardReason = ref('')
const discardTargetId = ref<string | null>(null)
const batchMode = ref<'single' | 'batch'>('single')

const batchResultVisible = ref(false)
const batchResult = ref<BatchOperationResultDto | null>(null)
const batchResultKind = ref<'retry' | 'discard'>('retry')

const columns = computed(() => [
  { title: '消息 ID', dataIndex: 'messageId', key: 'messageId', width: 180, ellipsis: true },
  { title: '来源', dataIndex: 'sourceContext', key: 'sourceContext', width: 100 },
  { title: '原始主题', dataIndex: 'originalTopic', key: 'originalTopic', width: 160, ellipsis: true },
  { title: '失败原因', dataIndex: 'errorReason', key: 'errorReason', ellipsis: true },
  { title: '重试', dataIndex: 'retryCount', key: 'retryCount', width: 70, align: 'right' as const },
  { title: '状态', key: 'status', width: 90 },
  { title: '进入时间', dataIndex: 'failedAt', key: 'failedAt', width: 150 },
  { title: '操作', key: 'action', width: 200, fixed: 'right' as const },
])

async function loadList() {
  loading.value = true
  try {
    const params = {
      sourceContext: filter.sourceContext.length ? filter.sourceContext : undefined,
      status: filter.status.length ? filter.status : undefined,
      startTime: filter.range?.[0],
      endTime: filter.range?.[1],
      page: filter.page,
      pageSize: filter.pageSize,
    }
    const res = await deadLetterApi.list(params)
    dataList.value = res.items
    total.value = res.total
    // 统计简化：从当前页与总数推导（后端如提供独立统计端点可替换）
    stats.pending = res.items.filter((i) => i.status === 'Pending').length
    const today = dayjs().format('YYYY-MM-DD')
    stats.retriedToday = res.items.filter((i) => i.status === 'Retried' && dayjs(i.operatedAt ?? i.failedAt).format('YYYY-MM-DD') === today).length
    stats.discardedToday = res.items.filter((i) => i.status === 'Discarded' && dayjs(i.operatedAt ?? i.failedAt).format('YYYY-MM-DD') === today).length
    stats.backlogQueues = new Set(res.items.map((i) => i.deadLetterQueue)).size
  } catch (e) {
    if (e instanceof BusinessError) message.error(e.message)
    else message.error('加载死信列表失败')
  } finally {
    loading.value = false
  }
}

function onSearch() {
  filter.page = 1
  selectedRowKeys.value = []
  loadList()
}

const rowSelection = computed(() => ({
  selectedRowKeys: selectedRowKeys.value,
  onChange: (keys: string[]) => { selectedRowKeys.value = keys },
}))

async function openDetail(record: DeadLetterMessageDto) {
  detail.value = record
  detailVisible.value = true
  detailLoading.value = true
  try {
    detail.value = await deadLetterApi.get(record.messageId)
  } catch (e) {
    if (e instanceof BusinessError) message.error(e.message)
  } finally {
    detailLoading.value = false
  }
}

function askRetrySingle(record: DeadLetterMessageDto) {
  batchMode.value = 'single'
  selectedRowKeys.value = [record.messageId]
  retryConfirm.value = true
}

function askRetryBatch() {
  if (selectedRowKeys.value.length === 0) return message.warning('请先选择消息')
  if (selectedRowKeys.value.length > 100) return message.warning('批量操作 ≤ 100 条/次')
  batchMode.value = 'batch'
  retryConfirm.value = true
}

function askDiscardSingle(record: DeadLetterMessageDto) {
  batchMode.value = 'single'
  discardTargetId.value = record.messageId
  selectedRowKeys.value = [record.messageId]
  discardReason.value = ''
  discardConfirm.value = true
}

function askDiscardBatch() {
  if (selectedRowKeys.value.length === 0) return message.warning('请先选择消息')
  if (selectedRowKeys.value.length > 100) return message.warning('批量操作 ≤ 100 条/次')
  batchMode.value = 'batch'
  discardReason.value = ''
  discardConfirm.value = true
}

async function onConfirmRetry() {
  const ids = selectedRowKeys.value
  try {
    if (batchMode.value === 'single' && ids[0]) {
      await deadLetterApi.retry(ids[0])
      message.success('已重投')
      retryConfirm.value = false
    } else {
      const result = await deadLetterApi.batchRetry(ids)
      batchResult.value = result
      batchResultKind.value = 'retry'
      batchResultVisible.value = true
      retryConfirm.value = false
    }
    selectedRowKeys.value = []
    loadList()
  } catch (e) {
    if (e instanceof BusinessError) message.info(e.message)
    else message.error('重投失败')
  }
}

async function onConfirmDiscard() {
  if (!discardReason.value.trim()) {
    message.error('丢弃原因为必填项')
    return
  }
  const ids = selectedRowKeys.value
  try {
    if (batchMode.value === 'single' && ids[0]) {
      await deadLetterApi.discard(ids[0], { discardReason: discardReason.value.trim() })
      message.success('已丢弃')
      discardConfirm.value = false
    } else {
      const result = await deadLetterApi.batchDiscard(ids, discardReason.value.trim())
      batchResult.value = result
      batchResultKind.value = 'discard'
      batchResultVisible.value = true
      discardConfirm.value = false
    }
    selectedRowKeys.value = []
    loadList()
  } catch (e) {
    if (e instanceof BusinessError) message.error(e.message)
    else message.error('丢弃失败')
  }
}

function onPageChange(page: number, pageSize: number) {
  filter.page = page
  filter.pageSize = pageSize
  loadList()
}

onMounted(loadList)
</script>

<template>
  <div class="runtime-ops-dead-letter">
    <div class="page-header">
      <div class="page-title">死信队列</div>
      <div class="page-desc">跨域汇聚各 MQ 死信消息，查看详情、单条或批量重投、丢弃处置。重投与丢弃幂等；丢弃不可逆，需填理由。</div>
      <a-button style="position: absolute; right: 24px; top: 24px" @click="loadList">
        <ReloadOutlined />刷新
      </a-button>
    </div>

    <div class="stats-row">
      <div class="stat-mini"><div class="stat-mini-label">待处理死信</div><div class="stat-mini-value" style="color:#FAAD14">{{ stats.pending }}</div></div>
      <div class="stat-mini"><div class="stat-mini-label">今日已重投</div><div class="stat-mini-value" style="color:#1677FF">{{ stats.retriedToday }}</div></div>
      <div class="stat-mini"><div class="stat-mini-label">今日已丢弃</div><div class="stat-mini-value" style="color:#8C8C8C">{{ stats.discardedToday }}</div></div>
      <div class="stat-mini"><div class="stat-mini-label">积压队列数</div><div class="stat-mini-value">{{ stats.backlogQueues }}</div></div>
    </div>

    <div class="toolbar">
      <a-select
        v-model:value="filter.sourceContext"
        mode="multiple"
        placeholder="全部来源"
        allow-clear
        style="min-width: 220px"
        :options="sourceOptions.map((v) => ({ label: v, value: v }))"
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
    </div>

    <div v-if="selectedRowKeys.length > 0" class="batch-bar">
      <span>已选中 <b style="color:#1677FF">{{ selectedRowKeys.length }}</b> 条消息</span>
      <div class="spacer" />
      <PermissionGuard permission="dead-letter:dispose">
        <a-button type="primary" size="small" @click="askRetryBatch">
          <ReloadOutlined />批量重投
        </a-button>
      </PermissionGuard>
      <PermissionGuard permission="dead-letter:dispose">
        <a-button danger size="small" @click="askDiscardBatch">
          <DeleteOutlined />批量丢弃
        </a-button>
      </PermissionGuard>
    </div>

    <a-table
      :columns="columns"
      :data-source="dataList"
      :loading="loading"
      row-key="messageId"
      size="middle"
      :row-selection="rowSelection"
      :pagination="{
        current: filter.page,
        pageSize: filter.pageSize,
        total,
        showSizeChanger: true,
        onChange: onPageChange,
      }"
    >
      <template #emptyText>
        <EmptyState description="暂无死信消息" action-text="刷新" @action="loadList" />
      </template>
      <template #bodyCell="{ column, record }">
        <template v-if="column.key === 'sourceContext'">
          <a-tag color="purple">{{ record.sourceContext }}</a-tag>
        </template>
        <template v-else-if="column.key === 'errorReason'">
          <span style="color:#FF4D4F; font-size:12px">{{ record.errorReason }}</span>
        </template>
        <template v-else-if="column.key === 'status'">
          <StatusTag type="deadLetter" :status="record.status" />
        </template>
        <template v-else-if="column.key === 'action'">
          <a-space size="small">
            <a-button type="link" size="small" @click="openDetail(record)">
              <EyeOutlined />详情
            </a-button>
            <PermissionGuard permission="dead-letter:dispose">
              <a-button
                v-if="record.status === 'Pending'"
                type="link"
                size="small"
                @click="askRetrySingle(record)"
              >
                <ReloadOutlined />重投
              </a-button>
            </PermissionGuard>
            <PermissionGuard permission="dead-letter:dispose">
              <a-button
                v-if="record.status === 'Pending'"
                type="link"
                size="small"
                danger
                @click="askDiscardSingle(record)"
              >
                <DeleteOutlined />丢弃
              </a-button>
            </PermissionGuard>
          </a-space>
        </template>
      </template>
    </a-table>

    <a-drawer
      v-model:open="detailVisible"
      title="死信消息详情"
      width="720"
      placement="right"
    >
      <a-spin :spinning="detailLoading">
        <template v-if="detail">
          <a-descriptions :column="1" bordered size="small">
            <a-descriptions-item label="消息 ID"><span class="mono">{{ detail.messageId }}</span></a-descriptions-item>
            <a-descriptions-item label="原始消息 ID"><span class="mono">{{ detail.originalMessageId }}</span></a-descriptions-item>
            <a-descriptions-item label="来源上下文"><a-tag color="purple">{{ detail.sourceContext }}</a-tag></a-descriptions-item>
            <a-descriptions-item label="原始主题"><span class="mono">{{ detail.originalTopic }}</span></a-descriptions-item>
            <a-descriptions-item label="原始队列"><span class="mono">{{ detail.originalQueue }}</span></a-descriptions-item>
            <a-descriptions-item label="死信队列"><span class="mono">{{ detail.deadLetterQueue }}</span></a-descriptions-item>
            <a-descriptions-item label="失败原因"><span style="color:#FF4D4F">{{ detail.errorReason }}</span></a-descriptions-item>
            <a-descriptions-item label="进入死信时间">{{ detail.failedAt }}</a-descriptions-item>
            <a-descriptions-item label="重试次数"><b>{{ detail.retryCount }}</b> 次</a-descriptions-item>
            <a-descriptions-item label="状态"><StatusTag type="deadLetter" :status="detail.status" /></a-descriptions-item>
            <a-descriptions-item label="操作人">{{ detail.operatorId ?? '—' }}</a-descriptions-item>
            <a-descriptions-item v-if="detail.discardReason" label="丢弃原因">{{ detail.discardReason }}</a-descriptions-item>
          </a-descriptions>

          <div class="section-title">消息头（Headers）</div>
          <JsonViewer :data="detail.headers" :max-height="280" />

          <div class="section-title">原始消息体（Payload）</div>
          <JsonViewer :data="(() => { try { return JSON.parse(detail.payload) } catch { return detail.payload } })()" :max-height="280" />

          <div class="section-title">处置历史</div>
          <div class="history-list">
            <div v-for="(item, idx) in detail.history" :key="idx" class="history-item">
              <div class="history-dot" :class="{ retry: item.action === 'Retry', discard: item.action === 'Discard' }" />
              <div class="history-content">
                <div class="history-action">
                  <template v-if="item.action === 'Retry'">重投到原队列</template>
                  <template v-else-if="item.action === 'Discard'">丢弃消息</template>
                  <template v-else>消息进入死信队列</template>
                </div>
                <div class="history-meta">
                  操作人 {{ item.operator ?? '系统' }} · {{ item.operatedAt }} · 结果：{{ item.result }}
                </div>
              </div>
            </div>
          </div>

          <a-alert
            v-if="detail.retryCount >= 2 && detail.status === 'Pending'"
            type="warning"
            show-icon
            style="margin-top: 16px"
            :message="`该消息已重试 ${detail.retryCount} 次仍进入死信，建议检查下游服务日志后再决定重投或丢弃。`"
          />
        </template>
      </a-spin>
    </a-drawer>

    <ConfirmDialog
      v-model:open="retryConfirm"
      title="确认重投死信消息"
      :content="`即将重投 ${selectedRowKeys.length} 条消息。重投后消息将重新投递到原队列，可能触发重复业务逻辑。已重投或已丢弃的消息幂等返回当前状态。`"
      :danger="false"
      ok-text="确认重投"
      cancel-text="取消"
      @confirm="onConfirmRetry"
    />

    <ConfirmDialog
      v-model:open="discardConfirm"
      title="确认丢弃死信消息"
      :content="`即将丢弃 ${selectedRowKeys.length} 条消息。丢弃后该消息将永久不再处理，关联业务可能丢失。此操作不可逆。`"
      :danger="true"
      :require-input="{ label: '丢弃原因', placeholder: '请填写丢弃原因，将记录至审计日志', min: 1, max: 500 }"
      :input-value="discardReason"
      ok-text="确认丢弃"
      cancel-text="取消"
      @input-change="(v: string) => (discardReason = v)"
      @confirm="onConfirmDiscard"
    />

    <a-modal
      v-model:open="batchResultVisible"
      :title="batchResultKind === 'retry' ? '批量重投结果' : '批量丢弃结果'"
      width="520"
      ok-text="知道了"
      :cancel-button-props="{ style: { display: 'none' } }"
    >
      <template v-if="batchResult">
        <div class="result-summary" :class="{ partial: batchResult.failed.length > 0 }">
          <div>
            <div class="result-num ok">{{ batchResult.succeeded.length }}</div>
            <div class="result-label">成功</div>
          </div>
          <div>
            <div class="result-num fail">{{ batchResult.failed.length }}</div>
            <div class="result-label">失败</div>
          </div>
        </div>
        <div v-if="batchResult.failed.length > 0" class="section-title">失败明细</div>
        <div v-if="batchResult.failed.length > 0" class="fail-list">
          <div v-for="f in batchResult.failed" :key="f.messageId" class="fail-list-item">
            <span class="fail-id">{{ f.messageId }}</span>
            <span class="fail-reason">— {{ f.reason }}</span>
          </div>
        </div>
      </template>
    </a-modal>
  </div>
</template>

<style scoped>
.runtime-ops-dead-letter .page-header { position: relative; background: var(--n1, #fff); border-radius: 8px; padding: 24px; margin-bottom: 16px; box-shadow: 0 1px 2px rgba(0,0,0,.03); }
.runtime-ops-dead-letter .page-title { font-size: 20px; font-weight: 600; margin-bottom: 4px; }
.runtime-ops-dead-letter .page-desc { color: #8C8C8C; max-width: 760px; }
.runtime-ops-dead-letter .stats-row { display: flex; gap: 12px; margin-bottom: 16px; }
.runtime-ops-dead-letter .stat-mini { flex: 1; background: #fff; border-radius: 8px; box-shadow: 0 1px 2px rgba(0,0,0,.03); padding: 16px; }
.runtime-ops-dead-letter .stat-mini-label { font-size: 12px; color: #8C8C8C; }
.runtime-ops-dead-letter .stat-mini-value { font-size: 24px; font-weight: 600; margin-top: 4px; }
.runtime-ops-dead-letter .toolbar { display: flex; gap: 12px; margin-bottom: 16px; flex-wrap: wrap; align-items: center; }
.runtime-ops-dead-letter .spacer { flex: 1; }
.runtime-ops-dead-letter .batch-bar { display: flex; align-items: center; gap: 12px; padding: 12px 16px; background: #e6f4ff; border: 1px solid #91caff; border-radius: 6px; margin-bottom: 16px; }
.runtime-ops-dead-letter .section-title { font-size: 14px; font-weight: 500; margin: 16px 0 8px; display: flex; align-items: center; gap: 8px; }
.runtime-ops-dead-letter .mono { font-family: "SF Mono","Cascadia Code",Consolas,monospace; font-size: 12px; }
.runtime-ops-dead-letter .history-list { border: 1px solid #f0f0f0; border-radius: 6px; padding: 12px 16px; }
.runtime-ops-dead-letter .history-item { display: flex; gap: 12px; padding: 8px 0; border-bottom: 1px solid #f0f0f0; }
.runtime-ops-dead-letter .history-item:last-child { border-bottom: none; }
.runtime-ops-dead-letter .history-dot { width: 8px; height: 8px; border-radius: 50%; margin-top: 6px; flex-shrink: 0; background: #FAAD14; }
.runtime-ops-dead-letter .history-dot.retry { background: #1677FF; }
.runtime-ops-dead-letter .history-dot.discard { background: #8C8C8C; }
.runtime-ops-dead-letter .history-action { font-size: 13px; font-weight: 500; }
.runtime-ops-dead-letter .history-meta { font-size: 12px; color: #8C8C8C; margin-top: 2px; }
.runtime-ops-dead-letter .result-summary { display: flex; gap: 32px; padding: 12px 16px; border-radius: 6px; margin-bottom: 12px; background: #f6ffed; border: 1px solid #b7eb8f; }
.runtime-ops-dead-letter .result-summary.partial { background: #fffbe6; border-color: #ffe58f; }
.runtime-ops-dead-letter .result-num { font-size: 20px; font-weight: 600; }
.runtime-ops-dead-letter .result-num.ok { color: #52C41A; }
.runtime-ops-dead-letter .result-num.fail { color: #FF4D4F; }
.runtime-ops-dead-letter .result-label { font-size: 12px; color: #8C8C8C; }
.runtime-ops-dead-letter .fail-list { background: #f5f5f5; border-radius: 6px; padding: 12px; font-size: 12px; color: #595959; }
.runtime-ops-dead-letter .fail-list-item { padding: 4px 0; display: flex; gap: 8px; align-items: center; }
.runtime-ops-dead-letter .fail-id { font-family: "SF Mono",Consolas,monospace; color: #000000D9; }
.runtime-ops-dead-letter .fail-reason { color: #FF4D4F; }
</style>
