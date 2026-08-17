<!-- web/operations/src/modules/07-notification-ops/views/Templates.vue -->
<template>
  <div class="notification-templates">
    <!-- 区域 A：筛选条 -->
    <a-card :bordered="false" class="filter-card">
      <a-form layout="inline" class="filter-form">
        <a-form-item label="关键词">
          <a-input-search
            v-model:value="filters.keyword"
            placeholder="模板名称 / 编码"
            allow-clear
            style="width: 220px"
            @search="onQuery"
          />
        </a-form-item>
        <a-form-item label="事件类型">
          <a-select
            v-model:value="filters.eventType"
            placeholder="全部事件"
            allow-clear
            style="width: 130px"
            :options="eventTypeOptions"
          />
        </a-form-item>
        <a-form-item label="渠道">
          <a-select
            v-model:value="filters.channel"
            placeholder="全部渠道"
            allow-clear
            style="width: 130px"
            :options="channelOptions"
          />
        </a-form-item>
        <a-form-item label="状态">
          <a-select
            v-model:value="filters.status"
            placeholder="全部状态"
            allow-clear
            style="width: 120px"
            :options="statusOptions"
          />
        </a-form-item>
        <a-form-item>
          <a-button type="primary" @click="onQuery">查询</a-button>
          <a-button style="margin-left: 8px" @click="onReset">重置</a-button>
        </a-form-item>
      </a-form>
    </a-card>

    <!-- 区域 B + C：工具栏与模板表格 -->
    <a-card :bordered="false" class="table-card">
      <div class="table-toolbar">
        <a-button type="primary" @click="onOpenCreate">新增模板</a-button>
        <a-space>
          <a-button @click="onExportCsv">导出 CSV</a-button>
          <a-button :loading="loading" @click="fetchTemplates">刷新</a-button>
        </a-space>
      </div>

      <div v-if="errorMessage" class="table-error">
        <EmptyState :description="`加载失败：${errorMessage}`" action-text="重试" @action="fetchTemplates" />
      </div>
      <a-table
        v-else
        :columns="columns"
        :data-source="tableData"
        :loading="loading"
        :pagination="pagination"
        :row-key="(record: NotificationTemplateDto) => record.templateId"
        :scroll="{ x: 1180 }"
        @change="onTableChange"
      >
        <template #emptyText>
          <EmptyState
            :description="hasActiveFilters ? '筛选条件下暂无模板' : '暂无通知模板'"
            :action-text="hasActiveFilters ? '清空筛选条件' : '新增模板'"
            @action="hasActiveFilters ? onReset() : onOpenCreate()"
          />
        </template>
        <template #bodyCell="{ column, record }">
          <template v-if="column.key === 'code'">
            <a class="code-link" :aria-label="record.code" @click="goRecords(record.code)">{{ record.code }}</a>
          </template>
          <template v-else-if="column.key === 'eventType'">
            <a-tag :color="NOTIFICATION_EVENT_TYPE_META[record.eventType].color">
              {{ NOTIFICATION_EVENT_TYPE_META[record.eventType].label }}
            </a-tag>
          </template>
          <template v-else-if="column.key === 'channel'">
            <a-tag :color="NOTIFICATION_CHANNEL_META[record.channel].color">
              {{ NOTIFICATION_CHANNEL_META[record.channel].label }}
            </a-tag>
          </template>
          <template v-else-if="column.key === 'variables'">{{ record.variables.length }}</template>
          <template v-else-if="column.key === 'status'">
            <a-tag :color="NOTIFICATION_TEMPLATE_STATUS_META[record.status].color">
              {{ NOTIFICATION_TEMPLATE_STATUS_META[record.status].label }}
            </a-tag>
          </template>
          <template v-else-if="column.key === 'updatedAt'">{{ formatDateTime(record.updatedAt) }}</template>
          <template v-else-if="column.key === 'action'">
            <a-space>
              <a-button type="link" size="small" aria-label="编辑模板" @click="onOpenEdit(record)">编辑</a-button>
              <a-button type="link" size="small" aria-label="预览模板" @click="onOpenPreview(record)">预览</a-button>
              <a-button
                v-if="record.status === 'Active'"
                type="link"
                size="small"
                danger
                aria-label="停用模板"
                @click="onDisable(record)"
              >
                停用
              </a-button>
              <a-button
                v-else
                type="link"
                size="small"
                aria-label="启用模板"
                :loading="togglingId === record.templateId"
                @click="onEnable(record)"
              >
                启用
              </a-button>
            </a-space>
          </template>
        </template>
      </a-table>
    </a-card>

    <!-- 区域 D + E：新增/编辑模态框（左表单 + 右实时预览面板） -->
    <a-modal
      v-model:open="editorOpen"
      :title="editingId ? '编辑模板' : '新增模板'"
      width="720"
      :confirm-loading="submitting"
      ok-text="保存"
      cancel-text="取消"
      @ok="onSubmit"
    >
      <div class="editor-layout">
        <!-- 左侧表单 -->
        <a-form :label-col="{ span: 7 }" :wrapper-col="{ span: 17 }" class="editor-form">
          <a-form-item label="模板编码" required>
            <a-input
              v-model:value="form.code"
              :disabled="Boolean(editingId)"
              placeholder="大写字母开头，如 ORDER_PAID"
              :maxlength="64"
            />
          </a-form-item>
          <a-form-item label="模板名称" required>
            <a-input v-model:value="form.name" placeholder="如：订单已支付通知" :maxlength="50" />
          </a-form-item>
          <a-form-item label="事件类型" required>
            <a-select v-model:value="form.eventType" :options="eventTypeOptions" placeholder="选择事件类型" />
          </a-form-item>
          <a-form-item label="渠道" required>
            <a-select v-model:value="form.channel" :options="channelOptions" placeholder="选择渠道" />
          </a-form-item>
          <a-form-item label="标题模板" required>
            <a-input
              v-model:value="form.titleTemplate"
              placeholder="支持 {{变量}} 插值"
              :maxlength="200"
            />
          </a-form-item>
          <a-form-item label="正文模板" required>
            <a-textarea
              v-model:value="form.bodyTemplate"
              :rows="4"
              placeholder="支持 {{变量}} 插值；短信渠道渲染后限 70 字"
              :maxlength="1000"
              show-count
            />
          </a-form-item>
          <a-form-item label="状态">
            <a-switch
              :checked="form.status === 'Active'"
              checked-children="启用"
              un-checked-children="停用"
              @change="(checked: boolean | string | number) => onStatusSwitch(checked)"
            />
          </a-form-item>
        </a-form>

        <!-- 右侧变量列表与预览面板 -->
        <div class="editor-side">
          <div class="side-section-title">变量列表（{{ formVariables.length }}）</div>
          <div v-if="formVariables.length === 0" class="vars-empty">暂未定义变量</div>
          <div v-for="(v, index) in formVariables" :key="index" class="var-row">
            <a-input
              v-model:value="v.name"
              size="small"
              placeholder="变量名"
              :maxlength="32"
              class="var-input"
            />
            <a-input
              v-model:value="v.example"
              size="small"
              placeholder="示例值（预览用）"
              :maxlength="100"
              class="var-input"
            />
            <a-button
              type="link"
              size="small"
              danger
              aria-label="删除变量"
              @click="onRemoveVariable(index)"
            >
              删除
            </a-button>
          </div>
          <a-button size="small" block class="var-add" @click="onAddVariable">+ 添加变量</a-button>

          <div class="side-section-title">预览面板</div>
          <div v-if="formVariables.length === 0" class="vars-empty">定义变量后可输入测试值渲染预览</div>
          <div v-for="v in formVariables" :key="`pv-${formVariables.indexOf(v)}-${v.name}`" class="var-row">
            <span class="preview-var-name" :aria-label="v.name">{{ v.name }}</span>
            <a-input
              v-model:value="previewVars[v.name]"
              size="small"
              :placeholder="v.example ? `默认示例：${v.example}` : '输入测试值'"
              :maxlength="100"
              class="var-input"
            />
          </div>
          <a-button
            size="small"
            block
            :loading="previewLoading"
            :disabled="formVariables.length === 0"
            @click="onRenderPreview"
          >
            渲染预览
          </a-button>
          <div v-if="previewResult" class="preview-result">
            <div class="preview-title">{{ previewResult.title || '（无标题）' }}</div>
            <div class="preview-body">{{ previewResult.body || '（无正文）' }}</div>
            <div v-if="form.channel === 'Sms'" class="preview-count" :class="{ over: smsBodyLength > SMS_LIMIT }">
              短信正文 {{ smsBodyLength }}/{{ SMS_LIMIT }} 字
            </div>
          </div>
        </div>
      </div>
    </a-modal>

    <!-- 表格行「预览」：变量测试值 + 调用 preview 端点 -->
    <a-modal
      v-model:open="previewModalOpen"
      :title="`模板预览：${previewTarget?.code ?? ''}`"
      width="560"
      :footer="null"
    >
      <template v-if="previewTarget">
        <a-descriptions :column="2" size="small" bordered class="preview-modal-desc">
          <a-descriptions-item label="名称">{{ previewTarget.name }}</a-descriptions-item>
          <a-descriptions-item label="渠道">
            <a-tag :color="NOTIFICATION_CHANNEL_META[previewTarget.channel].color">
              {{ NOTIFICATION_CHANNEL_META[previewTarget.channel].label }}
            </a-tag>
          </a-descriptions-item>
        </a-descriptions>
        <div class="side-section-title">变量测试值</div>
        <div v-if="previewTarget.variables.length === 0" class="vars-empty">该模板未定义变量</div>
        <div v-for="v in previewTarget.variables" :key="v.name" class="var-row">
          <span class="preview-var-name" :aria-label="v.name">{{ v.name }}</span>
          <a-input
            v-model:value="previewModalVars[v.name]"
            size="small"
            :placeholder="v.example ? `默认示例：${v.example}` : '输入测试值'"
            :maxlength="100"
            class="var-input"
          />
        </div>
        <IdempotencyButton
          type="primary"
          block
          :loading="previewModalLoading"
          style="margin-top: 12px"
          @click="onRenderPreviewModal"
        >
          调用预览接口渲染
        </IdempotencyButton>
        <div v-if="previewModalResult" class="preview-result">
          <div class="preview-title">{{ previewModalResult.title || '（无标题）' }}</div>
          <div class="preview-body">{{ previewModalResult.body || '（无正文）' }}</div>
          <div
            v-if="previewTarget.channel === 'Sms'"
            class="preview-count"
            :class="{ over: previewModalResult.body.length > SMS_LIMIT }"
          >
            短信正文 {{ previewModalResult.body.length }}/{{ SMS_LIMIT }} 字
          </div>
        </div>
      </template>
    </a-modal>

    <!-- 停用二次确认 -->
    <ConfirmDialog
      :open="disableConfirmOpen"
      title="停用模板"
      :content="`停用「${disableTarget?.name ?? ''}」后，该事件将不再发送通知。确认停用？`"
      @confirm="onConfirmDisable"
      @cancel="disableConfirmOpen = false"
    />
  </div>
