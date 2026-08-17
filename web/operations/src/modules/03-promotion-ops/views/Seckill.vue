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
  Select,
  Space,
  Table,
  Typography,
} from 'ant-design-vue'
import { DeleteOutlined, PlusOutlined, ReloadOutlined } from '@ant-design/icons-vue'
import dayjs from 'dayjs'
import { seckillApi } from '../api/seckill.api'
import type {
  CreateSeckillActivityDto,
  ListSeckillActivitiesParams,
  SeckillActivityDto,
  SeckillItemDto,
  SeckillSkuConfigDto,
  SeckillStatus,
} from '../types/seckill.dto'
import PromoStatusTag from '../components/PromoStatusTag.vue'
import { ConcurrencyError } from '@/shared/http'
import { ConfirmDialog, DateTimeRangePicker, EmptyState, IdempotencyButton } from '@/shared/components'
import { formatMoney } from '@/shared/utils/format'

/**
 * 秒杀活动页（03-promotion-ops）
 *
 * - 筛选：状态（待生效/进行中/已关闭）
 * - 状态机操作：Pending→激活（明示初始化 Redis 库存）/编辑；Active→关闭（danger 明示库存回写 DB、不可逆）；Closed→无
 * - 新增/编辑抽屉（800px）：基础信息 + 多 SKU 配置动态表格（秒杀价不高于原价、库存与限购 ≥1、行校验与删除）
 */

const statusOptions = [
  { label: '待生效', value: 'Pending' },
  { label: '进行中', value: 'Active' },
  { label: '已关闭', value: 'Closed' },
]

interface FilterState {
  status: SeckillStatus | undefined
}

const filters = reactive<FilterState>({
  status: undefined,
})

const columns: TableColumnsType = [
  { title: '活动名称', dataIndex: 'name', key: 'name', width: 180, ellipsis: true },
  { title: 'SKU 数', key: 'skuCount', width: 84, align: 'right' },
  { title: '秒杀价区间', key: 'seckillPriceRange', width: 170 },
  { title: '原价区间', key: 'originalPriceRange', width: 170 },
  { title: '时间段', key: 'timeWindow', width: 260 },
  { title: 'Redis 库存', key: 'redisStock', width: 110 },
  { title: '状态', key: 'status', width: 96 },
  { title: '操作', key: 'action', width: 150, fixed: 'right' },
]

const activities = ref<SeckillActivityDto[]>([])
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
    const params: ListSeckillActivitiesParams = {
      page: pagination.current,
      pageSize: pagination.pageSize,
    }
    if (filters.status) params.status = filters.status
    const data = await seckillApi.list(params)
    activities.value = data.items
    pagination.total = data.total
  } catch (e) {
    activities.value = []
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
  filters.status = undefined
  onQuery()
}

function onTableChange(pag: { current: number; pageSize: number }) {
  pagination.current = pag.current
  pagination.pageSize = pag.pageSize
  void fetchList()
}

/** 短时间文案（MM-DD HH:mm） */
function shortTime(value: string): string {
  const d = dayjs(value)
  return d.isValid() ? d.format('MM-DD HH:mm') : '-'
}

function timeWindowText(activity: SeckillActivityDto): string {
  return `${shortTime(activity.startTime)} ~ ${shortTime(activity.endTime)}`
}

/** 价格区间文案：min ~ max，单一价直接展示 */
function priceRangeText(items: SeckillItemDto[], key: 'seckillPrice' | 'originalPrice'): string {
  if (items.length === 0) return '—'
  const values = items.map((item) => item[key])
  const min = Math.min(...values)
  const max = Math.max(...values)
  return min === max ? formatMoney(min) : `${formatMoney(min)} ~ ${formatMoney(max)}`
}

/** Redis 库存初始化状态：全部 SKU 已初始化才视为已初始化 */
function isRedisInitialized(activity: SeckillActivityDto): boolean {
  return activity.items.length > 0 && activity.items.every((item) => item.redisInitialized)
}

// ==================== 状态机操作 ====================

type SeckillAction = 'activate' | 'close'

const confirmState = ref<{ action: SeckillAction; record: SeckillActivityDto } | null>(null)
const actionLoadingKey = ref<string | null>(null)

function isActionLoading(id: string, action: SeckillAction): boolean {
  return actionLoadingKey.value === `${id}:${action}`
}

function askAction(record: SeckillActivityDto, action: SeckillAction) {
  confirmState.value = { action, record }
}

