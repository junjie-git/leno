<!-- web/system-admin/src/modules/04-runtime-ops/views/ScheduledTasks.vue -->
<!-- 定时任务管理：列表 + 新建/编辑弹窗（作业类型编辑只读）+ 历史抽屉 + 启停/立即执行确认 -->
<script setup lang="ts">
import { ref, reactive, computed, onMounted } from 'vue'
import { message } from 'ant-design-vue'
import {
  PlusOutlined, EditOutlined, PlayCircleOutlined, HistoryOutlined,
} from '@ant-design/icons-vue'
import { scheduledTaskApi } from '../api/scheduled-tasks.api'
import type {
  ScheduledTaskDto,
  ScheduledTaskStatus,
  SaveScheduledTaskDto,
  UpdateScheduledTaskDto,
  ScheduledTaskExecutionDto,
} from '../types/scheduled-task.dto'
import StatusTag from '@/shared/components/StatusTag.vue'
import ConfirmDialog from '@/shared/components/ConfirmDialog.vue'
import PermissionGuard from '@/shared/auth/PermissionGuard.vue'
import EmptyState from '@/shared/components/EmptyState.vue'
import { BusinessError } from '@/shared/http/errors'
import { InfoCircleOutlined } from '@ant-design/icons-vue'

const jobTypeOptions = [
  'Reconciliation', 'Report', 'Cleanup', 'Notification', 'Sync', 'Snapshot',
]
const statusOptions: { label: string; value: ScheduledTaskStatus }[] = [
  { label: '启用', value: 'Enabled' },
  { label: '停用', value: 'Disabled' },
]

const loading = ref(false)
const dataList = ref<ScheduledTaskDto[]>([])
const total = ref(0)
const filter = reactive<{ name: string; status: ScheduledTaskStatus[]; jobType: string; page: number; pageSize: number }>({
  name: '',
  status: [],
  jobType: '',
  page: 1,
  pageSize: 20,
})

const modalVisible = ref(false)
const modalMode = ref<'create' | 'edit'>('create')
const form = reactive<{
  taskId?: string
  name: string
  jobType: string
  cronExpression: string
  parameters: string
}>({ name: '', jobType: 'Reconciliation', cronExpression: '0 2 * * *', parameters: '{}' })
const submitting = ref(false)

const historyVisible = ref(false)
const historyLoading = ref(false)
const historyList = ref<ScheduledTaskExecutionDto[]>([])
const historyTaskName = ref('')

const confirmVisible = ref(false)
const confirmAction = ref<{
  kind: 'enable' | 'disable' | 'runNow'
  task: ScheduledTaskDto
} | null>(null)

const columns = computed(() => [
  { title: '任务名', dataIndex: 'name', key: 'name', width: 160 },
  { title: 'Cron', dataIndex: 'cronExpression', key: 'cronExpression', width: 130 },
  { title: '作业类型', dataIndex: 'jobType', key: 'jobType', width: 130 },
  { title: '状态', key: 'status', width: 90 },
  { title: '最近执行', dataIndex: 'lastRunAt', key: 'lastRunAt', width: 160 },
  { title: '下次执行', dataIndex: 'nextRunAt', key: 'nextRunAt', width: 160 },
  { title: '操作', key: 'action', width: 280, fixed: 'right' as const },
])

function validateCron(cron: string): boolean {
  // 简单 5 段校验，详细校验由后端完成
  const parts = cron.trim().split(/\s+/)
  return parts.length === 5
}

async function loadList() {
  loading.value = true
  try {
    const params = {
      name: filter.name || undefined,
      status: filter.status.length ? filter.status : undefined,
      jobType: filter.jobType || undefined,
      page: filter.page,
      pageSize: filter.pageSize,
    }
    const res = await scheduledTaskApi.list(params)
    dataList.value = res.items
    total.value = res.total
  } catch (e) {
    if (e instanceof BusinessError) message.error(e.message)
    else message.error('加载定时任务失败')
  } finally {
    loading.value = false
  }
}

function onSearch() {
  filter.page = 1
  loadList()
}

function openCreate() {
  modalMode.value = 'create'
  Object.assign(form, { taskId: undefined, name: '', jobType: 'Reconciliation', cronExpression: '0 2 * * *', parameters: '{}' })
  modalVisible.value = true
}

function openEdit(task: ScheduledTaskDto) {
  modalMode.value = 'edit'
  Object.assign(form, {
    taskId: task.taskId,
    name: task.name,
    jobType: task.jobType,
    cronExpression: task.cronExpression,
    parameters: JSON.stringify(task.parameters ?? {}, null, 2),
  })
  modalVisible.value = true
}

