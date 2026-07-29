<script setup lang="ts">
import { ref, computed, onMounted, h } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import {
  Breadcrumb,
  BreadcrumbItem,
  Card,
  Descriptions,
  DescriptionsItem,
  Tag,
  Image as AImage,
  ImagePreviewGroup as AImagePreviewGroup,
  Skeleton,
  Button,
  Space,
  Empty,
  message,
  Modal,
} from 'ant-design-vue'
import {
  CheckCircleOutlined,
  CloseCircleOutlined,
  InboxOutlined,
  ArrowLeftOutlined,
} from '@ant-design/icons-vue'
import { aftersalesApi } from '../api/aftersales.api'
import type {
  AfterSalesDetailDto,
  AfterSalesType,
} from '../types/aftersales.dto'
import { StatusTag, IdempotencyButton, ConfirmDialog, EmptyState } from '@/shared/components'
import { formatDateTime, formatMoney } from '@/shared/utils/format'
import { logger } from '@/shared/utils/logger'
import { ConcurrencyError } from '@/shared/http'
import { vPermission } from '@/shared/auth'

/**
 * 售后详情页
 *
 * 路由：/after-sales/:id
 * - 顶部状态卡片：售后单号、状态、申请时间、版本号
 * - 主体四区：基本信息、商品信息、凭证图片、操作区
 * - Pending：同意 + 拒绝（驳回需填理由）
 * - Approved + ReturnRefund：确认收货
 * - 409 冲突：弹「该售后单已被他人处理，请刷新后重试」对话框
 */

defineOptions({ name: 'AfterSalesDetail' })

const route = useRoute()
const router = useRouter()

const detail = ref<AfterSalesDetailDto | null>(null)
const loading = ref(false)
const approving = ref(false)
const confirmingReturn = ref(false)
const rejectDialogOpen = ref(false)

const TYPE_LABEL: Record<AfterSalesType, string> = {
  RefundOnly: '仅退款',
  ReturnRefund: '退货退款',
  Exchange: '换货',
}

const TYPE_TAG_COLOR: Record<AfterSalesType, 'blue' | 'orange' | 'purple'> = {
  RefundOnly: 'blue',
  ReturnRefund: 'orange',
  Exchange: 'purple',
}

const REJECT_REASON_MIN = 1
const REJECT_REASON_MAX = 200

const detailId = computed(() => String(route.params.id ?? ''))

const isPending = computed(() => detail.value?.status === 'Pending')
const canConfirmReturn = computed(
  () => detail.value?.status === 'Approved' && detail.value?.type === 'ReturnRefund',
)

const hasImages = computed(() => (detail.value?.images?.length ?? 0) > 0)

const unitPrice = computed(() => {
  if (!detail.value || detail.value.quantity === 0) return 0
  return detail.value.amount / detail.value.quantity
})

function currencySymbol(currency: string): string {
  return currency === 'CNY' ? '¥' : '$'
}

async function loadDetail(): Promise<void> {
  if (!detailId.value) return
  loading.value = true
  try {
    const res = await aftersalesApi.get(detailId.value)
    detail.value = res.data
  } catch (e) {
    logger.error('加载售后详情失败', e)
    message.error('加载售后详情失败')
    detail.value = null
  } finally {
    loading.value = false
  }
}

function onApprove(): void {
  if (!detail.value) return
  const version = detail.value.version
  Modal.confirm({
    title: '确认同意售后',
    content: '同意后买家将收到通知，此操作不可撤销。',
    okText: '确认同意',
    cancelText: '取消',
    onOk: async () => {
      approving.value = true
      try {
        await aftersalesApi.approve(detailId.value, version)
        message.success('已同意售后')
        await loadDetail()
      } catch (e) {
        handleOperationError(e)
      } finally {
        approving.value = false
      }
    },
  })
}

function onReject(): void {
  rejectDialogOpen.value = true
}

function onRejectConfirm(reason?: string): void {
  if (!detail.value || !reason) return
  const version = detail.value.version
  rejectDialogOpen.value = false
  void doReject(reason, version)
}

