<script setup lang="ts">
import { ConfigProvider } from 'vant'
import { vantTheme } from '@/shared/tokens/vant-theme'

/**
 * 全局 Vant 主题与本地化容器
 *
 * Vant 4 默认文案即为中文，无需额外注入 locale；
 * 主题通过 theme-vars 注入设计令牌（与 design-tokens.css 一致）。
 */
</script>

<template>
  <ConfigProvider :theme-vars="vantTheme">
    <slot />
  </ConfigProvider>
</template>
