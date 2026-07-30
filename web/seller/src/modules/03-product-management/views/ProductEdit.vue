<script setup lang="ts">
import { ref, computed, onMounted, reactive, h } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import {
  Breadcrumb,
  BreadcrumbItem,
  Card,
  Form,
  FormItem,
  Input,
  Select,
  Button,
  Space,
  Upload,
  message,
  Modal,
} from 'ant-design-vue'
import type { UploadProps, UploadFile } from 'ant-design-vue'
import { PlusOutlined, SaveOutlined, SendOutlined, ArrowLeftOutlined, DeleteOutlined } from '@ant-design/icons-vue'
import { productApi } from '../api/product.api'
import type {
  CreateProductDto,
  UpdateProductDto,
  ProductDetailDto,
  ProductStatus,
} from '../types/product.dto'
import { StatusTag, ShopStatusGuard } from '@/shared/components'
import { logger } from '@/shared/utils/logger'
import { ConcurrencyError } from '@/shared/http'

/**
 * 商品新增/编辑页
 *
 * 路由：
 * - /products/new      新增
 * - /products/:id/edit 编辑
 *
 * 通过 route.params.id 区分模式。编辑模式 onMounted 拉详情回填表单并记录 version。
 * 提交：新增 POST /products，编辑 PUT /products/{id}（带 version 乐观锁）。
 * 409 冲突弹「资源已被他人修改，是否刷新后重试？」。
 */

const route = useRoute()
const router = useRouter()

const productId = computed(() => (route.params.id as string | undefined) ?? null)
const isEdit = computed(() => !!productId.value)

const loading = ref(false)
const submitting = ref(false)
const submittingReview = ref(false)
const currentVersion = ref(0)
const currentStatus = ref<ProductStatus | null>(null)

/** 表单数据 */
const form = reactive({
  name: '',
  description: '',
  categoryId: '' as string,
  coverImage: '' as string,
  images: [] as string[],
  attributes: [] as Array<{ name: string; values: string[] }>,
})

/** 表单 ref */
const formRef = ref()

/** 表单校验规则 */
const rules = {
  name: [
    { required: true, message: '请输入商品名称', trigger: 'blur' },
    { min: 2, max: 100, message: '商品名称长度为 2-100 字', trigger: 'blur' },
  ],
  categoryId: [{ required: true, message: '请选择商品分类', trigger: 'change' }],
  description: [{ max: 2000, message: '描述最长 2000 字', trigger: 'blur' }],
}

/** 分类选项（叶子分类，由后端分类服务提供，当前为静态选项） */
const categoryOptions: Array<{ label: string; value: string }> = [
  { label: '服装 / 男装 / T恤', value: 'cat-tshirt' },
  { label: '服装 / 男装 / 衬衫', value: 'cat-shirt' },
  { label: '服装 / 女装 / 连衣裙', value: 'cat-dress' },
  { label: '数码 / 手机', value: 'cat-phone' },
  { label: '数码 / 配件 / 数据线', value: 'cat-cable' },
  { label: '家居 / 家纺', value: 'cat-home' },
]

/** 封面图 fileList（Upload 组件受控） */
const coverFileList = ref<UploadFile[]>([])
/** 详情图 fileList */
const imageFileList = ref<UploadFile[]>([])

const MAX_IMAGES = 9

/** 自定义上传：将文件转为 data URL 存储（无独立上传端点时使用） */
function customRequest(options: UploadProps['customRequest']): void {
  const { file, onSuccess, onError } = options
  const raw = file as File
  const reader = new FileReader()
  reader.onload = () => {
    const dataUrl = reader.result as string
    onSuccess?.({ url: dataUrl }, file)
  }
  reader.onerror = () => {
    onError?.(new Error('读取文件失败'))
  }
  reader.readAsDataURL(raw)
}

/** 封面图变化 */
function onCoverChange(info: { fileList: UploadFile[] }): void {
  coverFileList.value = info.fileList.slice(-1)
  const last = info.fileList[info.fileList.length - 1]
  if (last?.response?.url) {
    form.coverImage = last.response.url as string
  } else if (last?.url) {
    form.coverImage = last.url
  } else if (info.fileList.length === 0) {
    form.coverImage = ''
  }
}

/** 详情图变化 */
function onImagesChange(info: { fileList: UploadFile[] }): void {
  imageFileList.value = info.fileList.slice(0, MAX_IMAGES)
  form.images = info.fileList
    .filter((f) => f.status === 'done')
    .map((f) => (f.response?.url as string) ?? f.url ?? '')
    .filter((url): url is string => !!url)
}

