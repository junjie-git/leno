<script setup lang="ts">
import { Empty, Button } from 'ant-design-vue'

/**
 * 空态组件
 *
 * 包装 ant-design-vue 的 Empty，补充可选 CTA 按钮。
 * 与 spec §5.8 加载/空/错误三态保持一致。
 */
const props = withDefaults(
  defineProps<{
    /** 空态描述文案 */
    description: string
    /** CTA 按钮文案，不传则不显示按钮 */
    actionText?: string
  }>(),
  {
    actionText: undefined,
  },
)

const emit = defineEmits<{
  (e: 'action'): void
}>()

function onAction() {
  emit('action')
}
</script>

<template>
  <Empty :description="description">
    <template v-if="props.actionText" #default>
      <Button type="primary" @click="onAction">{{ props.actionText }}</Button>
    </template>
  </Empty>
</template>
