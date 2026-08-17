<script setup lang="ts">
import { h, onMounted, reactive, ref } from 'vue'
import { message } from 'ant-design-vue'
import type { FormInstance, TableColumnsType } from 'ant-design-vue'
import {
  Alert,
  Button,
  Card,
  Divider,
  Drawer,
  Form,
  FormItem,
  Input,
  InputNumber,
  Modal,
  Radio,
  RadioGroup,
  Select,
  Space,
  Table,
  Tag,
  Typography,
} from 'ant-design-vue'
import { PlusOutlined, ReloadOutlined } from '@ant-design/icons-vue'
import dayjs from 'dayjs'
import { couponApi } from '../api/coupon.api'
import type {
  CouponDto,
  CouponType,
  CouponValidityType,
  ListCouponsParams,
  SaveCouponDto,
} from '../types/coupon.dto'
import PromoStatusTag from '../components/PromoStatusTag.vue'
import { ConcurrencyError } from '@/shared/http'
import { ConfirmDialog, DateTimeRangePicker, EmptyState, IdempotencyButton } from '@/shared/components'
import { formatDate, formatMoney, formatNumber } from '@/shared/utils/format'

/**
 * 优惠券管理页（03-promotion-ops）
 *
 * - 筛选：名称关键词 / 状态 / 类型
 * - 状态机操作：Draft→编辑/发布；Published→发放/停用（停用二次确认）；Stopped→无
 * - 新增/编辑抽屉：类型切换表单字段（满减=面额+门槛；折扣=折扣率+上限；无门槛=面额）
 * - 发放 Modal：数量输入，校验 1 ≤ n ≤ 剩余库存，成功后局部更新已领/总量/剩余
 */

/** 券类型展示元数据 */
const COUPON_TYPE_META: Record<CouponType, { label: string; color: string }> = {
  FullReduction: { label: '满减券', color: 'orange' },
  Discount: { label: '折扣券', color: 'geekblue' },
  NoThreshold: { label: '无门槛券', color: 'purple' },
}

const statusOptions = [
  { label: '草稿', value: 'Draft' },
  { label: '已发布', value: 'Published' },
  { label: '已停用', value: 'Stopped' },
]

const typeOptions: { label: string; value: CouponType }[] = [
  { label: '满减券', value: 'FullReduction' },
  { label: '折扣券', value: 'Discount' },
  { label: '无门槛券', value: 'NoThreshold' },
]

const validityOptions: { label: string; value: CouponValidityType }[] = [
  { label: '固定区间', value: 'FixedRange' },
  { label: '领取后 N 天', value: 'AfterReceiveDays' },
]

interface FilterState {
  keyword: string
  status: CouponDto['status'] | undefined
  type: CouponType | undefined
}

const filters = reactive<FilterState>({
  keyword: '',
  status: undefined,
  type: undefined,
})

const columns: TableColumnsType = [
  { title: '券名称', dataIndex: 'name', key: 'name', width: 160, ellipsis: true },
  { title: '类型', key: 'type', width: 96 },
  { title: '面额', key: 'faceValue', width: 100 },
  { title: '门槛', key: 'threshold', width: 100 },
  { title: '有效期', key: 'validity', width: 150 },
  { title: '已领', key: 'issuedQuantity', width: 80, align: 'right' },
  { title: '总量', key: 'totalQuantity', width: 80, align: 'right' },
  { title: '剩余', key: 'remainingQuantity', width: 80, align: 'right' },
  { title: '状态', key: 'status', width: 96 },
  { title: '操作', key: 'action', width: 200, fixed: 'right' },
]

const coupons = ref<CouponDto[]>([])
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
    const params: ListCouponsParams = {
      page: pagination.current,
      pageSize: pagination.pageSize,
    }
    if (filters.keyword.trim()) params.keyword = filters.keyword.trim()
    if (filters.status) params.status = filters.status
    if (filters.type) params.type = filters.type
    const data = await couponApi.list(params)
    coupons.value = data.items
    pagination.total = data.total
  } catch (e) {
    coupons.value = []
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
  filters.keyword = ''
  filters.status = undefined
  filters.type = undefined
  onQuery()
}

