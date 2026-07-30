<script setup lang="ts">
import { ref, reactive, computed, onMounted, watch } from 'vue'
import { useRouter } from 'vue-router'
import {
  Breadcrumb,
  BreadcrumbItem,
  Card,
  Row,
  Col,
  Statistic,
  Tag,
  Select,
  InputSearch,
  Avatar,
  Rate,
  Typography,
  Image as AImage,
  Drawer,
  Form,
  FormItem,
  Input,
  Button,
  Space,
  Spin,
  Skeleton,
  message,
  Modal,
} from 'ant-design-vue'
import { UserOutlined } from '@ant-design/icons-vue'
import { reviewApi } from '../api/review.api'
import type {
  ReviewDto,
  ReviewQueryParams,
  ReviewStatus,
} from '../types/review.dto'
import { StatusTag, EmptyState, IdempotencyButton, DateTimeRangePicker } from '@/shared/components'
import { logger } from '@/shared/utils/logger'
import { formatDateTime } from '@/shared/utils/format'

/**
 * 评价回复页
 *
 * 路由 /reviews，权限 review:list
 * 严格遵循设计稿：评价卡片 4 区（头部/商品快照/正文/回复区）+ 回复抽屉 480px。
 * 新 BC 路径 /api/seller/reviews。
 */

const router = useRouter()

const loading = ref(false)
const replying = ref(false)
const reviews = ref<ReviewDto[]>([])
const total = ref(0)

const page = ref(1)
const pageSize = ref(20)

const filters = reactive({
  rating: undefined as number | undefined,
  replied: undefined as boolean | undefined,
  productName: '',
  dateRange: undefined as [string, string] | undefined,
})

// 回复抽屉
const replyDrawerOpen = ref(false)
const replyingReview = ref<ReviewDto | null>(null)
const replyContent = ref('')
const replyOriginalContent = ref('')
const isEditingReply = computed(() => !!replyingReview.value?.sellerReplyContent)

// 防抖
let debounceTimer: ReturnType<typeof setTimeout> | null = null

const ratingOptions = [
  { label: '全部', value: undefined },
  { label: '5 星', value: 5 },
  { label: '4 星', value: 4 },
  { label: '3 星', value: 3 },
  { label: '2 星', value: 2 },
  { label: '1 星', value: 1 },
]

const repliedOptions = [
  { label: '全部', value: undefined },
  { label: '待回复', value: false },
  { label: '已回复', value: true },
]

// 统计
const positiveRate = computed(() => {
  if (reviews.value.length === 0) return 0
  const positive = reviews.value.filter((r) => r.rating >= 4).length
  return Math.round((positive / reviews.value.length) * 100)
})

const pendingReplyCount = computed(
  () => reviews.value.filter((r) => !r.sellerReplyContent).length,
)

const replyCharCount = computed(() => replyContent.value.length)
const replyChanged = computed(() => replyContent.value !== replyOriginalContent.value)

function buildParams(): ReviewQueryParams {
  const params: ReviewQueryParams = {
    page: page.value,
    pageSize: pageSize.value,
  }
  if (filters.rating !== undefined) params.rating = filters.rating
  if (filters.replied !== undefined) params.replied = filters.replied
  if (filters.productName.trim()) params.productName = filters.productName.trim()
  if (filters.dateRange) {
    params.startDate = filters.dateRange[0]
    params.endDate = filters.dateRange[1]
  }
  return params
}

async function loadList(): Promise<void> {
  loading.value = true
  try {
    const result = await reviewApi.list(buildParams())
    reviews.value = result.items
    total.value = result.total
  } catch (e) {
    logger.error('加载评价列表失败', e)
    message.error('加载评价列表失败')
    reviews.value = []
    total.value = 0
  } finally {
    loading.value = false
  }
}

function onSearch(): void {
  page.value = 1
  void loadList()
}

function onProductNameInput(): void {
  if (debounceTimer) clearTimeout(debounceTimer)
  debounceTimer = setTimeout(() => {
    onSearch()
  }, 300)
}

function onDateRangeChange(value: [string, string]): void {
  filters.dateRange = value
  onSearch()
}

function openReplyDrawer(review: ReviewDto): void {
  replyingReview.value = review
  replyContent.value = review.sellerReplyContent || ''
  replyOriginalContent.value = review.sellerReplyContent || ''
  replyDrawerOpen.value = true
}

