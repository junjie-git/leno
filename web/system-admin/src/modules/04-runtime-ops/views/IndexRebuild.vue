<!-- web/system-admin/src/modules/04-runtime-ops/views/IndexRebuild.vue -->
<!-- 索引重建：列表 + 触发弹窗 + 详情抽屉（进度条 + 轮询） -->
<script setup lang="ts">
import { ref, reactive, computed, onMounted, onBeforeUnmount } from 'vue'
import { message } from 'ant-design-vue'
import {
  PlusOutlined, EyeOutlined, ReloadOutlined,
} from '@ant-design/icons-vue'
import { indexRebuildApi } from '../api/index-rebuilds.api'
import type {
  IndexRebuildTaskDto,
  IndexRebuildStatus,
  TriggerIndexRebuildDto,
} from '../types/index-rebuild.dto'
import StatusTag from '@/shared/components/StatusTag.vue'
import ConfirmDialog from '@/shared/components/ConfirmDialog.vue'
import PermissionGuard from '@/shared/auth/PermissionGuard.vue'
import EmptyState from '@/shared/components/EmptyState.vue'
import { BusinessError } from '@/shared/http/errors'

const contextOptions = ['Product', 'Order', 'Shop', 'Review', 'AfterSales', 'Points', 'Membership', 'UserCenter']
const statusOptions: { label: string; value: IndexRebuildStatus }[] = [
  { label: '待执行', value: 'Pending' },
  { label: '执行中', value: 'Running' },
  { label: '成功', value: 'Succeeded' },
  { label: '失败', value: 'Failed' },
]

const loading = ref(false)
const dataList = ref<IndexRebuildTaskDto[]>([])
const total = ref(0)
const filter = reactive<{ targetContext: string[]; status: IndexRebuildStatus[]; page: number; pageSize: number }>({
  targetContext: [],
  status: [],
  page: 1,
  pageSize: 20,
})

const triggerVisible = ref(false)
const triggerForm = reactive<TriggerIndexRebuildDto>({ targetContext: 'Product', indexName: '' })
const triggerSubmitting = ref(false)

const detailVisible = ref(false)
const detailLoading = ref(false)
const detail = ref<IndexRebuildTaskDto | null>(null)

const confirmVisible = ref(false)
const confirmTask = ref<IndexRebuildTaskDto | null>(null)
const confirmKind = ref<'trigger' | 'retry'>('trigger')

let pollTimer: ReturnType<typeof setInterval> | null = null

const columns = computed(() => [
  { title: '任务 ID', dataIndex: 'taskId', key: 'taskId', width: 160, ellipsis: true },
  { title: '上下文', dataIndex: 'targetContext', key: 'targetContext', width: 110 },
  { title: '索引名', dataIndex: 'indexName', key: 'indexName', width: 140 },
  { title: '状态', key: 'status', width: 100 },
  { title: '进度', key: 'progress', width: 160 },
  { title: '触发人', dataIndex: 'triggeredBy', key: 'triggeredBy', width: 110 },
  { title: '触发时间', dataIndex: 'triggeredAt', key: 'triggeredAt', width: 160 },
  { title: '操作', key: 'action', width: 180, fixed: 'right' as const },
])

function computeProgress(task: IndexRebuildTaskDto): number {
  if (task.totalDocs <= 0) return task.status === 'Succeeded' ? 100 : 0
  return Math.min(100, Math.floor((task.processedDocs / task.totalDocs) * 100))
}

async function loadList() {
  loading.value = true
  try {
    const params = {
      targetContext: filter.targetContext.length ? filter.targetContext : undefined,
      status: filter.status.length ? filter.status : undefined,
      page: filter.page,
      pageSize: filter.pageSize,
    }
    const res = await indexRebuildApi.list(params)
    dataList.value = res.items
    total.value = res.total
    schedulePollRunning()
  } catch (e) {
    if (e instanceof BusinessError) message.error(e.message)
    else message.error('加载索引重建任务失败')
  } finally {
    loading.value = false
  }
}

function schedulePollRunning() {
  if (pollTimer) clearInterval(pollTimer)
  const hasRunning = dataList.value.some((t) => t.status === 'Running')
  if (!hasRunning) return
  pollTimer = setInterval(async () => {
    const running = dataList.value.filter((t) => t.status === 'Running')
    for (const task of running) {
      try {
        const fresh = await indexRebuildApi.get(task.taskId)
        Object.assign(task, fresh)
      } catch {
        // 单条失败不阻塞其他轮询
      }
    }
    if (!dataList.value.some((t) => t.status === 'Running') && pollTimer) {
      clearInterval(pollTimer)
      pollTimer = null
    }
  }, 5000)
}

