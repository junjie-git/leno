<script setup lang="ts">
import { computed } from 'vue'
import { Spin, Empty } from 'ant-design-vue'
import VChart from 'vue-echarts'
import { use } from 'echarts/core'
import { BarChart } from 'echarts/charts'
import { GridComponent, TooltipComponent } from 'echarts/components'
import { CanvasRenderer } from 'echarts/renderers'
import type { EChartsOption } from 'echarts'

use([BarChart, GridComponent, TooltipComponent, CanvasRenderer])

/**
 * 横向柱状图组件
 *
 * - y 轴类目自上而下（第 1 名在最上方），满足「按数值降序」的排行展示
 * - 柱条右侧展示数值标签，可通过 valueFormatter 自定义格式（如金额）
 * - topHighlight 时第 1 名柱条高亮为绿色（与运营设计稿 Top1 高亮一致）
 */
const props = withDefaults(
  defineProps<{
    /** y 轴类目（按展示顺序，第一个在最上方） */
    categories: string[]
    /** 与 categories 对应的数值 */
    values: number[]
    /** 系列名称（tooltip 展示用） */
    seriesName?: string
    /** 高度（px），默认 320 */
    height?: number
    /** 数值标签格式化 */
    valueFormatter?: (value: number) => string
    /** 第 1 名柱条绿色高亮 */
    topHighlight?: boolean
    /** 加载中 */
    loading?: boolean
  }>(),
  {
    seriesName: '数值',
    height: 320,
    valueFormatter: undefined,
    topHighlight: false,
    loading: false,
  },
)

const hasData = computed(
  () => Array.isArray(props.categories) && props.categories.length > 0 && props.categories.length === props.values.length,
)

const option = computed<EChartsOption>(() => ({
  tooltip: {
    trigger: 'axis',
    axisPointer: { type: 'shadow' },
    formatter: (params: unknown) => {
      const list = Array.isArray(params) ? params : [params]
      const first = list[0] as { name: string; value: number }
      const label = props.valueFormatter ? props.valueFormatter(first.value) : first.value.toLocaleString('zh-CN')
      return `${first.name}<br/>${props.seriesName}：${label}`
    },
  },
  grid: { left: '3%', right: '12%', top: '3%', bottom: '3%', containLabel: true },
  xAxis: { type: 'value' },
  yAxis: {
    type: 'category',
    inverse: true,
    data: props.categories,
    axisLabel: { width: 120, overflow: 'truncate' },
  },
  series: [
    {
      name: props.seriesName,
      type: 'bar',
      barMaxWidth: 22,
      label: {
        show: true,
        position: 'right',
        fontSize: 12,
        formatter: (p: { value: unknown }) => {
          const value = typeof p.value === 'number' ? p.value : Number(p.value)
          return props.valueFormatter ? props.valueFormatter(value) : value.toLocaleString('zh-CN')
        },
      },
      itemStyle: { color: '#1677FF', borderRadius: [0, 4, 4, 0] },
      data: props.values.map((value, index) =>
        props.topHighlight && index === 0 ? { value, itemStyle: { color: '#52C41A' } } : value,
      ),
    },
  ],
}))
</script>

<template>
  <div class="chart-bar-horizontal" :style="{ height: `${height}px` }">
    <Spin v-if="loading" tip="加载中..." class="chart-bar-horizontal__spin" />
    <Empty v-else-if="!hasData" description="暂无数据" />
    <VChart v-else :option="option" autoresize />
  </div>
</template>

<style scoped>
.chart-bar-horizontal {
  width: 100%;
  position: relative;
}
.chart-bar-horizontal__spin {
  display: flex;
  align-items: center;
  justify-content: center;
  height: 100%;
}
</style>
