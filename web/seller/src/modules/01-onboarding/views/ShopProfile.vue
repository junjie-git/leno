<script setup lang="ts">
import { ref, reactive, onMounted } from 'vue'
import {
  Breadcrumb,
  BreadcrumbItem,
  Card,
  Form,
  FormItem,
  Input,
  message,
  Modal,
  Skeleton,
} from 'ant-design-vue'
import { shopApi } from '../api/shop.api'
import type { UpdateShopInfoDto } from '../types/shop.dto'
import { ImageUploader, IdempotencyButton, StatusTag } from '@/shared/components'
import { logger } from '@/shared/utils/logger'
import { ConcurrencyError } from '@/shared/http'

/**
 * 店铺资料页
 *
 * 路由 /shop/profile，权限 shop:profile:view
 * - GET /api/shops/me 拉取资料回填表单，记录 version
 * - PUT /api/shops/me 更新（含客服联系方式 + version 乐观锁）
 * - 409 冲突 → Modal.confirm「资源已被他人修改，是否刷新后重试？」
 */

const loading = ref(false)
const submitting = ref(false)
const currentVersion = ref(0)
const currentStatus = ref<string | null>(null)
const formRef = ref()

const form = reactive({
  name: '',
  logo: '' as string,
  description: '',
  customerService: {
    phone: '',
    email: '' as string,
    onlineAccount: '' as string,
  },
})

const rules = {
  name: [
    { required: true, message: '请输入店铺名称', trigger: 'blur' },
    { min: 2, max: 32, message: '店铺名称长度为 2-32 字', trigger: 'blur' },
  ],
  description: [{ max: 1000, message: '描述最长 1000 字', trigger: 'blur' }],
}

async function loadShop(): Promise<void> {
  loading.value = true
  try {
    const shop = await shopApi.getMyShop()
    form.name = shop.name
    form.logo = shop.logo ?? ''
    form.description = shop.description ?? ''
    form.customerService.phone = shop.customerService?.phone ?? ''
    form.customerService.email = shop.customerService?.email ?? ''
    form.customerService.onlineAccount = shop.customerService?.onlineAccount ?? ''
    currentVersion.value = shop.version
    currentStatus.value = shop.status
  } catch (e) {
    logger.error('加载店铺资料失败', e)
    message.error('加载店铺资料失败')
  } finally {
    loading.value = false
  }
}

function buildBody(): UpdateShopInfoDto {
  const body: UpdateShopInfoDto = {
    name: form.name.trim(),
    customerService: {
      phone: form.customerService.phone.trim(),
    },
    version: currentVersion.value,
  }
  if (form.logo) body.logo = form.logo
  if (form.description.trim()) body.description = form.description.trim()
  if (form.customerService.email.trim()) {
    body.customerService.email = form.customerService.email.trim()
  }
  if (form.customerService.onlineAccount.trim()) {
    body.customerService.onlineAccount = form.customerService.onlineAccount.trim()
  }
  return body
}

function handleConcurrencyError(): void {
  Modal.confirm({
    title: '资源已被他人修改',
    content: '该店铺资料已被他人修改，是否刷新后重试？',
    okText: '刷新后重试',
    cancelText: '取消',
    onOk: () => {
      return loadShop()
    },
  })
}

async function onSubmit(): Promise<void> {
  try {
    await formRef.value?.validate()
  } catch {
    message.warning('请完善表单必填项')
    return
  }
  submitting.value = true
  try {
    const updated = await shopApi.updateMyShop(buildBody())
    currentVersion.value = updated.version
    currentStatus.value = updated.status
    message.success('保存成功')
  } catch (e) {
    logger.error('保存店铺资料失败', e)
    if (e instanceof ConcurrencyError) {
      handleConcurrencyError()
    } else {
      message.error('保存失败，请稍后重试')
    }
  } finally {
    submitting.value = false
  }
}

onMounted(() => {
  void loadShop()
})
</script>