</template>

<script setup lang="ts">
import { computed, onMounted, reactive, ref } from 'vue'
import { useRouter } from 'vue-router'
import { message } from 'ant-design-vue'
import type { TableColumnsType } from 'ant-design-vue'
import { ConcurrencyError } from '@/shared/http'
import { ConfirmDialog, EmptyState, IdempotencyButton } from '@/shared/components'
import { formatDateTime } from '@/shared/utils/format'
import { templateApi } from '../api/template.api'
import {
  NOTIFICATION_CHANNELS,
  NOTIFICATION_CHANNEL_META,
  NOTIFICATION_EVENT_TYPE_META,
  NOTIFICATION_TEMPLATE_STATUS_META,
} from '../types/template.dto'
import type {
  NotificationChannel,
  NotificationEventType,
  NotificationTemplateDto,
  NotificationTemplateStatus,
  SaveNotificationTemplateDto,
  TemplatePreviewResultDto,
  TemplateVariableDto,
} from '../types/template.dto'

/**
 * 通知模板页（07-notification-ops）
 *
 * 筛选条 + 模板表格 + 新增/编辑模态框（左表单右预览）+ 行级预览 Modal。
 * - 编码全局唯一（创建后不可改），后端 409 透出「模板编码已存在」
 * - 变量插值与变量列表一致性校验（{{变量}} 必须已定义）
 * - 短信渠道正文渲染后限 70 字
 * - 预览：已保存模板调用 preview 端点，未保存模板前端本地插值渲染
 */

