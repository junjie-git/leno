import { describe, expect, it, beforeEach, afterEach } from 'vitest'
import MockAdapter from 'axios-mock-adapter'
import { client } from '@/shared/http'
import { shopApi } from './shop.api'
import type { QualificationDto, ShopDto } from '../types/shop.dto'

/**
 * 店铺审核与治理 API 单测（axios-mock-adapter 挂载到真实 client 实例）
 *
 * 覆盖点：
 * - list 调用 GET /admin/shops，分页 + keyword/applicant/status/category 组合传参并解包 data
 * - get / getQualifications 调用对应 GET 端点并解包业务负载
 * - approve / resume 自动携带 Idempotency-Key
 * - reject / suspend / close 传 ActionReasonDto 且携带 Idempotency-Key
 * - approveQualification / rejectQualification 命中资质子资源端点
 */
describe('04-seller-ops shopApi', () => {
  let mock: MockAdapter

  const fakeQualification: QualificationDto = {
    id: 'qual-0001',
    type: '营业执照',
    fileName: 'business-license.jpg',
    fileUrl: 'https://cdn.leno.com/shops/shop-0001/business-license.jpg',
    status: 'PendingReview',
    rejectReason: undefined,
    submittedAt: '2026-08-10T02:00:00.000Z',
  }

  const fakeShop: ShopDto = {
    id: 'shop-0001',
    name: '南极人官方旗舰店',
    ownerName: '张伟',
    sellerAccount: 'zhangwei',
    contactPhone: '138-6688-6688',
    mainCategory: '服饰鞋包',
    productCount: 156,
    orderCount: 2368,
    rating: 4.8,
    gmv: 856420,
    status: 'PendingReview',
    submittedAt: '2026-08-12T09:23:01.000Z',
    createdAt: '2026-08-12T09:23:01.000Z',
    lastGovernedAt: undefined,
    qualifications: [fakeQualification],
  }

  function ok<T>(data: T): [number, { code: number; message: string; data: T }] {
    return [200, { code: 200, message: 'OK', data }]
  }

  beforeEach(() => {
    mock = new MockAdapter(client)
    localStorage.clear()
  })

  afterEach(() => {
    mock.restore()
  })

  it('list 调用 GET /admin/shops 组合查询参数并解包 data', async () => {
    let capturedParams: Record<string, unknown> = {}
    mock.onGet('/admin/shops').reply((config) => {
      capturedParams = (config.params ?? {}) as Record<string, unknown>
      return ok({ items: [fakeShop], total: 1, page: 1, pageSize: 20 })
    })

    const { data } = await shopApi.list({
      page: 1,
      pageSize: 20,
      keyword: '南极人',
      applicant: '张伟',
      status: 'PendingReview',
      category: '服饰鞋包',
    })

    expect(data.items[0].id).toBe('shop-0001')
    expect(data.total).toBe(1)
    expect(mock.history.get.length).toBe(1)
    expect(mock.history.get[0].url).toBe('/admin/shops')
    expect(capturedParams).toMatchObject({
      page: 1,
      pageSize: 20,
      keyword: '南极人',
      applicant: '张伟',
      status: 'PendingReview',
      category: '服饰鞋包',
    })
  })

  it('list 仅传分页参数时不携带空筛选', async () => {
    let capturedParams: Record<string, unknown> = {}
    mock.onGet('/admin/shops').reply((config) => {
      capturedParams = (config.params ?? {}) as Record<string, unknown>
      return ok({ items: [], total: 0, page: 1, pageSize: 20 })
    })

    await shopApi.list({ page: 2, pageSize: 10 })

    expect(capturedParams).toEqual({ page: 2, pageSize: 10 })
  })

  it('get 调用 GET /admin/shops/{id} 并解包店铺详情', async () => {
    mock.onGet('/admin/shops/shop-0001').reply(() => ok(fakeShop))

    const { data } = await shopApi.get('shop-0001')

    expect(data.name).toBe('南极人官方旗舰店')
    expect(data.qualifications?.[0].id).toBe('qual-0001')
    expect(mock.history.get[0].url).toBe('/admin/shops/shop-0001')
  })

  it('approve 调用 POST /admin/shops/{id}/approve 并携带 Idempotency-Key', async () => {
    mock.onPost('/admin/shops/shop-0001/approve').reply(() => ok(null))

    await shopApi.approve('shop-0001')

    expect(mock.history.post.length).toBe(1)
    const req = mock.history.post[0]
    expect(req.url).toBe('/admin/shops/shop-0001/approve')
    expect(String(req.headers?.['Idempotency-Key'] ?? '')).not.toBe('')
  })

  it('reject 调用 POST /admin/shops/{id}/reject 并传 ActionReasonDto', async () => {
    mock.onPost('/admin/shops/shop-0001/reject').reply(() => ok(null))

    await shopApi.reject('shop-0001', { reason: '缺少品牌授权书原件扫描件' })

    const req = mock.history.post[0]
    expect(req.url).toBe('/admin/shops/shop-0001/reject')
    expect(JSON.parse(req.data as string)).toEqual({ reason: '缺少品牌授权书原件扫描件' })
    expect(String(req.headers?.['Idempotency-Key'] ?? '')).not.toBe('')
  })

  it('suspend 调用 POST /admin/shops/{id}/suspend 并传暂停原因', async () => {
    mock.onPost('/admin/shops/shop-0001/suspend').reply(() => ok(null))

    await shopApi.suspend('shop-0001', { reason: '虚假宣传投诉集中，暂停营业整改' })

    const req = mock.history.post[0]
    expect(req.url).toBe('/admin/shops/shop-0001/suspend')
    expect(JSON.parse(req.data as string)).toEqual({ reason: '虚假宣传投诉集中，暂停营业整改' })
    expect(String(req.headers?.['Idempotency-Key'] ?? '')).not.toBe('')
  })

  it('resume 调用 POST /admin/shops/{id}/resume 并携带 Idempotency-Key', async () => {
    mock.onPost('/admin/shops/shop-0001/resume').reply(() => ok(null))

    await shopApi.resume('shop-0001')

    const req = mock.history.post[0]
    expect(req.url).toBe('/admin/shops/shop-0001/resume')
    expect(String(req.headers?.['Idempotency-Key'] ?? '')).not.toBe('')
  })

  it('close 调用 POST /admin/shops/{id}/close 并传关闭原因', async () => {
    mock.onPost('/admin/shops/shop-0001/close').reply(() => ok(null))

    await shopApi.close('shop-0001', { reason: '资质失效且未按期整改，予以关闭' })

    const req = mock.history.post[0]
    expect(req.url).toBe('/admin/shops/shop-0001/close')
    expect(JSON.parse(req.data as string)).toEqual({ reason: '资质失效且未按期整改，予以关闭' })
    expect(String(req.headers?.['Idempotency-Key'] ?? '')).not.toBe('')
  })

  it('getQualifications 调用 GET /admin/shops/{id}/qualifications 并解包资质数组', async () => {
    mock.onGet('/admin/shops/shop-0001/qualifications').reply(() => ok([fakeQualification]))

    const { data } = await shopApi.getQualifications('shop-0001')

    expect(data).toHaveLength(1)
    expect(data[0].type).toBe('营业执照')
    expect(data[0].status).toBe('PendingReview')
    expect(mock.history.get[0].url).toBe('/admin/shops/shop-0001/qualifications')
  })

  it('approveQualification 命中资质子资源端点并携带 Idempotency-Key', async () => {
    mock.onPost('/admin/shops/shop-0001/qualifications/qual-0001/approve').reply(() => ok(null))

    await shopApi.approveQualification('shop-0001', 'qual-0001')

    const req = mock.history.post[0]
    expect(req.url).toBe('/admin/shops/shop-0001/qualifications/qual-0001/approve')
    expect(String(req.headers?.['Idempotency-Key'] ?? '')).not.toBe('')
  })

  it('rejectQualification 命中资质子资源端点并传驳回原因', async () => {
    mock.onPost('/admin/shops/shop-0001/qualifications/qual-0001/reject').reply(() => ok(null))

    await shopApi.rejectQualification('shop-0001', 'qual-0001', {
      reason: '营业执照影像模糊无法辨认',
    })

    const req = mock.history.post[0]
    expect(req.url).toBe('/admin/shops/shop-0001/qualifications/qual-0001/reject')
    expect(JSON.parse(req.data as string)).toEqual({ reason: '营业执照影像模糊无法辨认' })
    expect(String(req.headers?.['Idempotency-Key'] ?? '')).not.toBe('')
  })

  it('list 业务错误（code !== 200）抛出 BusinessError', async () => {
    mock.onGet('/admin/shops').reply(200, { code: 40301, message: '无店铺查询权限', data: null })

    await expect(shopApi.list({ page: 1, pageSize: 20 })).rejects.toThrowError('无店铺查询权限')
  })
})
