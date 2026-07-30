<script setup lang="ts">
import { ref, onMounted, h } from 'vue'
import {
  Breadcrumb,
  BreadcrumbItem,
  Card,
  Table,
  Button,
  Upload,
  Select,
  Tag,
  Tooltip,
  Skeleton,
  message,
} from 'ant-design-vue'
import type { TableColumnsType } from 'ant-design-vue'
import type { UploadProps } from 'ant-design-vue'
import { UploadOutlined } from '@ant-design/icons-vue'
import { shopApi } from '../api/shop.api'
import type {
  QualificationDto,
  QualificationType,
  QualificationStatus,
} from '../types/shop.dto'
import { EmptyState } from '@/shared/components'
import { logger } from '@/shared/utils/logger'
import { formatDateTime } from '@/shared/utils/format'

/**
 * 资质管理页
 *
 * 路由 /shop/qualifications，权限 shop:qualification:upload
 * - 资质列表表格（类型 / 文件名 / 状态 / 提交时间 / 审核时间 / 操作）
 * - 上传按钮（Upload，accept .jpg,.png,.pdf，maxSize 5MB，beforeUpload 校验）
 * - 资质类型选择（Select：营业执照 / 身份证 / 银行账户信息 / 其他）
 */

const loading = ref(false)
const uploading = ref(false)
const qualifications = ref<QualificationDto[]>([])
const uploadType = ref<QualificationType>('BusinessLicense')

const MAX_SIZE = 5 * 1024 * 1024

const typeLabels: Record<QualificationType, string> = {
  BusinessLicense: '营业执照',
  IdCard: '身份证',
  BankAccount: '银行账户信息',
  Other: '其他',
}

const statusMeta: Record<QualificationStatus, { color: string; label: string }> = {
  Pending: { color: 'warning', label: '待审核' },
  Approved: { color: 'success', label: '已通过' },
  Rejected: { color: 'error', label: '已驳回' },
}

const typeOptions: Array<{ label: string; value: QualificationType }> = [
  { label: '营业执照', value: 'BusinessLicense' },
  { label: '身份证', value: 'IdCard' },
  { label: '银行账户信息', value: 'BankAccount' },
  { label: '其他', value: 'Other' },
]

const columns: TableColumnsType = [
  { title: '类型', dataIndex: 'type', key: 'type', width: 140 },
  { title: '文件名', dataIndex: 'fileName', key: 'fileName', ellipsis: true },
  { title: '状态', dataIndex: 'status', key: 'status', width: 120 },
  { title: '提交时间', dataIndex: 'submittedAt', key: 'submittedAt', width: 180 },
  { title: '审核时间', dataIndex: 'auditedAt', key: 'auditedAt', width: 180 },
  { title: '操作', key: 'action', width: 100 },
]

async function loadList(): Promise<void> {
  loading.value = true
  try {
    qualifications.value = await shopApi.listQualifications()
  } catch (e) {
    logger.error('加载资质列表失败', e)
    message.error('加载资质列表失败')
  } finally {
    loading.value = false
  }
}

const beforeUpload: UploadProps['beforeUpload'] = (file) => {
  const acceptList = ['.jpg', '.png', '.pdf']
  const ext = '.' + (file.name.split('.').pop() ?? '').toLowerCase()
  if (!acceptList.includes(ext)) {
    message.error('仅支持 .jpg / .png / .pdf 文件')
    return false
  }
  if (file.size > MAX_SIZE) {
    message.error('文件大小超过 5MB 限制')
    return false
  }
  // 校验通过返回 true，由 customRequest 接管上传（data URL 化 / 后端调用）
  return true
}

const customRequest: UploadProps['customRequest'] = async (options) => {
  const { file, onSuccess, onError } = options
  const raw = file as File
  uploading.value = true
  try {
    const qual = await shopApi.uploadQualification({ file: raw, type: uploadType.value })
    qualifications.value = [...qualifications.value, qual]
    message.success('资质上传成功，等待审核')
    onSuccess?.({ url: qual.fileUrl }, file)
  } catch (e) {
    logger.error('上传资质失败', e)
    message.error('上传资质失败')
    onError?.(new Error('上传资质失败'))
  } finally {
    uploading.value = false
  }
}

onMounted(() => {
  void loadList()
})
</script>

<template>
  <div class="shop-qualifications-page">
    <Breadcrumb class="shop-qualifications-breadcrumb">
      <BreadcrumbItem>店铺设置</BreadcrumbItem>
      <BreadcrumbItem>资质管理</BreadcrumbItem>
    </Breadcrumb>

    <Card class="shop-qualifications-card" :bordered="true">
      <template #title>
        <span class="shop-qualifications-title">资质管理</span>
      </template>
      <template #extra>
        <div class="shop-qualifications-upload-bar">
          <Select
            v-model:value="uploadType"
            :options="typeOptions"
            style="width: 160px"
            placeholder="选择资质类型"
          />
          <Upload
            :before-upload="beforeUpload"
            :custom-request="customRequest"
            :show-upload-list="false"
            accept=".jpg,.png,.pdf"
          >
            <Button :icon="h(UploadOutlined)" :loading="uploading">上传资质</Button>
          </Upload>
        </div>
      </template>

      <Skeleton v-if="loading" active :paragraph="{ rows: 5 }" />
      <EmptyState
        v-else-if="qualifications.length === 0"
        description="暂无资质文件，请点击右上角「上传资质」"
      />
      <Table
        v-else
        :columns="columns"
        :data-source="qualifications"
        row-key="id"
        :pagination="false"
        size="middle"
      >
        <template #bodyCell="{ column, record }">
          <template v-if="column.key === 'type'">
            {{ typeLabels[record.type as QualificationType] || record.type }}
          </template>
          <template v-else-if="column.key === 'status'">
            <Tooltip v-if="record.status === 'Rejected' && record.rejectReason">
              <template #title>驳回原因：{{ record.rejectReason }}</template>
              <Tag :color="statusMeta[record.status as QualificationStatus].color">
                {{ statusMeta[record.status as QualificationStatus].label }}
              </Tag>
            </Tooltip>
            <Tag v-else :color="statusMeta[record.status as QualificationStatus].color">
              {{ statusMeta[record.status as QualificationStatus].label }}
            </Tag>
          </template>
          <template v-else-if="column.key === 'submittedAt'">
            {{ formatDateTime(record.submittedAt) }}
          </template>
          <template v-else-if="column.key === 'auditedAt'">
            {{ formatDateTime(record.auditedAt) }}
          </template>
          <template v-else-if="column.key === 'action'">
            <Button type="link" size="small" disabled>查看</Button>
          </template>
        </template>
      </Table>
    </Card>
  </div>
</template>

<style scoped>
.shop-qualifications-page {
  display: flex;
  flex-direction: column;
  gap: 16px;
}
.shop-qualifications-breadcrumb {
  font-size: 14px;
}
.shop-qualifications-card {
  border-radius: 8px;
}
.shop-qualifications-title {
  font-size: 15px;
  font-weight: 500;
}
.shop-qualifications-upload-bar {
  display: flex;
  align-items: center;
  gap: 8px;
}
</style>
