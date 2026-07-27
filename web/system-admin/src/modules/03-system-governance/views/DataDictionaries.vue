<!-- web/system-admin/src/modules/03-system-governance/views/DataDictionaries.vue -->
<!-- 数据字典管理：左字典列表 + 右详情 + 字典项表格 CRUD -->
<script setup lang="ts">
import { ref, reactive, onMounted, computed } from 'vue'
import { message } from 'ant-design-vue'
import {
  PlusOutlined,
  EditOutlined,
  DeleteOutlined,
  DatabaseOutlined,
} from '@ant-design/icons-vue'
import { dataDictionariesApi } from '../api/data-dictionaries.api'
import type {
  DataDictionaryDto,
  SaveDataDictionaryDto,
  DictionaryItemDto,
  AddDictionaryItemDto,
  UpdateDictionaryItemDto,
  DictionaryStatus,
} from '../types/data-dictionary.dto'
import ConfirmDialog from '@/shared/components/ConfirmDialog.vue'
import PermissionGuard from '@/shared/auth/PermissionGuard.vue'
import EmptyState from '@/shared/components/EmptyState.vue'
import { BusinessError } from '@/shared/http/errors'

interface DictFormState {
  dictionaryId?: string
  code: string
  name: string
  description: string
}

interface ItemFormState {
  itemId?: string
  code: string
  displayName: string
  sortOrder: number
}

const listLoading = ref(false)
const dictList = ref<DataDictionaryDto[]>([])
const searchKeyword = ref('')
const currentDict = ref<DataDictionaryDto | null>(null)

// 字典弹窗
const dictModalVisible = ref(false)
const dictModalMode = ref<'create' | 'edit'>('create')
const dictSubmitting = ref(false)
const dictForm = reactive<DictFormState>({
  code: '',
  name: '',
  description: '',
})

// 字典项弹窗
const itemModalVisible = ref(false)
const itemModalMode = ref<'create' | 'edit'>('create')
const itemSubmitting = ref(false)
const itemForm = reactive<ItemFormState>({
  code: '',
  displayName: '',
  sortOrder: 0,
})

// 移除确认
const removeConfirmVisible = ref(false)
const removeTarget = ref<{ dict: DataDictionaryDto; item: DictionaryItemDto } | null>(null)

// 启停确认
const toggleConfirmVisible = ref(false)
const toggleTarget = ref<{ kind: 'enable' | 'disable'; dict: DataDictionaryDto } | null>(null)
const toggleDanger = computed(() => toggleTarget.value?.kind === 'disable')
const toggleTitle = computed(() =>
  toggleTarget.value?.kind === 'disable' ? '停用数据字典' : '启用数据字典')
const toggleContent = computed(() =>
  toggleTarget.value?.kind === 'disable'
    ? '停用后该字典及其字典项将不再可用，引用该字典的功能将受影响。可随时启用恢复。'
    : '启用后该字典将立即生效。')

const itemColumns = computed(() => [
  { title: '编码', dataIndex: 'code', key: 'code', width: 180 },
  { title: '显示名', dataIndex: 'displayName', key: 'displayName' },
  { title: '排序', dataIndex: 'sortOrder', key: 'sortOrder', width: 80, align: 'right' as const },
  { title: '状态', key: 'status', width: 100 },
  { title: '操作', key: 'action', width: 160, fixed: 'right' as const },
])

async function loadList(): Promise<void> {
  listLoading.value = true
  try {
    const res = await dataDictionariesApi.list({
      name: searchKeyword.value || undefined,
      page: 1,
      pageSize: 100,
    })
    dictList.value = res.items
    // 默认选中首个
    if (!currentDict.value && res.items.length > 0) {
      await selectDict(res.items[0]!)
    } else if (currentDict.value) {
      // 刷新当前选中字典的详情
      const fresh = res.items.find((d) => d.dictionaryId === currentDict.value!.dictionaryId)
      if (fresh) currentDict.value = fresh
    }
  } catch (e) {
    if (e instanceof BusinessError) message.error(e.message)
    else message.error('加载数据字典失败')
  } finally {
    listLoading.value = false
  }
}

async function selectDict(dict: DataDictionaryDto): Promise<void> {
  currentDict.value = dict
}

function onSearch(): void {
  loadList()
}

