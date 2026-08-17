<!-- web/operations/src/modules/08-membership-ops/views/MemberLevels.vue -->
<template>
  <div class="member-levels">
    <a-card :bordered="false" class="table-card">
      <!-- 区域 A：工具栏 -->
      <div class="table-toolbar">
        <div class="toolbar-left">
          <a-button type="primary" @click="onAdd">新增等级</a-button>
          <span class="toolbar-hint">共 {{ tableData.length }} 个等级</span>
        </div>
        <a-space>
          <a-button @click="onExportCsv">导出 CSV</a-button>
          <a-button :loading="loading" @click="fetchLevels">刷新</a-button>
        </a-space>
      </div>

      <!-- 区域 B：等级表格（loading / error / empty 三态） -->
      <div v-if="errorMessage" class="table-error">
        <EmptyState
          :description="`加载失败：${errorMessage}`"
          action-text="重试"
          @action="fetchLevels"
        />
      </div>
      <a-table
        v-else
        :columns="columns"
        :data-source="tableData"
        :loading="loading"
        :pagination="false"
        row-key="id"
      >
        <template #emptyText>
          <EmptyState description="暂无会员等级" action-text="新增等级" @action="onAdd" />
        </template>
        <template #bodyCell="{ column, record }">
          <template v-if="column.key === 'levelNo'">
            <span class="text-muted">V{{ record.levelNo }}</span>
          </template>
          <template v-else-if="column.key === 'name'">
            <span class="level-name" :aria-label="`V${record.levelNo} ${record.name}`">
              {{ record.name }}
            </span>
          </template>
          <template v-else-if="column.key === 'growthThreshold'">
            <span class="growth-value">{{ formatNumber(record.growthThreshold) }}</span>
          </template>
          <template v-else-if="column.key === 'discountRate'">
            <span class="discount-rate" :aria-label="`折扣率 ${record.discountRate}`">
              {{ record.discountRate.toFixed(2) }}
            </span>
            <span class="text-muted">（{{ discountLabel(record.discountRate) }}）</span>
          </template>
          <template v-else-if="column.key === 'benefits'">
            <span class="text-muted">{{ record.benefits || '—' }}</span>
          </template>
          <template v-else-if="column.key === 'memberCount'">
            {{ formatNumber(record.memberCount) }}
          </template>
          <template v-else-if="column.key === 'status'">
            <a-tag :color="record.status === 'Active' ? 'success' : 'default'">
              {{ record.status === 'Active' ? '启用' : '停用' }}
            </a-tag>
          </template>
          <template v-else-if="column.key === 'action'">
            <a-space>
              <a-button type="link" size="small" aria-label="编辑等级" @click="onEdit(record)">
                编辑
              </a-button>
              <a-button
                v-if="record.status !== 'Active'"
                type="link"
                size="small"
                aria-label="启用等级"
                @click="onEnable(record)"
              >
                启用
              </a-button>
              <a-button
                v-else
                type="link"
                size="small"
                danger
                aria-label="停用等级"
                @click="onDisable(record)"
              >
                停用
              </a-button>
            </a-space>
          </template>
        </template>
      </a-table>
    </a-card>

    <!-- 区域 C：新增 / 编辑等级模态框 -->
    <a-modal
      v-model:open="modalOpen"
      :title="editingLevel ? '编辑会员等级' : '新增会员等级'"
      :confirm-loading="submitting"
      :ok-button-props="{ disabled: !!thresholdError || !!rateError }"
      width="520"
      ok-text="保存"
      cancel-text="取消"
      @ok="onSubmitLevel"
    >
      <a-form ref="formRef" :model="formState" :label-col="{ span: 5 }" :wrapper-col="{ span: 17 }">
        <a-form-item label="等级编号">
          <span class="level-no-readonly">V{{ currentLevelNo }}</span>
          <div class="field-hint">自动递增，不可修改</div>
        </a-form-item>
        <a-form-item label="等级名称" name="name" :rules="rules.name">
          <a-input
            v-model:value="formState.name"
            placeholder="输入等级名称，如「黄金会员」"
            :maxlength="20"
          />
        </a-form-item>
        <a-form-item label="成长值门槛" name="growthThreshold" :rules="rules.growthThreshold">
          <a-input-number
            v-model:value="formState.growthThreshold"
            :min="0"
            :precision="0"
            placeholder="如 5000"
            style="width: 100%"
          />
          <div v-if="thresholdError" class="field-error">{{ thresholdError }}</div>
          <div v-else-if="thresholdHint" class="field-hint">{{ thresholdHint }}</div>
        </a-form-item>
        <a-form-item label="折扣率" name="discountRate" :rules="rules.discountRate">
          <a-input-number
            v-model:value="formState.discountRate"
            :min="0"
            :max="1"
            :step="0.01"
            :precision="2"
            placeholder="0-1 之间，如 0.95"
            style="width: 100%"
          />
          <div v-if="rateError" class="field-error">{{ rateError }}</div>
          <div v-else class="field-hint">范围 0-1，须优于上一等级（递减）</div>
        </a-form-item>
        <a-form-item label="权益说明" name="benefits">
          <a-textarea
            v-model:value="formState.benefits"
            :rows="3"
            :maxlength="200"
            show-count
            placeholder="输入权益说明，如「95 折 + 积分加速 1.5x + 生日礼」"
          />
        </a-form-item>
        <a-form-item label="状态">
          <a-switch
            :checked="formState.status === 'Active'"
            checked-children="启用"
            un-checked-children="停用"
            @change="onStatusSwitch"
          />
          <span class="status-hint">停用后新会员不可达该等级，已有会员不受影响</span>
        </a-form-item>
      </a-form>
    </a-modal>

    <!-- 停用二次确认 -->
    <ConfirmDialog
      :open="disableConfirmOpen"
      danger
      title="确认停用等级"
      :content="`停用「${pendingDisable?.name ?? ''}」后，新会员将无法达到该等级。已有该等级的会员不受影响，此操作可逆。`"
      @confirm="onConfirmDisable"
      @cancel="disableConfirmOpen = false"
    />
  </div>
