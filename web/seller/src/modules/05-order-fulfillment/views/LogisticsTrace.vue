<script setup lang="ts">
import { ref, computed, onMounted } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import {
  Breadcrumb,
  BreadcrumbItem,
  Card,
  Timeline,
  TimelineItem,
  Button,
  Skeleton,
  Spin,
  Descriptions,
  DescriptionsItem,
  message,
} from 'ant-design-vue'
import { ArrowLeftOutlined, ReloadOutlined, EnvironmentOutlined } from '@ant-design/icons-vue'
import { orderApi } from '../api/order.api'
import type { OrderDetailDto, LogisticsTraceDto, LogisticsTraceNodeDto } from '../types/order.dto'
import { StatusTag, EmptyState } from '@/shared/components'
import { formatDateTime } from '@/shared/utils/format'
import { logger } from '@/shared/utils/logger'

/**
 * 物流轨迹页
 *
 * 顶部展示订单摘要（订单号 / 状态 / 收货人 / 地址 / 物流公司 / 物流单号），
 * 主体为垂直时间轴，按时间倒序（最新在前）展示物流轨迹节点。
 *
 * 数据来源：
 * - orderApi.get(id)：订单详情，提供状态与收货人信息
 * - orderApi.getLogisticsTrace(id)：物流轨迹（端点路径为 /orders/{id}/logistics-trace）
 */

const route = useRoute()
const router = useRouter()

const orderId = computed(() => String(route.params.id ?? ''))

const loading = ref(false)
const orderDetail = ref<OrderDetailDto | null>(null)
const trace = ref<LogisticsTraceDto | null>(null)

/**
 * 按时间倒序排列的轨迹节点（最新在前）
 */
const sortedTrace = computed<LogisticsTraceNodeDto[]>(() => {
  const nodes = trace.value?.trace ?? []
  return [...nodes].sort((a, b) => {
    const ta = new Date(a.time).getTime()
    const tb = new Date(b.time).getTime()
    if (Number.isNaN(ta) || Number.isNaN(tb)) return 0
    return tb - ta
  })
})

const hasTrace = computed(() => sortedTrace.value.length > 0)

async function loadAll(): Promise<void> {
  if (!orderId.value) {
    message.error('订单 ID 缺失')
    return
  }
  loading.value = true
  try {
    // 并行拉取订单详情与物流轨迹；任一失败不阻塞另一份数据展示
    const [detailRes, traceRes] = await Promise.allSettled([
      orderApi.get(orderId.value),
      orderApi.getLogisticsTrace(orderId.value),
    ])
    if (detailRes.status === 'fulfilled') {
      orderDetail.value = detailRes.value.data
    } else {
      logger.error('加载订单详情失败', detailRes.reason)
    }
    if (traceRes.status === 'fulfilled') {
      trace.value = traceRes.value.data
    } else {
      logger.error('加载物流轨迹失败', traceRes.reason)
    }
    if (detailRes.status === 'rejected' && traceRes.status === 'rejected') {
      message.error('加载物流轨迹失败')
    }
  } finally {
    loading.value = false
  }
}

function goBack(): void {
  void router.push('/orders')
}

onMounted(() => {
  void loadAll()
})
</script>