/** 添加属性行 */
function addAttribute(): void {
  form.attributes.push({ name: '', values: [] })
}

/** 删除属性行 */
function removeAttribute(index: number): void {
  form.attributes.splice(index, 1)
}

/** 加载商品详情（编辑模式） */
async function loadDetail(): Promise<void> {
  if (!productId.value) return
  loading.value = true
  try {
    const detail: ProductDetailDto = await productApi.get(productId.value)
    form.name = detail.name
    form.description = detail.description ?? ''
    form.categoryId = detail.categoryId
    form.coverImage = detail.coverImage ?? ''
    form.images = [...detail.images]
    form.attributes = detail.attributes.map((a) => ({ name: a.name, values: [...a.values] }))
    currentVersion.value = detail.version
    currentStatus.value = detail.status

    // 回填 Upload fileList
    if (form.coverImage) {
      coverFileList.value = [
        {
          uid: '-1',
          name: 'cover',
          status: 'done',
          url: form.coverImage,
        } as UploadFile,
      ]
    }
    imageFileList.value = form.images.map((url, idx) => ({
      uid: `-${idx + 2}`,
      name: `image-${idx + 1}`,
      status: 'done',
      url,
    })) as UploadFile[]
  } catch (e) {
    logger.error('加载商品详情失败', e)
    message.error('加载商品详情失败，将返回列表')
    router.push('/products')
  } finally {
    loading.value = false
  }
}

/** 构造提交 body */
function buildBody(): CreateProductDto {
  const body: CreateProductDto = {
    name: form.name.trim(),
    categoryId: form.categoryId,
  }
  if (form.description.trim()) body.description = form.description.trim()
  if (form.coverImage) body.coverImage = form.coverImage
  if (form.images.length > 0) body.images = form.images
  const validAttrs = form.attributes
    .filter((a) => a.name.trim() && a.values.length > 0)
    .map((a) => ({ name: a.name.trim(), values: a.values.map((v) => v.trim()).filter(Boolean) }))
  if (validAttrs.length > 0) body.attributes = validAttrs
  return body
}

/** 处理 409 冲突 */
function handleConcurrencyError(): void {
  Modal.confirm({
    title: '资源已被他人修改',
    content: '该商品已被他人修改，是否刷新后重试？',
    okText: '刷新后重试',
    cancelText: '返回列表',
    onOk: () => {
      return loadDetail()
    },
    onCancel: () => {
      router.push('/products')
    },
  })
}

/** 提交保存 */
async function onSubmit(): Promise<void> {
  try {
    await formRef.value?.validate()
  } catch {
    message.warning('请完善表单必填项')
    return
  }
  submitting.value = true
  try {
    const body = buildBody()
    if (isEdit.value && productId.value) {
      const updateBody: UpdateProductDto = { ...body, version: currentVersion.value }
      const detail = await productApi.update(productId.value, updateBody)
      currentVersion.value = detail.version
      currentStatus.value = detail.status
      message.success('保存成功')
    } else {
      const detail = await productApi.create(body)
      message.success('创建成功')
      router.replace(`/products/${detail.id}/edit`)
    }
  } catch (e) {
    logger.error('保存商品失败', e)
    if (e instanceof ConcurrencyError) {
      handleConcurrencyError()
    } else {
      message.error('保存失败，请稍后重试')
    }
  } finally {
    submitting.value = false
  }
}

/** 提交审核 */
async function onSubmitForReview(): Promise<void> {
  if (!productId.value) {
    message.warning('请先保存商品')
    return
  }
  if (currentStatus.value && currentStatus.value !== 'Draft' && currentStatus.value !== 'Rejected') {
    message.warning('当前状态不允许提交审核')
    return
  }
  submittingReview.value = true
  try {
    await productApi.submitForReview(productId.value)
    message.success('已提交审核')
    currentStatus.value = 'PendingReview'
    router.push('/products')
  } catch (e) {
    logger.error('提交审核失败', e)
    if (e instanceof ConcurrencyError) {
      handleConcurrencyError()
    } else {
      message.error('提交审核失败，请稍后重试')
    }
  } finally {
    submittingReview.value = false
  }
}

/** 取消返回 */
function onCancel(): void {
  router.push('/products')
}

onMounted(() => {
  if (isEdit.value) {
    void loadDetail()
  }
})
</script>

