<!-- web/operations/src/modules/08-membership-ops/views/PointsRules.vue -->
<template>
  <div class="points-rules">
    <a-tabs v-model:active-key="activeTab">
      <!-- 区域 A1：积分规则选项卡 -->
      <a-tab-pane key="rules" tab="积分规则">
        <a-card :bordered="false" class="table-card">
          <!-- 工具栏 -->
          <div class="table-toolbar">
            <div class="toolbar-left">
              <a-button type="primary" @click="onAddRule">新增规则</a-button>
              <span class="toolbar-hint">共 {{ rules.length }} 条规则</span>
            </div>
            <a-button :loading="loading" @click="fetchRules">刷新</a-button>
          </div>

          <!-- 规则表格（loading / error / empty 三态） -->
          <div v-if="errorMessage" class="table-error">
            <EmptyState
              :description="`加载失败：${errorMessage}`"
              action-text="重试"
              @action="fetchRules"
            />
          </div>
          <a-table
            v-else
            :columns="ruleColumns"
            :data-source="rules"
            :loading="loading"
            :pagination="false"
            row-key="id"
          >
            <template #emptyText>
              <EmptyState description="暂无积分规则" action-text="新增规则" @action="onAddRule" />
            </template>
            <template #bodyCell="{ column, record }">
              <template v-if="column.key === 'code'">
                <span class="rule-code" :aria-label="`规则编码 ${record.code}`">
                  {{ record.code }}
                </span>
              </template>
              <template v-else-if="column.key === 'actionType'">
                <a-tag color="blue">{{ actionTypeLabel(record.actionType) }}</a-tag>
              </template>
              <template v-else-if="column.key === 'points'">
                <span
                  class="points-value"
                  :class="record.points >= 0 ? 'points-positive' : 'points-negative'"
                  :aria-label="`积分值 ${record.points >= 0 ? '+' + record.points : record.points}`"
                >
                  {{ record.points >= 0 ? `+${record.points}` : String(record.points) }}
                </span>
              </template>
              <template v-else-if="column.key === 'dailyLimit'">
                <span class="daily-limit">{{ record.dailyLimit }} 次/日</span>
              </template>
              <template v-else-if="column.key === 'status'">
                <a-tag :color="record.status === 'Active' ? 'success' : 'default'">
                  {{ record.status === 'Active' ? '启用' : '停用' }}
                </a-tag>
              </template>
              <template v-else-if="column.key === 'action'">
                <a-space>
                  <a-button type="link" size="small" aria-label="编辑规则" @click="onEditRule(record)">
                    编辑
                  </a-button>
                  <a-button
                    v-if="record.status !== 'Active'"
                    type="link"
                    size="small"
                    aria-label="启用规则"
                    @click="onEnableRule(record)"
                  >
                    启用
                  </a-button>
                  <a-button
                    v-else
                    type="link"
                    size="small"
                    danger
                    aria-label="停用规则"
                    @click="onDisableRule(record)"
                  >
                    停用
                  </a-button>
                </a-space>
              </template>
            </template>
          </a-table>
        </a-card>
      </a-tab-pane>

      <!-- 区域 A2：手动发放选项卡 -->
      <a-tab-pane key="award" tab="手动发放">
        <a-card :bordered="false" class="award-card">
          <a-form
            ref="awardFormRef"
            :model="awardForm"
            :label-col="{ span: 5 }"
            :wrapper-col="{ span: 14 }"
            class="award-form"
          >
            <a-form-item label="用户 ID" name="userId" :rules="awardRules.userId">
              <a-input
                v-model:value="awardForm.userId"
                placeholder="输入目标用户 ID，如 U20240156"
                :maxlength="32"
                allow-clear
              />
            </a-form-item>
            <a-form-item label="发放积分值" name="points" :rules="awardRules.points">
              <a-input-number
                v-model:value="awardForm.points"
                :min="1"
                :max="100000"
                :precision="0"
                placeholder="正整数，如 100"
                style="width: 100%"
              />
              <div class="field-hint">正整数；负向调整请走积分扣减规则，不在此发放</div>
            </a-form-item>
            <a-form-item label="发放原因" name="reason" :rules="awardRules.reason">
              <a-textarea
                v-model:value="awardForm.reason"
                :rows="3"
                :maxlength="200"
                show-count
                placeholder="输入发放原因（5-200 字），如「VIP 会员专属活动补偿积分」"
              />
            </a-form-item>
            <a-form-item :wrapper-col="{ offset: 5, span: 14 }">
              <IdempotencyButton type="primary" :loading="awarding" @click="onSubmitAward">
                确认发放
              </IdempotencyButton>
              <span class="award-hint">发放后立即生效且不可撤销，将走二次确认</span>
            </a-form-item>
          </a-form>
        </a-card>
      </a-tab-pane>
    </a-tabs>

    <!-- 区域 B：新增 / 编辑规则模态框 -->
    <a-modal
      v-model:open="ruleModalOpen"
      :title="editingRule ? '编辑积分规则' : '新增积分规则'"
      :confirm-loading="submitting"
      width="520"
      ok-text="保存"
      cancel-text="取消"
      @ok="onSubmitRule"
    >
      <a-form ref="ruleFormRef" :model="ruleForm" :label-col="{ span: 5 }" :wrapper-col="{ span: 17 }">
        <a-form-item label="规则编码" name="code" :rules="ruleFormRules.code">
          <a-input
            v-model:value="ruleForm.code"
            :disabled="!!editingRule"
            :placeholder="editingRule ? '编码创建后不可修改' : '大写字母 + 下划线，如 DAILY_CHECK_IN'"
            :maxlength="50"
          />
          <div v-if="!editingRule" class="field-hint">全局唯一，创建后不可修改</div>
        </a-form-item>
        <a-form-item label="规则名称" name="name" :rules="ruleFormRules.name">
          <a-input v-model:value="ruleForm.name" placeholder="如 每日签到" :maxlength="30" />
        </a-form-item>
        <a-form-item label="行为类型" name="actionType" :rules="ruleFormRules.actionType">
          <a-select
            v-model:value="ruleForm.actionType"
            placeholder="选择行为类型"
            :options="actionTypeOptions"
          />
        </a-form-item>
        <a-form-item label="积分值" name="points" :rules="ruleFormRules.points">
          <a-input-number
            v-model:value="ruleForm.points"
            :min="-1000"
            :max="1000"
            :precision="0"
            placeholder="-1000 ~ 1000 的非零整数"
            style="width: 100%"
          />
          <div class="field-hint">正数发放、负数扣减，不可为 0</div>
        </a-form-item>
        <a-form-item label="每日上限" name="dailyLimit" :rules="ruleFormRules.dailyLimit">
          <a-input-number
            v-model:value="ruleForm.dailyLimit"
            :min="1"
            :max="100"
            :precision="0"
            placeholder="1-100 次/日"
            style="width: 100%"
          />
          <div class="field-hint">该行为每日最多计积分的次数（1-100）</div>
        </a-form-item>
        <a-form-item label="状态">
          <a-switch
            :checked="ruleForm.status === 'Active'"
            checked-children="启用"
            un-checked-children="停用"
            @change="onRuleStatusSwitch"
          />
          <span class="status-hint">停用后该行为不再发放积分，可随时重新启用</span>
        </a-form-item>
      </a-form>
    </a-modal>

    <!-- 停用规则二次确认 -->
    <ConfirmDialog
      :open="disableConfirmOpen"
      danger
      title="确认停用规则"
      :content="`停用「${pendingDisable?.name ?? ''}」后，该行为将不再发放积分，此操作可逆。`"
      @confirm="onConfirmDisableRule"
      @cancel="disableConfirmOpen = false"
    />

    <!-- 手动发放不可撤销确认 -->
    <ConfirmDialog
      :open="awardConfirmOpen"
      danger
      title="确认发放积分"
      :content="`即将向用户「${awardForm.userId || '-'}」发放 ${awardForm.points ?? 0} 积分，原因：${awardForm.reason || '-'}。积分发放后立即生效且不可撤销，请确认无误。`"
      @confirm="onConfirmAward"
      @cancel="awardConfirmOpen = false"
    />
  </div>
