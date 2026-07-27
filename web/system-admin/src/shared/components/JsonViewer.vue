<script setup lang="ts">
import { computed } from 'vue'

/**
 * JSON 查看器
 *
 * 用于死信 payload、健康检查 detail、缓存详情等结构化数据展示。
 * 支持两种 prop 调用方式：
 * - <JsonViewer :data="obj" />  （旧 API，向后兼容）
 * - <JsonViewer :value="obj" /> （新 API，Task 11 CacheMonitor 使用）
 */
const props = withDefaults(
  defineProps<{
    /** 待展示的数据（旧 API，与 value 二选一，value 优先） */
    data?: unknown
    /** 待展示的数据（新 API，与 data 二选一，value 优先） */
    value?: unknown
    /** 最大深度（超过深度的行标记为截断样式） */
    maxDepth?: number
    /** 最大高度（px），超出滚动 */
    maxHeight?: number
  }>(),
  {
    maxDepth: 3,
    maxHeight: 400,
  },
)

const actualValue = computed(() => {
  if (props.value !== undefined) return props.value
  return props.data
})

const formatted = computed(() => {
  const val = actualValue.value
  try {
    return JSON.stringify(val, null, 2) ?? ''
  } catch (e) {
    return `// 序列化失败：${e instanceof Error ? e.message : String(e)}`
  }
})

const lines = computed(() => formatted.value.split('\n'))

function getLineClass(line: string, depth: number): string {
  if (depth > props.maxDepth) return 'json-line json-line-truncated'
  if (line.includes(':')) return 'json-line json-line-key'
  return 'json-line'
}
</script>

<template>
  <div class="json-viewer" :style="{ maxHeight: `${maxHeight}px` }">
    <pre class="json-content"><code><template v-for="(line, i) in lines" :key="i"><span :class="getLineClass(line, 0)">{{ line }}</span>
</template></code></pre>
  </div>
</template>

<style scoped>
.json-viewer {
  background: #fafafa;
  border: 1px solid #f0f0f0;
  border-radius: 4px;
  padding: 12px;
  overflow: auto;
  font-family: var(--ff-mono, 'SF Mono', 'Cascadia Code', Consolas, monospace);
  font-size: 12px;
  line-height: 1.6;
  color: #595959;
}
.json-content {
  margin: 0;
  white-space: pre-wrap;
  word-break: break-all;
}
.json-line {
  display: block;
}
.json-line-key {
  color: #595959;
}
.json-line-truncated {
  color: #8c8c8c;
  font-style: italic;
}
</style>
