<script setup lang="ts" generic="T extends Record<string, unknown>">
import { ref, computed, watch } from 'vue'
import { Table } from 'ant-design-vue'
import type { TableColumnsType } from 'ant-design-vue'

const props = withDefaults(
  defineProps<{
    data: T[]
    columns: TableColumnsType
    rowKey: (record: T) => string
    parentKey: (record: T) => string | null
    draggable?: boolean
    expandedKeys?: string[]
  }>(),
  {
    draggable: true,
    expandedKeys: () => [],
  },
)

const emit = defineEmits<{
  (e: 'drop', payload: { dragKey: string; dropKey: string; position: 'before' | 'after' | 'inside' }): void
  (e: 'expand', keys: string[]): void
}>()

const innerExpandedKeys = ref<string[]>(props.expandedKeys)

watch(
  () => props.expandedKeys,
  (val) => {
    innerExpandedKeys.value = val
  },
)

function onExpand(keys: string[]): void {
  innerExpandedKeys.value = keys
  emit('expand', keys)
}

// 简化版拖拽：使用 antd Table 的 customRow 实现 dragstart/dragover/drop
const dragKey = ref<string | null>(null)

function onDragStart(record: T): void {
  dragKey.value = props.rowKey(record)
}

function onDragOver(e: DragEvent): void {
  e.preventDefault()
}

function onDrop(record: T, e: DragEvent): void {
  e.preventDefault()
  if (!dragKey.value) return
  const dropKey = props.rowKey(record)
  if (dragKey.value === dropKey) return
  // 简化：position 通过鼠标位置判断
  const target = e.currentTarget as HTMLElement
  const rect = target.getBoundingClientRect()
  const y = e.clientY - rect.top
  let position: 'before' | 'after' | 'inside' = 'inside'
  if (y < rect.height * 0.25) position = 'before'
  else if (y > rect.height * 0.75) position = 'after'
  emit('drop', { dragKey: dragKey.value, dropKey, position })
  dragKey.value = null
}

function customRow(record: T): Record<string, unknown> {
  return {
    draggable: props.draggable,
    onDragstart: () => onDragStart(record),
    onDragover: (e: DragEvent) => onDragOver(e),
    onDrop: (e: DragEvent) => onDrop(record, e),
  }
}

const tableProps = computed(() => ({
  columns: props.columns,
  dataSource: props.data,
  rowKey: props.rowKey as (record: T) => string,
  pagination: false as const,
  expandedRowKeys: innerExpandedKeys.value,
  'onUpdate:expandedRowKeys': onExpand,
  size: 'middle' as const,
  customRow,
}))
</script>

<template>
  <Table v-bind="tableProps">
    <template #bodyCell="{ column, record }">
      <slot name="bodyCell" :column="column" :record="record" />
    </template>
  </Table>
</template>
