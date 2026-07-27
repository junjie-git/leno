<script setup lang="ts">
import { computed } from 'vue'

/**
 * JSON 查看器
 *
 * 用于死信 payload、健康检查 detail 等结构化数据展示。
 * 简单实现：用 JSON.stringify + pre 标签；未来可替换为 react-json-view 等组件。
 */
const props = withDefaults(
  defineProps<{
    /** 待展示的数据 */
    data: unknown
    /** 最大高度（px），超出滚动 */
    maxHeight?: number
  }>(),
  {
    maxHeight: 400,
  },
)

const formatted = computed(() => {
  try {
    return JSON.stringify(props.data, null, 2)
  } catch (e) {
    return `// 序列化失败：${e instanceof Error ? e.message : String(e)}`
  }
})
</script>

<template>
  <pre class="json-viewer" :style="{ maxHeight: `${maxHeight}px` }">{{ formatted }}</pre>
</template>

<style scoped>
.json-viewer {
  margin: 0;
  padding: 12px;
  background: #f5f5f5;
  border-radius: 6px;
  font-family: var(--ff-mono, 'SF Mono', 'Cascadia Code', Consolas, monospace);
  font-size: 12px;
  line-height: 1.5;
  color: #595959;
  overflow: auto;
  white-space: pre-wrap;
  word-break: break-all;
}
</style>