<template>
  <div class="logistics-trace-page">
    <Breadcrumb class="page-breadcrumb">
      <BreadcrumbItem>首页</BreadcrumbItem>
      <BreadcrumbItem>订单履约</BreadcrumbItem>
      <BreadcrumbItem>订单列表</BreadcrumbItem>
      <BreadcrumbItem>物流轨迹</BreadcrumbItem>
    </Breadcrumb>

    <div class="page-header">
      <div>
        <h1 class="page-title">物流轨迹</h1>
        <p class="page-sub">查看已发货订单的物流运输轨迹，掌握包裹进度。</p>
      </div>
      <div class="page-actions">
        <Button @click="loadAll">
          <template #icon>
            <ReloadOutlined />
          </template>
          刷新轨迹
        </Button>
        <Button @click="goBack">
          <template #icon>
            <ArrowLeftOutlined />
          </template>
          返回订单列表
        </Button>
      </div>
    </div>

    <Spin :spinning="loading" tip="加载中...">
      <!-- 订单摘要卡片 -->
      <Card :bordered="true" class="info-card">
        <template #title>
          <span class="card-title-text">订单摘要</span>
        </template>
        <Skeleton
          v-if="loading && !orderDetail && !trace"
          :title="{ width: '40%' }"
          :paragraph="{ rows: 4 }"
          active
        />
        <Descriptions v-else :column="2" bordered size="small">
          <DescriptionsItem label="订单号">
            <span class="order-no">{{ trace?.orderNo ?? orderDetail?.orderNo ?? '-' }}</span>
          </DescriptionsItem>
          <DescriptionsItem label="订单状态">
            <StatusTag
              v-if="orderDetail?.status"
              type="order"
              :status="orderDetail.status"
            />
            <span v-else>-</span>
          </DescriptionsItem>
          <DescriptionsItem label="收货人">
            <template v-if="orderDetail">
              {{ orderDetail.receiverName }}
              <span class="cell-muted">{{ orderDetail.receiverPhone }}</span>
            </template>
            <span v-else>-</span>
          </DescriptionsItem>
          <DescriptionsItem label="收货地址">
            {{ orderDetail?.receiverAddress ?? '-' }}
          </DescriptionsItem>
          <DescriptionsItem label="物流公司">
            {{ trace?.logisticsCompany ?? orderDetail?.logisticsCompany ?? '-' }}
          </DescriptionsItem>
          <DescriptionsItem label="物流单号">
            <span class="order-no">{{ trace?.logisticsNo ?? orderDetail?.logisticsNo ?? '-' }}</span>
          </DescriptionsItem>
        </Descriptions>
      </Card>

      <!-- 物流轨迹时间轴 -->
      <Card :bordered="true" class="trace-card">
        <template #title>
          <span class="card-title-text">物流轨迹</span>
        </template>
        <template v-if="hasTrace" #extra>
          <span class="card-extra">共 {{ sortedTrace.length }} 条记录</span>
        </template>

        <Skeleton
          v-if="loading && !hasTrace"
          :title="{ width: '30%' }"
          :paragraph="{ rows: 6 }"
          active
        />
        <EmptyState v-else-if="!hasTrace" description="暂无物流轨迹" />
        <Timeline v-else class="trace-timeline" aria-label="物流轨迹">
          <TimelineItem
            v-for="(node, idx) in sortedTrace"
            :key="`${node.time}-${idx}`"
            :color="idx === 0 ? 'blue' : 'green'"
          >
            <div class="tl-time">{{ formatDateTime(node.time) }}</div>
            <div class="tl-title">{{ node.description }}</div>
            <div v-if="node.location" class="tl-location">
              <EnvironmentOutlined />
              <span>{{ node.location }}</span>
            </div>
            <div v-if="node.status" class="tl-status">
              <StatusTag type="order" :status="node.status" />
            </div>
          </TimelineItem>
        </Timeline>
      </Card>
    </Spin>
  </div>
</template>

<style scoped>
.logistics-trace-page {
  display: flex;
  flex-direction: column;
  gap: 16px;
}
.page-breadcrumb {
  font-size: 14px;
}
.page-header {
  display: flex;
  align-items: center;
  justify-content: space-between;
  gap: 16px;
}
.page-title {
  font-size: 20px;
  font-weight: 600;
  color: #000000d9;
  margin: 0;
}
.page-sub {
  font-size: 13px;
  color: #8c8c8c;
  margin-top: 4px;
}
.page-actions {
  display: flex;
  gap: 8px;
  flex-shrink: 0;
}
.info-card,
.trace-card {
  border-radius: 8px;
}
.card-title-text {
  font-size: 16px;
  font-weight: 500;
  color: #000000d9;
}
.card-extra {
  font-size: 13px;
  color: #8c8c8c;
}
.order-no {
  font-family: 'SF Mono', Consolas, monospace;
  font-size: 13px;
  color: #1677ff;
}
.cell-muted {
  font-size: 12px;
  color: #8c8c8c;
  margin-left: 4px;
}
.trace-timeline {
  padding: 8px 4px;
}
.tl-time {
  font-size: 13px;
  color: #8c8c8c;
  font-family: 'SF Mono', Consolas, monospace;
  margin-bottom: 4px;
}
.tl-title {
  font-size: 14px;
  color: #000000d9;
  font-weight: 500;
  line-height: 1.5;
}
.tl-location {
  display: inline-flex;
  align-items: center;
  gap: 4px;
  font-size: 12px;
  color: #8c8c8c;
  margin-top: 6px;
}
.tl-status {
  margin-top: 6px;
}
</style>
