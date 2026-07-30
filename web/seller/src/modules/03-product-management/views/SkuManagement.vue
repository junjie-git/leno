<script setup lang="ts">
import { ref, computed, onMounted, reactive, h } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import {
  Breadcrumb,
  BreadcrumbItem,
  Card,
  Table,
  Button,
  Space,
  Form,
  FormItem,
  Input,
  InputNumber,
  Select,
  Tag,
  Modal,
  Skeleton,
  message,
} from 'ant-design-vue'
import type { TableColumnsType } from 'ant-design-vue'
import { PlusOutlined, ArrowLeftOutlined } from '@ant-design/icons-vue'
import { productApi } from '../api/product.api'
import type { ProductDetailDto, ProductSkuDto, AddSkuDto } from '../types/product.dto'
import { StatusTag, EmptyState } from '@/shared/components'
import { formatMoney, formatNumber } from '@/shared/utils/format'
import { logger } from '@/shared/utils/logger'
import { ConcurrencyError } from '@/shared/http'

/**
 * SKU 管理页
 *
 * 路由：/products/:id/skus
 * 功能：展示商品 SKU 列表 / 新增 SKU / 调整价格。
 */

const route = useRoute()
const router = useRouter()

const productId = computed(() => route.params.id as string)

const loading = ref(true)
const detail = ref<ProductDetailDto | null>(null)
const skus = ref<ProductSkuDto[]>([])

/** 新增 SKU 弹窗 */
const addSkuModalOpen = ref(false)
const addSkuLoading = ref(false)
const addSkuForm = reactive<AddSkuDto>({
  skuCode: '',
  skuName: '',
  attributes: {},
  price: 0,
  stock: 0,
  lowStockThreshold: 10,
})
const addSkuAttrRows = ref<Array<{ name: string; value: string }>>([])

/** 调价弹窗 */
const adjustPriceModalOpen = ref(false)
const adjustPriceLoading = ref(false)
const adjustPriceTarget = ref<ProductSkuDto | null>(null)
const adjustPriceForm = reactive({
  newPrice: 0,
  reason: '',
})

/** 表格列定义 */
const columns: TableColumnsType = [
  {
    title: 'SKU 编码',
    dataIndex: 'skuCode',
    key: 'skuCode',
    width: 140,
  },
  {
    title: 'SKU 名称',
    dataIndex: 'skuName',
    key: 'skuName',
    width: 160,
    ellipsis: true,
  },
  {
    title: '属性',
    key: 'attributes',
    width: 200,
  },
  {
    title: '价格',
    dataIndex: 'price',
    key: 'price',
    width: 110,
    align: 'right',
  },
  {
    title: '库存',
    dataIndex: 'stock',
    key: 'stock',
    width: 100,
    align: 'right',
  },
  {
    title: '预警阈值',
    dataIndex: 'lowStockThreshold',
    key: 'lowStockThreshold',
    width: 100,
    align: 'right',
  },
  {
    title: '状态',
    key: 'status',
    width: 100,
  },
  {
    title: '操作',
    key: 'action',
    width: 120,
    fixed: 'right',
  },
]

/** 总库存 */
const totalStock = computed(() =>
  skus.value.reduce((sum, s) => sum + s.stock, 0),
)

/** 加载商品详情（含 SKU 列表） */
async function loadDetail(): Promise<void> {
  loading.value = true
  try {
    const data = await productApi.get(productId.value)
    detail.value = data
    skus.value = data.skus
  } catch (e) {
    logger.error('加载商品详情失败', e)
    message.error('加载商品详情失败，将返回列表')
    router.push('/products')
  } finally {
    loading.value = false
  }
}

/** 属性对象渲染为 Tag 列表 */
function attributePairs(attrs: Record<string, string>): Array<{ name: string; value: string }> {
  return Object.entries(attrs).map(([name, value]) => ({ name, value }))
}

/** 库存预警状态 */
function stockStatus(stock: number, threshold: number): { label: string; color: string } {
  if (stock === 0) return { label: '缺货', color: 'error' }
  if (stock < threshold) return { label: '预警', color: 'warning' }
  return { label: '可售', color: 'success' }
}

/** 打开新增 SKU 弹窗 */
function openAddSkuModal(): void {
  addSkuForm.skuCode = ''
  addSkuForm.skuName = ''
  addSkuForm.attributes = {}
  addSkuForm.price = 0
  addSkuForm.stock = 0
  addSkuForm.lowStockThreshold = 10
  addSkuAttrRows.value = [{ name: '', value: '' }]
  addSkuModalOpen.value = true
}

/** 添加属性行 */
function addAttrRow(): void {
  addSkuAttrRows.value.push({ name: '', value: '' })
}

