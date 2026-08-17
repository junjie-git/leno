<!-- web/operations/src/modules/02-product-ops/views/CategoryManagement.vue -->
<template>
  <div class="category-management">
    <!-- 区域 A + B：左侧分类树 -->
    <a-card :bordered="false" class="tree-card" :body-style="{ padding: '12px' }">
      <div class="tree-toolbar">
        <a-button type="primary" size="small" @click="onOpenCreate(null)">新增顶级分类</a-button>
        <a-button size="small" @click="onToggleExpandAll">{{ expandAll ? '折叠全部' : '展开全部' }}</a-button>
      </div>
      <a-input-search
        v-model:value="keyword"
        placeholder="搜索分类名称"
        allow-clear
        class="tree-search"
      />
      <a-spin :spinning="treeLoading">
        <div v-if="treeError" class="tree-state">
          <EmptyState :description="`加载失败：${treeError}`" action-text="重试" @action="reloadTree" />
        </div>
        <div v-else-if="treeData.length === 0" class="tree-state">
          <EmptyState description="暂无分类" action-text="新增顶级分类" @action="onOpenCreate(null)" />
        </div>
        <div v-else-if="displayTree.length === 0" class="tree-state">
          <EmptyState description="未找到匹配的分类" />
        </div>
        <a-tree
          v-else
          :tree-data="displayTree"
          :field-names="{ children: 'children', title: 'name', key: 'id' }"
          :expanded-keys="expandedKeys"
          :selected-keys="selectedKeys"
          block-node
          @expand="onExpand"
          @select="onTreeSelect"
        >
          <template #title="{ key }">
            <span class="tree-node" :aria-label="nodeAriaLabel(String(key))">
              <span class="tree-node-name">
                <span
                  v-for="(seg, index) in titleSegments(nodeMap.get(String(key)))"
                  :key="index"
                  :class="{ 'name-hit': seg.hit }"
                >{{ seg.text }}</span>
              </span>
              <span
                class="status-dot"
                :class="nodeMap.get(String(key))?.status === 'Active' ? 'dot-active' : 'dot-inactive'"
              />
            </span>
          </template>
        </a-tree>
      </a-spin>
    </a-card>

    <!-- 区域 C + D：右侧详情 / 编辑面板 -->
    <a-card :bordered="false" class="detail-card">
      <a-spin :spinning="detailLoading">
        <!-- 详情模式 -->
        <template v-if="panelMode === 'detail'">
          <EmptyState v-if="!detail" description="请在左侧选择分类节点查看详情" />
          <template v-else>
            <div class="detail-header">
              <h3 class="detail-title">{{ detail.name }}</h3>
              <StatusTag type="shop" :status="detail.status" />
            </div>
            <a-descriptions :column="2" bordered size="small">
              <a-descriptions-item label="名称">{{ detail.name }}</a-descriptions-item>
              <a-descriptions-item label="层级">第 {{ detail.level }} 级</a-descriptions-item>
              <a-descriptions-item label="父分类">{{ parentNameOf(detail) }}</a-descriptions-item>
              <a-descriptions-item label="排序值">{{ detail.sortOrder }}</a-descriptions-item>
              <a-descriptions-item label="图标">{{ detail.icon || '—' }}</a-descriptions-item>
              <a-descriptions-item label="关联商品数">
                <a @click="goProductAudit(detail.id)">{{ formatNumber(detail.productCount ?? 0) }}</a>
              </a-descriptions-item>
            </a-descriptions>

            <div class="detail-section-title">子分类（{{ detail.children?.length ?? 0 }}）</div>
            <a-list
              v-if="detail.children?.length"
              :data-source="detail.children"
              size="small"
              bordered
              class="children-list"
            >
              <template #renderItem="{ item }">
                <a-list-item class="child-item" @click="selectNode(item.id)">
                  <span class="child-name">{{ item.name }}</span>
                  <span class="child-meta">第 {{ item.level }} 级 · 商品 {{ formatNumber(item.productCount ?? 0) }}</span>
                  <StatusTag type="shop" :status="item.status" />
                </a-list-item>
              </template>
            </a-list>
            <div v-else class="no-children">暂无子分类</div>

            <a-space class="detail-actions">
              <a-tooltip
                v-if="detail.level >= MAX_CATEGORY_LEVEL"
                title="最多 3 级分类，第 3 级不能再创建子分类"
              >
                <a-button type="primary" disabled>新增子分类</a-button>
              </a-tooltip>
              <a-button v-else type="primary" @click="onOpenCreate(detail)">新增子分类</a-button>
              <a-button @click="startEdit">编辑</a-button>
              <a-button v-if="detail.status === 'Active'" danger @click="onDisable(detail)">停用</a-button>
              <a-button v-else @click="onEnable(detail)">启用</a-button>
            </a-space>
          </template>
        </template>

        <!-- 编辑模式（父级只读，名称同级唯一校验） -->
        <template v-else>
          <div class="detail-header">
            <h3 class="detail-title">编辑分类</h3>
          </div>
          <a-form
            ref="editFormRef"
            :model="editForm"
            :label-col="{ span: 5 }"
            :wrapper-col="{ span: 14 }"
          >
            <a-form-item label="父分类">
              <a-input :value="detail ? parentNameOf(detail) : ''" disabled />
            </a-form-item>
            <a-form-item label="名称" name="name" :rules="editNameRules">
              <a-input v-model:value="editForm.name" placeholder="请输入分类名称（1-30 字）" :maxlength="30" />
            </a-form-item>
            <a-form-item label="图标" name="icon">
              <a-input v-model:value="editForm.icon" placeholder="图标标识（选填）" :maxlength="50" />
            </a-form-item>
            <a-form-item label="排序值" name="sortOrder">
              <a-input-number v-model:value="editForm.sortOrder" :min="0" :max="9999" style="width: 120px" />
            </a-form-item>
            <a-form-item label="状态" name="status">
              <a-switch
                :checked="editForm.status === 'Active'"
                checked-children="启用"
                un-checked-children="停用"
                @change="(checked: boolean | string | number) => onEditStatusSwitch(checked)"
              />
            </a-form-item>
            <a-form-item :wrapper-col="{ offset: 5, span: 14 }">
              <a-space>
                <IdempotencyButton :loading="editSubmitting" @click="onSubmitEdit">保存</IdempotencyButton>
                <a-button @click="cancelEdit">取消</a-button>
              </a-space>
            </a-form-item>
          </a-form>
        </template>
      </a-spin>
    </a-card>

    <!-- 新增分类 Modal（顶级 / 子分类共用，父分类只读） -->
    <a-modal
      v-model:open="createModalOpen"
      :title="createParent ? `新增子分类（${createParent.name}）` : '新增顶级分类'"
      :confirm-loading="createSubmitting"
      width="520"
      ok-text="创建"
      cancel-text="取消"
      @ok="onSubmitCreate"
    >
      <a-form
        ref="createFormRef"
        :model="createForm"
        :label-col="{ span: 5 }"
        :wrapper-col="{ span: 16 }"
      >
        <a-form-item label="父分类">
          <a-input :value="createParentName" disabled />
        </a-form-item>
        <a-form-item label="层级">
          <a-input :value="`第 ${(createParent ? createParent.level + 1 : 1)} 级`" disabled />
        </a-form-item>
        <a-form-item label="名称" name="name" :rules="createNameRules">
          <a-input v-model:value="createForm.name" placeholder="请输入分类名称（1-30 字）" :maxlength="30" />
        </a-form-item>
        <a-form-item label="图标" name="icon">
          <a-input v-model:value="createForm.icon" placeholder="图标标识（选填）" :maxlength="50" />
        </a-form-item>
        <a-form-item label="排序值" name="sortOrder">
          <a-input-number v-model:value="createForm.sortOrder" :min="0" :max="9999" style="width: 120px" />
          <span class="sort-hint">数字越小越靠前</span>
        </a-form-item>
        <a-form-item label="状态" name="status">
          <a-switch
            :checked="createForm.status === 'Active'"
            checked-children="启用"
            un-checked-children="停用"
            @change="(checked: boolean | string | number) => onCreateStatusSwitch(checked)"
          />
        </a-form-item>
      </a-form>
    </a-modal>

    <!-- 停用二次确认 -->
    <ConfirmDialog
      :open="disableConfirmOpen"
      danger
      title="停用分类"
      :content="disableContent"
      @confirm="onConfirmDisable"
      @cancel="disableConfirmOpen = false"
    />
  </div>
