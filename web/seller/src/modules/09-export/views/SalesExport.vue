<script setup lang="ts">
import { ref, reactive, computed, watch, onMounted, onUnmounted } from 'vue'
import {
  Breadcrumb,
  BreadcrumbItem,
  Row,
  Col,
  Card,
  Form,
  FormItem,
  Select,
  RadioGroup,
  RadioButton,
  RangePicker,
  Table,
  Tag,
  Button,
  Skeleton,
  Tooltip,
  Space,
  message,
} from 'ant-design-vue'
import type { TableColumnsType } from 'ant-design-vue'
import type { Dayjs } from 'dayjs'
import { exportApi } from '../api/export.api'
import type {
  ReportType,
  ExportFormat,
  ExportTaskStatus,
  ExportTaskDto,
  CreateExportTaskDto,
} from '../types/export.dto'
import { http } from '@/shared/http'
import { IdempotencyButton, EmptyState } from '@/shared/components'
import { logger } from '@/shared/utils/logger'
import { formatDateTime } from '@/shared/utils/format'

/**
 * 销售报表导出页
 *
 * 路由 /export/sales，权限 export:sales
 * 3 个 API 端点全部 BE-3 标记：
 * - 提交新建任务 → createTask → mock 501 → message.warning('后端接口未就绪（BE-3）')
 * - 下载已完成任务 → getDownloadUrl + http.get → mock 501 → message.warning
 * - 历史任务列表 → listTasks → mock 200 空列表 → EmptyState
 *
 * 轮询：有 Processing 状态任务时每 3 秒刷新列表（当前 mock 返回空列表，不触发）。
 */

const loading = ref(false)
const submitting = ref(false)
const tasks = ref<ExportTaskDto[]>([])

const form = reactive<{
  reportType: ReportType
  dateRange: [Dayjs, Dayjs] | null
  format: ExportFormat
}>({
  reportType: 'SalesSummary',
  dateRange: null,
  format: 'Excel',
})

const reportTypeOptions: Array<{ label: string; value: ReportType }> = [
  { label: '销售汇总', value: 'SalesSummary' },
  { label: '订单明细', value: 'OrderDetail' },
  { label: '商品销量', value: 'ProductSales' },
]

const reportTypeLabels: Record<ReportType, string> = {
  SalesSummary: '销售汇总',
  OrderDetail: '订单明细',
  ProductSales: '商品销量',
}

const statusMeta: Record<ExportTaskStatus, { color: string; label: string }> = {
  Processing: { color: 'processing', label: '处理中' },
  Completed: { color: 'success', label: '已完成' },
  Failed: { color: 'error', label: '失败' },
}

const columns: TableColumnsType = [
  { title: '类型', dataIndex: 'reportType', key: 'reportType', width: 120 },
  { title: '时间范围', key: 'range', width: 200 },
  { title: '格式', dataIndex: 'format', key: 'format', width: 90 },
  { title: '状态', dataIndex: 'status', key: 'status', width: 110 },
  { title: '记录数', dataIndex: 'recordCount', key: 'recordCount', width: 100 },
  { title: '创建时间', dataIndex: 'createdAt', key: 'createdAt', width: 180 },
  { title: '操作', key: 'action', width: 140 },
]

const hasProcessing = computed(() =>
  tasks.value.some((t) => t.status === 'Processing'),
)

let pollTimer: ReturnType<typeof setTimeout> | null = null

function schedulePoll(): void {
  if (pollTimer !== null) return
  pollTimer = setTimeout(async () => {
    pollTimer = null
    await loadTasks(true)
    if (hasProcessing.value) schedulePoll()
  }, 3000)
}

function stopPoll(): void {
  if (pollTimer !== null) {
    clearTimeout(pollTimer)
    pollTimer = null
  }
}

watch(hasProcessing, (v) => {
  if (v) schedulePoll()
  else stopPoll()
})

async function loadTasks(silent = false): Promise<void> {
  if (!silent) loading.value = true
  try {
    const res = await exportApi.listTasks({ page: 1, pageSize: 50 })
    tasks.value = res.items
  } catch (e) {
    logger.error('加载导出任务列表失败', e)
    if (!silent) message.error('加载导出任务列表失败')
  } finally {
    if (!silent) loading.value = false
  }
}

