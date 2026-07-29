<!-- web/system-admin/src/modules/07-monitoring/views/ServerMonitor.vue -->
<!-- 服务器监控：实时快照卡片 + CPU/内存/磁盘 IO 时序图 + 系统信息描述列表 -->
<!-- 自动刷新：5s 轮询 snapshot，ChartLine 追加新数据点（上限 300） -->
<script setup lang="ts">
import { ref, computed, onMounted, onBeforeUnmount } from 'vue'
import { message } from 'ant-design-vue'
import dayjs from 'dayjs'
import type { EChartsOption } from 'echarts'
import { serverMonitorApi } from '../api/server-monitor.api'
import type { ServerSnapshotDto, MetricPointDto } from '../types/server-monitor.dto'
import { StatisticCard, ChartLine } from '@/shared/components'
import { formatDateTime } from '@/shared/utils/format'

/** 字节 ↔ GB / MB 换算常量 */
const BYTES_PER_GB = 1024 * 1024 * 1024
const BYTES_PER_MB = 1024 * 1024

/** 图表数据点上限：超过后滚动剔除最早点 */
const MAX_POINTS = 300
/** 轮询间隔（毫秒） */
const POLL_INTERVAL_MS = 5000

/** 最新服务器快照（驱动顶部卡片 + 底部描述列表） */
const snapshot = ref<ServerSnapshotDto | null>(null)
/** 卡片加载态（首次拉取） */
const snapshotLoading = ref(false)
/** 图表加载态（首次拉取历史） */
const chartLoading = ref(true)

// ---- CPU 时序图数据 ----
const cpuXAxis = ref<string[]>([])
const cpuData = ref<number[]>([])

// ---- 内存时序图数据（单位 GB） ----
const memXAxis = ref<string[]>([])
const memUsedData = ref<number[]>([])
const memCachedData = ref<number[]>([])
const memFreeData = ref<number[]>([])

// ---- 磁盘 I/O 时序图数据（单位 MB/s） ----
const diskXAxis = ref<string[]>([])
const diskReadData = ref<number[]>([])
const diskWriteData = ref<number[]>([])

/** 轮询定时器句柄 */
let pollTimer: ReturnType<typeof setInterval> | null = null

// ==================== 顶部 StatisticCard 计算属性 ====================

/** CPU 使用率状态色：<60 success / <80 warning / >=80 danger */
const cpuStatus = computed<'success' | 'warning' | 'danger'>(() => {
  const v = snapshot.value?.cpuUsagePercent ?? 0
  if (v >= 80) return 'danger'
  if (v >= 60) return 'warning'
  return 'success'
})

const cpuUsage = computed(() => snapshot.value?.cpuUsagePercent ?? 0)

const memTotalGb = computed(() => (snapshot.value?.memoryTotalBytes ?? 0) / BYTES_PER_GB)

const memUsedGb = computed(() => (snapshot.value?.memoryUsedBytes ?? 0) / BYTES_PER_GB)

/** 已用内存百分比 */
const memUsedPercent = computed(() => {
  const s = snapshot.value
  if (!s || s.memoryTotalBytes === 0) return 0
  return (s.memoryUsedBytes / s.memoryTotalBytes) * 100
})

/** 已用内存后缀：GB · xx.x% */
const memUsedSuffix = computed(() => `GB · ${memUsedPercent.value.toFixed(1)}%`)

const diskTotalGb = computed(() => (snapshot.value?.diskTotalBytes ?? 0) / BYTES_PER_GB)

const diskUsedGb = computed(() => (snapshot.value?.diskUsedBytes ?? 0) / BYTES_PER_GB)

/** 磁盘已用百分比 */
const diskUsedPercent = computed(() => {
  const s = snapshot.value
  if (!s || s.diskTotalBytes === 0) return 0
  return (s.diskUsedBytes / s.diskTotalBytes) * 100
})

/** 磁盘已用后缀：GB · xx.x% */
const diskUsedSuffix = computed(() => `GB · ${diskUsedPercent.value.toFixed(1)}%`)