function onTableChange(pag: { current: number; pageSize: number }) {
  pagination.current = pag.current
  pagination.pageSize = pag.pageSize
  void fetchList()
}

/** 面额/折扣展示文案 */
function faceText(coupon: CouponDto): string {
  if (coupon.type === 'Discount') {
    const folded = Number(((coupon.discountRate ?? 0) * 10).toFixed(1))
    return `${folded}折`
  }
  return formatMoney(coupon.faceValue)
}

/** 门槛展示文案 */
function thresholdText(coupon: CouponDto): string {
  if (coupon.type === 'FullReduction') return `满 ${formatMoney(coupon.threshold)}`
  if (coupon.type === 'NoThreshold') return '无门槛'
  return '—'
}

/** 有效期展示文案 */
function validityText(coupon: CouponDto): string {
  if (coupon.validityType === 'FixedRange') {
    const from = coupon.validFrom ? formatDate(coupon.validFrom) : '-'
    const to = coupon.validTo ? formatDate(coupon.validTo) : '-'
    return `${from} ~ ${to}`
  }
  return coupon.validDays ? `领取后 ${coupon.validDays} 天` : '—'
}

// ==================== 发布 / 停用 ====================

const actionLoadingKey = ref<string | null>(null)

function isActionLoading(id: string, action: string): boolean {
  return actionLoadingKey.value === `${id}:${action}`
}

async function onPublish(coupon: CouponDto) {
  const loadingKey = `${coupon.id}:publish`
  actionLoadingKey.value = loadingKey
  try {
    await couponApi.publish(coupon.id)
    message.success('券模板已发布，买家端可领取')
    await fetchList()
  } catch (e) {
    handleActionError(e, '发布失败')
  } finally {
    if (actionLoadingKey.value === loadingKey) actionLoadingKey.value = null
  }
}

const stopConfirmState = ref<CouponDto | null>(null)

async function onConfirmStop() {
  const coupon = stopConfirmState.value
  stopConfirmState.value = null
  if (!coupon) return
  const loadingKey = `${coupon.id}:stop`
  actionLoadingKey.value = loadingKey
  try {
    await couponApi.stop(coupon.id)
    message.success('券模板已停用')
    await fetchList()
  } catch (e) {
    handleActionError(e, '停用失败')
  } finally {
    if (actionLoadingKey.value === loadingKey) actionLoadingKey.value = null
  }
}

function handleActionError(err: unknown, fallback: string) {
  if (err instanceof ConcurrencyError) {
    message.warning('券模板状态已变更，请刷新后重试')
    return
  }
  message.error(err instanceof Error && err.message ? err.message : fallback)
}

// ==================== 发放 ====================

const issueModalOpen = ref(false)
const issueTarget = ref<CouponDto | null>(null)
const issueQuantity = ref<number | null>(null)
const issuing = ref(false)

function openIssue(coupon: CouponDto) {
  issueTarget.value = coupon
  issueQuantity.value = 1
  issueModalOpen.value = true
}

async function onSubmitIssue() {
  const coupon = issueTarget.value
  if (!coupon) return
  const quantity = issueQuantity.value
  if (quantity === null) {
    message.error('请输入发放数量')
    return
  }
  if (!Number.isInteger(quantity) || quantity < 1) {
    message.error('发放数量须为不小于 1 的整数')
    return
  }
  if (quantity > coupon.remainingQuantity) {
    message.error('发放数量超过剩余库存')
    return
  }
  issuing.value = true
  try {
    const updated = await couponApi.issue(coupon.id, quantity)
    // 局部更新已领/总量/剩余列（md §3 数据加载策略）
    const index = coupons.value.findIndex((c) => c.id === coupon.id)
    if (index >= 0) coupons.value[index] = updated
    message.success(`已发放 ${quantity} 张「${coupon.name}」`)
    issueModalOpen.value = false
    issueTarget.value = null
  } catch (e) {
    handleActionError(e, '发放失败')
  } finally {
    issuing.value = false
  }
}

// ==================== 新增/编辑抽屉 ====================

