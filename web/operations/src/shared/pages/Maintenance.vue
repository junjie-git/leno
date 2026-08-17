<script setup lang="ts">
import { Result, Button } from 'ant-design-vue'

/**
 * 系统维护中页面
 *
 * 后端返回 503 时展示，提示用户稍后重试。
 */
function reload() {
  window.location.reload()
}
</script>

<template>
  <Result status="info" title="系统维护中" sub-title="系统正在维护，请稍后再来访问">
    <template #extra>
      <Button type="primary" @click="reload">刷新页面</Button>
    </template>
  </Result>
</template>