</template>

<script setup lang="ts">
import { computed, onMounted, reactive, ref } from 'vue'
import { message } from 'ant-design-vue'
import type { FormInstance, TableColumnsType } from 'ant-design-vue'
import { ConfirmDialog, EmptyState, IdempotencyButton } from '@/shared/components'
import { pointsRuleApi } from '../api/pointsRule.api'
import {
  POINTS_ACTION_TYPES,
  POINTS_ACTION_TYPE_LABELS,
  type PointsActionType,
  type PointsRuleDto,
  type PointsRuleStatus,
} from '../types/pointsRule.dto'

/**
 * 积分规则页（08-membership-ops）
 *
 * 双选项卡布局：
 * - 积分规则：规则表格 CRUD（编码唯一不可改、积分值 -1000~1000 非零、每日上限 1-100、启停用）
 * - 手动发放：用户 ID + 正整数积分 + 原因（5-200 字），ConfirmDialog 强调不可撤销后提交
 */

const activeTab = ref<'rules' | 'award'>('rules')

// ---------- 积分规则列表 ----------
const ruleColumns: TableColumnsType = [
  { title: '编码', key: 'code', width: 180 },
  { title: '规则名称', dataIndex: 'name', key: 'name', width: 150 },
  { title: '行为类型', key: 'actionType', width: 100 },
  { title: '积分值', key: 'points', width: 100, align: 'right' },
  { title: '每日上限', key: 'dailyLimit', width: 110, align: 'right' },
  { title: '状态', key: 'status', width: 90 },
  { title: '操作', key: 'action', width: 130, fixed: 'right' },
]