const SMS_LIMIT = 70

const router = useRouter()

const eventTypeOptions = (Object.keys(NOTIFICATION_EVENT_TYPE_META) as NotificationEventType[]).map((value) => ({
  label: NOTIFICATION_EVENT_TYPE_META[value].label,
  value,
}))

const channelOptions = NOTIFICATION_CHANNELS.map((value) => ({
  label: NOTIFICATION_CHANNEL_META[value].label,
  value,
}))

const statusOptions: { label: string; value: NotificationTemplateStatus }[] = [
  { label: '启用', value: 'Active' },
  { label: '停用', value: 'Inactive' },
]

// ---------- 筛选与列表 ----------
interface FilterState {
  keyword: string
  eventType?: NotificationEventType
  channel?: NotificationChannel
  status?: NotificationTemplateStatus
}

const filters = reactive<FilterState>({
  keyword: '',
  eventType: undefined,
  channel: undefined,
  status: undefined,
})

const hasActiveFilters = computed(
  () => Boolean(filters.keyword || filters.eventType || filters.channel || filters.status),
)

const tableData = ref<NotificationTemplateDto[]>([])
const loading = ref(false)
const errorMessage = ref('')

const pagination = reactive({
  current: 1,
  pageSize: 20,
  total: 0,
  showSizeChanger: true,
  showTotal: (total: number) => `共 ${total} 条`,
})

