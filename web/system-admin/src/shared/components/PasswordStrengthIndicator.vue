<script setup lang="ts">
import { computed } from 'vue'

const props = defineProps<{
  password: string
}>()

type Strength = 'weak' | 'medium' | 'strong'

const strength = computed<Strength>(() => {
  const pwd = props.password
  if (!pwd) return 'weak'
  if (pwd.length < 8) return 'weak'
  const categories = countCategories(pwd)
  if (pwd.length < 12) {
    return categories >= 2 ? 'medium' : 'weak'
  }
  return categories >= 3 ? 'strong' : (categories >= 2 ? 'medium' : 'weak')
})

function countCategories(s: string): number {
  let count = 0
  if (/[a-z]/.test(s)) count++
  if (/[A-Z]/.test(s)) count++
  if (/[0-9]/.test(s)) count++
  if (/[^a-zA-Z0-9]/.test(s)) count++
  return count
}

const label = computed(() => {
  if (!props.password) return ''
  const map: Record<Strength, string> = { weak: '弱', medium: '中', strong: '强' }
  return map[strength.value] ?? ''
})

const color = computed(() => {
  const map: Record<Strength, string> = { weak: '#ff4d4f', medium: '#faad14', strong: '#52c41a' }
  return map[strength.value] ?? '#ff4d4f'
})

const segments = computed(() => {
  const map: Record<Strength, number> = { weak: 1, medium: 2, strong: 3 }
  const filled = map[strength.value] ?? 0
  return [1, 2, 3].map((i) => ({
    active: i <= filled && !!props.password,
    color: color.value,
  }))
})
</script>

<template>
  <div v-if="password" class="password-strength">
    <div class="segments">
      <div
        v-for="(seg, i) in segments"
        :key="i"
        class="segment"
        :style="{ backgroundColor: seg.active ? seg.color : '#f0f0f0' }"
      />
    </div>
    <span class="label" :style="{ color }">{{ label }}</span>
  </div>
</template>

<style scoped>
.password-strength {
  display: flex;
  align-items: center;
  gap: 8px;
  margin-top: 4px;
}
.segments {
  display: flex;
  gap: 4px;
  flex: 1;
}
.segment {
  height: 4px;
  flex: 1;
  border-radius: 2px;
  transition: background-color 0.2s;
}
.label {
  font-size: 12px;
  min-width: 16px;
}
</style>
