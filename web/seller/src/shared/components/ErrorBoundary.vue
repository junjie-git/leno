<script setup lang="ts">
import { ref, onErrorCaptured } from 'vue'
import { Result, Button } from 'ant-design-vue'
import { logger } from '@/shared/utils/logger'

/**
 * 错误边界组件
 *
 * 捕获子组件树抛出的错误，渲染 fallback 内容。
 * 与 spec §3.10 全局错误处理、§5.8 三态保持一致。
 *
 * 用法：
 * ```vue
 * <ErrorBoundary>
 *   <template #default>
 *     <Dashboard />
 *   </template>
 *   <template #fallback="{ error, retry }">
 *     <div>出错了：{{ error.message }} <button @click="retry">重试</button></div>
 *   </template>
 * </ErrorBoundary>
 * ```
 */
const error = ref<Error | null>(null)
const boomKey = ref(0)

onErrorCaptured((err) => {
  error.value = err instanceof Error ? err : new Error(String(err))
  logger.error('ErrorBoundary 捕获错误', err)
  // 阻止错误继续向上传播
  return false
})

function retry() {
  error.value = null
  boomKey.value += 1
}
</script>

<template>
  <slot v-if="!error" :key="boomKey" />
  <slot v-else name="fallback" :error="error" :retry="retry">
    <Result
      status="error"
      title="加载失败"
      :sub-title="error.message"
    >
      <template #extra>
        <Button type="primary" @click="retry">重试</Button>
      </template>
    </Result>
  </slot>
</template>
