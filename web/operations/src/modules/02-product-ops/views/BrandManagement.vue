<!-- web/operations/src/modules/02-product-ops/views/BrandManagement.vue -->
<template>
  <div class="brand-management">
    <!-- 区域 A：筛选条 -->
    <a-card :bordered="false" class="filter-card">
      <a-form layout="inline" class="filter-form">
        <a-form-item label="品牌名称">
          <a-input-search
            v-model:value="filters.keyword"
            placeholder="输入品牌名称关键词"
            allow-clear
            style="width: 220px"
            @search="onQuery"
          />
        </a-form-item>
        <a-form-item label="状态">
          <a-select
            v-model:value="filters.status"
            placeholder="全部状态"
            allow-clear
            style="width: 140px"
            :options="statusOptions"
          />
        </a-form-item>
        <a-form-item>
          <a-button type="primary" @click="onQuery">查询</a-button>
          <a-button style="margin-left: 8px" @click="onReset">重置</a-button>
        </a-form-item>
      </a-form>
    </a-card>

    <!-- 区域 B + C：工具栏与品牌表格 -->
    <a-card :bordered="false" class="table-card">
      <div class="table-toolbar">
        <a-button type="primary" @click="onAdd">新增品牌</a-button>
        <a-space>
          <a-button @click="onExportCsv">导出 CSV</a-button>
          <a-button :loading="loading" @click="fetchBrands">刷新</a-button>
        </a-space>
      </div>

      <div v-if="errorMessage" class="table-error">
        <EmptyState :description="`加载失败：${errorMessage}`" action-text="重试" @action="fetchBrands" />
      </div>
      <a-table
        v-else
        :columns="columns"
        :data-source="tableData"
        :loading="loading"
        :pagination="pagination"
        row-key="id"
        @change="onTableChange"
      >
        <template #emptyText>
          <EmptyState description="暂无品牌" action-text="新增品牌" @action="onAdd" />
        </template>
        <template #bodyCell="{ column, record }">
          <template v-if="column.key === 'logo'">
            <a-image
              v-if="record.logoUrl"
              :src="record.logoUrl"
              :alt="record.name"
              :width="40"
              :height="40"
              style="border-radius: 4px; object-fit: cover"
            />
            <div v-else class="logo-fallback" :aria-label="record.name">
              {{ initials(record.name) }}
            </div>
          </template>
          <template v-else-if="column.key === 'englishName'">{{ record.englishName || '—' }}</template>
          <template v-else-if="column.key === 'sortOrder'">{{ record.sortOrder }}</template>
          <template v-else-if="column.key === 'createdAt'">{{ formatDateTime(record.createdAt) }}</template>
          <template v-else-if="column.key === 'status'">
            <StatusTag type="shop" :status="record.status" />
          </template>
          <template v-else-if="column.key === 'action'">
            <a-space>
              <a-button type="link" size="small" aria-label="编辑品牌" @click="onEdit(record)">编辑</a-button>
              <a-button
                v-if="record.status !== 'Active'"
                type="link"
                size="small"
                aria-label="启用品牌"
                @click="onEnable(record)"
              >
                启用
              </a-button>
              <a-button
                v-else
                type="link"
                size="small"
                danger
                aria-label="停用品牌"
                @click="onDisable(record)"
              >
                停用
              </a-button>
            </a-space>
          </template>
        </template>
      </a-table>
    </a-card>

    <!-- 区域 D：新增 / 编辑品牌模态框 -->
    <a-modal
      v-model:open="modalOpen"
      :title="editingBrand ? '编辑品牌' : '新增品牌'"
      :confirm-loading="submitting"
      width="520"
      ok-text="保存"
      cancel-text="取消"
      @ok="onSubmitBrand"
    >
      <a-spin :spinning="detailLoading">
        <a-form ref="formRef" :model="formState" :label-col="{ span: 5 }" :wrapper-col="{ span: 17 }">
          <a-form-item label="品牌名称" name="name" :rules="rules.name">
            <a-input v-model:value="formState.name" placeholder="请输入品牌名称（1-50 字）" :maxlength="50" />
          </a-form-item>
          <a-form-item label="英文名" name="englishName" :rules="rules.englishName">
            <a-input v-model:value="formState.englishName" placeholder="请输入品牌英文名称（选填）" :maxlength="50" />
          </a-form-item>
          <a-form-item label="Logo" name="logoUrl">
            <div class="logo-uploader">
              <a-upload
                :file-list="logoFileList"
                list-type="picture-card"
                :show-upload-list="false"
                accept=".jpg,.jpeg,.png,.webp"
                :before-upload="beforeLogoUpload"
                @change="onLogoChange"
              >
                <img v-if="formState.logoUrl" :src="formState.logoUrl" :alt="`${formState.name || '品牌'} Logo`" class="logo-preview" />
                <div v-else class="upload-placeholder">
                  <PlusOutlined />
                  <div class="upload-text">点击上传</div>
                </div>
              </a-upload>
              <div class="logo-tips">
                <div>JPG / PNG / WebP，不超过 2MB，上传后转 base64 预览</div>
                <a-button
                  v-if="formState.logoUrl"
                  type="link"
                  size="small"
                  danger
                  @click="onRemoveLogo"
                >
                  移除 Logo
                </a-button>
              </div>
            </div>
          </a-form-item>
          <a-form-item label="品牌简介" name="description" :rules="rules.description">
            <a-textarea
              v-model:value="formState.description"
              :rows="3"
              :maxlength="200"
              show-count
              placeholder="请输入品牌简介（选填，最多 200 字）"
            />
          </a-form-item>
          <a-form-item label="排序值" name="sortOrder">
            <a-input-number v-model:value="formState.sortOrder" :min="0" :max="9999" style="width: 120px" />
            <span class="sort-hint">数字越小越靠前</span>
          </a-form-item>
          <a-form-item label="状态" name="status">
            <a-switch
              :checked="formState.status === 'Active'"
              checked-children="启用"
              un-checked-children="停用"
              @change="onStatusSwitch"
            />
          </a-form-item>
        </a-form>
      </a-spin>
    </a-modal>

    <!-- 停用二次确认 -->
    <ConfirmDialog
      :open="disableConfirmOpen"
      danger
      title="停用品牌"
      :content="`停用后卖家发布商品时将不可选择品牌「${pendingDisable?.name ?? ''}」。若该品牌被商品引用，后端将拒绝停用。`"
      @confirm="onConfirmDisable"
      @cancel="disableConfirmOpen = false"
    />
  </div>