interface ConfirmMeta {
  danger: boolean
  title: string
  content: string
}

const confirmMeta = computed<ConfirmMeta>(() => {
  const state = confirmState.value
  if (!state) {
    return { danger: false, title: '', content: '' }
  }
  const name = `「${state.record.name}」`
  if (state.action === 'activate') {
    return {
      danger: false,
      title: '激活秒杀活动',
      content: `${name}激活后将立即初始化全部 ${state.record.items.length} 个 SKU 的 Redis 库存，活动进入进行中且不可回退到待生效，确定继续？`,
    }
  }
  return {
    danger: true,
    title: '关闭秒杀活动',
    content: `${name}关闭后 Redis 剩余库存将回写 DB，买家端立即下架，且无法重新开启。此操作不可逆，确定继续？`,
  }
})

function onConfirmAction() {
  const state = confirmState.value
  confirmState.value = null
  if (!state) return
  void executeAction(state.record, state.action)
}

async function executeAction(record: SeckillActivityDto, action: SeckillAction) {
  const loadingKey = `${record.id}:${action}`
  actionLoadingKey.value = loadingKey
  try {
    if (action === 'activate') {
      await seckillApi.activate(record.id)
      message.success('活动已激活，Redis 库存已初始化')
    } else {
      await seckillApi.close(record.id)
      message.success('活动已关闭，剩余库存已回写 DB')
    }
    await fetchList()
  } catch (e) {
    if (e instanceof ConcurrencyError) {
      message.warning('活动状态已变更，请刷新后重试')
    } else if (action === 'activate') {
      message.error(e instanceof Error && e.message ? e.message : 'Redis 库存初始化失败，请重试')
    } else {
      message.error(e instanceof Error && e.message ? e.message : '库存回写失败，请联系系统管理员')
    }
  } finally {
    if (actionLoadingKey.value === loadingKey) actionLoadingKey.value = null
  }
}

// ==================== 新增/编辑抽屉 ====================

interface SeckillFormState {
  name: string
  timeRange: [string, string] | null
}

/** SKU 配置行编辑模型 */
interface SeckillSkuRow {
  key: number
  skuId: string
  skuName: string
  originalPrice: number | null
  seckillPrice: number | null
  stock: number | null
  perUserLimit: number | null
}

const drawerOpen = ref(false)
const submitting = ref(false)
const editingId = ref<string | null>(null)
const formRef = ref<FormInstance>()

const formState = reactive<SeckillFormState>({
  name: '',
  timeRange: null,
})

const skuRows = ref<SeckillSkuRow[]>([])
let skuKeySeq = 0

function newSkuRow(): SeckillSkuRow {
  skuKeySeq += 1
  return {
    key: skuKeySeq,
    skuId: '',
    skuName: '',
    originalPrice: null,
    seckillPrice: null,
    stock: null,
    perUserLimit: 1,
  }
}

function resetForm() {
  formState.name = ''
  formState.timeRange = null
  skuRows.value = [newSkuRow()]
  formRef.value?.clearValidate()
}

function openCreate() {
  editingId.value = null
  resetForm()
  drawerOpen.value = true
}

function openEdit(activity: SeckillActivityDto) {
  editingId.value = activity.id
  resetForm()
  formState.name = activity.name
  formState.timeRange = [activity.startTime, activity.endTime]
  skuRows.value = activity.items.map((item) => {
    skuKeySeq += 1
    return {
      key: skuKeySeq,
      skuId: item.skuId,
      skuName: item.skuName,
      originalPrice: item.originalPrice,
      seckillPrice: item.seckillPrice,
      stock: item.stock,
      perUserLimit: item.perUserLimit,
    }
  })
  drawerOpen.value = true
}

function addSkuRow() {
  skuRows.value.push(newSkuRow())
}

function removeSkuRow(index: number) {
  skuRows.value.splice(index, 1)
}

function onFormTimeRangeChange(value: [string, string]) {
  formState.timeRange = value
}

