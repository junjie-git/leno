<script setup lang="ts">
import { computed, onMounted, onUnmounted, ref } from 'vue'
import { useRouter } from 'vue-router'
import { message } from 'ant-design-vue'
import {
  ShoppingOutlined,
  ShopOutlined,
  CustomerServiceOutlined,
  BellOutlined,
  ThunderboltOutlined,
  GiftOutlined,
  TrophyOutlined,
  MailOutlined,
  ReloadOutlined,
} from '@ant-design/icons-vue'
import { fetchTodoBoard } from '../api/todo.api'
import type { TodoBoardDto, TodoCategoryDto, TodoItemDto } from '../types/account.dto'
import { DashboardCard, EmptyState, IdempotencyButton } from '@/shared/components'
import { formatDateTime } from '@/shared/utils/format'

/**
 * 待办工作台页（09-account，登录后默认首页）
 *
 * - 概览卡片：待审核商品 / 待审核入驻 / 待介入售后 / 待审核评价，点击跳转对应审核页
 * - 待办分类 Tabs：商品 / 卖家 / 订单 / 通知，各含 Top10 列表与跳转处理操作
 * - 快捷操作：新增促销 / 发放优惠券 / 积分规则 / 死信管理
 * - 每 5 分钟 setInterval 自动刷新，onUnmounted 清理定时器
 * - 单分类端点失败降级为空数据：卡片显示 --，Tab 内提示可重试
 * - 提交时间超 24 小时的待办标记红色「超时」tag
 */

/** 自动刷新间隔：5 分钟 */
const AUTO_REFRESH_INTERVAL_MS = 5 * 60 * 1000

/** 超时阈值：24 小时 */
const TIMEOUT_THRESHOLD_MS = 24 * 60 * 60 * 1000

const router = useRouter()

/* ============================== 概览卡片 ============================== */

interface OverviewCardConfig {
  key: keyof TodoBoardDto
  title: string
  description: string
  route: string
}

const overviewCards: OverviewCardConfig[] = [
  {
    key: 'products',
    title: '待审核商品',
    description: '卖家提交的商品审核',
    route: '/products/audit?status=PendingAudit',
  },
  {
    key: 'shops',
    title: '待审核入驻',
    description: '店铺入驻申请审核',
    route: '/sellers/shops?status=PendingReview',
  },
  {
    key: 'afterSales',
    title: '待介入售后',
    description: '需要运营介入的售后单',
    route: '/orders/after-sales?status=PendingIntervention',
  },
  {
    key: 'reviews',
    title: '待审核评价',
    description: '会员评价内容审核',
    route: '/products/reviews?status=Pending',
  },
]

const board = ref<TodoBoardDto | null>(null)
const loading = ref(false)
const lastRefreshedAt = ref<string | null>(null)

/** 是否存在失败分类（用于错误态提示与重试按钮） */
const hasFailedCategory = computed(() => {
  if (!board.value) return false
  return overviewCards.some((c) => board.value?.[c.key].failed) || board.value.notifications.failed
})

/** 卡片数值：失败显示 --，其余显示计数 */
function cardValue(category: TodoCategoryDto | undefined): number | string {
  if (!category || category.failed) return '--'
  return category.total
}

/** 计数 > 0 红色数字，=0 默认色 */
function cardValueColor(category: TodoCategoryDto | undefined): string {
  if (!category || category.failed) return '#8c8c8c'
  return category.total > 0 ? '#1677ff' : '#8c8c8c'
}

function goRoute(route: string) {
  void router.push(route)
}

async function loadBoard() {
  loading.value = true
  try {
    board.value = await fetchTodoBoard()
    lastRefreshedAt.value = new Date().toISOString()
  } catch {
    // fetchTodoBoard 内部已按分类降级，此处兜底理论上不可达
    message.error('加载待办面板失败，请刷新重试')
  } finally {
    loading.value = false
  }
}

/* ============================== 待办分类 Tabs ============================== */

type TabKey = 'product' | 'seller' | 'order' | 'notification'

const activeTab = ref<TabKey>('product')

interface TabConfig {
  key: TabKey
  label: string
  categoryKey: keyof TodoBoardDto
  route: string
}