</template>

<script setup lang="ts">
import { onMounted, reactive, ref } from 'vue'
import { message } from 'ant-design-vue'
import type { UploadFile } from 'ant-design-vue'
import type { FormInstance } from 'ant-design-vue'
import type { TableColumnsType } from 'ant-design-vue'
import { PlusOutlined } from '@ant-design/icons-vue'
import { ConfirmDialog, EmptyState, StatusTag } from '@/shared/components'
import { formatDateTime } from '@/shared/utils/format'
import { brandApi } from '../api/brand.api'
import type { BrandDto, BrandStatus } from '../types/brand.dto'

/**
 * 品牌管理页（02-product-ops）
 *
 * 四区布局：筛选条（名称/状态）/ 工具栏（新增/导出/刷新）/ 品牌表格 / 新增编辑 Modal。
 * - 默认加载启用状态品牌前 20 条
 * - 启停操作成功后局部更新状态列，不重新拉全量
 * - 停用被商品引用的品牌时后端 409，message 透出展示
 */

interface FilterState {
  keyword: string
  status?: BrandStatus
}

const filters = reactive<FilterState>({
  keyword: '',
  status: 'Active',
})

const statusOptions = [
  { label: '启用', value: 'Active' },
  { label: '停用', value: 'Inactive' },
]

const columns: TableColumnsType = [
  { title: 'Logo', key: 'logo', width: 80 },
  { title: '品牌名称', dataIndex: 'name', key: 'name', width: 160 },
  { title: '英文名', key: 'englishName', width: 140 },
  { title: '排序值', key: 'sortOrder', width: 90, align: 'center' },
  { title: '创建时间', key: 'createdAt', width: 170 },
  { title: '状态', key: 'status', width: 100 },
  { title: '操作', key: 'action', width: 150, fixed: 'right' },
]

const tableData = ref<BrandDto[]>([])
const loading = ref(false)
const errorMessage = ref('')

const pagination = reactive({
  current: 1,
  pageSize: 20,
  total: 0,
  showSizeChanger: true,
  showTotal: (total: number) => `共 ${total} 条`,
})