interface CouponFormState {
  name: string
  type: CouponType
  faceValue: number | null
  threshold: number | null
  discountRate: number | null
  discountCap: number | null
  validityType: CouponValidityType
  validRange: [string, string] | null
  validDays: number | null
  totalQuantity: number | null
  perUserLimit: number | null
}

const drawerOpen = ref(false)
const submitting = ref(false)
const editingId = ref<string | null>(null)
const formRef = ref<FormInstance>()

const formState = reactive<CouponFormState>({
  name: '',
  type: 'FullReduction',
  faceValue: null,
  threshold: null,
  discountRate: null,
  discountCap: null,
  validityType: 'AfterReceiveDays',
  validRange: null,
  validDays: null,
  totalQuantity: null,
  perUserLimit: 1,
})

function resetForm() {
  formState.name = ''
  formState.type = 'FullReduction'
  formState.faceValue = null
  formState.threshold = null
  formState.discountRate = null
  formState.discountCap = null
  formState.validityType = 'AfterReceiveDays'
  formState.validRange = null
  formState.validDays = null
  formState.totalQuantity = null
  formState.perUserLimit = 1
  formRef.value?.clearValidate()
}

function openCreate() {
  editingId.value = null
  resetForm()
  drawerOpen.value = true
}

function openEdit(coupon: CouponDto) {
  editingId.value = coupon.id
  resetForm()
  formState.name = coupon.name
  formState.type = coupon.type
  formState.faceValue = coupon.faceValue
  formState.threshold = coupon.threshold
  formState.discountRate = coupon.discountRate ?? null
  formState.discountCap = coupon.discountCap ?? null
  formState.validityType = coupon.validityType
  formState.validRange =
    coupon.validFrom && coupon.validTo ? [coupon.validFrom, coupon.validTo] : null
  formState.validDays = coupon.validDays ?? null
  formState.totalQuantity = coupon.totalQuantity
  formState.perUserLimit = coupon.perUserLimit
  drawerOpen.value = true
}

function onFormRangeChange(value: [string, string]) {
  formState.validRange = value
}

const formRules = {
  name: [{ required: true, message: '请输入券名称', trigger: 'blur' }],
  type: [{ required: true, message: '请选择券类型', trigger: 'change' }],
  faceValue: [
    {
      trigger: 'change',
      validator: (_rule: unknown, value: number | null) => {
        if (formState.type === 'Discount') return Promise.resolve()
        if (value === null || value <= 0) return Promise.reject(new Error('请输入面额（元）'))
        if (formState.type === 'FullReduction' && formState.threshold !== null && value > formState.threshold) {
          return Promise.reject(new Error('面额不能大于门槛'))
        }
        return Promise.resolve()
      },
    },
  ],
  threshold: [
    {
      trigger: 'change',
      validator: (_rule: unknown, value: number | null) => {
        if (formState.type !== 'FullReduction') return Promise.resolve()
        if (value === null || value <= 0) return Promise.reject(new Error('请输入使用门槛（元）'))
        if (formState.faceValue !== null && formState.faceValue > value) {
          return Promise.reject(new Error('面额不能大于门槛'))
        }
        return Promise.resolve()
      },
    },
  ],
  discountRate: [
    {
      trigger: 'change',
      validator: (_rule: unknown, value: number | null) => {
        if (formState.type !== 'Discount') return Promise.resolve()
        if (value === null || value <= 0 || value >= 1) {
          return Promise.reject(new Error('折扣率须在 0 与 1 之间（如 0.9 = 9 折）'))
        }
        return Promise.resolve()
      },
    },
  ],
  discountCap: [
    {
      trigger: 'change',
      validator: (_rule: unknown, value: number | null) => {
        if (value === null) return Promise.resolve()
        if (value <= 0) return Promise.reject(new Error('折扣上限须大于 0'))
        return Promise.resolve()
      },
    },
  ],
  validRange: [
    {
      trigger: 'change',
      validator: (_rule: unknown, value: [string, string] | null) => {
        if (formState.validityType !== 'FixedRange') return Promise.resolve()
        if (!value || !value[0] || !value[1]) return Promise.reject(new Error('请选择生效区间'))
        const from = dayjs(value[0])
        const to = dayjs(value[1])
        if (!from.isValid() || !to.isValid() || !to.isAfter(from)) {
          return Promise.reject(new Error('生效结束时间须晚于开始时间'))
        }
        return Promise.resolve()
      },
    },
  ],
  validDays: [
    {
      trigger: 'change',
      validator: (_rule: unknown, value: number | null) => {
        if (formState.validityType !== 'AfterReceiveDays') return Promise.resolve()
        if (value === null || !Number.isInteger(value) || value < 1) {
          return Promise.reject(new Error('请输入领取后有效天数（≥1 的整数）'))
        }
        return Promise.resolve()
      },
    },
  ],
  totalQuantity: [
    {
      trigger: 'change',
      validator: (_rule: unknown, value: number | null) => {
        if (value === null || !Number.isInteger(value) || value < 1) {
          return Promise.reject(new Error('发放总量须为不小于 1 的整数'))
        }
        return Promise.resolve()
      },
    },
  ],
  perUserLimit: [
    {
      trigger: 'change',
      validator: (_rule: unknown, value: number | null) => {
        if (value === null || !Number.isInteger(value) || value < 1) {
          return Promise.reject(new Error('每人限领须为不小于 1 的整数'))
        }
        return Promise.resolve()
      },
    },
  ],
}