async function doReject(reason: string, version: number): Promise<void> {
  try {
    await aftersalesApi.reject(detailId.value, { reason, version })
    message.success('已驳回售后')
    await loadDetail()
  } catch (e) {
    handleOperationError(e)
  }
}

function onRejectCancel(): void {
  rejectDialogOpen.value = false
}

function onConfirmReturn(): void {
  if (!detail.value) return
  const version = detail.value.version
  Modal.confirm({
    title: '确认收到退货',
    content: '确认收货后售后单将进入退款流程，不可撤销。请确认已实际收到买家退回的商品。',
    okText: '确认收货',
    cancelText: '取消',
    onOk: async () => {
      confirmingReturn.value = true
      try {
        await aftersalesApi.confirmReturn(detailId.value, version)
        message.success('已确认收货')
        await loadDetail()
      } catch (e) {
        handleOperationError(e)
      } finally {
        confirmingReturn.value = false
      }
    },
  })
}

function handleOperationError(e: unknown): void {
  if (e instanceof ConcurrencyError) {
    showConflictDialog()
  } else {
    logger.error('售后操作失败', e)
    message.error(e instanceof Error ? e.message : '操作失败')
  }
}

function showConflictDialog(): void {
  Modal.error({
    title: '操作冲突',
    content: '该售后单已被他人处理，请刷新后重试。',
    okText: '刷新',
    onOk: () => {
      void loadDetail()
    },
  })
}

function goBackToList(): void {
  router.push('/after-sales')
}

onMounted(() => {
  void loadDetail()
})
</script>