async function fetchBrands() {
  loading.value = true
  errorMessage.value = ''
  try {
    const params: Parameters<typeof brandApi.list>[0] = {
      page: pagination.current,
      pageSize: pagination.pageSize,
    }
    const keyword = filters.keyword.trim()
    if (keyword) params.keyword = keyword
    if (filters.status) params.status = filters.status

    const { data } = await brandApi.list(params)
    tableData.value = data.items
    pagination.total = data.total
  } catch (e) {
    errorMessage.value = e instanceof Error ? e.message : '加载品牌列表失败'
    tableData.value = []
    pagination.total = 0
  } finally {
    loading.value = false
  }
}

function onQuery() {
  pagination.current = 1
  void fetchBrands()
}

function onReset() {
  filters.keyword = ''
  filters.status = 'Active'
  onQuery()
}

function onTableChange(pag: { current?: number; pageSize?: number }) {
  if (pag.current !== undefined) pagination.current = pag.current
  if (pag.pageSize !== undefined) pagination.pageSize = pag.pageSize
  void fetchBrands()
}

function initials(name: string): string {
  return (name || '?').slice(0, 1).toUpperCase()
}

// ---------- 新增 / 编辑 ----------
interface BrandFormState {
  name: string
  englishName: string
  logoUrl: string
  description: string
  sortOrder: number
  status: BrandStatus
}

const modalOpen = ref(false)
const submitting = ref(false)
const detailLoading = ref(false)
const editingBrand = ref<BrandDto | null>(null)
const formRef = ref<FormInstance>()

const formState = reactive<BrandFormState>({
  name: '',
  englishName: '',
  logoUrl: '',
  description: '',
  sortOrder: 0,
  status: 'Active',
})

const rules = {
  name: [
    { required: true, message: '请输入品牌名称', trigger: 'blur' },
    { min: 1, max: 50, message: '品牌名称长度为 1-50 字', trigger: 'blur' },
  ],
  englishName: [{ max: 50, message: '英文名不能超过 50 字', trigger: 'blur' }],
  description: [{ max: 200, message: '品牌简介不能超过 200 字', trigger: 'blur' }],
}

const LOGO_MAX_SIZE = 2 * 1024 * 1024
const LOGO_TYPES = ['image/jpeg', 'image/png', 'image/webp']

const logoFileList = ref<UploadFile[]>([])

function resetForm() {
  formState.name = ''
  formState.englishName = ''
  formState.logoUrl = ''
  formState.description = ''
  formState.sortOrder = 0
  formState.status = 'Active'
  logoFileList.value = []
  formRef.value?.clearValidate()
}

function onAdd() {
  editingBrand.value = null
  resetForm()
  modalOpen.value = true
}

async function onEdit(record: BrandDto) {
  editingBrand.value = record
  resetForm()
  modalOpen.value = true
  detailLoading.value = true
  try {
    const { data } = await brandApi.get(record.id)
    formState.name = data.name
    formState.englishName = data.englishName ?? ''
    formState.logoUrl = data.logoUrl ?? ''
    formState.description = data.description ?? ''
    formState.sortOrder = data.sortOrder
    formState.status = data.status
    logoFileList.value = data.logoUrl
      ? [{ uid: '-logo', name: 'logo', status: 'done', url: data.logoUrl }]
      : []
  } catch (e) {
    message.error(e instanceof Error && e.message ? e.message : '加载品牌详情失败')
    modalOpen.value = false
  } finally {
    detailLoading.value = false
  }
}

/** Logo 上传：校验类型与大小，转 base64 回填 LogoUrl（不上传服务端） */
function beforeLogoUpload(file: File): boolean {
  if (!LOGO_TYPES.includes(file.type)) {
    message.error('Logo 仅支持 JPG/PNG/WebP 格式')
    return false
  }
  if (file.size > LOGO_MAX_SIZE) {
    message.error('Logo 大小不能超过 2MB')
    return false
  }

  const reader = new window.FileReader()
  reader.addEventListener('load', () => {
    formState.logoUrl = String(reader.result ?? '')
  })
  reader.readAsDataURL(file)
  return false
}

function onLogoChange({ fileList }: { fileList: UploadFile[] }) {
  logoFileList.value = fileList.slice(-1)
}

