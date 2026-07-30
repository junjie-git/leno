<script setup lang="ts">
import { ref, reactive, computed, onMounted, h } from 'vue'
import {
  Breadcrumb,
  BreadcrumbItem,
  Card,
  Table,
  Button,
  Space,
  Modal,
  Form,
  FormItem,
  Input,
  Select,
  InputNumber,
  Switch,
  Tag,
  Drawer,
  Skeleton,
  message,
} from 'ant-design-vue'
import type { TableColumnsType } from 'ant-design-vue'
import { PlusOutlined, EditOutlined } from '@ant-design/icons-vue'
import { freightTemplateApi } from '../api/freight-template.api'
import type {
  FreightTemplateDto,
  CreateFreightTemplateDto,
  UpdateFreightRulesDto,
  RegionRuleDto,
  PricingType,
} from '../types/freight-template.dto'
import { TemplateRuleEditor, EmptyState, IdempotencyButton } from '@/shared/components'
import { logger } from '@/shared/utils/logger'
import { ConcurrencyError } from '@/shared/http'
import { formatMoney, formatDateTime } from '@/shared/utils/format'

/**
 * 运费模板页
 *
 * 路由 /logistics/freight-templates，权限 freight-template:list
 * - 模板列表表格（名称 / 计费类型 Tag / 满额包邮 / 状态 Switch / 操作）
 * - 新建模板按钮 → 弹窗（名称 + 计费类型 + 固定运费 + 满额包邮）
 * - 编辑规则按钮 → 抽屉（TemplateRuleEditor 地区规则编辑器，Fixed 类型隐藏）
 * - 启停 Switch（调用 enable/disable）
 */

const loading = ref(false)
const submitting = ref(false)
const templates = ref<FreightTemplateDto[]>([])

// 新建弹窗
const createModalOpen = ref(false)
const createForm = reactive({
  name: '',
  pricingType: 'Fixed' as PricingType,
  fixedFee: 0,
  freeShippingThreshold: undefined as number | undefined,
})

// 编辑规则抽屉
const editDrawerOpen = ref(false)
const editingTemplate = ref<FreightTemplateDto | null>(null)
const editingRules = ref<RegionRuleDto[]>([])
const editingVersion = ref(0)

const pricingTypeOptions: Array<{ label: string; value: PricingType }> = [
  { label: '固定运费', value: 'Fixed' },
  { label: '按重量计费', value: 'ByWeight' },
  { label: '按件数计费', value: 'ByPiece' },
]

const pricingTypeLabels: Record<PricingType, string> = {
  Fixed: '固定运费',
  ByWeight: '按重量',
  ByPiece: '按件数',
}

const columns: TableColumnsType = [
  { title: '模板名称', dataIndex: 'name', key: 'name', width: 200, ellipsis: true },
  { title: '计费类型', dataIndex: 'pricingType', key: 'pricingType', width: 120 },
  { title: '固定运费', key: 'fixedFee', width: 120, align: 'right' },
  { title: '满额包邮', key: 'freeShippingThreshold', width: 120, align: 'right' },
  { title: '状态', dataIndex: 'isEnabled', key: 'isEnabled', width: 100 },
  { title: '更新时间', dataIndex: 'updatedAt', key: 'updatedAt', width: 180 },
  { title: '操作', key: 'action', width: 160, fixed: 'right' },
]

const showFixedFee = computed(() => createForm.pricingType === 'Fixed')
const showRuleEditor = computed(
  () => editingTemplate.value?.pricingType !== 'Fixed',
)

async function loadList(): Promise<void> {
  loading.value = true
  try {
    templates.value = await freightTemplateApi.listMine()
  } catch (e) {
    logger.error('加载运费模板列表失败', e)
    message.error('加载运费模板列表失败')
  } finally {
    loading.value = false
  }
}

function openCreateModal(): void {
  createForm.name = ''
  createForm.pricingType = 'Fixed'
  createForm.fixedFee = 0
  createForm.freeShippingThreshold = undefined
  createModalOpen.value = true
}

