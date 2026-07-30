# 卖家管理后台 P1 批次 2（物流管理 + 评价回复）实施计划

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 完成 P1 批次 2 的 3 个页面（04-logistics 2 页 + 07-review 1 页）、TemplateRuleEditor 通用组件、freight/logistics/review 三个 mock handler 与 04-logistics + 07-review 路由注册，全量验证通过后提交推送。

**Architecture:** 延续 P0 五段式模块结构（`api/ + types/ + views/ + routes.ts + index.ts`）。新增 `04-logistics` 与 `07-review` 两个模块。shared 层新增 `TemplateRuleEditor.vue`（Table 可编辑行组件）并扩展 `StatusTag` 的 review 状态映射。API 客户端统一使用批次 1 引入的 `http` 别名（`import { http, withIdempotency } from '@/shared/http'`），响应解包 `.then(r => r.data)`，乐观锁冲突通过 `ConcurrencyError` + `Modal.confirm` 处理。Mock 层新增 3 个 handler 文件（freight/logistics/review）与种子数据扩展，评价使用新 BC 路径 `/api/seller/reviews`。

**Tech Stack:** Vue 3.5 + TypeScript 5.7 + Vite 6 + Ant Design Vue 4.2 + Pinia 2.3 + Vue Router 4.5 + axios 1.7 + Vitest 2.1 + axios-mock-adapter 2.1

---

## 关键设计决策（实施前必读）

1. **批次 1 前置依赖**：本批次使用批次 1 在 `shared/http/index.ts` 引入的 `http` 别名（`export { client as http } from './client'`）。执行本批次前须确认批次 1 已完成（`http` 别名、shop mock handler、onboarding 路由均已就绪）。Task 2 Step 1 包含前置校验。
2. **StatusTag review 映射扩展**：`ReviewReply.vue` 需 `StatusTag(type="review")`，现有 StatusTag 无 `review` 类型。Task 1 追加 `review` 映射（`Approved` → 已通过 success / `Hidden` → 已隐藏 default）。
3. **TemplateRuleEditor 是 Table 可编辑行组件**：通过 `v-model:modelValue` 双向绑定 `RegionRuleDto[]`，根据 `pricingType` 动态切换列标题（首重 kg / 首件数 个）。Fixed 类型时父组件隐藏该编辑器。
4. **评价使用新 BC 路径**：`/api/seller/reviews`（list/get/reply），不使用旧 BC 路径 `/api/reviews/{id}/reply`。回复为覆盖式编辑（1-500 字）。
5. **LogisticsCompanies.vue 10 分钟前端缓存**：首次加载存 `localStorage` 键 `logistics_companies_cache`（含 `data` + `fetchedAt` 时间戳），10 分钟内直接读缓存，超时或不存在才请求 API。复制编码使用 `navigator.clipboard.writeText`。
6. **freight-template.api.ts 5 端点**：listMine / create / updateRules / enable / disable。create 与 enable/disable 注入 `withIdempotency()`。updateRules 携带 `version` 乐观锁。
7. **review.api.ts 3 端点**：list / get / reply。reply 注入 `withIdempotency()`。
8. **logistics-company.api.ts 1 端点**：listEnabled（卖家只读，无写操作）。
9. **Mock seed 扩展**：在批次 1 已扩展的 `MockSeed`（含 shop/qualifications）基础上追加 `freightTemplates` / `logisticsCompanies` / `reviews` 三个字段。mock/index.ts 注册 3 个新 handler，handler 数量从批次 1 的 6 个增至 9 个。
10. **响应解包**：所有 API 函数内部 `.then(r => r.data)` 解包（响应拦截器已 unwrap `ApiResponse.data`）。
11. **验证命令工作目录**：除特别说明外，所有 `pnpm` 命令在 `/workspace/web/seller` 下执行。

---

## File Structure

### 新建文件
| 文件 | 职责 |
|------|------|
| `web/seller/src/shared/components/TemplateRuleEditor.vue` | 运费模板地区规则表格编辑器（Table 可编辑行） |
| `web/seller/src/shared/components/TemplateRuleEditor.spec.ts` | TemplateRuleEditor 组件测试 |
| `web/seller/src/modules/04-logistics/types/freight-template.dto.ts` | 运费模板 DTO |
| `web/seller/src/modules/04-logistics/types/logistics-company.dto.ts` | 物流公司 DTO |
| `web/seller/src/modules/04-logistics/api/freight-template.api.ts` | 运费模板 API 客户端（5 端点） |
| `web/seller/src/modules/04-logistics/api/freight-template.api.spec.ts` | 运费模板 API 测试 |
| `web/seller/src/modules/04-logistics/api/logistics-company.api.ts` | 物流公司 API 客户端（1 端点） |
| `web/seller/src/modules/04-logistics/api/logistics-company.api.spec.ts` | 物流公司 API 测试 |
| `web/seller/src/modules/04-logistics/views/FreightTemplates.vue` | 运费模板列表+新建弹窗+编辑规则抽屉+启停Switch |
| `web/seller/src/modules/04-logistics/views/LogisticsCompanies.vue` | 物流公司只读表格+10分钟缓存+复制编码 |
| `web/seller/src/modules/04-logistics/routes.ts` | 模块路由 |
| `web/seller/src/modules/04-logistics/index.ts` | 模块出口 |
| `web/seller/src/modules/07-review/types/review.dto.ts` | 评价 DTO |
| `web/seller/src/modules/07-review/api/review.api.ts` | 评价 API 客户端（3 端点） |
| `web/seller/src/modules/07-review/api/review.api.spec.ts` | 评价 API 测试 |
| `web/seller/src/modules/07-review/views/ReviewReply.vue` | 评价卡片列表+回复抽屉 |
| `web/seller/src/modules/07-review/routes.ts` | 模块路由 |
| `web/seller/src/modules/07-review/index.ts` | 模块出口 |
| `web/seller/src/shared/http/mock/handlers/freight.ts` | 运费模板 mock handler |
| `web/seller/src/shared/http/mock/handlers/logistics.ts` | 物流公司 mock handler |
| `web/seller/src/shared/http/mock/handlers/review.ts` | 评价 mock handler |

### 修改文件
| 文件 | 改动 |
|------|------|
| `web/seller/src/shared/components/index.ts` | 导出 `TemplateRuleEditor` |
| `web/seller/src/shared/components/StatusTag.vue` | 追加 `review` 类型映射 |
| `web/seller/src/shared/components/StatusTag.spec.ts` | 追加 review 映射测试 |
| `web/seller/src/shared/http/mock/data/types.ts` | `MockSeed` 追加 `freightTemplates`/`logisticsCompanies`/`reviews` |
| `web/seller/src/shared/http/mock/data/seed.ts` | 追加运费模板/物流公司/评价种子与 builder |
| `web/seller/src/shared/http/mock/index.ts` | 注册 3 个新 handler，更新启动日志 |
| `web/seller/src/app/router.ts` | 注册 04-logistics + 07-review 路由 |

---

## Task 1: shared 组件 TemplateRuleEditor + StatusTag review 映射扩展

**Files:**
- Create: `web/seller/src/shared/components/TemplateRuleEditor.vue`
- Create: `web/seller/src/shared/components/TemplateRuleEditor.spec.ts`
- Modify: `web/seller/src/shared/components/index.ts`
- Modify: `web/seller/src/shared/components/StatusTag.vue`
- Modify: `web/seller/src/shared/components/StatusTag.spec.ts`

- [ ] **Step 1: 先写 TemplateRuleEditor 失败测试**

创建 `web/seller/src/shared/components/TemplateRuleEditor.spec.ts`：

```typescript
import { describe, it, expect } from 'vitest'
import { mount } from '@vue/test-utils'
import { InputNumber, Input, Button } from 'ant-design-vue'
import TemplateRuleEditor from './TemplateRuleEditor.vue'
import type { RegionRuleDto, PricingType } from '@/modules/04-logistics/types/freight-template.dto'

/**
 * TemplateRuleEditor 组件测试
 *
 * 验证：
 * - 渲染表格列随 pricingType 动态变化（首重 kg / 首件数 个）
 * - 添加行 / 删除行 / 编辑值
 * - v-model 双向绑定
 */

function makeRule(overrides: Partial<RegionRuleDto> = {}): RegionRuleDto {
  return {
    id: 'r-001',
    regionCode: 'CN',
    regionName: '全国',
    firstUnit: 1,
    firstPrice: 8,
    nextUnit: 1,
    nextPrice: 2,
    ...overrides,
  }
}

describe('shared/components/TemplateRuleEditor', () => {
  it('渲染传入的规则行', () => {
    const rules = [makeRule()]
    const wrapper = mount(TemplateRuleEditor, {
      props: { modelValue: rules, pricingType: 'ByWeight' as PricingType },
    })
    expect(wrapper.html()).toContain('全国')
    expect(wrapper.html()).toContain('ant-table')
  })

  it('ByWeight 类型列标题显示首重/续重（kg）', () => {
    const wrapper = mount(TemplateRuleEditor, {
      props: { modelValue: [makeRule()], pricingType: 'ByWeight' as PricingType },
    })
    expect(wrapper.html()).toContain('首重')
    expect(wrapper.html()).toContain('续重')
  })

  it('ByPiece 类型列标题显示首件/续件（个）', () => {
    const wrapper = mount(TemplateRuleEditor, {
      props: { modelValue: [makeRule()], pricingType: 'ByPiece' as PricingType },
    })
    expect(wrapper.html()).toContain('首件数')
    expect(wrapper.html()).toContain('续件数')
  })

  it('点击添加行按钮 emit update:modelValue 含新行', async () => {
    const wrapper = mount(TemplateRuleEditor, {
      props: { modelValue: [makeRule()], pricingType: 'ByWeight' as PricingType },
    })
    const addBtn = wrapper.findAll('button').find((b) => b.text().includes('添加'))
    expect(addBtn).toBeTruthy()
    await addBtn!.trigger('click')
    const emitted = wrapper.emitted('update:modelValue')
    expect(emitted).toBeTruthy()
    const newValue = emitted![0][0] as RegionRuleDto[]
    expect(newValue).toHaveLength(2)
    expect(newValue[1].regionName).toBe('')
  })

  it('点击删除行按钮 emit update:modelValue 移除对应行', async () => {
    const rules = [makeRule({ id: 'r-001', regionName: '全国' }), makeRule({ id: 'r-002', regionName: '江浙沪' })]
    const wrapper = mount(TemplateRuleEditor, {
      props: { modelValue: rules, pricingType: 'ByWeight' as PricingType },
    })
    const deleteBtns = wrapper.findAll('button').filter((b) => b.text().includes('删除'))
    expect(deleteBtns).toHaveLength(2)
    await deleteBtns[0].trigger('click')
    const emitted = wrapper.emitted('update:modelValue')
    expect(emitted).toBeTruthy()
    const newValue = emitted![0][0] as RegionRuleDto[]
    expect(newValue).toHaveLength(1)
    expect(newValue[0].regionName).toBe('江浙沪')
  })

  it('编辑地区名称 emit update:modelValue', async () => {
    const wrapper = mount(TemplateRuleEditor, {
      props: { modelValue: [makeRule()], pricingType: 'ByWeight' as PricingType },
    })
    const inputs = wrapper.findAllComponents(Input)
    const regionNameInput = inputs.find((i) => i.props('value') === '全国')
    expect(regionNameInput).toBeTruthy()
    await regionNameInput!.setValue('江浙沪')
    const emitted = wrapper.emitted('update:modelValue')
    expect(emitted).toBeTruthy()
    const newValue = emitted![0][0] as RegionRuleDto[]
    expect(newValue[0].regionName).toBe('江浙沪')
  })

  it('编辑首价 InputNumber emit update:modelValue', async () => {
    const wrapper = mount(TemplateRuleEditor, {
      props: { modelValue: [makeRule()], pricingType: 'ByWeight' as PricingType },
    })
    const inputNumbers = wrapper.findAllComponents(InputNumber)
    // 列顺序：地区编码 / 地区名称 / 首单位 / 首价 / 续单位 / 续价
    // 首价是第 4 个 InputNumber（index 3）
    const firstPriceInput = inputNumbers[3]
    expect(firstPriceInput).toBeTruthy()
    await firstPriceInput.vm.$emit('update:value', 12)
    const emitted = wrapper.emitted('update:modelValue')
    expect(emitted).toBeTruthy()
    const newValue = emitted![0][0] as RegionRuleDto[]
    expect(newValue[0].firstPrice).toBe(12)
  })

  it('disabled 时添加/删除按钮禁用', () => {
    const wrapper = mount(TemplateRuleEditor, {
      props: { modelValue: [makeRule()], pricingType: 'ByWeight' as PricingType, disabled: true },
    })
    const addBtn = wrapper.findAllComponents(Button).find((b) => b.text().includes('添加'))
    expect(addBtn?.props('disabled')).toBe(true)
  })
})
```

