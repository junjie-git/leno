<script setup lang="ts">
import { computed, h, onMounted, reactive, ref } from 'vue'
import { message } from 'ant-design-vue'
import type { FormInstance, TableColumnsType } from 'ant-design-vue'
import {
  Button,
  Card,
  Divider,
  Drawer,
  Form,
  FormItem,
  Input,
  InputNumber,
  Radio,
  RadioGroup,
  Select,
  Space,
  Spin,
  Table,
  Tag,
  Typography,
} from 'ant-design-vue'
import { DeleteOutlined, PlusOutlined, ReloadOutlined } from '@ant-design/icons-vue'
import dayjs from 'dayjs'
import { promotionApi } from '../api/promotion.api'
import type {
  ListPromotionsParams,
  PromotionActivityDto,
  PromotionRuleDto,
  PromotionScopeType,
  PromotionStatus,
  PromotionType,
  SavePromotionActivityDto,
} from '../types/promotion.dto'
import PromoStatusTag from '../components/PromoStatusTag.vue'
import { ConcurrencyError } from '@/shared/http'
import { ConfirmDialog, DateTimeRangePicker, EmptyState, IdempotencyButton } from '@/shared/components'
import { formatDateTime } from '@/shared/utils/format'

/**
 * 促销活动列表页（03-promotion-ops）
 *
 * - 筛选：名称 / 状态 / 时间范围
 * - 状态机操作：Pending→激活/编辑；Active→暂停/关闭；Paused→恢复/关闭；Closed→无
 * - 新增/编辑抽屉：基础信息 + 阶梯规则动态行编辑器 + 适用范围
 * - 关闭为终态操作，danger 二次确认明示不可逆
 */

/** 活动类型展示元数据 */
const PROMOTION_TYPE_META: Record<PromotionType, { label: string; color: string }> = {
  FullReduction: { label: '满减', color: 'orange' },
  FullDiscount: { label: '满折', color: 'geekblue' },
  FullGift: { label: '满赠', color: 'purple' },
}

/** 适用范围展示元数据 */
const PROMOTION_SCOPE_META: Record<PromotionScopeType, string> = {
  All: '全平台',
  Category: '指定分类',
  Product: '指定商品',
}

/** 筛选状态下拉选项 */
const statusOptions = [
  { label: '待生效', value: 'Pending' },
  { label: '进行中', value: 'Active' },
  { label: '已暂停', value: 'Paused' },
  { label: '已关闭', value: 'Closed' },
]

/** 活动类型抽屉单选选项 */
const typeOptions: { label: string; value: PromotionType }[] = [
  { label: '满减', value: 'FullReduction' },
  { label: '满折', value: 'FullDiscount' },
  { label: '满赠', value: 'FullGift' },
]

interface FilterState {
  name: string
  status: PromotionStatus | undefined
  timeRange: [string, string] | null
}

const filters = reactive<FilterState>({
  name: '',
  status: undefined,
  timeRange: null,
})

const columns: TableColumnsType = [
  { title: '活动名称', dataIndex: 'name', key: 'name', width: 170, ellipsis: true },
  { title: '类型', key: 'type', width: 84 },
  { title: '门槛与优惠', key: 'rules', width: 230 },
  { title: '适用范围', key: 'scope', width: 110 },
  { title: '开始时间', key: 'startTime', width: 160 },
  { title: '结束时间', key: 'endTime', width: 160 },
  { title: '状态', key: 'status', width: 96 },
  { title: '操作', key: 'action', width: 210, fixed: 'right' },
]

const promotions = ref<PromotionActivityDto[]>([])
const loading = ref(false)
const errorMessage = ref<string | null>(null)

const pagination = reactive({
  current: 1,
  pageSize: 20,
  total: 0,
  showSizeChanger: true,
  showTotal: (total: number) => `共 ${total} 条`,
})

