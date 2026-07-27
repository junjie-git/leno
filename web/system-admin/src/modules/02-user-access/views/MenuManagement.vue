<!-- web/system-admin/src/modules/02-user-access/views/MenuManagement.vue -->
<template>
  <div class="menu-management">
    <!-- 区域 A：工具栏 -->
    <a-card :bordered="false" class="toolbar-card">
      <div class="toolbar">
        <a-space>
          <a-button type="primary" @click="onCreateRoot">
            <template #icon><PlusOutlined /></template>
            新增根菜单
          </a-button>
          <a-button @click="toggleExpandAll">
            {{ allExpanded ? '折叠全部' : '展开全部' }}
          </a-button>
        </a-space>
        <span class="toolbar-tip">拖拽行可调整菜单排序与层级</span>
      </div>
    </a-card>

    <!-- 区域 B：菜单树表格 -->
    <a-card :bordered="false" class="table-card">
      <a-spin :spinning="loading || sortLoading">
        <div v-if="!loading && menuStore.menus.length === 0" class="empty-wrap">
          <EmptyState
            description="暂无菜单数据"
            action-text="新增根菜单"
            @action="onCreateRoot"
          />
        </div>
        <TreeTableDraggable
          v-else
          :data="menuStore.menus"
          :columns="columns"
          :row-key="rowKey"
          :parent-key="parentKey"
          :expanded-keys="expandedKeys"
          @drop="onDrop"
          @expand="onExpand"
        >
          <template #bodyCell="{ column, record }">
            <template v-if="column.key === 'name'">
              <span class="menu-name">{{ record.name }}</span>
            </template>
            <template v-else-if="column.key === 'icon'">
              <span v-if="record.icon" class="icon-name">{{ record.icon }}</span>
              <span v-else class="empty-cell">—</span>
            </template>
            <template v-else-if="column.key === 'path'">
              <span v-if="record.path" class="path-text">{{ record.path }}</span>
              <span v-else class="empty-cell">—</span>
            </template>
            <template v-else-if="column.key === 'type'">
              <StatusTag type="menuType" :status="record.type" />
            </template>
            <template v-else-if="column.key === 'sort'">
              {{ record.sort }}
            </template>
            <template v-else-if="column.key === 'status'">
              <a-tag v-if="record.visible" color="success">启用</a-tag>
              <a-tag v-else color="default">禁用</a-tag>
            </template>
            <template v-else-if="column.key === 'roles'">
              <a-tag v-for="r in record.roles" :key="r" color="blue">{{ r }}</a-tag>
            </template>
            <template v-else-if="column.key === 'action'">
              <a-space>
                <a-button type="link" size="small" @click="onEdit(record)">编辑</a-button>
                <a-button type="link" size="small" @click="onAddChild(record)">新增子菜单</a-button>
                <a-popconfirm
                  title="确定删除该菜单吗？子菜单将一并删除。"
                  ok-text="删除"
                  ok-type="danger"
                  cancel-text="取消"
                  @confirm="onDelete(record)"
                >
                  <a-button type="link" size="small" danger>删除</a-button>
                </a-popconfirm>
              </a-space>
            </template>
          </template>
        </TreeTableDraggable>
      </a-spin>
    </a-card>

    <!-- 区域 C：新建/编辑抽屉 -->
    <a-drawer
      v-model:open="drawerOpen"
      :title="formMode === 'create' ? '新增菜单' : '编辑菜单'"
      placement="right"
      width="520"
      :destroy-on-close="true"
    >
      <a-form
        ref="formRef"
        :model="formData"
        :rules="formRules"
        layout="vertical"
      >
        <a-form-item label="父级菜单" name="parentId">
          <a-tree-select
            v-model:value="formData.parentId"
            :tree-data="treeSelectData"
            allow-clear
            placeholder="留空为根菜单"
            tree-default-expand-all
            style="width: 100%"
          />
        </a-form-item>

        <a-form-item label="菜单名称" name="name">
          <a-input
            v-model:value="formData.name"
            placeholder="请输入菜单名称"
            :maxlength="32"
          />
        </a-form-item>

        <a-form-item label="菜单类型" name="type">
          <a-radio-group v-model:value="formData.type">
            <a-radio v-for="opt in typeOptions" :key="opt.value" :value="opt.value">
              {{ opt.label }}
            </a-radio>
          </a-radio-group>
        </a-form-item>

        <a-form-item v-if="formData.type !== 'Button'" label="路由路径" name="path">
          <a-input
            v-model:value="formData.path"
            placeholder="如 /user-access/menus"
          />
        </a-form-item>

        <a-form-item v-if="formData.type === 'Menu'" label="前端组件" name="component">
          <a-auto-complete
            v-model:value="formData.component"
            :options="componentOptions"
            placeholder="如 02-user-access/views/MenuManagement"
            allow-clear
          />
        </a-form-item>

        <a-form-item label="图标" name="icon">
          <a-input
            v-model:value="formData.icon"
            placeholder="Ant Design 图标名，如 MenuOutlined"
            allow-clear
          />
        </a-form-item>

        <a-form-item label="排序号" name="sort">
          <a-input-number
            v-model:value="formData.sort"
            :min="0"
            style="width: 100%"
          />
        </a-form-item>

        <a-form-item label="权限标识" name="permission">
          <a-input
            v-model:value="formData.permission"
            placeholder="如 menu:write"
            allow-clear
          />
        </a-form-item>

        <a-form-item label="可访问角色" name="roles">
          <a-checkbox-group v-model:value="formData.roles" :options="roleOptions" />
        </a-form-item>

        <a-form-item label="是否可见" name="visible">
          <a-switch v-model:checked="formData.visible" />
        </a-form-item>

        <a-form-item label="缓存 (KeepAlive)" name="cache">
          <a-switch v-model:checked="formData.cache" />
        </a-form-item>
      </a-form>

      <template #footer>
        <div class="drawer-footer">
          <a-space>
            <a-button @click="drawerOpen = false">取消</a-button>
            <IdempotencyButton type="primary" :loading="submitting" @click="onSubmit">
              {{ formMode === 'create' ? '创建' : '保存' }}
            </IdempotencyButton>
          </a-space>
        </div>
      </template>
    </a-drawer>
  </div>