const tabConfigs: TabConfig[] = [
  { key: 'product', label: '商品', categoryKey: 'products', route: '/products/audit?status=PendingAudit' },
  { key: 'seller', label: '卖家', categoryKey: 'shops', route: '/sellers/shops?status=PendingReview' },
  { key: 'order', label: '订单', categoryKey: 'afterSales', route: '/orders/after-sales?status=PendingIntervention' },
  {
    key: 'notification',
    label: '通知',
    categoryKey: 'notifications',
    route: '/notifications/records?status=DeadLetter',
  },
]

function categoryOf(categoryKey: keyof TodoBoardDto): TodoCategoryDto {
  const empty: TodoCategoryDto = { total: 0, items: [], failed: false }
  return board.value?.[categoryKey] ?? empty
}

/** 超时判定：提交时间距现在超过 24 小时 */
function isTimeout(item: TodoItemDto): boolean {
  if (!item.submittedAt) return false
  const submitted = new Date(item.submittedAt).getTime()
  if (Number.isNaN(submitted)) return false
  return Date.now() - submitted > TIMEOUT_THRESHOLD_MS
}

/** Tab 待办项处理跳转路径（按分类带筛选状态） */
function itemRoute(tab: TabConfig, _item: TodoItemDto): string {
  return tab.route
}

/** 各 Tab 图标与说明 */
const tabMetaMap: Record<TabKey, { icon: typeof ShoppingOutlined; emptyText: string }> = {
  product: { icon: ShoppingOutlined, emptyText: '暂无待审核商品' },
  seller: { icon: ShopOutlined, emptyText: '暂无待审核入驻' },
  order: { icon: CustomerServiceOutlined, emptyText: '暂无待介入售后' },
  notification: { icon: BellOutlined, emptyText: '暂无死信通知' },
}

/* ============================== 快捷操作 ============================== */

interface QuickAction {
  key: string
  label: string
  description: string
  icon: typeof ThunderboltOutlined
  iconClass: string
  route: string
}

const quickActions: QuickAction[] = [
  {
    key: 'promotion',
    label: '新增促销',
    description: '创建促销活动',
    icon: ThunderboltOutlined,
    iconClass: 'quick-action-icon--promo',
    route: '/promotions/activities/create',
  },
  {
    key: 'coupon',
    label: '发放优惠券',
    description: '批量发放优惠券',
    icon: GiftOutlined,
    iconClass: 'quick-action-icon--coupon',
    route: '/promotions/coupons/grant',
  },
  {
    key: 'points',
    label: '积分规则',
    description: '维护积分规则',
    icon: TrophyOutlined,
    iconClass: 'quick-action-icon--points',
    route: '/members/points-rules',
  },
  {
    key: 'deadletter',
    label: '死信管理',
    description: '重发失败通知',
    icon: MailOutlined,
    iconClass: 'quick-action-icon--deadletter',
    route: '/notifications/records?status=DeadLetter',
  },
]

function onQuickAction(action: QuickAction) {
  void router.push(action.route)
}

/* ============================== 自动刷新 ============================== */

let refreshTimer: number | undefined

onMounted(() => {
  loadBoard()
  refreshTimer = window.setInterval(() => {
    loadBoard()
  }, AUTO_REFRESH_INTERVAL_MS)
})

onUnmounted(() => {
  if (refreshTimer !== undefined) {
    window.clearInterval(refreshTimer)
    refreshTimer = undefined
  }
})
</script>

