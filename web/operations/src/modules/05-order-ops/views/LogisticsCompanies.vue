<!-- web/operations/src/modules/05-order-ops/views/LogisticsCompanies.vue -->
<template>
  <div class="logistics-companies">
    <!-- 区域 A：筛选条 -->
    <a-card :bordered="false" class="filter-card">
      <a-form layout="inline" class="filter-form">
        <a-form-item label="公司名称">
          <a-input-search
            v-model:value="filters.keyword"
            placeholder="名称或代码关键词，如 顺丰 / SF"
            allow-clear
            style="width: 240px"
            @search="onQuery"
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

    <!-- 区域 B + C：工具栏与物流公司表格 -->
    <a-card :bordered="false" class="table-card">
      <div class="table-toolbar">
        <a-button type="primary" @click="onAdd">
          <PlusOutlined /> 新增物流公司
        </a-button>
        <a-space>
          <a-button @click="onExportCsv">导出 CSV</a-button>
          <a-button :loading="loading" @click="fetchCompanies">刷新</a-button>
        </a-space>
      </div>

      <div v-if="errorMessage" class="table-error">
        <EmptyState :description="`加载失败：${errorMessage}`" action-text="重试" @action="onQuery" />
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
          <EmptyState description="暂无物流公司" action-text="新增物流公司" @action="onAdd" />
        </template>
        <template #bodyCell="{ column, record }">
          <template v-if="column.key === 'logo'">
            <a-image
              v-if="record.logoUrl"
              :src="record.logoUrl"
              :alt="record.name"
              :width="32"
              :height="32"
              style="border-radius: 4px; object-fit: cover"
            />
            <div v-else class="logo-fallback" :aria-label="record.name">
              {{ initials(record.name) }}
            </div>
          </template>
          <template v-else-if="column.key === 'name'">
            <span class="company-name">{{ record.name }}</span>
          </template>
          <template v-else-if="column.key === 'code'">
            <span class="mono code-text">{{ record.code }}</span>
          </template>
          <template v-else-if="column.key === 'phone'">
            <span :class="record.phone ? 'phone-text' : 'cell-sub'">{{ record.phone || '—' }}</span>
          </template>
          <template v-else-if="column.key === 'website'">
            <a
              v-if="record.website"
              :href="record.website"
              target="_blank"
              rel="noopener noreferrer"
              class="website-link"
            >
              <LinkOutlined /> {{ record.website }}
            </a>
            <span v-else class="cell-sub">—</span>
          </template>
          <template v-else-if="column.key === 'sortOrder'">{{ record.sortOrder }}</template>
          <template v-else-if="column.key === 'createdAt'">{{ formatDateTime(record.createdAt) }}</template>
          <template v-else-if="column.key === 'status'">
            <StatusTag type="shop" :status="record.status" />
          </template>
          <template v-else-if="column.key === 'action'">
            <a-space>
              <a-button type="link" size="small" aria-label="编辑物流公司" @click="onEdit(record)">编辑</a-button>
              <a-button
                v-if="record.status !== 'Active'"
                type="link"
                size="small"
                aria-label="启用物流公司"
                @click="onEnable(record)"
              >
                启用
              </a-button>
              <a-button
                v-else
                type="link"
                size="small"
                danger
                aria-label="停用物流公司"
                @click="onDisable(record)"
              >
                停用
              </a-button>
            </a-space>
          </template>
        </template>
      </a-table>
    </a-card>

    <!-- 区域 D：新增 / 编辑模态框 -->
    <a-modal
      v-model:open="modalOpen"
      :title="editingCompany ? '编辑物流公司' : '新增物流公司'"
      :confirm-loading="submitting"
      width="520"
      ok-text="保存"
      cancel-text="取消"
      @ok="onSubmitCompany"
    >
      <a-form ref="formRef" :model="formState" :label-col="{ span: 5 }" :wrapper-col="{ span: 17 }">
        <a-form-item label="公司名称" name="name" :rules="rules.name">
          <a-input v-model:value="formState.name" placeholder="请输入公司名称（1-50 字）" :maxlength="50" />
        </a-form-item>
        <a-form-item label="公司代码" name="code" :rules="rules.code" extra="全局唯一，如 SF / ZTO；重复时后端返回「公司代码已存在」">
          <a-input
            v-model:value="formState.code"
            placeholder="请输入公司代码"
            :maxlength="20"
            :disabled="Boolean(editingCompany)"
          />
        </a-form-item>
        <a-form-item label="Logo" name="logoUrl">
          <div class="logo-uploader">
            <a-upload
              :file-list="logoFileList"
              list-type="picture-card"
              :show-upload-list="false"
              accept=".png,.svg"
              :before-upload="beforeLogoUpload"
              @change="onLogoChange"
            >
              <img
                v-if="formState.logoUrl"
                :src="formState.logoUrl"
                :alt="`${formState.name || '物流公司'} Logo`"
                class="logo-preview"
              />
              <div v-else class="upload-placeholder">
                <PlusOutlined />
                <div class="upload-text">点击上传</div>
              </div>
            </a-upload>
            <div class="logo-tips">
              <div>PNG / SVG，不超过 1MB，上传后转 base64 预览</div>
              <a-button v-if="formState.logoUrl" type="link" size="small" danger @click="onRemoveLogo">
                移除 Logo
              </a-button>
            </div>
          </div>
        </a-form-item>
        <a-form-item label="官方电话" name="phone" :rules="rules.phone">
          <a-input v-model:value="formState.phone" placeholder="如 95338 / 010-12345678（选填）" :maxlength="20" />
        </a-form-item>
        <a-form-item label="官网链接" name="website" :rules="rules.website">
          <a-input v-model:value="formState.website" placeholder="如 https://www.sf-express.com（选填）" />
        </a-form-item>
        <a-form-item label="排序值" name="sortOrder">
          <a-input-number v-model:value="formState.sortOrder" :min="0" :max="9999" style="width: 120px" />
          <span class="sort-hint">数字越小越靠前，列表按排序值升序</span>
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
    </a-modal>

    <!-- 停用二次确认 -->
    <ConfirmDialog
      :open="disableConfirmOpen"
      danger
      title="停用物流公司"
      :content="`停用后新订单将不可选择「${pendingDisable?.name ?? ''}」，历史订单不受影响。`"
      @confirm="onConfirmDisable"
      @cancel="disableConfirmOpen = false"
    />
  </div>