- [ ] **Step 2: 运行测试确认失败**

Run (cwd: `web/seller`): `pnpm test -- src/shared/components/TemplateRuleEditor.spec.ts`
Expected: FAIL（`Cannot find module './TemplateRuleEditor.vue'`）

- [ ] **Step 3: 实现 TemplateRuleEditor.vue**

创建 `web/seller/src/shared/components/TemplateRuleEditor.vue`：

```vue
<script setup lang="ts">
import { computed } from 'vue'
import { Table, Input, InputNumber, Button, Space } from 'ant-design-vue'
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

<script lang="ts">
import { h } from 'vue'
</script>

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
```

> **注**：上方独立的 `<script lang="ts">` 块用于导入 `h`（模板中 `:icon="h(PlusOutlined)"` 需要）。
> 若 eslint 报"混合 script 块"，可将 `<script setup>` 的第一行改为 `import { h, computed } from 'vue'` 并删除独立 script 块。推荐采用后者。

- [ ] **Step 4: 简化 script 块（推荐）**

将 `<script setup lang="ts">` 的第一行改为同时导入 `h`，并删除文件末尾独立的 `<script lang="ts">` 块：

将：
```typescript
import { computed } from 'vue'
```
改为：
```typescript
import { h, computed } from 'vue'
```

并删除文件末尾的：
```vue
<script lang="ts">
import { h } from 'vue'
</script>
```

- [ ] **Step 5: 运行测试确认通过**

Run (cwd: `web/seller`): `pnpm test -- src/shared/components/TemplateRuleEditor.spec.ts`
Expected: PASS（7 tests passed）

- [ ] **Step 6: 导出 TemplateRuleEditor**

修改 `web/seller/src/shared/components/index.ts`，在末尾追加一行：

```typescript
export { default as TemplateRuleEditor } from './TemplateRuleEditor.vue'
```

- [ ] **Step 7: 扩展 StatusTag review 映射 — 先写失败测试**

修改 `web/seller/src/shared/components/StatusTag.spec.ts`，在 `freightTemplate` 测试块之后（"未知状态" 用例之前）追加：

```typescript
  // ===== review 评价状态映射 =====
  it('review 类型 + Approved 状态渲染 success tag（已通过）', () => {
    const wrapper = mount(StatusTag, { props: { type: 'review', status: 'Approved' } })
    expect(wrapper.html()).toContain('已通过')
    expect(wrapper.html()).toContain('ant-tag-success')
  })

  it('review 类型 + Hidden 状态渲染 default tag（已隐藏）', () => {
    const wrapper = mount(StatusTag, { props: { type: 'review', status: 'Hidden' } })
    expect(wrapper.html()).toContain('已隐藏')
  })
```

- [ ] **Step 8: 运行测试确认失败**

Run (cwd: `web/seller`): `pnpm test -- src/shared/components/StatusTag.spec.ts`
Expected: FAIL（`review` 类型未定义，渲染原始字符串 `Approved`）

- [ ] **Step 9: 扩展 StatusTag.vue review 映射**

修改 `web/seller/src/shared/components/StatusTag.vue`：

9a. 在 `StatusTagType` 联合类型中追加 `'review'`：

```typescript
type StatusTagType =
  | 'deadLetter'
  | 'orderPayment'
  | 'shop'
  | 'user'
  | 'oauth'
  | 'operator'
  | 'loginResult'
  | 'cacheType'
  | 'menuType'
  | 'onlineUser'
  | 'product'
  | 'order'
  | 'aftersales'
  | 'freightTemplate'
  | 'review'
```

9b. 在 `STATUS_MAP` 中 `freightTemplate` 块之后追加 `review` 块：

```typescript
  review: {
    Approved: { label: '已通过', color: 'success' },
    Hidden: { label: '已隐藏', color: 'default' },
  },
```

- [ ] **Step 10: 运行测试确认通过**

Run (cwd: `web/seller`): `pnpm test -- src/shared/components/StatusTag.spec.ts`
Expected: PASS（全部用例通过，含新增 2 个 review 用例）

- [ ] **Step 11: 类型检查 + lint**

Run (cwd: `web/seller`): `pnpm typecheck`
Expected: 0 errors

Run (cwd: `web/seller`): `pnpm lint`
Expected: 0 errors / 0 warnings

- [ ] **Step 12: 提交**

```bash
git add web/seller/src/shared/components/TemplateRuleEditor.vue web/seller/src/shared/components/TemplateRuleEditor.spec.ts web/seller/src/shared/components/index.ts web/seller/src/shared/components/StatusTag.vue web/seller/src/shared/components/StatusTag.spec.ts
git commit -m "feat(seller): add TemplateRuleEditor component and extend StatusTag review mapping"
```

---

## Task 2: 04-logistics 类型与 API 客户端

**Files:**
- Verify: `web/seller/src/shared/http/index.ts`（前置依赖：`http` 别名须存在）
- Create: `web/seller/src/modules/04-logistics/types/freight-template.dto.ts`
- Create: `web/seller/src/modules/04-logistics/types/logistics-company.dto.ts`
- Create: `web/seller/src/modules/04-logistics/api/freight-template.api.spec.ts`
- Create: `web/seller/src/modules/04-logistics/api/freight-template.api.ts`
- Create: `web/seller/src/modules/04-logistics/api/logistics-company.api.spec.ts`
- Create: `web/seller/src/modules/04-logistics/api/logistics-company.api.ts`

- [ ] **Step 1: 前置依赖校验 — http 别名须存在**

Run (cwd: `web/seller`): `pnpm typecheck 2>&1 | head -20`

确认 `shared/http/index.ts` 中包含 `export { client as http } from './client'`（批次 1 引入）。

若该行缺失（批次 1 未执行），请先执行批次 1 计划。若批次 1 已执行但该行缺失，则在 `web/seller/src/shared/http/index.ts` 的 `export { client, withIdempotency } from './client'` 之后追加：

```typescript
export { client as http } from './client'
```

Expected: `http` 别名可从 `@/shared/http` 导入，typecheck 0 errors

- [ ] **Step 2: 创建 freight-template.dto.ts**

创建 `web/seller/src/modules/04-logistics/types/freight-template.dto.ts`：

```typescript
/**
 * 04-logistics 运费模板 DTO
 *
 * 与后端 FreightTemplateController 对接：
 * - GET    /api/seller/freight-templates/mine        查询当前卖家运费模板列表
 * - POST   /api/seller/freight-templates             创建运费模板（幂等）
 * - PUT    /api/seller/freight-templates/{id}/rules  更新区域规则（乐观锁 version）
 * - POST   /api/seller/freight-templates/{id}/enable 启用模板（幂等）
 * - POST   /api/seller/freight-templates/{id}/disable 停用模板（幂等）
 */

/** 计费类型 */
export type PricingType = 'ByWeight' | 'ByPiece' | 'Fixed'

/** 区域规则 */
export interface RegionRuleDto {
  id: string
  regionCode: string
  regionName: string
  firstUnit: number
  firstPrice: number
  nextUnit: number
  nextPrice: number
}

/** 运费模板 */
export interface FreightTemplateDto {
  id: string
  name: string
  pricingType: PricingType
  fixedFee?: number
  freeShippingThreshold?: number
  regionRules: RegionRuleDto[]
  isEnabled: boolean
  version: number
  createdAt: string
  updatedAt: string
}

/** 创建运费模板 */
export interface CreateFreightTemplateDto {
  name: string
  pricingType: PricingType
  fixedFee?: number
  freeShippingThreshold?: number
}

/** 更新区域规则 */
export interface UpdateFreightRulesDto {
  regionRules: RegionRuleDto[]
  version: number
}
```

- [ ] **Step 3: 创建 logistics-company.dto.ts**

创建 `web/seller/src/modules/04-logistics/types/logistics-company.dto.ts`：

```typescript
/**
 * 04-logistics 物流公司 DTO
 *
 * 与后端 LogisticsCompanyController 对接：
 * - GET /api/seller/logistics-companies  查询启用态物流公司（卖家只读）
 */

/** 物流公司（卖家只读视图） */
export interface LogisticsCompanyDto {
  id: string
  name: string
  code: string
  servicePhone?: string
  website?: string
  supportsTracking: boolean
  sortOrder: number
}
```

- [ ] **Step 4: 先写 freight-template.api 失败测试**

创建 `web/seller/src/modules/04-logistics/api/freight-template.api.spec.ts`：

```typescript
import { describe, expect, it, vi, beforeEach } from 'vitest'
import type { AxiosResponse } from 'axios'
import { freightTemplateApi } from './freight-template.api'
import { http, withIdempotency } from '@/shared/http'

/**
 * freightTemplateApi 单元测试
 *
 * client 响应拦截器已 unwrap ApiResponse.data，故 mock http 方法返回
 * AxiosResponse 形态（{ data: 业务对象 }），api 函数内部 .then(r => r.data) 解包。
 */
vi.mock('@/shared/http', () => ({
  http: {
    get: vi.fn(),
    post: vi.fn(),
    put: vi.fn(),
  },
  withIdempotency: vi.fn(() => ({ headers: { 'Idempotency-Key': 'mock-key' } })),
}))

function mockResponse<T>(data: T): AxiosResponse<T> {
  return { data } as AxiosResponse<T>
}

describe('freightTemplateApi', () => {
  beforeEach(() => {
    vi.clearAllMocks()
    vi.mocked(withIdempotency).mockReturnValue({
      headers: { 'Idempotency-Key': 'mock-key' },
    })
  })

  describe('listMine', () => {
    it('调用 GET /seller/freight-templates/mine', async () => {
      vi.mocked(http.get).mockResolvedValue(mockResponse([]))
      await freightTemplateApi.listMine()
      expect(http.get).toHaveBeenCalledWith('/seller/freight-templates/mine')
    })

    it('返回解包后的运费模板数组', async () => {
      const templates = [
        {
          id: 'ft-001',
          name: '全国统一运费',
          pricingType: 'Fixed',
          fixedFee: 10,
          regionRules: [],
          isEnabled: true,
          version: 1,
          createdAt: '2026-02-01T00:00:00Z',
          updatedAt: '2026-02-01T00:00:00Z',
        },
      ]
      vi.mocked(http.get).mockResolvedValue(mockResponse(templates))
      const result = await freightTemplateApi.listMine()
      expect(result).toEqual(templates)
    })
  })

  describe('create', () => {
    it('调用 POST /seller/freight-templates 带 Idempotency-Key', async () => {
      vi.mocked(http.post).mockResolvedValue(
        mockResponse({ id: 'ft-003', name: '新模板', pricingType: 'Fixed', regionRules: [], isEnabled: true, version: 1, createdAt: '', updatedAt: '' }),
      )
      const body = { name: '新模板', pricingType: 'Fixed' as const, fixedFee: 15 }
      await freightTemplateApi.create(body)

      expect(http.post).toHaveBeenCalledWith('/seller/freight-templates', body, {
        headers: { 'Idempotency-Key': 'mock-key' },
      })
      expect(withIdempotency).toHaveBeenCalled()
    })
  })

  describe('updateRules', () => {
    it('调用 PUT /seller/freight-templates/{id}/rules 带 version 乐观锁', async () => {
      vi.mocked(http.put).mockResolvedValue(
        mockResponse({ id: 'ft-001', name: '模板', pricingType: 'ByWeight', regionRules: [], isEnabled: true, version: 2, createdAt: '', updatedAt: '' }),
      )
      const body = {
        regionRules: [
          { id: 'r-001', regionCode: 'CN', regionName: '全国', firstUnit: 1, firstPrice: 8, nextUnit: 1, nextPrice: 2 },
        ],
        version: 1,
      }
      await freightTemplateApi.updateRules('ft-001', body)

      expect(http.put).toHaveBeenCalledWith('/seller/freight-templates/ft-001/rules', body)
    })
  })

  describe('enable', () => {
    it('调用 POST /seller/freight-templates/{id}/enable 带 Idempotency-Key', async () => {
      vi.mocked(http.post).mockResolvedValue(mockResponse(undefined))
      await freightTemplateApi.enable('ft-001')

      expect(http.post).toHaveBeenCalledWith(
        '/seller/freight-templates/ft-001/enable',
        {},
        { headers: { 'Idempotency-Key': 'mock-key' } },
      )
      expect(withIdempotency).toHaveBeenCalled()
    })
  })

  describe('disable', () => {
    it('调用 POST /seller/freight-templates/{id}/disable 带 Idempotency-Key', async () => {
      vi.mocked(http.post).mockResolvedValue(mockResponse(undefined))
      await freightTemplateApi.disable('ft-001')

      expect(http.post).toHaveBeenCalledWith(
        '/seller/freight-templates/ft-001/disable',
        {},
        { headers: { 'Idempotency-Key': 'mock-key' } },
      )
      expect(withIdempotency).toHaveBeenCalled()
    })
  })
})
```

