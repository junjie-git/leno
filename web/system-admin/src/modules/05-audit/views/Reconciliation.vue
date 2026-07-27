<!-- web/system-admin/src/modules/05-audit/views/Reconciliation.vue -->
<!-- 对账管理：4 状态卡片 + 触发对账（幂等+确认） + 历史表格 + 详情抽屉（差异项明细） -->
<script setup lang="ts">
import { ref, reactive, computed, onMounted } from 'vue'
import { useRoute } from 'vue-router'
import { message } from 'ant-design-vue'
import {
  PlayCircleOutlined, CheckCircleOutlined, WarningOutlined,
  ExclamationCircleOutlined, EyeOutlined,
} from '@ant-design/icons-vue'
import dayjs from 'dayjs'
import { reconciliationApi } from '../api/reconciliation.api'
import type {
  ReconciliationStatusDto,
  ReconciliationRecordDto,
  ReconciliationReportType,
  ReconciliationStatus,
} from '../types/reconciliation.dto'
import IdempotencyButton from '@/shared/components/IdempotencyButton.vue'
import DateTimeRangePicker from '@/shared/components/DateTimeRangePicker.vue'
import ConfirmDialog from '@/shared/components/ConfirmDialog.vue'
import PermissionGuard from '@/shared/auth/PermissionGuard.vue'
import EmptyState from '@/shared/components/EmptyState.vue'
import { formatDateTime } from '@/shared/utils/format'
import { BusinessError } from '@/shared/http/errors'

const route = useRoute()

const reportTypeOptions: { label: string; value: ReconciliationReportType | '' }[] = [
  { label: '全部', value: '' },
  { label: '订单 GMV', value: 'OrderGmv' },
  { label: '支付成功率', value: 'PaymentSuccessRate' },
  { label: '积分发放', value: 'PointsIssued' },
  { label: '通知送达', value: 'NotificationDelivery' },
  { label: '售后量', value: 'AfterSalesVolume' },
  { label: '店铺排名', value: 'ShopRanking' },
  { label: '转化率', value: 'ConversionRate' },
]

function reportTypeLabel(rt: ReconciliationReportType): string {
  const found = reportTypeOptions.find((o) => o.value === rt)
  return found ? found.label : rt
}

// 默认近 7 天（ISO 8601 UTC）
function defaultRange(): [string, string] {
  return [
    dayjs().subtract(7, 'day').toISOString(),
    dayjs().toISOString(),
  ]
}

const statusLoading = ref(false)
const status = ref<ReconciliationStatusDto | null>(null)

const triggerForm = reactive<{
  reportType: ReconciliationReportType | ''
  timeRange: [string, string] | null
}>({
  reportType: (route.query.reportType as ReconciliationReportType) || '',
  timeRange: defaultRange(),
})

const listLoading = ref(false)
const records = ref<ReconciliationRecordDto[]>([])
const listFilter = reactive<{
  reportType: ReconciliationReportType | ''
  timeRange: [string, string] | null
  page: number
  pageSize: number
}>({
  reportType: (route.query.reportType as ReconciliationReportType) || '',
  timeRange: defaultRange(),
  page: 1,
  pageSize: 20,
})
const listTotal = ref(0)

const triggerConfirmVisible = ref(false)
const triggering = ref(false)

// 详情抽屉
const drawerVisible = ref(false)
const currentRecord = ref<ReconciliationRecordDto | null>(null)

const columns = computed(() => [
  { title: '记录ID', dataIndex: 'recordId', key: 'recordId', width: 140, ellipsis: true },
  { title: '报表类型', key: 'reportType', width: 130, customRender: ({ record }: { record: ReconciliationRecordDto }) => reportTypeLabel(record.reportType) },
  { title: '对账时间', dataIndex: 'reconciledAt', key: 'reconciledAt', width: 180, customRender: ({ text }: { text: string }) => formatDateTime(text) },
  { title: '状态', key: 'status', width: 100 },
  { title: '差异项数', dataIndex: 'discrepancyCount', key: 'discrepancyCount', width: 100, align: 'right' as const },
  { title: '告警', key: 'alertTriggered', width: 80 },
  { title: '修正', key: 'correctionTriggered', width: 80 },
  { title: '错误信息', dataIndex: 'errorMessage', key: 'errorMessage', ellipsis: true },
  { title: '操作', key: 'action-col', width: 90, fixed: 'right' as const },
])