const columns: TableColumnsType = [
  { title: '模板编码', key: 'code', width: 200 },
  { title: '模板名称', dataIndex: 'name', key: 'name', width: 180, ellipsis: true },
  { title: '事件类型', key: 'eventType', width: 100 },
  { title: '渠道', key: 'channel', width: 100 },
  { title: '变量数', key: 'variables', width: 80, align: 'center' },
  { title: '状态', key: 'status', width: 90 },
  { title: '更新时间', key: 'updatedAt', width: 170 },
  { title: '操作', key: 'action', width: 190, fixed: 'right' },
]

async function fetchTemplates() {
  loading.value = true
  errorMessage.value = ''
  try {
    const params: Parameters<typeof templateApi.list>[0] = {
      page: pagination.current,
      pageSize: pagination.pageSize,
    }
    const keyword = filters.keyword.trim()
    if (keyword) params.keyword = keyword
    if (filters.eventType) params.eventType = filters.eventType
    if (filters.channel) params.channel = filters.channel
    if (filters.status) params.status = filters.status

    const { data } = await templateApi.list(params)
    tableData.value = data.items
    pagination.total = data.total
  } catch (e) {
    errorMessage.value = e instanceof Error ? e.message : '加载模板列表失败'
    tableData.value = []
    pagination.total = 0
  } finally {
    loading.value = false
  }
}

function onQuery() {
  pagination.current = 1
  void fetchTemplates()
}

function onReset() {
  filters.keyword = ''
  filters.eventType = undefined
  filters.channel = undefined
  filters.status = undefined
  onQuery()
}

function onTableChange(pag: { current?: number; pageSize?: number }) {
  if (pag.current !== undefined) pagination.current = pag.current
  if (pag.pageSize !== undefined) pagination.pageSize = pag.pageSize
  void fetchTemplates()
}

function goRecords(templateCode: string) {
  void router.push({ path: '/notification-ops/records', query: { templateCode } })
}

// ---------- 新增 / 编辑模态框 ----------
interface TemplateFormState {
  code: string
  name: string
  eventType: NotificationEventType
  channel: NotificationChannel
  titleTemplate: string
  bodyTemplate: string
  status: NotificationTemplateStatus
}

const editorOpen = ref(false)
const editingId = ref('')
const submitting = ref(false)
const form = reactive<TemplateFormState>({
  code: '',
  name: '',
  eventType: 'Order',
  channel: 'Sms',
  titleTemplate: '',
  bodyTemplate: '',
  status: 'Active',
})
const formVariables = ref<TemplateVariableDto[]>([])
const previewVars = reactive<Record<string, string>>({})
const previewResult = ref<TemplatePreviewResultDto | null>(null)
const previewLoading = ref(false)