const rules = ref<PointsRuleDto[]>([])
const loading = ref(false)
const errorMessage = ref('')

function actionTypeLabel(actionType: PointsActionType): string {
  return POINTS_ACTION_TYPE_LABELS[actionType]
}

const actionTypeOptions = computed(() =>
  POINTS_ACTION_TYPES.map((t) => ({ label: POINTS_ACTION_TYPE_LABELS[t], value: t })),
)

async function fetchRules() {
  loading.value = true
  errorMessage.value = ''
  try {
    const { data } = await pointsRuleApi.list()
    rules.value = data
  } catch (e) {
    errorMessage.value = e instanceof Error ? e.message : '加载积分规则失败'
    rules.value = []
  } finally {
    loading.value = false
  }
}

// ---------- 新增 / 编辑规则 ----------
interface RuleFormState {
  code: string
  name: string
  actionType: PointsActionType | undefined
  points: number
  dailyLimit: number
  status: PointsRuleStatus
}

const ruleModalOpen = ref(false)
const submitting = ref(false)
const editingRule = ref<PointsRuleDto | null>(null)
const ruleFormRef = ref<FormInstance>()

const ruleForm = reactive<RuleFormState>({
  code: '',
  name: '',
  actionType: undefined,
  points: 1,
  dailyLimit: 1,
  status: 'Active',
})

/** 积分值须为 -1000~1000 的非零整数 */
const pointsValidator = async (_rule: unknown, value: number) => {
  if (value === null || value === undefined) {
    return Promise.reject(new Error('请输入积分值'))
  }
  if (!Number.isInteger(value)) {
    return Promise.reject(new Error('积分值须为整数'))
  }
  if (value === 0) {
    return Promise.reject(new Error('积分值不可为 0'))
  }
  if (value < -1000 || value > 1000) {
    return Promise.reject(new Error('积分值范围为 -1000 ~ 1000'))
  }
  return Promise.resolve()
}

/** 每日上限须为 1-100 的正整数 */
const dailyLimitValidator = async (_rule: unknown, value: number) => {
  if (value === null || value === undefined) {
    return Promise.reject(new Error('请输入每日上限'))
  }
  if (!Number.isInteger(value) || value < 1 || value > 100) {
    return Promise.reject(new Error('每日上限须为 1-100 的正整数'))
  }
  return Promise.resolve()
}

const ruleFormRules = {
  code: [
    { required: true, message: '请输入规则编码', trigger: 'blur' },
    {
      pattern: /^[A-Z][A-Z0-9_]{1,49}$/,
      message: '编码为大写字母开头的字母 / 数字 / 下划线组合（2-50 位）',
      trigger: 'blur',
    },
  ],
  name: [
    { required: true, message: '请输入规则名称', trigger: 'blur' },
    { min: 1, max: 30, message: '规则名称长度为 1-30 字', trigger: 'blur' },
  ],
  actionType: [{ required: true, message: '请选择行为类型', trigger: 'change' }],
  points: [{ required: true, validator: pointsValidator, trigger: 'blur' }],
  dailyLimit: [{ required: true, validator: dailyLimitValidator, trigger: 'blur' }],
}

function resetRuleForm() {
  ruleForm.code = ''
  ruleForm.name = ''
  ruleForm.actionType = undefined
  ruleForm.points = 1
  ruleForm.dailyLimit = 1
  ruleForm.status = 'Active'
  ruleFormRef.value?.clearValidate()
}

function onAddRule() {
  editingRule.value = null
  resetRuleForm()
  ruleModalOpen.value = true
}

function onEditRule(record: PointsRuleDto) {
  editingRule.value = record
  resetRuleForm()
  ruleForm.code = record.code
  ruleForm.name = record.name
  ruleForm.actionType = record.actionType
  ruleForm.points = record.points
  ruleForm.dailyLimit = record.dailyLimit
  ruleForm.status = record.status
  ruleModalOpen.value = true
}

function onRuleStatusSwitch(checked: boolean | string | number) {
  ruleForm.status = checked ? 'Active' : 'Inactive'
}

