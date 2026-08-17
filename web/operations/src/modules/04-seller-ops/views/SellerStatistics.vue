<!-- web/operations/src/modules/04-seller-ops/views/SellerStatistics.vue -->
<template>
  <div class="seller-statistics">
    <!-- 区域 A：时间预设 + 自定义区间 + 类目筛选 -->
    <a-card :bordered="false" class="filter-card">
      <div class="filter-bar">
        <a-space wrap>
          <a-radio-group
            v-model:value="preset"
            :options="presetOptions"
            option-type="button"
            @change="onPresetChange"
          />
          <span class="filter-label">自定义时间</span>
          <DateTimeRangePicker :value="rangeValue" @change="onRangeChange" />
          <span class="filter-label">主营类目</span>
          <a-select
            v-model:value="category"
            placeholder="全部类目"
            allow-clear
            style="width: 150px"
            :options="categoryOptions"
            @change="onFilterChange"
          />
          <a-button :loading="loading" @click="fetchStats">刷新</a-button>
        </a-space>
      </div>
    </a-card>

    <!-- 区域 B：4 张指标卡片 -->
    <a-row :gutter="16" class="cards-row">
      <a-col :xs="24" :sm="12" :lg="6">
        <DashboardCard
          title="卖家总数"
          :value="overview ? overview.totalSellers : '--'"
          :loading="loading"
          tooltip="统计口径：平台全部注册卖家（含暂停/关闭）"
        />
      </a-col>
      <a-col :xs="24" :sm="12" :lg="6">
        <DashboardCard
          title="活跃卖家"
          :value="overview ? overview.activeSellers : '--'"
          :loading="loading"
          tooltip="统计口径：状态为「已通过」且正常营业的卖家"
          :value-color="overview && overview.activeSellers > 0 ? '#52C41A' : ''"
        />
      </a-col>
      <a-col :xs="24" :sm="12" :lg="6">
        <DashboardCard
          title="新增卖家"
          :value="overview ? overview.newSellers : '--'"
          :loading="loading"
          :description="overview ? `统计周期内新入驻 ${overview.newSellers} 家` : ''"
        />
      </a-col>
      <a-col :xs="24" :sm="12" :lg="6">
        <DashboardCard
          title="平均评分"
          :value="overview ? overview.avgRating.toFixed(1) : '--'"
          :loading="loading"
          tooltip="统计口径：全部在册卖家店铺评分均值（0-5）"
          :value-color="overview ? ratingColor(overview.avgRating) : ''"
        />
      </a-col>
    </a-row>

    <!-- 区域 C + D：Top10 排行 + 类目分布 -->
    <a-row :gutter="16">
      <a-col :xs="24" :lg="14">
        <a-card title="Top 10 卖家排行（按 GMV 降序）" :bordered="false">
          <ChartBarHorizontal
            :categories="topCategories"
            :values="topValues"
            series-name="GMV"
            :loading="loading"
            :value-formatter="formatGmvShort"
            top-highlight
            :height="320"
          />
        </a-card>
      </a-col>
      <a-col :xs="24" :lg="10">
        <a-card title="类目分布" :bordered="false">
          <ChartDonut
            :data="categoryChartData"
            :center-value="overview ? overview.totalSellers : undefined"
            center-label="总卖家"
            :loading="loading"
            :height="320"
          />
        </a-card>
      </a-col>
    </a-row>

    <!-- 区域 E：卖家明细表格 -->
    <a-card :bordered="false" class="table-card" title="卖家明细">
      <div v-if="errorMessage" class="table-error">
        <EmptyState :description="`加载失败：${errorMessage}`" action-text="重试" @action="fetchStats" />
      </div>
      <a-table
        v-else
        :columns="columns"
        :data-source="rows"
        :loading="loading"
        :pagination="tablePagination"
        :row-key="(record: SellerStatsSellerRowDto) => record.shopId"
        :row-class-name="rowClassName"
        :custom-row="customRow"
        :scroll="{ x: 1200 }"
        @change="onTableChange"
      >
        <template #emptyText>
          <EmptyState description="暂无卖家统计数据" />
        </template>
        <template #bodyCell="{ column, record }">
          <template v-if="column.key === 'seller'">
            <div class="shop-cell">
              <div class="cell-main" :title="record.name">{{ record.name }}</div>
              <div class="cell-sub">{{ record.sellerAccount }}</div>
            </div>
          </template>
          <template v-else-if="column.key === 'gmv'">
            {{ formatMoney(record.gmv as number) }}
          </template>
          <template v-else-if="column.key === 'rating'">
            <span class="rating-value" :style="{ color: ratingColor(record.rating as number) }">
              {{ Number(record.rating).toFixed(1) }}
            </span>
            <a-tag v-if="record.needsGovernance" color="error" class="governance-tag">
              <WarningOutlined /> 待治理
            </a-tag>
          </template>
          <template v-else-if="column.key === 'status'">
            <a-tag :color="SHOP_STATUS_META[record.status as ShopStatus].color">
              {{ SHOP_STATUS_META[record.status as ShopStatus].label }}
            </a-tag>
          </template>
          <template v-else-if="column.key === 'action'">
            <a-button
              type="link"
              size="small"
              aria-label="查看店铺治理"
              @click.stop="goGovernance(record)"
            >
              查看治理
            </a-button>
          </template>
        </template>
      </a-table>
    </a-card>
  </div>