<template>
  <div class="aftersales-detail-page">
    <Breadcrumb class="aftersales-detail-breadcrumb">
      <BreadcrumbItem>首页</BreadcrumbItem>
      <BreadcrumbItem>售后处理</BreadcrumbItem>
      <BreadcrumbItem>
        <a class="aftersales-detail-breadcrumb-link" @click="goBackToList">售后列表</a>
      </BreadcrumbItem>
      <BreadcrumbItem>售后详情</BreadcrumbItem>
    </Breadcrumb>

    <Skeleton v-if="loading && !detail" :title="{ width: '60%' }" :paragraph="{ rows: 8 }" active />

    <EmptyState
      v-else-if="!detail"
      description="售后单不存在或无权访问"
      action-text="返回列表"
      @action="goBackToList"
    />

    <template v-else>
      <!-- 顶部状态卡片 -->
      <Card class="aftersales-detail-status-card" :bordered="true">
        <div class="aftersales-detail-status-header">
          <div class="aftersales-detail-status-meta">
            <span class="aftersales-detail-no">{{ detail.afterSalesNo }}</span>
            <StatusTag type="aftersales" :status="detail.status" />
            <Tag :color="TYPE_TAG_COLOR[detail.type]">{{ TYPE_LABEL[detail.type] }}</Tag>
          </div>
          <div class="aftersales-detail-status-extra">
            <div class="aftersales-detail-status-field">
              <span class="aftersales-detail-status-label">申请时间</span>
              <span class="aftersales-detail-status-value">{{ formatDateTime(detail.createdAt) }}</span>
            </div>
            <div class="aftersales-detail-status-field">
              <span class="aftersales-detail-status-label">版本号</span>
              <span class="aftersales-detail-status-value">v{{ detail.version }}</span>
            </div>
          </div>
        </div>
        <div class="aftersales-detail-status-amount">
          <span class="aftersales-detail-status-amount-label">申请金额</span>
          <span class="aftersales-detail-status-amount-value">
            {{ formatMoney(detail.amount, { symbol: currencySymbol(detail.currency) }) }}
          </span>
        </div>
      </Card>

      <div class="aftersales-detail-body">
        <div class="aftersales-detail-main">
          <!-- 1. 基本信息区 -->
          <Card class="aftersales-detail-section" :bordered="true">
            <template #title>
              <span class="aftersales-detail-section-title">基本信息</span>
            </template>
            <Descriptions :column="2" bordered size="small">
              <DescriptionsItem label="售后单号">{{ detail.afterSalesNo }}</DescriptionsItem>
              <DescriptionsItem label="订单号">{{ detail.orderNo }}</DescriptionsItem>
              <DescriptionsItem label="售后类型">
                <Tag :color="TYPE_TAG_COLOR[detail.type]">{{ TYPE_LABEL[detail.type] }}</Tag>
              </DescriptionsItem>
              <DescriptionsItem label="申请金额">
                {{ formatMoney(detail.amount, { symbol: currencySymbol(detail.currency) }) }}
              </DescriptionsItem>
              <DescriptionsItem label="申请原因" :span="2">{{ detail.reason }}</DescriptionsItem>
              <DescriptionsItem label="详细描述" :span="2">
                {{ detail.description || '无' }}
              </DescriptionsItem>
              <DescriptionsItem label="申请时间" :span="2">
                {{ formatDateTime(detail.createdAt) }}
              </DescriptionsItem>
              <DescriptionsItem v-if="detail.rejectReason" label="拒绝原因" :span="2">
                {{ detail.rejectReason }}
              </DescriptionsItem>
            </Descriptions>
          </Card>

          <!-- 2. 商品信息区 -->
          <Card class="aftersales-detail-section" :bordered="true">
            <template #title>
              <span class="aftersales-detail-section-title">商品信息</span>
            </template>
            <Descriptions :column="2" bordered size="small">
              <DescriptionsItem label="商品名称" :span="2">{{ detail.productName }}</DescriptionsItem>
              <DescriptionsItem label="商品编号">{{ detail.productId }}</DescriptionsItem>
              <DescriptionsItem label="SKU 编码">{{ detail.skuId }}</DescriptionsItem>
              <DescriptionsItem label="SKU 名">{{ detail.skuName }}</DescriptionsItem>
              <DescriptionsItem label="数量">{{ detail.quantity }}</DescriptionsItem>
              <DescriptionsItem label="单价">
                {{ formatMoney(unitPrice, { symbol: currencySymbol(detail.currency) }) }}
              </DescriptionsItem>
              <DescriptionsItem label="小计">
                {{ formatMoney(detail.amount, { symbol: currencySymbol(detail.currency) }) }}
              </DescriptionsItem>
            </Descriptions>
          </Card>

          <!-- 3. 凭证图片区 -->
          <Card class="aftersales-detail-section" :bordered="true">
            <template #title>
              <span class="aftersales-detail-section-title">凭证图片</span>
            </template>
            <div v-if="hasImages" class="aftersales-detail-images">
              <AImagePreviewGroup>
                <AImage
                  v-for="img in detail.images"
                  :key="img"
                  :src="img"
                  :width="120"
                  :height="120"
                  class="aftersales-detail-image"
                />
              </AImagePreviewGroup>
            </div>
            <Empty v-else description="无凭证图片" />
          </Card>
        </div>

        <!-- 4. 操作区 -->
        <div class="aftersales-detail-aside">
          <Card class="aftersales-detail-action-card" :bordered="true">
            <template #title>
              <span class="aftersales-detail-section-title">操作</span>
            </template>
            <div class="aftersales-detail-action-body">
              <div class="aftersales-detail-action-status">
                <span class="aftersales-detail-action-status-label">当前状态</span>
                <StatusTag type="aftersales" :status="detail.status" />
              </div>

              <Space v-if="isPending" direction="vertical" class="aftersales-detail-action-buttons">
                <IdempotencyButton
                  v-permission="'aftersales:approve'"
                  type="primary"
                  block
                  :loading="approving"
                  @click="onApprove"
                >
                  <template #icon><CheckCircleOutlined /></template>
                  同意
                </IdempotencyButton>
                <IdempotencyButton
                  v-permission="'aftersales:reject'"
                  danger
                  block
                  @click="onReject"
                >
                  <template #icon><CloseCircleOutlined /></template>
                  拒绝
                </IdempotencyButton>
              </Space>

              <Space
                v-else-if="canConfirmReturn"
                direction="vertical"
                class="aftersales-detail-action-buttons"
              >
                <IdempotencyButton
                  v-permission="'aftersales:confirm-return'"
                  type="primary"
                  block
                  :loading="confirmingReturn"
                  @click="onConfirmReturn"
                >
                  <template #icon><InboxOutlined /></template>
                  确认收货
                </IdempotencyButton>
              </Space>

              <div v-else class="aftersales-detail-action-readonly">
                当前售后单状态为「{{ detail.status }}」，无可执行操作。
              </div>

              <div class="aftersales-detail-action-back">
                <Button type="link" :icon="h(ArrowLeftOutlined)" @click="goBackToList">
                  返回列表
                </Button>
              </div>
            </div>
          </Card>
        </div>
      </div>
    </template>

    <!-- 拒绝原因对话框 -->
    <ConfirmDialog
      :open="rejectDialogOpen"
      danger
      title="确认驳回售后"
      content="驳回后买家可重新发起售后或撤销申请，此操作不可撤销。"
      :require-input="{ label: '驳回原因', min: REJECT_REASON_MIN, max: REJECT_REASON_MAX }"
      @confirm="onRejectConfirm"
      @cancel="onRejectCancel"
    />
  </div>
