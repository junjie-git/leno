<script setup lang="ts">
import { computed } from 'vue'
import { Spin, Empty } from 'ant-design-vue'
import VChart from 'vue-echarts'
import type { EChartsOption } from 'echarts'

/**
 * 柱状图组件
 *
 * 包装 vue-echarts，预设主题色（与 design-tokens.css --c-primary 一致）。
 */
const props = withDefaults(
  defineProps<{
    /** 柱状数据序列 */
    series: EChartsOption['series']
    /** X 轴标签数组 */
    xAxis: string[]
    /** 加载中 */
    loading?: boolean
    /** 高度（px），默认 320 */
    height?: number
  }>(),
  {
    loading: false,
    height: 320,
  },
)

const option = computed<EChartsOption>(() => ({
  tooltip: { trigger: 'axis', axisPointer: { type: 'shadow' } },
  legend: { top: 0 },
  grid: { left: '3%', right: '4%', bottom: '3%', containLabel: true },
  xAxis: {
    type: 'category',
    data: props.xAxis,
  },
  yAxis: { type: 'value' },
  series: props.series,
  color: ['#1677FF', '#52C41A', '#FAAD14', '#FF4D4F', '#722ED1'],
}))

const hasData = computed(() => Array.isArray(props.series) && props.series.length > 0)
</script>

<template>
  <div class="chart-bar" :style="{ height: `${height}px` }">
    <Spin v-if="loading" tip="加载中..." class="chart-spin" />
    <Empty v-else-if="!hasData" description="暂无数据" />
    <VChart v-else :option="option" autoresize />
  </div>
</template>

<style scoped>
.chart-bar {
  width: 100%;
  position: relative;
}
.chart-spin {
  display: flex;
  align-items: center;
  justify-content: center;
  height: 100%;
}
</style>