</template>

<script setup lang="ts">
import { computed, onMounted, reactive, ref, watch } from 'vue'
import { useRouter } from 'vue-router'
import type { TableColumnsType } from 'ant-design-vue'
import { WarningOutlined } from '@ant-design/icons-vue'
import dayjs from 'dayjs'
import { DashboardCard, DateTimeRangePicker, EmptyState } from '@/shared/components'
import { formatMoney } from '@/shared/utils/format'
import { fetchSellerStatsOverview } from '../api/sellerStats.api'
import type {
  SellerStatsOverviewDto,
  SellerStatsSellerRowDto,
  ShopStatus,
} from '../types/shop.dto'
import ChartBarHorizontal from '../components/ChartBarHorizontal.vue'
import ChartDonut from '../components/ChartDonut.vue'

/**
 * 卖家统计页（04-seller-ops）
 *
 * 看板页：时间预设（今日/近7天/近30天/本月）+ 自定义区间 + 类目筛选，
 * 4 张指标卡 + Top10 GMV 横向柱状图 + 类目分布环形图 + 卖家明细表。
 * - 数据来自 fetchSellerStatsOverview 前端降级聚合（shop-ranking + 全量店铺）
 * - 评分 < 4.0 行高亮 #FFF1F0 并标记「待治理」
 * - 行点击 / 操作列跳转店铺治理页（携带 shopId）
 */

/** 时间预设类型（'' 表示自定义区间） */
type TimePreset = 'today' | 'last7days' | 'last30days' | 'thisMonth' | ''

/** 店铺状态展示映射（与 ShopGovernance 保持一致） */
const SHOP_STATUS_META: Record<ShopStatus, { label: string; color: string }> = {
  PendingReview: { label: '待审核', color: 'warning' },
  Active: { label: '已通过', color: 'success' },
  Rejected: { label: '已驳回', color: 'error' },
  Suspended: { label: '已暂停', color: 'warning' },
  Closed: { label: '已关闭', color: 'default' },
}

const presetOptions: { label: string; value: Exclude<TimePreset, ''> }[] = [
  { label: '今日', value: 'today' },
  { label: '近7天', value: 'last7days' },
  { label: '近30天', value: 'last30days' },
  { label: '本月', value: 'thisMonth' },
]

/** 主营类目筛选（与店铺治理页口径一致） */
const categoryOptions: { label: string; value: string }[] = [
  { label: '数码电器', value: '数码电器' },
  { label: '服饰鞋包', value: '服饰鞋包' },
  { label: '美妆个护', value: '美妆个护' },
  { label: '食品生鲜', value: '食品生鲜' },
  { label: '家居日用', value: '家居日用' },
]

const router = useRouter()

// ---------- 时间与类目筛选 ----------
const preset = ref<TimePreset>('last30days')
const rangeValue = ref<[string, string]>(computeRange('last30days'))
const category = ref<string | undefined>(undefined)

/** 按预设计算 [start, end]（ISO 8601 UTC，end 为当前时刻） */
function computeRange(p: Exclude<TimePreset, ''>): [string, string] {
  const now = dayjs()
  switch (p) {
    case 'today':
      return [now.startOf('day').toISOString(), now.toISOString()]
    case 'last7days':
      return [now.subtract(7, 'day').toISOString(), now.toISOString()]
    case 'last30days':
      return [now.subtract(30, 'day').toISOString(), now.toISOString()]
    case 'thisMonth':
      return [now.startOf('month').toISOString(), now.toISOString()]
  }
}

function onPresetChange() {
  if (preset.value) {
    rangeValue.value = computeRange(preset.value)
    fetchStats()
  }
}

/** 自定义区间覆盖预设（preset 置空取消单选高亮） */
function onRangeChange(value: [string, string]) {
  preset.value = ''
  rangeValue.value = value
  fetchStats()
}

function onFilterChange() {
  fetchStats()
}

// ---------- 统计数据加载 ----------
const overview = ref<SellerStatsOverviewDto | null>(null)
const loading = ref(false)
const errorMessage = ref('')