async function fetchList() {
  loading.value = true
  errorMessage.value = null
  try {
    const params: ListPromotionsParams = {
      page: pagination.current,
      pageSize: pagination.pageSize,
    }
    if (filters.name.trim()) params.name = filters.name.trim()
    if (filters.status) params.status = filters.status
    if (filters.timeRange) {
      params.startTime = filters.timeRange[0]
      params.endTime = filters.timeRange[1]
    }
    const data = await promotionApi.list(params)
    promotions.value = data.items
    pagination.total = data.total
  } catch (e) {
    promotions.value = []
    pagination.total = 0
    errorMessage.value = e instanceof Error && e.message ? e.message : '网络异常'
  } finally {
    loading.value = false
  }
}

function onQuery() {
  pagination.current = 1
  void fetchList()
}

function onReset() {
  filters.name = ''
  filters.status = undefined
  filters.timeRange = null
  onQuery()
}

function onFilterTimeRangeChange(value: [string, string]) {
  filters.timeRange = value
}

function onTableChange(pag: { current: number; pageSize: number }) {
  pagination.current = pag.current
  pagination.pageSize = pag.pageSize
  void fetchList()
}

/** 金额短格式：整数省略小数位 */
function shortMoney(value: number): string {
  return Number.isInteger(value) ? String(value) : value.toFixed(2)
}

/** 折扣率短文案：0.85 → 8.5折 */
function shortRate(rate: number): string {
  const folded = Number((rate * 10).toFixed(1))
  return `${folded}折`
}

/** 阶梯规则文案 */
function ruleText(rule: PromotionRuleDto, type: PromotionType): string {
  if (type === 'FullReduction') {
    return `满${shortMoney(rule.threshold)}减${shortMoney(rule.discountValue)}`
  }
  if (type === 'FullDiscount') {
    return `满${shortMoney(rule.threshold)}打${shortRate(rule.discountValue)}`
  }
  const gift = rule.giftSkuName || rule.giftSkuId || '赠品'
  const quantity = rule.giftQuantity && rule.giftQuantity > 1 ? `×${rule.giftQuantity}` : ''
  return `满${shortMoney(rule.threshold)}赠${gift}${quantity}`
}

function scopeText(record: PromotionActivityDto): string {
  const base = PROMOTION_SCOPE_META[record.scope]
  if (record.scope === 'All' || record.scopeIds.length === 0) return base
  return `${base}（${record.scopeIds.length}）`
}

// ==================== 状态机操作 ====================

type PromotionAction = 'activate' | 'pause' | 'close'

const confirmState = ref<{ action: PromotionAction; record: PromotionActivityDto } | null>(null)
/** 行内操作按钮 loading 标识 `${id}:${action}` */
const actionLoadingKey = ref<string | null>(null)

function isActionLoading(id: string, action: PromotionAction): boolean {
  return actionLoadingKey.value === `${id}:${action}`
}

function askAction(record: PromotionActivityDto, action: PromotionAction) {
  confirmState.value = { action, record }
}

const confirmMeta = computed(() => {
  const state = confirmState.value
  if (!state) {
    return { danger: false, title: '', content: '' }
  }
  const name = `「${state.record.name}」`
  if (state.action === 'activate') {
    return {
      danger: false,
      title: state.record.status === 'Paused' ? '恢复活动' : '激活活动',
      content: `${name}激活后将立即生效并对买家可见，促销规则随订单结算生效，确定继续？`,
    }
  }
  if (state.action === 'pause') {
    return {
      danger: false,
      title: '暂停活动',
      content: `${name}暂停后将立即停止生效，买家侧不可再参与，可随时恢复。`,
    }
  }
  return {
    danger: true,
    title: '关闭活动',
    content: `${name}关闭后促销规则立即失效，买家端同步下架，且无法恢复或重新开启。此操作不可逆，确定继续？`,
  }
})

function onConfirmAction() {
  const state = confirmState.value
  if (!state) return
  confirmState.value = null
  void executeAction(state.record, state.action)
}

