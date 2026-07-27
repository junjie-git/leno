<!-- web/system-admin/src/modules/05-audit/views/LoginLogs.vue -->
<!-- 登录日志：用户名/结果/时间区间筛选 + 主表格 + 详情抽屉 + 导出 CSV，严格只读 -->
<script setup lang="ts">
import { onMounted, reactive, ref } from 'vue'
import { message } from 'ant-design-vue'
import type { TableColumnsType } from 'ant-design-vue'
import { DownloadOutlined, EyeOutlined } from '@ant-design/icons-vue'
import dayjs from 'dayjs'
import { loginLogsApi } from '../api/login-logs.api'
import type { LoginLogDto, LoginLogQueryDto, LoginResult } from '../types/login-log.dto'
import { StatusTag, DateTimeRangePicker, EmptyState } from '@/shared/components'
import { formatDateTime } from '@/shared/utils/format'

interface FilterState {
  username: string
  result: LoginResult | ''
  timeRange: [string, string] | null
}

/** 默认近 24 小时（ISO 8601 UTC 字符串，与后端约定一致） */
function defaultRange(): [string, string] {
  return [
    dayjs().subtract(24, 'hour').toISOString(),
    dayjs().toISOString(),
  ]
}

const filters = reactive<FilterState>({
  username: '',
  result: '',
  timeRange: defaultRange(),
})

const resultOptions: { label: string; value: LoginResult | '' }[] = [
  { label: '全部', value: '' },
  { label: '成功', value: 'Success' },
  { label: '失败', value: 'Failed' },
]

const columns: TableColumnsType = [
  { title: '登录时间', key: 'loginAt', width: 180 },
  { title: '用户名', dataIndex: 'username', key: 'username', width: 140 },
  { title: 'IP 地址', dataIndex: 'ipAddress', key: 'ipAddress', width: 140 },
  { title: '地理位置', dataIndex: 'geoLocation', key: 'geoLocation', width: 160, ellipsis: true },
  { title: '浏览器', dataIndex: 'browser', key: 'browser', width: 120, ellipsis: true },
  { title: '操作系统', dataIndex: 'os', key: 'os', width: 120, ellipsis: true },
  { title: '结果', key: 'result', width: 90 },
  { title: '失败原因', dataIndex: 'failureReason', key: 'failureReason', width: 180, ellipsis: true },
  { title: '耗时(ms)', dataIndex: 'durationMs', key: 'durationMs', width: 110, align: 'right' },
  { title: '操作', key: 'action', width: 90, fixed: 'right' },
]

const tableData = ref<LoginLogDto[]>([])
const loading = ref(false)
const exporting = ref(false)

const pagination = reactive({
  current: 1,
  pageSize: 20,
  total: 0,
  showSizeChanger: true,
  pageSizeOptions: ['10', '20', '50', '100'],
  showTotal: (total: number) => `共 ${total} 条`,
})

/** 组装列表查询参数 */
function buildQueryParams(): LoginLogQueryDto {
  const params: LoginLogQueryDto = {
    page: pagination.current,
    pageSize: pagination.pageSize,
  }
  const username = filters.username.trim()
  if (username) params.username = username
  if (filters.result) params.result = filters.result
  if (filters.timeRange) {
    params.loginAtFrom = filters.timeRange[0]
    params.loginAtTo = filters.timeRange[1]
  }
  return params
}

/** 拉取登录日志列表 */
async function fetchList(): Promise<void> {
  loading.value = true
  try {
    const res = await loginLogsApi.list(buildQueryParams())
    tableData.value = res.items
    pagination.total = res.total
  } catch (e: unknown) {
    const msg = e instanceof Error ? e.message : '加载登录日志失败'
    message.error(msg)
  } finally {
    loading.value = false
  }
}

/** 点击查询：回到第一页后拉取 */
function onQuery(): void {
  pagination.current = 1
  fetchList()
}

/** 重置筛选条件并重新查询 */
function onReset(): void {
  filters.username = ''
  filters.result = ''
  filters.timeRange = defaultRange()
  onQuery()
}

/** 时间范围变更回调（DateTimeRangePicker 仅 emit change） */
function onTimeRangeChange(value: [string, string]): void {
  filters.timeRange = value
}

/** 表格分页/每页条数变更 */
function onTableChange(pag: { current: number; pageSize: number }): void {
  pagination.current = pag.current
  pagination.pageSize = pag.pageSize
  fetchList()
}

// 详情抽屉
const drawerOpen = ref(false)
const detailLoading = ref(false)
const detail = ref<LoginLogDto | null>(null)

/** 打开详情抽屉并拉取完整记录 */
async function onView(record: LoginLogDto): Promise<void> {
  drawerOpen.value = true
  detailLoading.value = true
  detail.value = null
  try {
    detail.value = await loginLogsApi.get(record.id)
  } catch (e: unknown) {
    const msg = e instanceof Error ? e.message : '加载登录日志详情失败'
    message.error(msg)
    drawerOpen.value = false
  } finally {
    detailLoading.value = false
  }
}