</template>

<script setup lang="ts">
import { onMounted, reactive, ref } from 'vue'
import { message } from 'ant-design-vue'
import type { FormInstance, TableColumnsType, UploadFile } from 'ant-design-vue'
import { LinkOutlined, PlusOutlined } from '@ant-design/icons-vue'
import { ConfirmDialog, EmptyState, StatusTag } from '@/shared/components'
import { formatDateTime } from '@/shared/utils/format'
import { logisticsApi } from '../api/logistics.api'
import type { LogisticsCompanyDto, LogisticsCompanyStatus } from '../types/logistics.dto'

/**
 * 物流公司管理页（05-order-ops）
 *
 * 四区布局：筛选条（名称 / 状态）/ 工具栏（新增 / 导出 / 刷新）/ 公司表格 / 新增编辑 Modal。
 * - 列表按排序值升序展示（后端排序 + 前端兜底）
 * - 公司代码唯一由后端校验（409 透出「公司代码已存在」）
 * - Logo 上传限制单张 1MB PNG/SVG，转 base64 预览
 * - 启停操作成功后局部更新状态列
 */

interface FilterState {
  keyword: string
  status?: LogisticsCompanyStatus
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
  { title: '公司名称', key: 'name', width: 150 },
  { title: '代码', key: 'code', width: 90 },
  { title: '官方电话', key: 'phone', width: 130 },
  { title: '官网链接', key: 'website', width: 220, ellipsis: true },
  { title: '排序值', key: 'sortOrder', width: 90, align: 'center' },
  { title: '创建时间', key: 'createdAt', width: 170 },
  { title: '状态', key: 'status', width: 90 },
  { title: '操作', key: 'action', width: 130, fixed: 'right' },
]

const tableData = ref<LogisticsCompanyDto[]>([])
const loading = ref(false)
const errorMessage = ref('')

const pagination = reactive({
  current: 1,
  pageSize: 20,
  total: 0,
  showSizeChanger: true,
  showTotal: (total: number) => `共 ${total} 条`,
})

