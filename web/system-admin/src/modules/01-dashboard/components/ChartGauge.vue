<template>
  <a-card :bordered="true" class="chart-gauge">
    <template #title>
      <span class="chart-gauge__title">{{ title }}</span>
    </template>
    <a-spin :spinning="loading">
      <div v-show="!loading" ref="chartRef" class="chart-gauge__canvas" :style="{ height: `${height}px` }" />
    </a-spin>
  </a-card>
</template>

<script setup lang="ts">
import { ref, watch, onMounted, onBeforeUnmount, nextTick } from 'vue'
import * as echarts from 'echarts/core'
import { GaugeChart } from 'echarts/charts'
import { CanvasRenderer } from 'echarts/renderers'

echarts.use([GaugeChart, CanvasRenderer])

interface Props {
  value: number
  title: string
  height?: number
  thresholds?: [number, number]
  loading?: boolean
}

const props = withDefaults(defineProps<Props>(), {
  height: 200,
  thresholds: () => [80, 95] as [number, number],
  loading: false,
})

const chartRef = ref<HTMLDivElement | null>(null)
let chart: echarts.ECharts | null = null

// 根据阈值返回颜色
function getColor(value: number): string {
  const [low, mid] = props.thresholds
  if (value < low) return '#FF4D4F'
  if (value < mid) return '#FAAD14'
  return '#52C41A'
}

function renderChart() {
  if (!chartRef.value || props.loading) return
  if (chart) {
    chart.dispose()
  }
  chart = echarts.init(chartRef.value)
  const color = getColor(props.value)
  chart.setOption({
    series: [
      {
        type: 'gauge',
        min: 0,
        max: 100,
        progress: { show: true, width: 18 },
        axisLine: { lineStyle: { width: 18, color: [[0.8, '#FF4D4F'], [0.95, '#FAAD14'], [1, '#52C41A']] } },
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
        data: [{ value: props.value, itemStyle: { color } }],
      },
    ],
  })
}

function handleResize() {
  chart?.resize()
}

onMounted(async () => {
  await nextTick()
  renderChart()
  window.addEventListener('resize', handleResize)
})

onBeforeUnmount(() => {
  window.removeEventListener('resize', handleResize)
  chart?.dispose()
  chart = null
})

watch(() => [props.value, props.loading, props.thresholds], async () => {
  await nextTick()
  renderChart()
}, { deep: true })
</script>

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
</style>