/** 删除属性行 */
function removeAttrRow(index: number): void {
  addSkuAttrRows.value.splice(index, 1)
}

/** 确认新增 SKU */
async function confirmAddSku(): Promise<void> {
  if (!addSkuForm.skuCode.trim()) {
    message.warning('请输入 SKU 编码')
    return
  }
  if (!addSkuForm.skuName.trim()) {
    message.warning('请输入 SKU 名称')
    return
  }
  if (addSkuForm.price <= 0) {
    message.warning('价格必须大于 0')
    return
  }
  if (addSkuForm.stock < 0) {
    message.warning('库存不能为负')
    return
  }

  // 组装 attributes
  const attrs: Record<string, string> = {}
  for (const row of addSkuAttrRows.value) {
    const name = row.name.trim()
    const value = row.value.trim()
    if (name && value) {
      attrs[name] = value
    }
  }
  addSkuForm.attributes = attrs

  addSkuLoading.value = true
  try {
    const body: AddSkuDto = {
      skuCode: addSkuForm.skuCode.trim(),
      skuName: addSkuForm.skuName.trim(),
      attributes: addSkuForm.attributes,
      price: addSkuForm.price,
      stock: addSkuForm.stock,
      lowStockThreshold: addSkuForm.lowStockThreshold,
    }
    const updated = await productApi.addSku(productId.value, body)
    detail.value = updated
    skus.value = updated.skus
    message.success('新增 SKU 成功')
    addSkuModalOpen.value = false
  } catch (e) {
    logger.error('新增 SKU 失败', e)
    if (e instanceof ConcurrencyError) {
      message.warning('商品已被他人修改，已自动刷新')
      addSkuModalOpen.value = false
      await loadDetail()
    } else {
      message.error('新增 SKU 失败，请稍后重试')
    }
  } finally {
    addSkuLoading.value = false
  }
}

/** 取消新增 SKU */
function cancelAddSku(): void {
  addSkuModalOpen.value = false
}

/** 打开调价弹窗 */
function openAdjustPriceModal(record: ProductSkuDto): void {
  adjustPriceTarget.value = record
  adjustPriceForm.newPrice = record.price
  adjustPriceForm.reason = ''
  adjustPriceModalOpen.value = true
}

/** 确认调价 */
async function confirmAdjustPrice(): Promise<void> {
  const target = adjustPriceTarget.value
  if (!target) return
  if (adjustPriceForm.newPrice <= 0) {
    message.warning('新价格必须大于 0')
    return
  }
  if (adjustPriceForm.newPrice === target.price) {
    message.warning('新价格与当前价格相同')
    return
  }
  adjustPriceLoading.value = true
  try {
    const body = {
      newPrice: adjustPriceForm.newPrice,
      reason: adjustPriceForm.reason.trim() || undefined,
    }
    const updated = await productApi.adjustPrice(productId.value, target.id, body)
    detail.value = updated
    skus.value = updated.skus
    message.success('调价成功')
    adjustPriceModalOpen.value = false
    adjustPriceTarget.value = null
  } catch (e) {
    logger.error('调价失败', e)
    if (e instanceof ConcurrencyError) {
      message.warning('商品已被他人修改，已自动刷新')
      adjustPriceModalOpen.value = false
      await loadDetail()
    } else {
      message.error('调价失败，请稍后重试')
    }
  } finally {
    adjustPriceLoading.value = false
  }
}

/** 取消调价 */
function cancelAdjustPrice(): void {
  adjustPriceModalOpen.value = false
  adjustPriceTarget.value = null
}

/** 跳转价格历史 */
function goPriceHistory(): void {
  router.push(`/products/${productId.value}/price-history`)
}

/** 返回列表 */
function goBack(): void {
  router.push('/products')
}

onMounted(() => {
  void loadDetail()
})
</script>

