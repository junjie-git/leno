<!-- web/operations/src/modules/08-membership-ops/views/MembershipPackages.vue -->
<template>
  <div class="membership-packages">
    <a-card :bordered="false" class="table-card">
      <!-- 区域 A：工具栏 -->
      <div class="table-toolbar">
        <div class="toolbar-left">
          <a-button type="primary" @click="onAdd">新增套餐</a-button>
          <span class="toolbar-hint">共 {{ tableData.length }} 个套餐</span>
        </div>
        <a-button :loading="loading" @click="fetchAll">刷新</a-button>
      </div>

      <!-- 区域 B：套餐表格（loading / error / empty 三态） -->
      <div v-if="errorMessage" class="table-error">
        <EmptyState
          :description="`加载失败：${errorMessage}`"
          action-text="重试"
          @action="fetchAll"
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
          <EmptyState description="暂无会员套餐" action-text="新增套餐" @action="onAdd" />
        </template>
        <template #bodyCell="{ column, record }">
          <template v-if="column.key === 'name'">
            <span class="package-name">{{ record.name }}</span>
          </template>
          <template v-else-if="column.key === 'price'">
            <span class="price" :aria-label="`价格 ${record.price} 元`">
              ¥{{ record.price.toFixed(2) }}
            </span>
          </template>
          <template v-else-if="column.key === 'durationDays'">
            <a-tag color="blue">{{ record.durationDays }} 天</a-tag>
          </template>
          <template v-else-if="column.key === 'linkedLevel'">
            {{ linkedLevelName(record) }}
          </template>
          <template v-else-if="column.key === 'benefits'">
            <div class="benefit-tags">
              <a-tag
                v-for="benefit in record.benefits"
                :key="benefit"
                class="benefit-tag"
                :aria-label="benefitLabel(benefit)"
              >
                {{ benefitLabel(benefit) }}
              </a-tag>
              <span v-if="record.benefits.length === 0" class="text-muted">—</span>
            </div>
          </template>
          <template v-else-if="column.key === 'subscriberCount'">
            {{ formatNumber(record.subscriberCount) }}
          </template>
          <template v-else-if="column.key === 'status'">
            <a-tag :color="record.status === 'Active' ? 'success' : 'default'">
              {{ record.status === 'Active' ? '启用' : '停用' }}
            </a-tag>
          </template>
          <template v-else-if="column.key === 'action'">
            <a-space>
              <a-button type="link" size="small" aria-label="编辑套餐" @click="onEdit(record)">
                编辑
              </a-button>
              <a-button
                v-if="record.status !== 'Active'"
                type="link"
                size="small"
                aria-label="启用套餐"
                @click="onEnable(record)"
              >
                启用
              </a-button>
              <a-button
                v-else
                type="link"
                size="small"
                danger
                aria-label="停用套餐"
                @click="onDisable(record)"
              >
                停用
              </a-button>
            </a-space>
          </template>
        </template>
      </a-table>
    </a-card>

    <!-- 区域 C：新增 / 编辑套餐模态框 -->
    <a-modal
      v-model:open="modalOpen"
      :title="editingPackage ? '编辑会员套餐' : '新增会员套餐'"
      :confirm-loading="submitting"
      :ok-button-props="{ disabled: !!linkedLevelError }"
      width="640"
      ok-text="保存"
      cancel-text="取消"
      @ok="onSubmitPackage"
    >
      <a-form ref="formRef" :model="formState" :label-col="{ span: 5 }" :wrapper-col="{ span: 18 }">
        <div class="form-section-title">基础信息</div>
        <a-form-item label="套餐名称" name="name" :rules="rules.name">
          <a-input
            v-model:value="formState.name"
            placeholder="输入套餐名称，如「月度会员」"
            :maxlength="30"
          />
        </a-form-item>
        <a-form-item label="价格" name="price" :rules="rules.price">
          <a-input-number
            v-model:value="formState.price"
            :min="0.01"
            :precision="2"
            placeholder="输入价格（元）"
            style="width: 180px"
          />
          <span class="unit-hint">元（须大于 0，保留两位小数）</span>
        </a-form-item>
        <a-form-item label="时长" name="durationDays" :rules="rules.durationDays">
          <a-select
            v-model:value="formState.durationDays"
            placeholder="选择套餐时长"
            :options="durationOptions"
            style="width: 180px"
          />
        </a-form-item>
        <a-form-item label="关联等级" name="linkedLevelId" :rules="rules.linkedLevelId">
          <a-select
            v-model:value="formState.linkedLevelId"
            placeholder="选择关联会员等级（仅已启用可选）"
            :options="levelOptions"
            :loading="levelsLoading"
          />
          <div v-if="linkedLevelError" class="field-error">{{ linkedLevelError }}</div>
          <div v-else class="field-hint">须选择已启用的会员等级</div>
        </a-form-item>

        <div class="form-section-title">权益配置</div>
        <a-form-item label="权益" name="benefits" :rules="rules.benefits">
          <a-checkbox-group v-model:value="formState.benefits" :options="benefitOptions" />
          <div class="field-hint">至少选择一项权益，已选 {{ formState.benefits.length }} 项</div>
        </a-form-item>

        <div class="form-section-title">状态设置</div>
        <a-form-item label="状态">
          <a-switch
            :checked="formState.status === 'Active'"
            checked-children="启用"
            un-checked-children="停用"
            @change="onStatusSwitch"
          />
          <span class="status-hint">停用后新用户不可订阅，已订阅用户不受影响</span>
        </a-form-item>
      </a-form>
    </a-modal>

    <!-- 停用二次确认 -->
    <ConfirmDialog
      :open="disableConfirmOpen"
      danger
      title="确认停用套餐"
      :content="`停用「${pendingDisable?.name ?? ''}」后，新用户将无法订阅此套餐。已订阅 ${pendingDisable?.subscriberCount ?? 0} 名用户的权益不受影响，可继续使用至到期。`"
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
import { membershipPackageApi } from '../api/membershipPackage.api'
import { memberLevelApi } from '../api/memberLevel.api'
import type { MembershipPackageDto, MembershipPackageStatus, MembershipBenefit } from '../types/membershipPackage.dto'
import {
  MEMBERSHIP_BENEFITS,
  MEMBERSHIP_BENEFIT_LABELS,
} from '../types/membershipPackage.dto'
import type { MemberLevelDto } from '../types/memberLevel.dto'

