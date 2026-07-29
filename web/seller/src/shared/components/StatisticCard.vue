<script setup lang="ts">
import { computed } from 'vue'
import { Card, Statistic, Skeleton } from 'ant-design-vue'
import { ArrowUpOutlined, ArrowDownOutlined, MinusOutlined } from '@ant-design/icons-vue'

type Status = 'success' | 'warning' | 'danger' | 'default'
type Trend = 'up' | 'down' | 'flat'

const props = withDefaults(
  defineProps<{
    title: string
    value: number | string
    unit?: string
    precision?: number
    trend?: Trend
    trendValue?: number
    status?: Status
    loading?: boolean
    suffix?: string
  }>(),
  {
    unit: '',
    precision: 0,
    status: 'default',
    loading: false,
  },
)

const statusColor = computed<Record<Status, string>>(() => ({
  success: '#52c41a',
  warning: '#faad14',
  danger: '#ff4d4f',
  default: '#1677ff',
}))

const trendIcon = computed(() => {
  if (props.trend === 'up') return ArrowUpOutlined
  if (props.trend === 'down') return ArrowDownOutlined
  return MinusOutlined
})

const trendColor = computed(() => {
  if (props.trend === 'up') return '#52c41a'
  if (props.trend === 'down') return '#ff4d4f'
  return '#8c8c8c'
})

const displayValue = computed(() => {
  if (typeof props.value === 'number') {
    return props.value.toFixed(props.precision)
  }
  return props.value
})
</script>

<template>
  <Card class="statistic-card" :bordered="true" size="small">
    <Skeleton v-if="loading" active :paragraph="{ rows: 2 }" />
    <div v-else>
      <div class="statistic-title">{{ title }}</div>
      <Statistic
        :value="displayValue"
        :suffix="suffix || unit"
        :value-style="{ color: statusColor[status], fontSize: '24px', fontWeight: 600 }"
      />
      <div v-if="trend" class="statistic-trend" :style="{ color: trendColor }">
        <component :is="trendIcon" />
        <span v-if="trendValue !== undefined" class="trend-value">{{ Math.abs(trendValue) }}</span>
      </div>
    </div>
  </Card>
</template>

<style scoped>
.statistic-card {
  height: 100%;
}
.statistic-title {
  font-size: 13px;
  color: #8c8c8c;
  margin-bottom: 8px;
}
.statistic-trend {
  display: flex;
  align-items: center;
  gap: 4px;
  font-size: 12px;
  margin-top: 4px;
}
.trend-value {
  font-weight: 500;
}
</style>