- [ ] **Step 5: 运行测试确认失败**

Run (cwd: `web/seller`): `pnpm test -- src/modules/04-logistics/api/freight-template.api.spec.ts`
Expected: FAIL（`Cannot find module './freight-template.api'`）

- [ ] **Step 6: 实现 freight-template.api.ts**

创建 `web/seller/src/modules/04-logistics/api/freight-template.api.ts`：

```typescript
import { http, withIdempotency } from '@/shared/http'
import type {
  FreightTemplateDto,
  CreateFreightTemplateDto,
  UpdateFreightRulesDto,
} from '../types/freight-template.dto'

/**
 * 运费模板 API 客户端
 *
 * 与后端 FreightTemplateController 对接（响应拦截器已解包 ApiResponse.data，
 * 调用方拿到的就是业务负载）：
 * - GET    /seller/freight-templates/mine        查询当前卖家运费模板列表
 * - POST   /seller/freight-templates            创建运费模板（幂等）
 * - PUT    /seller/freight-templates/{id}/rules 更新区域规则（version 乐观锁）
 * - POST   /seller/freight-templates/{id}/enable  启用模板（幂等）
 * - POST   /seller/freight-templates/{id}/disable 停用模板（幂等）
 */
export const freightTemplateApi = {
  /** 查询当前卖家运费模板列表 */
  listMine(): Promise<FreightTemplateDto[]> {
    return http
      .get<FreightTemplateDto[]>('/seller/freight-templates/mine')
      .then((r) => r.data)
  },

  /** 创建运费模板 */
  create(body: CreateFreightTemplateDto): Promise<FreightTemplateDto> {
    return http
      .post<FreightTemplateDto>('/seller/freight-templates', body, withIdempotency())
      .then((r) => r.data)
  },

  /** 更新区域规则（整体替换，带 version 乐观锁） */
  updateRules(id: string, body: UpdateFreightRulesDto): Promise<FreightTemplateDto> {
    return http
      .put<FreightTemplateDto>(`/seller/freight-templates/${id}/rules`, body)
      .then((r) => r.data)
  },

  /** 启用模板 */
  enable(id: string): Promise<void> {
    return http
      .post<void>(`/seller/freight-templates/${id}/enable`, {}, withIdempotency())
      .then((r) => r.data)
  },

  /** 停用模板 */
  disable(id: string): Promise<void> {
    return http
      .post<void>(`/seller/freight-templates/${id}/disable`, {}, withIdempotency())
      .then((r) => r.data)
  },
}
```

- [ ] **Step 7: 运行测试确认通过**

Run (cwd: `web/seller`): `pnpm test -- src/modules/04-logistics/api/freight-template.api.spec.ts`
Expected: PASS（6 tests passed）

- [ ] **Step 8: 先写 logistics-company.api 失败测试**

创建 `web/seller/src/modules/04-logistics/api/logistics-company.api.spec.ts`：

```typescript
import { describe, expect, it, vi, beforeEach } from 'vitest'
import type { AxiosResponse } from 'axios'
import { logisticsCompanyApi } from './logistics-company.api'
import { http } from '@/shared/http'

/**
 * logisticsCompanyApi 单元测试
 *
 * client 响应拦截器已 unwrap ApiResponse.data，故 mock http 方法返回
 * AxiosResponse 形态（{ data: 业务对象 }），api 函数内部 .then(r => r.data) 解包。
 */
vi.mock('@/shared/http', () => ({
  http: {
    get: vi.fn(),
  },
}))

function mockResponse<T>(data: T): AxiosResponse<T> {
  return { data } as AxiosResponse<T>
}

describe('logisticsCompanyApi', () => {
  beforeEach(() => {
    vi.clearAllMocks()
  })

  describe('listEnabled', () => {
    it('调用 GET /seller/logistics-companies', async () => {
      vi.mocked(http.get).mockResolvedValue(mockResponse([]))
      await logisticsCompanyApi.listEnabled()
      expect(http.get).toHaveBeenCalledWith('/seller/logistics-companies')
    })

    it('返回解包后的物流公司数组', async () => {
      const companies = [
        {
          id: 'lc-001',
          name: '顺丰速运',
          code: 'SF',
          servicePhone: '95338',
          website: 'https://www.sf-express.com',
          supportsTracking: true,
          sortOrder: 1,
        },
      ]
      vi.mocked(http.get).mockResolvedValue(mockResponse(companies))
      const result = await logisticsCompanyApi.listEnabled()
      expect(result).toEqual(companies)
    })
  })
})
```

- [ ] **Step 9: 运行测试确认失败**

Run (cwd: `web/seller`): `pnpm test -- src/modules/04-logistics/api/logistics-company.api.spec.ts`
Expected: FAIL（`Cannot find module './logistics-company.api'`）

- [ ] **Step 10: 实现 logistics-company.api.ts**

创建 `web/seller/src/modules/04-logistics/api/logistics-company.api.ts`：

```typescript
import { http } from '@/shared/http'
import type { LogisticsCompanyDto } from '../types/logistics-company.dto'

/**
 * 物流公司 API 客户端（卖家只读）
 *
 * 与后端 LogisticsCompanyController 对接：
 * - GET /seller/logistics-companies  查询启用态物流公司
 */
export const logisticsCompanyApi = {
  /** 查询启用态物流公司（卖家只读） */
  listEnabled(): Promise<LogisticsCompanyDto[]> {
    return http
      .get<LogisticsCompanyDto[]>('/seller/logistics-companies')
      .then((r) => r.data)
  },
}
```

- [ ] **Step 11: 运行测试确认通过**

Run (cwd: `web/seller`): `pnpm test -- src/modules/04-logistics/api/logistics-company.api.spec.ts`
Expected: PASS（2 tests passed）

- [ ] **Step 12: 类型检查**

Run (cwd: `web/seller`): `pnpm typecheck`
Expected: 0 errors

- [ ] **Step 13: 提交**

```bash
git add web/seller/src/modules/04-logistics/types/freight-template.dto.ts web/seller/src/modules/04-logistics/types/logistics-company.dto.ts web/seller/src/modules/04-logistics/api/freight-template.api.ts web/seller/src/modules/04-logistics/api/freight-template.api.spec.ts web/seller/src/modules/04-logistics/api/logistics-company.api.ts web/seller/src/modules/04-logistics/api/logistics-company.api.spec.ts
git commit -m "feat(seller): add freight-template and logistics-company DTO and API client for 04-logistics"
```

---

## Task 3: 07-review 类型与 API 客户端

**Files:**
- Create: `web/seller/src/modules/07-review/types/review.dto.ts`
- Create: `web/seller/src/modules/07-review/api/review.api.spec.ts`
- Create: `web/seller/src/modules/07-review/api/review.api.ts`

- [ ] **Step 1: 创建 review.dto.ts**

创建 `web/seller/src/modules/07-review/types/review.dto.ts`：

```typescript
/**
 * 07-review 评价回复 DTO
 *
 * 与后端 ReviewController 对接（新 BC 路径 /api/seller/reviews）：
 * - GET  /api/seller/reviews          评价列表（分页 + 筛选）
 * - GET  /api/seller/reviews/{id}      评价详情
 * - POST /api/seller/reviews/{id}/reply 回复评价（覆盖式编辑，1-500 字，幂等）
 */

/** 评价状态（卖家仅可见 Approved） */
export type ReviewStatus = 'Approved' | 'Hidden'

/** 评价查询参数 */
export interface ReviewQueryParams {
  rating?: number
  replied?: boolean
  productName?: string
  startDate?: string
  endDate?: string
  page: number
  pageSize: number
}

/** 评价列表结果 */
export interface ReviewListResultDto {
  items: ReviewDto[]
  total: number
  page: number
  pageSize: number
}

/** 评价详情 */
export interface ReviewDto {
  reviewId: string
  orderId: string
  orderLineId: string
  spuId: string
  skuId: string
  userId: string
  userMaskedName: string
  rating: number
  content: string
  images: string[]
  status: ReviewStatus
  sellerReplyContent?: string
  sellerReplyBy?: string
  sellerReplyAt?: string
  submittedAt: string
  auditedAt?: string
  productName?: string
  productImage?: string
  skuSpec?: string
}

/** 卖家回复 DTO */
export interface SellerReplyDto {
  content: string
}
```

- [ ] **Step 2: 先写 review.api 失败测试**

创建 `web/seller/src/modules/07-review/api/review.api.spec.ts`：

```typescript
import { describe, expect, it, vi, beforeEach } from 'vitest'
import type { AxiosResponse } from 'axios'
import { reviewApi } from './review.api'
import { http, withIdempotency } from '@/shared/http'

/**
 * reviewApi 单元测试
 *
 * client 响应拦截器已 unwrap ApiResponse.data，故 mock http 方法返回
 * AxiosResponse 形态（{ data: 业务对象 }），api 函数内部 .then(r => r.data) 解包。
 */
vi.mock('@/shared/http', () => ({
  http: {
    get: vi.fn(),
    post: vi.fn(),
  },
  withIdempotency: vi.fn(() => ({ headers: { 'Idempotency-Key': 'mock-key' } })),
}))

function mockResponse<T>(data: T): AxiosResponse<T> {
  return { data } as AxiosResponse<T>
}

describe('reviewApi', () => {
  beforeEach(() => {
    vi.clearAllMocks()
    vi.mocked(withIdempotency).mockReturnValue({
      headers: { 'Idempotency-Key': 'mock-key' },
    })
  })

  describe('list', () => {
    it('调用 GET /seller/reviews 并透传查询参数', async () => {
      vi.mocked(http.get).mockResolvedValue(
        mockResponse({ items: [], total: 0, page: 1, pageSize: 20 }),
      )
      const params = { page: 1, pageSize: 20, rating: 5, replied: false }
      await reviewApi.list(params)

      expect(http.get).toHaveBeenCalledWith('/seller/reviews', { params })
    })

    it('返回解包后的评价列表结果', async () => {
      const result = {
        items: [{ reviewId: 'rev-001', rating: 5, content: '好评', images: [], status: 'Approved' }],
        total: 1,
        page: 1,
        pageSize: 20,
      }
      vi.mocked(http.get).mockResolvedValue(mockResponse(result))
      const res = await reviewApi.list({ page: 1, pageSize: 20 })
      expect(res).toEqual(result)
    })
  })

  describe('get', () => {
    it('调用 GET /seller/reviews/{id}', async () => {
      vi.mocked(http.get).mockResolvedValue(
        mockResponse({ reviewId: 'rev-001', rating: 5, content: '好评', images: [], status: 'Approved' }),
      )
      await reviewApi.get('rev-001')
      expect(http.get).toHaveBeenCalledWith('/seller/reviews/rev-001')
    })
  })

  describe('reply', () => {
    it('调用 POST /seller/reviews/{id}/reply 带 Idempotency-Key', async () => {
      vi.mocked(http.post).mockResolvedValue(
        mockResponse({ reviewId: 'rev-001', rating: 5, content: '好评', images: [], status: 'Approved', sellerReplyContent: '感谢支持' }),
      )
      const body = { content: '感谢支持' }
      await reviewApi.reply('rev-001', body)

      expect(http.post).toHaveBeenCalledWith('/seller/reviews/rev-001/reply', body, {
        headers: { 'Idempotency-Key': 'mock-key' },
      })
      expect(withIdempotency).toHaveBeenCalled()
    })

    it('返回解包后的评价详情（含回复）', async () => {
      const review = {
        reviewId: 'rev-001',
        rating: 5,
        content: '好评',
        images: [],
        status: 'Approved',
        sellerReplyContent: '感谢支持',
        sellerReplyAt: '2026-07-30T10:00:00Z',
      }
      vi.mocked(http.post).mockResolvedValue(mockResponse(review))
      const result = await reviewApi.reply('rev-001', { content: '感谢支持' })
      expect(result).toEqual(review)
    })
  })
})
```