</template>

<script setup lang="ts">
import { computed, onMounted, reactive, ref, watch } from 'vue'
import { message } from 'ant-design-vue'
import type { TableColumnsType } from 'ant-design-vue'
import type { FormInstance, Rule } from 'ant-design-vue/es/form'
import { PlusOutlined } from '@ant-design/icons-vue'
import { useMenuStore } from '@/shared/menu/menu.store'
import {
  TreeTableDraggable,
  StatusTag,
  EmptyState,
  IdempotencyButton,
} from '@/shared/components'
import { componentMap } from '@/shared/router/component-map'
import type {
  MenuDto,
  MenuType,
  CreateMenuDto,
  UpdateMenuDto,
  MenuSortItemDto,
} from '../types/menu.dto'

const menuStore = useMenuStore()

// ---------------------------------------------------------------------------
// 列定义
// ---------------------------------------------------------------------------
const columns: TableColumnsType = [
  { title: '菜单名称', dataIndex: 'name', key: 'name', width: 200 },
  { title: '图标', dataIndex: 'icon', key: 'icon', width: 150, ellipsis: true },
  { title: '路由路径', dataIndex: 'path', key: 'path', width: 220, ellipsis: true },
  { title: '类型', key: 'type', width: 90 },
  { title: '排序', dataIndex: 'sort', key: 'sort', width: 70 },
  { title: '状态', key: 'status', width: 90 },
  { title: '角色', key: 'roles', width: 140 },
  { title: '操作', key: 'action', width: 220, fixed: 'right' },
]

