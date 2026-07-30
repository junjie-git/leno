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