<template>
  <div class="todo-workbench">
    <div class="page-header">
      <div class="page-header-left">
        <h1>待办工作台</h1>
        <p class="sub">聚合各业务域待办事项，按优先级展示，提供快捷处理入口。每 5 分钟自动刷新。</p>
      </div>
      <div class="page-header-right">
        <span v-if="lastRefreshedAt" class="refresh-hint">
          上次刷新：{{ formatDateTime(lastRefreshedAt) }}
        </span>
        <IdempotencyButton :loading="loading" @click="loadBoard">
          <ReloadOutlined />
          立即刷新
        </IdempotencyButton>
      </div>
    </div>

    <!-- 区域 A：概览卡片（4 列） -->
    <div class="overview-grid">
      <DashboardCard
        v-for="card in overviewCards"
        :key="card.key"
        :title="card.title"
        :value="cardValue(board?.[card.key])"
        :description="card.description"
        :value-color="cardValueColor(board?.[card.key])"
        :loading="loading && !board"
        :tooltip="board?.[card.key]?.failed ? '该分类加载失败，点击立即刷新重试' : ''"
        :aria-label="`${card.title} ${cardValue(board?.[card.key])} 条`"
        @click="goRoute(card.route)"
      />
    </div>

    <!-- 加载失败提示条（部分分类降级时展示，可重试） -->
    <a-alert
      v-if="hasFailedCategory && !loading"
      type="warning"
      show-icon
      message="部分待办分类加载失败，对应卡片显示为 --"
      description="单点接口异常不影响其他分类展示，可点击右侧按钮重试。"
      closable
      class="failed-alert"
    >
      <template #action>
        <a-button size="small" type="primary" @click="loadBoard">重试</a-button>
      </template>
    </a-alert>

    <!-- 区域 B：待办分类列表 -->
    <a-card :bordered="false" class="todo-card">
      <template #title>
        待办列表
        <span class="sub-title">按优先级排序，Top 10 待办项</span>
      </template>

      <a-tabs v-model:activeKey="activeTab">
        <a-tab-pane v-for="tab in tabConfigs" :key="tab.key">
          <template #tab>
            <span :aria-label="`${tab.label}待办 ${categoryOf(tab.categoryKey).total} 条`">
              <component :is="tabMetaMap[tab.key].icon" />
              {{ tab.label }}
              <a-badge
                :count="categoryOf(tab.categoryKey).total"
                :number-style="categoryOf(tab.categoryKey).total > 0 ? { backgroundColor: '#ff4d4f' } : { backgroundColor: '#d9d9d9', color: '#595959', boxShadow: '0 0 0 1px #d9d9d9 inset' }"
                :overflow-count="99"
                class="tab-badge"
              />
            </span>
          </template>

          <a-spin :spinning="loading">
            <!-- 该分类端点失败：错误态 + 重试 -->
            <div v-if="categoryOf(tab.categoryKey).failed" class="state-block">
              <EmptyState
                :description="`${tab.label}待办加载失败`"
                action-text="重新加载"
                @action="loadBoard"
              />
            </div>
            <!-- 空数据 -->
            <EmptyState
              v-else-if="categoryOf(tab.categoryKey).items.length === 0"
              :description="tabMetaMap[tab.key].emptyText"
            />
            <!-- 正常列表 -->
            <ul v-else class="todo-list">
              <li
                v-for="item in categoryOf(tab.categoryKey).items"
                :key="item.id"
                class="todo-item"
                :aria-label="`待办：${item.title}`"
              >
                <div class="todo-body">
                  <div class="todo-header">
                    <div class="todo-title">
                      <span class="todo-title__text">{{ item.title }}</span>
                      <a-tag v-if="isTimeout(item)" color="error">超时</a-tag>
                    </div>
                    <span class="todo-time">{{ formatDateTime(item.submittedAt) }}</span>
                  </div>
                  <div class="todo-footer">
                    <span class="todo-source">来源：{{ item.source ?? '—' }}</span>
                    <span class="todo-id">{{ item.id }}</span>
                    <div class="todo-actions">
                      <a-button
                        type="primary"
                        size="small"
                        @click="goRoute(itemRoute(tab, item))"
                      >
                        立即处理
                      </a-button>
                    </div>
                  </div>
                </div>
              </li>
            </ul>
          </a-spin>
        </a-tab-pane>
      </a-tabs>
    </a-card>

    <!-- 区域 C：快捷操作 -->
    <a-card :bordered="false" class="quick-card">
      <template #title>
        <ThunderboltOutlined class="quick-title-icon" />
        快捷操作
      </template>
      <div class="quick-actions">
        <div
          v-for="action in quickActions"
          :key="action.key"
          class="quick-action"
          :aria-label="`快捷操作：${action.label}`"
          @click="onQuickAction(action)"
        >
          <div :class="['quick-action-icon', action.iconClass]">
            <component :is="action.icon" />
          </div>
          <div class="quick-action-label">{{ action.label }}</div>
          <div class="quick-action-desc">{{ action.description }}</div>
        </div>
      </div>
    </a-card>
  </div>
</template>

<style scoped>
.todo-workbench {
  display: flex;
  flex-direction: column;
  gap: 16px;
}

.page-header {
  display: flex;
  align-items: flex-start;
  justify-content: space-between;
  gap: 16px;
}