const rowKey = (record: MenuDto): string => record.id
const parentKey = (record: MenuDto): string | null => record.parentId

// ---------------------------------------------------------------------------
// 加载状态
// ---------------------------------------------------------------------------
const loading = ref(true)
const sortLoading = ref(false)

async function fetchMenus(): Promise<void> {
  loading.value = true
  try {
    await menuStore.fetchMenus()
  } catch {
    message.error('加载菜单树失败')
  } finally {
    loading.value = false
  }
}

// ---------------------------------------------------------------------------
// 展开 / 折叠
// ---------------------------------------------------------------------------
const expandedKeys = ref<string[]>([])

function collectAllIds(nodes: MenuDto[]): string[] {
  const ids: string[] = []
  for (const node of nodes) {
    ids.push(node.id)
    if (node.children?.length) {
      ids.push(...collectAllIds(node.children))
    }
  }
  return ids
}

const allKeys = computed(() => collectAllIds(menuStore.menus))

const allExpanded = computed(
  () => allKeys.value.length > 0 && expandedKeys.value.length >= allKeys.value.length,
)

function toggleExpandAll(): void {
  if (allExpanded.value) {
    expandedKeys.value = []
  } else {
    expandedKeys.value = [...allKeys.value]
  }
}

function onExpand(keys: string[]): void {
  expandedKeys.value = keys
}

// ---------------------------------------------------------------------------
// 拖拽排序
// ---------------------------------------------------------------------------
interface DropPayload {
  dragKey: string
  dropKey: string
  position: 'before' | 'after' | 'inside'
}

function deepClone<T>(value: T): T {
  return JSON.parse(JSON.stringify(value)) as T
}

function findAndRemove(nodes: MenuDto[], id: string): MenuDto | null {
  for (let i = 0; i < nodes.length; i++) {
    const node = nodes[i]
    if (!node) continue
    if (node.id === id) {
      return nodes.splice(i, 1)[0] ?? null
    }
    if (node.children?.length) {
      const found = findAndRemove(node.children, id)
      if (found) return found
    }
  }
  return null
}

function findNode(nodes: MenuDto[], id: string): MenuDto | null {
  for (const node of nodes) {
    if (node.id === id) return node
    if (node.children?.length) {
      const found = findNode(node.children, id)
      if (found) return found
    }
  }
  return null
}

function findParentArray(nodes: MenuDto[], id: string): MenuDto[] | null {
  for (const node of nodes) {
    if (node.id === id) return nodes
    if (node.children?.length) {
      const found = findParentArray(node.children, id)
      if (found) return found
    }
  }
  return null
}

function performMove(
  tree: MenuDto[],
  dragKey: string,
  dropKey: string,
  position: 'before' | 'after' | 'inside',
): MenuDto[] | null {
  const cloned = deepClone(tree)
  const dragNode = findAndRemove(cloned, dragKey)
  if (!dragNode) return null
  const dropNode = findNode(cloned, dropKey)
  if (!dropNode) return null

  if (position === 'inside') {
    if (!dropNode.children) dropNode.children = []
    dropNode.children.push(dragNode)
    dragNode.parentId = dropNode.id
  } else {
    const parentArr = findParentArray(cloned, dropKey)
    if (!parentArr) return null
    const idx = parentArr.findIndex((n) => n.id === dropKey)
    if (idx === -1) return null
    if (position === 'before') {
      parentArr.splice(idx, 0, dragNode)
    } else {
      parentArr.splice(idx + 1, 0, dragNode)
    }
    dragNode.parentId = dropNode.parentId
  }
  return cloned
}

function flattenForSort(nodes: MenuDto[], parentId: string | null = null): MenuSortItemDto[] {
  const result: MenuSortItemDto[] = []
  nodes.forEach((node, index) => {
    result.push({ id: node.id, parentId, sort: index })
    if (node.children?.length) {
      result.push(...flattenForSort(node.children, node.id))
    }
  })
  return result
}