function statusTagType(s: ReconciliationStatus | null): 'success' | 'warning' | 'error' | 'default' {
  if (s === 'Consistent') return 'success'
  if (s === 'Discrepancy') return 'warning'
  if (s === 'Failed') return 'error'
  return 'default'
}
function statusLabel(s: ReconciliationStatus | null): string {
  if (s === 'Consistent') return '一致'
  if (s === 'Discrepancy') return '有差异'
  if (s === 'Failed') return '失败'
  return '尚未执行'
}

async function loadStatus(): Promise<void> {
  statusLoading.value = true
  try {
    const res = await reconciliationApi.getStatus()
    status.value = res.data
  } catch (e: unknown) {
    const msg = e instanceof Error ? e.message : '加载对账状态失败'
    message.error(msg)
  } finally {
    statusLoading.value = false
  }
}

async function loadRecords(): Promise<void> {
  listLoading.value = true
  try {
    const params = {
      reportType: listFilter.reportType || undefined,
      start: listFilter.timeRange ? listFilter.timeRange[0] : undefined,
      end: listFilter.timeRange ? listFilter.timeRange[1] : undefined,
      page: listFilter.page,
      pageSize: listFilter.pageSize,
    }
    const res = await reconciliationApi.listRecords(params)
    records.value = res.data.items
    listTotal.value = res.data.total
  } catch (e: unknown) {
    const msg = e instanceof Error ? e.message : '加载对账记录失败'
    message.error(msg)
  } finally {
    listLoading.value = false
  }
}

function onListSearch(): void {
  listFilter.page = 1
  loadRecords()
}

function openTriggerConfirm(): void {
  triggerConfirmVisible.value = true
}

async function onConfirmTrigger(): Promise<void> {
  // 防止对话框停留期间重复点击确认
  if (triggering.value) return
  if (!triggerForm.timeRange) {
    message.warning('请先选择时间范围')
    return
  }
  triggering.value = true
  try {
    const params = {
      reportType: triggerForm.reportType || undefined,
      start: triggerForm.timeRange[0],
      end: triggerForm.timeRange[1],
    }
    const res = await reconciliationApi.trigger(params)
    triggerConfirmVisible.value = false
    const result = res.data
    if (result.length === 1) {
      message.success('对账已完成')
    } else {
      message.info(`已对账全部报表类型，共 ${result.length} 条记录`)
    }
    // 刷新状态卡片 + 记录列表
    await Promise.all([loadStatus(), loadRecords()])
  } catch (e: unknown) {
    if (e instanceof BusinessError) message.error(e.message)
    else {
      const msg = e instanceof Error ? e.message : '触发对账失败'
      message.error(msg)
    }
  } finally {
    triggering.value = false
  }
}

function onCancelTrigger(): void {
  triggerConfirmVisible.value = false
}

function openDetail(record: ReconciliationRecordDto): void {
  currentRecord.value = record
  drawerVisible.value = true
}

function onPageChange(page: number, pageSize: number): void {
  listFilter.page = page
  listFilter.pageSize = pageSize
  loadRecords()
}

function onTriggerTimeRangeChange(val: [string, string]): void {
  triggerForm.timeRange = val
}
function onListTimeRangeChange(val: [string, string]): void {
  listFilter.timeRange = val
  onListSearch()
}

// 差异项 > 0 行高亮 className
function rowClassName(record: ReconciliationRecordDto): string {
  return record.discrepancyCount > 0 ? 'reconciliation-row-highlight' : ''
}

onMounted(() => {
  Promise.all([loadStatus(), loadRecords()])
})
</script>