async function fetchCompanies() {
  loading.value = true
  errorMessage.value = ''
  try {
    const params: Parameters<typeof logisticsApi.list>[0] = {
      page: pagination.current,
      pageSize: pagination.pageSize,
    }
    const keyword = filters.keyword.trim()
    if (keyword) params.keyword = keyword
    if (filters.status) params.status = filters.status

    const { data } = await logisticsApi.list(params)
    // 列表按排序值升序（后端排序 + 前端兜底）
    tableData.value = [...data.items].sort((a, b) => a.sortOrder - b.sortOrder)
    pagination.total = data.total
  } catch (e) {
    errorMessage.value = e instanceof Error ? e.message : '加载物流公司列表失败'
    tableData.value = []
    pagination.total = 0
  } finally {
    loading.value = false
  }
}

function onQuery() {
  pagination.current = 1
  void fetchCompanies()
}

function onReset() {
  filters.keyword = ''
  filters.status = 'Active'
  onQuery()
}

function onTableChange(pag: { current?: number; pageSize?: number }) {
  if (pag.current !== undefined) pagination.current = pag.current
  if (pag.pageSize !== undefined) pagination.pageSize = pag.pageSize
  void fetchCompanies()
}

function initials(name: string): string {
  return (name || '?').slice(0, 1).toUpperCase()
}

// ---------- 新增 / 编辑 ----------
interface CompanyFormState {
  name: string
  code: string
  logoUrl: string
  phone: string
  website: string
  sortOrder: number
  status: LogisticsCompanyStatus
}

const modalOpen = ref(false)
const submitting = ref(false)
const editingCompany = ref<LogisticsCompanyDto | null>(null)
const formRef = ref<FormInstance>()

const formState = reactive<CompanyFormState>({
  name: '',
  code: '',
  logoUrl: '',
  phone: '',
  website: '',
  sortOrder: 0,
  status: 'Active',
})

/** 电话格式：数字与 + - ( ) 空格组合，5-20 位 */
const PHONE_PATTERN = /^[0-9+\-()\s]{5,20}$/
/** 官网格式：http(s):// 开头 */
const WEBSITE_PATTERN = /^https?:\/\/[^\s]+$/i

const rules = {
  name: [
    { required: true, message: '请输入公司名称', trigger: 'blur' },
    { min: 1, max: 50, message: '公司名称长度为 1-50 字', trigger: 'blur' },
  ],
  code: [
    { required: true, message: '请输入公司代码', trigger: 'blur' },
    { pattern: /^[A-Za-z0-9_-]{1,20}$/, message: '代码仅支持字母 / 数字 / - / _，长度 1-20', trigger: 'blur' },
  ],
  phone: [{ pattern: PHONE_PATTERN, message: '电话格式不正确（数字与 + - ( ) 组合）', trigger: 'blur' }],
  website: [{ pattern: WEBSITE_PATTERN, message: '官网链接须以 http:// 或 https:// 开头', trigger: 'blur' }],
}

const LOGO_MAX_SIZE = 1 * 1024 * 1024
const LOGO_TYPES = ['image/png', 'image/svg+xml']

const logoFileList = ref<UploadFile[]>([])

function resetForm() {
  formState.name = ''
  formState.code = ''
  formState.logoUrl = ''
  formState.phone = ''
  formState.website = ''
  formState.sortOrder = 0
  formState.status = 'Active'
  logoFileList.value = []
  formRef.value?.clearValidate()
}

function onAdd() {
  editingCompany.value = null
  resetForm()
  modalOpen.value = true
}

function onEdit(record: LogisticsCompanyDto) {
  editingCompany.value = record
  resetForm()
  formState.name = record.name
  formState.code = record.code
  formState.logoUrl = record.logoUrl ?? ''
  formState.phone = record.phone ?? ''
  formState.website = record.website ?? ''
  formState.sortOrder = record.sortOrder
  formState.status = record.status
  logoFileList.value = record.logoUrl
    ? [{ uid: '-logo', name: 'logo', status: 'done', url: record.logoUrl }]
    : []
  modalOpen.value = true
}