function resetEditor() {
  editingId.value = ''
  form.code = ''
  form.name = ''
  form.eventType = 'Order'
  form.channel = 'Sms'
  form.titleTemplate = ''
  form.bodyTemplate = ''
  form.status = 'Active'
  formVariables.value = []
  Object.keys(previewVars).forEach((key) => {
    delete previewVars[key]
  })
  previewResult.value = null
}

function fillEditor(template: NotificationTemplateDto) {
  editingId.value = template.templateId
  form.code = template.code
  form.name = template.name
  form.eventType = template.eventType
  form.channel = template.channel
  form.titleTemplate = template.titleTemplate
  form.bodyTemplate = template.bodyTemplate
  form.status = template.status
  formVariables.value = template.variables.map((v) => ({ ...v }))
  Object.keys(previewVars).forEach((key) => {
    delete previewVars[key]
  })
  template.variables.forEach((v) => {
    previewVars[v.name] = v.example ?? ''
  })
  previewResult.value = null
}

function onOpenCreate() {
  resetEditor()
  editorOpen.value = true
}

async function onOpenEdit(record: NotificationTemplateDto) {
  try {
    const { data } = await templateApi.detail(record.templateId)
    fillEditor(data)
    editorOpen.value = true
  } catch (e) {
    message.error(e instanceof Error && e.message ? e.message : '加载模板详情失败')
  }
}

function onStatusSwitch(checked: boolean | string | number) {
  form.status = checked ? 'Active' : 'Inactive'
}

function onAddVariable() {
  formVariables.value.push({ name: '', description: '', example: '' })
}

function onRemoveVariable(index: number) {
  const removed = formVariables.value.splice(index, 1)[0]
  if (removed?.name) delete previewVars[removed.name]
}

/** 从标题 + 正文模板中提取 {{变量}} 插值名 */
function extractTemplateTokens(text: string): string[] {
  const tokens: string[] = []
  const pattern = /\{\{\s*([A-Za-z][A-Za-z0-9_]*)\s*\}\}/g
  let match: RegExpExecArray | null
  while ((match = pattern.exec(text)) !== null) {
    if (!tokens.includes(match[1])) tokens.push(match[1])
  }
  return tokens
}

/** 前端本地插值渲染（未保存模板的预览降级路径） */
function renderLocal(title: string, body: string, vars: Record<string, string>): TemplatePreviewResultDto {
  const replace = (text: string) =>
    text.replace(/\{\{\s*([A-Za-z][A-Za-z0-9_]*)\s*\}\}/g, (_, key: string) => vars[key] ?? '')
  return { title: replace(title), body: replace(body) }
}

const smsBodyLength = computed(() => {
  if (form.channel !== 'Sms' || !previewResult.value) return 0
  return previewResult.value.body.length
})

async function onRenderPreview() {
  const result = renderLocal(form.titleTemplate, form.bodyTemplate, { ...previewVars })
  if (!editingId.value) {
    previewResult.value = result
    return
  }
  previewLoading.value = true
  try {
    const { data } = await templateApi.preview(editingId.value, { variables: { ...previewVars } })
    previewResult.value = data
  } catch (e) {
    previewResult.value = result
    message.error(e instanceof Error && e.message ? e.message : '预览接口调用失败，已展示本地渲染结果')
  } finally {
    previewLoading.value = false
  }
}

/** 提交前校验：编码 / 名称 / 模板内容 / 变量一致性 / 短信 70 字限制 */
function validateForm(): string | null {
  const code = form.code.trim()
  if (!/^[A-Z][A-Z0-9_]{1,63}$/.test(code)) {
    return '模板编码必填：大写字母开头，仅含大写字母 / 数字 / 下划线，2-64 字符'
  }
  if (!form.name.trim()) return '模板名称必填'
  if (!form.titleTemplate.trim()) return '标题模板必填'
  if (!form.bodyTemplate.trim()) return '正文模板必填'

  const names = formVariables.value.map((v) => v.name.trim())
  if (names.some((n) => !/^[A-Za-z][A-Za-z0-9_]{0,31}$/.test(n))) {
    return '变量名须以字母开头，仅含字母 / 数字 / 下划线，1-32 字符'
  }
  const duplicated = names.find((n, i) => names.indexOf(n) !== i)
  if (duplicated) return `变量名重复：${duplicated}`

  const defined = new Set(names)
  const tokens = [
    ...extractTemplateTokens(form.titleTemplate),
    ...extractTemplateTokens(form.bodyTemplate),
  ]
  const undefinedTokens = tokens.filter((t) => !defined.has(t))
  if (undefinedTokens.length > 0) {
    return `模板含未定义变量：${undefinedTokens.join('、')}`
  }

  if (form.channel === 'Sms') {
    const vars: Record<string, string> = {}
    formVariables.value.forEach((v) => {
      vars[v.name.trim()] = previewVars[v.name.trim()] || v.example || ''
    })
    const renderedLength = renderLocal(form.titleTemplate, form.bodyTemplate, vars).body.length
    if (renderedLength > SMS_LIMIT) {
      return `短信正文渲染后 ${renderedLength} 字，超出 ${SMS_LIMIT} 字限制，请精简模板或示例值`
    }
  }
  return null
}