<template>
  <div class="reconciliation">
    <div class="page-header">
      <div class="page-title">对账管理</div>
      <div class="page-desc">查看最近一次对账状态，手动触发按报表类型与时间范围的对账，查看历史对账记录与差异项，确保跨域统计指标一致。</div>
    </div>

    <!-- 状态卡片区 -->
    <a-row :gutter="24" class="status-cards">
      <a-col :xs="24" :sm="12" :xl="6">
        <a-card :loading="statusLoading" class="status-card">
          <a-statistic
            title="对账状态"
            :value="status ? statusLabel(status.status) : '尚未执行'"
            :value-style="{ color: status ? (status.isConsistent ? '#52C41A' : (status.status === 'Failed' ? '#FF4D4F' : '#FAAD14')) : '#8C8C8C' }"
          >
            <template #prefix>
              <CheckCircleOutlined v-if="status?.isConsistent" style="color: #52C41A" />
              <WarningOutlined v-else-if="status?.status === 'Discrepancy'" style="color: #FAAD14" />
              <ExclamationCircleOutlined v-else-if="status?.status === 'Failed'" style="color: #FF4D4F" />
            </template>
          </a-statistic>
        </a-card>
      </a-col>
      <a-col :xs="24" :sm="12" :xl="6">
        <a-card :loading="statusLoading" class="status-card">
          <a-statistic
            title="差异项数量"
            :value="status?.discrepancyCount ?? 0"
            :value-style="{ color: (status?.discrepancyCount ?? 0) > 0 ? '#FAAD14' : '#595959' }"
          />
        </a-card>
      </a-col>
      <a-col :xs="24" :sm="12" :xl="6">
        <a-card :loading="statusLoading" class="status-card">
          <a-statistic
            title="最近对账时间"
            :value="status?.reconciledAt ? dayjs(status.reconciledAt).format('MM-DD HH:mm') : '—'"
          />
        </a-card>
      </a-col>
      <a-col :xs="24" :sm="12" :xl="6">
        <a-card :loading="statusLoading" class="status-card">
          <a-statistic
            title="告警 / 修正"
            :value="status ? `${status.alertTriggered ? '是' : '否'} / ${status.correctionTriggered ? '是' : '否'}` : '—'"
            :value-style="{ color: status?.alertTriggered ? '#FF4D4F' : '#595959' }"
          />
        </a-card>
      </a-col>
    </a-row>

    <!-- 触发对账区 -->
    <a-card class="trigger-card">
      <div class="trigger-row">
        <a-select
          v-model:value="triggerForm.reportType"
          placeholder="报表类型"
          style="width: 200px"
          :options="reportTypeOptions"
        />
        <DateTimeRangePicker :value="triggerForm.timeRange ?? undefined" @change="onTriggerTimeRangeChange" />
        <PermissionGuard permission="reconciliation:trigger">
          <IdempotencyButton type="primary" :loading="triggering" @click="openTriggerConfirm">
            <PlayCircleOutlined />触发对账
          </IdempotencyButton>
        </PermissionGuard>
      </div>
    </a-card>

    <!-- 历史记录表格 -->
    <a-card title="对账历史记录" class="records-card">
      <div class="toolbar">
        <a-select
          v-model:value="listFilter.reportType"
          placeholder="报表类型"
          style="width: 200px"
          :options="reportTypeOptions"
          @change="onListSearch"
        />
        <DateTimeRangePicker :value="listFilter.timeRange ?? undefined" @change="onListTimeRangeChange" />
        <a-button type="primary" @click="onListSearch">筛选</a-button>
      </div>
      <a-table
        :columns="columns"
        :data-source="records"
        :loading="listLoading"
        row-key="recordId"
        size="middle"
        :scroll="{ x: 1100 }"
        :row-class-name="rowClassName"
        :pagination="{
          current: listFilter.page,
          pageSize: listFilter.pageSize,
          total: listTotal,
          showSizeChanger: true,
          onChange: onPageChange,
        }"
      >
        <template #emptyText>
          <EmptyState description="暂无对账记录" action-text="触发首次对账" @action="openTriggerConfirm" />
        </template>
        <template #bodyCell="{ column, record }">
          <template v-if="column.key === 'status'">
            <a-tag :color="statusTagType(record.status)">{{ statusLabel(record.status) }}</a-tag>
          </template>
          <template v-else-if="column.key === 'alertTriggered'">
            <a-tag :color="record.alertTriggered ? 'error' : 'default'">{{ record.alertTriggered ? '是' : '否' }}</a-tag>
          </template>
          <template v-else-if="column.key === 'correctionTriggered'">
            <a-tag :color="record.correctionTriggered ? 'warning' : 'default'">{{ record.correctionTriggered ? '是' : '否' }}</a-tag>
          </template>
          <template v-else-if="column.key === 'action-col'">
            <a-button type="link" size="small" @click="openDetail(record)">
              <EyeOutlined />详情
            </a-button>
          </template>
        </template>
      </a-table>
    </a-card>

    <!-- 触发对账确认 -->
    <ConfirmDialog
      :open="triggerConfirmVisible"
      :danger="false"
      title="确认触发对账"
      content="触发对账将重新计算指定报表类型的统计指标并与各域数据比对，可能耗时较长（视数据量而定）。是否继续？"
      @confirm="onConfirmTrigger"
      @cancel="onCancelTrigger"
    />

    <!-- 详情抽屉 -->
    <a-drawer
      v-model:open="drawerVisible"
      title="对账记录详情"
      placement="right"
      :width="720"
    >
      <template v-if="currentRecord">
        <a-descriptions :column="2" bordered size="small">
          <a-descriptions-item label="记录ID">{{ currentRecord.recordId }}</a-descriptions-item>
          <a-descriptions-item label="报表类型">{{ reportTypeLabel(currentRecord.reportType) }}</a-descriptions-item>
          <a-descriptions-item label="对账时间">{{ formatDateTime(currentRecord.reconciledAt) }}</a-descriptions-item>
          <a-descriptions-item label="状态">
            <a-tag :color="statusTagType(currentRecord.status)">{{ statusLabel(currentRecord.status) }}</a-tag>
          </a-descriptions-item>
          <a-descriptions-item label="差异项数">{{ currentRecord.discrepancyCount }}</a-descriptions-item>
          <a-descriptions-item label="告警">
            <a-tag :color="currentRecord.alertTriggered ? 'error' : 'default'">{{ currentRecord.alertTriggered ? '是' : '否' }}</a-tag>
          </a-descriptions-item>
          <a-descriptions-item label="修正">
            <a-tag :color="currentRecord.correctionTriggered ? 'warning' : 'default'">{{ currentRecord.correctionTriggered ? '是' : '否' }}</a-tag>
          </a-descriptions-item>
          <a-descriptions-item label="错误信息" :span="2">
            <span v-if="currentRecord.errorMessage" class="error-msg">{{ currentRecord.errorMessage }}</span>
            <span v-else>—</span>
          </a-descriptions-item>
        </a-descriptions>

        <div class="discrepancy-section">
          <div class="discrepancy-title">差异项明细（{{ currentRecord.discrepancies.length }} 项）</div>
          <a-table
            v-if="currentRecord.discrepancies.length > 0"
            :columns="[
              { title: '报表类型', dataIndex: 'reportType', key: 'reportType', customRender: ({ text }: { text: ReconciliationReportType }) => reportTypeLabel(text) },
              { title: '指标名', dataIndex: 'metricName', key: 'metricName' },
              { title: '期望值', dataIndex: 'expectedValue', key: 'expectedValue', align: 'right' as const },
              { title: '实际值', dataIndex: 'actualValue', key: 'actualValue', align: 'right' as const },
              { title: '差异值', dataIndex: 'diffValue', key: 'diffValue', align: 'right' as const },
            ]"
            :data-source="currentRecord.discrepancies"
            row-key="metricName"
            size="small"
            :pagination="false"
          />
          <EmptyState v-else description="无差异项，指标一致" />
        </div>
      </template>
    </a-drawer>
  </div>