function onSearch() {
  filter.page = 1
  loadList()
}

function openTrigger() {
  Object.assign(triggerForm, { targetContext: 'Product', indexName: '' })
  confirmKind.value = 'trigger'
  confirmVisible.value = true
}

const confirmTitle = computed(() =>
  confirmKind.value === 'trigger' ? '确认触发索引重建' : '确认重试索引重建任务')
const confirmContent = computed(() =>
  confirmKind.value === 'trigger'
    ? '重建期间查询走旧索引，切换瞬间有秒级双读窗口。增量事件暂存补偿，重建完成后回放。'
    : '重试将重新执行索引重建，期间查询走旧索引。原任务记录保留。')

async function onConfirm() {
  if (confirmKind.value === 'trigger') {
    if (!triggerForm.indexName.trim()) {
      message.error('索引名必填')
      return
    }
    triggerSubmitting.value = true
    try {
      await indexRebuildApi.trigger({
        targetContext: triggerForm.targetContext,
        indexName: triggerForm.indexName.trim(),
      })
      message.success('重建任务已触发')
      confirmVisible.value = false
      loadList()
    } catch (e) {
      if (e instanceof BusinessError) message.error(e.message)
      else message.error('触发失败')
    } finally {
      triggerSubmitting.value = false
    }
  } else if (confirmTask.value) {
    try {
      await indexRebuildApi.retry(confirmTask.value.taskId)
      message.success('已重新加入队列')
      confirmVisible.value = false
      loadList()
    } catch (e) {
      if (e instanceof BusinessError) message.error(e.message)
      else message.error('重试失败')
    }
  }
}

async function openDetail(task: IndexRebuildTaskDto) {
  detail.value = task
  detailVisible.value = true
  detailLoading.value = true
  try {
    detail.value = await indexRebuildApi.get(task.taskId)
  } catch (e) {
    if (e instanceof BusinessError) message.error(e.message)
  } finally {
    detailLoading.value = false
  }
}

function askRetry(task: IndexRebuildTaskDto) {
  confirmTask.value = task
  confirmKind.value = 'retry'
  confirmVisible.value = true
}

function onPageChange(page: number, pageSize: number) {
  filter.page = page
  filter.pageSize = pageSize
  loadList()
}

onMounted(loadList)
onBeforeUnmount(() => {
  if (pollTimer) clearInterval(pollTimer)
})
</script>