<template>
  <div class="sku-management-page">
    <Breadcrumb class="sku-management-breadcrumb">
      <BreadcrumbItem>首页</BreadcrumbItem>
      <BreadcrumbItem>商品管理</BreadcrumbItem>
      <BreadcrumbItem>SKU 管理</BreadcrumbItem>
    </Breadcrumb>

    <!-- 商品信息卡片 -->
    <Card class="sku-management-summary" :bordered="true">
      <Skeleton v-if="loading" active :paragraph="{ rows: 2 }" />
      <div v-else-if="detail" class="sku-management-summary-grid">
        <div class="sku-management-summary-item">
          <span class="sku-management-summary-label">商品名称：</span>
          <span class="sku-management-summary-value">{{ detail.name }}</span>
        </div>
        <div class="sku-management-summary-item">
          <span class="sku-management-summary-label">商品状态：</span>
          <StatusTag type="product" :status="detail.status" />
        </div>
        <div class="sku-management-summary-item">
          <span class="sku-management-summary-label">SKU 数：</span>
          <span class="sku-management-summary-value">{{ formatNumber(skus.length) }}</span>
        </div>
        <div class="sku-management-summary-item">
          <span class="sku-management-summary-label">总库存：</span>
          <span class="sku-management-summary-value">{{ formatNumber(totalStock) }}</span>
        </div>
      </div>
    </Card>

    <!-- SKU 列表 -->
    <Card class="sku-management-table-card" :bordered="true">
      <template #title>
        <Space>
          <Button :icon="h(ArrowLeftOutlined)" size="small" @click="goBack">返回</Button>
          <span class="sku-management-table-title">SKU 列表</span>
        </Space>
      </template>
      <template #extra>
        <Space>
          <Button @click="goPriceHistory">价格历史</Button>
          <Button
            v-permission="'product:sku:manage'"
            type="primary"
            :icon="h(PlusOutlined)"
            @click="openAddSkuModal"
          >
            新增 SKU
          </Button>
        </Space>
      </template>

      <EmptyState v-if="!loading && skus.length === 0" description="暂无 SKU，点击「新增 SKU」开始配置" />
      <Table
        v-else
        :columns="columns"
        :data-source="skus"
        :row-key="(record: ProductSkuDto) => record.id"
        :loading="loading"
        :pagination="false"
        size="middle"
        :scroll="{ x: 1100 }"
      >
        <template #bodyCell="{ column, record }">
          <template v-if="column.key === 'skuCode'">
            <span class="sku-code">{{ record.skuCode }}</span>
          </template>

          <template v-else-if="column.key === 'attributes'">
            <Space :size="4" wrap>
              <Tag v-for="pair in attributePairs(record.attributes)" :key="pair.name">
                {{ pair.name }}: {{ pair.value }}
              </Tag>
              <span v-if="attributePairs(record.attributes).length === 0" class="sku-empty">-</span>
            </Space>
          </template>

          <template v-else-if="column.key === 'price'">
            <span class="sku-price">{{ formatMoney(record.price) }}</span>
          </template>

          <template v-else-if="column.key === 'stock'">
            <span :class="['sku-stock', record.stock === 0 ? 'danger' : record.stock < record.lowStockThreshold ? 'warn' : '']">
              {{ formatNumber(record.stock) }}
            </span>
          </template>

          <template v-else-if="column.key === 'lowStockThreshold'">
            {{ formatNumber(record.lowStockThreshold) }}
          </template>

          <template v-else-if="column.key === 'status'">
            <Tag :color="stockStatus(record.stock, record.lowStockThreshold).color">
              {{ stockStatus(record.stock, record.lowStockThreshold).label }}
            </Tag>
          </template>

          <template v-else-if="column.key === 'action'">
            <Button
              v-permission="'product:sku:manage'"
              type="link"
              size="small"
              @click="openAdjustPriceModal(record)"
            >
              调整价格
            </Button>
          </template>
        </template>
      </Table>
    </Card>

    <!-- 新增 SKU 弹窗 -->
    <Modal
      v-model:open="addSkuModalOpen"
      title="新增 SKU"
      ok-text="确认新增"
      cancel-text="取消"
      :confirm-loading="addSkuLoading"
      :width="560"
      @ok="confirmAddSku"
      @cancel="cancelAddSku"
    >
      <Form layout="vertical">
        <FormItem label="SKU 编码" required>
          <Input
            v-model:value="addSkuForm.skuCode"
            placeholder="请输入 SKU 编码，如 SKU-NJ-006"
          />
        </FormItem>
        <FormItem label="SKU 名称" required>
          <Input
            v-model:value="addSkuForm.skuName"
            placeholder="请输入 SKU 名称，如 白色/L"
          />
        </FormItem>

        <FormItem label="规格属性">
          <div class="sku-add-attr-list">
            <div
              v-for="(row, index) in addSkuAttrRows"
              :key="index"
              class="sku-add-attr-row"
            >
              <Input
                v-model:value="row.name"
                placeholder="属性名（如：颜色）"
                style="width: 140px"
              />
              <Input
                v-model:value="row.value"
                placeholder="属性值（如：白色）"
                style="flex: 1"
              />
              <Button
                v-if="addSkuAttrRows.length > 1"
                type="link"
                danger
                size="small"
                @click="removeAttrRow(index)"
              >
                删除
              </Button>
            </div>
          </div>
          <Button type="link" size="small" :icon="h(PlusOutlined)" @click="addAttrRow">
            添加属性
          </Button>
        </FormItem>

        <Space :size="16" style="width: 100%">
          <FormItem label="销售价（元）" required style="flex: 1">
            <InputNumber
              v-model:value="addSkuForm.price"
              :min="0"
              :precision="2"
              style="width: 100%"
            />
          </FormItem>
          <FormItem label="库存" required style="flex: 1">
            <InputNumber
              v-model:value="addSkuForm.stock"
              :min="0"
              :precision="0"
              style="width: 100%"
            />
          </FormItem>
          <FormItem label="预警阈值" style="flex: 1">
            <InputNumber
              v-model:value="addSkuForm.lowStockThreshold"
              :min="0"
              :precision="0"
              style="width: 100%"
            />
          </FormItem>
        </Space>
      </Form>
    </Modal>

    <!-- 调价弹窗 -->
    <Modal
      v-model:open="adjustPriceModalOpen"
      title="调整价格"
      ok-text="确认调整"
      cancel-text="取消"
      :confirm-loading="adjustPriceLoading"
      :ok-button-props="{ danger: adjustPriceTarget && adjustPriceForm.newPrice > adjustPriceTarget.price * 1.2 }"
      @ok="confirmAdjustPrice"
      @cancel="cancelAdjustPrice"
    >
      <div v-if="adjustPriceTarget" class="sku-adjust-info">
        <div class="sku-adjust-info-row">
          <span class="sku-adjust-info-label">SKU 编码</span>
          <span class="sku-adjust-info-value">{{ adjustPriceTarget.skuCode }}</span>
        </div>
        <div class="sku-adjust-info-row">
          <span class="sku-adjust-info-label">SKU 名称</span>
          <span class="sku-adjust-info-value">{{ adjustPriceTarget.skuName }}</span>
        </div>
        <div class="sku-adjust-info-row">
          <span class="sku-adjust-info-label">当前价格</span>
          <span class="sku-adjust-info-value">{{ formatMoney(adjustPriceTarget.price) }}</span>
        </div>
      </div>
      <Form layout="vertical" style="margin-top: 16px">
        <FormItem label="新价格（元）" required>
          <InputNumber
            v-model:value="adjustPriceForm.newPrice"
            :min="0"
            :precision="2"
            style="width: 100%"
          />
          <div
            v-if="adjustPriceTarget && adjustPriceForm.newPrice > adjustPriceTarget.price * 1.2"
            class="sku-adjust-warn"
          >
            价格变动幅度较大，请确认后提交
          </div>
        </FormItem>
        <FormItem label="变更原因（选填）">
          <Input
            v-model:value="adjustPriceForm.reason"
            placeholder="请输入调价原因，如：夏季促销"
            :maxlength="100"
          />
        </FormItem>
      </Form>
    </Modal>
  </div>
