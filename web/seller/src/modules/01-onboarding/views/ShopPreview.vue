<script setup lang="ts">
import { ref, onMounted } from 'vue'
import {
  Breadcrumb,
  BreadcrumbItem,
  Card,
  Skeleton,
  Avatar,
  Typography,
} from 'ant-design-vue'
import { ShopOutlined } from '@ant-design/icons-vue'
import { shopApi } from '../api/shop.api'
import type { ShopInfoDto } from '../types/shop.dto'
import { StatusTag, EmptyState } from '@/shared/components'
import { logger } from '@/shared/utils/logger'

/**
 * 店铺前台预览页（只读）
 *
 * 路由 /shop/preview，权限 shop:profile:view
 * GET /api/shops/me 拉取资料，以卡片形式模拟买家视角展示。
 */

const loading = ref(false)
const shop = ref<ShopInfoDto | null>(null)

async function loadShop(): Promise<void> {
  loading.value = true
  try {
    shop.value = await shopApi.getMyShop()
  } catch (e) {
    logger.error('加载店铺预览失败', e)
  } finally {
    loading.value = false
  }
}

onMounted(() => {
  void loadShop()
})
</script>

<template>
  <div class="shop-preview-page">
    <Breadcrumb class="shop-preview-breadcrumb">
      <BreadcrumbItem>店铺设置</BreadcrumbItem>
      <BreadcrumbItem>店铺预览</BreadcrumbItem>
    </Breadcrumb>

    <Skeleton v-if="loading" active :paragraph="{ rows: 6 }" />
    <EmptyState
      v-else-if="!shop"
      description="暂无店铺资料，无法预览"
      action-text="去完善资料"
      @action="$router.push('/shop/profile')"
    />
    <div v-else class="shop-preview-body">
      <!-- 店铺头部 -->
      <Card class="shop-preview-card" :bordered="true">
        <div class="shop-preview-header">
          <Avatar
            :size="72"
            :src="shop.logo || undefined"
            class="shop-preview-logo"
          >
            <ShopOutlined v-if="!shop.logo" />
          </Avatar>
          <div class="shop-preview-header-info">
            <div class="shop-preview-name-row">
              <span class="shop-preview-name">{{ shop.name }}</span>
              <StatusTag type="shop" :status="shop.status" />
            </div>
            <div class="shop-preview-category">
              主营类目：{{ shop.mainCategory || '—' }}
            </div>
          </div>
        </div>
      </Card>

      <!-- 店铺描述 -->
      <Card class="shop-preview-card" :bordered="true">
        <template #title>
          <span class="shop-preview-section-title">店铺描述</span>
        </template>
        <Typography.Paragraph>
          {{ shop.description || '该店铺暂未填写描述。' }}
        </Typography.Paragraph>
      </Card>

      <!-- 客服联系方式 -->
      <Card class="shop-preview-card" :bordered="true">
        <template #title>
          <span class="shop-preview-section-title">客服联系方式</span>
        </template>
        <div class="shop-preview-cs-list">
          <div class="shop-preview-cs-row">
            <span class="shop-preview-cs-label">客服电话</span>
            <span class="shop-preview-cs-value">
              {{ shop.customerService?.phone || '—' }}
            </span>
          </div>
          <div class="shop-preview-cs-row">
            <span class="shop-preview-cs-label">客服邮箱</span>
            <span class="shop-preview-cs-value">
              {{ shop.customerService?.email || '—' }}
            </span>
          </div>
          <div class="shop-preview-cs-row">
            <span class="shop-preview-cs-label">在线客服账号</span>
            <span class="shop-preview-cs-value">
              {{ shop.customerService?.onlineAccount || '—' }}
            </span>
          </div>
        </div>
      </Card>
    </div>
  </div>
</template>

<style scoped>
.shop-preview-page {
  display: flex;
  flex-direction: column;
  gap: 16px;
}
.shop-preview-breadcrumb {
  font-size: 14px;
}
.shop-preview-body {
  display: flex;
  flex-direction: column;
  gap: 16px;
}
.shop-preview-card {
  border-radius: 8px;
}
.shop-preview-header {
  display: flex;
  align-items: center;
  gap: 16px;
}
.shop-preview-logo {
  background: #fafafa;
  color: #8c8c8c;
  flex-shrink: 0;
}
.shop-preview-header-info {
  display: flex;
  flex-direction: column;
  gap: 6px;
}
.shop-preview-name-row {
  display: flex;
  align-items: center;
  gap: 12px;
}
.shop-preview-name {
  font-size: 20px;
  font-weight: 500;
  color: #000000d9;
}
.shop-preview-category {
  font-size: 13px;
  color: #8c8c8c;
}
.shop-preview-section-title {
  font-size: 15px;
  font-weight: 500;
}
.shop-preview-cs-list {
  display: flex;
  flex-direction: column;
  gap: 12px;
}
.shop-preview-cs-row {
  display: flex;
  align-items: center;
  gap: 16px;
}
.shop-preview-cs-label {
  width: 120px;
  color: #8c8c8c;
  font-size: 13px;
}
.shop-preview-cs-value {
  color: #000000d9;
  font-size: 14px;
}
</style>