.page-header h1 {
  margin: 0;
  font-size: 20px;
  font-weight: 600;
  color: #000000d9;
  display: flex;
  align-items: center;
  gap: 8px;
}

.page-header .sub {
  margin: 4px 0 0;
  font-size: 12px;
  color: #8c8c8c;
}

.page-header-right {
  display: flex;
  align-items: center;
  gap: 12px;
  flex-shrink: 0;
}

.refresh-hint {
  font-size: 12px;
  color: #8c8c8c;
}

/* 概览卡片 */
.overview-grid {
  display: grid;
  grid-template-columns: repeat(4, 1fr);
  gap: 16px;
}

.failed-alert {
  border-radius: 8px;
}

/* 待办列表 */
.todo-card {
  border-radius: 8px;
}

.sub-title {
  margin-left: 8px;
  font-size: 12px;
  font-weight: 400;
  color: #8c8c8c;
}

.tab-badge {
  margin-left: 6px;
}

.state-block {
  padding: 32px 0;
}

.todo-list {
  list-style: none;
  margin: 0;
  padding: 0;
  display: flex;
  flex-direction: column;
  gap: 12px;
}

.todo-item {
  padding: 12px 16px;
  border: 1px solid #f0f0f0;
  border-radius: 6px;
  transition: border-color 0.15s, background-color 0.15s;
}

.todo-item:hover {
  border-color: #1677ff;
  background: #fafcff;
}

.todo-body {
  min-width: 0;
}

.todo-header {
  display: flex;
  align-items: center;
  justify-content: space-between;
  gap: 12px;
}

.todo-title {
  display: flex;
  align-items: center;
  gap: 8px;
  min-width: 0;
}

.todo-title__text {
  font-size: 14px;
  font-weight: 500;
  color: #000000d9;
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
}

.todo-time {
  font-size: 12px;
  color: #8c8c8c;
  flex-shrink: 0;
}

.todo-footer {
  display: flex;
  align-items: center;
  gap: 12px;
  margin-top: 8px;
}

.todo-source {
  font-size: 12px;
  color: #8c8c8c;
}

.todo-id {
  font-size: 12px;
  color: #8c8c8c;
  font-family: 'SF Mono', Consolas, monospace;
}

.todo-actions {
  margin-left: auto;
}

/* 快捷操作 */
.quick-card {
  border-radius: 8px;
}

.quick-title-icon {
  color: #1677ff;
  margin-right: 8px;
}

.quick-actions {
  display: grid;
  grid-template-columns: repeat(4, 1fr);
  gap: 12px;
}

.quick-action {
  display: flex;
  flex-direction: column;
  align-items: center;
  justify-content: center;
  gap: 8px;
  padding: 16px;
  border: 1px solid #d9d9d9;
  border-radius: 8px;
  background: #ffffff;
  cursor: pointer;
  transition: all 0.2s;
}

.quick-action:hover {
  border-color: #1677ff;
  background: #fafcff;
  transform: translateY(-2px);
  box-shadow: 0 1px 2px 0 rgba(0, 0, 0, 0.03), 0 2px 6px -1px rgba(0, 0, 0, 0.04);
}

.quick-action-icon {
  width: 44px;
  height: 44px;
  border-radius: 6px;
  display: flex;
  align-items: center;
  justify-content: center;
  font-size: 20px;
}

.quick-action-icon--promo {
  background: #e6f4ff;
  color: #1677ff;
}

.quick-action-icon--coupon {
  background: #f6ffed;
  color: #52c41a;
}

.quick-action-icon--points {
  background: #fff7e6;
  color: #faad14;
}

.quick-action-icon--deadletter {
  background: #fff2f0;
  color: #ff4d4f;
}

.quick-action-label {
  font-size: 14px;
  font-weight: 500;
  color: #000000d9;
}

.quick-action-desc {
  font-size: 12px;
  color: #8c8c8c;
  text-align: center;
}

/* 响应式：992-1199px 概览/快捷 2 列，<992px 1 列 */
@media (max-width: 1199px) {
  .overview-grid,
  .quick-actions {
    grid-template-columns: repeat(2, 1fr);
  }
}

@media (max-width: 991px) {
  .overview-grid,
  .quick-actions {
    grid-template-columns: 1fr;
  }

  .page-header {
    flex-direction: column;
  }
}
</style>