function onDateRangeChange(dates: [Dayjs, Dayjs] | null): void {
  form.dateRange = dates
  if (dates && dates[0] && dates[1]) {
    const diffDays = dates[1].diff(dates[0], 'day')
    if (diffDays > 90) {
      message.error('时间范围不能超过 90 天')
      form.dateRange = null
    }
    if (diffDays < 0) {
      message.error('结束时间不能早于开始时间')
      form.dateRange = null
    }
  }
}

function buildBody(): CreateExportTaskDto | null {
  if (!form.dateRange || !form.dateRange[0] || !form.dateRange[1]) {
    return null
  }
  return {
    reportType: form.reportType,
    startDate: form.dateRange[0].format('YYYY-MM-DD'),
    endDate: form.dateRange[1].format('YYYY-MM-DD'),
    format: form.format,
  }
}

async function onSubmit(): Promise<void> {
  const body = buildBody()
  if (!body) {
    message.warning('请选择时间范围')
    return
  }
  submitting.value = true
  try {
    await exportApi.createTask(body)
    // BE-3 就绪后：创建成功，刷新列表
    await loadTasks()
    message.success('导出任务已创建，请稍后在右侧列表查看进度')
  } catch (e) {
    logger.warn('创建导出任务失败（BE-3）', e)
    message.warning('后端接口未就绪（BE-3）')
  } finally {
    submitting.value = false
  }
}

async function onDownload(task: ExportTaskDto): Promise<void> {
  const fullUrl = exportApi.getDownloadUrl(task.id)
  // axios baseURL=/api，故去掉 /api 前缀再交给 http.get，以命中 mock 拦截
  const axiosPath = fullUrl.replace(/^\/api/, '')
  try {
    const res = await http.get<Blob>(axiosPath, { responseType: 'blob' })
    // BE-3 就绪后：用 Blob 触发浏览器下载
    const blobUrl = URL.createObjectURL(res.data)
    const a = document.createElement('a')
    a.href = blobUrl
    a.download = `${task.reportType}-${task.id}.${
      task.format === 'Excel' ? 'xlsx' : 'csv'
    }`
    document.body.appendChild(a)
    a.click()
    document.body.removeChild(a)
    URL.revokeObjectURL(blobUrl)
  } catch (e) {
    logger.warn('下载导出文件失败（BE-3）', e)
    message.warning('后端接口未就绪（BE-3）')
  }
}

async function onRetry(task: ExportTaskDto): Promise<void> {
  submitting.value = true
  try {
    await exportApi.createTask({
      reportType: task.reportType,
      startDate: task.startDate,
      endDate: task.endDate,
      format: task.format,
    })
    await loadTasks()
    message.success('重试任务已创建')
  } catch (e) {
    logger.warn('重试导出任务失败（BE-3）', e)
    message.warning('后端接口未就绪（BE-3）')
  } finally {
    submitting.value = false
  }
}

onMounted(() => {
  void loadTasks()
})

onUnmounted(() => {
  stopPoll()
})
</script>

