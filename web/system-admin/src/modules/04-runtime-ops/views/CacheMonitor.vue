<!-- web/system-admin/src/modules/04-runtime-ops/views/CacheMonitor.vue -->
<!-- 缓存监控：Redis 信息 / Keyspace / Key 浏览 三 Tab，支持 30s 自动刷新（仅信息与 Keyspace） -->
<template>
  <div class="cache-monitor">
    <!-- 顶部工具栏 -->
    <a-card :bordered="false" class="toolbar-card">
      <div class="toolbar">
        <div class="toolbar-left">
          <a-button type="primary" :loading="refreshing" @click="onRefreshAll">
            <ReloadOutlined />刷新
          </a-button>
          <a-divider type="vertical" />
          <span class="auto-refresh-label">自动刷新（30s）</span>
          <a-switch v-model:checked="autoRefresh" @change="onAutoRefreshChange" />
          <span v-if="autoRefresh" class="auto-refresh-hint">仅轮询 Redis 信息与 Keyspace</span>
        </div>
      </div>
    </a-card>

    <a-tabs v-model:activeKey="activeTab" type="card">
      <!-- Tab 1: Redis 信息 -->
      <a-tab-pane key="info" tab="Redis 信息">
        <a-card :bordered="false">
          <a-skeleton :loading="infoLoading && !redisInfo" active>
            <a-descriptions
              v-if="redisInfo"
              title="Redis 服务器信息"
              bordered
              :column="{ xs: 1, sm: 2, lg: 3 }"
              size="small"
            >
              <a-descriptions-item label="redis_version">{{ redisInfo.redisVersion }}</a-descriptions-item>
              <a-descriptions-item label="redis_mode">{{ redisInfo.redisMode }}</a-descriptions-item>
              <a-descriptions-item label="os">{{ redisInfo.os }}</a-descriptions-item>
              <a-descriptions-item label="arch_bits">{{ redisInfo.archBits }}</a-descriptions-item>
              <a-descriptions-item label="tcp_port">{{ redisInfo.tcpPort }}</a-descriptions-item>
              <a-descriptions-item label="uptime_in_days">{{ redisInfo.uptimeInDays }} 天</a-descriptions-item>
              <a-descriptions-item label="connected_clients">{{ redisInfo.connectedClients }}</a-descriptions-item>
              <a-descriptions-item label="used_memory_human">{{ redisInfo.usedMemoryHuman }}</a-descriptions-item>
              <a-descriptions-item label="used_memory_peak_human">{{ redisInfo.usedMemoryPeakHuman }}</a-descriptions-item>
              <a-descriptions-item label="maxmemory_human">{{ redisInfo.maxmemoryHuman }}</a-descriptions-item>
              <a-descriptions-item label="mem_fragmentation_ratio">{{ redisInfo.memFragmentationRatio }}</a-descriptions-item>
              <a-descriptions-item label="total_connections_received">{{ redisInfo.totalConnectionsReceived }}</a-descriptions-item>
              <a-descriptions-item label="total_commands_processed">{{ redisInfo.totalCommandsProcessed }}</a-descriptions-item>
              <a-descriptions-item label="keyspace_hits">{{ redisInfo.keyspaceHits }}</a-descriptions-item>
              <a-descriptions-item label="keyspace_misses">{{ redisInfo.keyspaceMisses }}</a-descriptions-item>
              <a-descriptions-item label="evicted_keys">{{ redisInfo.evictedKeys }}</a-descriptions-item>
            </a-descriptions>
            <a-empty v-else-if="!infoLoading" description="暂无 Redis 信息" />
          </a-skeleton>
        </a-card>
      </a-tab-pane>

      <!-- Tab 2: Keyspace -->
      <a-tab-pane key="keyspace" tab="Keyspace">
        <a-card :bordered="false">
          <a-row :gutter="[16, 16]" style="margin-bottom: 16px">
            <a-col :xs="24" :sm="8">
              <StatisticCard title="总 keys" :value="keyspaceStats.totalKeys" :loading="keyspaceLoading" />
            </a-col>
            <a-col :xs="24" :sm="8">
              <StatisticCard title="带 TTL keys" :value="keyspaceStats.totalExpires" :loading="keyspaceLoading" />
            </a-col>
            <a-col :xs="24" :sm="8">
              <StatisticCard title="平均 TTL" :value="keyspaceStats.avgTtlDisplay" :loading="keyspaceLoading" />
            </a-col>
          </a-row>
          <a-table
            :columns="keyspaceColumns"
            :data-source="keyspaceTableData"
            :loading="keyspaceLoading"
            :pagination="false"
            row-key="db"
            size="small"
          >
            <template #bodyCell="{ column, record }">
              <template v-if="column.key === 'db'">db{{ record.db }}</template>
              <template v-else-if="column.key === 'avgTtl'">{{ formatTtlMs(record.avgTtl) }}</template>
            </template>
          </a-table>
        </a-card>
      </a-tab-pane>

      <!-- Tab 3: Key 浏览 -->
      <a-tab-pane key="browse" tab="Key 浏览">
        <a-card :bordered="false">
          <a-form layout="inline" style="margin-bottom: 16px">
            <a-form-item label="DB">
              <a-select v-model:value="keyQuery.db" style="width: 100px" :options="dbOptions" />
            </a-form-item>
            <a-form-item label="Pattern">
              <a-input
                v-model:value="keyQuery.pattern"
                placeholder="如 *user*"
                allow-clear
                style="width: 240px"
                @press-enter="onSearchKeys"
              />
            </a-form-item>
            <a-form-item label="类型">
              <a-select
                v-model:value="keyQuery.type"
                style="width: 120px"
                allow-clear
                placeholder="全部"
                :options="typeOptions"
              />
            </a-form-item>
            <a-form-item>
              <a-button type="primary" @click="onSearchKeys">查询</a-button>
              <a-button style="margin-left: 8px" @click="onResetKeyQuery">重置</a-button>
            </a-form-item>
          </a-form>
          <a-table
            :columns="keyColumns"
            :data-source="keysData.items"
            :loading="keysLoading"
            :pagination="keyPagination"
            row-key="key"
            size="small"
            @change="onKeyTableChange"
          >
            <template #bodyCell="{ column, record }">
              <template v-if="column.key === 'type'">
                <a-tag :color="typeColor(record.type)">{{ record.type }}</a-tag>
              </template>
              <template v-else-if="column.key === 'ttl'">{{ formatTtl(record.ttl) }}</template>
              <template v-else-if="column.key === 'action'">
                <a-space>
                  <a-button type="link" size="small" @click="onViewKey(record)">查看</a-button>
                  <a-button type="link" size="small" danger @click="onDeleteKey(record)">删除</a-button>
                </a-space>
              </template>
            </template>
          </a-table>
        </a-card>
      </a-tab-pane>
    </a-tabs>

    <!-- Key 详情弹窗 -->
    <a-modal
      v-model:open="detailModalOpen"
      title="Key 详情"
      width="720"
      :footer="null"
      :destroy-on-close="true"
    >
      <a-spin :spinning="detailLoading">
        <template v-if="keyDetail">
          <a-descriptions :column="1" bordered size="small" style="margin-bottom: 16px">
            <a-descriptions-item label="Key">{{ keyDetail.key }}</a-descriptions-item>
            <a-descriptions-item label="DB">db{{ keyDetail.db }}</a-descriptions-item>
            <a-descriptions-item label="类型">
              <a-tag :color="typeColor(keyDetail.type)">{{ keyDetail.type }}</a-tag>
            </a-descriptions-item>
            <a-descriptions-item label="Size">{{ keyDetail.size }}</a-descriptions-item>
            <a-descriptions-item label="TTL">{{ formatTtl(keyDetail.ttl) }}</a-descriptions-item>
          </a-descriptions>
          <div class="detail-value-title">Value</div>
          <JsonViewer
            v-if="detailValueIsJson"
            :value="detailJsonValue"
            :max-depth="4"
            :max-height="420"
          />
          <pre v-else class="detail-value-text">{{ detailRawValue }}</pre>
        </template>
      </a-spin>
    </a-modal>

    <!-- 删除确认 -->
    <ConfirmDialog
      :open="deleteConfirmOpen"
      danger
      title="删除 Key"
      :content="deleteConfirmContent"
      @confirm="onConfirmDelete"
      @cancel="deleteConfirmOpen = false"
    />
  </div>