const formRules = {
  name: [{ required: true, message: '请输入活动名称', trigger: 'blur' }],
  timeRange: [
    {
      required: true,
      trigger: 'change',
      validator: (_rule: unknown, value: [string, string] | null) => {
        if (!value || !value[0] || !value[1]) {
          return Promise.reject(new Error('请选择活动时间段'))
        }
        const start = dayjs(value[0])
        const end = dayjs(value[1])
        if (!start.isValid() || !end.isValid()) {
          return Promise.reject(new Error('请选择活动时间段'))
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
}

/** 校验并构造 SKU 配置数组；不合法时 toast 提示并返回 null */
function buildItems(): SeckillSkuConfigDto[] | null {
  const rows = skuRows.value
  if (rows.length === 0) {
    message.error('请至少配置一个 SKU')
    return null
  }
  const seenSkuIds = new Set<string>()
  for (let i = 0; i < rows.length; i++) {
    const row = rows[i]
    const label = `SKU 行 ${i + 1}`
    if (!row.skuId.trim()) {
      message.error(`${label}：请填写 SKU ID`)
      return null
    }
    if (seenSkuIds.has(row.skuId.trim())) {
      message.error(`${label}：SKU ID「${row.skuId.trim()}」重复`)
      return null
    }
    seenSkuIds.add(row.skuId.trim())
    if (!row.skuName.trim()) {
      message.error(`${label}：请填写商品名称`)
      return null
    }
    if (row.originalPrice === null || row.originalPrice <= 0) {
      message.error(`${label}：原价须大于 0`)
      return null
    }
    if (row.seckillPrice === null || row.seckillPrice <= 0) {
      message.error(`${label}：秒杀价须大于 0`)
      return null
    }
    if (row.seckillPrice > row.originalPrice) {
      message.error(`${label}：秒杀价不能高于原价`)
      return null
    }
    if (row.stock === null || !Number.isInteger(row.stock) || row.stock < 1) {
      message.error(`${label}：秒杀库存须为不小于 1 的整数`)
      return null
    }
    if (row.perUserLimit === null || !Number.isInteger(row.perUserLimit) || row.perUserLimit < 1) {
      message.error(`${label}：每人限购须为不小于 1 的整数`)
      return null
    }
  }
  return rows.map((row) => ({
    skuId: row.skuId.trim(),
    skuName: row.skuName.trim(),
    originalPrice: row.originalPrice as number,
    seckillPrice: row.seckillPrice as number,
    stock: row.stock as number,
    perUserLimit: row.perUserLimit as number,
  }))
}

async function onSubmit() {
  try {
    await formRef.value?.validate()
  } catch {
    return
  }
  const items = buildItems()
  if (items === null) return
  const [startTime, endTime] = formState.timeRange as [string, string]
  const body: CreateSeckillActivityDto = {
    name: formState.name.trim(),
    startTime,
    endTime,
    items,
  }
  submitting.value = true
  try {
    if (editingId.value) {
      await seckillApi.update(editingId.value, body)
      message.success('秒杀活动已更新，待激活生效')
    } else {
      await seckillApi.create(body)
      message.success('秒杀活动已创建，待激活生效')
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
  <div class="seckill-page">
    <!-- 区域 A：筛选条 -->
    <Card :bordered="false" class="filter-card">
      <Form layout="inline">
        <FormItem label="状态">
          <Select
            v-model:value="filters.status"
            :options="statusOptions"
            placeholder="全部状态"
            allow-clear
            style="width: 140px"
          />
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
        :data-source="activities"
        :loading="loading"
        :pagination="pagination"
        row-key="id"
        @change="onTableChange"
      >
        <template #emptyText>
          <EmptyState description="暂无秒杀活动" action-text="新增活动" @action="openCreate" />
        </template>
        <template #bodyCell="{ column, record }">
          <template v-if="column.key === 'skuCount'">
            {{ record.items.length }}
          </template>
          <template v-else-if="column.key === 'seckillPriceRange'">
            <span class="seckill-price">{{ priceRangeText(record.items, 'seckillPrice') }}</span>
          </template>
          <template v-else-if="column.key === 'originalPriceRange'">
            <span class="original-price">{{ priceRangeText(record.items, 'originalPrice') }}</span>
          </template>
          <template v-else-if="column.key === 'timeWindow'">
            <span class="time-text">{{ timeWindowText(record) }}</span>
          </template>
          <template v-else-if="column.key === 'redisStock'">
            <span
              class="redis-status"
              :aria-label="isRedisInitialized(record) ? 'Redis 库存已初始化' : 'Redis 库存未初始化'"
            >
              <span
                class="redis-dot"
                :class="isRedisInitialized(record) ? 'redis-dot-on' : 'redis-dot-off'"
              />
              {{ isRedisInitialized(record) ? '已初始化' : '未初始化' }}
            </span>
          </template>
          <template v-else-if="column.key === 'status'">
            <PromoStatusTag kind="seckill" :status="record.status" />
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

    <!-- 区域 D：新增/编辑抽屉（多 SKU 配置） -->
    <Drawer
      v-model:open="drawerOpen"
      :title="editingId ? '编辑秒杀活动' : '新增秒杀活动'"
      placement="right"
      width="800"
      destroy-on-close
    >
      <Form ref="formRef" layout="vertical" :model="formState" :rules="formRules">
        <Typography.Title :level="5" class="section-title">基础信息</Typography.Title>
        <FormItem label="活动名称" name="name">
          <Input
            v-model:value="formState.name"
            placeholder="如 12点整点秒杀"
            :maxlength="50"
            show-count
            allow-clear
          />
        </FormItem>
        <FormItem label="活动时间段" name="timeRange">
          <DateTimeRangePicker :value="formState.timeRange" show-time @change="onFormTimeRangeChange" />
        </FormItem>

        <Divider class="section-divider">SKU 配置（秒杀价不得高于原价，激活时按库存初始化 Redis）</Divider>
      </Form>
      <div class="sku-grid sku-grid-head">
        <span class="sku-col-id">SKU ID</span>
        <span class="sku-col-name">商品名称</span>
        <span class="sku-col-price">原价（元）</span>
        <span class="sku-col-price">秒杀价（元）</span>
        <span class="sku-col-num">秒杀库存</span>
        <span class="sku-col-num">每人限购</span>
        <span class="sku-col-op">操作</span>
      </div>
      <div v-for="(row, index) in skuRows" :key="row.key" class="sku-grid sku-grid-row">
        <div class="sku-col-id">
          <Input v-model:value="row.skuId" placeholder="如 sku-1001" allow-clear />
        </div>
        <div class="sku-col-name">
          <Input v-model:value="row.skuName" placeholder="如 蓝牙耳机 标准版" allow-clear />
        </div>
        <div class="sku-col-price">
          <InputNumber
            v-model:value="row.originalPrice"
            :min="0.01"
            :precision="2"
            placeholder="原价"
            style="width: 100%"
          />
        </div>
        <div class="sku-col-price">
          <InputNumber
            v-model:value="row.seckillPrice"
            :min="0.01"
            :precision="2"
            placeholder="秒杀价"
            style="width: 100%"
          />
        </div>
        <div class="sku-col-num">
          <InputNumber
            v-model:value="row.stock"
            :min="1"
            :precision="0"
            placeholder="库存"
            style="width: 100%"
          />
        </div>
        <div class="sku-col-num">
          <InputNumber
            v-model:value="row.perUserLimit"
            :min="1"
            :precision="0"
            style="width: 100%"
          />
        </div>
        <div class="sku-col-op">
          <Button
            type="text"
            danger
            size="small"
            :icon="h(DeleteOutlined)"
            :disabled="skuRows.length <= 1"
            @click="removeSkuRow(index)"
          >删除</Button>
        </div>
      </div>
      <Button type="dashed" block class="sku-add" :icon="h(PlusOutlined)" @click="addSkuRow">添加 SKU</Button>
      <div class="drawer-footer">
        <Button @click="drawerOpen = false">取消</Button>
        <IdempotencyButton type="primary" :loading="submitting" @click="onSubmit">保存</IdempotencyButton>
      </div>
    </Drawer>

    <!-- 激活/关闭二次确认 -->
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
.seckill-page {
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

.seckill-price {
  font-size: 16px;
  font-weight: 600;
  color: #ff4d4f;
}

.original-price {
  font-size: 12px;
  color: #8c8c8c;
  text-decoration: line-through;
}

.time-text {
  font-size: 12px;
  color: #8c8c8c;
}

.redis-status {
  display: inline-flex;
  align-items: center;
  gap: 6px;
  font-size: 13px;
  color: #595959;
}

.redis-dot {
  width: 8px;
  height: 8px;
  border-radius: 50%;
}

.redis-dot-on {
  background: #52c41a;
}

.redis-dot-off {
  background: #8c8c8c;
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

.sku-grid {
  display: grid;
  grid-template-columns: 130px 1fr 110px 110px 100px 100px 64px;
  gap: 8px;
  align-items: center;
}

.sku-grid-head {
  margin-bottom: 8px;
  font-size: 13px;
  color: #595959;
}

.sku-grid-row {
  margin-bottom: 8px;
  padding: 8px;
  border: 1px solid #f0f0f0;
  border-radius: 8px;
  background: #fafafa;
}

.sku-add {
  margin-top: 4px;
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
