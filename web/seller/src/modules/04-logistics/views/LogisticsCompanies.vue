<script setup lang="ts">
import { ref, onMounted, h } from 'vue'
import {
  Breadcrumb,
  BreadcrumbItem,
  Card,
  Table,
  Tag,
  Button,
  Skeleton,
  message,
} from 'ant-design-vue'
import type { TableColumnsType } from 'ant-design-vue'
import { CopyOutlined, LinkOutlined } from '@ant-design/icons-vue'
import { logisticsCompanyApi } from '../api/logistics-company.api'
import type { LogisticsCompanyDto } from '../types/logistics-company.dto'
import { EmptyState } from '@/shared/components'
import { logger } from '@/shared/utils/logger'

/**
 * 物流公司页（只读）
 *
 * 路由 /logistics/companies，权限 logistics-company:list
 * - 只读表格（名称 / 编码 / 客服电话 / 是否支持轨迹查询 / 官网链接）
 * - 10 分钟前端缓存：localStorage 键 logistics_companies_cache，含 data + fetchedAt
 * - 复制编码功能：点击编码复制到剪贴板
 */

const CACHE_KEY = 'logistics_companies_cache'
const CACHE_TTL = 10 * 60 * 1000 // 10 分钟

interface CacheEntry {
  data: LogisticsCompanyDto[]
  fetchedAt: number
}

const loading = ref(false)
const companies = ref<LogisticsCompanyDto[]>([])

const columns: TableColumnsType = [
  { title: '名称', dataIndex: 'name', key: 'name', width: 180, ellipsis: true },
  { title: '编码', dataIndex: 'code', key: 'code', width: 140 },
  { title: '客服电话', dataIndex: 'servicePhone', key: 'servicePhone', width: 140 },
  { title: '支持轨迹查询', dataIndex: 'supportsTracking', key: 'supportsTracking', width: 140, align: 'center' },
  { title: '官网', dataIndex: 'website', key: 'website', ellipsis: true },
]

function readCache(): LogisticsCompanyDto[] | null {
  try {
    const raw = localStorage.getItem(CACHE_KEY)
    if (!raw) return null
    const entry = JSON.parse(raw) as CacheEntry
    if (Date.now() - entry.fetchedAt > CACHE_TTL) return null
    return entry.data
  } catch {
    return null
  }
}

function writeCache(data: LogisticsCompanyDto[]): void {
  const entry: CacheEntry = { data, fetchedAt: Date.now() }
  localStorage.setItem(CACHE_KEY, JSON.stringify(entry))
}

async function loadList(): Promise<void> {
  // 优先读缓存
  const cached = readCache()
  if (cached) {
    companies.value = cached
    return
  }
  loading.value = true
  try {
    const data = await logisticsCompanyApi.listEnabled()
    companies.value = data
    writeCache(data)
  } catch (e) {
    logger.error('加载物流公司列表失败', e)
    message.error('加载物流公司列表失败')
  } finally {
    loading.value = false
  }
}

async function onCopyCode(code: string): Promise<void> {
  try {
    await navigator.clipboard.writeText(code)
    message.success(`已复制编码：${code}`)
  } catch {
    // 降级方案：使用 textarea
    const textarea = document.createElement('textarea')
    textarea.value = code
    document.body.appendChild(textarea)
    textarea.select()
    try {
      document.execCommand('copy')
      message.success(`已复制编码：${code}`)
    } catch {
      message.error('复制失败，请手动复制')
    }
    document.body.removeChild(textarea)
  }
}

onMounted(() => {
  void loadList()
})
</script>

<template>
  <div class="logistics-companies-page">
    <Breadcrumb class="logistics-companies-breadcrumb">
      <BreadcrumbItem>物流管理</BreadcrumbItem>
      <BreadcrumbItem>物流公司</BreadcrumbItem>
    </Breadcrumb>

    <Card class="logistics-companies-card" :bordered="true">
      <template #title>
        <span class="logistics-companies-title">物流公司</span>
      </template>
      <template #extra>
        <span class="logistics-companies-cache-hint">
          数据缓存 10 分钟
        </span>
      </template>

      <Skeleton v-if="loading" active :paragraph="{ rows: 5 }" />
      <EmptyState
        v-else-if="companies.length === 0"
        description="暂无启用的物流公司"
      />
      <Table
        v-else
        :columns="columns"
        :data-source="companies"
        row-key="id"
        :pagination="false"
        size="middle"
      >
        <template #bodyCell="{ column, record }">
          <template v-if="column.key === 'code'">
            <Button
              type="link"
              size="small"
              :icon="h(CopyOutlined)"
              @click="onCopyCode(record.code)"
            >
              {{ record.code }}
            </Button>
          </template>
          <template v-else-if="column.key === 'servicePhone'">
            {{ record.servicePhone || '—' }}
          </template>
          <template v-else-if="column.key === 'supportsTracking'">
            <Tag v-if="record.supportsTracking" color="success">支持</Tag>
            <Tag v-else color="default">不支持</Tag>
          </template>
          <template v-else-if="column.key === 'website'">
            <a
              v-if="record.website"
              :href="record.website"
              target="_blank"
              rel="noopener noreferrer"
            >
              <LinkOutlined />
              {{ record.website }}
            </a>
            <span v-else>—</span>
          </template>
        </template>
      </Table>
    </Card>
  </div>
</template>

<style scoped>
.logistics-companies-page {
  display: flex;
  flex-direction: column;
  gap: 16px;
}
.logistics-companies-breadcrumb {
  font-size: 14px;
}
.logistics-companies-card {
  border-radius: 8px;
}
.logistics-companies-title {
  font-size: 15px;
  font-weight: 500;
}
.logistics-companies-cache-hint {
  font-size: 12px;
  color: #8c8c8c;
}
</style>