</template>

<script setup lang="ts">
import { computed, onMounted, reactive, ref } from 'vue'
import { message } from 'ant-design-vue'
import type { FormInstance } from 'ant-design-vue'
import type { TableColumnsType } from 'ant-design-vue'
import { ConfirmDialog, EmptyState } from '@/shared/components'
import { formatNumber } from '@/shared/utils/format'
import { memberLevelApi } from '../api/memberLevel.api'
import type { MemberLevelDto, MemberLevelStatus } from '../types/memberLevel.dto'

/**
 * 会员等级页（08-membership-ops）
 *
 * 三区布局：工具栏（新增/导出/刷新）/ 等级表格 / 新增编辑 Modal。
 * - 全量列表按等级编号升序展示（后端保证，前端兜底排序）
 * - 等级编号自动递增只读展示，不可修改
 * - 保存时前端校验：门槛须大于上一等级且小于下一等级（递增）；
 *   折扣率须优于上一等级且劣于下一等级（递减），校验失败红字提示并禁用提交
 * - 停用走 ConfirmDialog 二次确认，说明已有会员不受影响
 */

const columns: TableColumnsType = [
  { title: '编号', key: 'levelNo', width: 70 },
  { title: '等级名称', dataIndex: 'name', key: 'name', width: 140 },
  { title: '成长值门槛', key: 'growthThreshold', width: 110 },
  { title: '折扣率', key: 'discountRate', width: 130 },
  { title: '权益说明', key: 'benefits', ellipsis: true },
  { title: '会员数', key: 'memberCount', width: 100, align: 'right' },
  { title: '状态', key: 'status', width: 90 },
  { title: '操作', key: 'action', width: 130, fixed: 'right' },
]

const tableData = ref<MemberLevelDto[]>([])
const loading = ref(false)
const errorMessage = ref('')

/** 折扣率展示：0.95 → 95 折，1 → 无折扣 */
function discountLabel(rate: number): string {
  if (rate >= 1) return '无折扣'
  return `${Math.round(rate * 100)} 折`
}

async function fetchLevels() {
  loading.value = true
  errorMessage.value = ''
  try {
    const { data } = await memberLevelApi.list()
    // 后端按等级编号升序返回，前端兜底排序保证展示顺序稳定
    tableData.value = [...data].sort((a, b) => a.levelNo - b.levelNo)
  } catch (e) {
    errorMessage.value = e instanceof Error ? e.message : '加载会员等级失败'
    tableData.value = []
  } finally {
    loading.value = false
  }
}

// ---------- 新增 / 编辑 ----------
interface LevelFormState {
  name: string
  growthThreshold: number
  discountRate: number
  benefits: string
  status: MemberLevelStatus
}

const modalOpen = ref(false)
const submitting = ref(false)
const editingLevel = ref<MemberLevelDto | null>(null)
const formRef = ref<FormInstance>()

const formState = reactive<LevelFormState>({
  name: '',
  growthThreshold: 0,
  discountRate: 1,
  benefits: '',
  status: 'Active',
})