/** 导出 CSV：调用后端导出接口拿到 CSV 字符串，前端生成 Blob 并触发下载 */
async function onExport(): Promise<void> {
  exporting.value = true
  try {
    const csv = await loginLogsApi.exportCsv(buildQueryParams())
    // 加 UTF-8 BOM，保证 Excel 正确识别中文
    const bom = '\uFEFF'
    const blob = new Blob([bom + csv], { type: 'text/csv;charset=utf-8;' })
    const filename = `login-logs-${dayjs().format('YYYYMMDD-HHmmss')}.csv`
    const url = URL.createObjectURL(blob)
    const link = document.createElement('a')
    link.href = url
    link.download = filename
    document.body.appendChild(link)
    link.click()
    document.body.removeChild(link)
    URL.revokeObjectURL(url)
    message.success(`已导出 ${filename}`)
  } catch (e: unknown) {
    const msg = e instanceof Error ? e.message : '导出失败，请缩小时间范围后重试'
    message.error(msg)
  } finally {
    exporting.value = false
  }
}

onMounted(() => {
  fetchList()
})
</script>

<template>
  <div class="login-logs">
    <!-- 区域 A：筛选条 -->
    <a-card :bordered="false" class="filter-card">
      <a-form layout="inline">
        <a-form-item label="用户名">
          <a-input-search
            v-model:value="filters.username"
            placeholder="输入用户名"
            allow-clear
            style="width: 200px"
            @search="onQuery"
          />
        </a-form-item>
        <a-form-item label="登录结果">
          <a-select
            v-model:value="filters.result"
            placeholder="全部"
            style="width: 140px"
            :options="resultOptions"
          />
        </a-form-item>
        <a-form-item label="时间范围">
          <DateTimeRangePicker
            :value="filters.timeRange ?? undefined"
            :show-time="true"
            @change="onTimeRangeChange"
          />
        </a-form-item>
        <a-form-item>
          <a-button type="primary" @click="onQuery">查询</a-button>
          <a-button style="margin-left: 8px" @click="onReset">重置</a-button>
          <a-button
            style="margin-left: 8px"
            :loading="exporting"
            @click="onExport"
          >
            <DownloadOutlined />导出 CSV
          </a-button>
        </a-form-item>
      </a-form>
    </a-card>

    <!-- 区域 B：主表格 -->
    <a-card :bordered="false" class="table-card">
      <a-table
        :columns="columns"
        :data-source="tableData"
        :loading="loading"
        :pagination="pagination"
        row-key="id"
        :scroll="{ x: 1300 }"
        @change="onTableChange"
      >
        <template #emptyText>
          <EmptyState description="暂无登录日志" action-text="清空筛选条件" @action="onReset" />
        </template>
        <template #bodyCell="{ column, record }">
          <template v-if="column.key === 'loginAt'">
            {{ formatDateTime(record.loginAt) }}
          </template>
          <template v-else-if="column.key === 'result'">
            <StatusTag type="loginResult" :status="record.result" />
          </template>
          <template v-else-if="column.key === 'failureReason'">
            {{ record.failureReason || '—' }}
          </template>
          <template v-else-if="column.key === 'action'">
            <a-button type="link" size="small" @click="onView(record)">
              <EyeOutlined />详情
            </a-button>
          </template>
        </template>
      </a-table>
    </a-card>

    <!-- 区域 C：详情抽屉 -->
    <a-drawer
      v-model:open="drawerOpen"
      title="登录日志详情"
      placement="right"
      width="600"
      :destroy-on-close="true"
    >
      <a-spin :spinning="detailLoading">
        <a-descriptions v-if="detail" :column="1" bordered size="small">
          <a-descriptions-item label="登录 ID">{{ detail.id }}</a-descriptions-item>
          <a-descriptions-item label="用户名">{{ detail.username }}</a-descriptions-item>
          <a-descriptions-item label="IP 地址">{{ detail.ipAddress }}</a-descriptions-item>
          <a-descriptions-item label="地理位置">{{ detail.geoLocation }}</a-descriptions-item>
          <a-descriptions-item label="浏览器">{{ detail.browser }}</a-descriptions-item>
          <a-descriptions-item label="操作系统">{{ detail.os }}</a-descriptions-item>
          <a-descriptions-item label="登录结果">
            <StatusTag type="loginResult" :status="detail.result" />
          </a-descriptions-item>
          <a-descriptions-item label="失败原因">{{ detail.failureReason || '—' }}</a-descriptions-item>
          <a-descriptions-item label="耗时">{{ detail.durationMs }} ms</a-descriptions-item>
          <a-descriptions-item label="登录时间">{{ formatDateTime(detail.loginAt) }}</a-descriptions-item>
          <a-descriptions-item label="User-Agent">
            <span class="ua-text">{{ detail.userAgent }}</span>
          </a-descriptions-item>
          <a-descriptions-item label="设备指纹">
            <code class="mono-text">{{ detail.deviceFingerprint }}</code>
          </a-descriptions-item>
          <a-descriptions-item label="Referer URL">{{ detail.refererUrl || '—' }}</a-descriptions-item>
          <a-descriptions-item label="TraceId">
            <code class="mono-text">{{ detail.traceId }}</code>
          </a-descriptions-item>
        </a-descriptions>
        <EmptyState v-else-if="!detailLoading" description="登录日志条目不存在" />
      </a-spin>
    </a-drawer>
  </div>
</template>

<style scoped>
.login-logs {
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
.ua-text {
  word-break: break-all;
  white-space: pre-wrap;
}
.mono-text {
  font-family: 'SF Mono', 'Cascadia Code', Consolas, monospace;
  font-size: 12px;
  word-break: break-all;
}
</style>