async function executeAction(record: PromotionActivityDto, action: PromotionAction) {
  const loadingKey = `${record.id}:${action}`
  actionLoadingKey.value = loadingKey
  try {
    if (action === 'activate') {
      await promotionApi.activate(record.id)
      message.success(record.status === 'Paused' ? '活动已恢复' : '活动已激活')
    } else if (action === 'pause') {
      await promotionApi.pause(record.id)
      message.success('活动已暂停')
    } else {
      await promotionApi.close(record.id)
      message.success('活动已关闭')
    }
    await fetchList()
  } catch (e) {
    if (e instanceof ConcurrencyError) {
      message.warning('活动状态已变更，请刷新后重试')
    } else {
      message.error(e instanceof Error && e.message ? e.message : '状态变更失败')
    }
  } finally {
    if (actionLoadingKey.value === loadingKey) actionLoadingKey.value = null
  }
}

// ==================== 新增/编辑抽屉 ====================

interface PromotionFormState {
  name: string
  type: PromotionType
  timeRange: [string, string] | null
  scope: PromotionScopeType
  scopeIds: string[]
}

/** 阶梯规则行编辑模型 */
interface RuleRowModel {
  key: number
  threshold: number | null
  discountValue: number | null
  giftSkuId: string
  giftQuantity: number
}

const drawerOpen = ref(false)
const detailLoading = ref(false)
const submitting = ref(false)
const editingId = ref<string | null>(null)
const formRef = ref<FormInstance>()

const formState = reactive<PromotionFormState>({
  name: '',
  type: 'FullReduction',
  timeRange: null,
  scope: 'All',
  scopeIds: [],
})

const ruleRows = ref<RuleRowModel[]>([])
let ruleKeySeq = 0

function newRuleRow(): RuleRowModel {
  ruleKeySeq += 1
  return { key: ruleKeySeq, threshold: null, discountValue: null, giftSkuId: '', giftQuantity: 1 }
}

function resetForm() {
  formState.name = ''
  formState.type = 'FullReduction'
  formState.timeRange = null
  formState.scope = 'All'
  formState.scopeIds = []
  ruleRows.value = [newRuleRow()]
  formRef.value?.clearValidate()
}

function openCreate() {
  editingId.value = null
  resetForm()
  drawerOpen.value = true
}

async function openEdit(record: PromotionActivityDto) {
  editingId.value = record.id
  resetForm()
  drawerOpen.value = true
  detailLoading.value = true
  try {
    const detail = await promotionApi.get(record.id)
    formState.name = detail.name
    formState.type = detail.type
    formState.timeRange = [detail.startTime, detail.endTime]
    formState.scope = detail.scope
    formState.scopeIds = [...detail.scopeIds]
    ruleRows.value = detail.rules.map((rule) => {
      ruleKeySeq += 1
      return {
        key: ruleKeySeq,
        threshold: rule.threshold,
        discountValue: rule.discountValue,
        giftSkuId: rule.giftSkuId ?? '',
        giftQuantity: rule.giftQuantity ?? 1,
      }
    })
    if (ruleRows.value.length === 0) ruleRows.value = [newRuleRow()]
  } catch {
    message.error('加载活动详情失败')
    drawerOpen.value = false
  } finally {
    detailLoading.value = false
  }
}

function addRuleRow() {
  ruleRows.value.push(newRuleRow())
}

function removeRuleRow(index: number) {
  ruleRows.value.splice(index, 1)
}

/** 类型切换后优惠值语义变化，重置阶梯行避免脏数据 */
function onTypeChange() {
  ruleRows.value = ruleRows.value.map((row) => ({
    ...row,
    discountValue: null,
    giftSkuId: '',
    giftQuantity: 1,
  }))
}

function onFormTimeRangeChange(value: [string, string]) {
  formState.timeRange = value
}

