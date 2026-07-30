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
    await regionNameInput!.vm.$emit('update:value', '江浙沪')
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
    // 列顺序：地区编码(Input) / 地区名称(Input) / 首单位 / 首价 / 续单位 / 续价
    // InputNumber 顺序：firstUnit(0) / firstPrice(1) / nextUnit(2) / nextPrice(3)
    // 首价是第 2 个 InputNumber（index 1）
    const firstPriceInput = inputNumbers[1]
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