/**
 * 会员套餐页（08-membership-ops）
 *
 * 三区布局：工具栏（新增/刷新）/ 套餐表格 / 新增编辑 Modal（含权益多选）。
 * - 套餐列表来自共享字典 GET /membership-packages（全量展示）
 * - 关联等级选项来自 memberLevel api，仅已启用等级可选（停用项 disabled）
 * - 校验：价格 > 0 两位小数；关联等级必须已启用，失败红字提示并禁用提交
 * - 停用走 ConfirmDialog 二次确认，说明已订阅用户不受影响
 */

const columns: TableColumnsType = [
  { title: '套餐名称', dataIndex: 'name', key: 'name', width: 140 },
  { title: '价格', key: 'price', width: 110 },
  { title: '时长', key: 'durationDays', width: 100 },
  { title: '关联等级', key: 'linkedLevel', width: 130 },
  { title: '权益摘要', key: 'benefits', ellipsis: true },
  { title: '订阅数', key: 'subscriberCount', width: 90, align: 'right' },
  { title: '状态', key: 'status', width: 90 },
  { title: '操作', key: 'action', width: 130, fixed: 'right' },
]

/** 时长选项（30 天 / 90 天 / 365 天） */
const durationOptions = [
  { label: '30 天', value: 30 },
  { label: '90 天', value: 90 },
  { label: '365 天', value: 365 },
]