</template>

<script setup lang="ts">
import { computed, onMounted, reactive, ref, watch } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import { message } from 'ant-design-vue'
import type { FormInstance } from 'ant-design-vue'
import type { Rule } from 'ant-design-vue/es/form'
import { ConfirmDialog, EmptyState, IdempotencyButton, StatusTag } from '@/shared/components'
import { formatNumber } from '@/shared/utils/format'
import { categoryApi } from '../api/category.api'
import type { CategoryDto, CategoryStatus } from '../types/category.dto'

/**
 * 分类管理页（02-product-ops）
 *
 * 左右分栏：左侧分类树（状态点 / 搜索高亮并展开父链 / 展开折叠全部），
 * 右侧详情面板（基本信息 / 子分类列表 / 操作）与编辑表单切换。
 * - 最多 3 级：第 3 级节点禁用「新增子分类」
 * - 同级名称唯一：前端校验 + 后端 409 透出
 * - 停用含启用子分类或被商品引用的分类：后端 409，message 透出
 */

const MAX_CATEGORY_LEVEL = 3

const route = useRoute()
const router = useRouter()

// ---------- 分类树数据 ----------
const treeData = ref<CategoryDto[]>([])
const treeLoading = ref(false)
const treeError = ref('')
const keyword = ref('')
const expandedKeys = ref<string[]>([])
const selectedKeys = ref<string[]>([])
const expandAll = ref(false)