async function onDrop(payload: DropPayload): Promise<void> {
  // 禁止将节点拖入 Button 类型节点
  const dropNode = findNode(menuStore.menus, payload.dropKey)
  if (payload.position === 'inside' && dropNode?.type === 'Button') {
    message.warning('按钮类型不可作为父级菜单')
    return
  }

  const newTree = performMove(
    menuStore.menus,
    payload.dragKey,
    payload.dropKey,
    payload.position,
  )
  if (!newTree) {
    message.warning('无法移动到该位置')
    return
  }

  const sortItems = flattenForSort(newTree)
  sortLoading.value = true
  try {
    await menuStore.sortMenus(sortItems)
    message.success('排序已更新')
  } catch {
    message.error('排序更新失败')
  } finally {
    sortLoading.value = false
  }
}

// ---------------------------------------------------------------------------
// 表单
// ---------------------------------------------------------------------------
interface MenuFormState {
  parentId: string | undefined
  name: string
  type: MenuType
  path: string
  component: string
  icon: string
  sort: number
  permission: string
  roles: string[]
  visible: boolean
  cache: boolean
}

const drawerOpen = ref(false)
const formMode = ref<'create' | 'edit'>('create')
const formRef = ref<FormInstance>()
const submitting = ref(false)
const editingId = ref<string | null>(null)

const formData = reactive<MenuFormState>({
  parentId: undefined,
  name: '',
  type: 'Menu',
  path: '',
  component: '',
  icon: '',
  sort: 0,
  permission: '',
  roles: ['Admin'],
  visible: true,
  cache: false,
})

const typeOptions: { label: string; value: MenuType }[] = [
  { label: '目录', value: 'Directory' },
  { label: '菜单', value: 'Menu' },
  { label: '按钮', value: 'Button' },
]

const roleOptions: { label: string; value: string }[] = [
  { label: 'Admin', value: 'Admin' },
  { label: 'Operator', value: 'Operator' },
]

const componentOptions = computed(() =>
  Object.keys(componentMap).map((key) => ({ value: key, label: key })),
)

interface TreeSelectNode {
  value: string
  label: string
  children?: TreeSelectNode[]
}

function toTreeSelectData(menus: MenuDto[], excludeId: string | null): TreeSelectNode[] {
  const result: TreeSelectNode[] = []
  for (const menu of menus) {
    if (menu.id === excludeId) continue
    if (menu.type === 'Button') continue
    const childNodes = menu.children?.length
      ? toTreeSelectData(menu.children, excludeId)
      : undefined
    result.push({
      value: menu.id,
      label: menu.name,
      children: childNodes && childNodes.length > 0 ? childNodes : undefined,
    })
  }
  return result
}

const treeSelectData = computed<TreeSelectNode[]>(() => {
  const excludeId = formMode.value === 'edit' ? editingId.value : null
  return toTreeSelectData(menuStore.menus, excludeId)
})

const formRules = computed<Record<string, Rule[]>>(() => ({
  name: [
    { required: true, message: '请输入菜单名称', trigger: 'blur' },
    { min: 1, max: 32, message: '长度需在 1-32 字符之间', trigger: 'blur' },
  ],
  type: [{ required: true, message: '请选择菜单类型', trigger: 'change' }],
  path:
    formData.type !== 'Button'
      ? [
          { required: true, message: '请输入路由路径', trigger: 'blur' },
          {
            pattern: /^(\/[a-z0-9-]+)+$/,
            message: '以 / 开头，仅含小写字母、数字和连字符',
            trigger: 'blur',
          },
        ]
      : [],
  component:
    formData.type === 'Menu'
      ? [{ required: true, message: '请输入或选择前端组件路径', trigger: 'blur' }]
      : [],
  sort: [
    { required: true, message: '请输入排序号', trigger: 'blur' },
    { type: 'number', min: 0, message: '排序号须 ≥ 0', trigger: 'blur' },
  ],
}))

