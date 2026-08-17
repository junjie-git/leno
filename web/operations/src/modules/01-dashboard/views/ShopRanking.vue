<template>
  <div class="shop-ranking">
    <!-- 区域 A：时间筛选 + TopN 选择器 + 刷新 -->
    <div class="shop-ranking__toolbar">
      <DateTimeRangePicker :value="dateRange" @change="onDateRangeChange" />
      <span class="shop-ranking__topn-label">TopN</span>
      <a-select v-model:value="topN" :options="topNOptions" style="width: 96px" />
      <a-button :loading="loading" @click="loadData">
        <template #icon><ReloadOutlined /></template>
        刷新
      </a-button>
    </div>

    <!-- 区域 B：TopN GMV 横向柱状图（Top1 高亮，前端切片不重新请求） -->
    <a-card title="店铺 GMV 排行" class="shop-ranking__card">
      <template #extra>
        <span class="shop-ranking__hint">Top {{ topN }} / 共 {{ totalShops }} 家</span>
      </template>
      <ChartBarHorizontal
        v-if="hasRankingData"
        :categories="barCategories"
        :values="barValues"
        series-name="GMV"
        :height="400"
        :top-highlight="true"
        :value-formatter="formatGmv"
        :loading="loading"
      />
      <EmptyState
        v-else-if="!loading"
        :description="errorMessage"
        :action-text="error ? '重试' : '刷新'"
        @action="loadData"
      />
    </a-card>

    <!-- 区域 C：排行明细表 -->
    <a-card title="排行明细" class="shop-ranking__card">
      <a-table
        v-if="!error"
        :columns="tableColumns"
        :data-source="rankedItems"
        :loading="loading"
        :pagination="{ pageSize: 20, showSizeChanger: false }"
        row-key="shopId"
        size="middle"
        :scroll="{ x: 960 }"
        :custom-row="shopRowProps"
      >
        <template #emptyText>
          <EmptyState description="暂无店铺排行数据" />
        </template>
        <template #bodyCell="{ column, record, index }">
          <template v-if="column.key === 'rank'">
            <span :class="rankClass(index + 1)">{{ index + 1 }}</span>
          </template>
          <template v-else-if="column.key === 'shopName'">
            <span class="shop-ranking__shop-name">{{ record.shopName }}</span>
          </template>
          <template v-else-if="column.key === 'gmv'">{{ formatGmv(record.gmv) }}</template>
          <template v-else-if="column.key === 'avgOrderValue'">¥{{ record.avgOrderValue.toFixed(1) }}</template>
          <template v-else-if="column.key === 'positiveRate'">{{ record.positiveRate.toFixed(1) }}%</template>
          <template v-else-if="column.key === 'status'">
            <a-tag :color="statusMeta(record.status).color">{{ statusMeta(record.status).label }}</a-tag>
          </template>
        </template>
      </a-table>
      <EmptyState v-else description="加载店铺排行失败" action-text="重试" @action="loadData" />
    </a-card>
  </div>
</template>

<script setup lang="ts">
import { ref, computed, watch } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import { ReloadOutlined } from '@ant-design/icons-vue'
import { message } from 'ant-design-vue'
import { DateTimeRangePicker, EmptyState } from '@/shared/components'
import { dashboardApi } from '../api/dashboard.api'
import {
  parseShopRankingData,
  type ShopRankingData,
  type ShopRankingItem,
  type ShopStatus,
  type DateRangeParams,
} from '../types/dashboard.dto'
import ChartBarHorizontal from '../components/ChartBarHorizontal.vue'

const route = useRoute()
const router = useRouter()
const loading = ref(false)
const error = ref(false)
const data = ref<ShopRankingData | null>(null)

// TopN 选择器：10/20/50，切换仅前端切片，不重新请求
const topN = ref<number>(10)
const topNOptions = [
  { label: '10', value: 10 },
  { label: '20', value: 20 },
  { label: '50', value: 50 },
]

// 初始化时间范围：优先读取路由 query，否则默认近 7 天
function initDateRange(): [string, string] {
  const queryStart = route.query.start as string | undefined
  const queryEnd = route.query.end as string | undefined
  if (queryStart && queryEnd) return [queryStart, queryEnd]
  return getLast7DaysRange()
}

function getLast7DaysRange(): [string, string] {
  const end = new Date()
  const start = new Date()
  start.setDate(start.getDate() - 7)
  return [start.toISOString(), end.toISOString()]
}

const dateRange = ref<[string, string]>(initDateRange())

function onDateRangeChange(val: [string, string]) {
  dateRange.value = val
}

const errorMessage = computed(() => (error.value ? '加载店铺排行失败' : '暂无店铺排行数据'))