<template>
  <div class="product-edit-page">
    <Breadcrumb class="product-edit-breadcrumb">
      <BreadcrumbItem>首页</BreadcrumbItem>
      <BreadcrumbItem>商品管理</BreadcrumbItem>
      <BreadcrumbItem>{{ isEdit ? '编辑商品' : '新增商品' }}</BreadcrumbItem>
    </Breadcrumb>

    <!-- 顶部操作栏 -->
    <Card class="product-edit-header" :bordered="true" size="small">
      <div class="product-edit-header-inner">
        <div class="product-edit-header-left">
          <Button :icon="h(ArrowLeftOutlined)" @click="onCancel">返回</Button>
          <span class="product-edit-title">{{ isEdit ? '编辑商品' : '新增商品' }}</span>
          <template v-if="isEdit && currentStatus">
            <span class="product-edit-status-label">状态：</span>
            <StatusTag type="product" :status="currentStatus" />
          </template>
        </div>
        <Space>
          <Button @click="onCancel">取消</Button>
          <Button :icon="h(SaveOutlined)" :loading="submitting" @click="onSubmit">
            保存
          </Button>
          <Button
            v-if="isEdit"
            v-permission="'product:submit-review'"
            type="primary"
            :icon="h(SendOutlined)"
            :loading="submittingReview"
            @click="onSubmitForReview"
          >
            提交审核
          </Button>
        </Space>
      </div>
    </Card>

    <div class="product-edit-grid">
      <!-- 左侧主表单 -->
      <div class="product-edit-main">
        <!-- 基本信息 -->
        <Card class="product-edit-section" :bordered="true" :loading="loading">
          <template #title>
            <span class="product-edit-section-title">基本信息</span>
          </template>
          <Form
            ref="formRef"
            :model="form"
            :rules="rules"
            layout="vertical"
            :label-col="{ style: { width: '120px' } }"
          >
            <FormItem label="商品名称" name="name" required>
              <Input
                v-model:value="form.name"
                placeholder="请输入商品名称（2-100 字）"
                :maxlength="100"
                show-count
              />
            </FormItem>

            <FormItem label="商品描述" name="description">
              <Input
                v-model:value="form.description"
                type="textarea"
                :rows="4"
                placeholder="请输入商品描述（选填，最长 2000 字）"
                :maxlength="2000"
                show-count
              />
            </FormItem>

            <FormItem label="商品分类" name="categoryId" required>
              <Select
                v-model:value="form.categoryId"
                placeholder="请选择叶子分类"
                :options="categoryOptions"
                :disabled="isEdit"
              />
              <template v-if="isEdit" #extra>
                <span class="product-edit-hint">分类提交后不可修改</span>
              </template>
            </FormItem>
          </Form>
        </Card>

        <!-- 图片信息 -->
        <Card class="product-edit-section" :bordered="true" :loading="loading">
          <template #title>
            <span class="product-edit-section-title">图片信息</span>
          </template>
          <Form layout="vertical">
            <FormItem label="封面图">
              <Upload
                :file-list="coverFileList"
                list-type="picture-card"
                :max-count="1"
                :custom-request="customRequest"
                accept="image/*"
                @change="onCoverChange"
              >
                <div v-if="coverFileList.length === 0" class="product-edit-upload-trigger">
                  <PlusOutlined />
                  <div class="product-edit-upload-text">上传封面</div>
                </div>
              </Upload>
              <div class="product-edit-hint">建议尺寸 800×800px，≤5MB，仅支持 1 张</div>
            </FormItem>

            <FormItem label="详情图">
              <Upload
                :file-list="imageFileList"
                list-type="picture-card"
                :max-count="MAX_IMAGES"
                :custom-request="customRequest"
                accept="image/*"
                multiple
                @change="onImagesChange"
              >
                <div v-if="imageFileList.length < MAX_IMAGES" class="product-edit-upload-trigger">
                  <PlusOutlined />
                  <div class="product-edit-upload-text">上传详情图</div>
                </div>
              </Upload>
              <div class="product-edit-hint">最多 {{ MAX_IMAGES }} 张，建议尺寸 750×750px</div>
            </FormItem>
          </Form>
        </Card>

        <!-- 商品属性 -->
        <Card class="product-edit-section" :bordered="true" :loading="loading">
          <template #title>
            <span class="product-edit-section-title">商品属性</span>
          </template>
          <template #extra>
            <Button type="link" :icon="h(PlusOutlined)" @click="addAttribute">添加属性</Button>
          </template>
          <div v-if="form.attributes.length === 0" class="product-edit-empty-attrs">
            暂无属性，点击「添加属性」配置规格维度（如颜色、尺码）
          </div>
          <div
            v-for="(attr, index) in form.attributes"
            :key="index"
            class="product-edit-attr-row"
          >
            <Input
              v-model:value="attr.name"
              placeholder="属性名（如：颜色）"
              style="width: 200px"
            />
            <Select
              v-model:value="attr.values"
              mode="tags"
              placeholder="输入属性值后回车（如：白色、黑色）"
              style="flex: 1; min-width: 240px"
              :token-separators="[',']"
            />
            <Button
              type="link"
              danger
              :icon="h(DeleteOutlined)"
              @click="removeAttribute(index)"
            />
          </div>
        </Card>
      </div>

      <!-- 右侧侧栏（占位提示） -->
      <div class="product-edit-aside">
        <Card class="product-edit-section" :bordered="true">
          <template #title>
            <span class="product-edit-section-title">操作提示</span>
          </template>
          <ul class="product-edit-tips">
            <li>商品名称为 2-100 字，必填</li>
            <li>分类选择后不可修改，请谨慎选择叶子分类</li>
            <li>封面图建议正方形，详情图最多 9 张</li>
            <li>属性用于生成 SKU 规格组合，如颜色×尺码</li>
            <li>保存后可点击「提交审核」送平台审核</li>
          </ul>
          <ShopStatusGuard
            v-if="!isEdit"
            requires="canPublish"
            fallback-text="店铺当前状态不允许上架新商品"
          >
            <div class="product-edit-shop-ok">店铺状态正常，可上架新商品</div>
          </ShopStatusGuard>
        </Card>
      </div>
    </div>

    <!-- 底部操作栏 -->
    <Card class="product-edit-footer" :bordered="true" size="small">
      <div class="product-edit-footer-inner">
        <Button @click="onCancel">取消</Button>
        <Space>
          <Button :icon="h(SaveOutlined)" :loading="submitting" @click="onSubmit">保存</Button>
          <Button
            v-if="isEdit"
            v-permission="'product:submit-review'"
            type="primary"
            :icon="h(SendOutlined)"
            :loading="submittingReview"
            @click="onSubmitForReview"
          >
            提交审核
          </Button>
        </Space>
      </div>
    </Card>
  </div>