watch(
  () => formData.type,
  () => {
    formRef.value?.clearValidate(['path', 'component'])
  },
)

function resetForm(): void {
  formData.parentId = undefined
  formData.name = ''
  formData.type = 'Menu'
  formData.path = ''
  formData.component = ''
  formData.icon = ''
  formData.sort = 0
  formData.permission = ''
  formData.roles = ['Admin']
  formData.visible = true
  formData.cache = false
}

function onCreateRoot(): void {
  formMode.value = 'create'
  editingId.value = null
  resetForm()
  formData.parentId = undefined
  drawerOpen.value = true
}

function onAddChild(record: MenuDto): void {
  formMode.value = 'create'
  editingId.value = null
  resetForm()
  formData.parentId = record.id
  formData.type = record.type === 'Directory' ? 'Menu' : 'Button'
  drawerOpen.value = true
}

function onEdit(record: MenuDto): void {
  formMode.value = 'edit'
  editingId.value = record.id
  formData.parentId = record.parentId ?? undefined
  formData.name = record.name
  formData.type = record.type
  formData.path = record.path
  formData.component = record.component ?? ''
  formData.icon = record.icon ?? ''
  formData.sort = record.sort
  formData.permission = record.permission ?? ''
  formData.roles = [...record.roles]
  formData.visible = record.visible
  formData.cache = record.cache
  drawerOpen.value = true
}

function buildBody(): CreateMenuDto {
  return {
    parentId: formData.parentId ?? null,
    name: formData.name,
    type: formData.type,
    path: formData.type === 'Button' ? '' : formData.path,
    component: formData.type === 'Menu' && formData.component ? formData.component : null,
    icon: formData.icon || null,
    sort: formData.sort,
    permission: formData.permission || null,
    roles: formData.roles,
    visible: formData.visible,
    cache: formData.cache,
  }
}

async function onSubmit(): Promise<void> {
  try {
    await formRef.value?.validate()
  } catch {
    return
  }
  submitting.value = true
  try {
    if (formMode.value === 'create') {
      await menuStore.createMenu(buildBody())
      message.success('菜单已创建')
    } else if (editingId.value) {
      const body: UpdateMenuDto = buildBody()
      await menuStore.updateMenu(editingId.value, body)
      message.success('菜单已更新')
    }
    drawerOpen.value = false
  } catch {
    message.error(formMode.value === 'create' ? '创建菜单失败' : '更新菜单失败')
  } finally {
    submitting.value = false
  }
}

async function onDelete(record: MenuDto): Promise<void> {
  try {
    await menuStore.deleteMenu(record.id)
    message.success('菜单已删除')
  } catch {
    message.error('删除失败：可能存在子菜单，请先删除子节点')
  }
}

onMounted(() => {
  fetchMenus()
})
</script>

<style scoped>
.menu-management {
  display: flex;
  flex-direction: column;
  gap: 16px;
}
.toolbar-card :deep(.ant-card-body) {
  padding: 16px 24px;
}
.toolbar {
  display: flex;
  align-items: center;
  justify-content: space-between;
}
.toolbar-tip {
  color: #8c8c8c;
  font-size: 13px;
}
.table-card :deep(.ant-card-body) {
  padding: 0;
}
.empty-wrap {
  padding: 48px 0;
}
.menu-name {
  font-weight: 500;
}
.icon-name {
  font-family: 'SFMono-Regular', Consolas, 'Liberation Mono', Menlo, monospace;
  font-size: 13px;
  color: #595959;
}
.path-text {
  font-family: 'SFMono-Regular', Consolas, 'Liberation Mono', Menlo, monospace;
  font-size: 13px;
  color: #595959;
}
.empty-cell {
  color: #bfbfbf;
}
.drawer-footer {
  text-align: right;
}
</style>