/** Logo 上传：校验类型与大小（PNG/SVG ≤1MB），转 base64 回填 LogoUrl（不上传服务端） */
function beforeLogoUpload(file: File): boolean {
  if (!LOGO_TYPES.includes(file.type)) {
    message.error('Logo 仅支持 PNG / SVG 格式')
    return false
  }
  if (file.size > LOGO_MAX_SIZE) {
    message.error('Logo 大小不能超过 1MB')
    return false
  }

  const reader = new window.FileReader()
  reader.addEventListener('load', () => {
    formState.logoUrl = String(reader.result ?? '')
  })
  reader.addEventListener('error', () => {
    message.error('Logo 上传失败')
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

async function onSubmitCompany() {
  try {
    await formRef.value?.validate()
  } catch {
    return
  }

  submitting.value = true
  const body = {
    name: formState.name.trim(),
    code: formState.code.trim(),
    logoUrl: formState.logoUrl || undefined,
    phone: formState.phone.trim() || undefined,
    website: formState.website.trim() || undefined,
    sortOrder: formState.sortOrder,
    status: formState.status,
  }

  try {
    if (editingCompany.value) {
      await logisticsApi.update(editingCompany.value.id, body)
      message.success('物流公司已更新')
    } else {
      await logisticsApi.create(body)
      message.success('物流公司已创建')
    }
    modalOpen.value = false
    await fetchCompanies()
  } catch (e) {
    // 代码重复 / 乐观锁冲突等：透出后端 message（如「公司代码已存在」）
    message.error(e instanceof Error && e.message ? e.message : '保存物流公司失败，请重试')
  } finally {
    submitting.value = false
  }
}

// ---------- 启用 / 停用 ----------
const disableConfirmOpen = ref(false)
const pendingDisable = ref<LogisticsCompanyDto | null>(null)

function onDisable(record: LogisticsCompanyDto) {
  pendingDisable.value = record
  disableConfirmOpen.value = true
}

async function onConfirmDisable() {
  disableConfirmOpen.value = false
  const target = pendingDisable.value
  if (!target) return

  try {
    await logisticsApi.disable(target.id)
    // 局部更新状态列，不重新拉全量
    target.status = 'Inactive'
    message.success(`物流公司「${target.name}」已停用`)
  } catch (e) {
    message.error(e instanceof Error && e.message ? e.message : '停用失败，请重试')
  } finally {
    pendingDisable.value = null
  }
}

async function onEnable(record: LogisticsCompanyDto) {
  try {
    await logisticsApi.enable(record.id)
    record.status = 'Active'
    message.success(`物流公司「${record.name}」已启用`)
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

  const header = ['物流公司ID', '公司名称', '代码', '官方电话', '官网链接', '排序值', '创建时间', '状态']
  const rows = tableData.value.map((c) => [
    c.id,
    c.name,
    c.code,
    c.phone ?? '',
    c.website ?? '',
    String(c.sortOrder),
    formatDateTime(c.createdAt),
    c.status === 'Active' ? '启用' : '停用',
  ])

  const csv = [header, ...rows].map((row) => row.map(csvEscape).join(',')).join('\n')
  const blob = new Blob([`\uFEFF${csv}`], { type: 'text/csv;charset=utf-8;' })
  const url = URL.createObjectURL(blob)
  const link = document.createElement('a')
  link.href = url
  link.download = `物流公司导出_${Date.now()}.csv`
  document.body.appendChild(link)
  link.click()
  document.body.removeChild(link)
  URL.revokeObjectURL(url)
  message.success(`已导出当前页 ${rows.length} 条数据`)
}

// ---------- 初始化 ----------
onMounted(() => {
  void fetchCompanies()
})
</script>

<style scoped>
.logistics-companies {
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

.mono {
  font-family: 'SFMono-Regular', Consolas, 'Liberation Mono', Menlo, monospace;
  font-size: 13px;
}

.code-text {
  color: #8c8c8c;
}

.company-name {
  font-size: 14px;
  font-weight: 500;
  color: #000000d9;
}

.phone-text {
  font-size: 12px;
  color: #8c8c8c;
}

.website-link {
  font-size: 13px;
  color: #1677ff;
}

.cell-sub {
  font-size: 12px;
  color: #8c8c8c;
}

.logo-fallback {
  display: flex;
  align-items: center;
  justify-content: center;
  width: 32px;
  height: 32px;
  background: #f0f0f0;
  border-radius: 4px;
  color: #8c8c8c;
  font-size: 14px;
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
