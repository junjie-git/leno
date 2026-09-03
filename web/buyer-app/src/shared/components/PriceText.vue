<script setup lang="ts">
import { computed } from 'vue'
import { formatPrice } from '@/shared/utils/format'

/**
 * 价格文本：¥ 符号小号 + 整数大号 + 小数小号（对齐设计稿价格排版）
 *
 * - amount 单位为「分」，内部转元展示
 * - color 默认使用电商价签红（--c-error），可通过 prop 覆盖
 */
const props = withDefaults(
  defineProps<{
    /** 金额（分） */
    amount: number
    /** 颜色：默认价签红 */
    color?: string
    /** 字号（整数部分，px） */
    size?: number
    /** 是否展示原价删除线（原价，分） */
    original?: number
  }>(),
  {
    color: 'var(--c-error)',
    size: 16,
    original: undefined,
  },
)

const parts = computed(() => {
  const text = formatPrice(props.amount)
  const dot = text.indexOf('.')
  if (dot < 0) {
    return { int: text, dec: '' }
  }
  return { int: text.slice(0, dot), dec: text.slice(dot) }
})

const originalText = computed(() => (props.original != null ? `¥${formatPrice(props.original)}` : ''))
</script>

<template>
  <span class="price-text" :style="{ color }">
    <span class="symbol" :style="{ fontSize: `${Math.max(11, Math.round(size * 0.72))}px` }">¥</span>
    <span class="int" :style="{ fontSize: `${size}px` }">{{ parts.int }}</span>
    <span v-if="parts.dec" class="dec" :style="{ fontSize: `${Math.max(11, Math.round(size * 0.72))}px` }">{{
      parts.dec
    }}</span>
    <span v-if="originalText" class="original">¥{{ originalText.slice(1) }}</span>
  </span>
</template>

<style scoped>
.price-text {
  display: inline-flex;
  align-items: baseline;
  font-weight: var(--fw-semibold);
  line-height: 1;
}

.symbol {
  margin-right: 1px;
}

.original {
  margin-left: 6px;
  color: var(--n7);
  font-size: var(--fs-sm);
  font-weight: var(--fw-normal);
  text-decoration: line-through;
}
</style>
