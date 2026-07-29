<script setup lang="ts">
import { computed } from 'vue'
import { RangePicker } from 'ant-design-vue'
import dayjs, { type Dayjs } from 'dayjs'

/**
 * 日期时间范围选择器
 *
 * 包装 ant-design-vue RangePicker：
 * - v-model:value 接收 [string, string] ISO 8601 UTC 字符串
 * - change 事件输出 [string, string] ISO 8601 UTC 字符串
 *
 * 与 spec §3 后端约定保持一致：所有时间字段使用 ISO 8601 UTC 字符串传输。
 */
const props = withDefaults(
  defineProps<{
    /** 当前值 [start, end] ISO 8601 UTC 字符串 */
    value?: [string, string]
    /** 是否显示时间选择，默认 false */
    showTime?: boolean
    /** 占位符 */
    placeholders?: [string, string]
    /** 是否禁用 */
    disabled?: boolean
  }>(),
  {
    value: undefined,
    showTime: false,
    placeholders: () => ['开始时间', '结束时间'],
    disabled: false,
  },
)

const emit = defineEmits<{
  (e: 'change', value: [string, string]): void
}>()

const dayjsValue = computed<[Dayjs, Dayjs] | undefined>(() => {
  if (!props.value) return undefined
  const [start, end] = props.value
  return [dayjs(start), dayjs(end)]
})

function onChange(value: [Dayjs | Date, Dayjs | Date] | null) {
  if (!value) return
  emit('change', [dayjs(value[0]).toISOString(), dayjs(value[1]).toISOString()])
}
</script>

<template>
  <RangePicker
    :value="dayjsValue"
    :show-time="showTime"
    :placeholders="placeholders"
    :disabled="disabled"
    @change="onChange as any"
  />
</template>