- [ ] **Step 3: 运行测试确认失败**

Run (cwd: `web/seller`): `pnpm test -- src/modules/07-review/api/review.api.spec.ts`
Expected: FAIL（`Cannot find module './review.api'`）

- [ ] **Step 4: 实现 review.api.ts**

创建 `web/seller/src/modules/07-review/api/review.api.ts`：

```typescript
import { http, withIdempotency } from '@/shared/http'
import type {
  ReviewDto,
  ReviewListResultDto,
  ReviewQueryParams,
  SellerReplyDto,
} from '../types/review.dto'

/**
 * 评价 API 客户端（新 BC 路径 /api/seller/reviews）
 *
 * 与后端 ReviewController 对接（响应拦截器已解包 ApiResponse.data，
 * 调用方拿到的就是业务负载）：
 * - GET  /seller/reviews          评价列表（分页 + 筛选）
 * - GET  /seller/reviews/{id}      评价详情
 * - POST /seller/reviews/{id}/reply 回复评价（覆盖式编辑，1-500 字，幂等）
 */
export const reviewApi = {
  /** 查询卖家评价列表 */
  list(params: ReviewQueryParams): Promise<ReviewListResultDto> {
    return http
      .get<ReviewListResultDto>('/seller/reviews', { params })
      .then((r) => r.data)
  },

  /** 查询评价详情 */
  get(id: string): Promise<ReviewDto> {
    return http.get<ReviewDto>(`/seller/reviews/${id}`).then((r) => r.data)
  },

  /** 回复评价（覆盖式编辑，1-500 字） */
  reply(id: string, body: SellerReplyDto): Promise<ReviewDto> {
    return http
      .post<ReviewDto>(`/seller/reviews/${id}/reply`, body, withIdempotency())
      .then((r) => r.data)
  },
}
```

- [ ] **Step 5: 运行测试确认通过**

Run (cwd: `web/seller`): `pnpm test -- src/modules/07-review/api/review.api.spec.ts`
Expected: PASS（5 tests passed）

- [ ] **Step 6: 类型检查**

Run (cwd: `web/seller`): `pnpm typecheck`
Expected: 0 errors

- [ ] **Step 7: 提交**

```bash
git add web/seller/src/modules/07-review/types/review.dto.ts web/seller/src/modules/07-review/api/review.api.ts web/seller/src/modules/07-review/api/review.api.spec.ts
git commit -m "feat(seller): add review DTO and API client for 07-review module"
```

---

## Task 4: Mock handlers（freight + logistics + review）+ seed 扩展 + 装配注册

**Files:**
- Modify: `web/seller/src/shared/http/mock/data/types.ts`
- Modify: `web/seller/src/shared/http/mock/data/seed.ts`
- Create: `web/seller/src/shared/http/mock/handlers/freight.ts`
- Create: `web/seller/src/shared/http/mock/handlers/logistics.ts`
- Create: `web/seller/src/shared/http/mock/handlers/review.ts`
- Modify: `web/seller/src/shared/http/mock/index.ts`

- [ ] **Step 1: 扩展 MockSeed 类型**

修改 `web/seller/src/shared/http/mock/data/types.ts`。

若批次 1 已执行，当前文件应已含 `shop`/`qualifications`。在 `qualifications` 之后追加三个字段（保留既有字段）：

```typescript
export interface MockSeed {
  menus: unknown[]
  onlineUsers: unknown[]
  loginLogs: unknown[]
  redisKeys: unknown[]
  redisInfo: unknown
  keyspaces: unknown[]
  serverSnapshot: unknown
  serverHistory: { cpu: unknown[]; memory: unknown[]; diskIo: unknown[] }
  shop: unknown
  qualifications: unknown[]
  freightTemplates: unknown[]
  logisticsCompanies: unknown[]
  reviews: unknown[]
  nextId: number
}
```

> **注**：若批次 1 未执行（`shop`/`qualifications` 缺失），请先执行批次 1 计划。本批次假定 `shop`/`qualifications` 已存在。

- [ ] **Step 2: 扩展 seed.ts — 注入 freight/logistics/review 种子**

修改 `web/seller/src/shared/http/mock/data/seed.ts`：

2a. 在 `ensureSeedData` 函数的 seed 初始化对象中，`qualifications` 之后、`nextId` 之前追加三行：

```typescript
    freightTemplates: buildFreightTemplateSeed(),
    logisticsCompanies: buildLogisticsCompanySeed(),
    reviews: buildReviewSeed(),
```

修改后 `ensureSeedData` 内 seed 对象片段（若批次 1 已执行）：

```typescript
  const seed: MockSeed = {
    menus: buildMenuSeed(),
    onlineUsers: buildOnlineUserSeed(),
    loginLogs: buildLoginLogSeed(),
    redisKeys: buildRedisKeySeed(),
    redisInfo: buildRedisInfoSeed(),
    keyspaces: buildKeyspaceSeed(),
    serverSnapshot: buildServerSnapshotSeed(),
    serverHistory: { cpu: [], memory: [], diskIo: [] },
    shop: buildShopSeed(),
    qualifications: buildQualificationSeed(),
    freightTemplates: buildFreightTemplateSeed(),
    logisticsCompanies: buildLogisticsCompanySeed(),
    reviews: buildReviewSeed(),
    nextId: 1000,
  }
```

> **注**：若批次 1 未执行，`shop`/`qualifications` 行不存在，请先执行批次 1。

2b. 在文件末尾（`advanceServerHistory` 函数之后，或批次 1 追加的 builder 之后）追加三个 builder：

```typescript
// ===== 运费模板种子（2 个：固定运费 + 按重量）=====

function buildFreightTemplateSeed(): unknown[] {
  return [
    {
      id: 'ft-001',
      name: '全国统一运费',
      pricingType: 'Fixed',
      fixedFee: 10,
      freeShippingThreshold: undefined,
      regionRules: [],
      isEnabled: true,
      version: 1,
      createdAt: '2026-02-01T00:00:00Z',
      updatedAt: '2026-02-01T00:00:00Z',
    },
    {
      id: 'ft-002',
      name: '按重量计费',
      pricingType: 'ByWeight',
      fixedFee: undefined,
      freeShippingThreshold: 99,
      regionRules: [
        {
          id: 'r-001',
          regionCode: 'CN',
          regionName: '全国',
          firstUnit: 1,
          firstPrice: 8,
          nextUnit: 1,
          nextPrice: 2,
        },
      ],
      isEnabled: true,
      version: 1,
      createdAt: '2026-02-01T00:00:00Z',
      updatedAt: '2026-02-01T00:00:00Z',
    },
  ]
}

// ===== 物流公司种子（5 个）=====

function buildLogisticsCompanySeed(): unknown[] {
  return [
    { id: 'lc-001', name: '顺丰速运', code: 'SF', servicePhone: '95338', website: 'https://www.sf-express.com', supportsTracking: true, sortOrder: 1 },
    { id: 'lc-002', name: '中通快递', code: 'ZTO', servicePhone: '95311', website: 'https://www.zto.com', supportsTracking: true, sortOrder: 2 },
    { id: 'lc-003', name: '圆通速递', code: 'YTO', servicePhone: '95554', website: 'https://www.yto.net.cn', supportsTracking: true, sortOrder: 3 },
    { id: 'lc-004', name: '韵达快递', code: 'YUNDA', servicePhone: '95546', website: 'https://www.yundaex.com', supportsTracking: true, sortOrder: 4 },
    { id: 'lc-005', name: 'EMS', code: 'EMS', servicePhone: '11183', website: 'https://www.ems.com.cn', supportsTracking: true, sortOrder: 5 },
  ]
}

// ===== 评价种子（10 条：5 已回复 + 5 未回复，评分 1-5 星分布）=====

function buildReviewSeed(): unknown[] {
  return [
    { reviewId: 'rev-001', orderId: 'ord-101', orderLineId: 'ol-101', spuId: 'spu-001', skuId: 'sku-001', userId: 'u-001', userMaskedName: '13****5678', rating: 5, content: '质量非常好，面料舒适，做工精细，物流也很快！', images: [], status: 'Approved', sellerReplyContent: '感谢您的支持，欢迎再次光临！', sellerReplyBy: 'seller-001', sellerReplyAt: '2026-07-15T10:00:00Z', submittedAt: '2026-07-14T15:30:00Z', auditedAt: '2026-07-14T16:00:00Z', productName: '纯棉圆领T恤 白色 L', productImage: '', skuSpec: '白色 / L' },
    { reviewId: 'rev-002', orderId: 'ord-102', orderLineId: 'ol-102', spuId: 'spu-002', skuId: 'sku-002', userId: 'u-002', userMaskedName: '18****1234', rating: 4, content: '整体不错，就是尺码偏小，建议买大一码。', images: ['img-001.jpg'], status: 'Approved', sellerReplyContent: '感谢反馈，我们会优化尺码表。', sellerReplyBy: 'seller-001', sellerReplyAt: '2026-07-16T09:00:00Z', submittedAt: '2026-07-15T20:00:00Z', auditedAt: '2026-07-15T21:00:00Z', productName: '修身衬衫 蓝色 M', productImage: '', skuSpec: '蓝色 / M' },
    { reviewId: 'rev-003', orderId: 'ord-103', orderLineId: 'ol-103', spuId: 'spu-001', skuId: 'sku-003', userId: 'u-003', userMaskedName: '15****8888', rating: 5, content: '回购第三次了，一如既往的好！', images: [], status: 'Approved', sellerReplyContent: '感恩老客户，已为您发放优惠券！', sellerReplyBy: 'seller-001', sellerReplyAt: '2026-07-17T14:00:00Z', submittedAt: '2026-07-16T11:00:00Z', auditedAt: '2026-07-16T12:00:00Z', productName: '纯棉圆领T恤 黑色 XL', productImage: '', skuSpec: '黑色 / XL' },
    { reviewId: 'rev-004', orderId: 'ord-104', orderLineId: 'ol-104', spuId: 'spu-003', skuId: 'sku-004', userId: 'u-004', userMaskedName: '19****6666', rating: 3, content: '一般般，性价比还行，但颜色和图片有点色差。', images: ['img-002.jpg', 'img-003.jpg'], status: 'Approved', sellerReplyContent: '抱歉给您带来不便，我们会改进拍摄。', sellerReplyBy: 'seller-001', sellerReplyAt: '2026-07-18T10:00:00Z', submittedAt: '2026-07-17T16:00:00Z', auditedAt: '2026-07-17T17:00:00Z', productName: '雪纺连衣裙 粉色 S', productImage: '', skuSpec: '粉色 / S' },
    { reviewId: 'rev-005', orderId: 'ord-105', orderLineId: 'ol-105', spuId: 'spu-002', skuId: 'sku-005', userId: 'u-005', userMaskedName: '17****3333', rating: 4, content: '衬衫质量不错，包装也很好。', images: [], status: 'Approved', sellerReplyContent: '谢谢好评！', sellerReplyBy: 'seller-001', sellerReplyAt: '2026-07-19T08:00:00Z', submittedAt: '2026-07-18T09:00:00Z', auditedAt: '2026-07-18T10:00:00Z', productName: '修身衬衫 白色 L', productImage: '', skuSpec: '白色 / L' },
    { reviewId: 'rev-006', orderId: 'ord-106', orderLineId: 'ol-106', spuId: 'spu-001', skuId: 'sku-001', userId: 'u-006', userMaskedName: '13****9999', rating: 2, content: '面料有点硬，洗了一次就起球了，不太满意。', images: ['img-004.jpg'], status: 'Approved', submittedAt: '2026-07-20T13:00:00Z', auditedAt: '2026-07-20T14:00:00Z', productName: '纯棉圆领T恤 白色 L', productImage: '', skuSpec: '白色 / L' },
    { reviewId: 'rev-007', orderId: 'ord-107', orderLineId: 'ol-107', spuId: 'spu-003', skuId: 'sku-006', userId: 'u-007', userMaskedName: '18****7777', rating: 5, content: '裙子很漂亮，版型好，朋友都说好看！', images: [], status: 'Approved', submittedAt: '2026-07-21T10:00:00Z', auditedAt: '2026-07-21T11:00:00Z', productName: '雪纺连衣裙 蓝色 M', productImage: '', skuSpec: '蓝色 / M' },
    { reviewId: 'rev-008', orderId: 'ord-108', orderLineId: 'ol-108', spuId: 'spu-002', skuId: 'sku-002', userId: 'u-008', userMaskedName: '15****2222', rating: 1, content: '扣子掉了两个，质量太差了，要求退款。', images: ['img-005.jpg', 'img-006.jpg', 'img-007.jpg'], status: 'Approved', submittedAt: '2026-07-22T17:00:00Z', auditedAt: '2026-07-22T18:00:00Z', productName: '修身衬衫 蓝色 M', productImage: '', skuSpec: '蓝色 / M' },
    { reviewId: 'rev-009', orderId: 'ord-109', orderLineId: 'ol-109', spuId: 'spu-001', skuId: 'sku-003', userId: 'u-009', userMaskedName: '19****4444', rating: 4, content: '不错，穿着很舒服，就是快递有点慢。', images: [], status: 'Approved', submittedAt: '2026-07-23T08:00:00Z', auditedAt: '2026-07-23T09:00:00Z', productName: '纯棉圆领T恤 黑色 XL', productImage: '', skuSpec: '黑色 / XL' },
    { reviewId: 'rev-010', orderId: 'ord-110', orderLineId: 'ol-110', spuId: 'spu-003', skuId: 'sku-004', userId: 'u-010', userMaskedName: '17****0000', rating: 3, content: '裙子颜色不错但偏短，身高170穿刚好。', images: [], status: 'Approved', submittedAt: '2026-07-24T12:00:00Z', auditedAt: '2026-07-24T13:00:00Z', productName: '雪纺连衣裙 粉色 S', productImage: '', skuSpec: '粉色 / S' },
  ]
}
```