/** 权益多选选项（五项固定权益） */
const benefitOptions = MEMBERSHIP_BENEFITS.map((benefit) => ({
  label: MEMBERSHIP_BENEFIT_LABELS[benefit],
  value: benefit,
}))

function benefitLabel(benefit: MembershipBenefit): string {
  return MEMBERSHIP_BENEFIT_LABELS[benefit] ?? benefit
}

const tableData = ref<MembershipPackageDto[]>([])
const loading = ref(false)
const errorMessage = ref('')

const levels = ref<MemberLevelDto[]>([])
const levelsLoading = ref(false)

async function fetchPackages() {
  loading.value = true
  errorMessage.value = ''
  try {
    const { data } = await membershipPackageApi.list()
    tableData.value = data
  } catch (e) {
    errorMessage.value = e instanceof Error ? e.message : '加载会员套餐失败'
    tableData.value = []
  } finally {
    loading.value = false
  }
}

async function fetchLevels() {
  levelsLoading.value = true
  try {
    const { data } = await memberLevelApi.list()
    levels.value = [...data].sort((a, b) => a.levelNo - b.levelNo)
  } catch (e) {
    message.error(e instanceof Error && e.message ? e.message : '加载会员等级失败')
    levels.value = []
  } finally {
    levelsLoading.value = false
  }
}

function fetchAll() {
  void fetchPackages()
  void fetchLevels()
}

/** 关联等级名称展示：后端冗余字段优先，缺失时回查等级列表 */
function linkedLevelName(record: MembershipPackageDto): string {
  if (record.linkedLevelName) return record.linkedLevelName
  const level = levels.value.find((l) => l.id === record.linkedLevelId)
  return level ? `${level.name}（V${level.levelNo}）` : '—'
}

// ---------- 新增 / 编辑 ----------
interface PackageFormState {
  name: string
  price: number
  durationDays: number | undefined
  linkedLevelId: string | undefined
  benefits: MembershipBenefit[]
  status: MembershipPackageStatus
}

const modalOpen = ref(false)
const submitting = ref(false)
const editingPackage = ref<MembershipPackageDto | null>(null)
const formRef = ref<FormInstance>()

const formState = reactive<PackageFormState>({
  name: '',
  price: 30,
  durationDays: 30,
  linkedLevelId: undefined,
  benefits: [],
  status: 'Active',
})

const rules = {
  name: [
    { required: true, message: '请输入套餐名称', trigger: 'blur' },
    { min: 1, max: 30, message: '套餐名称长度为 1-30 字', trigger: 'blur' },
  ],
  price: [
    {
      required: true,
      validator: (_rule: unknown, value: number | null) => {
        if (value === null || value === undefined) {
          return Promise.reject('请输入价格')
        }
        if (value <= 0) {
          return Promise.reject('价格须大于 0')
        }
        return Promise.resolve()
      },
      trigger: 'change',
    },
  ],
  durationDays: [{ required: true, message: '请选择套餐时长', trigger: 'change' }],
  linkedLevelId: [{ required: true, message: '请选择关联会员等级', trigger: 'change' }],
  benefits: [
    { required: true, type: 'array' as const, message: '请至少选择一项权益', trigger: 'change' },
  ],
}

/** 关联等级选项：仅已启用等级可选，停用项置灰禁选 */
const levelOptions = computed(() =>
  levels.value.map((level) => ({
    label:
      level.status === 'Active'
        ? `${level.name}（V${level.levelNo}）`
        : `${level.name}（V${level.levelNo}）· 已停用`,
    value: level.id,
    disabled: level.status !== 'Active',
  })),
)

/** 关联等级启用校验：所选等级必须处于 Active 状态，失败红字提示并禁用提交 */
const linkedLevelError = computed<string | null>(() => {
  if (!formState.linkedLevelId) return null
  const level = levels.value.find((l) => l.id === formState.linkedLevelId)
  if (!level) return null
  if (level.status !== 'Active') {
    return `关联会员等级「${level.name}」已停用，请选择已启用的等级`
  }
  return null
})