async function onSubmit() {
  const invalid = validateForm()
  if (invalid) {
    message.error(invalid)
    return
  }

  const body: SaveNotificationTemplateDto = {
    code: form.code.trim(),
    name: form.name.trim(),
    eventType: form.eventType,
    channel: form.channel,
    variables: formVariables.value.map((v) => ({
      name: v.name.trim(),
      description: v.description?.trim() ?? '',
      example: v.example?.trim() || undefined,
    })),
    titleTemplate: form.titleTemplate,
    bodyTemplate: form.bodyTemplate,
    status: form.status,
  }

  submitting.value = true
  try {
    if (editingId.value) {
      await templateApi.update(editingId.value, body)
      message.success('模板已更新')
    } else {
      await templateApi.create(body)
      message.success('模板已创建')
    }
    editorOpen.value = false
    await fetchTemplates()
  } catch (e) {
    if (e instanceof ConcurrencyError) {
      message.error(editingId.value ? '模板已被他人修改，请刷新后重试' : '模板编码已存在，请更换编码')
    } else {
      message.error(e instanceof Error && e.message ? e.message : '保存模板失败，请重试')
    }
  } finally {
    submitting.value = false
  }
}

// ---------- 启用 / 停用 ----------
const togglingId = ref('')
const disableConfirmOpen = ref(false)
const disableTarget = ref<NotificationTemplateDto | null>(null)

function onDisable(record: NotificationTemplateDto) {
  disableTarget.value = record
  disableConfirmOpen.value = true
}

async function onConfirmDisable() {
  const target = disableTarget.value
  disableConfirmOpen.value = false
  if (!target) return
  togglingId.value = target.templateId
  try {
    await templateApi.disable(target.templateId)
    message.success(`模板「${target.name}」已停用`)
    await fetchTemplates()
  } catch (e) {
    if (e instanceof ConcurrencyError) {
      message.warning('模板状态已变更，请刷新列表')
    } else {
      message.error(e instanceof Error && e.message ? e.message : '停用失败，请重试')
    }
  } finally {
    togglingId.value = ''
    disableTarget.value = null
  }
}

async function onEnable(record: NotificationTemplateDto) {
  togglingId.value = record.templateId
  try {
    await templateApi.enable(record.templateId)
    message.success(`模板「${record.name}」已启用`)
    await fetchTemplates()
  } catch (e) {
    if (e instanceof ConcurrencyError) {
      message.warning('模板状态已变更，请刷新列表')
    } else {
      message.error(e instanceof Error && e.message ? e.message : '启用失败，请重试')
    }
  } finally {
    togglingId.value = ''
  }
}

// ---------- 行级预览 Modal（调用 preview 端点） ----------
const previewModalOpen = ref(false)
const previewTarget = ref<NotificationTemplateDto | null>(null)
const previewModalVars = reactive<Record<string, string>>({})
const previewModalResult = ref<TemplatePreviewResultDto | null>(null)
const previewModalLoading = ref(false)

function onOpenPreview(record: NotificationTemplateDto) {
  previewTarget.value = record
  previewModalResult.value = null
  Object.keys(previewModalVars).forEach((key) => {
    delete previewModalVars[key]
  })
  record.variables.forEach((v) => {
    previewModalVars[v.name] = v.example ?? ''
  })
  previewModalOpen.value = true
}

