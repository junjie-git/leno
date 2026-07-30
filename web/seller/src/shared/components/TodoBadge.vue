<script setup lang="ts">
import { Badge } from 'ant-design-vue'

defineProps<{
  count: number
  label: string
}>()

const emit = defineEmits<{
  click: []
}>()
</script>

<template>
  <Badge :count="count" :overflow-count="99" :offset="[6, 0]">
    <span class="todo-badge-label" @click="emit('click')">
      {{ label }}
    </span>
  </Badge>
</template>

<style scoped>
.todo-badge-label {
  cursor: pointer;
  padding: 4px 8px;
  font-size: 14px;
  color: rgba(0, 0, 0, 0.65);
  transition: color 0.2s;
}
.todo-badge-label:hover {
  color: #1677ff;
}
</style>