function closeReplyDrawer(): void {
  if (replyChanged.value) {
    Modal.confirm({
      title: '确认放弃当前编辑内容？',
      content: '您有未保存的回复内容，关闭后将丢失。',
      okText: '放弃',
      cancelText: '继续编辑',
      okType: 'danger',
      onOk: () => {
        replyDrawerOpen.value = false
        replyingReview.value = null
        replyContent.value = ''
        replyOriginalContent.value = ''
      },
    })
    return
  }
  replyDrawerOpen.value = false
  replyingReview.value = null
}

async function onSubmitReply(): Promise<void> {
  if (!replyingReview.value) return
  const content = replyContent.value.trim()
  if (content.length === 0) {
    message.warning('回复内容不能为空')
    return
  }
  if (content.length > 500) {
    message.warning('回复内容不超过 500 字')
    return
  }
  replying.value = true
  try {
    const updated = await reviewApi.reply(replyingReview.value.reviewId, { content })
    reviews.value = reviews.value.map((r) =>
      r.reviewId === updated.reviewId ? updated : r,
    )
    message.success(isEditingReply.value ? '回复已更新' : '回复成功')
    replyDrawerOpen.value = false
    replyingReview.value = null
    replyContent.value = ''
    replyOriginalContent.value = ''
  } catch (e) {
    logger.error('回复评价失败', e)
    message.error('回复失败，请稍后重试')
  } finally {
    replying.value = false
  }
}

function goEditProduct(spuId: string): void {
  router.push(`/products/${spuId}/edit`)
}

onMounted(() => {
  void loadList()
})

watch(
  () => [filters.rating, filters.replied] as [number | undefined, boolean | undefined],
  () => {
    onSearch()
  },
)
</script>