async function onRenderPreviewModal() {
  const target = previewTarget.value
  if (!target) return
  previewModalLoading.value = true
  try {
    const { data } = await templateApi.preview(target.templateId, { variables: { ...previewModalVars } })
    previewModalResult.value = data
  } catch (e) {
    message.error(e instanceof Error && e.message ? e.message : '预览失败，请重试')
  } finally {
    previewModalLoading.value = false
  }
}

// ---------- CSV 导出（当前筛选页数据，前端生成） ----------
function csvEscape(value: string): string {
  const escaped = value.replace(/"/g, '""')
  return /[",\n]/.test(escaped) ? `"${escaped}"` : escaped
}

function onExportCsv() {
  if (tableData.value.length === 0) {
    message.warning('当前页无数据可导出')
    return
  }

  const header = ['模板编码', '模板名称', '事件类型', '渠道', '变量数', '状态', '更新时间']
  const rows = tableData.value.map((t) => [
    t.code,
    t.name,
    NOTIFICATION_EVENT_TYPE_META[t.eventType].label,
    NOTIFICATION_CHANNEL_META[t.channel].label,
    String(t.variables.length),
    NOTIFICATION_TEMPLATE_STATUS_META[t.status].label,
    formatDateTime(t.updatedAt),
  ])

  const csv = [header, ...rows].map((row) => row.map(csvEscape).join(',')).join('\n')
  const blob = new Blob([`\uFEFF${csv}`], { type: 'text/csv;charset=utf-8;' })
  const url = URL.createObjectURL(blob)
  const link = document.createElement('a')
  link.href = url
  link.download = `通知模板导出_${Date.now()}.csv`
  document.body.appendChild(link)
  link.click()
  document.body.removeChild(link)
  URL.revokeObjectURL(url)
  message.success(`已导出当前页 ${rows.length} 条数据`)
}

// ---------- 初始化 ----------
onMounted(() => {
  void fetchTemplates()
})
</script>

<style scoped>
.notification-templates {
  display: flex;
  flex-direction: column;
  gap: 16px;
}

.filter-card :deep(.ant-card-body) {
  padding: 16px 24px;
}

.filter-form {
  flex-wrap: wrap;
  row-gap: 8px;
}

.table-card :deep(.ant-card-body) {
  padding: 16px;
}

.table-toolbar {
  display: flex;
  align-items: center;
  justify-content: space-between;
  margin-bottom: 16px;
}

.table-error {
  padding: 24px;
  text-align: center;
}

.code-link {
  font-family: 'SF Mono', 'Cascadia Code', Consolas, monospace;
  font-size: 13px;
}

.editor-layout {
  display: flex;
  gap: 16px;
}

.editor-form {
  flex: 1;
  min-width: 0;
}

.editor-side {
  width: 250px;
  flex-shrink: 0;
}

.side-section-title {
  margin: 12px 0 8px;
  font-size: 13px;
  font-weight: 600;
  color: #000000d9;
}

.editor-side .side-section-title:first-child {
  margin-top: 0;
}

.vars-empty {
  font-size: 12px;
  color: #8c8c8c;
  margin-bottom: 8px;
}

.var-row {
  display: flex;
  align-items: center;
  gap: 4px;
  margin-bottom: 6px;
}

.var-input {
  flex: 1;
  min-width: 0;
}

.preview-var-name {
  width: 72px;
  flex-shrink: 0;
  font-family: 'SF Mono', 'Cascadia Code', Consolas, monospace;
  font-size: 12px;
  color: #1677ff;
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
}

.var-add {
  margin-bottom: 4px;
}

.preview-result {
  margin-top: 12px;
  padding: 12px;
  background: #fafafa;
  border: 1px solid #f0f0f0;
  border-radius: 6px;
}

.preview-title {
  font-size: 14px;
  font-weight: 500;
  color: #000000d9;
  margin-bottom: 8px;
}

.preview-body {
  font-size: 13px;
  color: #595959;
  white-space: pre-wrap;
  word-break: break-all;
}

.preview-count {
  margin-top: 8px;
  font-size: 12px;
  color: #52c41a;
}

.preview-count.over {
  color: #ff4d4f;
}

.preview-modal-desc {
  margin-bottom: 4px;
}
</style>