async function onSubmit() {
  if (!form.name.trim()) return message.error('任务名必填')
  if (!validateCron(form.cronExpression)) return message.error('Cron 表达式必须为 5 段（分 时 日 月 周）')
  let parsedParameters: Record<string, unknown> = {}
  try {
    parsedParameters = JSON.parse(form.parameters || '{}')
  } catch {
    return message.error('参数 JSON 格式错误')
  }
  submitting.value = true
  try {
    if (modalMode.value === 'create') {
      const body: SaveScheduledTaskDto = {
        name: form.name.trim(),
        jobType: form.jobType,
        cronExpression: form.cronExpression.trim(),
        parameters: parsedParameters,
      }
      await scheduledTaskApi.create(body)
      message.success('任务已创建（停用态）')
    } else if (form.taskId) {
      const body: UpdateScheduledTaskDto = {
        name: form.name.trim(),
        cronExpression: form.cronExpression.trim(),
        parameters: parsedParameters,
      }
      await scheduledTaskApi.update(form.taskId, body)
      message.success('任务已更新')
    }
    modalVisible.value = false
    loadList()
  } catch (e) {
    if (e instanceof BusinessError) message.error(e.message)
    else message.error('保存失败')
  } finally {
    submitting.value = false
  }
}

function askAction(kind: 'enable' | 'disable' | 'runNow', task: ScheduledTaskDto) {
  confirmAction.value = { kind, task }
  confirmVisible.value = true
}

const confirmTitle = computed(() => {
  if (!confirmAction.value) return ''
  return { enable: '启用定时任务', disable: '停用定时任务', runNow: '立即执行任务' }[confirmAction.value.kind]
})
const confirmDanger = computed(() => confirmAction.value?.kind === 'disable')
const confirmContent = computed(() => {
  if (!confirmAction.value) return ''
  const map = {
    enable: '启用后任务将向调度器注册，按 Cron 表达式自动执行。',
    disable: '停用后任务将从调度器注销，不再按 Cron 执行。已注册的下一次执行取消。可随时启用恢复。',
    runNow: '立即执行将忽略 Cron 调度，立即触发一次任务。请确认非高峰时段。',
  }
  return map[confirmAction.value.kind]
})

async function onConfirm() {
  if (!confirmAction.value) return
  const { kind, task } = confirmAction.value
  try {
    if (kind === 'enable') await scheduledTaskApi.enable(task.taskId)
    else if (kind === 'disable') await scheduledTaskApi.disable(task.taskId)
    else await scheduledTaskApi.runNow(task.taskId)
    message.success({ enable: '已启用', disable: '已停用', runNow: '已触发立即执行' }[kind])
    confirmVisible.value = false
    loadList()
  } catch (e) {
    if (e instanceof BusinessError) message.error(e.message)
    else message.error('操作失败')
  }
}