async function fetchStats() {
  loading.value = true
  errorMessage.value = ''
  try {
    overview.value = await fetchSellerStatsOverview({
      start: rangeValue.value[0],
      end: rangeValue.value[1],
      category: category.value || undefined,
    })
  } catch (e) {
    overview.value = null
    errorMessage.value = e instanceof Error ? e.message : '加载卖家统计失败'
  } finally {
    loading.value = false
  }
}

// ---------- 图表数据 ----------
const topCategories = computed(() => overview.value?.topShops.map((s) => s.shopName) ?? [])
const topValues = computed(() => overview.value?.topShops.map((s) => s.gmv) ?? [])
const categoryChartData = computed(
  () =>
    overview.value?.categoryDistribution.map((c) => ({ name: c.category, value: c.count })) ?? [],
)

/** GMV 短格式（柱状图标签：万为单位缩写） */
function formatGmvShort(value: number): string {
  if (!Number.isFinite(value)) return '¥0'
  if (Math.abs(value) >= 100000000) return `¥${(value / 100000000).toFixed(1)}亿`
  if (Math.abs(value) >= 10000) return `¥${(value / 10000).toFixed(1)}万`
  return `¥${value.toLocaleString('zh-CN')}`
}

/** 评分色阶（与店铺治理页一致） */
function ratingColor(rating: number): string {
  if (rating >= 4.5) return '#52C41A'
  if (rating >= 4.0) return '#FAAD14'
  return '#FF4D4F'
}

// ---------- 明细表格（聚合数据源前端分页） ----------
const rows = computed(() => overview.value?.items ?? [])

const tablePagination = reactive({
  current: 1,
  pageSize: 20,
  total: 0,
  showSizeChanger: true,
  showTotal: (total: number) => `共 ${total} 条`,
})

const columns: TableColumnsType = [
  { title: '卖家', key: 'seller', width: 200, ellipsis: true },
  { title: '类目', dataIndex: 'category', key: 'category', width: 110 },
  { title: 'GMV', key: 'gmv', width: 130, align: 'right' },
  { title: '订单数', dataIndex: 'orderCount', key: 'orderCount', width: 90, align: 'center' },
  { title: '商品数', dataIndex: 'productCount', key: 'productCount', width: 90, align: 'center' },
  { title: '评分', key: 'rating', width: 130, align: 'center' },
  { title: '状态', key: 'status', width: 100 },
  { title: '操作', key: 'action', width: 110, fixed: 'right' },
]

/** 评分 < 4.0 行高亮（md §6：#FFF1F0 背景） */
function rowClassName(record: SellerStatsSellerRowDto): string {
  return record.needsGovernance ? 'row-needs-governance' : ''
}

/** 行点击跳转店铺治理（携带 shopId） */
function customRow(record: SellerStatsSellerRowDto) {
  return {
    onClick: () => goGovernance(record),
  }
}

function goGovernance(record: SellerStatsSellerRowDto) {
  void router.push({ path: '/seller-ops/shop-governance', query: { shopId: record.shopId } })
}

function onTableChange(pag: { current?: number; pageSize?: number }) {
  if (pag.current !== undefined) tablePagination.current = pag.current
  if (pag.pageSize !== undefined) tablePagination.pageSize = pag.pageSize
}

// 同步聚合总数到分页器（数据源变化时回到第一页）
watch(
  () => rows.value.length,
  (total) => {
    tablePagination.total = total
    tablePagination.current = 1
  },
  { immediate: true },
)

// ---------- 初始化（默认近 30 天） ----------
onMounted(() => {
  fetchStats()
})
</script>

<style scoped>
.seller-statistics {
  display: flex;
  flex-direction: column;
  gap: 16px;
}

.filter-card :deep(.ant-card-body) {
  padding: 16px 24px;
}

.filter-bar {
  display: flex;
  align-items: center;
  flex-wrap: wrap;
  row-gap: 8px;
}

.filter-label {
  font-size: 14px;
  color: #595959;
}

.cards-row {
  margin-bottom: 0;
}

.table-card :deep(.ant-card-body) {
  padding: 16px;
}

.table-card :deep(.row-needs-governance) td {
  background: #fff1f0;
}

.table-error {
  padding: 24px;
  text-align: center;
}

.shop-cell {
  display: flex;
  flex-direction: column;
}

.cell-main {
  font-size: 14px;
  font-weight: 500;
  color: #000000d9;
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
}

.cell-sub {
  font-size: 12px;
  color: #8c8c8c;
}

.rating-value {
  font-size: 14px;
  font-weight: 600;
  margin-right: 4px;
}

.governance-tag {
  margin-left: 4px;
  cursor: default;
}

.table-card :deep(.ant-table-row) {
  cursor: pointer;
}
</style>