/** id → 节点（基于原始树，供标题插槽与父链计算） */
const nodeMap = computed(() => {
  const map = new Map<string, CategoryDto>()
  const walk = (nodes: CategoryDto[]) => {
    for (const node of nodes) {
      map.set(node.id, node)
      if (node.children?.length) walk(node.children)
    }
  }
  walk(treeData.value)
  return map
})

const matchedIds = computed(() => {
  const kw = keyword.value.trim().toLowerCase()
  const ids = new Set<string>()
  if (!kw) return ids
  for (const node of nodeMap.value.values()) {
    if (node.name.toLowerCase().includes(kw)) ids.add(node.id)
  }
  return ids
})

/** 关键词过滤：保留匹配节点及其祖先链 */
const displayTree = computed<CategoryDto[]>(() => {
  if (!keyword.value.trim()) return treeData.value

  const filter = (nodes: CategoryDto[]): CategoryDto[] => {
    const result: CategoryDto[] = []
    for (const node of nodes) {
      const children = node.children?.length ? filter(node.children) : []
      if (matchedIds.value.has(node.id) || children.length > 0) {
        result.push({ ...node, children })
      }
    }
    return result
  }
  return filter(treeData.value)
})

/** 标题高亮分段 */
function titleSegments(node: CategoryDto | undefined): { text: string; hit: boolean }[] {
  if (!node) return []
  const kw = keyword.value.trim()
  if (!kw) return [{ text: node.name, hit: false }]

  const lowerName = node.name.toLowerCase()
  const lowerKw = kw.toLowerCase()
  const segments: { text: string; hit: boolean }[] = []
  let cursor = 0
  while (cursor < node.name.length) {
    const index = lowerName.indexOf(lowerKw, cursor)
    if (index === -1) {
      segments.push({ text: node.name.slice(cursor), hit: false })
      break
    }
    if (index > cursor) {
      segments.push({ text: node.name.slice(cursor, index), hit: false })
    }
    segments.push({ text: node.name.slice(index, index + kw.length), hit: true })
    cursor = index + kw.length
  }
  return segments
}

function nodeAriaLabel(id: string): string {
  const node = nodeMap.value.get(id)
  if (!node) return ''
  return `${node.name}（${node.status === 'Active' ? '启用' : '停用'}）`
}