/** 折扣率即时提示文案 */
function rateHint(value: number | null): string {
  if (value === null || value <= 0 || value >= 1) return ''
  return `约 ${Number((value * 10).toFixed(1))}折`
}

async function onSubmit() {
  try {
    await formRef.value?.validate()
  } catch {
    return
  }
  const body: SaveCouponDto = {
    name: formState.name.trim(),
    type: formState.type,
    validityType: formState.validityType,
    totalQuantity: formState.totalQuantity as number,
    perUserLimit: formState.perUserLimit as number,
  }
  if (formState.type === 'Discount') {
    body.discountRate = formState.discountRate as number
    if (formState.discountCap !== null) body.discountCap = formState.discountCap
  } else {
    body.faceValue = formState.faceValue as number
    if (formState.type === 'FullReduction') body.threshold = formState.threshold as number
  }
  if (formState.validityType === 'FixedRange') {
    const [validFrom, validTo] = formState.validRange as [string, string]
    body.validFrom = validFrom
    body.validTo = validTo
  } else {
    body.validDays = formState.validDays as number
  }
  submitting.value = true
  try {
    if (editingId.value) {
      await couponApi.update(editingId.value, body)
      message.success('券模板已更新')
    } else {
      await couponApi.create(body)
      message.success('券模板已创建（草稿），发布后买家端可见')
    }
    drawerOpen.value = false
    await fetchList()
  } catch (e) {
    handleActionError(e, '保存失败')
  } finally {
    submitting.value = false
  }
}

onMounted(() => {
  void fetchList()
})
</script>