> **注**：若批次 1 未执行，`buildShopSeed`/`buildQualificationSeed` 不存在，请先执行批次 1。

- [ ] **Step 3: 实现 handlers/freight.ts**

创建 `web/seller/src/shared/http/mock/handlers/freight.ts`：

```typescript
/* eslint-disable @typescript-eslint/no-explicit-any */
import type MockAdapter from 'axios-mock-adapter'
import { loadSeedData, saveSeedData, nextId } from '../data/seed'

/**
 * 运费模板 handler 注册
 *
 * 端点（baseURL=/api，故拦截 /seller/freight-templates/...）：
 * - GET    /seller/freight-templates/mine        查询当前卖家运费模板列表
 * - POST   /seller/freight-templates             创建运费模板
 * - PUT    /seller/freight-templates/{id}/rules  更新区域规则（乐观锁 version）
 * - POST   /seller/freight-templates/{id}/enable  启用模板
 * - POST   /seller/freight-templates/{id}/disable 停用模板
 */
export function registerFreightHandlers(mock: MockAdapter): void {
  // 查询当前卖家运费模板列表
  mock.onGet('/seller/freight-templates/mine').reply(() => {
    const seed = loadSeedData()
    return [200, { code: 200, message: 'OK', data: seed.freightTemplates }]
  })

  // 创建运费模板
  mock.onPost('/seller/freight-templates').reply((config) => {
    const seed = loadSeedData()
    const body = JSON.parse(config.data || '{}')
    if (!body.name || !body.pricingType) {
      return [200, { code: 40001, message: '模板名称与计费类型必填', data: null }]
    }
    const now = new Date().toISOString()
    const tpl = {
      id: nextId(seed, 'ft'),
      name: body.name,
      pricingType: body.pricingType,
      fixedFee: body.fixedFee,
      freeShippingThreshold: body.freeShippingThreshold,
      regionRules: [],
      isEnabled: true,
      version: 1,
      createdAt: now,
      updatedAt: now,
    }
    ;(seed.freightTemplates as any[]).push(tpl)
    saveSeedData(seed)
    return [200, { code: 200, message: 'OK', data: tpl }]
  })

  // 更新区域规则（乐观锁）
  mock.onPut(/\/seller\/freight-templates\/[^/]+\/rules$/).reply((config) => {
    const id = config.url!.split('/')[3]
    const seed = loadSeedData()
    const tpl = (seed.freightTemplates as any[]).find((t) => t.id === id)
    if (!tpl) {
      return [200, { code: 40400, message: `运费模板 ${id} 不存在`, data: null }]
    }
    const body = JSON.parse(config.data || '{}')
    if (typeof body.version === 'number' && body.version !== tpl.version) {
      return [
        409,
        {
          code: 409,
          message: '运费模板已被他人修改',
          currentVersion: tpl.version,
          data: null,
        },
      ]
    }
    tpl.regionRules = body.regionRules || []
    tpl.version = (tpl.version ?? 1) + 1
    tpl.updatedAt = new Date().toISOString()
    saveSeedData(seed)
    return [200, { code: 200, message: 'OK', data: tpl }]
  })

  // 启用模板
  mock.onPost(/\/seller\/freight-templates\/[^/]+\/enable$/).reply((config) => {
    const id = config.url!.split('/')[3]
    const seed = loadSeedData()
    const tpl = (seed.freightTemplates as any[]).find((t) => t.id === id)
    if (!tpl) {
      return [200, { code: 40400, message: `运费模板 ${id} 不存在`, data: null }]
    }
    tpl.isEnabled = true
    tpl.updatedAt = new Date().toISOString()
    saveSeedData(seed)
    return [200, { code: 200, message: 'OK', data: null }]
  })

  // 停用模板
  mock.onPost(/\/seller\/freight-templates\/[^/]+\/disable$/).reply((config) => {
    const id = config.url!.split('/')[3]
    const seed = loadSeedData()
    const tpl = (seed.freightTemplates as any[]).find((t) => t.id === id)
    if (!tpl) {
      return [200, { code: 40400, message: `运费模板 ${id} 不存在`, data: null }]
    }
    tpl.isEnabled = false
    tpl.updatedAt = new Date().toISOString()
    saveSeedData(seed)
    return [200, { code: 200, message: 'OK', data: null }]
  })
}
```

- [ ] **Step 4: 实现 handlers/logistics.ts**

创建 `web/seller/src/shared/http/mock/handlers/logistics.ts`：

```typescript
/* eslint-disable @typescript-eslint/no-explicit-any */
import type MockAdapter from 'axios-mock-adapter'
import { loadSeedData } from '../data/seed'

/**
 * 物流公司 handler 注册
 *
 * 端点（baseURL=/api，故拦截 /seller/logistics-companies）：
 * - GET /seller/logistics-companies  查询启用态物流公司（卖家只读）
 */
export function registerLogisticsHandlers(mock: MockAdapter): void {
  mock.onGet('/seller/logistics-companies').reply(() => {
    const seed = loadSeedData()
    const companies = [...(seed.logisticsCompanies as any[])].sort(
      (a, b) => a.sortOrder - b.sortOrder,
    )
    return [200, { code: 200, message: 'OK', data: companies }]
  })
}
```

- [ ] **Step 5: 实现 handlers/review.ts**

创建 `web/seller/src/shared/http/mock/handlers/review.ts`：

```typescript
/* eslint-disable @typescript-eslint/no-explicit-any */
import type MockAdapter from 'axios-mock-adapter'
import { loadSeedData, saveSeedData } from '../data/seed'

/**
 * 评价 handler 注册
 *
 * 端点（baseURL=/api，故拦截 /seller/reviews/...）：
 * - GET  /seller/reviews          评价列表（分页 + 筛选）
 * - GET  /seller/reviews/{id}      评价详情
 * - POST /seller/reviews/{id}/reply 回复评价（覆盖式编辑）
 */
export function registerReviewHandlers(mock: MockAdapter): void {
  // 评价列表（分页 + 筛选）
  mock.onGet('/seller/reviews').reply((config) => {
    const seed = loadSeedData()
    const params = config.params || {}
    let items = [...(seed.reviews as any[])]

    // 评分筛选
    if (params.rating !== undefined && params.rating !== null && params.rating !== '') {
      items = items.filter((r) => r.rating === Number(params.rating))
    }
    // 回复状态筛选
    if (params.replied !== undefined && params.replied !== null && params.replied !== '') {
      const replied = params.replied === true || params.replied === 'true'
      items = items.filter((r) => !!r.sellerReplyContent === replied)
    }
    // 商品名称筛选（模糊匹配）
    if (params.productName) {
      const kw = String(params.productName).toLowerCase()
      items = items.filter((r) => (r.productName || '').toLowerCase().includes(kw))
    }
    // 时间范围筛选
    if (params.startDate) {
      items = items.filter((r) => new Date(r.submittedAt) >= new Date(params.startDate))
    }
    if (params.endDate) {
      items = items.filter((r) => new Date(r.submittedAt) <= new Date(params.endDate))
    }

    const page = Number(params.page) || 1
    const pageSize = Number(params.pageSize) || 20
    const total = items.length
    const start = (page - 1) * pageSize
    const paged = items.slice(start, start + pageSize)

    return [
      200,
      { code: 200, message: 'OK', data: { items: paged, total, page, pageSize } },
    ]
  })

  // 评价详情
  mock.onGet(/\/seller\/reviews\/[^/]+$/).reply((config) => {
    const id = config.url!.split('/').pop()!
    const seed = loadSeedData()
    const review = (seed.reviews as any[]).find((r) => r.reviewId === id)
    if (!review) {
      return [200, { code: 40400, message: `评价 ${id} 不存在`, data: null }]
    }
    return [200, { code: 200, message: 'OK', data: review }]
  })

  // 回复评价（覆盖式编辑）
  mock.onPost(/\/seller\/reviews\/[^/]+\/reply$/).reply((config) => {
    const id = config.url!.split('/')[3]
    const seed = loadSeedData()
    const review = (seed.reviews as any[]).find((r) => r.reviewId === id)
    if (!review) {
      return [200, { code: 40400, message: `评价 ${id} 不存在`, data: null }]
    }
    const body = JSON.parse(config.data || '{}')
    if (!body.content || body.content.trim().length === 0) {
      return [200, { code: 40001, message: '回复内容不能为空', data: null }]
    }
    if (body.content.length > 500) {
      return [200, { code: 40001, message: '回复内容不超过 500 字', data: null }]
    }
    review.sellerReplyContent = body.content
    review.sellerReplyBy = 'seller-001'
    review.sellerReplyAt = new Date().toISOString()
    saveSeedData(seed)
    return [200, { code: 200, message: 'OK', data: review }]
  })
}
```

- [ ] **Step 6: 在 mock/index.ts 注册 3 个新 handler**

修改 `web/seller/src/shared/http/mock/index.ts`：

6a. 在 import 区追加（`registerServerMonitorHandlers` 之后，批次 1 的 `registerShopHandlers` 之后）：

```typescript
import { registerFreightHandlers } from './handlers/freight'
import { registerLogisticsHandlers } from './handlers/logistics'
import { registerReviewHandlers } from './handlers/review'
```

> **注**：若批次 1 已执行，`registerShopHandlers` import 应已存在。追加 3 行在其后。

6b. 在 `registerShopHandlers(mock)` 之后（批次 1 追加的行之后）追加 3 行：

```typescript
  registerFreightHandlers(mock)
  registerLogisticsHandlers(mock)
  registerReviewHandlers(mock)
```

6c. 将启动日志行改为：

```typescript
  console.log('[Mock] 已启用 9 个 handler，共 30 个 endpoint')
```

> **注**：批次 1 将日志改为 `6 个 handler，24 个 endpoint`。本批次再追加 3 个 handler（freight 5 端点 + logistics 1 端点 + review 3 端点 = 9 端点），合计 9 个 handler / 33 个 endpoint。若批次 1 未执行（handler 数为 5），则本批次后为 8 个 handler。请根据实际批次 1 执行情况调整日志数字。推荐统一写为 `9 个 handler`（批次 1+2 合计）。

- [ ] **Step 7: 类型检查 + lint**

Run (cwd: `web/seller`): `pnpm typecheck`
Expected: 0 errors

Run (cwd: `web/seller`): `pnpm lint`
Expected: 0 errors / 0 warnings

- [ ] **Step 8: 提交**