<template>
  <div class="shop-profile-page">
    <Breadcrumb class="shop-profile-breadcrumb">
      <BreadcrumbItem>店铺设置</BreadcrumbItem>
      <BreadcrumbItem>店铺资料</BreadcrumbItem>
    </Breadcrumb>

    <Card class="shop-profile-header" :bordered="true" size="small">
      <div class="shop-profile-header-inner">
        <span class="shop-profile-title">店铺资料</span>
        <template v-if="currentStatus">
          <span class="shop-profile-status-label">状态：</span>
          <StatusTag type="shop" :status="currentStatus" />
        </template>
      </div>
    </Card>

    <Skeleton v-if="loading" active :paragraph="{ rows: 8 }" />
    <div v-else class="shop-profile-body">
      <!-- 基础信息 -->
      <Card class="shop-profile-section" :bordered="true">
        <template #title>
          <span class="shop-profile-section-title">基础信息</span>
        </template>
        <Form
          ref="formRef"
          :model="form"
          :rules="rules"
          layout="vertical"
          :label-col="{ style: { width: '120px' } }"
        >
          <FormItem label="店铺名称" name="name" required>
            <Input
              v-model:value="form.name"
              placeholder="请输入店铺名称（2-32 字）"
              :maxlength="32"
              show-count
            />
          </FormItem>
          <FormItem label="店铺 Logo">
            <ImageUploader
              v-model="form.logo"
              accept=".jpg,.png,.webp"
              :max-size="5 * 1024 * 1024"
              label="上传 Logo"
            />
            <div class="shop-profile-hint">建议尺寸 200×200px，≤5MB，仅支持 JPG/PNG/WebP</div>
          </FormItem>
          <FormItem label="店铺描述" name="description">
            <Input
              v-model:value="form.description"
              type="textarea"
              :rows="4"
              placeholder="请输入店铺描述（选填，最长 1000 字）"
              :maxlength="1000"
              show-count
            />
          </FormItem>
        </Form>
      </Card>

      <!-- 客服联系方式 -->
      <Card class="shop-profile-section" :bordered="true">
        <template #title>
          <span class="shop-profile-section-title">客服联系方式</span>
        </template>
        <Form layout="vertical" :label-col="{ style: { width: '120px' } }">
          <FormItem label="客服电话" required>
            <Input
              v-model:value="form.customerService.phone"
              placeholder="请输入客服电话"
            />
          </FormItem>
          <FormItem label="客服邮箱">
            <Input
              v-model:value="form.customerService.email"
              placeholder="请输入客服邮箱（选填）"
            />
          </FormItem>
          <FormItem label="在线客服账号">
            <Input
              v-model:value="form.customerService.onlineAccount"
              placeholder="请输入在线客服账号（选填，如微信号）"
            />
          </FormItem>
        </Form>
      </Card>

      <!-- 底部保存 -->
      <div class="shop-profile-actions">
        <IdempotencyButton :loading="submitting" @click="onSubmit">保存</IdempotencyButton>
      </div>
    </div>
  </div>
</template>

<style scoped>
.shop-profile-page {
  display: flex;
  flex-direction: column;
  gap: 16px;
}
.shop-profile-breadcrumb {
  font-size: 14px;
}
.shop-profile-header {
  border-radius: 8px;
}
.shop-profile-header-inner {
  display: flex;
  align-items: center;
  gap: 12px;
}
.shop-profile-title {
  font-size: 16px;
  font-weight: 500;
  color: #000000d9;
}
.shop-profile-status-label {
  font-size: 13px;
  color: #8c8c8c;
}
.shop-profile-body {
  display: flex;
  flex-direction: column;
  gap: 16px;
}
.shop-profile-section {
  border-radius: 8px;
}
.shop-profile-section-title {
  font-size: 15px;
  font-weight: 500;
}
.shop-profile-hint {
  font-size: 12px;
  color: #8c8c8c;
  margin-top: 4px;
}
.shop-profile-actions {
  display: flex;
  justify-content: flex-end;
}
</style>