<template>
  <div class="review-reply-page">
    <Breadcrumb class="review-reply-breadcrumb">
      <BreadcrumbItem>评价回复</BreadcrumbItem>
    </Breadcrumb>

    <!-- 顶部统计区 -->
    <Card class="review-reply-stats-card" :bordered="true" size="small">
      <Row :gutter="24">
        <Col :span="8">
          <Statistic title="好评率（≥4 星）" :value="positiveRate" suffix="%" />
        </Col>
        <Col :span="8">
          <div class="review-reply-stat-box">
            <div class="review-reply-stat-label">待回复</div>
            <Tag v-if="pendingReplyCount > 0" color="red">
              {{ pendingReplyCount }} 条
            </Tag>
            <Tag v-else color="success">无待回复</Tag>
          </div>
        </Col>
        <Col :span="8">
          <div class="review-reply-stat-box">
            <div class="review-reply-stat-label">总评价数</div>
            <span class="review-reply-stat-value">{{ total }}</span>
          </div>
        </Col>
      </Row>
    </Card>

    <!-- 筛选栏 -->
    <Card class="review-reply-filter-card" :bordered="true" size="small">
      <Row :gutter="12">
        <Col :span="4">
          <div class="review-reply-filter-label">评分</div>
          <Select
            v-model:value="filters.rating"
            :options="ratingOptions"
            style="width: 100%"
            placeholder="评分"
            allow-clear
          />
        </Col>
        <Col :span="4">
          <div class="review-reply-filter-label">回复状态</div>
          <Select
            v-model:value="filters.replied"
            :options="repliedOptions"
            style="width: 100%"
            placeholder="回复状态"
            allow-clear
          />
        </Col>
        <Col :span="6">
          <div class="review-reply-filter-label">商品名称</div>
          <InputSearch
            v-model:value="filters.productName"
            placeholder="搜索商品名称"
            allow-clear
            @input="onProductNameInput"
            @search="onSearch"
          />
        </Col>
        <Col :span="10">
          <div class="review-reply-filter-label">评价时间范围</div>
          <DateTimeRangePicker
            :value="filters.dateRange"
            :show-time="true"
            @change="onDateRangeChange"
          />
        </Col>
      </Row>
    </Card>

    <!-- 评价卡片列表 -->
    <Spin :spinning="loading && reviews.length > 0">
      <Skeleton v-if="loading && reviews.length === 0" active :paragraph="{ rows: 8 }" />
      <EmptyState
        v-else-if="reviews.length === 0"
        description="暂无评价"
      />
      <div v-else class="review-reply-list">
        <Card
          v-for="review in reviews"
          :key="review.reviewId"
          class="review-reply-card"
          :bordered="true"
          size="small"
        >
          <!-- C1 头部 -->
          <div class="review-reply-card-header">
            <div class="review-reply-card-header-left">
              <Avatar :size="36">
                <UserOutlined />
              </Avatar>
              <span class="review-reply-card-user">{{ review.userMaskedName }}</span>
              <Rate :value="review.rating" disabled allow-half size="small" />
              <span class="review-reply-card-time">
                {{ formatDateTime(review.submittedAt) }}
              </span>
            </div>
            <StatusTag type="review" :status="review.status as ReviewStatus" />
          </div>

          <!-- C2 商品快照 -->
          <div
            class="review-reply-card-product"
            @click="goEditProduct(review.spuId)"
          >
            <AImage
              v-if="review.productImage"
              :src="review.productImage"
              :width="48"
              :height="48"
              class="review-reply-card-product-img"
            />
            <div v-else class="review-reply-card-product-img-placeholder">
              <UserOutlined />
            </div>
            <div class="review-reply-card-product-info">
              <div class="review-reply-card-product-name">
                {{ review.productName || '—' }}
              </div>
              <div class="review-reply-card-product-spec">
                {{ review.skuSpec || '—' }}
              </div>
            </div>
          </div>

          <!-- C3 正文 -->
          <div class="review-reply-card-body">
            <Typography.Paragraph
              :ellipsis="{ rows: 3, expandable: true, symbol: '展开' }"
            >
              {{ review.content }}
            </Typography.Paragraph>
            <div v-if="review.images.length > 0" class="review-reply-card-images">
              <AImage.PreviewGroup>
                <AImage
                  v-for="(img, idx) in review.images"
                  :key="idx"
                  :src="img"
                  :width="80"
                  :height="80"
                  class="review-reply-card-image-thumb"
                />
              </AImage.PreviewGroup>
            </div>
          </div>

          <!-- C4 回复区 -->
          <div class="review-reply-card-reply">
            <template v-if="review.sellerReplyContent">
              <div class="review-reply-card-reply-content">
                <div class="review-reply-card-reply-label">卖家回复：</div>
                <div class="review-reply-card-reply-text">
                  {{ review.sellerReplyContent }}
                </div>
                <div class="review-reply-card-reply-time">
                  {{ formatDateTime(review.sellerReplyAt) }}
                </div>
              </div>
              <Button type="link" size="small" @click="openReplyDrawer(review)">
                编辑回复
              </Button>
            </template>
            <template v-else>
              <Button type="primary" size="small" @click="openReplyDrawer(review)">
                回复
              </Button>
            </template>
          </div>
        </Card>
      </div>
    </Spin>

    <!-- 回复抽屉 480px -->
    <Drawer
      v-model:open="replyDrawerOpen"
      :title="isEditingReply ? '编辑回复' : '回复评价'"
      :width="480"
      :mask-closable="true"
      @close="closeReplyDrawer"
    >
      <template v-if="replyingReview">
        <!-- 评价摘要（只读） -->
        <div class="review-reply-drawer-summary">
          <div class="review-reply-drawer-summary-header">
            <Avatar :size="32">
              <UserOutlined />
            </Avatar>
            <span class="review-reply-drawer-summary-user">
              {{ replyingReview.userMaskedName }}
            </span>
            <Rate :value="replyingReview.rating" disabled allow-half size="small" />
          </div>
          <div class="review-reply-drawer-summary-product">
            {{ replyingReview.productName || '—' }}
          </div>
          <div class="review-reply-drawer-summary-content">
            {{ replyingReview.content }}
          </div>
        </div>

        <!-- 回复表单 -->
        <Form layout="vertical" class="review-reply-drawer-form">
          <FormItem label="回复内容">
            <Input
              v-model:value="replyContent"
              type="textarea"
              :rows="4"
              :maxlength="500"
              show-count
              placeholder="请输入回复内容（1-500 字）"
            />
            <div class="review-reply-drawer-char-count">
              {{ replyCharCount }} / 500
            </div>
          </FormItem>
        </Form>
      </template>

      <template #footer>
        <Space>
          <Button @click="closeReplyDrawer">取消</Button>
          <IdempotencyButton :loading="replying" @click="onSubmitReply">
            {{ isEditingReply ? '更新回复' : '提交回复' }}
          </IdempotencyButton>
        </Space>
      </template>
    </Drawer>
  </div>