// 搜索：高亮匹配节点并自动展开父链
watch(keyword, (value) => {
  const kw = value.trim()
  if (!kw) {
    expandAll.value = false
    expandedKeys.value = treeData.value.map((node) => node.id)
    return
  }

  const chain = new Set<string>()
  for (const id of matchedIds.value) {
    chain.add(id)
    let current = nodeMap.value.get(id)
    while (current?.parentId) {
      chain.add(current.parentId)
      current = nodeMap.value.get(current.parentId)
    }
  }
  expandedKeys.value = [...chain]
})

function onExpand(keys: (string | number)[]) {
  expandedKeys.value = keys.map(String)
  expandAll.value = expandedKeys.value.length >= nodeMap.value.size
}

function onToggleExpandAll() {
  expandAll.value = !expandAll.value
  expandedKeys.value = expandAll.value ? [...nodeMap.value.keys()] : []
}

// ---------- 树加载与节点选择 ----------
const detail = ref<CategoryDto | null>(null)
const detailLoading = ref(false)

async function loadTree(preselectId?: string) {
  treeLoading.value = true
  treeError.value = ''
  try {
    const { data } = await categoryApi.tree()
    treeData.value = data ?? []
    if (!keyword.value.trim()) {
      expandedKeys.value = treeData.value.map((node) => node.id)
    }

    // 默认选中第一个顶级分类（或跳转参数指定的分类）
    const targetId =
      preselectId && nodeMap.value.has(preselectId)
        ? preselectId
        : treeData.value[0]?.id ?? ''
    if (targetId) {
      await selectNode(targetId)
    } else {
      detail.value = null
      selectedKeys.value = []
    }
  } catch (e) {
    treeError.value = e instanceof Error ? e.message : '加载分类树失败'
    treeData.value = []
    detail.value = null
  } finally {
    treeLoading.value = false
  }
}

function reloadTree() {
  void loadTree()
}

function onTreeSelect(keys: (string | number)[], info: { selected: boolean }) {
  if (!info.selected) return
  const id = String(keys[0] ?? '')
  if (id) void selectNode(id)
}

async function selectNode(id: string) {
  selectedKeys.value = [id]
  panelMode.value = 'detail'
  detailLoading.value = true
  try {
    const { data } = await categoryApi.get(id)
    detail.value = data
    // 保证选中节点可见（展开其父级）
    const parent = nodeMap.value.get(id)?.parentId
    if (parent && !expandedKeys.value.includes(parent)) {
      expandedKeys.value = [...expandedKeys.value, parent]
    }
  } catch (e) {
    message.error(e instanceof Error && e.message ? e.message : '加载分类详情失败')
  } finally {
    detailLoading.value = false
  }
}

function parentNameOf(node: CategoryDto): string {
  if (!node.parentId) return '顶级分类'
  return nodeMap.value.get(node.parentId)?.name ?? '顶级分类'
}

function goProductAudit(categoryId: string) {
  void router.push({ path: '/product-ops/product-audit', query: { categoryId } })
}

/** 同级节点名称集合（排除自身），用于同级唯一校验 */
function siblingNames(parentId: string | null, excludeId?: string): string[] {
  const siblings = parentId ? (nodeMap.value.get(parentId)?.children ?? []) : treeData.value
  return siblings.filter((s) => s.id !== excludeId).map((s) => s.name.trim())
}

/** 局部更新树节点（名称 / 排序 / 图标 / 状态） */
function patchTreeNode(id: string, patch: Partial<CategoryDto>) {
  const node = nodeMap.value.get(id)
  if (node) Object.assign(node, patch)
}

// ---------- 右侧编辑表单 ----------
const panelMode = ref<'detail' | 'edit'>('detail')

interface CategoryEditFormState {
  name: string
  icon: string
  sortOrder: number
  status: CategoryStatus
}

const editFormRef = ref<FormInstance>()
const editSubmitting = ref(false)
const editForm = reactive<CategoryEditFormState>({
  name: '',
  icon: '',
  sortOrder: 0,
  status: 'Active',
})

const editNameRules: Rule[] = [
  { required: true, message: '请输入分类名称', trigger: 'blur' },
  { min: 1, max: 30, message: '分类名称长度为 1-30 字', trigger: 'blur' },
  {
    validator: (_rule: Rule, value: string) => {
      const current = detail.value
      const names = siblingNames(current?.parentId ?? null, current?.id)
      return names.includes(value.trim())
        ? Promise.reject(new Error('同级下已存在同名分类'))
        : Promise.resolve()
    },
    trigger: 'blur',
  },
]

