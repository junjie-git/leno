<!-- web/system-admin/src/modules/03-system-governance/views/SystemConfigs.vue -->
<!-- 系统配置管理：左分组导航 + 筛选 + 表格 + 新建/编辑弹窗 + 明文查看 -->
<script setup lang="ts">
import { ref, reactive, onMounted, computed } from 'vue'
import { message } from 'ant-design-vue'
import {
  PlusOutlined,
  EditOutlined,
  EyeOutlined,
  KeyOutlined,
} from '@ant-design/icons-vue'
import { systemConfigsApi } from '../api/system-configs.api'
import type {
  SystemConfigDto,
  SaveSystemConfigDto,
  SystemConfigStatus,
  SystemConfigValueType,
  SystemConfigGroupDto,
} from '../types/system-config.dto'
import ConfirmDialog from '@/shared/components/ConfirmDialog.vue'
import PermissionGuard from '@/shared/auth/PermissionGuard.vue'
import EmptyState from '@/shared/components/EmptyState.vue'
import { formatDateTime } from '@/shared/utils/format'
import { BusinessError } from '@/shared/http/errors'

interface FilterState {
  key: string
  status: SystemConfigStatus[]
  group: string
  page: number
  pageSize: number
}

interface FormState {
  configId?: string
  key: string
  group: string
  valueType: SystemConfigValueType
  value: string
  description: string
  status: SystemConfigStatus
}

const loading = ref(false)
const dataList = ref<SystemConfigDto[]>([])
const total = ref(0)
const groups = ref<SystemConfigGroupDto[]>([])
const selectedGroup = ref<string>('')

const filter = reactive<FilterState>({
  key: '',
  status: [],
  group: '',
  page: 1,
  pageSize: 20,
})

const columns = computed(() => [
  { title: 'Key', dataIndex: 'key', key: 'key', width: 200, ellipsis: true },
  { title: '分组', dataIndex: 'group', key: 'group', width: 120 },
  { title: '值', key: 'valueMasked', ellipsis: true },
  { title: '状态', key: 'status', width: 100 },
  { title: '最近变更', key: 'updatedAt', width: 160 },
  { title: '操作', key: 'action', width: 260, fixed: 'right' as const },
])

const valueTypeOptions: { label: string; value: SystemConfigValueType }[] = [
  { label: '字符串', value: 'String' },
  { label: '整数', value: 'Int' },
  { label: '布尔', value: 'Bool' },
  { label: 'JSON', value: 'Json' },
  { label: '敏感', value: 'Secret' },
]

// 弹窗
const modalVisible = ref(false)
const modalMode = ref<'create' | 'edit'>('create')
const submitting = ref(false)
const revealLoading = ref(false)
const valueVisible = ref(false)
const form = reactive<FormState>({
  key: '',
  group: '',
  valueType: 'String',
  value: '',
  description: '',
  status: 'Enabled',
})

// 确认弹窗
const confirmVisible = ref(false)
const confirmAction = ref<{ kind: 'enable' | 'disable'; config: SystemConfigDto } | null>(null)
const confirmDanger = computed(() => confirmAction.value?.kind === 'disable')
const confirmTitle = computed(() =>
  confirmAction.value?.kind === 'disable' ? '停用系统配置' : '启用系统配置')
const confirmContent = computed(() =>
  confirmAction.value?.kind === 'disable'
    ? '停用后使用该配置的功能将回退到默认值，可能影响线上行为。可随时启用恢复。'
    : '启用后该配置将立即生效。')

async function loadGroups(): Promise<void> {
  try {
    const res = await systemConfigsApi.groups()
    groups.value = res
  } catch {
    // 分组加载失败不阻塞列表
    groups.value = []
  }
}

async function loadList(): Promise<void> {
  loading.value = true
  try {
    const params = {
      key: filter.key || undefined,
      group: selectedGroup.value || filter.group || undefined,
      status: filter.status.length ? filter.status : undefined,
      page: filter.page,
      pageSize: filter.pageSize,
    }
    const res = await systemConfigsApi.list(params)
    dataList.value = res.items
    total.value = res.total
  } catch (e) {
    if (e instanceof BusinessError) message.error(e.message)
    else message.error('加载系统配置失败')
  } finally {
    loading.value = false
  }
}