function onRemoveLogo() {
  formState.logoUrl = ''
  logoFileList.value = []
}

function onStatusSwitch(checked: boolean | string | number) {
  formState.status = checked ? 'Active' : 'Inactive'
}

async function onSubmitBrand() {
  try {
    await formRef.value?.validate()
  } catch {
    return
  }

  submitting.value = true
  const body = {
    name: formState.name.trim(),
    englishName: formState.englishName.trim() || undefined,
    logoUrl: formState.logoUrl || undefined,
    description: formState.description.trim() || undefined,
    sortOrder: formState.sortOrder,
    status: formState.status,
  }

  try {
    if (editingBrand.value) {
      await brandApi.update(editingBrand.value.id, body)
      message.success('品牌已更新')
    } else {
      await brandApi.create(body)
      message.success('品牌已创建')
    }
    modalOpen.value = false
    await fetchBrands()
  } catch (e) {
    // 名称重复 / 乐观锁冲突等：透出后端 message（如「品牌名称已存在」）
    message.error(e instanceof Error && e.message ? e.message : '保存品牌失败，请重试')
  } finally {
    submitting.value = false
  }
}

// ---------- 启用 / 停用 ----------
const disableConfirmOpen = ref(false)
const pendingDisable = ref<BrandDto | null>(null)

function onDisable(record: BrandDto) {
  pendingDisable.value = record
  disableConfirmOpen.value = true
}

async function onConfirmDisable() {
  disableConfirmOpen.value = false
  const target = pendingDisable.value
  if (!target) return

  try {
    await brandApi.disable(target.id)
    // 局部更新状态列，不重新拉全量
    target.status = 'Inactive'
    message.success(`品牌「${target.name}」已停用`)
  } catch (e) {
    // 409 冲突等：透出后端 message（如「该品牌被 N 个商品引用，无法停用」）
    message.error(e instanceof Error && e.message ? e.message : '停用失败，请重试')
  } finally {
    pendingDisable.value = null
  }
}

async function onEnable(record: BrandDto) {
  try {
    await brandApi.enable(record.id)
    record.status = 'Active'
    message.success(`品牌「${record.name}」已启用`)
  } catch (e) {
    message.error(e instanceof Error && e.message ? e.message : '启用失败，请重试')
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

  const header = ['品牌ID', '品牌名称', '英文名', '排序值', '创建人', '创建时间', '状态']
  const rows = tableData.value.map((b) => [
    b.id,
    b.name,
    b.englishName ?? '',
    String(b.sortOrder),
    b.createdBy ?? '',
    formatDateTime(b.createdAt),
    b.status === 'Active' ? '启用' : '停用',
  ])

  const csv = [header, ...rows].map((row) => row.map(csvEscape).join(',')).join('\n')
  const blob = new Blob([`\uFEFF${csv}`], { type: 'text/csv;charset=utf-8;' })
  const url = URL.createObjectURL(blob)
  const link = document.createElement('a')
  link.href = url
  link.download = `品牌管理导出_${Date.now()}.csv`
  document.body.appendChild(link)
  link.click()
  document.body.removeChild(link)
  URL.revokeObjectURL(url)
  message.success(`已导出当前页 ${rows.length} 条数据`)
}

onMounted(() => {
  void fetchBrands()
})
</script>

<style scoped>
.brand-management {
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

.logo-fallback {
  display: flex;
  align-items: center;
  justify-content: center;
  width: 40px;
  height: 40px;
  background: #f0f0f0;
  border-radius: 4px;
  color: #8c8c8c;
  font-size: 16px;
  font-weight: 500;
}

.logo-uploader {
  display: flex;
  align-items: flex-start;
  gap: 12px;
}

.logo-preview {
  width: 96px;
  height: 96px;
  object-fit: cover;
  border-radius: 4px;
}

.upload-placeholder {
  display: flex;
  flex-direction: column;
  align-items: center;
  justify-content: center;
  color: #8c8c8c;
  font-size: 22px;
}

.upload-text {
  margin-top: 4px;
  font-size: 12px;
}

.logo-tips {
  font-size: 12px;
  color: #8c8c8c;
  line-height: 20px;
}

.sort-hint {
  margin-left: 8px;
  font-size: 12px;
  color: #8c8c8c;
}
</style>
