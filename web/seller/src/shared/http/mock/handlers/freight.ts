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
