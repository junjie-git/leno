<script setup lang="ts">
import { h, computed } from 'vue'
import { Table, Input, InputNumber, Button } from 'ant-design-vue'
import type { TableColumnsType } from 'ant-design-vue'
import { PlusOutlined, DeleteOutlined } from '@ant-design/icons-vue'
import type { RegionRuleDto, PricingType } from '@/modules/04-logistics/types/freight-template.dto'

/**
 * 运费模板地区规则表格编辑器
 *
 * 通过 v-model:modelValue 双向绑定 RegionRuleDto[]。
 * 根据 pricingType 动态切换列标题（首重 kg / 首件数 个）。
 * 支持添加行 / 删除行 / 行内编辑。
 */

const props = withDefaults(
  defineProps<{
    /** 规则数组 */
    modelValue: RegionRuleDto[]
    /** 计费类型，影响列标题 */
    pricingType: PricingType
    /** 禁用编辑 */
    disabled?: boolean
  }>(),
  {
    disabled: false,
  },
)

const emit = defineEmits<{
  (e: 'update:modelValue', value: RegionRuleDto[]): void
}>()

const isByWeight = computed(() => props.pricingType === 'ByWeight')
const isByPiece = computed(() => props.pricingType === 'ByPiece')

const firstUnitTitle = computed(() => (isByWeight.value ? '首重' : isByPiece.value ? '首件数' : '首单位'))
const nextUnitTitle = computed(() => (isByWeight.value ? '续重' : isByPiece.value ? '续件数' : '续单位'))
const unitSuffix = computed(() => (isByWeight.value ? 'kg' : isByPiece.value ? '个' : ''))

const columns = computed<TableColumnsType>(() => [
  { title: '地区编码', dataIndex: 'regionCode', key: 'regionCode', width: 120 },
  { title: '地区名称', dataIndex: 'regionName', key: 'regionName', width: 140 },
  { title: firstUnitTitle.value, dataIndex: 'firstUnit', key: 'firstUnit', width: 130 },
  { title: '首价', dataIndex: 'firstPrice', key: 'firstPrice', width: 130 },
  { title: nextUnitTitle.value, dataIndex: 'nextUnit', key: 'nextUnit', width: 130 },
  { title: '续价', dataIndex: 'nextPrice', key: 'nextPrice', width: 130 },
  { title: '操作', key: 'action', width: 90, fixed: 'right' },
])

function emitChange(rows: RegionRuleDto[]): void {
  emit('update:modelValue', rows.map((r) => ({ ...r })))
}

function addRow(): void {
  const newRow: RegionRuleDto = {
    id: `r-${Date.now()}`,
    regionCode: '',
    regionName: '',
    firstUnit: 1,
    firstPrice: 0,
    nextUnit: 1,
    nextPrice: 0,
  }
  emitChange([...props.modelValue, newRow])
}

function deleteRow(index: number): void {
  const rows = [...props.modelValue]
  rows.splice(index, 1)
  emitChange(rows)
}

function updateField(index: number, field: keyof RegionRuleDto, value: string | number): void {
  const rows = [...props.modelValue]
  rows[index] = { ...rows[index], [field]: value }
  emitChange(rows)
}
</script>

<template>
  <div class="template-rule-editor">
    <div class="template-rule-editor-toolbar">
      <span class="template-rule-editor-hint">
        单位后缀：{{ unitSuffix || '—' }}
      </span>
      <Button
        type="dashed"
        size="small"
        :icon="h(PlusOutlined)"
        :disabled="disabled"
        @click="addRow"
      >
        添加行
      </Button>
    </div>
    <Table
      :columns="columns"
      :data-source="modelValue"
      row-key="id"
      :pagination="false"
      size="small"
      :scroll="{ x: 870 }"
    >
      <template #bodyCell="{ column, record, index }">
        <template v-if="column.key === 'regionCode'">
          <Input
            :value="record.regionCode"
            placeholder="如 CN"
            :disabled="disabled"
            size="small"
            style="width: 100px"
            @update:value="(v: string) => updateField(index, 'regionCode', v)"
          />
        </template>
        <template v-else-if="column.key === 'regionName'">
          <Input
            :value="record.regionName"
            placeholder="如 全国"
            :disabled="disabled"
            size="small"
            style="width: 120px"
            @update:value="(v: string) => updateField(index, 'regionName', v)"
          />
        </template>
        <template v-else-if="column.key === 'firstUnit'">
          <InputNumber
            :value="record.firstUnit"
            :min="0"
            :disabled="disabled"
            size="small"
            style="width: 100px"
            @update:value="(v: number) => updateField(index, 'firstUnit', v)"
          />
        </template>
        <template v-else-if="column.key === 'firstPrice'">
          <InputNumber
            :value="record.firstPrice"
            :min="0"
            :precision="2"
            :disabled="disabled"
            size="small"
            style="width: 100px"
            prefix="¥"
            @update:value="(v: number) => updateField(index, 'firstPrice', v)"
          />
        </template>
        <template v-else-if="column.key === 'nextUnit'">
          <InputNumber
            :value="record.nextUnit"
            :min="0"
            :disabled="disabled"
            size="small"
            style="width: 100px"
            @update:value="(v: number) => updateField(index, 'nextUnit', v)"
          />
        </template>
        <template v-else-if="column.key === 'nextPrice'">
          <InputNumber
            :value="record.nextPrice"
            :min="0"
            :precision="2"
            :disabled="disabled"
            size="small"
            style="width: 100px"
            prefix="¥"
            @update:value="(v: number) => updateField(index, 'nextPrice', v)"
          />
        </template>
        <template v-else-if="column.key === 'action'">
          <Button
            type="link"
            danger
            size="small"
            :icon="h(DeleteOutlined)"
            :disabled="disabled"
            @click="deleteRow(index)"
          >
            删除
          </Button>
        </template>
      </template>
    </Table>
  </div>
</template>

<style scoped>
.template-rule-editor {
  display: flex;
  flex-direction: column;
  gap: 12px;
}
.template-rule-editor-toolbar {
  display: flex;
  align-items: center;
  justify-content: space-between;
}
.template-rule-editor-hint {
  font-size: 12px;
  color: #8c8c8c;
}
</style>