```bash
git add web/seller/src/shared/http/mock/data/types.ts web/seller/src/shared/http/mock/data/seed.ts web/seller/src/shared/http/mock/handlers/freight.ts web/seller/src/shared/http/mock/handlers/logistics.ts web/seller/src/shared/http/mock/handlers/review.ts web/seller/src/shared/http/mock/index.ts
git commit -m "feat(seller): add freight/logistics/review mock handlers and seed data"
```

---

## Task 5: FreightTemplates.vue — 运费模板列表 + 新建弹窗 + 编辑规则抽屉 + 启停 Switch

**Files:**
- Create: `web/seller/src/modules/04-logistics/views/FreightTemplates.vue`

- [ ] **Step 1: 实现 FreightTemplates.vue**

创建 `web/seller/src/modules/04-logistics/views/FreightTemplates.vue`：

```vue
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
```

- [ ] **Step 2: 类型检查 + lint**

Run (cwd: `web/seller`): `pnpm typecheck`
Expected: 0 errors

Run (cwd: `web/seller`): `pnpm lint`
Expected: 0 errors / 0 warnings

- [ ] **Step 3: 提交**

```bash
git add web/seller/src/modules/04-logistics/views/FreightTemplates.vue
git commit -m "feat(seller): add FreightTemplates page with create modal and rule editor drawer"
```

---

## Task 6: LogisticsCompanies.vue — 物流公司只读表格 + 10 分钟缓存 + 复制编码

**Files:**
- Create: `web/seller/src/modules/04-logistics/views/LogisticsCompanies.vue`

- [ ] **Step 1: 实现 LogisticsCompanies.vue**

创建 `web/seller/src/modules/04-logistics/views/LogisticsCompanies.vue`：

```vue
<script setup lang="ts">
import { ref, onMounted, h } from 'vue'
import {
  Breadcrumb,
  BreadcrumbItem,
  Card,
  Table,
  Tag,
  Button,
  Tooltip,
  Skeleton,
  message,
} from 'ant-design-vue'
import type { TableColumnsType } from 'ant-design-vue'
import { CopyOutlined, LinkOutlined } from '@ant-design/icons-vue'
import { logisticsCompanyApi } from '../api/logistics-company.api'
import type { LogisticsCompanyDto } from '../types/logistics-company.dto'
import { EmptyState } from '@/shared/components'
import { logger } from '@/shared/utils/logger'

/**
 * 物流公司页（只读）
 *
 * 路由 /logistics/companies，权限 logistics-company:list
 * - 只读表格（名称 / 编码 / 客服电话 / 是否支持轨迹查询 / 官网链接）
 * - 10 分钟前端缓存：localStorage 键 logistics_companies_cache，含 data + fetchedAt
 * - 复制编码功能：点击编码复制到剪贴板
 */

const CACHE_KEY = 'logistics_companies_cache'
const CACHE_TTL = 10 * 60 * 1000 // 10 分钟

interface CacheEntry {
  data: LogisticsCompanyDto[]
  fetchedAt: number
}

const loading = ref(false)
const companies = ref<LogisticsCompanyDto[]>([])

const columns: TableColumnsType = [
  { title: '名称', dataIndex: 'name', key: 'name', width: 180, ellipsis: true },
  { title: '编码', dataIndex: 'code', key: 'code', width: 140 },
  { title: '客服电话', dataIndex: 'servicePhone', key: 'servicePhone', width: 140 },
  { title: '支持轨迹查询', dataIndex: 'supportsTracking', key: 'supportsTracking', width: 140, align: 'center' },
  { title: '官网', dataIndex: 'website', key: 'website', ellipsis: true },
]

function readCache(): LogisticsCompanyDto[] | null {
  try {
    const raw = localStorage.getItem(CACHE_KEY)
    if (!raw) return null
    const entry = JSON.parse(raw) as CacheEntry
    if (Date.now() - entry.fetchedAt > CACHE_TTL) return null
    return entry.data
  } catch {
    return null
  }
}

function writeCache(data: LogisticsCompanyDto[]): void {
  const entry: CacheEntry = { data, fetchedAt: Date.now() }
  localStorage.setItem(CACHE_KEY, JSON.stringify(entry))
}

async function loadList(): Promise<void> {
  // 优先读缓存
  const cached = readCache()
  if (cached) {
    companies.value = cached
    return
  }
  loading.value = true
  try {
    const data = await logisticsCompanyApi.listEnabled()
    companies.value = data
    writeCache(data)
  } catch (e) {
    logger.error('加载物流公司列表失败', e)
    message.error('加载物流公司列表失败')
  } finally {
    loading.value = false
  }
}

async function onCopyCode(code: string): Promise<void> {
  try {
    await navigator.clipboard.writeText(code)
    message.success(`已复制编码：${code}`)
  } catch {
    // 降级方案：使用 textarea
    const textarea = document.createElement('textarea')
    textarea.value = code
    document.body.appendChild(textarea)
    textarea.select()
    try {
      document.execCommand('copy')
      message.success(`已复制编码：${code}`)
    } catch {
      message.error('复制失败，请手动复制')
    }
    document.body.removeChild(textarea)
  }
}

onMounted(() => {
  void loadList()
})
</script>

<template>
  <div class="logistics-companies-page">
    <Breadcrumb class="logistics-companies-breadcrumb">
      <BreadcrumbItem>物流管理</BreadcrumbItem>
      <BreadcrumbItem>物流公司</BreadcrumbItem>
    </Breadcrumb>

    <Card class="logistics-companies-card" :bordered="true">
      <template #title>
        <span class="logistics-companies-title">物流公司</span>
      </template>
      <template #extra>
        <span class="logistics-companies-cache-hint">
          数据缓存 10 分钟
        </span>
      </template>

      <Skeleton v-if="loading" active :paragraph="{ rows: 5 }" />
      <EmptyState
        v-else-if="companies.length === 0"
        description="暂无启用的物流公司"
      />
      <Table
        v-else
        :columns="columns"
        :data-source="companies"
        row-key="id"
        :pagination="false"
        size="middle"
      >
        <template #bodyCell="{ column, record }">
          <template v-if="column.key === 'code'">
            <Button
              type="link"
              size="small"
              :icon="h(CopyOutlined)"
              @click="onCopyCode(record.code)"
            >
              {{ record.code }}
            </Button>
          </template>
          <template v-else-if="column.key === 'servicePhone'">
            {{ record.servicePhone || '—' }}
          </template>
          <template v-else-if="column.key === 'supportsTracking'">
            <Tag v-if="record.supportsTracking" color="success">支持</Tag>
            <Tag v-else color="default">不支持</Tag>
          </template>
          <template v-else-if="column.key === 'website'">
            <a
              v-if="record.website"
              :href="record.website"
              target="_blank"
              rel="noopener noreferrer"
            >
              <LinkOutlined />
              {{ record.website }}
            </a>
            <span v-else>—</span>
          </template>
        </template>
      </Table>
    </Card>
  </div>
</template>

<style scoped>
.logistics-companies-page {
  display: flex;
  flex-direction: column;
  gap: 16px;
}
.logistics-companies-breadcrumb {
  font-size: 14px;
}
.logistics-companies-card {
  border-radius: 8px;
}
.logistics-companies-title {
  font-size: 15px;
  font-weight: 500;
}
.logistics-companies-cache-hint {
  font-size: 12px;
  color: #8c8c8c;
}
</style>
```

- [ ] **Step 2: 类型检查 + lint**

Run (cwd: `web/seller`): `pnpm typecheck`
Expected: 0 errors

Run (cwd: `web/seller`): `pnpm lint`
Expected: 0 errors / 0 warnings

- [ ] **Step 3: 提交**

```bash
git add web/seller/src/modules/04-logistics/views/LogisticsCompanies.vue
git commit -m "feat(seller): add LogisticsCompanies readonly page with 10min cache and copy code"
```

---

## Task 7: ReviewReply.vue — 评价卡片列表 + 回复抽屉

**Files:**
- Create: `web/seller/src/modules/07-review/views/ReviewReply.vue`

- [ ] **Step 1: 实现 ReviewReply.vue**

创建 `web/seller/src/modules/07-review/views/ReviewReply.vue`：

