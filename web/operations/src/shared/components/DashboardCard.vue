<script setup lang="ts">
import { computed } from 'vue'
import { Card, Tooltip, Skeleton } from 'ant-design-vue'
import { ArrowUpOutlined, ArrowDownOutlined, InfoCircleOutlined } from '@ant-design/icons-vue'

interface Trend {
  value: number
  direction: 'up' | 'down'
}

interface Props {
  title: string
  value: number | string
  unit?: string
  trend?: Trend
  loading?: boolean
  tooltip?: string
  description?: string
  valueColor?: string
  bordered?: boolean
}

const props = withDefaults(defineProps<Props>(), {
  unit: '',
  loading: false,
  tooltip: '',
  description: '',
  valueColor: '',
  bordered: true,
})

const emit = defineEmits<{ click: [] }>()

const formattedValue = computed(() => {
  if (typeof props.value === 'string') return props.value
  const formatted = props.value.toLocaleString('zh-CN')
  return props.unit ? `${formatted} ${props.unit}` : formatted
})

const trendValueClass = computed(() => {
  if (!props.trend) return ''
  return props.trend.direction === 'up'
    ? 'dashboard-card__trend-value dashboard-card__trend-value--up'
    : 'dashboard-card__trend-value dashboard-card__trend-value--down'
})

function handleClick() {
  emit('click')
}
</script>

<template>
  <Card class="dashboard-card" :bordered="bordered" hoverable @click="handleClick">
    <div class="dashboard-card__header">
      <span class="dashboard-card__title">{{ title }}</span>
      <Tooltip v-if="tooltip" :title="tooltip">
        <InfoCircleOutlined class="dashboard-card__info-icon" />
      </Tooltip>
    </div>
    <div class="dashboard-card__value" :style="{ color: valueColor || undefined }">
      <Skeleton v-if="loading" :title="{ width: '60%' }" :paragraph="false" active />
      <span v-else class="dashboard-card__number">{{ formattedValue }}</span>
    </div>
    <div v-if="trend" class="dashboard-card__trend">
      <ArrowUpOutlined v-if="trend.direction === 'up'" class="dashboard-card__arrow dashboard-card__arrow--up" />
      <ArrowDownOutlined v-else class="dashboard-card__arrow dashboard-card__arrow--down" />
      <span :class="trendValueClass">{{ trend.value.toFixed(1) }}%</span>
    </div>
    <div v-if="description" class="dashboard-card__desc">{{ description }}</div>
  </Card>
</template>

<style scoped>
.dashboard-card {
  border-radius: 8px;
  cursor: pointer;
}
.dashboard-card__header {
  display: flex;
  align-items: center;
  gap: 4px;
  margin-bottom: 8px;
}
.dashboard-card__title {
  font-size: 14px;
  color: #8C8C8C;
}
.dashboard-card__info-icon {
  font-size: 12px;
  color: #8C8C8C;
  cursor: help;
}
.dashboard-card__value {
  font-size: 24px;
  font-weight: 600;
  color: #000000D9;
  line-height: 1.4;
}
.dashboard-card__trend {
  display: flex;
  align-items: center;
  gap: 4px;
  margin-top: 8px;
  font-size: 12px;
}
.dashboard-card__arrow {
  font-size: 12px;
}
.dashboard-card__arrow--up {
  color: #52C41A;
}
.dashboard-card__arrow--down {
  color: #FF4D4F;
}
.dashboard-card__trend-value--up {
  color: #52C41A;
}
.dashboard-card__trend-value--down {
  color: #FF4D4F;
}
.dashboard-card__desc {
  margin-top: 4px;
  font-size: 12px;
  color: #8C8C8C;
}
</style>