</template>

<style scoped>
.product-edit-page {
  display: flex;
  flex-direction: column;
  gap: 16px;
}
.product-edit-breadcrumb {
  font-size: 14px;
}
.product-edit-header {
  border-radius: 8px;
}
.product-edit-header-inner {
  display: flex;
  align-items: center;
  justify-content: space-between;
}
.product-edit-header-left {
  display: flex;
  align-items: center;
  gap: 12px;
}
.product-edit-title {
  font-size: 16px;
  font-weight: 500;
  color: #000000d9;
}
.product-edit-status-label {
  font-size: 13px;
  color: #8c8c8c;
}
.product-edit-grid {
  display: grid;
  grid-template-columns: 1fr 320px;
  gap: 16px;
  align-items: start;
}
.product-edit-main {
  display: flex;
  flex-direction: column;
  gap: 16px;
}
.product-edit-section {
  border-radius: 8px;
}
.product-edit-section-title {
  font-size: 15px;
  font-weight: 500;
}
.product-edit-hint {
  font-size: 12px;
  color: #8c8c8c;
  margin-top: 4px;
}
.product-edit-upload-trigger {
  display: flex;
  flex-direction: column;
  align-items: center;
  justify-content: center;
  color: #8c8c8c;
}
.product-edit-upload-text {
  font-size: 12px;
  margin-top: 4px;
}
.product-edit-empty-attrs {
  padding: 24px;
  text-align: center;
  color: #8c8c8c;
  font-size: 13px;
  background: #fafafa;
  border-radius: 6px;
}
.product-edit-attr-row {
  display: flex;
  align-items: center;
  gap: 8px;
  margin-bottom: 12px;
}
.product-edit-attr-row:last-child {
  margin-bottom: 0;
}
.product-edit-tips {
  margin: 0;
  padding-left: 20px;
  color: #595959;
  font-size: 13px;
  line-height: 1.8;
}
.product-edit-shop-ok {
  margin-top: 12px;
  padding: 8px 12px;
  background: #f6ffed;
  border: 1px solid #b7eb8f;
  border-radius: 6px;
  color: #389e0d;
  font-size: 13px;
}
.product-edit-footer {
  border-radius: 8px;
}
.product-edit-footer-inner {
  display: flex;
  align-items: center;
  justify-content: flex-end;
  gap: 8px;
}

@media (max-width: 1199px) {
  .product-edit-grid {
    grid-template-columns: 1fr;
  }
}
</style>