```vue
<script setup lang="ts">
import { ref, reactive, computed, onMounted, watch } from 'vue'
import { useRouter } from 'vue-router'
import {
  Breadcrumb,
  BreadcrumbItem,
  Card,
  Row,
  Col,
  Statistic,
  Tag,
  Select,
  InputSearch,
  Avatar,
  Rate,
  Typography,
  Image as AImage,
  Drawer,
  Form,
  FormItem,
  Input,
  Button,
  Space,
  Spin,
  Skeleton,
  message,
  Modal,
} from 'ant-design-vue'
import { UserOutlined } from '@ant-design/icons-vue'
import { reviewApi } from '../api/review.api'
import type {
  ReviewDto,
  ReviewQueryParams,
  ReviewStatus,
} from '../types/review.dto'
import { StatusTag, EmptyState, IdempotencyButton, DateTimeRangePicker } from '@/shared/components'
import { logger } from '@/shared/utils/logger'
import { formatDateTime } from '@/shared/utils/format'

/**
 * 评价回复页
 *
 * 路由 /reviews，权限 review:list
 * 严格遵循设计稿：评价卡片 4 区（头部/商品快照/正文/回复区）+ 回复抽屉 480px。
 * 新 BC 路径 /api/seller/reviews。
 */

const router = useRouter()

const loading = ref(false)
const replying = ref(false)
const reviews = ref<ReviewDto[]>([])
const total = ref(0)

const page = ref(1)
const pageSize = ref(20)

const filters = reactive({
  rating: undefined as number | undefined,
  replied: undefined as boolean | undefined,
  productName: '',
  dateRange: undefined as [string, string] | undefined,
})

// 回复抽屉
const replyDrawerOpen = ref(false)
const replyingReview = ref<ReviewDto | null>(null)
const replyContent = ref('')
const replyOriginalContent = ref('')
const isEditingReply = computed(() => !!replyingReview.value?.sellerReplyContent)

// 防抖
let debounceTimer: ReturnType<typeof setTimeout> | null = null

const ratingOptions = [
  { label: '全部', value: undefined },
  { label: '5 星', value: 5 },
  { label: '4 星', value: 4 },
  { label: '3 星', value: 3 },
  { label: '2 星', value: 2 },
  { label: '1 星', value: 1 },
]

const repliedOptions = [
  { label: '全部', value: undefined },
  { label: '待回复', value: false },
  { label: '已回复', value: true },
]

// 统计
const positiveRate = computed(() => {
  if (reviews.value.length === 0) return 0
  const positive = reviews.value.filter((r) => r.rating >= 4).length
  return Math.round((positive / reviews.value.length) * 100)
})

const pendingReplyCount = computed(
  () => reviews.value.filter((r) => !r.sellerReplyContent).length,
)

const replyCharCount = computed(() => replyContent.value.length)
const replyChanged = computed(() => replyContent.value !== replyOriginalContent.value)

function buildParams(): ReviewQueryParams {
  const params: ReviewQueryParams = {
    page: page.value,
    pageSize: pageSize.value,
  }
  if (filters.rating !== undefined) params.rating = filters.rating
  if (filters.replied !== undefined) params.replied = filters.replied
  if (filters.productName.trim()) params.productName = filters.productName.trim()
  if (filters.dateRange) {
    params.startDate = filters.dateRange[0]
    params.endDate = filters.dateRange[1]
  }
  return params
}

async function loadList(): Promise<void> {
  loading.value = true
  try {
    const result = await reviewApi.list(buildParams())
    reviews.value = result.items
    total.value = result.total
  } catch (e) {
    logger.error('加载评价列表失败', e)
    message.error('加载评价列表失败')
    reviews.value = []
    total.value = 0
  } finally {
    loading.value = false
  }
}

function onSearch(): void {
  page.value = 1
  void loadList()
}

function onProductNameInput(): void {
  if (debounceTimer) clearTimeout(debounceTimer)
  debounceTimer = setTimeout(() => {
    onSearch()
  }, 300)
}

function onDateRangeChange(value: [string, string]): void {
  filters.dateRange = value
  onSearch()
}

function openReplyDrawer(review: ReviewDto): void {
  replyingReview.value = review
  replyContent.value = review.sellerReplyContent || ''
  replyOriginalContent.value = review.sellerReplyContent || ''
  replyDrawerOpen.value = true
}

function closeReplyDrawer(): void {
  if (replyChanged.value) {
    Modal.confirm({
      title: '确认放弃当前编辑内容？',
      content: '您有未保存的回复内容，关闭后将丢失。',
      okText: '放弃',
      cancelText: '继续编辑',
      okType: 'danger',
      onOk: () => {
        replyDrawerOpen.value = false
        replyingReview.value = null
        replyContent.value = ''
        replyOriginalContent.value = ''
      },
    })
    return
  }
  replyDrawerOpen.value = false
  replyingReview.value = null
}

async function onSubmitReply(): Promise<void> {
  if (!replyingReview.value) return
  const content = replyContent.value.trim()
  if (content.length === 0) {
    message.warning('回复内容不能为空')
    return
  }
  if (content.length > 500) {
    message.warning('回复内容不超过 500 字')
    return
  }
  replying.value = true
  try {
    const updated = await reviewApi.reply(replyingReview.value.reviewId, { content })
    reviews.value = reviews.value.map((r) =>
      r.reviewId === updated.reviewId ? updated : r,
    )
    message.success(isEditingReply.value ? '回复已更新' : '回复成功')
    replyDrawerOpen.value = false
    replyingReview.value = null
    replyContent.value = ''
    replyOriginalContent.value = ''
  } catch (e) {
    logger.error('回复评价失败', e)
    message.error('回复失败，请稍后重试')
  } finally {
    replying.value = false
  }
}

function goEditProduct(spuId: string): void {
  router.push(`/products/${spuId}/edit`)
}

onMounted(() => {
  void loadList()
})

watch(
  () => [filters.rating, filters.replied] as [number | undefined, boolean | undefined],
  () => {
    onSearch()
  },
)
</script>

<template>
  <div class="review-reply-page">
    <Breadcrumb class="review-reply-breadcrumb">
      <BreadcrumbItem>评价回复</BreadcrumbItem>
    </Breadcrumb>

    <!-- 顶部统计区 -->
    <Card class="review-reply-stats-card" :bordered="true" size="small">
      <Row :gutter="24">
        <Col :span="8">
          <Statistic title="好评率（≥4 星）" :value="positiveRate" suffix="%" />
        </Col>
        <Col :span="8">
          <div class="review-reply-stat-box">
            <div class="review-reply-stat-label">待回复</div>
            <Tag v-if="pendingReplyCount > 0" color="red">
              {{ pendingReplyCount }} 条
            </Tag>
            <Tag v-else color="success">无待回复</Tag>
          </div>
        </Col>
        <Col :span="8">
          <div class="review-reply-stat-box">
            <div class="review-reply-stat-label">总评价数</div>
            <span class="review-reply-stat-value">{{ total }}</span>
          </div>
        </Col>
      </Row>
    </Card>

    <!-- 筛选栏 -->
    <Card class="review-reply-filter-card" :bordered="true" size="small">
      <Row :gutter="12">
        <Col :span="4">
          <div class="review-reply-filter-label">评分</div>
          <Select
            v-model:value="filters.rating"
            :options="ratingOptions"
            style="width: 100%"
            placeholder="评分"
            allow-clear
          />
        </Col>
        <Col :span="4">
          <div class="review-reply-filter-label">回复状态</div>
          <Select
            v-model:value="filters.replied"
            :options="repliedOptions"
            style="width: 100%"
            placeholder="回复状态"
            allow-clear
          />
        </Col>
        <Col :span="6">
          <div class="review-reply-filter-label">商品名称</div>
          <InputSearch
            v-model:value="filters.productName"
            placeholder="搜索商品名称"
            allow-clear
            @input="onProductNameInput"
            @search="onSearch"
          />
        </Col>
        <Col :span="10">
          <div class="review-reply-filter-label">评价时间范围</div>
          <DateTimeRangePicker
            :value="filters.dateRange"
            :show-time="true"
            @change="onDateRangeChange"
          />
        </Col>
      </Row>
    </Card>

    <!-- 评价卡片列表 -->
    <Spin :spinning="loading && reviews.length > 0">
      <Skeleton v-if="loading && reviews.length === 0" active :paragraph="{ rows: 8 }" />
      <EmptyState
        v-else-if="reviews.length === 0"
        description="暂无评价"
      />
      <div v-else class="review-reply-list">
        <Card
          v-for="review in reviews"
          :key="review.reviewId"
          class="review-reply-card"
          :bordered="true"
          size="small"
        >
          <!-- C1 头部 -->
          <div class="review-reply-card-header">
            <div class="review-reply-card-header-left">
              <Avatar :size="36">
                <UserOutlined />
              </Avatar>
              <span class="review-reply-card-user">{{ review.userMaskedName }}</span>
              <Rate :value="review.rating" disabled allow-half size="small" />
              <span class="review-reply-card-time">
                {{ formatDateTime(review.submittedAt) }}
              </span>
            </div>
            <StatusTag type="review" :status="review.status as ReviewStatus" />
          </div>

          <!-- C2 商品快照 -->
          <div
            class="review-reply-card-product"
            @click="goEditProduct(review.spuId)"
          >
            <AImage
              v-if="review.productImage"
              :src="review.productImage"
              :width="48"
              :height="48"
              class="review-reply-card-product-img"
            />
            <div v-else class="review-reply-card-product-img-placeholder">
              <UserOutlined />
            </div>
            <div class="review-reply-card-product-info">
              <div class="review-reply-card-product-name">
                {{ review.productName || '—' }}
              </div>
              <div class="review-reply-card-product-spec">
                {{ review.skuSpec || '—' }}
              </div>
            </div>
          </div>

          <!-- C3 正文 -->
          <div class="review-reply-card-body">
            <Typography.Paragraph
              :ellipsis="{ rows: 3, expandable: true, symbol: '展开' }"
            >
              {{ review.content }}
            </Typography.Paragraph>
            <div v-if="review.images.length > 0" class="review-reply-card-images">
              <AImage.PreviewGroup>
                <AImage
                  v-for="(img, idx) in review.images"
                  :key="idx"
                  :src="img"
                  :width="80"
                  :height="80"
                  class="review-reply-card-image-thumb"
                />
              </AImage.PreviewGroup>
            </div>
          </div>

          <!-- C4 回复区 -->
          <div class="review-reply-card-reply">
            <template v-if="review.sellerReplyContent">
              <div class="review-reply-card-reply-content">
                <div class="review-reply-card-reply-label">卖家回复：</div>
                <div class="review-reply-card-reply-text">
                  {{ review.sellerReplyContent }}
                </div>
                <div class="review-reply-card-reply-time">
                  {{ formatDateTime(review.sellerReplyAt) }}
                </div>
              </div>
              <Button type="link" size="small" @click="openReplyDrawer(review)">
                编辑回复
              </Button>
            </template>
            <template v-else>
              <Button type="primary" size="small" @click="openReplyDrawer(review)">
                回复
              </Button>
            </template>
          </div>
        </Card>
      </div>
    </Spin>

    <!-- 回复抽屉 480px -->
    <Drawer
      v-model:open="replyDrawerOpen"
      :title="isEditingReply ? '编辑回复' : '回复评价'"
      :width="480"
      :mask-closable="true"
      @close="closeReplyDrawer"
    >
      <template v-if="replyingReview">
        <!-- 评价摘要（只读） -->
        <div class="review-reply-drawer-summary">
          <div class="review-reply-drawer-summary-header">
            <Avatar :size="32">
              <UserOutlined />
            </Avatar>
            <span class="review-reply-drawer-summary-user">
              {{ replyingReview.userMaskedName }}
            </span>
            <Rate :value="replyingReview.rating" disabled allow-half size="small" />
          </div>
          <div class="review-reply-drawer-summary-product">
            {{ replyingReview.productName || '—' }}
          </div>
          <div class="review-reply-drawer-summary-content">
            {{ replyingReview.content }}
          </div>
        </div>

        <!-- 回复表单 -->
        <Form layout="vertical" class="review-reply-drawer-form">
          <FormItem label="回复内容">
            <Input
              v-model:value="replyContent"
              type="textarea"
              :rows="4"
              :maxlength="500"
              show-count
              placeholder="请输入回复内容（1-500 字）"
            />
            <div class="review-reply-drawer-char-count">
              {{ replyCharCount }} / 500
            </div>
          </FormItem>
        </Form>
      </template>

      <template #footer>
        <Space>
          <Button @click="closeReplyDrawer">取消</Button>
          <IdempotencyButton :loading="replying" @click="onSubmitReply">
            {{ isEditingReply ? '更新回复' : '提交回复' }}
          </IdempotencyButton>
        </Space>
      </template>
    </Drawer>
  </div>
</template>

<style scoped>
.review-reply-page {
  display: flex;
  flex-direction: column;
  gap: 16px;
}
.review-reply-breadcrumb {
  font-size: 14px;
}
.review-reply-stats-card {
  border-radius: 8px;
}
.review-reply-stat-box {
  display: flex;
  flex-direction: column;
  gap: 4px;
}
.review-reply-stat-label {
  font-size: 13px;
  color: #8c8c8c;
}
.review-reply-stat-value {
  font-size: 24px;
  font-weight: 500;
  color: #000000d9;
}
.review-reply-filter-card {
  border-radius: 8px;
}
.review-reply-filter-label {
  font-size: 12px;
  color: #8c8c8c;
  margin-bottom: 4px;
}
.review-reply-list {
  display: flex;
  flex-direction: column;
  gap: 12px;
}
.review-reply-card {
  border-radius: 8px;
}
.review-reply-card-header {
  display: flex;
  align-items: center;
  justify-content: space-between;
  margin-bottom: 12px;
}
.review-reply-card-header-left {
  display: flex;
  align-items: center;
  gap: 8px;
}
.review-reply-card-user {
  font-size: 14px;
  font-weight: 500;
  color: #000000d9;
}
.review-reply-card-time {
  font-size: 12px;
  color: #8c8c8c;
}
.review-reply-card-product {
  display: flex;
  align-items: center;
  gap: 12px;
  padding: 8px 12px;
  background: #fafafa;
  border-radius: 6px;
  margin-bottom: 12px;
  cursor: pointer;
  transition: background 0.2s;
}
.review-reply-card-product:hover {
  background: #f0f0f0;
}
.review-reply-card-product-img {
  border-radius: 4px;
  flex-shrink: 0;
}
.review-reply-card-product-img-placeholder {
  width: 48px;
  height: 48px;
  display: flex;
  align-items: center;
  justify-content: center;
  background: #f0f0f0;
  border-radius: 4px;
  color: #8c8c8c;
  flex-shrink: 0;
}
.review-reply-card-product-info {
  display: flex;
  flex-direction: column;
  gap: 2px;
  overflow: hidden;
}
.review-reply-card-product-name {
  font-size: 13px;
  color: #000000d9;
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
}
.review-reply-card-product-spec {
  font-size: 12px;
  color: #8c8c8c;
}
.review-reply-card-body {
  margin-bottom: 12px;
}
.review-reply-card-images {
  display: flex;
  gap: 8px;
  flex-wrap: wrap;
  margin-top: 8px;
}
.review-reply-card-image-thumb {
  border-radius: 4px;
  object-fit: cover;
}
.review-reply-card-reply {
  display: flex;
  align-items: flex-start;
  justify-content: space-between;
  padding-top: 12px;
  border-top: 1px solid #f0f0f0;
}
.review-reply-card-reply-content {
  flex: 1;
  display: flex;
  flex-direction: column;
  gap: 4px;
}
.review-reply-card-reply-label {
  font-size: 12px;
  color: #8c8c8c;
}
.review-reply-card-reply-text {
  font-size: 13px;
  color: #000000d9;
  line-height: 1.6;
}
.review-reply-card-reply-time {
  font-size: 12px;
  color: #8c8c8c;
}
.review-reply-drawer-summary {
  padding: 12px;
  background: #fafafa;
  border-radius: 6px;
  margin-bottom: 16px;
}
.review-reply-drawer-summary-header {
  display: flex;
  align-items: center;
  gap: 8px;
  margin-bottom: 8px;
}
.review-reply-drawer-summary-user {
  font-size: 13px;
  font-weight: 500;
}
.review-reply-drawer-summary-product {
  font-size: 12px;
  color: #8c8c8c;
  margin-bottom: 6px;
}
.review-reply-drawer-summary-content {
  font-size: 13px;
  color: #000000d9;
  line-height: 1.6;
}
.review-reply-drawer-form {
  margin-top: 8px;
}
.review-reply-drawer-char-count {
  font-size: 12px;
  color: #8c8c8c;
  text-align: right;
  margin-top: 4px;
}
</style>
```

- [ ] **Step 2: 类型检查 + lint**