</template>

<style scoped>
.reconciliation .page-header { background: var(--n1, #fff); border-radius: 8px; padding: 24px; margin-bottom: 16px; box-shadow: 0 1px 2px rgba(0,0,0,.03); }
.reconciliation .page-title { font-size: 20px; font-weight: 600; margin-bottom: 4px; }
.reconciliation .page-desc { color: #8C8C8C; }
.reconciliation .status-cards { margin-bottom: 16px; }
.reconciliation .status-card { border-radius: 8px; }
.reconciliation .trigger-card { margin-bottom: 16px; border-radius: 8px; }
.reconciliation .trigger-row { display: flex; gap: 12px; flex-wrap: wrap; align-items: center; }
.reconciliation .records-card { border-radius: 8px; }
.reconciliation .toolbar { display: flex; gap: 12px; margin-bottom: 16px; flex-wrap: wrap; align-items: center; }
.reconciliation .discrepancy-section { margin-top: 24px; }
.reconciliation .discrepancy-title { font-size: 14px; font-weight: 500; margin-bottom: 12px; color: #595959; }
.reconciliation .error-msg { color: #FF4D4F; font-size: 12px; word-break: break-all; }
.reconciliation :deep(.reconciliation-row-highlight) { background-color: #FFF7E6; }
.reconciliation :deep(.reconciliation-row-highlight:hover) > td { background-color: #FFE7BA !important; }
</style>
