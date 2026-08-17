import { describe, expect, it, beforeEach, afterEach } from 'vitest'
import MockAdapter from 'axios-mock-adapter'
import { client } from '@/shared/http'
import { couponApi } from './coupon.api'
import type { CouponDto, SaveCouponDto } from '../types/coupon.dto'

/**
 * couponApi 单测（axios-mock-adapter 挂载到真实 client 实例）
 *
 * 覆盖点：
 * - list 调用 GET /coupons 并透传 status/keyword/type + 分页参数
 * - create/update 调用 POST/PUT 并自动携带 Idempotency-Key
 * - publish/stop 调用券模板状态端点并携带 Idempotency-Key
 * - issue 调用 POST /coupons/{couponId}/issue?quantity=n（quantity 走 query、body 为空对象）
 */
describe('03-promotion-ops couponApi', () => {
  let mock: MockAdapter

  const fakeCoupon: CouponDto = {
    id: 'coupon-0001',
    name: '新人立减券',
    type: 'FullReduction',
    faceValue: 10,
    threshold: 50,
    validityType: 'AfterReceiveDays',
    validDays: 30,
    totalQuantity: 5000,
    issuedQuantity: 1200,
    remainingQuantity: 3800,
    perUserLimit: 1,
    status: 'Published',
    createdAt: '2026-08-01T10:00:00Z',
  }

  const saveBody: SaveCouponDto = {
    name: '新人立减券',
    type: 'FullReduction',
    faceValue: 10,
    threshold: 50,
    validityType: 'AfterReceiveDays',
    validDays: 30,
    totalQuantity: 5000,
    perUserLimit: 1,
  }

  beforeEach(() => {
    mock = new MockAdapter(client)
    localStorage.clear()
  })

  afterEach(() => {
    mock.restore()
  })

  it('list 调用 GET /coupons 并透传筛选与分页参数', async () => {
    mock
      .onGet('/coupons')
      .reply(200, { code: 200, message: 'OK', data: { items: [fakeCoupon], total: 1, page: 1, pageSize: 20 } })

    const result = await couponApi.list({
      page: 1,
      pageSize: 20,
      status: 'Published',
      keyword: '新人',
      type: 'FullReduction',
    })

    expect(result.items).toHaveLength(1)
    expect(result.items[0].name).toBe('新人立减券')
    expect(result.total).toBe(1)

    expect(mock.history.get.length).toBe(1)
    const req = mock.history.get[0]
    expect(req.url).toBe('/coupons')
    expect(req.params).toEqual({
      page: 1,
      pageSize: 20,
      status: 'Published',
      keyword: '新人',
      type: 'FullReduction',
    })
  })

  it('create 调用 POST /coupons 并自动携带 Idempotency-Key', async () => {
    mock.onPost('/coupons').reply(200, { code: 200, message: 'OK', data: fakeCoupon })

    const result = await couponApi.create(saveBody)

    expect(result.id).toBe('coupon-0001')
    expect(mock.history.post.length).toBe(1)
    const req = mock.history.post[0]
    expect(req.url).toBe('/coupons')
    expect(JSON.parse(req.data as string)).toEqual(saveBody)
    const headers = req.headers ?? {}
    expect(String(headers['Idempotency-Key'] ?? '')).not.toBe('')
  })

  it('update 调用 PUT /coupons/{couponId} 并自动携带 Idempotency-Key', async () => {
    mock.onPut('/coupons/coupon-0001').reply(200, { code: 200, message: 'OK', data: fakeCoupon })

    const result = await couponApi.update('coupon-0001', saveBody)

    expect(result.id).toBe('coupon-0001')
    expect(mock.history.put.length).toBe(1)
    const req = mock.history.put[0]
    expect(req.url).toBe('/coupons/coupon-0001')
    expect(JSON.parse(req.data as string)).toEqual(saveBody)
    const headers = req.headers ?? {}
    expect(String(headers['Idempotency-Key'] ?? '')).not.toBe('')
  })

  it.each([
    { method: 'publish', url: '/coupons/coupon-0001/publish', call: () => couponApi.publish('coupon-0001') },
    { method: 'stop', url: '/coupons/coupon-0001/stop', call: () => couponApi.stop('coupon-0001') },
  ] as const)('$method 调用 POST $url 并携带 Idempotency-Key', async ({ url, call }) => {
    mock.onPost(url).reply(200, { code: 200, message: 'OK', data: null })

    await call()

    expect(mock.history.post.length).toBe(1)
    const req = mock.history.post[0]
    expect(req.url).toBe(url)
    const headers = req.headers ?? {}
    expect(String(headers['Idempotency-Key'] ?? '')).not.toBe('')
  })

  it('issue 调用 POST /coupons/{couponId}/issue?quantity=n 且 body 为空对象', async () => {
    const issued: CouponDto = { ...fakeCoupon, issuedQuantity: 1210, remainingQuantity: 3790 }
    mock.onPost('/coupons/coupon-0001/issue').reply(200, { code: 200, message: 'OK', data: issued })

    const result = await couponApi.issue('coupon-0001', 10)

    expect(result.issuedQuantity).toBe(1210)
    expect(result.remainingQuantity).toBe(3790)

    expect(mock.history.post.length).toBe(1)
    const req = mock.history.post[0]
    expect(req.url).toBe('/coupons/coupon-0001/issue')
    // quantity 走 query，body 为空对象
    expect(req.params).toEqual({ quantity: 10 })
    expect(JSON.parse(req.data as string)).toEqual({})
    const headers = req.headers ?? {}
    expect(String(headers['Idempotency-Key'] ?? '')).not.toBe('')
  })

  it('发放超量业务错误（code !== 200）抛 BusinessError', async () => {
    mock
      .onPost('/coupons/coupon-0001/issue')
      .reply(200, { code: 40020, message: '发放数量超过剩余库存', data: null })

    await expect(couponApi.issue('coupon-0001', 999999)).rejects.toThrowError('发放数量超过剩余库存')
  })
})