const formRules = {
  name: [{ required: true, message: '请输入活动名称', trigger: 'blur' }],
  type: [{ required: true, message: '请选择活动类型', trigger: 'change' }],
  timeRange: [
    {
      required: true,
      trigger: 'change',
      validator: (_rule: unknown, value: [string, string] | null) => {
        if (!value || !value[0] || !value[1]) {
          return Promise.reject(new Error('请选择活动时间范围'))
        }
        const start = dayjs(value[0])
        const end = dayjs(value[1])
        if (!start.isValid() || !end.isValid()) {
          return Promise.reject(new Error('请选择活动时间范围'))
        }
        if (!start.isAfter(dayjs())) {
          return Promise.reject(new Error('开始时间须晚于当前时间'))
        }
        if (!end.isAfter(start)) {
          return Promise.reject(new Error('结束时间须晚于开始时间'))
        }
        return Promise.resolve()
      },
    },
  ],
  scope: [{ required: true, message: '请选择适用范围', trigger: 'change' }],
}

/** 优惠值输入的动态标签与提示文案 */
const discountFieldMeta = computed(() => {
  if (formState.type === 'FullReduction') {
    return { label: '优惠金额（元）', placeholder: '如 50' }
  }
  if (formState.type === 'FullDiscount') {
    return { label: '折扣率（0-1）', placeholder: '如 0.85 = 八五折' }
  }
  return { label: '', placeholder: '' }
})

/** 折扣率即时提示文案 */
function discountHint(value: number | null): string {
  if (value === null || value <= 0 || value >= 1) return ''
  return `约 ${shortRate(value)}`
}

/** 校验并构造阶梯规则；不合法时 toast 提示并返回 null */
function buildRules(): PromotionRuleDto[] | null {
  const rows = ruleRows.value
  for (let i = 0; i < rows.length; i++) {
    const row = rows[i]
    const label = `阶梯 ${i + 1}`
    if (row.threshold === null || row.threshold <= 0) {
      message.error(`${label}：门槛金额须大于 0`)
      return null
    }
    if (i > 0 && rows[i - 1].threshold !== null && row.threshold <= (rows[i - 1].threshold as number)) {
      message.error(`${label}：门槛金额须大于上一阶梯（阶梯递增）`)
      return null
    }
    if (formState.type === 'FullReduction') {
      if (row.discountValue === null || row.discountValue <= 0) {
        message.error(`${label}：优惠金额须大于 0`)
        return null
      }
      if (row.discountValue > row.threshold) {
        message.error(`${label}：优惠金额不能大于门槛`)
        return null
      }
    } else if (formState.type === 'FullDiscount') {
      if (row.discountValue === null || row.discountValue <= 0 || row.discountValue >= 1) {
        message.error(`${label}：折扣率须在 0 与 1 之间（如 0.85 = 八五折）`)
        return null
      }
    } else {
      if (!row.giftSkuId.trim()) {
        message.error(`${label}：请填写赠品 SKU ID`)
        return null
      }
      if (!row.giftQuantity || row.giftQuantity < 1) {
        message.error(`${label}：赠品数量至少为 1`)
        return null
      }
    }
  }
  return rows.map((row) => {
    if (formState.type === 'FullGift') {
      const skuId = row.giftSkuId.trim()
      const body: PromotionRuleDto = {
        threshold: row.threshold as number,
        discountValue: 0,
        giftSkuId: skuId,
        giftSkuName: skuId,
        giftQuantity: row.giftQuantity,
      }
      return body
    }
    return {
      threshold: row.threshold as number,
      discountValue: row.discountValue as number,
    }
  })
}

async function onSubmit() {
  try {
    await formRef.value?.validate()
  } catch {
    return
  }
  if (formState.scope !== 'All' && formState.scopeIds.length === 0) {
    message.error(formState.scope === 'Category' ? '请填写指定分类 ID' : '请填写指定商品 ID')
    return
  }
  const rules = buildRules()
  if (rules === null) return
  const [startTime, endTime] = formState.timeRange as [string, string]
  const body: SavePromotionActivityDto = {
    name: formState.name.trim(),
    type: formState.type,
    startTime,
    endTime,
    rules,
    scope: formState.scope,
    scopeIds: formState.scope === 'All' ? [] : [...formState.scopeIds],
  }
  submitting.value = true
  try {
    if (editingId.value) {
      await promotionApi.update(editingId.value, body)
      message.success('活动已更新')
    } else {
      await promotionApi.create(body)
      message.success('活动已创建，待激活生效')
    }
    drawerOpen.value = false
    await fetchList()
  } catch (e) {
    if (e instanceof ConcurrencyError) {
      message.warning('活动状态已变更，请刷新后重试')
    } else {
      message.error(e instanceof Error && e.message ? e.message : '保存失败')
    }
  } finally {
    submitting.value = false
  }
}