</template>

<script setup lang="ts">
import { ref, reactive, computed, onMounted, onBeforeUnmount } from 'vue'
import { message } from 'ant-design-vue'
import type { TableColumnsType } from 'ant-design-vue'
import { ReloadOutlined } from '@ant-design/icons-vue'
import { cacheApi } from '../api/cache.api'
import type {
  RedisInfoDto,
  KeyspaceDto,
  RedisKeyDto,
  RedisKeyDetailDto,
  CacheKeyQueryDto,
  RedisKeyType,
} from '../types/cache.dto'
import { StatisticCard, JsonViewer, ConfirmDialog } from '@/shared/components'
import type { PageResult } from '@/shared/types'

type TabKey = 'info' | 'keyspace' | 'browse'

const activeTab = ref<TabKey>('info')

// loading 状态
const refreshing = ref(false)
const infoLoading = ref(false)
const keyspaceLoading = ref(false)
const keysLoading = ref(false)
const detailLoading = ref(false)

// 数据
const redisInfo = ref<RedisInfoDto | null>(null)
const keyspaces = ref<KeyspaceDto[]>([])
const keysData = ref<PageResult<RedisKeyDto>>({ items: [], total: 0, page: 1, pageSize: 20 })
const keyDetail = ref<RedisKeyDetailDto | null>(null)