const rules = {
  name: [
    { required: true, message: '请输入等级名称', trigger: 'blur' },
    { min: 1, max: 20, message: '等级名称长度为 1-20 字', trigger: 'blur' },
  ],
  growthThreshold: [{ required: true, message: '请输入成长值门槛', trigger: 'blur' }],
  discountRate: [{ required: true, message: '请输入折扣率', trigger: 'blur' }],
}

/** 新增时自动递增的等级编号（当前最大编号 + 1，空列表从 1 起） */
const nextLevelNo = computed(() =>
  tableData.value.length === 0
    ? 1
    : Math.max(...tableData.value.map((l) => l.levelNo)) + 1,
)

/** 当前编辑位对应的等级编号（新增态为 nextLevelNo） */
const currentLevelNo = computed(() => editingLevel.value?.levelNo ?? nextLevelNo.value)

/** 相邻等级：上一等级（编号更小的最大者）与下一等级（编号更大的最小者） */
const adjacentLevels = computed(() => {
  const no = currentLevelNo.value
  const lower = tableData.value
    .filter((l) => l.levelNo < no)
    .sort((a, b) => b.levelNo - a.levelNo)
  const higher = tableData.value
    .filter((l) => l.levelNo > no)
    .sort((a, b) => a.levelNo - b.levelNo)
  return {
    prev: lower[0],
    next: higher[0],
  }
})

/** 门槛递增校验：须大于上一等级、小于下一等级，失败红字提示并禁提交 */
const thresholdError = computed<string | null>(() => {
  const { prev, next } = adjacentLevels.value
  const threshold = formState.growthThreshold
  if (threshold === null || threshold === undefined) return null
  if (prev && threshold <= prev.growthThreshold) {
    return `成长值门槛须大于上一等级「${prev.name}」的 ${formatNumber(prev.growthThreshold)}`
  }
  if (next && threshold >= next.growthThreshold) {
    return `成长值门槛须小于下一等级「${next.name}」的 ${formatNumber(next.growthThreshold)}`
  }
  return null
})

/** 门槛提示文案（无错误时展示上下界约束） */
const thresholdHint = computed(() => {
  const { prev, next } = adjacentLevels.value
  const parts: string[] = []
  if (prev) parts.push(`须大于上一等级 ${formatNumber(prev.growthThreshold)}`)
  if (next) parts.push(`须小于下一等级 ${formatNumber(next.growthThreshold)}`)
  return parts.join('，')
})

/** 折扣率递减校验：须优于上一等级、劣于下一等级，失败红字提示并禁提交 */
const rateError = computed<string | null>(() => {
  const { prev, next } = adjacentLevels.value
  const rate = formState.discountRate
  if (rate === null || rate === undefined) return null
  if (prev && rate >= prev.discountRate) {
    return `折扣率须优于上一等级「${prev.name}」的 ${prev.discountRate.toFixed(2)}（折扣率须递减）`
  }
  if (next && rate <= next.discountRate) {
    return `折扣率须优于下一等级「${next.name}」的 ${next.discountRate.toFixed(2)}（折扣率须递减）`
  }
  return null
})

function resetForm() {
  formState.name = ''
  formState.growthThreshold = 0
  formState.discountRate = 1
  formState.benefits = ''
  formState.status = 'Active'
  formRef.value?.clearValidate()
}

function onAdd() {
  editingLevel.value = null
  resetForm()
  // 新增等级默认门槛：上一等级门槛 + 1000，折扣率：上一等级 - 0.05（仅作初始值）
  const prev = tableData.value[tableData.value.length - 1]
  if (prev) {
    formState.growthThreshold = prev.growthThreshold + 1000
    formState.discountRate = Math.max(0.05, Number((prev.discountRate - 0.05).toFixed(2)))
  }
  modalOpen.value = true
}

function onEdit(record: MemberLevelDto) {
  editingLevel.value = record
  resetForm()
  formState.name = record.name
  formState.growthThreshold = record.growthThreshold
  formState.discountRate = record.discountRate
  formState.benefits = record.benefits ?? ''
  formState.status = record.status
  modalOpen.value = true
}

function onStatusSwitch(checked: boolean | string | number) {
  formState.status = checked ? 'Active' : 'Inactive'
}