const loadAvg1 = computed(() => snapshot.value?.loadAvg1 ?? 0)
const loadAvg5 = computed(() => snapshot.value?.loadAvg5 ?? 0)
const loadAvg15 = computed(() => snapshot.value?.loadAvg15 ?? 0)
const loadSuffix = computed(() => `/${loadAvg5.value}/${loadAvg15.value}`)

// ==================== 中部 ChartLine 计算属性 ====================

/** CPU 使用率：单条折线 */
const cpuSeries = computed<EChartsOption['series']>(() => [
  { name: 'CPU 使用率', type: 'line' as const, smooth: true, data: cpuData.value },
])

/** 内存使用：已用 / 缓存 / 空闲，堆叠面积图 */
const memSeries = computed<EChartsOption['series']>(() => [
  { name: '已用', type: 'line' as const, areaStyle: {}, stack: 'mem', smooth: true, data: memUsedData.value },
  { name: '缓存', type: 'line' as const, areaStyle: {}, stack: 'mem', smooth: true, data: memCachedData.value },
  { name: '空闲', type: 'line' as const, areaStyle: {}, stack: 'mem', smooth: true, data: memFreeData.value },
])

/** 磁盘 I/O：读取 / 写入，双折线（MB/s） */
const diskSeries = computed<EChartsOption['series']>(() => [
  { name: '读取', type: 'line' as const, smooth: true, data: diskReadData.value },
  { name: '写入', type: 'line' as const, smooth: true, data: diskWriteData.value },
])

// ==================== 工具函数 ====================

/** 将 ISO 时间戳格式化为 HH:mm:ss，用于图表 X 轴标签 */
function formatChartTime(t: string): string {
  const d = dayjs(t)
  return d.isValid() ? d.format('HH:mm:ss') : t
}

/** 各图表数组超出上限时滚动剔除最早点，保持 X 轴与各序列长度对齐 */
function shiftIfNeeded(): void {
  if (cpuXAxis.value.length > MAX_POINTS) {
    cpuXAxis.value.shift()
    cpuData.value.shift()
  }
  if (memXAxis.value.length > MAX_POINTS) {
    memXAxis.value.shift()
    memUsedData.value.shift()
    memCachedData.value.shift()
    memFreeData.value.shift()
  }
  if (diskXAxis.value.length > MAX_POINTS) {
    diskXAxis.value.shift()
    diskReadData.value.shift()
    diskWriteData.value.shift()
  }
}

/**
 * 将快照数据追加到三条时序图
 *
 * - CPU：追加 cpuUsagePercent
 * - 内存：追加 used / cached / free（free = total - used - cached），单位 GB
 * - 磁盘 I/O：追加 read / write，单位 MB/s
 */
function appendSnapshotToCharts(s: ServerSnapshotDto): void {
  const t = formatChartTime(s.sampledAt)

  // CPU
  cpuXAxis.value.push(t)
  cpuData.value.push(Number(s.cpuUsagePercent.toFixed(2)))

  // 内存（GB）
  const usedGb = s.memoryUsedBytes / BYTES_PER_GB
  const cachedGb = s.memoryCachedBytes / BYTES_PER_GB
  const freeGb = (s.memoryTotalBytes - s.memoryUsedBytes - s.memoryCachedBytes) / BYTES_PER_GB
  memXAxis.value.push(t)
  memUsedData.value.push(Number(usedGb.toFixed(4)))
  memCachedData.value.push(Number(cachedGb.toFixed(4)))
  memFreeData.value.push(Number(freeGb.toFixed(4)))

  // 磁盘 I/O（MB/s）
  const readMbs = s.diskReadBytesPerSec / BYTES_PER_MB
  const writeMbs = s.diskWriteBytesPerSec / BYTES_PER_MB
  diskXAxis.value.push(t)
  diskReadData.value.push(Number(readMbs.toFixed(4)))
  diskWriteData.value.push(Number(writeMbs.toFixed(4)))

  shiftIfNeeded()
}

