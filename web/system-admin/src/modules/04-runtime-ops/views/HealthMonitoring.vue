<!-- web/system-admin/src/modules/04-runtime-ops/views/HealthMonitoring.vue -->
<!-- 健康监控：整体状态条 + 模块网格 + 详情抽屉 + 30s 轮询 -->
<script setup lang="ts">
import { ref, computed, onMounted, onBeforeUnmount } from 'vue'
import { message, notification } from 'ant-design-vue'
import {
  CheckCircleFilled, ExclamationCircleFilled, CloseCircleFilled, ReloadOutlined,
} from '@ant-design/icons-vue'
import { healthApi } from '../api/health.api'
import type {
  HealthAggregationResultDto,
  ModuleHealthDto,
  DependencyStatus,
} from '../types/health.dto'
import StatusTag from '@/shared/components/StatusTag.vue'
import EmptyState from '@/shared/components/EmptyState.vue'
import { BusinessError } from '@/shared/http/errors'

const loading = ref(false)
const aggregated = ref<HealthAggregationResultDto | null>(null)
const modules = ref<ModuleHealthDto[]>([])
const detailVisible = ref(false)
const detail = ref<ModuleHealthDto | null>(null)

let pollTimer: ReturnType<typeof setInterval> | null = null
let firstLoad = true

const sortedModules = computed(() => {
  const order: Record<DependencyStatus, number> = { Unhealthy: 0, Degraded: 1, Healthy: 2 }
  return [...modules.value].sort((a, b) => order[a.status] - order[b.status])
})

function countUnhealthy(m: ModuleHealthDto): number {
  return m.dependencies.filter((d) => d.status !== 'Healthy').length
}

async function loadAll() {
  loading.value = true
  try {
    const [agg, mods] = await Promise.all([healthApi.getAggregated(), healthApi.getModules()])
    aggregated.value = agg
    modules.value = mods
    if (firstLoad) {
      firstLoad = false
      const unhealthy = mods.filter((m) => m.status === 'Unhealthy')
      if (unhealthy.length > 0) {
        notification.error({
          message: '检测到不健康模块',
          description: `${unhealthy.map((m) => m.moduleName).join('、')} 处于不健康状态，请检查依赖项。`,
          duration: 5,
        })
      }
    }
  } catch (e) {
    if (e instanceof BusinessError) message.error(e.message)
    else message.error('健康检查失败')
  } finally {
    loading.value = false
  }
}

function openDetail(m: ModuleHealthDto) {
  detail.value = m
  detailVisible.value = true
}

function statusColor(s: DependencyStatus): string {
  return s === 'Healthy' ? '#52C41A' : s === 'Degraded' ? '#FAAD14' : '#FF4D4F'
}

onMounted(() => {
  loadAll()
  pollTimer = setInterval(loadAll, 30_000)
})
onBeforeUnmount(() => {
  if (pollTimer) clearInterval(pollTimer)
})
</script>