function openCreateDict(): void {
  dictModalMode.value = 'create'
  Object.assign(dictForm, { dictionaryId: undefined, code: '', name: '', description: '' })
  dictModalVisible.value = true
}

function openEditDict(): void {
  if (!currentDict.value) return
  dictModalMode.value = 'edit'
  Object.assign(dictForm, {
    dictionaryId: currentDict.value.dictionaryId,
    code: currentDict.value.code,
    name: currentDict.value.name,
    description: currentDict.value.description,
  })
  dictModalVisible.value = true
}

async function onSubmitDict(): Promise<void> {
  if (!dictForm.code.trim()) {
    message.error('编码必填')
    return
  }
  if (!dictForm.name.trim()) {
    message.error('名称必填')
    return
  }
  dictSubmitting.value = true
  try {
    const body: SaveDataDictionaryDto = {
      code: dictForm.code.trim(),
      name: dictForm.name.trim(),
      description: dictForm.description.trim(),
    }
    if (dictModalMode.value === 'create') {
      const created = await dataDictionariesApi.create(body)
      message.success('字典已创建')
      dictModalVisible.value = false
      await loadList()
      await selectDict(created)
    } else if (dictForm.dictionaryId) {
      await dataDictionariesApi.update(dictForm.dictionaryId, body)
      message.success('字典已更新')
      dictModalVisible.value = false
      loadList()
    }
  } catch (e) {
    if (e instanceof BusinessError) {
      message.error(e.message || '字典编码已存在')
    } else {
      message.error('保存失败')
    }
  } finally {
    dictSubmitting.value = false
  }
}

function askToggleDict(): void {
  if (!currentDict.value) return
  toggleTarget.value = {
    kind: currentDict.value.status === 'Enabled' ? 'disable' : 'enable',
    dict: currentDict.value,
  }
  toggleConfirmVisible.value = true
}

async function onConfirmToggleDict(): Promise<void> {
  if (!toggleTarget.value) return
  const { kind, dict } = toggleTarget.value
  try {
    if (kind === 'enable') {
      await dataDictionariesApi.enable(dict.dictionaryId)
      message.success('字典已启用')
    } else {
      await dataDictionariesApi.disable(dict.dictionaryId)
      message.success('字典已停用')
    }
    toggleConfirmVisible.value = false
    loadList()
  } catch (e) {
    if (e instanceof BusinessError) message.error(e.message)
    else message.error('操作失败')
  }
}

function openCreateItem(): void {
  itemModalMode.value = 'create'
  Object.assign(itemForm, {
    itemId: undefined,
    code: '',
    displayName: '',
    sortOrder: currentDict.value?.items.length ?? 0,
  })
  itemModalVisible.value = true
}

function openEditItem(item: DictionaryItemDto): void {
  itemModalMode.value = 'edit'
  Object.assign(itemForm, {
    itemId: item.itemId,
    code: item.code,
    displayName: item.displayName,
    sortOrder: item.sortOrder,
  })
  itemModalVisible.value = true
}

async function onSubmitItem(): Promise<void> {
  if (!currentDict.value) return
  if (!itemForm.code.trim()) {
    message.error('项编码必填')
    return
  }
  if (!itemForm.displayName.trim()) {
    message.error('显示名必填')
    return
  }
  itemSubmitting.value = true
  try {
    if (itemModalMode.value === 'create') {
      const body: AddDictionaryItemDto = {
        code: itemForm.code.trim(),
        displayName: itemForm.displayName.trim(),
        sortOrder: itemForm.sortOrder,
      }
      await dataDictionariesApi.addItem(currentDict.value.dictionaryId, body)
      message.success('字典项已新增')
    } else if (itemForm.itemId) {
      const body: UpdateDictionaryItemDto = {
        code: itemForm.code.trim(),
        displayName: itemForm.displayName.trim(),
        sortOrder: itemForm.sortOrder,
      }
      await dataDictionariesApi.updateItem(currentDict.value.dictionaryId, itemForm.itemId, body)
      message.success('字典项已更新')
    }
    itemModalVisible.value = false
    loadList()
  } catch (e) {
    if (e instanceof BusinessError) {
      message.error(e.message || '字典编码已存在')
    } else {
      message.error('保存失败')
    }
  } finally {
    itemSubmitting.value = false
  }
}

function askRemoveItem(item: DictionaryItemDto): void {
  if (!currentDict.value) return
  removeTarget.value = { dict: currentDict.value, item }
  removeConfirmVisible.value = true
}