</template>

<style scoped>
.review-reply-page {
  display: flex;
  flex-direction: column;
  gap: 16px;
}
.review-reply-breadcrumb {
  font-size: 14px;
}
.review-reply-stats-card {
  border-radius: 8px;
}
.review-reply-stat-box {
  display: flex;
  flex-direction: column;
  gap: 4px;
}
.review-reply-stat-label {
  font-size: 13px;
  color: #8c8c8c;
}
.review-reply-stat-value {
  font-size: 24px;
  font-weight: 500;
  color: #000000d9;
}
.review-reply-filter-card {
  border-radius: 8px;
}
.review-reply-filter-label {
  font-size: 12px;
  color: #8c8c8c;
  margin-bottom: 4px;
}
.review-reply-list {
  display: flex;
  flex-direction: column;
  gap: 12px;
}
.review-reply-card {
  border-radius: 8px;
}
.review-reply-card-header {
  display: flex;
  align-items: center;
  justify-content: space-between;
  margin-bottom: 12px;
}
.review-reply-card-header-left {
  display: flex;
  align-items: center;
  gap: 8px;
}
.review-reply-card-user {
  font-size: 14px;
  font-weight: 500;
  color: #000000d9;
}
.review-reply-card-time {
  font-size: 12px;
  color: #8c8c8c;
}
.review-reply-card-product {
  display: flex;
  align-items: center;
  gap: 12px;
  padding: 8px 12px;
  background: #fafafa;
  border-radius: 6px;
  margin-bottom: 12px;
  cursor: pointer;
  transition: background 0.2s;
}
.review-reply-card-product:hover {
  background: #f0f0f0;
}
.review-reply-card-product-img {
  border-radius: 4px;
  flex-shrink: 0;
}
.review-reply-card-product-img-placeholder {
  width: 48px;
  height: 48px;
  display: flex;
  align-items: center;
  justify-content: center;
  background: #f0f0f0;
  border-radius: 4px;
  color: #8c8c8c;
  flex-shrink: 0;
}
.review-reply-card-product-info {
  display: flex;
  flex-direction: column;
  gap: 2px;
  overflow: hidden;
}
.review-reply-card-product-name {
  font-size: 13px;
  color: #000000d9;
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
}
.review-reply-card-product-spec {
  font-size: 12px;
  color: #8c8c8c;
}
.review-reply-card-body {
  margin-bottom: 12px;
}
.review-reply-card-images {
  display: flex;
  gap: 8px;
  flex-wrap: wrap;
  margin-top: 8px;
}
.review-reply-card-image-thumb {
  border-radius: 4px;
  object-fit: cover;
}
.review-reply-card-reply {
  display: flex;
  align-items: flex-start;
  justify-content: space-between;
  padding-top: 12px;
  border-top: 1px solid #f0f0f0;
}
.review-reply-card-reply-content {
  flex: 1;
  display: flex;
  flex-direction: column;
  gap: 4px;
}
.review-reply-card-reply-label {
  font-size: 12px;
  color: #8c8c8c;
}
.review-reply-card-reply-text {
  font-size: 13px;
  color: #000000d9;
  line-height: 1.6;
}
.review-reply-card-reply-time {
  font-size: 12px;
  color: #8c8c8c;
}
.review-reply-drawer-summary {
  padding: 12px;
  background: #fafafa;
  border-radius: 6px;
  margin-bottom: 16px;
}
.review-reply-drawer-summary-header {
  display: flex;
  align-items: center;
  gap: 8px;
  margin-bottom: 8px;
}
.review-reply-drawer-summary-user {
  font-size: 13px;
  font-weight: 500;
}
.review-reply-drawer-summary-product {
  font-size: 12px;
  color: #8c8c8c;
  margin-bottom: 6px;
}
.review-reply-drawer-summary-content {
  font-size: 13px;
  color: #000000d9;
  line-height: 1.6;
}
.review-reply-drawer-form {
  margin-top: 8px;
}
.review-reply-drawer-char-count {
  font-size: 12px;
  color: #8c8c8c;
  text-align: right;
  margin-top: 4px;
}
</style>
