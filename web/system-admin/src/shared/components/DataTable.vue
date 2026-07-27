<script setup lang="ts">
import { ref, onMounted, watch, h } from 'vue'
import { Table, Button, Space } from 'ant-design-vue'
import { ReloadOutlined } from '@ant-design/icons-vue'
import EmptyState from './EmptyState.vue'
import type { TableColumn, PageResult, PageQuery } from '@/shared/types'
import { logger } from '@/shared/utils/logger'

/**
 * 通用数据表格组件
 *
 * 包装 ant-design-vue Table，统一分页/筛选/空态/错误/加载四态。
 * 调用方提供 columns 与 fetcher，组件自动管理分页状态与数据加载。
 */

const props = defineProps<{
  /** 列定义 */
  columns: TableColumn[]
  /** 数据获取函数，返回 PageResult */
  fetcher: (params: PageQuery & Record<string, unknown>) => Promise<PageResult<unknown>>
  /** 行 key */
  rowKey: string | ((record: unknown) => string)
  /** 每页条数，默认 10 */
  pageSize?: number
  /** 额外查询参数，变化时触发重新加载 */
  queryParams?: Record<string, unknown>
}>()

const dataSource = ref<unknown[]>([])
const total = ref(0)
const currentPage = ref(1)
const currentPageSize = ref(props.pageSize ?? 10)
const loading = ref(false)
const errorMessage = ref<string | null>(null)

async function loadData() {
  loading.value = true
  errorMessage.value = null
  try {
    const result = await props.fetcher({
      page: currentPage.value,
      pageSize: currentPageSize.value,
      ...(props.queryParams ?? {}),
    })
    dataSource.value = result.items
    total.value = result.total
  } catch (e) {
    logger.error('DataTable 加载失败', e)
    errorMessage.value = e instanceof Error ? e.message : '加载失败'
    dataSource.value = []
    total.value = 0
  } finally {
    loading.value = false
  }
}

function onPageChange(pagination: { current?: number; pageSize?: number }) {
  if (pagination.current !== undefined) currentPage.value = pagination.current
  if (pagination.pageSize !== undefined) currentPageSize.value = pagination.pageSize
  void loadData()
}

function onRefresh() {
  void loadData()
}

onMounted(() => {
  void loadData()
})

watch(
  () => props.queryParams,
  () => {
    currentPage.value = 1
    void loadData()
  },
  { deep: true },
)
</script>

<template>
  <div class="data-table-wrap">
    <div class="data-table-toolbar">
      <Space>
        <Button data-testid="refresh" :icon="h(ReloadOutlined)" @click="onRefresh">刷新</Button>
      </Space>
    </div>
    <div v-if="errorMessage" class="data-table-error">
      <EmptyState :description="`加载失败：${errorMessage}`" action-text="重试" @action="onRefresh" />
    </div>
    <Table
      v-else
      :columns="columns"
      :data-source="dataSource"
      :row-key="rowKey"
      :loading="loading"
      :pagination="{
        current: currentPage,
        pageSize: currentPageSize,
        total,
        showSizeChanger: true,
        showTotal: (t: number) => `共 ${t} 条`,
      }"
      size="middle"
      @change="onPageChange as any"
    >
      <template #bodyCell="{ column, record }">
        <slot name="bodyCell" :column="column" :record="record" />
      </template>
    </Table>
  </div>
</template>

<style scoped>
.data-table-wrap {
  width: 100%;
}
.data-table-toolbar {
  margin-bottom: 12px;
  display: flex;
  justify-content: flex-end;
}
.data-table-error {
  padding: 24px;
  text-align: center;
}
</style>