<template>
  <div class="coupons-page">
    <!-- 区域 A：筛选条 -->
    <Card :bordered="false" class="filter-card">
      <Form layout="inline">
        <FormItem label="券名称">
          <Input
            v-model:value="filters.keyword"
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
            style="width: 130px"
          />
        </FormItem>
        <FormItem label="类型">
          <Select
            v-model:value="filters.type"
            :options="typeOptions"
            placeholder="全部类型"
            allow-clear
            style="width: 130px"
          />
        </FormItem>
        <FormItem>
          <Button type="primary" @click="onQuery">查询</Button>
          <Button style="margin-left: 8px" @click="onReset">重置</Button>
        </FormItem>
      </Form>
    </Card>

    <!-- 区域 B + C：工具栏与券模板表格 -->
    <Card :bordered="false" class="table-card">
      <div class="table-toolbar">
        <Button type="primary" :icon="h(PlusOutlined)" @click="openCreate">新增券模板</Button>
        <Button :icon="h(ReloadOutlined)" :loading="loading" @click="fetchList">刷新</Button>
      </div>
      <div v-if="errorMessage" class="state-wrap">
        <EmptyState :description="`加载失败：${errorMessage}`" action-text="重试" @action="fetchList" />
      </div>
      <Table
        v-else
        :columns="columns"
        :data-source="coupons"
        :loading="loading"
        :pagination="pagination"
        row-key="id"
        @change="onTableChange"
      >
        <template #emptyText>
          <EmptyState description="暂无优惠券模板" action-text="新增券模板" @action="openCreate" />
        </template>
        <template #bodyCell="{ column, record }">
          <template v-if="column.key === 'type'">
            <Tag :color="COUPON_TYPE_META[record.type as CouponType].color">
              {{ COUPON_TYPE_META[record.type as CouponType].label }}
            </Tag>
          </template>
          <template v-else-if="column.key === 'faceValue'">
            <span class="face-value" :aria-label="`面额 ${faceText(record)}`">{{ faceText(record) }}</span>
          </template>
          <template v-else-if="column.key === 'threshold'">
            {{ thresholdText(record) }}
          </template>
          <template v-else-if="column.key === 'validity'">
            <span class="time-text">{{ validityText(record) }}</span>
          </template>
          <template v-else-if="column.key === 'issuedQuantity'">
            {{ formatNumber(record.issuedQuantity) }}
          </template>
          <template v-else-if="column.key === 'totalQuantity'">
            {{ formatNumber(record.totalQuantity) }}
          </template>
          <template v-else-if="column.key === 'remainingQuantity'">
            <span :class="record.remainingQuantity <= 0 ? 'stock-out' : 'stock-ok'">
              {{ formatNumber(record.remainingQuantity) }}
            </span>
          </template>
          <template v-else-if="column.key === 'status'">
            <PromoStatusTag kind="coupon" :status="record.status" />
          </template>
          <template v-else-if="column.key === 'action'">
            <Space>
              <IdempotencyButton
                v-if="record.status === 'Draft'"
                type="link"
                size="small"
                :loading="isActionLoading(record.id, 'publish')"
                @click="onPublish(record)"
              >发布</IdempotencyButton>
              <IdempotencyButton
                v-if="record.status === 'Draft'"
                type="link"
                size="small"
                @click="openEdit(record)"
              >编辑</IdempotencyButton>
              <IdempotencyButton
                v-if="record.status === 'Published'"
                type="link"
                size="small"
                :disabled="record.remainingQuantity <= 0"
                @click="openIssue(record)"
              >发放</IdempotencyButton>
              <IdempotencyButton
                v-if="record.status === 'Published'"
                type="link"
                size="small"
                :loading="isActionLoading(record.id, 'stop')"
                @click="stopConfirmState = record"
              >停用</IdempotencyButton>
              <span v-if="record.status === 'Stopped'" class="action-placeholder">—</span>
            </Space>
          </template>
        </template>
      </Table>
    </Card>

    <!-- 区域 D：新增/编辑抽屉 -->
    <Drawer
      v-model:open="drawerOpen"
      :title="editingId ? '编辑券模板' : '新增券模板'"
      placement="right"
      width="640"
      destroy-on-close
    >
      <Form ref="formRef" layout="vertical" :model="formState" :rules="formRules">
        <Typography.Title :level="5" class="section-title">基础信息</Typography.Title>
        <FormItem label="券名称" name="name">
          <Input
            v-model:value="formState.name"
            placeholder="如 新人立减券"
            :maxlength="30"
            show-count
            allow-clear
          />
        </FormItem>
        <FormItem label="券类型" name="type">
          <RadioGroup v-model:value="formState.type">
            <Radio
              v-for="option in typeOptions"
              :key="option.value"
              :value="option.value"
            >{{ option.label }}</Radio>
          </RadioGroup>
        </FormItem>

        <Divider class="section-divider">优惠配置</Divider>
        <template v-if="formState.type === 'Discount'">
          <FormItem label="折扣率（0-1）" name="discountRate">
            <InputNumber
              v-model:value="formState.discountRate"
              :min="0.01"
              :max="0.99"
              :step="0.01"
              :precision="2"
              placeholder="如 0.9 = 9 折"
              style="width: 100%"
            />
            <span class="field-hint">{{ rateHint(formState.discountRate) }}</span>
          </FormItem>
          <FormItem label="单张优惠上限（元，可选）" name="discountCap">
            <InputNumber
              v-model:value="formState.discountCap"
              :min="0.01"
              :precision="2"
              placeholder="如 100，留空表示不设上限"
              style="width: 100%"
            />
          </FormItem>
        </template>
        <template v-else>
          <FormItem label="面额（元）" name="faceValue">
            <InputNumber
              v-model:value="formState.faceValue"
              :min="0.01"
              :precision="2"
              placeholder="如 10"
              style="width: 100%"
            />
          </FormItem>
          <FormItem v-if="formState.type === 'FullReduction'" label="使用门槛（元）" name="threshold">
            <InputNumber
              v-model:value="formState.threshold"
              :min="0.01"
              :precision="2"
              placeholder="如 满 50 可用"
              style="width: 100%"
            />
          </FormItem>
        </template>

        <Divider class="section-divider">有效期</Divider>
        <FormItem name="validityType">
          <RadioGroup v-model:value="formState.validityType">
            <Radio
              v-for="option in validityOptions"
              :key="option.value"
              :value="option.value"
            >{{ option.label }}</Radio>
          </RadioGroup>
        </FormItem>
        <FormItem v-if="formState.validityType === 'FixedRange'" label="生效区间" name="validRange">
          <DateTimeRangePicker :value="formState.validRange" @change="onFormRangeChange" />
        </FormItem>
        <FormItem v-else label="领取后有效天数" name="validDays">
          <InputNumber
            v-model:value="formState.validDays"
            :min="1"
            :precision="0"
            placeholder="如 30"
            style="width: 100%"
          />
        </FormItem>

        <Divider class="section-divider">库存与限领</Divider>
        <FormItem label="发放总量（张）" name="totalQuantity">
          <InputNumber
            v-model:value="formState.totalQuantity"
            :min="1"
            :precision="0"
            placeholder="如 5000"
            style="width: 100%"
          />
        </FormItem>
        <FormItem label="每人限领（张）" name="perUserLimit">
          <InputNumber
            v-model:value="formState.perUserLimit"
            :min="1"
            :precision="0"
            style="width: 100%"
          />
        </FormItem>
      </Form>
      <div class="drawer-footer">
        <Button @click="drawerOpen = false">取消</Button>
        <IdempotencyButton type="primary" :loading="submitting" @click="onSubmit">保存</IdempotencyButton>
      </div>
    </Drawer>

    <!-- 区域 E：发放对话框 -->
    <Modal
      :open="issueModalOpen"
      title="发放优惠券"
      ok-text="确认发放"
      cancel-text="取消"
      :confirm-loading="issuing"
      @ok="onSubmitIssue"
      @cancel="issueModalOpen = false"
    >
      <Form layout="vertical">
        <FormItem label="发放数量" required>
          <InputNumber
            v-model:value="issueQuantity"
            :min="1"
            :max="issueTarget ? issueTarget.remainingQuantity : 1"
            :precision="0"
            style="width: 100%"
          />
        </FormItem>
        <Alert
          type="warning"
          show-icon
          :message="issueTarget ? `「${issueTarget.name}」剩余库存 ${issueTarget.remainingQuantity} 张` : ''"
          description="发放将直接增加该券的已发放量并通知目标用户，请核对数量后确认。"
        />
      </Form>
    </Modal>

    <!-- 停用二次确认 -->
    <ConfirmDialog
      :open="stopConfirmState !== null"
      title="停用券模板"
      :content="stopConfirmState
        ? `停用后「${stopConfirmState.name}」买家端将不可再领取，已领取的券仍可正常使用。`
        : ''"
      @confirm="onConfirmStop"
      @cancel="stopConfirmState = null"
    />
  </div>
</template>

<style scoped>
.coupons-page {
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

.face-value {
  font-size: 16px;
  font-weight: 600;
  color: #ff4d4f;
}

.time-text {
  font-size: 12px;
  color: #8c8c8c;
}

.stock-ok {
  color: #52c41a;
}

.stock-out {
  color: #bfbfbf;
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

.field-hint {
  display: block;
  margin-top: 4px;
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
