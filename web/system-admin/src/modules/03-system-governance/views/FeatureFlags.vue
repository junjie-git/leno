<!-- web/system-admin/src/modules/03-system-governance/views/FeatureFlags.vue -->
<!-- 功能开关管理：筛选 + 表格 + 新建/编辑弹窗 + 评估抽屉 -->
<script setup lang="ts">
import { ref, reactive, onMounted, computed } from 'vue'
import { message } from 'ant-design-vue'
import { PlusOutlined, EditOutlined, PlayCircleOutlined } from '@ant-design/icons-vue'
import { featureFlagsApi } from '../api/feature-flags.api'
import type {
  FeatureFlagDto,
  SaveFeatureFlagDto,
  FeatureFlagStatus,
  EvaluateFlagResultDto,
} from '../types/feature-flag.dto'
import IdempotencyButton from '@/shared/components/IdempotencyButton.vue'
import ConfirmDialog from '@/shared/components/ConfirmDialog.vue'
import PermissionGuard from '@/shared/auth/PermissionGuard.vue'
import EmptyState from '@/shared/components/EmptyState.vue'
import { formatDateTime } from '@/shared/utils/format'
import { BusinessError } from '@/shared/http/errors'

interface FilterState {
  key: string
  status: FeatureFlagStatus[]
  group: string
  page: number
  pageSize: number
}

interface FormState {
  flagId?: string
  key: string
  description: string
  group: string
  ruleJson: string
  status: FeatureFlagStatus
}

const loading = ref(false)
const dataList = ref<FeatureFlagDto[]>([])
const total = ref(0)
const filter = reactive<FilterState>({
  key: '',
  status: [],
  group: '',
  page: 1,
  pageSize: 20,
})

const columns = computed(() => [
  { title: 'Key', dataIndex: 'key', key: 'key', width: 180, ellipsis: true },
  { title: '描述', dataIndex: 'description', key: 'description', ellipsis: true },
  { title: '分组', dataIndex: 'group', key: 'group', width: 120 },
  { title: '状态', key: 'status', width: 100 },
  { title: '最近变更', key: 'updatedAt', width: 160 },
  { title: '操作', key: 'action', width: 240, fixed: 'right' as const },
])

// 弹窗状态
const modalVisible = ref(false)
const modalMode = ref<'create' | 'edit'>('create')
const submitting = ref(false)
const form = reactive<FormState>({
  key: '',
  description: '',
  group: '',
  ruleJson: '{}',
  status: 'Disabled',
})

// 确认弹窗（启停）
const confirmVisible = ref(false)
const confirmAction = ref<{ kind: 'enable' | 'disable'; flag: FeatureFlagDto } | null>(null)
const confirmDanger = computed(() => confirmAction.value?.kind === 'disable')
const confirmTitle = computed(() =>
  confirmAction.value?.kind === 'disable' ? '停用功能开关' : '启用功能开关')
const confirmContent = computed(() =>
  confirmAction.value?.kind === 'disable'
    ? `停用后该功能对所有用户立即失效，可能影响线上行为。可随时启用恢复。`
    : `启用后该功能将根据规则立即生效。`)

// 评估抽屉
const drawerVisible = ref(false)
const drawerLoading = ref(false)
const evaluateKey = ref('')
const evaluateContext = ref('{\n  "userId": "u-1",\n  "role": "Admin"\n}')
const evaluateResult = ref<EvaluateFlagResultDto | null>(null)

async function loadList(): Promise<void> {
  loading.value = true
  try {
    const params = {
      key: filter.key || undefined,
      status: filter.status.length ? filter.status : undefined,
      group: filter.group || undefined,
      page: filter.page,
      pageSize: filter.pageSize,
    }
    const res = await featureFlagsApi.list(params)
    dataList.value = res.items
    total.value = res.total
  } catch (e) {
    if (e instanceof BusinessError) message.error(e.message)
    else message.error('加载功能开关失败')
  } finally {
    loading.value = false
  }
}

function onSearch(): void {
  filter.page = 1
  loadList()
}

function onTableChange(pag: { current?: number; pageSize?: number }): void {
  filter.page = pag.current ?? 1
  filter.pageSize = pag.pageSize ?? 20
  loadList()
}

function openCreate(): void {
  modalMode.value = 'create'
  Object.assign(form, {
    flagId: undefined,
    key: '',
    description: '',
    group: '',
    ruleJson: '{}',
    status: 'Disabled',
  })
  modalVisible.value = true
}

