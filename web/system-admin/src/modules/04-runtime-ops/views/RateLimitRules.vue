<!-- web/system-admin/src/modules/04-runtime-ops/views/RateLimitRules.vue -->
<!-- 限流规则管理：列表 + 筛选 + 新建/编辑弹窗 + 启停确认 -->
<script setup lang="ts">
import { ref, reactive, onMounted, computed } from 'vue'
import { message } from 'ant-design-vue'
import { PlusOutlined, EditOutlined, ThunderboltOutlined } from '@ant-design/icons-vue'
import { rateLimitRuleApi } from '../api/rate-limit-rules.api'
import type {
  RateLimitRuleDto,
  SaveRateLimitRuleDto,
  RateLimitAlgorithm,
  RateLimitScope,
} from '../types/rate-limit-rule.dto'
import StatusTag from '@/shared/components/StatusTag.vue'
import ConfirmDialog from '@/shared/components/ConfirmDialog.vue'
import PermissionGuard from '@/shared/auth/PermissionGuard.vue'
import EmptyState from '@/shared/components/EmptyState.vue'
import { ConcurrencyError, BusinessError } from '@/shared/http/errors'

interface FilterState {
  targetApi: string
  enabled: '' | 'true' | 'false'
  targetContext: string[]
  page: number
  pageSize: number
}

interface FormState {
  ruleId?: string
  targetApi: string
  targetContext: string
  limit: number
  windowSeconds: number
  algorithm: RateLimitAlgorithm
  scope: RateLimitScope
  version?: number
}

const contextOptions = [
  'Identity', 'AccessControl', 'UserCenter', 'Points', 'Membership',
  'Review', 'AfterSales', 'Product', 'Order', 'Payment', 'Notification', 'Inventory',
]
const algorithmOptions: { label: string; value: RateLimitAlgorithm }[] = [
  { label: '滑动窗口', value: 'SlidingWindow' },
  { label: '令牌桶', value: 'TokenBucket' },
  { label: '固定窗口', value: 'FixedWindow' },
]
const scopeOptions: { label: string; value: RateLimitScope }[] = [
  { label: 'IP', value: 'IP' },
  { label: '用户', value: 'User' },
  { label: '全局', value: 'Global' },
  { label: '店铺', value: 'Shop' },
]

const loading = ref(false)
const dataList = ref<RateLimitRuleDto[]>([])
const total = ref(0)
const filter = reactive<FilterState>({
  targetApi: '',
  enabled: '',
  targetContext: [],
  page: 1,
  pageSize: 20,
})

const modalVisible = ref(false)
const modalMode = ref<'create' | 'edit'>('create')
const form = reactive<FormState>({
  targetApi: '',
  targetContext: 'Order',
  limit: 100,
  windowSeconds: 60,
  algorithm: 'SlidingWindow',
  scope: 'User',
})
const submitting = ref(false)
const confirmVisible = ref(false)
const confirmAction = ref<{ kind: 'enable' | 'disable'; rule: RateLimitRuleDto } | null>(null)

const columns = computed(() => [
  { title: '目标 API', dataIndex: 'targetApi', key: 'targetApi', width: 200, ellipsis: true },
  { title: '目标上下文', dataIndex: 'targetContext', key: 'targetContext', width: 120 },
  { title: '阈值', dataIndex: 'limit', key: 'limit', width: 80, align: 'right' as const },
  { title: '窗口', key: 'windowSeconds', width: 100, customRender: ({ record }: { record: RateLimitRuleDto }) => `${record.windowSeconds}s` },
  { title: '算法', key: 'algorithm', width: 110 },
  { title: '维度', key: 'scope', width: 90 },
  { title: '状态', key: 'enabled', width: 100 },
  { title: '操作', key: 'action', width: 180, fixed: 'right' as const },
])