Run (cwd: `web/seller`): `pnpm typecheck`
Expected: 0 errors

Run (cwd: `web/seller`): `pnpm lint`
Expected: 0 errors / 0 warnings

- [ ] **Step 3: 提交**

```bash
git add web/seller/src/modules/07-review/views/ReviewReply.vue
git commit -m "feat(seller): add ReviewReply page with card list and reply drawer"
```

---

## Task 8: 04-logistics + 07-review routes.ts + index.ts

**Files:**
- Create: `web/seller/src/modules/04-logistics/routes.ts`
- Create: `web/seller/src/modules/04-logistics/index.ts`
- Create: `web/seller/src/modules/07-review/routes.ts`
- Create: `web/seller/src/modules/07-review/index.ts`

- [ ] **Step 1: 实现 04-logistics routes.ts**

创建 `web/seller/src/modules/04-logistics/routes.ts`：

```typescript
import type { RouteRecordRaw } from 'vue-router'

const routes: RouteRecordRaw[] = [
  {
    path: '/logistics/freight-templates',
    name: 'logistics.freight-templates',
    component: () => import('./views/FreightTemplates.vue'),
    meta: {
      title: '运费模板',
      menuKey: 'logistics.freight-templates',
      roles: ['Seller'],
      permission: 'freight-template:list',
      menuGroup: '04-logistics',
    },
  },
  {
    path: '/logistics/companies',
    name: 'logistics.companies',
    component: () => import('./views/LogisticsCompanies.vue'),
    meta: {
      title: '物流公司',
      menuKey: 'logistics.companies',
      roles: ['Seller'],
      permission: 'logistics-company:list',
      menuGroup: '04-logistics',
    },
  },
]

export default routes
```

- [ ] **Step 2: 实现 04-logistics index.ts**

创建 `web/seller/src/modules/04-logistics/index.ts`：

```typescript
export { default } from './routes'
export { freightTemplateApi } from './api/freight-template.api'
export { logisticsCompanyApi } from './api/logistics-company.api'
```

- [ ] **Step 3: 实现 07-review routes.ts**

创建 `web/seller/src/modules/07-review/routes.ts`：

```typescript
import type { RouteRecordRaw } from 'vue-router'

const routes: RouteRecordRaw[] = [
  {
    path: '/reviews',
    name: 'review.reply',
    component: () => import('./views/ReviewReply.vue'),
    meta: {
      title: '评价回复',
      menuKey: 'review.reply',
      roles: ['Seller'],
      permission: 'review:list',
      menuGroup: '07-review',
    },
  },
]

export default routes
```

- [ ] **Step 4: 实现 07-review index.ts**

创建 `web/seller/src/modules/07-review/index.ts`：

```typescript
export { default } from './routes'
export { reviewApi } from './api/review.api'
```

- [ ] **Step 5: 类型检查**

Run (cwd: `web/seller`): `pnpm typecheck`
Expected: 0 errors（views 已全部存在，懒加载可解析）

- [ ] **Step 6: 提交**

```bash
git add web/seller/src/modules/04-logistics/routes.ts web/seller/src/modules/04-logistics/index.ts web/seller/src/modules/07-review/routes.ts web/seller/src/modules/07-review/index.ts
git commit -m "feat(seller): add 04-logistics and 07-review module routes and entry"
```

---

## Task 9: app/router.ts 注册 04-logistics + 07-review 路由

**Files:**
- Modify: `web/seller/src/app/router.ts`

- [ ] **Step 1: 添加 logistics + review 路由 import**

修改 `web/seller/src/app/router.ts`，在模块路由 import 区追加两行。

若批次 1 已执行，import 区当前应为：

```typescript
// 模块路由
import onboarding from '@/modules/01-onboarding/routes'
import dashboard from '@/modules/02-dashboard/routes'
import product from '@/modules/03-product-management/routes'
import order from '@/modules/05-order-fulfillment/routes'
import afterSales from '@/modules/06-after-sales/routes'
import account from '@/modules/08-account/routes'
```

在 `product` 之后追加 `logistics`，在 `afterSales` 之后追加 `review`：

```typescript
// 模块路由
import onboarding from '@/modules/01-onboarding/routes'
import dashboard from '@/modules/02-dashboard/routes'
import product from '@/modules/03-product-management/routes'
import logistics from '@/modules/04-logistics/routes'
import order from '@/modules/05-order-fulfillment/routes'
import afterSales from '@/modules/06-after-sales/routes'
import review from '@/modules/07-review/routes'
import account from '@/modules/08-account/routes'
```

> **注**：若批次 1 未执行（`onboarding` 缺失），请先执行批次 1。本步骤假定 `onboarding` 已存在。

- [ ] **Step 2: 将 logistics + review 路由注入 BasicLayout children**

在 `app/router.ts` 的 BasicLayout `children` 数组中，`...product` 之后追加 `...logistics`，`...afterSales` 之后追加 `...review`：

```typescript
    children: [
      { path: '', redirect: '/dashboard/overview' },
      ...onboarding,
      ...dashboard,
      ...product,
      ...logistics,
      ...order,
      ...afterSales,
      ...review,
      ...account,
    ],
```

> **注**：若批次 1 未执行（`...onboarding` 缺失），请先执行批次 1。本步骤假定 `...onboarding` 已存在。

- [ ] **Step 3: 类型检查 + lint**

Run (cwd: `web/seller`): `pnpm typecheck`
Expected: 0 errors

Run (cwd: `web/seller`): `pnpm lint`
Expected: 0 errors / 0 warnings

- [ ] **Step 4: 提交**

```bash
git add web/seller/src/app/router.ts
git commit -m "feat(seller): register 04-logistics and 07-review routes"
```

---

## Task 10: 全量验证 + 提交推送

**Files:**
- 无（仅验证与推送）

- [ ] **Step 1: Lint 全量检查**

Run (cwd: `web/seller`): `pnpm lint`
Expected: 0 errors / 0 warnings

- [ ] **Step 2: TypeCheck 全量检查**

Run (cwd: `web/seller`): `pnpm typecheck`
Expected: 0 errors

- [ ] **Step 3: 全量单元测试**

Run (cwd: `web/seller`): `pnpm test`
Expected: 全部通过（P0 既有用例 + 批次 1 用例 + 本批次新增 TemplateRuleEditor.spec.ts / StatusTag.spec.ts 新增 review 用例 / freight-template.api.spec.ts / logistics-company.api.spec.ts / review.api.spec.ts 全部 PASS）

- [ ] **Step 4: 生产构建**

Run (cwd: `web/seller`): `pnpm build`
Expected: 构建成功（`vue-tsc --noEmit` 通过 + `vite build` 产出 `dist`）

- [ ] **Step 5: 推送到远程仓库**

```bash
git push origin dev
```
Expected: 推送成功，远程 `origin/dev` 包含本批次全部 10 个 commit。

- [ ] **Step 6: 人工冒烟（可选，mock 模式）**

启动 `VITE_USE_MOCK=true pnpm dev`，逐页验证：
- `/logistics/freight-templates` 列出 2 个模板，新建模板后列表新增，Switch 启停切换生效，编辑规则抽屉可添加/删除行并保存（按重量/按件数列标题正确切换）
- `/logistics/companies` 列出 5 个物流公司，点击编码复制成功，10 分钟内刷新页面不发起请求（读缓存）
- `/reviews` 列出 10 条评价卡片（4 区结构正确），好评率统计正确，筛选评分/回复状态/商品名称（300ms 防抖）/时间范围生效，点「回复」打开 480px 抽屉，输入 1-500 字提交后卡片回复区显示，点「编辑回复」回填原内容，编辑后未保存关闭抽屉弹确认框

---

## Self-Review（计划自检）

**1. Spec 覆盖检查（对照批次 2 范围 10 项）**

| 批次 2 范围项 | 覆盖 Task |
|---|---|
| 1. shared 组件 TemplateRuleEditor.vue + spec | Task 1 |
| 2. 04-logistics: freight-template.dto.ts + logistics-company.dto.ts | Task 2 |
| 3. 04-logistics: freight-template.api.ts + spec + logistics-company.api.ts + spec | Task 2 |
| 4. 04-logistics: FreightTemplates.vue（列表+新建弹窗+编辑规则抽屉+启停Switch） | Task 5 |
| 5. 04-logistics: LogisticsCompanies.vue（只读+10分钟缓存+复制编码） | Task 6 |
| 6. 07-review: review.dto.ts + review.api.ts + spec | Task 3 |
| 7. 07-review: ReviewReply.vue（卡片4区+回复抽屉480px） | Task 7 |
| 8. Mock handlers: freight.ts + logistics.ts + review.ts + seed 扩展 | Task 4 |
| 9. 路由更新: app/router.ts 注册 04-logistics + 07-review | Task 8（routes/index）+ Task 9（router.ts） |
| 10. 全量验证 + 提交推送 | Task 10 |

补充项：StatusTag `review` 类型映射扩展（Task 1 Step 7-10），满足 ReviewReply.vue 中 `StatusTag(type="review")` 需求。

无遗漏。

**2. 占位符扫描**

- 全文未出现 `TODO`/`FIXME`/`...省略`/`Similar to Task` 等占位符。
- 所有 Vue SFC 均含完整 `<script setup lang="ts">` + `<template>` + `<style scoped>`。
- 所有 API/mock/组件代码为可直接编译运行的完整实现。
- Task 4 中对批次 1 的依赖有明确前置说明（"若批次 1 未执行"），非占位符。

**3. 类型一致性检查**

- `FreightTemplateDto` / `CreateFreightTemplateDto` / `UpdateFreightRulesDto` / `RegionRuleDto` / `PricingType` 在 `freight-template.dto.ts`（Task 2）定义，被 `freight-template.api.ts`、`freight.ts` mock handler、`FreightTemplates.vue`、`TemplateRuleEditor.vue` 一致引用。
- `LogisticsCompanyDto` 在 `logistics-company.dto.ts`（Task 2）定义，被 `logistics-company.api.ts`、`logistics.ts` mock handler、`LogisticsCompanies.vue` 一致引用。
- `ReviewDto` / `ReviewListResultDto` / `ReviewQueryParams` / `SellerReplyDto` / `ReviewStatus` 在 `review.dto.ts`（Task 3）定义，被 `review.api.ts`、`review.ts` mock handler、`ReviewReply.vue` 一致引用。
- `freightTemplateApi` 方法名（`listMine`/`create`/`updateRules`/`enable`/`disable`）在 API、测试、mock handler、页面中一致。
- `reviewApi` 方法名（`list`/`get`/`reply`）在 API、测试、mock handler、页面中一致。
- `logisticsCompanyApi` 方法名（`listEnabled`）在 API、测试、mock handler、页面中一致。
- `StatusTag` 的 `review` 映射（`Approved`/`Hidden`）与 `ReviewStatus` 类型一致。
- 乐观锁 `version` 字段在 `FreightTemplateDto.version`、`UpdateFreightRulesDto.version`、`FreightTemplates.vue` 的 `editingVersion`、mock PUT 校验中一致。
- mock 路径与 API 路径一致（baseURL=/api，故 mock 拦截 `/seller/freight-templates/...`、`/seller/logistics-companies`、`/seller/reviews/...`）。

**4. 已知限制与前置依赖**

- **批次 1 前置依赖**：本批次使用批次 1 引入的 `http` 别名与 shop mock handler 装配。若批次 1 未执行，Task 2 Step 1 与 Task 4 会失败。请先执行批次 1 计划。
- `TemplateRuleEditor.vue` 的 Fixed 计费类型由父组件 `FreightTemplates.vue` 通过 `v-if="showRuleEditor"` 隐藏编辑器，组件本身不处理 Fixed 类型。
- `LogisticsCompanies.vue` 的 10 分钟缓存使用 `localStorage`，若用户手动清除浏览器存储会重新请求。
- `ReviewReply.vue` 的图片预览使用 `AImage.PreviewGroup`，mock 种子中图片字段为空字符串或文件名（非真实 URL），实际预览需后端返回真实图片 URL。

---

## 执行交接

计划已完成并保存至 `docs/superpowers/plans/2026-07-30-seller-admin-p1-batch2-logistics-review.md`。两种执行方式可选：

**1. Subagent 驱动（推荐）** — 每个 Task 派发独立 subagent，任务间审查，迭代快速。

**2. 内联执行** — 在当前会话使用 executing-plans 批量执行，设检查点审查。

选择哪种方式？