async function onCreate(): Promise<void> {
  if (!createForm.name.trim()) {
    message.warning('请输入模板名称')
    return
  }
  submitting.value = true
  try {
    const body: CreateFreightTemplateDto = {
      name: createForm.name.trim(),
      pricingType: createForm.pricingType,
    }
    if (showFixedFee.value) body.fixedFee = createForm.fixedFee
    if (createForm.freeShippingThreshold !== undefined) {
      body.freeShippingThreshold = createForm.freeShippingThreshold
    }
    const created = await freightTemplateApi.create(body)
    templates.value = [...templates.value, created]
    message.success('创建运费模板成功')
    createModalOpen.value = false
  } catch (e) {
    logger.error('创建运费模板失败', e)
    message.error('创建运费模板失败')
  } finally {
    submitting.value = false
  }
}

function openEditDrawer(record: FreightTemplateDto): void {
  editingTemplate.value = record
  editingRules.value = record.regionRules.map((r) => ({ ...r }))
  editingVersion.value = record.version
  editDrawerOpen.value = true
}

function handleConcurrencyError(): void {
  Modal.confirm({
    title: '资源已被他人修改',
    content: '该运费模板规则已被他人修改，是否刷新后重试？',
    okText: '刷新后重试',
    cancelText: '取消',
    onOk: () => {
      return loadList().then(() => {
        if (editingTemplate.value) {
          const fresh = templates.value.find((t) => t.id === editingTemplate.value!.id)
          if (fresh) {
            editingTemplate.value = fresh
            editingRules.value = fresh.regionRules.map((r) => ({ ...r }))
            editingVersion.value = fresh.version
          }
        }
      })
    },
  })
}

async function onSaveRules(): Promise<void> {
  if (!editingTemplate.value) return
  if (showRuleEditor.value) {
    const invalid = editingRules.value.some(
      (r) => !r.regionCode || !r.regionName || r.firstPrice < 0 || r.nextPrice < 0,
    )
    if (invalid) {
      message.warning('存在未填写或价格小于 0 的规则行')
      return
    }
  }
  submitting.value = true
  try {
    const body: UpdateFreightRulesDto = {
      regionRules: editingRules.value,
      version: editingVersion.value,
    }
    const updated = await freightTemplateApi.updateRules(
      editingTemplate.value.id,
      body,
    )
    templates.value = templates.value.map((t) =>
      t.id === updated.id ? updated : t,
    )
    editingTemplate.value = updated
    editingVersion.value = updated.version
    message.success('规则保存成功')
    editDrawerOpen.value = false
  } catch (e) {
    logger.error('保存运费模板规则失败', e)
    if (e instanceof ConcurrencyError) {
      handleConcurrencyError()
    } else {
      message.error('保存失败，请稍后重试')
    }
  } finally {
    submitting.value = false
  }
}

async function onToggleEnabled(record: FreightTemplateDto, checked: boolean): Promise<void> {
  try {
    if (checked) {
      await freightTemplateApi.enable(record.id)
      message.success('模板已启用')
    } else {
      await freightTemplateApi.disable(record.id)
      message.success('模板已停用')
    }
    templates.value = templates.value.map((t) =>
      t.id === record.id ? { ...t, isEnabled: checked } : t,
    )
  } catch (e) {
    logger.error('切换模板状态失败', e)
    message.error('操作失败，请稍后重试')
  }
}

onMounted(() => {
  void loadList()
})
</script>