<template>
  <div class="runtime-ops-health">
    <div class="page-header">
      <div class="page-title">健康监控</div>
      <div class="page-desc">聚合各模块 /health 端点状态，查看整体健康与各模块依赖项（DB/Redis/ES/MQ/支付渠道/通知渠道）明细。每 30s 自动刷新。</div>
    </div>

    <a-skeleton :loading="loading && !aggregated" active>
      <a-alert
        v-if="aggregated"
        :type="aggregated.overallStatus === 'Healthy' ? 'success' : aggregated.overallStatus === 'Degraded' ? 'warning' : 'error'"
        show-icon
        style="margin-bottom: 16px"
      >
        <template #message>
          <span style="font-weight: 600">
            整体状态：
            <StatusTag type="health" :status="aggregated.overallStatus" />
            <span style="margin-left: 16px; color: #8C8C8C; font-weight: normal">
              检查时间 {{ aggregated.checkedAt }}
            </span>
          </span>
        </template>
        <template #action>
          <a-button size="small" type="primary" @click="loadAll">
            <ReloadOutlined />立即检查
          </a-button>
        </template>
      </a-alert>
    </a-skeleton>

    <a-row v-if="sortedModules.length > 0" :gutter="[16, 16]">
      <a-col v-for="m in sortedModules" :key="m.moduleName" :xs="24" :sm="12" :lg="6">
        <a-card
          hoverable
          size="small"
          :body-style="{ padding: '16px' }"
          :style="{ borderColor: statusColor(m.status), borderWidth: '1px' }"
          @click="openDetail(m)"
        >
          <div class="module-card">
            <div class="module-name">
              <component
                :is="m.status === 'Healthy' ? CheckCircleFilled : m.status === 'Degraded' ? ExclamationCircleFilled : CloseCircleFilled"
                :style="{ color: statusColor(m.status), fontSize: '20px' }"
              />
              <span style="margin-left: 8px">{{ m.moduleName }}</span>
            </div>
            <div class="module-status">
              <StatusTag type="health" :status="m.status" />
              <span style="margin-left: 8px; font-size: 12px; color: #8C8C8C">{{ m.latencyMs }}ms</span>
            </div>
            <div class="module-meta">
              <span>{{ m.dependencies.length }} 依赖</span>
              <span v-if="countUnhealthy(m) > 0" style="color: #FF4D4F; margin-left: 8px">{{ countUnhealthy(m) }} 不健康</span>
            </div>
          </div>
        </a-card>
      </a-col>
    </a-row>
    <EmptyState
      v-else-if="!loading"
      description="暂无健康数据，请稍后重试"
      action-text="立即检查"
      @action="loadAll"
    />

    <a-drawer
      v-model:open="detailVisible"
      :title="detail ? `模块详情 - ${detail.moduleName}` : '模块详情'"
      width="640"
      placement="right"
    >
      <template v-if="detail">
        <a-descriptions :column="1" bordered size="small" style="margin-bottom: 16px">
          <a-descriptions-item label="模块名">{{ detail.moduleName }}</a-descriptions-item>
          <a-descriptions-item label="状态"><StatusTag type="health" :status="detail.status" /></a-descriptions-item>
          <a-descriptions-item label="延迟">{{ detail.latencyMs }} ms</a-descriptions-item>
          <a-descriptions-item label="依赖项数">{{ detail.dependencies.length }}</a-descriptions-item>
        </a-descriptions>

        <div class="section-title">依赖项明细</div>
        <a-table
          :data-source="detail.dependencies"
          row-key="name"
          size="small"
          :pagination="false"
          :columns="[
            { title: '名称', dataIndex: 'name', key: 'name' },
            { title: '状态', key: 'status', width: 100 },
            { title: '延迟', key: 'latencyMs', width: 80 },
            { title: '错误', dataIndex: 'error', key: 'error', ellipsis: true },
            { title: '最近检查', dataIndex: 'lastCheckedAt', key: 'lastCheckedAt', width: 160 },
          ]"
        >
          <template #bodyCell="{ column, record }">
            <template v-if="column.key === 'status'">
              <StatusTag type="health" :status="record.status" />
            </template>
            <template v-else-if="column.key === 'latencyMs'">
              {{ record.latencyMs }}ms
            </template>
          </template>
        </a-table>
      </template>
    </a-drawer>
  </div>
</template>

<style scoped>
.runtime-ops-health .page-header { background: var(--n1, #fff); border-radius: 8px; padding: 24px; margin-bottom: 16px; box-shadow: 0 1px 2px rgba(0,0,0,.03); }
.runtime-ops-health .page-title { font-size: 20px; font-weight: 600; margin-bottom: 4px; }
.runtime-ops-health .page-desc { color: #8C8C8C; }
.runtime-ops-health .module-card { display: flex; flex-direction: column; gap: 8px; }
.runtime-ops-health .module-name { display: flex; align-items: center; font-size: 16px; font-weight: 500; }
.runtime-ops-health .module-status { display: flex; align-items: center; }
.runtime-ops-health .module-meta { font-size: 12px; color: #8C8C8C; }
.runtime-ops-health .section-title { font-size: 14px; font-weight: 500; margin: 16px 0 8px; }
</style>