function onSelectGroup(group: string): void {
  selectedGroup.value = group
  filter.page = 1
  loadList()
}

function onSearch(): void {
  selectedGroup.value = ''
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
    configId: undefined,
    key: '',
    group: selectedGroup.value || '',
    valueType: 'String',
    value: '',
    description: '',
    status: 'Enabled',
  })
  valueVisible.value = false
  modalVisible.value = true
}

function openEdit(config: SystemConfigDto): void {
  modalMode.value = 'edit'
  Object.assign(form, {
    configId: config.configId,
    key: config.key,
    group: config.group,
    valueType: config.valueType,
    value: '', // 编辑时默认空，Secret 类型需点「显示明文」获取
    description: config.description,
    status: config.status,
  })
  valueVisible.value = false
  modalVisible.value = true
}

async function onRevealValue(): Promise<void> {
  if (!form.key) return
  revealLoading.value = true
  try {
    const res = await systemConfigsApi.getByKey(form.key)
    form.value = res.value
    valueVisible.value = true
    message.success('明文已加载')
  } catch (e) {
    if (e instanceof BusinessError) message.error(e.message)
    else message.error('鉴权失败或加载失败')
  } finally {
    revealLoading.value = false
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
  if (modalMode.value === 'create' && !form.value) {
    message.error('值必填')
    return
  }
  submitting.value = true
  try {
    const body: SaveSystemConfigDto = {
      key: form.key.trim(),
      group: form.group.trim(),
      valueType: form.valueType,
      value: form.value,
      description: form.description.trim(),
    }
    if (modalMode.value === 'create') {
      await systemConfigsApi.create(body)
      message.success('配置已创建')
    } else if (form.configId) {
      await systemConfigsApi.update(form.configId, body)
      message.success('配置已更新')
    }
    modalVisible.value = false
    loadList()
    loadGroups()
  } catch (e) {
    if (e instanceof BusinessError) {
      // 409 key 冲突
      message.error(e.message || '配置键已存在')
    } else {
      message.error('保存失败')
    }
  } finally {
    submitting.value = false
  }
}

function askToggle(config: SystemConfigDto): void {
  confirmAction.value = {
    kind: config.status === 'Enabled' ? 'disable' : 'enable',
    config,
  }
  confirmVisible.value = true
}

async function onConfirmToggle(): Promise<void> {
  if (!confirmAction.value) return
  const { kind, config } = confirmAction.value
  try {
    if (kind === 'enable') {
      await systemConfigsApi.enable(config.configId)
      message.success('配置已启用')
    } else {
      await systemConfigsApi.disable(config.configId)
      message.success('配置已停用')
    }
    confirmVisible.value = false
    loadList()
  } catch (e) {
    if (e instanceof BusinessError) message.error(e.message)
    else message.error('操作失败')
  }
}

function statusTagColor(status: SystemConfigStatus): string {
  return status === 'Enabled' ? 'success' : 'default'
}

function statusTagText(status: SystemConfigStatus): string {
  return status === 'Enabled' ? '启用' : '停用'
}

onMounted(() => {
  loadGroups()
  loadList()
})
</script>

<template>
  <div class="system-configs-page">
    <a-row :gutter="16">
      <!-- 区域 A：左侧分组导航 -->
      <a-col :xs="24" :md="6" :lg="5">
        <a-card :bordered="false" title="全部分组">
          <a-menu
            mode="inline"
            :selected-keys="selectedGroup ? [selectedGroup] : []"
            @click="(e: { key: string | number }) => onSelectGroup(String(e.key))"
          >
            <a-menu-item key="">全部</a-menu-item>
            <a-menu-item v-for="g in groups" :key="g.group">
              {{ g.group }} ({{ g.count }})
            </a-menu-item>
          </a-menu>
        </a-card>
      </a-col>

      <!-- 区域 B+C：筛选 + 主表格 -->
      <a-col :xs="24" :md="18" :lg="19">
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
            <a-button type="primary" @click="onSearch">查询</a-button>
            <PermissionGuard permission="config:write">
              <a-button type="primary" @click="openCreate">
                <PlusOutlined />新建配置
              </a-button>
            </PermissionGuard>
          </a-space>
        </a-card>

        <a-card :bordered="false" style="margin-top: 16px">
          <a-table
            :columns="columns"
            :data-source="dataList"
            :loading="loading"
            :row-key="(r: SystemConfigDto) => r.configId"
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
              <EmptyState
                :description="selectedGroup ? '该分组下暂无配置' : '暂无系统配置'"
                action-text="新建配置"
                @action="openCreate"
              />
            </template>
            <template #bodyCell="{ column, record }">
              <template v-if="column.key === 'key'">
                <span style="font-family: 'SF Mono', Consolas, monospace">{{ record.key }}</span>
              </template>
              <template v-else-if="column.key === 'valueMasked'">
                <a-tag v-if="record.valueType === 'Secret'" color="orange">{{ record.valueMasked }}</a-tag>
                <span v-else style="color: #595959; font-size: 12px">{{ record.valueMasked }}</span>
              </template>
              <template v-else-if="column.key === 'status'">
                <a-tag :color="statusTagColor(record.status)">{{ statusTagText(record.status) }}</a-tag>
              </template>
              <template v-else-if="column.key === 'updatedAt'">
                {{ formatDateTime(record.updatedAt) }}
              </template>
              <template v-else-if="column.key === 'action'">
                <a-space :size="4">
                  <PermissionGuard permission="config:write">
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
                </a-space>
              </template>
            </template>
          </a-table>
        </a-card>
      </a-col>
    </a-row>

    <!-- 区域 D：新建/编辑弹窗 -->
    <a-modal
      v-model:open="modalVisible"
      :title="modalMode === 'create' ? '新建系统配置' : '编辑系统配置'"
      width="560px"
      :confirm-loading="submitting"
      @ok="onSubmit"
    >
      <a-form layout="vertical">
        <a-form-item label="Key" required>
          <a-input
            v-model:value="form.key"
            :disabled="modalMode === 'edit'"
            placeholder="如 payment.timeout"
            style="font-family: 'SF Mono', Consolas, monospace"
          />
        </a-form-item>
        <a-form-item label="分组" required>
          <a-input v-model:value="form.group" placeholder="如 payment / notify / cart / search" />
        </a-form-item>
        <a-form-item label="值类型" required>
          <a-select
            v-model:value="form.valueType"
            :options="valueTypeOptions"
            :disabled="modalMode === 'edit'"
          />
        </a-form-item>
        <a-form-item label="值" required>
          <a-input-group compact>
            <a-textarea
              v-if="form.valueType === 'Secret' || form.valueType === 'Json'"
              v-model:value="form.value"
              :rows="3"
              :placeholder="form.valueType === 'Secret' ? '敏感值（创建后掩码展示）' : 'JSON 值'"
              :style="{
                fontFamily: 'SF Mono, Consolas, monospace',
                fontSize: '12px',
                width: 'calc(100% - 100px)',
              }"
            />
            <a-input
              v-else
              v-model:value="form.value"
              :type="form.valueType === 'Secret' && !valueVisible ? 'password' : 'text'"
              placeholder="配置值"
              style="width: calc(100% - 100px)"
            />
            <PermissionGuard permission="config:reveal">
              <a-button
                v-if="modalMode === 'edit' && form.valueType === 'Secret'"
                :loading="revealLoading"
                style="width: 100px"
                @click="onRevealValue"
              >
                <EyeOutlined />{{ valueVisible ? '已显示' : '显示明文' }}
              </a-button>
              <a-button v-else style="width: 100px" disabled>
                <KeyOutlined />明文
              </a-button>
            </PermissionGuard>
          </a-input-group>
        </a-form-item>
        <a-form-item label="描述">
          <a-textarea v-model:value="form.description" :rows="2" placeholder="配置用途说明" />
        </a-form-item>
      </a-form>
    </a-modal>

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