<template>
  <div class="freight-templates-page">
    <Breadcrumb class="freight-templates-breadcrumb">
      <BreadcrumbItem>物流管理</BreadcrumbItem>
      <BreadcrumbItem>运费模板</BreadcrumbItem>
    </Breadcrumb>

    <Card class="freight-templates-card" :bordered="true">
      <template #title>
        <span class="freight-templates-title">运费模板</span>
      </template>
      <template #extra>
        <Button type="primary" :icon="h(PlusOutlined)" @click="openCreateModal">
          新建模板
        </Button>
      </template>

      <Skeleton v-if="loading" active :paragraph="{ rows: 5 }" />
      <EmptyState
        v-else-if="templates.length === 0"
        description="暂无运费模板，请点击右上角「新建模板」"
      />
      <Table
        v-else
        :columns="columns"
        :data-source="templates"
        row-key="id"
        :pagination="false"
        size="middle"
      >
        <template #bodyCell="{ column, record }">
          <template v-if="column.key === 'pricingType'">
            <Tag :color="record.pricingType === 'Fixed' ? 'blue' : 'cyan'">
              {{ pricingTypeLabels[record.pricingType as PricingType] }}
            </Tag>
          </template>
          <template v-else-if="column.key === 'fixedFee'">
            {{ record.fixedFee != null ? formatMoney(record.fixedFee) : '—' }}
          </template>
          <template v-else-if="column.key === 'freeShippingThreshold'">
            {{ record.freeShippingThreshold != null ? formatMoney(record.freeShippingThreshold) : '—' }}
          </template>
          <template v-else-if="column.key === 'isEnabled'">
            <Switch
              :checked="record.isEnabled"
              checked-children="启用"
              un-checked-children="停用"
              @change="(checked: boolean) => onToggleEnabled(record, checked)"
            />
          </template>
          <template v-else-if="column.key === 'updatedAt'">
            {{ formatDateTime(record.updatedAt) }}
          </template>
          <template v-else-if="column.key === 'action'">
            <Button
              type="link"
              size="small"
              :icon="h(EditOutlined)"
              @click="openEditDrawer(record)"
            >
              编辑规则
            </Button>
          </template>
        </template>
      </Table>
    </Card>

    <!-- 新建模板弹窗 -->
    <Modal
      v-model:open="createModalOpen"
      title="新建运费模板"
      :confirm-loading="submitting"
      @ok="onCreate"
    >
      <Form layout="vertical">
        <FormItem label="模板名称" required>
          <Input
            v-model:value="createForm.name"
            placeholder="请输入模板名称"
            :maxlength="50"
            show-count
          />
        </FormItem>
        <FormItem label="计费类型" required>
          <Select
            v-model:value="createForm.pricingType"
            :options="pricingTypeOptions"
            placeholder="请选择计费类型"
          />
        </FormItem>
        <FormItem v-if="showFixedFee" label="固定运费">
          <InputNumber
            v-model:value="createForm.fixedFee"
            :min="0"
            :precision="2"
            prefix="¥"
            style="width: 100%"
            placeholder="请输入固定运费"
          />
        </FormItem>
        <FormItem label="满额包邮（选填）">
          <InputNumber
            v-model:value="createForm.freeShippingThreshold"
            :min="0"
            :precision="2"
            prefix="¥"
            style="width: 100%"
            placeholder="满此金额免运费"
          />
        </FormItem>
      </Form>
    </Modal>

    <!-- 编辑规则抽屉 -->
    <Drawer
      v-model:open="editDrawerOpen"
      title="编辑区域规则"
      :width="960"
      :destroy-on-close="true"
    >
      <template v-if="editingTemplate">
        <div class="freight-templates-edit-header">
          <span class="freight-templates-edit-name">
            {{ editingTemplate.name }}
          </span>
          <Tag :color="editingTemplate.pricingType === 'Fixed' ? 'blue' : 'cyan'">
            {{ pricingTypeLabels[editingTemplate.pricingType] }}
          </Tag>
        </div>

        <div v-if="!showRuleEditor" class="freight-templates-fixed-hint">
          固定运费模式无需配置地区规则。
        </div>

        <TemplateRuleEditor
          v-else
          v-model="editingRules"
          :pricing-type="editingTemplate.pricingType"
        />
      </template>

      <template #footer>
        <Space>
          <Button @click="editDrawerOpen = false">取消</Button>
          <IdempotencyButton
            v-if="showRuleEditor"
            :loading="submitting"
            @click="onSaveRules"
          >
            保存规则
          </IdempotencyButton>
        </Space>
      </template>
    </Drawer>
  </div>
</template>

<style scoped>
.freight-templates-page {
  display: flex;
  flex-direction: column;
  gap: 16px;
}
.freight-templates-breadcrumb {
  font-size: 14px;
}
.freight-templates-card {
  border-radius: 8px;
}
.freight-templates-title {
  font-size: 15px;
  font-weight: 500;
}
.freight-templates-edit-header {
  display: flex;
  align-items: center;
  gap: 12px;
  margin-bottom: 16px;
}
.freight-templates-edit-name {
  font-size: 16px;
  font-weight: 500;
  color: #000000d9;
}
.freight-templates-fixed-hint {
  padding: 24px;
  background: #fafafa;
  border-radius: 6px;
  color: #8c8c8c;
  font-size: 14px;
  text-align: center;
}
</style>