function startEdit() {
  if (!detail.value) return
  editForm.name = detail.value.name
  editForm.icon = detail.value.icon ?? ''
  editForm.sortOrder = detail.value.sortOrder
  editForm.status = detail.value.status
  editFormRef.value?.clearValidate()
  panelMode.value = 'edit'
}

function cancelEdit() {
  panelMode.value = 'detail'
}

function onEditStatusSwitch(checked: boolean | string | number) {
  editForm.status = checked ? 'Active' : 'Inactive'
}

async function onSubmitEdit() {
  const current = detail.value
  if (!current) return

  try {
    await editFormRef.value?.validate()
  } catch {
    return
  }

  editSubmitting.value = true
  try {
    const body = {
      parentId: current.parentId,
      name: editForm.name.trim(),
      icon: editForm.icon.trim() || undefined,
      sortOrder: editForm.sortOrder,
      status: editForm.status,
    }
    const { data } = await categoryApi.update(current.id, body)
    // 局部更新树节点与详情（仅覆盖表单字段，保留 children 等面板数据）
    patchTreeNode(current.id, {
      name: data?.name ?? body.name,
      icon: body.icon,
      sortOrder: body.sortOrder,
      status: body.status,
    })
    detail.value = {
      ...detail.value,
      name: data?.name ?? body.name,
      icon: body.icon,
      sortOrder: body.sortOrder,
      status: body.status,
      productCount: data?.productCount ?? detail.value.productCount,
    }
    message.success('分类已更新')
    panelMode.value = 'detail'
  } catch (e) {
    // 同级重名 / 乐观锁冲突等：透出后端 message
    message.error(e instanceof Error && e.message ? e.message : '更新分类失败，请重试')
  } finally {
    editSubmitting.value = false
  }
}

// ---------- 新增分类 Modal ----------
const createModalOpen = ref(false)
const createSubmitting = ref(false)
const createParent = ref<CategoryDto | null>(null)
const createFormRef = ref<FormInstance>()
const createForm = reactive<CategoryEditFormState>({
  name: '',
  icon: '',
  sortOrder: 0,
  status: 'Active',
})

const createParentName = computed(() => createParent.value?.name ?? '顶级分类')

const createNameRules = computed<Rule[]>(() => [
  { required: true, message: '请输入分类名称', trigger: 'blur' },
  { min: 1, max: 30, message: '分类名称长度为 1-30 字', trigger: 'blur' },
  {
    validator: (_rule: Rule, value: string) => {
      const names = siblingNames(createParent.value?.id ?? null)
      return names.includes(value.trim())
        ? Promise.reject(new Error('同级下已存在同名分类'))
        : Promise.resolve()
    },
    trigger: 'blur',
  },
])

function onOpenCreate(parent: CategoryDto | null) {
  createParent.value = parent
  createForm.name = ''
  createForm.icon = ''
  createForm.sortOrder = 0
  createForm.status = 'Active'
  createFormRef.value?.clearValidate()
  createModalOpen.value = true
}

function onCreateStatusSwitch(checked: boolean | string | number) {
  createForm.status = checked ? 'Active' : 'Inactive'
}

async function onSubmitCreate() {
  try {
    await createFormRef.value?.validate()
  } catch {
    return
  }

  createSubmitting.value = true
  try {
    const parent = createParent.value
    const body = {
      parentId: parent?.id ?? null,
      name: createForm.name.trim(),
      icon: createForm.icon.trim() || undefined,
      sortOrder: createForm.sortOrder,
      status: createForm.status,
    }
    const { data } = await categoryApi.create(body)
    createModalOpen.value = false

    keyword.value = ''
    await loadTree(data?.id)
    message.success(`分类「${body.name}」已创建`)
  } catch (e) {
    // 同级重名等：透出后端 message
    message.error(e instanceof Error && e.message ? e.message : '创建分类失败，请重试')
  } finally {
    createSubmitting.value = false
  }
}

// ---------- 启用 / 停用 ----------
const disableConfirmOpen = ref(false)
const pendingDisable = ref<CategoryDto | null>(null)

