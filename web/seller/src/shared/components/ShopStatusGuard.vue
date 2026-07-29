<script setup lang="ts">
import { computed } from 'vue'
import { Alert } from 'ant-design-vue'
import { useShopStore } from '@/shared/shop'

const props = withDefaults(defineProps<{
  requires: 'canPublish' | 'canFulfill'
  fallbackText?: string
}>(), {
  fallbackText: '当前店铺状态不允许此操作',
})

const shop = useShopStore()

const allowed = computed(() => {
  return props.requires === 'canPublish' ? shop.canPublish : shop.canFulfill
})
</script>

<template>
  <Alert
    v-if="!allowed"
    class="shop-status-guard-fallback"
    type="warning"
    show-icon
    :message="fallbackText"
  />
  <slot v-else />
</template>