// ==================== 数据加载 ====================

/**
 * 初始化：并行拉取 snapshot + 3 条历史指标
 *
 * - snapshot 驱动顶部卡片与底部描述列表
 * - 历史 points 填充三条图表初始序列
 *   - CPU：v = 使用率（%）
 *   - memory：v = 已用内存字节（cached/free 历史不可得，cached 置 0，free 用 snapshot 总量估算）
 *   - disk-io：v = 读取字节/秒（write 历史不可得，置 0，由轮询补全）
 */
async function loadInitial(): Promise<void> {
  snapshotLoading.value = true
  chartLoading.value = true
  try {
    const [s, cpuHistory, memHistory, diskHistory] = await Promise.all([
      serverMonitorApi.snapshot(),
      serverMonitorApi.history('cpu'),
      serverMonitorApi.history('memory'),
      serverMonitorApi.history('disk-io'),
    ])

    snapshot.value = s

    // CPU 历史
    cpuXAxis.value = cpuHistory.points.map((p: MetricPointDto) => formatChartTime(p.timestamp))
    cpuData.value = cpuHistory.points.map((p: MetricPointDto) => Number(p.value.toFixed(2)))

    // 内存历史：v 为已用字节，cached 历史不可得置 0，free 用当前总量估算
    const memTotalBytes = s.memoryTotalBytes
    memXAxis.value = memHistory.points.map((p: MetricPointDto) => formatChartTime(p.timestamp))
    memUsedData.value = memHistory.points.map((p: MetricPointDto) => Number((p.value / BYTES_PER_GB).toFixed(4)))
    memCachedData.value = memHistory.points.map(() => 0)
    memFreeData.value = memHistory.points.map((p: MetricPointDto) =>
      memTotalBytes > 0 ? Number(((memTotalBytes - p.value) / BYTES_PER_GB).toFixed(4)) : 0,
    )

    // 磁盘 I/O 历史：v 为读取字节/秒，write 历史不可得置 0
    diskXAxis.value = diskHistory.points.map((p: MetricPointDto) => formatChartTime(p.timestamp))
    diskReadData.value = diskHistory.points.map((p: MetricPointDto) => Number((p.value / BYTES_PER_MB).toFixed(4)))
    diskWriteData.value = diskHistory.points.map(() => 0)
  } catch {
    message.error('初始化监控数据失败，请检查后端服务状态')
  } finally {
    snapshotLoading.value = false
    chartLoading.value = false
  }
}

/**
 * 轮询单次：拉取 snapshot → 更新卡片 + 追加图表点
 *
 * 轮询期间失败不弹错误提示，避免每 5s 刷屏；卡片保持上次快照值。
 */
async function pollSnapshot(): Promise<void> {
  try {
    const s = await serverMonitorApi.snapshot()
    snapshot.value = s
    appendSnapshotToCharts(s)
  } catch {
    // 轮询失败静默处理，等待下次重试
  }
}

function startPolling(): void {
  stopPolling()
  pollTimer = setInterval(() => {
    void pollSnapshot()
  }, POLL_INTERVAL_MS)
}

function stopPolling(): void {
  if (pollTimer !== null) {
    clearInterval(pollTimer)
    pollTimer = null
  }
}

onMounted(async () => {
  await loadInitial()
  startPolling()
})

onBeforeUnmount(() => {
  stopPolling()
})
</script>