const disableContent = computed(() => {
  const node = pendingDisable.value
  if (!node) return ''
  const hasChildren = Boolean(node.children?.length)
  if (hasChildren) {
    return `停用后「${node.name}」及其子分类将对买家端隐藏。若存在启用中的子分类或被商品引用，后端将拒绝停用。`
  }
  return `停用后「${node.name}」将对买家端隐藏。若该分类被商品引用，后端将拒绝停用。`
})

function onDisable(node: CategoryDto) {
  pendingDisable.value = node
  disableConfirmOpen.value = true
}

async function onConfirmDisable() {
  disableConfirmOpen.value = false
  const target = pendingDisable.value
  if (!target) return

  try {
    await categoryApi.disable(target.id)
    // 局部更新树节点与详情状态
    patchTreeNode(target.id, { status: 'Inactive' })
    if (detail.value?.id === target.id) detail.value = { ...detail.value, status: 'Inactive' }
    message.success(`分类「${target.name}」已停用`)
  } catch (e) {
    // 409 冲突：透出后端 message（如「请先停用或删除子分类」/「该分类被 N 个商品引用，无法停用」）
    message.error(e instanceof Error && e.message ? e.message : '停用失败，请重试')
  } finally {
    pendingDisable.value = null
  }
}

async function onEnable(node: CategoryDto) {
  try {
    await categoryApi.enable(node.id)
    patchTreeNode(node.id, { status: 'Active' })
    if (detail.value?.id === node.id) detail.value = { ...detail.value, status: 'Active' }
    message.success(`分类「${node.name}」已启用`)
  } catch (e) {
    message.error(e instanceof Error && e.message ? e.message : '启用失败，请重试')
  }
}

// ---------- 初始化 ----------
onMounted(() => {
  // 支持商品审核页分类列跳转携带分类预选
  const queryCategoryId = typeof route.query.categoryId === 'string' ? route.query.categoryId : ''
  void loadTree(queryCategoryId)
})
</script>

<style scoped>
.category-management {
  display: flex;
  gap: 16px;
  align-items: stretch;
}

.tree-card {
  width: 320px;
  flex-shrink: 0;
}

.tree-toolbar {
  display: flex;
  gap: 8px;
  margin-bottom: 8px;
}

.tree-search {
  margin-bottom: 8px;
}

.tree-state {
  padding: 24px 0;
  text-align: center;
}

.tree-node {
  display: inline-flex;
  align-items: center;
  gap: 6px;
}

.tree-node-name {
  font-size: 14px;
  color: #000000d9;
}

.name-hit {
  color: #1677ff;
  font-weight: 600;
}

.status-dot {
  width: 6px;
  height: 6px;
  border-radius: 50%;
}

.dot-active {
  background: #52c41a;
}

.dot-inactive {
  background: #8c8c8c;
}

.detail-card {
  flex: 1;
  min-width: 0;
}

.detail-header {
  display: flex;
  align-items: center;
  gap: 12px;
  margin-bottom: 16px;
}

.detail-title {
  margin: 0;
  font-size: 16px;
  font-weight: 600;
  color: #000000d9;
}

.detail-section-title {
  margin: 20px 0 12px;
  font-size: 14px;
  font-weight: 600;
  color: #000000d9;
}

.children-list {
  max-height: 320px;
  overflow-y: auto;
}

.child-item {
  display: flex;
  align-items: center;
  justify-content: space-between;
  gap: 12px;
  cursor: pointer;
}

.child-item:hover {
  background: #f5f5f5;
}

.child-name {
  font-size: 14px;
  color: #000000d9;
}

.child-meta {
  font-size: 12px;
  color: #8c8c8c;
}

.no-children {
  padding: 16px;
  font-size: 12px;
  color: #8c8c8c;
  background: #fafafa;
  border: 1px dashed #d9d9d9;
  border-radius: 6px;
  text-align: center;
}

.detail-actions {
  margin-top: 24px;
}

.sort-hint {
  margin-left: 8px;
  font-size: 12px;
  color: #8c8c8c;
}

@media (max-width: 992px) {
  .category-management {
    flex-direction: column;
  }

  .tree-card {
    width: 100%;
  }
}
</style>
