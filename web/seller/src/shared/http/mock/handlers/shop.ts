/* eslint-disable @typescript-eslint/no-explicit-any */
import type MockAdapter from 'axios-mock-adapter'
import { loadSeedData, saveSeedData, nextId } from '../data/seed'

/**
 * 店铺 handler 注册
 *
 * 端点（baseURL=/api，故拦截 /shops/...）：
 * - POST   /shops/application          提交入驻申请
 * - GET    /shops/me                   查询当前卖家店铺资料
 * - PUT    /shops/me                   更新店铺资料（乐观锁 version）
 * - GET    /shops/me/qualifications    资质列表
 * - POST   /shops/me/qualifications    上传资质（multipart/form-data）
 *
 * 店铺对象为双形态：同时含 id/name/version/customerService（P1）与
 * shopId/shopName/qualificationsStatus（P0 shop.store），兼容两个消费方。
 */
export function registerShopHandlers(mock: MockAdapter): void {
  // 提交入驻申请
  mock.onPost('/shops/application').reply((config) => {
    const seed = loadSeedData()
    const body = JSON.parse(config.data || '{}')
    if (!body.name || !body.mainCategory || !body.contactPhone) {
      return [200, { code: 40001, message: '店铺名称、主营类目、联系电话必填', data: null }]
    }
    const now = new Date().toISOString()
    const shop = seed.shop as any
    shop.name = body.name
    shop.shopName = body.name
    shop.mainCategory = body.mainCategory
    if (body.description) shop.description = body.description
    shop.status = 'Pending'
    shop.customerService = {
      phone: body.contactPhone,
      email: body.contactEmail,
    }
    shop.version = 1
    shop.createdAt = now
    shop.updatedAt = now
    saveSeedData(seed)
    return [200, { code: 200, message: 'OK', data: shop }]
  })

  // 查询当前卖家店铺资料
  mock.onGet('/shops/me').reply(() => {
    const seed = loadSeedData()
    return [200, { code: 200, message: 'OK', data: seed.shop }]
  })

  // 更新店铺资料（乐观锁）
  mock.onPut('/shops/me').reply((config) => {
    const seed = loadSeedData()
    const shop = seed.shop as any
    const body = JSON.parse(config.data || '{}')
    if (typeof body.version === 'number' && body.version !== shop.version) {
      return [
        409,
        {
          code: 409,
          message: '店铺资料已被他人修改',
          currentVersion: shop.version,
          data: null,
        },
      ]
    }
    if (body.name) {
      shop.name = body.name
      shop.shopName = body.name
    }
    if (body.logo !== undefined) shop.logo = body.logo
    if (body.description !== undefined) shop.description = body.description
    if (body.customerService) shop.customerService = body.customerService
    shop.version = (shop.version ?? 1) + 1
    shop.updatedAt = new Date().toISOString()
    saveSeedData(seed)
    return [200, { code: 200, message: 'OK', data: shop }]
  })

  // 资质列表
  mock.onGet('/shops/me/qualifications').reply(() => {
    const seed = loadSeedData()
    return [200, { code: 200, message: 'OK', data: seed.qualifications }]
  })

  // 上传资质（multipart/form-data）
  mock.onPost('/shops/me/qualifications').reply((config) => {
    const seed = loadSeedData()
    const data = config.data
    let type = 'Other'
    let fileName = 'unknown'
    if (typeof FormData !== 'undefined' && data instanceof FormData) {
      type = String(data.get('type') || 'Other')
      const file = data.get('file') as File | null
      fileName = file?.name ?? 'unknown'
    }
    const now = new Date().toISOString()
    const qual = {
      id: nextId(seed, 'qual'),
      type,
      fileName,
      fileUrl: '',
      status: 'Pending',
      submittedAt: now,
    }
    ;(seed.qualifications as any[]).push(qual)
    saveSeedData(seed)
    return [200, { code: 200, message: 'OK', data: qual }]
  })
}