</template>

<style scoped>
.sku-management-page {
  display: flex;
  flex-direction: column;
  gap: 16px;
}
.sku-management-breadcrumb {
  font-size: 14px;
}
.sku-management-summary {
  border-radius: 8px;
}
.sku-management-summary-grid {
  display: grid;
  grid-template-columns: repeat(4, 1fr);
  gap: 16px;
}
.sku-management-summary-item {
  display: flex;
  align-items: center;
  gap: 8px;
}
.sku-management-summary-label {
  font-size: 13px;
  color: #8c8c8c;
  white-space: nowrap;
}
.sku-management-summary-value {
  font-size: 14px;
  color: #000000d9;
  font-weight: 500;
}
.sku-management-table-card {
  border-radius: 8px;
}
.sku-management-table-title {
  font-size: 16px;
  font-weight: 500;
}
.sku-code {
  font-family: 'SF Mono', Consolas, monospace;
  font-size: 13px;
  color: #1677ff;
}
.sku-price {
  font-weight: 600;
  color: #000000d9;
}
.sku-stock {
  font-weight: 500;
}
.sku-stock.warn {
  color: #faad14;
}
.sku-stock.danger {
  color: #ff4d4f;
}
.sku-empty {
  color: #8c8c8c;
}
.sku-add-attr-list {
  display: flex;
  flex-direction: column;
  gap: 8px;
  margin-bottom: 8px;
}
.sku-add-attr-row {
  display: flex;
  align-items: center;
  gap: 8px;
}
.sku-adjust-info {
  background: #fafafa;
  border-radius: 6px;
  padding: 12px 16px;
}
.sku-adjust-info-row {
  display: flex;
  justify-content: space-between;
  padding: 4px 0;
  font-size: 13px;
}
.sku-adjust-info-label {
  color: #8c8c8c;
}
.sku-adjust-info-value {
  color: #000000d9;
  font-weight: 500;
}
.sku-adjust-warn {
  margin-top: 4px;
  font-size: 12px;
  color: #ff4d4f;
}

@media (max-width: 991px) {
  .sku-management-summary-grid {
    grid-template-columns: repeat(2, 1fr);
  }
}
</style>
