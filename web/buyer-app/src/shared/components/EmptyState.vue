<script setup lang="ts">
/**
 * 空状态（对齐设计稿 empty-state：插画 + 标题 + CTA）
 */
withDefaults(
  defineProps<{
    title: string
    /** CTA 文案（不传则不展示按钮） */
    actionText?: string
  }>(),
  {
    actionText: '',
  },
)

const emit = defineEmits<{
  action: []
}>()
</script>

<template>
  <div class="empty-state">
    <svg class="illustration" viewBox="0 0 120 120" fill="none">
      <rect x="20" y="34" width="80" height="60" rx="6" fill="#F5F5F5" />
      <rect x="30" y="46" width="40" height="8" rx="4" fill="#E0E0E0" />
      <rect x="30" y="62" width="60" height="8" rx="4" fill="#E8E8E8" />
      <rect x="30" y="76" width="28" height="8" rx="4" fill="#E8E8E8" />
      <circle cx="92" cy="36" r="14" fill="#FAFAFA" stroke="#E0E0E0" stroke-width="1.5" />
      <path d="M92 30v6l4 2" stroke="#BFBFBF" stroke-width="1.6" stroke-linecap="round" />
    </svg>
    <div class="title">{{ title }}</div>
    <van-button v-if="actionText" type="primary" size="small" round class="cta" @click="emit('action')">
      {{ actionText }}
    </van-button>
  </div>
</template>

<style scoped>
.empty-state {
  display: flex;
  flex-direction: column;
  align-items: center;
  justify-content: center;
  padding: 64px 0;
}

.illustration {
  width: 120px;
  height: 120px;
}

.title {
  margin-top: var(--s4);
  font-size: var(--fs-base);
  color: var(--n7);
}

.cta {
  margin-top: var(--s4);
  padding: 0 20px;
}
</style>