async function onConfirmRemoveItem(): Promise<void> {
  if (!removeTarget.value) return
  const { dict, item } = removeTarget.value
  try {
    await dataDictionariesApi.removeItem(dict.dictionaryId, item.itemId)
    message.success('字典项已移除')
    removeConfirmVisible.value = false
    loadList()
  } catch (e) {
    if (e instanceof BusinessError) {
      message.error(e.message || '该项被引用，无法移除')
    } else {
      message.error('移除失败')
    }
  }
}

function statusTagColor(status: DictionaryStatus): string {
  return status === 'Enabled' ? 'success' : 'default'
}

function statusTagText(status: DictionaryStatus): string {
  return status === 'Enabled' ? '启用' : '停用'
}

onMounted(() => {
  loadList()
})
</script>

<template>
  <div class="data-dictionaries-page">
    <a-row :gutter="16">
      <!-- 区域 A：左侧字典列表 -->
      <a-col :xs="24" :md="8" :lg="7">
        <a-card :bordered="false" title="数据字典">
          <template #extra>
            <PermissionGuard permission="dictionary:write">
              <a-button type="primary" size="small" @click="openCreateDict">
                <PlusOutlined />新增字典
              </a-button>
            </PermissionGuard>
          </template>
          <a-input
            v-model:value="searchKeyword"
            placeholder="搜索名称/编码"
            allow-clear
            style="margin-bottom: 12px"
            @press-enter="onSearch"
          />
          <a-spin :spinning="listLoading">
            <a-list
              v-if="dictList.length > 0"
              :data-source="dictList"
              :split="false"
              size="small"
            >
              <template #renderItem="{ item }">
                <a-list-item
                  :style="{
                    padding: '8px 12px',
                    cursor: 'pointer',
                    borderRadius: '6px',
                    background: currentDict?.dictionaryId === item.dictionaryId ? '#E6F4FF' : 'transparent',
                    marginBottom: '4px',
                  }"
                  @click="selectDict(item)"
                >
                  <a-list-item-meta>
                    <template #avatar>
                      <DatabaseOutlined style="font-size: 16px; color: #1677ff" />
                    </template>
                    <template #title>
                      <span style="font-family: 'SF Mono', Consolas, monospace; font-size: 13px">
                        {{ item.code }}
                      </span>
                    </template>
                    <template #description>
                      {{ item.name }} · {{ item.items.length }} 项
                    </template>
                  </a-list-item-meta>
                  <template #actions>
                    <a-tag :color="statusTagColor(item.status)">{{ statusTagText(item.status) }}</a-tag>
                  </template>
                </a-list-item>
              </template>
            </a-list>
            <EmptyState
              v-else
              description="暂无数据字典"
              action-text="新增字典"
              @action="openCreateDict"
            />
          </a-spin>
        </a-card>
      </a-col>

      <!-- 区域 B+C：右侧详情与字典项 -->
      <a-col :xs="24" :md="16" :lg="17">
        <a-card v-if="currentDict" :bordered="false">
          <!-- 区域 B：字典基本信息 -->
          <a-descriptions :column="2" bordered size="small">
            <a-descriptions-item label="编码">
              <span style="font-family: 'SF Mono', Consolas, monospace">{{ currentDict.code }}</span>
            </a-descriptions-item>
            <a-descriptions-item label="名称">{{ currentDict.name }}</a-descriptions-item>
            <a-descriptions-item label="描述" :span="2">{{ currentDict.description || '—' }}</a-descriptions-item>
            <a-descriptions-item label="状态">
              <a-tag :color="statusTagColor(currentDict.status)">{{ statusTagText(currentDict.status) }}</a-tag>
            </a-descriptions-item>
            <a-descriptions-item label="字典项数">{{ currentDict.items.length }}</a-descriptions-item>
          </a-descriptions>
          <a-space style="margin-top: 12px">
            <PermissionGuard permission="dictionary:write">
              <a-button size="small" @click="openEditDict">
                <EditOutlined />编辑
              </a-button>
            </PermissionGuard>
            <a-button
              size="small"
              :danger="currentDict.status === 'Enabled'"
              @click="askToggleDict"
            >
              {{ currentDict.status === 'Enabled' ? '停用' : '启用' }}
            </a-button>
          </a-space>

          <a-divider style="margin: 16px 0" />

          <!-- 区域 C：字典项表格 -->
          <div style="display: flex; justify-content: space-between; align-items: center; margin-bottom: 12px">
            <h3 style="margin: 0">字典项</h3>
            <PermissionGuard permission="dictionary:write">
              <a-button type="primary" size="small" @click="openCreateItem">
                <PlusOutlined />新增项
              </a-button>
            </PermissionGuard>
          </div>
          <a-table
            :columns="itemColumns"
            :data-source="currentDict.items"
            :row-key="(r: DictionaryItemDto) => r.itemId"
            :pagination="false"
            size="middle"
          >
            <template #emptyText>
              <EmptyState description="暂无字典项" action-text="新增项" @action="openCreateItem" />
            </template>
            <template #bodyCell="{ column, record }">
              <template v-if="column.key === 'code'">
                <span style="font-family: 'SF Mono', Consolas, monospace">{{ record.code }}</span>
              </template>
              <template v-else-if="column.key === 'status'">
                <a-tag :color="statusTagColor(record.status)">{{ statusTagText(record.status) }}</a-tag>
              </template>
              <template v-else-if="column.key === 'action'">
                <a-space :size="4">
                  <PermissionGuard permission="dictionary:write">
                    <a-button type="link" size="small" @click="openEditItem(record)">
                      <EditOutlined />编辑
                    </a-button>
                  </PermissionGuard>
                  <PermissionGuard permission="dictionary:write">
                    <a-button type="link" size="small" danger @click="askRemoveItem(record)">
                      <DeleteOutlined />移除
                    </a-button>
                  </PermissionGuard>
                </a-space>
              </template>
            </template>
          </a-table>
        </a-card>
        <a-card v-else :bordered="false">
          <EmptyState description="请选择左侧字典查看详情" />
        </a-card>
      </a-col>
    </a-row>

    <!-- 字典新建/编辑弹窗 -->
    <a-modal
      v-model:open="dictModalVisible"
      :title="dictModalMode === 'create' ? '新增数据字典' : '编辑数据字典'"
      width="480px"
      :confirm-loading="dictSubmitting"
      @ok="onSubmitDict"
    >
      <a-form layout="vertical">
        <a-form-item label="编码" required>
          <a-input
            v-model:value="dictForm.code"
            :disabled="dictModalMode === 'edit'"
            placeholder="如 order_status"
            style="font-family: 'SF Mono', Consolas, monospace"
          />
        </a-form-item>
        <a-form-item label="名称" required>
          <a-input v-model:value="dictForm.name" placeholder="如 订单状态" />
        </a-form-item>
        <a-form-item label="描述">
          <a-textarea v-model:value="dictForm.description" :rows="2" />
        </a-form-item>
      </a-form>
    </a-modal>

    <!-- 字典项新建/编辑弹窗 -->
    <a-modal
      v-model:open="itemModalVisible"
      :title="itemModalMode === 'create' ? '新增字典项' : '编辑字典项'"
      width="440px"
      :confirm-loading="itemSubmitting"
      @ok="onSubmitItem"
    >
      <a-form layout="vertical">
        <a-form-item label="编码" required>
          <a-input
            v-model:value="itemForm.code"
            placeholder="如 pending / paid / shipped"
            style="font-family: 'SF Mono', Consolas, monospace"
          />
        </a-form-item>
        <a-form-item label="显示名" required>
          <a-input v-model:value="itemForm.displayName" placeholder="如 待支付" />
        </a-form-item>
        <a-form-item label="排序">
          <a-input-number v-model:value="itemForm.sortOrder" :min="0" style="width: 100%" />
        </a-form-item>
      </a-form>
    </a-modal>

    <!-- 移除字典项确认 -->
    <ConfirmDialog
      :open="removeConfirmVisible"
      danger
      title="移除字典项"
      content="移除后该字典项将不再可用，已引用该项的业务需手动迁移。此操作幂等，重复请求无副作用。"
      @confirm="onConfirmRemoveItem"
      @cancel="removeConfirmVisible = false"
    />

    <!-- 字典启停确认 -->
    <ConfirmDialog
      :open="toggleConfirmVisible"
      :danger="toggleDanger"
      :title="toggleTitle"
      :content="toggleContent"
      @confirm="onConfirmToggleDict"
      @cancel="toggleConfirmVisible = false"
    />
  </div>
</template>