onMounted(() => {
  void fetchList()
})
</script>

<template>
  <div class="promotions-page">
    <!-- 区域 A：筛选条 -->
    <Card :bordered="false" class="filter-card">
      <Form layout="inline">
        <FormItem label="活动名称">
          <Input
            v-model:value="filters.name"
            placeholder="名称关键词"
            allow-clear
            style="width: 200px"
            @press-enter="onQuery"
          />
        </FormItem>
        <FormItem label="状态">
          <Select
            v-model:value="filters.status"
            :options="statusOptions"
            placeholder="全部状态"
            allow-clear
            style="width: 140px"
          />
        </FormItem>
        <FormItem label="时间范围">
          <DateTimeRangePicker :value="filters.timeRange" @change="onFilterTimeRangeChange" />
        </FormItem>
        <FormItem>
          <Button type="primary" @click="onQuery">查询</Button>
          <Button style="margin-left: 8px" @click="onReset">重置</Button>
        </FormItem>
      </Form>
    </Card>

    <!-- 区域 B + C：工具栏与活动表格 -->
    <Card :bordered="false" class="table-card">
      <div class="table-toolbar">
        <Button type="primary" :icon="h(PlusOutlined)" @click="openCreate">新增活动</Button>
        <Button :icon="h(ReloadOutlined)" :loading="loading" @click="fetchList">刷新</Button>
      </div>
      <div v-if="errorMessage" class="state-wrap">
        <EmptyState :description="`加载失败：${errorMessage}`" action-text="重试" @action="fetchList" />
      </div>
      <Table
        v-else
        :columns="columns"
        :data-source="promotions"
        :loading="loading"
        :pagination="pagination"
        row-key="id"
        @change="onTableChange"
      >
        <template #emptyText>
          <EmptyState description="暂无促销活动" action-text="新增活动" @action="openCreate" />
        </template>
        <template #bodyCell="{ column, record }">
          <template v-if="column.key === 'type'">
            <Tag :color="PROMOTION_TYPE_META[record.type as PromotionType].color">
              {{ PROMOTION_TYPE_META[record.type as PromotionType].label }}
            </Tag>
          </template>
          <template v-else-if="column.key === 'rules'">
            <div class="rules-cell">
              <span v-for="rule in record.rules" :key="`${record.id}-${rule.threshold}`" class="rule-item">
                {{ ruleText(rule, record.type as PromotionType) }}
              </span>
            </div>
          </template>
          <template v-else-if="column.key === 'scope'">
            {{ scopeText(record) }}
          </template>
          <template v-else-if="column.key === 'startTime'">
            <span class="time-text">{{ formatDateTime(record.startTime) }}</span>
          </template>
          <template v-else-if="column.key === 'endTime'">
            <span class="time-text">{{ formatDateTime(record.endTime) }}</span>
          </template>
          <template v-else-if="column.key === 'status'">
            <PromoStatusTag kind="promotion" :status="record.status" />
          </template>
          <template v-else-if="column.key === 'action'">
            <Space>
              <IdempotencyButton
                v-if="record.status === 'Pending'"
                type="link"
                size="small"
                :loading="isActionLoading(record.id, 'activate')"
                @click="askAction(record, 'activate')"
              >激活</IdempotencyButton>
              <IdempotencyButton
                v-if="record.status === 'Pending'"
                type="link"
                size="small"
                @click="openEdit(record)"
              >编辑</IdempotencyButton>
              <IdempotencyButton
                v-if="record.status === 'Active'"
                type="link"
                size="small"
                :loading="isActionLoading(record.id, 'pause')"
                @click="askAction(record, 'pause')"
              >暂停</IdempotencyButton>
              <IdempotencyButton
                v-if="record.status === 'Paused'"
                type="link"
                size="small"
                :loading="isActionLoading(record.id, 'activate')"
                @click="askAction(record, 'activate')"
              >恢复</IdempotencyButton>
              <IdempotencyButton
                v-if="record.status === 'Active' || record.status === 'Paused'"
                type="link"
                size="small"
                danger
                :loading="isActionLoading(record.id, 'close')"
                @click="askAction(record, 'close')"
              >关闭</IdempotencyButton>
              <span v-if="record.status === 'Closed'" class="action-placeholder">—</span>
            </Space>
          </template>
        </template>
      </Table>
    </Card>

    <!-- 区域 D：新增/编辑抽屉 -->
    <Drawer
      v-model:open="drawerOpen"
      :title="editingId ? '编辑活动' : '新增活动'"
      placement="right"
      width="640"
      destroy-on-close
    >
      <Spin :spinning="detailLoading">
        <Form ref="formRef" layout="vertical" :model="formState" :rules="formRules">
          <Typography.Title :level="5" class="section-title">基础信息</Typography.Title>
          <FormItem label="活动名称" name="name">
            <Input
              v-model:value="formState.name"
              placeholder="请输入活动名称"
              :maxlength="50"
              show-count
              allow-clear
            />
          </FormItem>
          <FormItem label="活动类型" name="type">
            <RadioGroup v-model:value="formState.type" @change="onTypeChange">
              <Radio
                v-for="option in typeOptions"
                :key="option.value"
                :value="option.value"
              >{{ option.label }}</Radio>
            </RadioGroup>
          </FormItem>
          <FormItem label="活动时间" name="timeRange">
            <DateTimeRangePicker :value="formState.timeRange" show-time @change="onFormTimeRangeChange" />
          </FormItem>

          <Divider class="section-divider">阶梯规则</Divider>
          <div class="rule-rows">
            <div v-for="(row, index) in ruleRows" :key="row.key" class="rule-row">
              <div class="rule-row-head">
                <span class="rule-row-index">阶梯 {{ index + 1 }}</span>
                <Button
                  type="text"
                  danger
                  size="small"
                  :icon="h(DeleteOutlined)"
                  :disabled="ruleRows.length <= 1"
                  @click="removeRuleRow(index)"
                >删除</Button>
              </div>
              <div class="rule-row-fields">
                <div class="rule-field">
                  <label class="rule-label">门槛金额（元）</label>
                  <InputNumber
                    v-model:value="row.threshold"
                    :min="0.01"
                    :precision="2"
                    placeholder="如 300"
                    style="width: 100%"
                  />
                </div>
                <div v-if="formState.type === 'FullGift'" class="rule-field rule-field-grow">
                  <label class="rule-label">赠品 SKU ID</label>
                  <Input v-model:value="row.giftSkuId" placeholder="如 sku-1001" allow-clear />
                </div>
                <div v-else class="rule-field">
                  <label class="rule-label">{{ discountFieldMeta.label }}</label>
                  <InputNumber
                    v-model:value="row.discountValue"
                    :min="0.01"
                    :max="formState.type === 'FullDiscount' ? 0.99 : undefined"
                    :step="formState.type === 'FullDiscount' ? 0.01 : 1"
                    :precision="2"
                    :placeholder="discountFieldMeta.placeholder"
                    style="width: 100%"
                  />
                </div>
                <div v-if="formState.type === 'FullGift'" class="rule-field">
                  <label class="rule-label">赠品数量</label>
                  <InputNumber
                    v-model:value="row.giftQuantity"
                    :min="1"
                    :precision="0"
                    style="width: 100%"
                  />
                </div>
                <div v-if="formState.type === 'FullDiscount'" class="rule-field rule-field-hint">
                  <span class="rule-hint">{{ discountHint(row.discountValue) }}</span>
                </div>
              </div>
            </div>
          </div>
          <Button type="dashed" block :icon="h(PlusOutlined)" @click="addRuleRow">添加阶梯</Button>

          <Divider class="section-divider">适用范围</Divider>
          <FormItem name="scope">
            <RadioGroup v-model:value="formState.scope">
              <Radio value="All">全平台</Radio>
              <Radio value="Category">指定分类</Radio>
              <Radio value="Product">指定商品</Radio>
            </RadioGroup>
          </FormItem>
          <FormItem
            v-if="formState.scope === 'Category'"
            label="指定分类 ID（回车确认，可多个）"
          >
            <Select
              v-model:value="formState.scopeIds"
              mode="tags"
              placeholder="如 cat-food、cat-digital"
              :token-separators="[',', '，']"
              style="width: 100%"
            />
          </FormItem>
          <FormItem
            v-if="formState.scope === 'Product'"
            label="指定商品 ID（回车确认，可多个）"
          >
            <Select
              v-model:value="formState.scopeIds"
              mode="tags"
              placeholder="如 p-1001、p-1002"
              :token-separators="[',', '，']"
              style="width: 100%"
            />
          </FormItem>
        </Form>
        <div class="drawer-footer">
          <Button @click="drawerOpen = false">取消</Button>
          <IdempotencyButton type="primary" :loading="submitting" @click="onSubmit">保存</IdempotencyButton>
        </div>
      </Spin>
    </Drawer>

    <!-- 激活/暂停/关闭二次确认 -->
    <ConfirmDialog
      :open="confirmState !== null"
      :danger="confirmMeta.danger"
      :title="confirmMeta.title"
      :content="confirmMeta.content"
      @confirm="onConfirmAction"
      @cancel="confirmState = null"
    />
  </div>