function resetForm() {
  formState.name = ''
  formState.price = 30
  formState.durationDays = 30
  formState.linkedLevelId = undefined
  formState.benefits = []
  formState.status = 'Active'
  formRef.value?.clearValidate()
}

function onAdd() {
  editingPackage.value = null
  resetForm()
  modalOpen.value = true
}

function onEdit(record: MembershipPackageDto) {
  editingPackage.value = record
  resetForm()
  formState.name = record.name
  formState.price = record.price
  formState.durationDays = record.durationDays
  formState.linkedLevelId = record.linkedLevelId
  formState.benefits = [...record.benefits]
  formState.status = record.status
  modalOpen.value = true
}

function onStatusSwitch(checked: boolean | string | number) {
  formState.status = checked ? 'Active' : 'Inactive'
}

async function onSubmitPackage() {
  try {
    await formRef.value?.validate()
  } catch {
    return
  }
  // 双保险：关联等级未启用时阻止提交
  if (linkedLevelError.value) {
    message.error(linkedLevelError.value)
    return
  }

  submitting.value = true
  const body = {
    name: formState.name.trim(),
    price: formState.price,
    durationDays: formState.durationDays as number,
    linkedLevelId: formState.linkedLevelId as string,
    benefits: [...formState.benefits],
    status: formState.status,
  }

  try {
    if (editingPackage.value) {
      await membershipPackageApi.update(editingPackage.value.id, body)
      message.success('套餐已更新')
    } else {
      await membershipPackageApi.create(body)
      message.success('套餐已创建')
    }
    modalOpen.value = false
    await fetchPackages()
  } catch (e) {
    // 关联等级未启用 / 价格非法等：透出后端 message
    message.error(e instanceof Error && e.message ? e.message : '保存套餐失败，请重试')
  } finally {
    submitting.value = false
  }
}

// ---------- 启用 / 停用 ----------
const disableConfirmOpen = ref(false)
const pendingDisable = ref<MembershipPackageDto | null>(null)

function onDisable(record: MembershipPackageDto) {
  pendingDisable.value = record
  disableConfirmOpen.value = true
}

async function onConfirmDisable() {
  disableConfirmOpen.value = false
  const target = pendingDisable.value
  if (!target) return

  try {
    await membershipPackageApi.disable(target.id)
    // 局部更新状态列，不重新拉全量
    target.status = 'Inactive'
    message.success(`套餐「${target.name}」已停用`)
  } catch (e) {
    message.error(e instanceof Error && e.message ? e.message : '停用失败，请重试')
  } finally {
    pendingDisable.value = null
  }
}

async function onEnable(record: MembershipPackageDto) {
  try {
    await membershipPackageApi.enable(record.id)
    record.status = 'Active'
    message.success(`套餐「${record.name}」已启用`)
  } catch (e) {
    message.error(e instanceof Error && e.message ? e.message : '启用失败，请重试')
  }
}

onMounted(() => {
  fetchAll()
})
</script>

<style scoped>
.membership-packages {
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

.package-name {
  font-weight: 500;
}

.price {
  color: #ff4d4f;
  font-size: 16px;
  font-weight: 600;
}

.benefit-tags {
  display: flex;
  flex-wrap: wrap;
  gap: 4px;
}

.benefit-tag {
  margin-inline-end: 0;
  background: #e6f4ff;
  color: #0958d9;
  border-color: #91caff;
}

.form-section-title {
  margin-bottom: 12px;
  padding-left: 8px;
  border-left: 3px solid #1677ff;
  font-size: 14px;
  font-weight: 500;
  color: rgba(0, 0, 0, 0.85);
}

.unit-hint {
  margin-left: 8px;
  font-size: 12px;
  color: #8c8c8c;
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
