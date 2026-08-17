<script setup lang="ts">
import { computed } from 'vue'
import { Button } from 'ant-design-vue'

/**
 * 按钮类型
 */
type ButtonType = 'primary' | 'default' | 'link' | 'text'

/**
 * 按钮尺寸
 */
type ButtonSize = 'small' | 'middle' | 'large'

const props = withDefaults(
  defineProps<{
    /** 按钮类型 */
    type?: ButtonType
    /** 危险样式（删除/丢弃/重投） */
    danger?: boolean
    /** 尺寸 */
    size?: ButtonSize
    /** 加载中（调用方控制，发起请求时 true，完成时 false） */
    loading?: boolean
    /** 禁用 */
    disabled?: boolean
    /** 块级宽度 */
    block?: boolean
  }>(),
  {
    type: 'primary',
    danger: false,
    size: 'middle',
    loading: false,
    disabled: false,
    block: false,
  },
)

const emit = defineEmits<{
  (e: 'click', event: MouseEvent): void
}>()

const antSize = computed<'small' | 'middle' | 'large'>(() => props.size)

function onClick(event: MouseEvent) {
  if (props.loading || props.disabled) return
  emit('click', event)
}
</script>

<template>
  <Button
    :type="type"
    :danger="danger"
    :size="antSize"
    :loading="loading"
    :disabled="disabled"
    :block="block"
    @click="onClick"
  >
    <slot />
  </Button>
</template>