async function onSubmitRule() {
  try {
    await ruleFormRef.value?.validate()
  } catch {
    return
  }
  if (!ruleForm.actionType) {
    message.error('请选择行为类型')
    return
  }

  submitting.value = true
  const body = {
    code: ruleForm.code.trim().toUpperCase(),
    name: ruleForm.name.trim(),
    actionType: ruleForm.actionType,
    points: ruleForm.points,
    dailyLimit: ruleForm.dailyLimit,
    status: ruleForm.status,
  }

  try {
    if (editingRule.value) {
      await pointsRuleApi.update(editingRule.value.id, body)
      message.success('规则已更新')
    } else {
      await pointsRuleApi.create(body)
      message.success('规则已创建')
    }
    ruleModalOpen.value = false
    await fetchRules()
  } catch (e) {
    // 编码重复（409）等后端校验：透出 message
    message.error(e instanceof Error && e.message ? e.message : '保存规则失败，请重试')
  } finally {
    submitting.value = false
  }
}

// ---------- 启用 / 停用规则 ----------
const disableConfirmOpen = ref(false)
const pendingDisable = ref<PointsRuleDto | null>(null)

function onDisableRule(record: PointsRuleDto) {
  pendingDisable.value = record
  disableConfirmOpen.value = true
}

async function onConfirmDisableRule() {
  disableConfirmOpen.value = false
  const target = pendingDisable.value
  if (!target) return

  try {
    await pointsRuleApi.disable(target.id)
    target.status = 'Inactive'
    message.success(`规则「${target.name}」已停用`)
  } catch (e) {
    message.error(e instanceof Error && e.message ? e.message : '停用失败，请重试')
  } finally {
    pendingDisable.value = null
  }
}

async function onEnableRule(record: PointsRuleDto) {
  try {
    await pointsRuleApi.enable(record.id)
    record.status = 'Active'
    message.success(`规则「${record.name}」已启用`)
  } catch (e) {
    message.error(e instanceof Error && e.message ? e.message : '启用失败，请重试')
  }
}

// ---------- 手动发放 ----------
interface AwardFormState {
  userId: string
  points: number
  reason: string
}

const awardFormRef = ref<FormInstance>()
const awarding = ref(false)
const awardConfirmOpen = ref(false)

const awardForm = reactive<AwardFormState>({
  userId: '',
  points: 100,
  reason: '',
})

/** 发放积分须为正整数 */
const awardPointsValidator = async (_rule: unknown, value: number) => {
  if (value === null || value === undefined) {
    return Promise.reject(new Error('请输入发放积分值'))
  }
  if (!Number.isInteger(value) || value < 1) {
    return Promise.reject(new Error('发放积分值须为正整数'))
  }
  return Promise.resolve()
}

const awardRules = {
  userId: [
    { required: true, message: '请输入用户 ID', trigger: 'blur' },
    { min: 1, max: 32, message: '用户 ID 长度为 1-32 字符', trigger: 'blur' },
  ],
  points: [{ required: true, validator: awardPointsValidator, trigger: 'blur' }],
  reason: [
    { required: true, message: '请输入发放原因', trigger: 'blur' },
    { min: 5, max: 200, message: '发放原因长度为 5-200 字', trigger: 'blur' },
  ],
}

async function onSubmitAward() {
  try {
    await awardFormRef.value?.validate()
  } catch {
    return
  }
  awardConfirmOpen.value = true
}

async function onConfirmAward() {
  awardConfirmOpen.value = false
  awarding.value = true

  try {
    await pointsRuleApi.award({
      userId: awardForm.userId.trim(),
      points: awardForm.points,
      reason: awardForm.reason.trim(),
    })
    message.success(`已向用户「${awardForm.userId.trim()}」发放 ${awardForm.points} 积分`)
    awardForm.userId = ''
    awardForm.points = 100
    awardForm.reason = ''
    awardFormRef.value?.clearValidate()
  } catch (e) {
    // 用户不存在等后端校验：透出 message
    message.error(e instanceof Error && e.message ? e.message : '积分发放失败，请重试')
  } finally {
    awarding.value = false
  }
}

onMounted(() => {
  void fetchRules()
})
</script>

<style scoped>
.points-rules {
  display: flex;
  flex-direction: column;
  gap: 16px;
}

.table-card :deep(.ant-card-body),
.award-card :deep(.ant-card-body) {
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

.rule-code {
  font-family: 'SFMono-Regular', Consolas, 'Liberation Mono', Menlo, monospace;
  font-size: 13px;
  color: #000000d9;
}

.points-value {
  font-weight: 600;
  font-variant-numeric: tabular-nums;
}

.points-positive {
  color: #52c41a;
}

.points-negative {
  color: #ff4d4f;
}

.daily-limit {
  font-size: 12px;
  color: #8c8c8c;
}

.field-hint {
  margin-top: 4px;
  font-size: 12px;
  color: #8c8c8c;
  line-height: 20px;
}

.status-hint {
  margin-left: 12px;
  font-size: 12px;
  color: #8c8c8c;
}

.award-form {
  max-width: 560px;
  margin-top: 8px;
}

.award-hint {
  margin-left: 12px;
  font-size: 12px;
  color: #8c8c8c;
}
</style>