function openEdit(flag: FeatureFlagDto): void {
  modalMode.value = 'edit'
  Object.assign(form, {
    flagId: flag.flagId,
    key: flag.key,
    description: flag.description,
    group: flag.group,
    ruleJson: flag.ruleJson,
    status: flag.status,
  })
  modalVisible.value = true
}

function validateRuleJson(): boolean {
  try {
    JSON.parse(form.ruleJson)
    return true
  } catch {
    message.error('规则 JSON 格式不正确')
    return false
  }
}

async function onSubmit(): Promise<void> {
  if (!form.key.trim()) {
    message.error('Key 必填')
    return
  }
  if (!form.group.trim()) {
    message.error('分组必填')
    return
  }
  if (!validateRuleJson()) return
  submitting.value = true
  try {
    const body: SaveFeatureFlagDto = {
      key: form.key.trim(),
      description: form.description.trim(),
      group: form.group.trim(),
      ruleJson: form.ruleJson,
      status: form.status,
    }
    if (modalMode.value === 'create') {
      await featureFlagsApi.create(body)
      message.success('开关已创建')
    } else if (form.flagId) {
      await featureFlagsApi.update(form.flagId, body)
      message.success('开关已更新')
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

function askToggle(flag: FeatureFlagDto): void {
  confirmAction.value = {
    kind: flag.status === 'Enabled' ? 'disable' : 'enable',
    flag,
  }
  confirmVisible.value = true
}

async function onConfirmToggle(): Promise<void> {
  if (!confirmAction.value) return
  const { kind, flag } = confirmAction.value
  try {
    if (kind === 'enable') {
      await featureFlagsApi.enable(flag.flagId)
      message.success('开关已启用')
    } else {
      await featureFlagsApi.disable(flag.flagId)
      message.success('开关已停用')
    }
    confirmVisible.value = false
    loadList()
  } catch (e) {
    if (e instanceof BusinessError) message.error(e.message)
    else message.error('操作失败')
  }
}

function openEvaluate(flag: FeatureFlagDto): void {
  evaluateKey.value = flag.key
  evaluateContext.value = '{\n  "userId": "u-1",\n  "role": "Admin"\n}'
  evaluateResult.value = null
  drawerVisible.value = true
}

async function onEvaluate(): Promise<void> {
  let context: Record<string, unknown>
  try {
    context = JSON.parse(evaluateContext.value)
  } catch {
    message.error('上下文 JSON 格式不正确')
    return
  }
  drawerLoading.value = true
  try {
    evaluateResult.value = await featureFlagsApi.evaluate({
      key: evaluateKey.value,
      context,
    })
  } catch (e) {
    if (e instanceof BusinessError) message.error(e.message)
    else message.error('评估失败')
  } finally {
    drawerLoading.value = false
  }
}

function statusTagColor(status: FeatureFlagStatus): string {
  return status === 'Enabled' ? 'success' : 'default'
}

function statusTagText(status: FeatureFlagStatus): string {
  return status === 'Enabled' ? '启用' : '停用'
}

onMounted(() => {
  loadList()
})
</script>

<template>
  <div class="feature-flags-page">
    <!-- 区域 A：筛选条 -->
    <a-card :bordered="false" class="filter-card">
      <a-space :size="12" wrap>
        <a-input
          v-model:value="filter.key"
          placeholder="搜索 Key"
          allow-clear
          style="width: 200px"
          @press-enter="onSearch"
        />
        <a-select
          v-model:value="filter.status"
          mode="multiple"
          placeholder="状态"
          allow-clear
          style="width: 180px"
          :options="[
            { label: '启用', value: 'Enabled' },
            { label: '停用', value: 'Disabled' },
          ]"
        />
        <a-input
          v-model:value="filter.group"
          placeholder="分组"
          allow-clear
          style="width: 160px"
          @press-enter="onSearch"
        />
        <a-button type="primary" @click="onSearch">查询</a-button>
        <PermissionGuard permission="feature:write">
          <a-button type="primary" @click="openCreate">
            <PlusOutlined />新建开关
          </a-button>
        </PermissionGuard>
      </a-space>
    </a-card>

    <!-- 区域 B：主表格 -->
    <a-card :bordered="false" style="margin-top: 16px">
      <a-table
        :columns="columns"
        :data-source="dataList"
        :loading="loading"
        :row-key="(r: FeatureFlagDto) => r.flagId"
        :pagination="{
          current: filter.page,
          pageSize: filter.pageSize,
          total,
          showSizeChanger: true,
          showTotal: (t: number) => `共 ${t} 条`,
        }"
        @change="onTableChange"
      >
        <template #emptyText>
          <EmptyState description="暂无功能开关" action-text="新建开关" @action="openCreate" />
        </template>
        <template #bodyCell="{ column, record }">
          <template v-if="column.key === 'status'">
            <a-tag :color="statusTagColor(record.status)">{{ statusTagText(record.status) }}</a-tag>
          </template>
          <template v-else-if="column.key === 'updatedAt'">
            {{ formatDateTime(record.updatedAt) }}
          </template>
          <template v-else-if="column.key === 'action'">
            <a-space :size="4">
              <PermissionGuard permission="feature:write">
                <a-button type="link" size="small" @click="openEdit(record)">
                  <EditOutlined />编辑
                </a-button>
              </PermissionGuard>
              <a-button
                type="link"
                size="small"
                :danger="record.status === 'Enabled'"
                @click="askToggle(record)"
              >
                {{ record.status === 'Enabled' ? '停用' : '启用' }}
              </a-button>
              <a-button type="link" size="small" @click="openEvaluate(record)">
                <PlayCircleOutlined />评估
              </a-button>
            </a-space>
          </template>
        </template>
      </a-table>
    </a-card>

    <!-- 区域 C：新建/编辑弹窗 -->
    <a-modal
      v-model:open="modalVisible"
      :title="modalMode === 'create' ? '新建功能开关' : '编辑功能开关'"
      width="560px"
      :confirm-loading="submitting"
      @ok="onSubmit"
    >
      <a-form layout="vertical">
        <a-form-item label="Key" required>
          <a-input
            v-model:value="form.key"
            :disabled="modalMode === 'edit'"
            placeholder="如 order.enable-new-checkout"
            style="font-family: 'SF Mono', Consolas, monospace"
          />
        </a-form-item>
        <a-form-item label="描述">
          <a-textarea v-model:value="form.description" :rows="2" placeholder="开关用途说明" />
        </a-form-item>
        <a-form-item label="分组" required>
          <a-input v-model:value="form.group" placeholder="如 payment / order / notify" />
        </a-form-item>
        <a-form-item label="规则 JSON" required>
          <a-textarea
            v-model:value="form.ruleJson"
            :rows="6"
            placeholder='{"op":"eq","field":"role","value":"Admin"}'
            style="font-family: 'SF Mono', Consolas, monospace; font-size: 12px"
          />
        </a-form-item>
        <a-form-item v-if="modalMode === 'create'" label="初始状态">
          <a-radio-group v-model:value="form.status">
            <a-radio value="Enabled">启用</a-radio>
            <a-radio value="Disabled">停用</a-radio>
          </a-radio-group>
        </a-form-item>
      </a-form>
    </a-modal>

    <!-- 区域 D：评估抽屉 -->
    <a-drawer
      v-model:open="drawerVisible"
      title="评估开关"
      width="480px"
    >
      <a-spin :spinning="drawerLoading">
        <a-form layout="vertical">
          <a-form-item label="开关 Key">
            <a-input :value="evaluateKey" disabled />
          </a-form-item>
          <a-form-item label="上下文 JSON">
            <a-textarea
              v-model:value="evaluateContext"
              :rows="10"
              style="font-family: 'SF Mono', Consolas, monospace; font-size: 12px"
            />
          </a-form-item>
          <a-form-item>
            <IdempotencyButton type="primary" :loading="drawerLoading" @click="onEvaluate">
              评估
            </IdempotencyButton>
          </a-form-item>
        </a-form>
        <a-divider v-if="evaluateResult" />
        <a-result
          v-if="evaluateResult"
          :status="evaluateResult.enabled ? 'success' : 'info'"
          :title="evaluateResult.enabled ? '生效' : '不生效'"
        >
          <template #subTitle>
            <div>命中规则：{{ evaluateResult.matchedRule }}</div>
          </template>
        </a-result>
      </a-spin>
    </a-drawer>

    <!-- 启停确认弹窗 -->
    <ConfirmDialog
      :open="confirmVisible"
      :danger="confirmDanger"
      :title="confirmTitle"
      :content="confirmContent"
      @confirm="onConfirmToggle"
      @cancel="confirmVisible = false"
    />
  </div>
</template>

<style scoped>
.filter-card :deep(.ant-card-body) {
  padding: 16px 24px;
}
</style>
