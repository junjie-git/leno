<template>
  <div class="shop-ranking">
    <!-- 筛选条 -->
    <div class="shop-ranking__toolbar">
      <DateTimeRangePicker :value="dateRange" @change="onDateRangeChange" />
      <a-segmented v-model:value="dimension" :options="dimensionOptions" />
      <span class="shop-ranking__topn-label">TopN</span>
      <a-input-number v-model:value="topN" :min="5" :max="50" :step="1" />
      <a-button :loading="loading" @click="loadData">
        <template #icon><ReloadOutlined /></template>
        刷新
      </a-button>
    </div>

    <!-- Top 3 领奖台 -->
    <div class="shop-ranking__podium">
      <!-- 第 2 名 -->
      <div class="shop-ranking__podium-item shop-ranking__podium-item--silver">
        <TrophyOutlined class="shop-ranking__medal shop-ranking__medal--silver" />
        <template v-if="top3[1]">
          <div class="shop-ranking__shop-name">{{ top3[1].shopName }}</div>
          <div class="shop-ranking__category">{{ top3[1].category }}</div>
          <div class="shop-ranking__metric-value">{{ formatMetric(top3[1]) }}</div>
          <GrowthTag :rate="top3[1].growthRate" />
        </template>
        <a-empty v-else image="simple" />
      </div>
      <!-- 第 1 名（居中放大） -->
      <div class="shop-ranking__podium-item shop-ranking__podium-item--gold">
        <TrophyOutlined class="shop-ranking__medal shop-ranking__medal--gold" />
        <template v-if="top3[0]">
          <div class="shop-ranking__shop-name shop-ranking__shop-name--first">
            {{ top3[0].shopName }}
          </div>
          <div class="shop-ranking__category">{{ top3[0].category }}</div>
          <div class="shop-ranking__metric-value shop-ranking__metric-value--first">
            {{ formatMetric(top3[0]) }}
          </div>
          <GrowthTag :rate="top3[0].growthRate" />
        </template>
        <a-empty v-else image="simple" />
      </div>
      <!-- 第 3 名 -->
      <div class="shop-ranking__podium-item shop-ranking__podium-item--bronze">
        <TrophyOutlined class="shop-ranking__medal shop-ranking__medal--bronze" />
        <template v-if="top3[2]">
          <div class="shop-ranking__shop-name">{{ top3[2].shopName }}</div>
          <div class="shop-ranking__category">{{ top3[2].category }}</div>
          <div class="shop-ranking__metric-value">{{ formatMetric(top3[2]) }}</div>
          <GrowthTag :rate="top3[2].growthRate" />
        </template>
        <a-empty v-else image="simple" />
      </div>
    </div>

    <!-- 主排行表 -->
    <a-card title="店铺排行明细" class="shop-ranking__card">
      <a-spin :spinning="loading">
        <a-table
          v-if="rankedItems.length > 0"
          :columns="tableColumns"
          :data-source="rankedItems"
          :pagination="{ pageSize: 20, showSizeChanger: false }"
          row-key="shopId"
          :scroll="{ y: 480 }"
        >
          <template #bodyCell="{ column, record, index }">
            <template v-if="column.key === 'rank'">
              <span :class="rankClass(index + 1)">{{ index + 1 }}</span>
            </template>
            <template v-else-if="column.key === 'shopName'">
              <a class="shop-ranking__link" @click="navigateToAuditLogs(record.shopId)">
                {{ record.shopName }}
              </a>
            </template>
            <template v-else-if="column.key === 'metricValue'">
              {{ formatMetric(record) }}
            </template>
            <template v-else-if="column.key === 'growthRate'">
              <GrowthTag :rate="record.growthRate" />
            </template>
            <template v-else-if="column.key === 'status'">
              <StatusTag :status="record.status" type="shop" />
            </template>
          </template>
        </a-table>
        <EmptyState
          v-else-if="!loading"
          description="所选时间范围暂无店铺排行数据"
          action-text="刷新"
          @action="loadData"
        />
      </a-spin>
    </a-card>
  </div>
</template>

<script setup lang="ts">
import { ref, computed, onMounted, watch, defineComponent, h, type PropType } from 'vue'
import { useRouter, useRoute } from 'vue-router'
import { ReloadOutlined, TrophyOutlined, ArrowUpOutlined, ArrowDownOutlined } from '@ant-design/icons-vue'
import { message } from 'ant-design-vue'
import DateTimeRangePicker from '@/shared/components/DateTimeRangePicker.vue'
import EmptyState from '@/shared/components/EmptyState.vue'
import StatusTag from '@/shared/components/StatusTag.vue'
import { dashboardApi } from '../api/dashboard.api'
import {
  parseShopRankingData,
  type ShopRankingData,
  type ShopRankingItem,
  type DateRangeParams,
} from '../types/dashboard.dto'

// 增长率标签内联组件：正绿↑、负红↓
const GrowthTag = defineComponent({
  name: 'GrowthTag',
  props: {
    rate: { type: Number as PropType<number>, required: true },
  },
  setup(props) {
    return () => {
      const isUp = props.rate >= 0
      return h('span', {
        style: { color: isUp ? '#52C41A' : '#FF4D4F', fontSize: '12px' },
      }, [
        h(isUp ? ArrowUpOutlined : ArrowDownOutlined, { style: { marginRight: '4px' } }),
        `${Math.abs(props.rate).toFixed(1)}%`,
      ])
    }
  },
})