// 自动刷新
const autoRefresh = ref(false)
let pollTimer: ReturnType<typeof setInterval> | null = null
const POLL_INTERVAL = 30_000

// 选择器选项
const dbOptions = Array.from({ length: 16 }, (_, i) => ({ label: `db${i}`, value: i }))

const typeOptions: { label: string; value: RedisKeyType }[] = [
  { label: 'string', value: 'string' },
  { label: 'hash', value: 'hash' },
  { label: 'list', value: 'list' },
  { label: 'set', value: 'set' },
  { label: 'zset', value: 'zset' },
]

const keyQuery = reactive<{ db: number; pattern: string; type?: RedisKeyType }>({
  db: 0,
  pattern: '',
  type: undefined,
})

const keyPagination = reactive({
  current: 1,
  pageSize: 20,
  total: 0,
  showSizeChanger: true,
  showTotal: (total: number) => `共 ${total} 条`,
})

const keyspaceColumns: TableColumnsType = [
  { title: 'DB', dataIndex: 'db', key: 'db', width: 100 },
  { title: 'Keys', dataIndex: 'keys', key: 'keys', width: 120 },
  { title: 'Expires', dataIndex: 'expires', key: 'expires', width: 120 },
  { title: 'Avg TTL', key: 'avgTtl' },
]

const keyColumns: TableColumnsType = [
  { title: 'Key', dataIndex: 'key', key: 'key', ellipsis: true },
  { title: '类型', dataIndex: 'type', key: 'type', width: 100 },
  { title: 'Size', dataIndex: 'size', key: 'size', width: 100 },
  { title: 'TTL', key: 'ttl', width: 140 },
  { title: '操作', key: 'action', width: 140, fixed: 'right' },
]