</template>

<style scoped>
.aftersales-detail-page {
  display: flex;
  flex-direction: column;
  gap: 16px;
}
.aftersales-detail-breadcrumb {
  font-size: 14px;
}
.aftersales-detail-breadcrumb-link {
  color: #1677ff;
  cursor: pointer;
}
.aftersales-detail-breadcrumb-link:hover {
  text-decoration: underline;
}
.aftersales-detail-status-card {
  border-radius: 8px;
}
.aftersales-detail-status-header {
  display: flex;
  align-items: center;
  justify-content: space-between;
  flex-wrap: wrap;
  gap: 16px;
}
.aftersales-detail-status-meta {
  display: inline-flex;
  align-items: center;
  gap: 12px;
}
.aftersales-detail-no {
  font-size: 16px;
  font-weight: 600;
  color: #000000d9;
}
.aftersales-detail-status-extra {
  display: inline-flex;
  align-items: center;
  gap: 24px;
}
.aftersales-detail-status-field {
  display: inline-flex;
  align-items: center;
  gap: 6px;
}
.aftersales-detail-status-label {
  font-size: 13px;
  color: #8c8c8c;
}
.aftersales-detail-status-value {
  font-size: 14px;
  color: #000000d9;
}
.aftersales-detail-status-amount {
  margin-top: 12px;
  padding-top: 12px;
  border-top: 1px solid #f0f0f0;
  display: flex;
  align-items: baseline;
  gap: 12px;
}
.aftersales-detail-status-amount-label {
  font-size: 14px;
  color: #595959;
}
.aftersales-detail-status-amount-value {
  font-size: 24px;
  font-weight: 600;
  color: #fa541c;
}
.aftersales-detail-body {
  display: grid;
  grid-template-columns: minmax(0, 2fr) minmax(280px, 1fr);
  gap: 16px;
  align-items: start;
}
.aftersales-detail-main {
  display: flex;
  flex-direction: column;
  gap: 16px;
}
.aftersales-detail-section {
  border-radius: 8px;
}
.aftersales-detail-section-title {
  font-size: 16px;
  font-weight: 500;
}
.aftersales-detail-images {
  display: flex;
  flex-wrap: wrap;
  gap: 8px;
}
.aftersales-detail-image {
  border-radius: 6px;
  object-fit: cover;
}
.aftersales-detail-aside {
  position: sticky;
  top: 16px;
}
.aftersales-detail-action-card {
  border-radius: 8px;
}
.aftersales-detail-action-body {
  display: flex;
  flex-direction: column;
  gap: 16px;
}
.aftersales-detail-action-status {
  display: flex;
  align-items: center;
  gap: 8px;
}
.aftersales-detail-action-status-label {
  font-size: 14px;
  color: #595959;
}
.aftersales-detail-action-buttons {
  width: 100%;
}
.aftersales-detail-action-buttons :deep(.ant-space-item) {
  width: 100%;
}
.aftersales-detail-action-readonly {
  padding: 12px;
  background: #fafafa;
  border-radius: 6px;
  font-size: 14px;
  color: #8c8c8c;
  text-align: center;
}
.aftersales-detail-action-back {
  text-align: center;
}

@media (max-width: 1199px) {
  .aftersales-detail-body {
    grid-template-columns: 1fr;
  }
  .aftersales-detail-aside {
    position: static;
    order: -1;
  }
}
</style>