<template>
  <div class="server-monitor">
    <!-- 区域 A：顶部 6 个指标卡片 -->
    <a-row :gutter="[16, 16]" class="stat-row">
      <a-col :xs="24" :sm="12" :lg="8" :xl="4">
        <StatisticCard
          title="CPU 使用率"
          :value="cpuUsage"
          unit="%"
          :precision="2"
          :status="cpuStatus"
          :loading="snapshotLoading"
        />
      </a-col>
      <a-col :xs="24" :sm="12" :lg="8" :xl="4">
        <StatisticCard
          title="总内存"
          :value="memTotalGb"
          unit="GB"
          :precision="2"
          :loading="snapshotLoading"
        />
      </a-col>
      <a-col :xs="24" :sm="12" :lg="8" :xl="4">
        <StatisticCard
          title="已用内存"
          :value="memUsedGb"
          :precision="2"
          :suffix="memUsedSuffix"
          :loading="snapshotLoading"
        />
      </a-col>
      <a-col :xs="24" :sm="12" :lg="8" :xl="4">
        <StatisticCard
          title="磁盘总量"
          :value="diskTotalGb"
          unit="GB"
          :precision="2"
          :loading="snapshotLoading"
        />
      </a-col>
      <a-col :xs="24" :sm="12" :lg="8" :xl="4">
        <StatisticCard
          title="磁盘已用"
          :value="diskUsedGb"
          :precision="2"
          :suffix="diskUsedSuffix"
          :loading="snapshotLoading"
        />
      </a-col>
      <a-col :xs="24" :sm="12" :lg="8" :xl="4">
        <StatisticCard
          title="系统负载"
          :value="loadAvg1"
          :precision="2"
          :suffix="loadSuffix"
          :loading="snapshotLoading"
        />
      </a-col>
    </a-row>

    <!-- 区域 B：三条时序图 -->
    <a-row :gutter="[16, 16]" class="chart-row">
      <a-col :xs="24" :xl="8">
        <a-card title="CPU 使用率（近 5 分钟）" :bordered="false" size="small">
          <ChartLine :series="cpuSeries" :x-axis="cpuXAxis" :height="240" :loading="chartLoading" />
        </a-card>
      </a-col>
      <a-col :xs="24" :xl="8">
        <a-card title="内存使用（已用 / 缓存 / 空闲）" :bordered="false" size="small">
          <ChartLine :series="memSeries" :x-axis="memXAxis" :height="240" :loading="chartLoading" />
        </a-card>
      </a-col>
      <a-col :xs="24" :xl="8">
        <a-card title="磁盘 I/O（读取 / 写入 MB/s）" :bordered="false" size="small">
          <ChartLine :series="diskSeries" :x-axis="diskXAxis" :height="240" :loading="chartLoading" />
        </a-card>
      </a-col>
    </a-row>

    <!-- 区域 C：系统信息描述列表 -->
    <a-card title="系统信息" :bordered="false" size="small" class="info-card">
      <a-descriptions :column="{ xs: 1, sm: 2, lg: 3 }" bordered size="small">
        <a-descriptions-item label="主机名">{{ snapshot?.hostname || '—' }}</a-descriptions-item>
        <a-descriptions-item label="操作系统">{{ snapshot?.os || '—' }}</a-descriptions-item>
        <a-descriptions-item label="内核版本">{{ snapshot?.kernelVersion || '—' }}</a-descriptions-item>
        <a-descriptions-item label="CPU 型号">{{ snapshot?.cpuModel || '—' }}</a-descriptions-item>
        <a-descriptions-item label="CPU 核心数">{{ snapshot?.cpuCores ?? '—' }}</a-descriptions-item>
        <a-descriptions-item label="进程数">{{ snapshot?.processCount ?? '—' }}</a-descriptions-item>
        <a-descriptions-item label="启动时间">
          {{ snapshot?.bootTime ? formatDateTime(snapshot.bootTime) : '—' }}
        </a-descriptions-item>
        <a-descriptions-item label=".NET 运行时">{{ snapshot?.dotnetRuntimeVersion || '—' }}</a-descriptions-item>
        <a-descriptions-item label="GC 累计回收次数">{{ snapshot?.gcTotalCollections ?? '—' }}</a-descriptions-item>
      </a-descriptions>
    </a-card>
  </div>
</template>

<style scoped>
.server-monitor {
  display: flex;
  flex-direction: column;
  gap: 16px;
}
.stat-row {
  margin-bottom: 0;
}
.chart-row {
  margin-bottom: 0;
}
.info-card :deep(.ant-card-body) {
  padding: 16px;
}
</style>
