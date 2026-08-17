<script setup lang="ts">
import { computed } from 'vue'
import { Card, Spin } from 'ant-design-vue'
import VChart from 'vue-echarts'
import { use } from 'echarts/core'
import { GaugeChart } from 'echarts/charts'
import { CanvasRenderer } from 'echarts/renderers'
import type { EChartsOption } from 'echarts'

use([GaugeChart, CanvasRenderer])

/**
 * 仪表盘组件
 *
 * 阈值着色（默认 [95, 99]，与运营设计稿一致）：
 * - < 95   红色 #FF4D4F
 * - 95~99  黄色 #FAAD14
 * - >= 99  绿色 #52C41A
 */
const props = withDefaults(
  defineProps<{
    /** 仪表盘数值（0-100） */
    value: number
    /** 卡片标题 */
    title: string
    /** 高度（px），默认 200 */
    height?: number
    /** 着色阈值 [红黄边界, 黄绿边界] */
    thresholds?: [number, number]
    /** 数值小数位，默认 1 */
    precision?: number
    /** 加载中 */
    loading?: boolean
  }>(),
  {
    height: 200,
    thresholds: () => [95, 99] as [number, number],
    precision: 1,
    loading: false,
  },
)

function getColor(value: number, low: number, mid: number): string {
  if (value < low) return '#FF4D4F'
  if (value < mid) return '#FAAD14'
  return '#52C41A'
}

const option = computed<EChartsOption>(() => {
  const [low, mid] = props.thresholds
  const color = getColor(props.value, low, mid)
  return {
    series: [
      {
        type: 'gauge',
        min: 0,
        max: 100,
        progress: { show: true, width: 18 },
        axisLine: {
          lineStyle: {
            width: 18,
            color: [
              [low / 100, '#FF4D4F'],
              [mid / 100, '#FAAD14'],
              [1, '#52C41A'],
            ],
          },
        },
        axisTick: { show: false },
        splitLine: { length: 10, lineStyle: { width: 2, color: '#999' } },
        axisLabel: { distance: 25, color: '#999', fontSize: 12 },
        pointer: { show: true, length: '60%', width: 5 },
        detail: {
          valueAnimation: true,
          formatter: '{value}%',
          color,
          fontSize: 24,
          fontWeight: 600,
          offsetCenter: [0, '70%'],
        },
        data: [{ value: Number(props.value.toFixed(props.precision)), itemStyle: { color } }],
      },
    ],
  }
})
</script>

<template>
  <Card class="chart-gauge" :bordered="true">
    <template #title>
      <span class="chart-gauge__title">{{ title }}</span>
    </template>
    <div class="chart-gauge__canvas" :style="{ height: `${height}px` }">
      <Spin v-if="loading" tip="加载中..." class="chart-gauge__spin" />
      <VChart v-else :option="option" autoresize />
    </div>
  </Card>
</template>

<style scoped>
.chart-gauge {
  border-radius: 8px;
}
.chart-gauge__title {
  font-size: 14px;
  font-weight: 500;
}
.chart-gauge__canvas {
  width: 100%;
}
.chart-gauge__spin {
  display: flex;
  align-items: center;
  justify-content: center;
  height: 100%;
}
</style>