<template>
  <div class="runtime-ops-index-rebuild">
    <div class="page-header">
      <div class="page-title">索引重建</div>
      <div class="page-desc">触发各域 ES 读库全量索引重建，跟踪任务进度，重试失败任务。执行中任务每 5s 自动刷新进度。</div>
    </div>

    <div class="toolbar">
      <a-select
        v-model:value="filter.targetContext"
        mode="multiple"
        placeholder="目标上下文"
        allow-clear
        style="min-width: 220px"
        :options="contextOptions.map((v) => ({ label: v, value: v }))"
      />
      <a-select
        v-model:value="filter.status"
        mode="multiple"
        placeholder="状态"
        allow-clear
        style="min-width: 200px"
        :options="statusOptions"
      />
      <a-button type="primary" @click="onSearch">筛选</a-button>
      <div class="spacer" />
      <PermissionGuard permission="index-rebuild:trigger">
        <a-button type="primary" @click="openTrigger">
          <PlusOutlined />触发重建
        </a-button>
      </PermissionGuard>
    </div>

    <a-table
      :columns="columns"
      :data-source="dataList"
      :loading="loading"
      row-key="taskId"
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
        <EmptyState description="暂无重建任务" action-text="触发重建" @action="openTrigger" />
      </template>
      <template #bodyCell="{ column, record }">
        <template v-if="column.key === 'status'">
          <StatusTag type="indexRebuild" :status="record.status" />
        </template>
        <template v-else-if="column.key === 'progress'">
          <a-progress :percent="computeProgress(record)" size="small" :status="record.status === 'Failed' ? 'exception' : record.status === 'Succeeded' ? 'success' : 'active'" />
        </template>
        <template v-else-if="column.key === 'action'">
          <a-space size="small">
            <a-button type="link" size="small" @click="openDetail(record)">
              <EyeOutlined />详情
            </a-button>
            <PermissionGuard permission="index-rebuild:trigger">
              <a-button
                v-if="record.status === 'Failed'"
                type="link"
                size="small"
                @click="askRetry(record)"
              >
                <ReloadOutlined />重试
              </a-button>
            </PermissionGuard>
          </a-space>
        </template>
      </template>
    </a-table>

    <!-- 触发重建弹窗（嵌入 ConfirmDialog 内的表单） -->
    <a-modal
      v-model:open="triggerVisible"
      title="触发索引重建"
      width="480"
      :confirm-loading="triggerSubmitting"
      ok-text="触发"
      cancel-text="取消"
      @ok="onConfirm"
    >
      <a-alert
        type="info"
        message="重建期间查询走旧索引，切换瞬间有秒级双读窗口。增量事件暂存补偿，重建完成后回放。"
        show-icon
        style="margin-bottom: 16px"
      />
      <a-form layout="vertical">
        <a-form-item label="目标上下文" required>
          <a-select v-model:value="triggerForm.targetContext" :options="contextOptions.map((v) => ({ label: v, value: v }))" />
        </a-form-item>
        <a-form-item label="索引名" required>
          <a-input v-model:value="triggerForm.indexName" placeholder="products / orders / shops" />
        </a-form-item>
      </a-form>
    </a-modal>

    <ConfirmDialog
      v-model:open="confirmVisible"
      :title="confirmTitle"
      :content="confirmContent"
      :danger="false"
      ok-text="确认"
      cancel-text="取消"
      @confirm="onConfirm"
    />

    <a-drawer
      v-model:open="detailVisible"
      title="索引重建任务详情"
      width="640"
      placement="right"
    >
      <a-spin :spinning="detailLoading">
        <template v-if="detail">
          <a-descriptions :column="1" bordered size="small">
            <a-descriptions-item label="任务 ID"><span class="mono">{{ detail.taskId }}</span></a-descriptions-item>
            <a-descriptions-item label="目标上下文">{{ detail.targetContext }}</a-descriptions-item>
            <a-descriptions-item label="索引名"><span class="mono">{{ detail.indexName }}</span></a-descriptions-item>
            <a-descriptions-item label="状态">
              <StatusTag type="indexRebuild" :status="detail.status" />
            </a-descriptions-item>
            <a-descriptions-item label="进度">
              <a-progress :percent="computeProgress(detail)" :status="detail.status === 'Failed' ? 'exception' : detail.status === 'Succeeded' ? 'success' : 'active'" />
              <div style="margin-top: 4px; color: #8C8C8C; font-size: 12px">
                {{ detail.processedDocs }} / {{ detail.totalDocs }} 文档
              </div>
            </a-descriptions-item>
            <a-descriptions-item label="触发人">{{ detail.triggeredBy }}</a-descriptions-item>
            <a-descriptions-item label="触发时间">{{ detail.triggeredAt }}</a-descriptions-item>
            <a-descriptions-item label="开始时间">{{ detail.startedAt ?? '—' }}</a-descriptions-item>
            <a-descriptions-item label="结束时间">{{ detail.finishedAt ?? '—' }}</a-descriptions-item>
            <a-descriptions-item label="重试次数">{{ detail.retryCount }}</a-descriptions-item>
            <a-descriptions-item v-if="detail.errorMessage" label="失败原因">
              <span style="color: #FF4D4F">{{ detail.errorMessage }}</span>
            </a-descriptions-item>
          </a-descriptions>
          <a-alert
            v-if="detail.status === 'Running'"
            type="info"
            message="重建期间查询走旧索引，切换瞬间有秒级双读窗口。"
            show-icon
            style="margin-top: 16px"
          />
        </template>
      </a-spin>
    </a-drawer>
  </div>
</template>

<style scoped>
.runtime-ops-index-rebuild .page-header { background: var(--n1, #fff); border-radius: 8px; padding: 24px; margin-bottom: 16px; box-shadow: 0 1px 2px rgba(0,0,0,.03); }
.runtime-ops-index-rebuild .page-title { font-size: 20px; font-weight: 600; margin-bottom: 4px; }
.runtime-ops-index-rebuild .page-desc { color: #8C8C8C; }
.runtime-ops-index-rebuild .toolbar { display: flex; gap: 12px; margin-bottom: 16px; flex-wrap: wrap; align-items: center; }
.runtime-ops-index-rebuild .spacer { flex: 1; }
.runtime-ops-index-rebuild .mono { font-family: "SF Mono","Cascadia Code",Consolas,monospace; font-size: 12px; }
</style>