async function loadList() {
  loading.value = true
  try {
    const params = {
      targetApi: filter.targetApi || undefined,
      enabled: filter.enabled === '' ? undefined : filter.enabled === 'true',
      targetContext: filter.targetContext.length ? filter.targetContext : undefined,
      page: filter.page,
      pageSize: filter.pageSize,
    }
    const res = await rateLimitRuleApi.list(params)
    dataList.value = res.items
    total.value = res.total
  } catch (e) {
    if (e instanceof BusinessError) message.error(e.message)
    else message.error('加载限流规则失败')
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
  Object.assign(form, {
    ruleId: undefined, targetApi: '', targetContext: 'Order',
    limit: 100, windowSeconds: 60, algorithm: 'SlidingWindow', scope: 'User', version: undefined,
  })
  modalVisible.value = true
}

async function openEdit(rule: RateLimitRuleDto) {
  modalMode.value = 'edit'
  Object.assign(form, {
    ruleId: rule.ruleId, targetApi: rule.targetApi, targetContext: rule.targetContext,
    limit: rule.limit, windowSeconds: rule.windowSeconds, algorithm: rule.algorithm,
    scope: rule.scope, version: rule.version,
  })
  modalVisible.value = true
}

async function onSubmit() {
  if (!form.targetApi.trim()) return message.error('目标 API 必填')
  if (form.limit <= 0) return message.error('阈值必须 > 0')
  if (form.windowSeconds <= 0) return message.error('窗口必须 > 0')
  submitting.value = true
  try {
    const body: SaveRateLimitRuleDto = {
      targetApi: form.targetApi.trim(),
      targetContext: form.targetContext,
      limit: form.limit,
      windowSeconds: form.windowSeconds,
      algorithm: form.algorithm,
      scope: form.scope,
      version: form.version,
    }
    if (modalMode.value === 'create') {
      await rateLimitRuleApi.create(body)
      message.success('规则已创建')
    } else if (form.ruleId) {
      await rateLimitRuleApi.update(form.ruleId, body)
      message.success('规则已更新')
    }
    modalVisible.value = false
    loadList()
  } catch (e) {
    if (e instanceof ConcurrencyError) {
      message.error('数据已被其他用户修改，已自动刷新')
      if (form.ruleId) {
        const fresh = await rateLimitRuleApi.get(form.ruleId)
        Object.assign(form, { version: fresh.version })
      }
    } else if (e instanceof BusinessError) {
      message.error(e.message)
    } else {
      message.error('保存失败')
    }
  } finally {
    submitting.value = false
  }
}

function askToggle(rule: RateLimitRuleDto) {
  confirmAction.value = { kind: rule.enabled ? 'disable' : 'enable', rule }
  confirmVisible.value = true
}

const confirmTitle = computed(() =>
  confirmAction.value?.kind === 'disable' ? '停用限流规则' : '启用限流规则')
const confirmDanger = computed(() => confirmAction.value?.kind === 'disable')
const confirmContent = computed(() => {
  if (!confirmAction.value) return ''
  return confirmAction.value.kind === 'disable'
    ? '停用后该 API 将不再受限流保护，可能在高并发下被击穿。可随时启用恢复。'
    : '启用后该 API 将立即生效，按当前阈值与窗口进行限流。'
})

async function onConfirmToggle() {
  if (!confirmAction.value) return
  const { kind, rule } = confirmAction.value
  try {
    if (kind === 'enable') await rateLimitRuleApi.enable(rule.ruleId)
    else await rateLimitRuleApi.disable(rule.ruleId)
    message.success(kind === 'enable' ? '已启用' : '已停用')
    confirmVisible.value = false
    loadList()
  } catch (e) {
    if (e instanceof BusinessError) message.error(e.message)
    else message.error('操作失败')
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
  <div class="runtime-ops-rate-limit">
    <div class="page-header">
      <div class="page-title">限流规则</div>
      <div class="page-desc">管理各域 API 限流规则，配置阈值/窗口/算法/维度，启停规则并热生效。</div>
    </div>

    <div class="toolbar">
      <a-input
        v-model:value="filter.targetApi"
        placeholder="搜索目标 API 路径"
        allow-clear
        style="width: 240px"
        @press-enter="onSearch"
      />
      <a-select
        v-model:value="filter.enabled"
        placeholder="启用状态"
        allow-clear
        style="width: 140px"
      >
        <a-select-option value="">全部</a-select-option>
        <a-select-option value="true">启用</a-select-option>
        <a-select-option value="false">停用</a-select-option>
      </a-select>
      <a-select
        v-model:value="filter.targetContext"
        mode="multiple"
        placeholder="目标上下文"
        allow-clear
        style="min-width: 220px"
        :options="contextOptions.map((v) => ({ label: v, value: v }))"
      />
      <a-button type="primary" @click="onSearch">筛选</a-button>
      <div class="spacer" />
      <PermissionGuard permission="rate-limit:write">
        <a-button type="primary" @click="openCreate">
          <PlusOutlined />新增规则
        </a-button>
      </PermissionGuard>
    </div>

    <a-table
      :columns="columns"
      :data-source="dataList"
      :loading="loading"
      row-key="ruleId"
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
        <EmptyState description="暂无限流规则" action-text="新增规则" @action="openCreate" />
      </template>
      <template #bodyCell="{ column, record }">
        <template v-if="column.key === 'algorithm'">
          <a-tag color="blue">{{ algorithmOptions.find((o) => o.value === record.algorithm)?.label ?? record.algorithm }}</a-tag>
        </template>
        <template v-else-if="column.key === 'scope'">
          <a-tag color="cyan">{{ scopeOptions.find((o) => o.value === record.scope)?.label ?? record.scope }}</a-tag>
        </template>
        <template v-else-if="column.key === 'enabled'">
          <StatusTag type="rateLimit" :status="record.enabled ? 'Enabled' : 'Disabled'" />
        </template>
        <template v-else-if="column.key === 'action'">
          <a-space size="small">
            <PermissionGuard permission="rate-limit:write">
              <a-button type="link" size="small" @click="openEdit(record)">
                <EditOutlined />编辑
              </a-button>
            </PermissionGuard>
            <PermissionGuard permission="rate-limit:write">
              <a-button
                type="link"
                size="small"
                :danger="record.enabled"
                @click="askToggle(record)"
              >
                <ThunderboltOutlined />{{ record.enabled ? '停用' : '启用' }}
              </a-button>
            </PermissionGuard>
          </a-space>
        </template>
      </template>
    </a-table>

    <a-modal
      v-model:open="modalVisible"
      :title="modalMode === 'create' ? '新增限流规则' : '编辑限流规则'"
      width="560"
      :confirm-loading="submitting"
      ok-text="保存"
      cancel-text="取消"
      @ok="onSubmit"
    >
      <a-form layout="vertical">
        <a-form-item label="目标 API" required>
          <a-input v-model:value="form.targetApi" placeholder="/api/orders" />
        </a-form-item>
        <a-form-item label="目标上下文" required>
          <a-select v-model:value="form.targetContext" :options="contextOptions.map((v) => ({ label: v, value: v }))" />
        </a-form-item>
        <a-row :gutter="16">
          <a-col :span="12">
            <a-form-item label="阈值（请求数）" required>
              <a-input-number v-model:value="form.limit" :min="1" style="width: 100%" />
            </a-form-item>
          </a-col>
          <a-col :span="12">
            <a-form-item label="窗口（秒）" required>
              <a-input-number v-model:value="form.windowSeconds" :min="1" style="width: 100%" />
            </a-form-item>
          </a-col>
        </a-row>
        <a-row :gutter="16">
          <a-col :span="12">
            <a-form-item label="算法" required>
              <a-select v-model:value="form.algorithm" :options="algorithmOptions" />
            </a-form-item>
          </a-col>
          <a-col :span="12">
            <a-form-item label="维度" required>
              <a-select v-model:value="form.scope" :options="scopeOptions" />
            </a-form-item>
          </a-col>
        </a-row>
      </a-form>
    </a-modal>

    <ConfirmDialog
      v-model:open="confirmVisible"
      :title="confirmTitle"
      :content="confirmContent"
      :danger="confirmDanger"
      ok-text="确认"
      cancel-text="取消"
      @confirm="onConfirmToggle"
    />
  </div>
</template>

<style scoped>
.runtime-ops-rate-limit .page-header { background: var(--n1, #fff); border-radius: 8px; padding: 24px; margin-bottom: 16px; box-shadow: 0 1px 2px rgba(0,0,0,.03); }
.runtime-ops-rate-limit .page-title { font-size: 20px; font-weight: 600; margin-bottom: 4px; }
.runtime-ops-rate-limit .page-desc { color: #8C8C8C; }
.runtime-ops-rate-limit .toolbar { display: flex; gap: 12px; margin-bottom: 16px; flex-wrap: wrap; align-items: center; }
.runtime-ops-rate-limit .spacer { flex: 1; }
</style>