// 补齐 db0-db15，缺失库填 0
const keyspaceTableData = computed<KeyspaceDto[]>(() => {
  const map = new Map<number, KeyspaceDto>(keyspaces.value.map((k) => [k.db, k]))
  const result: KeyspaceDto[] = []
  for (let i = 0; i < 16; i++) {
    const item = map.get(i)
    result.push(item ?? { db: i, keys: 0, expires: 0, avgTtl: 0 })
  }
  return result
})

const keyspaceStats = computed(() => {
  const list = keyspaces.value
  const totalKeys = list.reduce((sum, k) => sum + k.keys, 0)
  const totalExpires = list.reduce((sum, k) => sum + k.expires, 0)
  const withTtl = list.filter((k) => k.avgTtl > 0)
  const avgTtlMs =
    withTtl.length > 0
      ? Math.round(withTtl.reduce((sum, k) => sum + k.avgTtl, 0) / withTtl.length)
      : 0
  return {
    totalKeys,
    totalExpires,
    avgTtlDisplay: avgTtlMs > 0 ? formatTtlMs(avgTtlMs) : '—',
  }
})

function formatTtl(ttl: number): string {
  if (ttl < 0) return '永不过期'
  if (ttl === 0) return '已过期'
  const days = Math.floor(ttl / 86400)
  const hours = Math.floor((ttl % 86400) / 3600)
  const minutes = Math.floor((ttl % 3600) / 60)
  const seconds = ttl % 60
  const parts: string[] = []
  if (days > 0) parts.push(`${days}天`)
  if (hours > 0) parts.push(`${hours}小时`)
  if (minutes > 0) parts.push(`${minutes}分`)
  if (seconds > 0 && days === 0 && hours === 0) parts.push(`${seconds}秒`)
  return parts.join('') || '0秒'
}

function formatTtlMs(ttlMs: number): string {
  if (ttlMs <= 0) return '—'
  return formatTtl(Math.floor(ttlMs / 1000))
}

function typeColor(type: RedisKeyType): string {
  const colors: Record<RedisKeyType, string> = {
    string: 'blue',
    hash: 'green',
    list: 'orange',
    set: 'purple',
    zset: 'cyan',
  }
  return colors[type]
}

async function fetchRedisInfo() {
  infoLoading.value = true
  try {
    redisInfo.value = await cacheApi.info()
  } catch {
    message.error('加载 Redis 信息失败')
  } finally {
    infoLoading.value = false
  }
}

async function fetchKeyspaces() {
  keyspaceLoading.value = true
  try {
    keyspaces.value = await cacheApi.keyspaces()
  } catch {
    message.error('加载 Keyspace 失败')
  } finally {
    keyspaceLoading.value = false
  }
}

async function fetchKeys() {
  keysLoading.value = true
  try {
    const params: CacheKeyQueryDto = {
      db: keyQuery.db,
      pattern: keyQuery.pattern || '*',
      page: keyPagination.current,
      pageSize: keyPagination.pageSize,
    }
    if (keyQuery.type) params.type = keyQuery.type
    const result = await cacheApi.listKeys(params)
    keysData.value = result
    keyPagination.total = result.total
    keyPagination.current = result.page
  } catch {
    message.error('加载 Key 列表失败')
  } finally {
    keysLoading.value = false
  }
}

async function onRefreshAll() {
  refreshing.value = true
  try {
    await Promise.allSettled([fetchRedisInfo(), fetchKeyspaces(), fetchKeys()])
  } finally {
    refreshing.value = false
  }
}

function onAutoRefreshChange(checked: boolean) {
  if (checked) {
    startPolling()
  } else {
    stopPolling()
  }
}

function startPolling() {
  stopPolling()
  pollTimer = setInterval(() => {
    void Promise.allSettled([fetchRedisInfo(), fetchKeyspaces()])
  }, POLL_INTERVAL)
}

function stopPolling() {
  if (pollTimer) {
    clearInterval(pollTimer)
    pollTimer = null
  }
}

function onSearchKeys() {
  keyPagination.current = 1
  fetchKeys()
}