const router = useRouter()
const route = useRoute()
const loading = ref(false)
const data = ref<ShopRankingData | null>(null)
const dimension = ref<'salesAmount' | 'orderCount' | 'avgOrderAmount'>('salesAmount')
const topN = ref<number>(10)

const dimensionOptions = [
  { label: '销售额', value: 'salesAmount' },
  { label: '订单量', value: 'orderCount' },
  { label: '客单价', value: 'avgOrderAmount' },
]

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

// 按维度排序后的所有店铺
const sortedItems = computed<ShopRankingItem[]>(() => {
  if (!data.value) return []
  return data.value.items
    .slice()
    .sort((a, b) => (b[dimension.value] as number) - (a[dimension.value] as number))
})

// 按 TopN 截取后的列表（表格用）
const rankedItems = computed<ShopRankingItem[]>(() =>
  sortedItems.value.slice(0, topN.value),
)

// Top 3 领奖台数据
const top3 = computed<(ShopRankingItem | null)[]>(() => {
  const items = sortedItems.value
  return [items[0] ?? null, items[1] ?? null, items[2] ?? null]
})

// 按维度格式化指标值
function formatMetric(item: ShopRankingItem): string {
  const value = item[dimension.value] as number
  if (dimension.value === 'salesAmount') {
    if (value >= 10000) return `¥${(value / 10000).toFixed(1)}万`
    return `¥${value.toLocaleString('zh-CN')}`
  }
  if (dimension.value === 'avgOrderAmount') {
    return `¥${value.toFixed(2)}`
  }
  return value.toLocaleString('zh-CN')
}

// 排名样式：前 3 名高亮
function rankClass(rank: number): string {
  if (rank === 1) return 'shop-ranking__rank shop-ranking__rank--gold'
  if (rank === 2) return 'shop-ranking__rank shop-ranking__rank--silver'
  if (rank === 3) return 'shop-ranking__rank shop-ranking__rank--bronze'
  return 'shop-ranking__rank'
}

// 主表列定义
const tableColumns = [
  { title: '排名', key: 'rank', width: 80 },
  { title: '店铺名', key: 'shopName' },
  { title: '所在类目', dataIndex: 'category', key: 'category', width: 160 },
  { title: '指标值', key: 'metricValue', width: 160 },
  { title: '环比增长率', key: 'growthRate', width: 140 },
  { title: '状态', key: 'status', width: 120 },
]

async function loadData() {
  const [start, end] = dateRange.value
  if (new Date(start) >= new Date(end)) {
    message.warning('结束时间需晚于开始时间')
    return
  }
  loading.value = true
  try {
    const params: DateRangeParams = { start, end }
    const { data: report } = await dashboardApi.getShopRanking(params)
    data.value = parseShopRankingData(report)
    // 若返回数据中 dimension 与当前选择不一致，以当前选择为准（前端重新排序）
  } catch {
    message.error('店铺排行加载失败')
  } finally {
    loading.value = false
  }
}

function navigateToAuditLogs(shopId: string) {
  router.push({
    path: '/audit/audit-logs',
    query: { resourceType: 'Shop', keyword: shopId },
  })
}

watch(dateRange, () => loadData())

onMounted(() => loadData())
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
  color: #8C8C8C;
}
.shop-ranking__podium {
  display: flex;
  justify-content: center;
  align-items: flex-end;
  gap: 24px;
  padding: 24px 0;
  background: #FAFAFA;
  border-radius: 8px;
}
.shop-ranking__podium-item {
  display: flex;
  flex-direction: column;
  align-items: center;
  padding: 24px 16px;
  border-radius: 8px;
  background: #FFFFFF;
  width: 220px;
  min-height: 180px;
  box-shadow: 0 2px 8px rgba(0, 0, 0, 0.06);
}
.shop-ranking__podium-item--gold {
  width: 260px;
  min-height: 220px;
  border: 2px solid #FAAD14;
}
.shop-ranking__podium-item--silver {
  border: 2px solid #D9D9D9;
}
.shop-ranking__podium-item--bronze {
  border: 2px solid #D48806;
}
.shop-ranking__medal {
  font-size: 32px;
  margin-bottom: 12px;
}
.shop-ranking__medal--gold {
  color: #FAAD14;
}
.shop-ranking__medal--silver {
  color: #8C8C8C;
}
.shop-ranking__medal--bronze {
  color: #D48806;
}
.shop-ranking__shop-name {
  font-size: 16px;
  font-weight: 500;
  color: #000000D9;
  margin-bottom: 4px;
}
.shop-ranking__shop-name--first {
  font-size: 20px;
  font-weight: 600;
}
.shop-ranking__category {
  font-size: 12px;
  color: #8C8C8C;
  margin-bottom: 8px;
}
.shop-ranking__metric-value {
  font-size: 16px;
  font-weight: 600;
  color: #1677FF;
  margin-bottom: 4px;
}
.shop-ranking__metric-value--first {
  font-size: 20px;
}
.shop-ranking__card {
  border-radius: 8px;
}
.shop-ranking__link {
  color: #1677FF;
  cursor: pointer;
}
.shop-ranking__link:hover {
  text-decoration: underline;
}
.shop-ranking__rank {
  display: inline-block;
  min-width: 24px;
  text-align: center;
  font-weight: 600;
}
.shop-ranking__rank--gold {
  color: #FAAD14;
}
.shop-ranking__rank--silver {
  color: #8C8C8C;
}
.shop-ranking__rank--bronze {
  color: #D48806;
}
</style>