async function openHistory(task: ScheduledTaskDto) {
  historyTaskName.value = task.name
  historyVisible.value = true
  historyLoading.value = true
  try {
    historyList.value = await scheduledTaskApi.getHistory(task.taskId)
  } catch (e) {
    if (e instanceof BusinessError) message.error(e.message)
    historyList.value = []
  } finally {
    historyLoading.value = false
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
  <div class="runtime-ops-scheduled-tasks">
    <div class="page-header">
      <div class="page-title">定时任务</div>
      <div class="page-desc">管理定时任务，CRUD/启停/立即触发，监控任务执行状态。作业类型创建后不可变更。</div>
    </div>

    <div class="toolbar">
      <a-input
        v-model:value="filter.name"
        placeholder="搜索任务名"
        allow-clear
        style="width: 200px"
        @press-enter="onSearch"
      />
      <a-select
        v-model:value="filter.status"
        mode="multiple"
        placeholder="状态"
        allow-clear
        style="min-width: 180px"
        :options="statusOptions"
      />
      <a-select
        v-model:value="filter.jobType"
        placeholder="作业类型"
        allow-clear
        style="width: 160px"
        :options="jobTypeOptions.map((v) => ({ label: v, value: v }))"
      />
      <a-button type="primary" @click="onSearch">筛选</a-button>
      <div class="spacer" />
      <PermissionGuard permission="scheduled-task:write">
        <a-button type="primary" @click="openCreate">
          <PlusOutlined />新增任务
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
        <EmptyState description="暂无定时任务" action-text="新增任务" @action="openCreate" />
      </template>
      <template #bodyCell="{ column, record }">
        <template v-if="column.key === 'cronExpression'">
          <span class="mono">{{ record.cronExpression }}</span>
        </template>
        <template v-else-if="column.key === 'jobType'">
          <a-tag color="geekblue">{{ record.jobType }}</a-tag>
        </template>
        <template v-else-if="column.key === 'status'">
          <StatusTag type="scheduledTask" :status="record.status" />
        </template>
        <template v-else-if="column.key === 'lastRunAt'">
          {{ record.lastRunAt ?? '—' }}
        </template>
        <template v-else-if="column.key === 'nextRunAt'">
          {{ record.nextRunAt ?? '—' }}
        </template>
        <template v-else-if="column.key === 'action'">
          <a-space size="small" wrap>
            <PermissionGuard permission="scheduled-task:write">
              <a-button type="link" size="small" @click="openEdit(record)">
                <EditOutlined />编辑
              </a-button>
            </PermissionGuard>
            <PermissionGuard permission="scheduled-task:write">
              <a-button
                v-if="record.status === 'Disabled'"
                type="link"
                size="small"
                @click="askAction('enable', record)"
              >
                启用
              </a-button>
              <a-button
                v-else
                type="link"
                size="small"
                danger
                @click="askAction('disable', record)"
              >
                停用
              </a-button>
            </PermissionGuard>
            <PermissionGuard permission="scheduled-task:run-now">
              <a-button
                type="link"
                size="small"
                :disabled="record.status === 'Disabled'"
                @click="askAction('runNow', record)"
              >
                <PlayCircleOutlined />立即执行
              </a-button>
            </PermissionGuard>
            <a-button type="link" size="small" @click="openHistory(record)">
              <HistoryOutlined />历史
            </a-button>
          </a-space>
        </template>
      </template>
    </a-table>

    <a-modal
      v-model:open="modalVisible"
      :title="modalMode === 'create' ? '新增定时任务' : '编辑定时任务'"
      width="560"
      :confirm-loading="submitting"
      ok-text="保存"
      cancel-text="取消"
      @ok="onSubmit"
    >
      <a-form layout="vertical">
        <a-form-item label="任务名" required>
          <a-input v-model:value="form.name" placeholder="对账任务" />
        </a-form-item>
        <a-form-item required>
          <template #label>
            作业类型
            <a-tooltip v-if="modalMode === 'edit'" title="作业类型不可变">
              <InfoCircleOutlined style="margin-left: 4px; color: #8C8C8C" />
            </a-tooltip>
          </template>
          <a-select
            v-model:value="form.jobType"
            :disabled="modalMode === 'edit'"
            :options="jobTypeOptions.map((v) => ({ label: v, value: v }))"
          />
        </a-form-item>
        <a-form-item label="Cron 表达式" required>
          <a-input v-model:value="form.cronExpression" placeholder="0 2 * * *" class="mono" />
          <div style="font-size: 12px; color: #8C8C8C; margin-top: 4px">5 段：分 时 日 月 周</div>
        </a-form-item>
        <a-form-item label="参数（JSON）">
          <a-textarea v-model:value="form.parameters" :rows="6" class="mono" />
        </a-form-item>
      </a-form>
    </a-modal>

    <ConfirmDialog
      v-model:open="confirmVisible"
      :title="confirmTitle"
      :content="confirmContent"
      :danger="confirmDanger"
      ok-text="确认"
      cancel-text="取消"
      @confirm="onConfirm"
    />

    <a-drawer
      v-model:open="historyVisible"
      :title="`执行历史 - ${historyTaskName}`"
      width="640"
      placement="right"
    >
      <a-spin :spinning="historyLoading">
        <a-empty v-if="historyList.length === 0" description="暂无执行记录" />
        <a-timeline v-else>
          <a-timeline-item
            v-for="exec in historyList"
            :key="exec.executionId"
            :color="exec.status === 'Succeeded' ? 'green' : exec.status === 'Failed' ? 'red' : 'blue'"
          >
            <div style="font-weight: 500">{{ exec.status }}</div>
            <div style="font-size: 12px; color: #8C8C8C">
              开始 {{ exec.startedAt }} · 结束 {{ exec.finishedAt ?? '—' }}
            </div>
            <div v-if="exec.errorMessage" style="font-size: 12px; color: #FF4D4F; margin-top: 4px">
              {{ exec.errorMessage }}
            </div>
          </a-timeline-item>
        </a-timeline>
      </a-spin>
    </a-drawer>
  </div>
</template>

<style scoped>
.runtime-ops-scheduled-tasks .page-header { background: var(--n1, #fff); border-radius: 8px; padding: 24px; margin-bottom: 16px; box-shadow: 0 1px 2px rgba(0,0,0,.03); }
.runtime-ops-scheduled-tasks .page-title { font-size: 20px; font-weight: 600; margin-bottom: 4px; }
.runtime-ops-scheduled-tasks .page-desc { color: #8C8C8C; }
.runtime-ops-scheduled-tasks .toolbar { display: flex; gap: 12px; margin-bottom: 16px; flex-wrap: wrap; align-items: center; }
.runtime-ops-scheduled-tasks .spacer { flex: 1; }
.runtime-ops-scheduled-tasks .mono { font-family: "SF Mono","Cascadia Code",Consolas,monospace; font-size: 12px; }
</style>