// 店铺状态元信息（shared StatusTag shop 映射缺 Suspended/Closed/QualificationExpired，模块内对齐运营设计稿文案）
const STATUS_META: Record<ShopStatus, { label: string; color: string }> = {
  Pending: { label: '待审核', color: 'warning' },
  Active: { label: '营业中', color: 'success' },
  Suspended: { label: '已暂停', color: 'warning' },
  Closed: { label: '已关闭', color: 'default' },
  QualificationExpired: { label: '资质过期', color: 'error' },
}

function statusMeta(status: ShopStatus): { label: string; color: string } {
  return STATUS_META[status] ?? { label: status, color: 'default' }
}

// 后端固定返回 Top50，前端按 GMV 降序兜底排序
const sortedItems = computed<ShopRankingItem[]>(() => {
  if (!data.value) return []
  return data.value.items.slice().sort((a, b) => b.gmv - a.gmv)
})

// 按 TopN 前端切片
const rankedItems = computed<ShopRankingItem[]>(() => sortedItems.value.slice(0, topN.value))

const totalShops = computed(() => sortedItems.value.length)

const hasRankingData = computed(() => rankedItems.value.length > 0)

// GMV 格式化：≥1 万显示万单位
function formatGmv(value: number): string {
  if (value >= 10000) return `¥${(value / 10000).toFixed(1)}万`
  return `¥${value.toLocaleString('zh-CN')}`
}

// 柱状图数据：TopN 店铺名与 GMV
const barCategories = computed(() => rankedItems.value.map((item) => item.shopName))
const barValues = computed(() => rankedItems.value.map((item) => item.gmv))

// 排名样式：前 3 名高亮
function rankClass(rank: number): string {
  if (rank === 1) return 'shop-ranking__rank shop-ranking__rank--gold'
  if (rank === 2) return 'shop-ranking__rank shop-ranking__rank--silver'
  if (rank === 3) return 'shop-ranking__rank shop-ranking__rank--bronze'
  return 'shop-ranking__rank'
}

// 主表列定义
const tableColumns = [
  { title: '排名', key: 'rank', width: 72, align: 'center' as const },
  { title: '店铺名称', key: 'shopName', width: 200 },
  { title: '卖家账号', dataIndex: 'sellerAccount', key: 'sellerAccount', width: 140 },
  { title: 'GMV', key: 'gmv', width: 120, align: 'right' as const },
  { title: '订单量', dataIndex: 'orderCount', key: 'orderCount', width: 100, align: 'right' as const },
  { title: '客单价', key: 'avgOrderValue', width: 100, align: 'right' as const },
  { title: '好评率', key: 'positiveRate', width: 100, align: 'right' as const },
  { title: '状态', key: 'status', width: 110 },
]

// 行点击跳转店铺治理页（携带 shopId）
function shopRowProps(record: ShopRankingItem) {
  return {
    onClick: () => {
      router.push({ path: '/seller-ops/shop-governance', query: { shopId: record.shopId } })
    },
    style: { cursor: 'pointer' },
  }
}

async function loadData() {
  const [start, end] = dateRange.value
  if (new Date(start) >= new Date(end)) {
    message.warning('结束时间需晚于开始时间')
    return
  }
  loading.value = true
  error.value = false
  try {
    const params: DateRangeParams = { start, end }
    const report = await dashboardApi.getShopRanking(params)
    data.value = parseShopRankingData(report)
  } catch {
    error.value = true
    message.error('加载店铺排行失败')
  } finally {
    loading.value = false
  }
}

// 时间变化即重新加载（含首次进入）；TopN 切换纯前端切片不触发请求
watch(
  dateRange,
  () => {
    loadData()
  },
  { deep: true, immediate: true },
)
</script>

<style scoped>
.shop-ranking {
  display: flex;
  flex-direction: column;
  gap: 24px;
}
.shop-ranking__toolbar {
  display: flex;
  gap: 12px;
  align-items: center;
  flex-wrap: wrap;
}
.shop-ranking__topn-label {
  font-size: 14px;
  color: #8c8c8c;
}
.shop-ranking__card {
  border-radius: 8px;
}
.shop-ranking__hint {
  font-size: 12px;
  color: #8c8c8c;
}
.shop-ranking__shop-name {
  font-weight: 500;
}
.shop-ranking__rank {
  display: inline-block;
  min-width: 24px;
  text-align: center;
  font-weight: 600;
}
.shop-ranking__rank--gold {
  color: #faad14;
}
.shop-ranking__rank--silver {
  color: #8c8c8c;
}
.shop-ranking__rank--bronze {
  color: #d48806;
}
</style>