async function onSubmitLevel() {
  try {
    await formRef.value?.validate()
  } catch {
    return
  }
  // 双保险：按钮已禁用，此处再拦一次避免键盘提交绕过
  if (thresholdError.value || rateError.value) {
    message.error(thresholdError.value ?? rateError.value ?? '校验未通过')
    return
  }

  submitting.value = true
  const body = {
    name: formState.name.trim(),
    growthThreshold: formState.growthThreshold,
    discountRate: formState.discountRate,
    benefits: formState.benefits.trim() || undefined,
    status: formState.status,
  }

  try {
    if (editingLevel.value) {
      await memberLevelApi.update(editingLevel.value.id, body)
      message.success('等级已更新')
    } else {
      await memberLevelApi.create(body)
      message.success('等级已创建')
    }
    modalOpen.value = false
    await fetchLevels()
  } catch (e) {
    // 门槛不递增 / 折扣率不递减等后端校验：透出 message
    message.error(e instanceof Error && e.message ? e.message : '保存等级失败，请重试')
  } finally {
    submitting.value = false
  }
}

// ---------- 启用 / 停用 ----------
const disableConfirmOpen = ref(false)
const pendingDisable = ref<MemberLevelDto | null>(null)

function onDisable(record: MemberLevelDto) {
  pendingDisable.value = record
  disableConfirmOpen.value = true
}

async function onConfirmDisable() {
  disableConfirmOpen.value = false
  const target = pendingDisable.value
  if (!target) return

  try {
    await memberLevelApi.disable(target.id)
    // 局部更新状态列，不重新拉全量
    target.status = 'Inactive'
    message.success(`等级「${target.name}」已停用`)
  } catch (e) {
    message.error(e instanceof Error && e.message ? e.message : '停用失败，请重试')
  } finally {
    pendingDisable.value = null
  }
}

async function onEnable(record: MemberLevelDto) {
  try {
    await memberLevelApi.enable(record.id)
    record.status = 'Active'
    message.success(`等级「${record.name}」已启用`)
  } catch (e) {
    message.error(e instanceof Error && e.message ? e.message : '启用失败，请重试')
  }
}

// ---------- CSV 导出（全量等级，前端生成） ----------
function csvEscape(value: string): string {
  const escaped = value.replace(/"/g, '""')
  return /[",\n\r]/.test(escaped) ? `"${escaped}"` : escaped
}

function onExportCsv() {
  if (tableData.value.length === 0) {
    message.warning('当前无等级数据可导出')
    return
  }

  const header = ['等级编号', '等级名称', '成长值门槛', '折扣率', '权益说明', '会员数', '状态']
  const rows = tableData.value.map((l) => [
    `V${l.levelNo}`,
    l.name,
    String(l.growthThreshold),
    l.discountRate.toFixed(2),
    l.benefits ?? '',
    String(l.memberCount),
    l.status === 'Active' ? '启用' : '停用',
  ])

  const csv = [header, ...rows].map((row) => row.map(csvEscape).join(',')).join('\r\n')
  const blob = new Blob([`\uFEFF${csv}`], { type: 'text/csv;charset=utf-8;' })
  const url = URL.createObjectURL(blob)
  const link = document.createElement('a')
  link.href = url
  link.download = `会员等级导出_${Date.now()}.csv`
  document.body.appendChild(link)
  link.click()
  document.body.removeChild(link)
  URL.revokeObjectURL(url)
  message.success(`已导出 ${rows.length} 条等级数据`)
}

onMounted(() => {
  void fetchLevels()
})
</script>

<style scoped>
.member-levels {
  display: flex;
  flex-direction: column;
  gap: 16px;
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

.toolbar-left {
  display: flex;
  align-items: center;
  gap: 12px;
}

.toolbar-hint {
  font-size: 12px;
  color: #595959;
}

.table-error {
  padding: 24px;
  text-align: center;
}

.text-muted {
  color: #8c8c8c;
  font-size: 12px;
}

.level-name {
  font-weight: 500;
}

.growth-value {
  font-weight: 500;
  color: rgba(0, 0, 0, 0.85);
}

.discount-rate {
  color: #ff4d4f;
  font-weight: 600;
}

.level-no-readonly {
  display: inline-block;
  padding: 4px 11px;
  background: #f5f5f5;
  border-radius: 6px;
  border: 1px solid #d9d9d9;
  color: #595959;
  cursor: not-allowed;
}

.field-hint {
  margin-top: 4px;
  font-size: 12px;
  color: #8c8c8c;
  line-height: 20px;
}

.field-error {
  margin-top: 4px;
  font-size: 12px;
  color: #ff4d4f;
  line-height: 20px;
}

.status-hint {
  margin-left: 12px;
  font-size: 12px;
  color: #8c8c8c;
}
</style>