</template>

<style scoped>
.promotions-page {
  display: flex;
  flex-direction: column;
  gap: 16px;
}

.filter-card :deep(.ant-card-body) {
  padding: 16px 24px;
}

.table-card :deep(.ant-card-body) {
  padding: 0;
}

.table-toolbar {
  display: flex;
  justify-content: space-between;
  gap: 8px;
  padding: 16px 24px;
}

.state-wrap {
  padding: 32px 0;
  text-align: center;
}

.rules-cell {
  display: flex;
  flex-wrap: wrap;
  gap: 4px 12px;
}

.rule-item {
  font-size: 14px;
  color: #ff4d4f;
}

.time-text {
  font-size: 12px;
  color: #8c8c8c;
}

.action-placeholder {
  color: #bfbfbf;
}

.section-title {
  margin-bottom: 16px;
}

.section-divider {
  margin: 20px 0 16px;
}

.rule-rows {
  display: flex;
  flex-direction: column;
  gap: 12px;
  margin-bottom: 12px;
}

.rule-row {
  padding: 12px;
  border: 1px solid #f0f0f0;
  border-radius: 8px;
  background: #fafafa;
}

.rule-row-head {
  display: flex;
  justify-content: space-between;
  align-items: center;
  margin-bottom: 8px;
}

.rule-row-index {
  font-size: 13px;
  font-weight: 500;
  color: #595959;
}

.rule-row-fields {
  display: flex;
  flex-wrap: wrap;
  gap: 12px;
}

.rule-field {
  width: 160px;
}

.rule-field-grow {
  flex: 1;
  min-width: 200px;
}

.rule-field-hint {
  width: auto;
  display: flex;
  align-items: flex-end;
  padding-bottom: 2px;
}

.rule-label {
  display: block;
  margin-bottom: 4px;
  font-size: 13px;
  color: #595959;
}

.rule-hint {
  font-size: 12px;
  color: #8c8c8c;
}

.drawer-footer {
  display: flex;
  justify-content: flex-end;
  gap: 8px;
  margin-top: 24px;
  padding-top: 16px;
  border-top: 1px solid #f0f0f0;
}
</style>