<template>
  <div class="sales-export-page">
    <Breadcrumb class="sales-export-breadcrumb">
      <BreadcrumbItem>数据导出</BreadcrumbItem>
      <BreadcrumbItem>销售报表</BreadcrumbItem>
    </Breadcrumb>

    <Row :gutter="16" class="sales-export-row">
      <!-- 左栏：新建导出任务 -->
      <Col :span="8">
        <Card class="sales-export-card" :bordered="true">
          <template #title>
            <span class="sales-export-card-title">新建导出任务</span>
          </template>
          <Form layout="vertical" :label-col="{ style: { width: '100px' } }">
            <FormItem label="报表类型" required>
              <Select
                v-model:value="form.reportType"
                :options="reportTypeOptions"
                placeholder="请选择报表类型"
              />
            </FormItem>
            <FormItem label="时间范围" required>
              <RangePicker
                :value="form.dateRange"
                style="width: 100%"
                :allow-clear="true"
                @change="onDateRangeChange"
              />
              <div class="sales-export-hint">单次导出时间范围不能超过 90 天</div>
            </FormItem>
            <FormItem label="导出格式" required>
              <RadioGroup v-model:value="form.format">
                <RadioButton value="Excel">Excel</RadioButton>
                <RadioButton value="CSV">CSV</RadioButton>
              </RadioGroup>
            </FormItem>
            <FormItem>
              <IdempotencyButton
                :loading="submitting"
                block
                @click="onSubmit"
              >
                创建导出任务
              </IdempotencyButton>
            </FormItem>
          </Form>
          <div class="sales-export-be3-tip">
            后端导出接口未就绪（BE-3），提交后将提示"后端接口未就绪"。
          </div>
        </Card>
      </Col>

      <!-- 右栏：历史任务列表 -->
      <Col :span="16">
        <Card class="sales-export-card" :bordered="true">
          <template #title>
            <span class="sales-export-card-title">历史任务列表</span>
          </template>
          <template #extra>
            <Button size="small" @click="loadTasks()">刷新</Button>
          </template>

          <Skeleton v-if="loading" active :paragraph="{ rows: 5 }" />
          <EmptyState
            v-else-if="tasks.length === 0"
            description="暂无导出任务"
          />
          <Table
            v-else
            :columns="columns"
            :data-source="tasks"
            row-key="id"
            :pagination="false"
            size="middle"
          >
            <template #bodyCell="{ column, record }">
              <template v-if="column.key === 'reportType'">
                {{ reportTypeLabels[record.reportType as ReportType] || record.reportType }}
              </template>
              <template v-else-if="column.key === 'range'">
                {{ record.startDate }} ~ {{ record.endDate }}
              </template>
              <template v-else-if="column.key === 'format'">
                {{ record.format }}
              </template>
              <template v-else-if="column.key === 'status'">
                <Tag :color="statusMeta[record.status as ExportTaskStatus].color">
                  {{ statusMeta[record.status as ExportTaskStatus].label }}
                </Tag>
              </template>
              <template v-else-if="column.key === 'recordCount'">
                {{ record.recordCount ?? '—' }}
              </template>
              <template v-else-if="column.key === 'createdAt'">
                {{ formatDateTime(record.createdAt) }}
              </template>
              <template v-else-if="column.key === 'action'">
                <Space>
                  <Button
                    v-if="record.status === 'Completed'"
                    type="link"
                    size="small"
                    @click="onDownload(record as ExportTaskDto)"
                  >
                    下载
                  </Button>
                  <Tooltip
                    v-if="record.status === 'Failed'"
                    :title="record.errorMessage || '任务失败，可重试'"
                  >
                    <Button
                      type="link"
                      size="small"
                      :loading="submitting"
                      @click="onRetry(record as ExportTaskDto)"
                    >
                      重试
                    </Button>
                  </Tooltip>
                  <span
                    v-if="record.status === 'Processing'"
                    class="sales-export-processing-text"
                  >
                    处理中…
                  </span>
                </Space>
              </template>
            </template>
          </Table>
        </Card>
      </Col>
    </Row>
  </div>
</template>

<style scoped>
.sales-export-page {
  display: flex;
  flex-direction: column;
  gap: 16px;
}
.sales-export-breadcrumb {
  font-size: 14px;
}
.sales-export-row {
  align-items: stretch;
}
.sales-export-card {
  border-radius: 8px;
  height: 100%;
}
.sales-export-card-title {
  font-size: 15px;
  font-weight: 500;
}
.sales-export-hint {
  font-size: 12px;
  color: #8c8c8c;
  margin-top: 4px;
}
.sales-export-be3-tip {
  margin-top: 12px;
  padding: 8px 12px;
  background: #fffbe6;
  border: 1px solid #ffe58f;
  border-radius: 6px;
  font-size: 12px;
  color: #ad6800;
  line-height: 1.6;
}
.sales-export-processing-text {
  font-size: 12px;
  color: #8c8c8c;
}
</style>