function onResetKeyQuery() {
  keyQuery.db = 0
  keyQuery.pattern = ''
  keyQuery.type = undefined
  keyPagination.current = 1
  fetchKeys()
}

function onKeyTableChange(pag: { current: number; pageSize: number }) {
  keyPagination.current = pag.current
  keyPagination.pageSize = pag.pageSize
  fetchKeys()
}

// Key 详情弹窗
const detailModalOpen = ref(false)

async function onViewKey(record: RedisKeyDto) {
  detailModalOpen.value = true
  detailLoading.value = true
  keyDetail.value = null
  try {
    keyDetail.value = await cacheApi.getKey(record.key, keyQuery.db)
  } catch {
    message.error('加载 Key 详情失败')
  } finally {
    detailLoading.value = false
  }
}

const detailValueIsJson = computed(() => {
  if (!keyDetail.value) return false
  const v = keyDetail.value.value
  if (v === null || v === undefined) return false
  if (typeof v === 'object') return true
  if (typeof v === 'string') {
    const trimmed = v.trim()
    if (trimmed === '') return false
    try {
      JSON.parse(trimmed)
      return true
    } catch {
      return false
    }
  }
  return false
})

const detailJsonValue = computed<unknown>(() => {
  if (!keyDetail.value) return null
  const v = keyDetail.value.value
  if (typeof v === 'string') {
    try {
      return JSON.parse(v)
    } catch {
      return v
    }
  }
  return v
})

const detailRawValue = computed<string>(() => {
  if (!keyDetail.value) return ''
  const v = keyDetail.value.value
  if (v === null || v === undefined) return ''
  if (typeof v === 'string') return v
  try {
    return JSON.stringify(v, null, 2)
  } catch {
    return String(v)
  }
})

// 删除确认
const deleteConfirmOpen = ref(false)
const pendingDelete = ref<RedisKeyDto | null>(null)

const deleteConfirmContent = computed(() => {
  if (!pendingDelete.value) return ''
  return `确认删除 db${keyQuery.db} 中的 key "${pendingDelete.value.key}"？此操作不可恢复。`
})

function onDeleteKey(record: RedisKeyDto) {
  pendingDelete.value = record
  deleteConfirmOpen.value = true
}

async function onConfirmDelete() {
  deleteConfirmOpen.value = false
  const target = pendingDelete.value
  pendingDelete.value = null
  if (!target) return
  try {
    await cacheApi.deleteKey(target.key, keyQuery.db)
    message.success('Key 已删除')
    await fetchKeys()
  } catch {
    message.error('删除 Key 失败')
  }
}

onMounted(() => {
  fetchRedisInfo()
  fetchKeyspaces()
  fetchKeys()
})

onBeforeUnmount(() => {
  stopPolling()
})
</script>

<style scoped>
.cache-monitor {
  display: flex;
  flex-direction: column;
  gap: 16px;
}
.toolbar-card :deep(.ant-card-body) {
  padding: 12px 24px;
}
.toolbar {
  display: flex;
  align-items: center;
  justify-content: space-between;
}
.toolbar-left {
  display: flex;
  align-items: center;
  gap: 8px;
}
.auto-refresh-label {
  font-size: 14px;
  color: #595959;
  margin-left: 8px;
}
.auto-refresh-hint {
  font-size: 12px;
  color: #8c8c8c;
}
.detail-value-title {
  font-size: 14px;
  font-weight: 500;
  margin-bottom: 8px;
  color: #262626;
}
.detail-value-text {
  margin: 0;
  padding: 12px;
  background: #fafafa;
  border: 1px solid #f0f0f0;
  border-radius: 4px;
  font-family: var(--ff-mono, 'SF Mono', 'Cascadia Code', Consolas, monospace);
  font-size: 12px;
  line-height: 1.6;
  color: #595959;
  white-space: pre-wrap;
  word-break: break-all;
  max-height: 420px;
  overflow: auto;
}
</style>
