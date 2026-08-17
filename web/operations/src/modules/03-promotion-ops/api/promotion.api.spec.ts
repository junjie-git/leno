import { describe, expect, it, beforeEach, afterEach } from 'vitest'
import MockAdapter from 'axios-mock-adapter'
import { client } from '@/shared/http'
import { ConcurrencyError } from '@/shared/http/errors'
import { promotionApi } from './promotion.api'
import type {
  PromotionActivityDto,
  SavePromotionActivityDto,
} from '../types/promotion.dto'

/**
 * promotionApi 单测（axios-mock-adapter 挂载到真实 client 实例）
 *
 * 覆盖点：
 * - list 调用 GET /promotions 并透传 name/status/startTime/endTime + 分页参数
 * - get 调用 GET /promotions/{activityId} 并解包
 * - create/update 调用 POST/PUT 并自动携带 Idempotency-Key
 * - activate/pause/close 调用状态机端点并携带 Idempotency-Key
 * - 409 冲突响应转换为 ConcurrencyError（活动状态已被他人变更）
 */
describe('03-promotion-ops promotionApi', () => {
  let mock: MockAdapter

  const fakePromotion: PromotionActivityDto = {
    id: 'promo-0001',
    name: '双11大促满减',
    type: 'FullReduction',
    status: 'Pending',
    startTime: '2026-11-11T00:00:00Z',
    endTime: '2026-11-11T23:59:59Z',
    rules: [{ threshold: 300, discountValue: 50 }],
    scope: 'All',
    scopeIds: [],
    createdBy: 'operator',
    createdAt: '2026-08-01T10:00:00Z',
  }

  const saveBody: SavePromotionActivityDto = {
    name: '双11大促满减',
    type: 'FullReduction',
    startTime: '2026-11-11T00:00:00Z',
    endTime: '2026-11-11T23:59:59Z',
    rules: [{ threshold: 300, discountValue: 50 }],
    scope: 'All',
    scopeIds: [],
  }

  beforeEach(() => {
    mock = new MockAdapter(client)
    localStorage.clear()
  })

  afterEach(() => {
    mock.restore()
  })

  it('list 调用 GET /promotions 并透传筛选与分页参数', async () => {
    mock
      .onGet('/promotions')
      .reply(200, { code: 200, message: 'OK', data: { items: [fakePromotion], total: 1, page: 1, pageSize: 20 } })

    const result = await promotionApi.list({
      page: 1,
      pageSize: 20,
      name: '双11',
      status: 'Pending',
      startTime: '2026-11-01T00:00:00Z',
      endTime: '2026-11-30T23:59:59Z',
    })

    expect(result.items).toHaveLength(1)
    expect(result.items[0].name).toBe('双11大促满减')
    expect(result.total).toBe(1)

    expect(mock.history.get.length).toBe(1)
    const req = mock.history.get[0]
    expect(req.url).toBe('/promotions')
    expect(req.params).toEqual({
      page: 1,
      pageSize: 20,
      name: '双11',
      status: 'Pending',
      startTime: '2026-11-01T00:00:00Z',
      endTime: '2026-11-30T23:59:59Z',
    })
  })

  it('get 调用 GET /promotions/{activityId} 并解包 data', async () => {
    mock.onGet('/promotions/promo-0001').reply(200, { code: 200, message: 'OK', data: fakePromotion })

    const result = await promotionApi.get('promo-0001')

    expect(result.id).toBe('promo-0001')
    expect(result.rules).toEqual([{ threshold: 300, discountValue: 50 }])
    expect(mock.history.get.length).toBe(1)
    expect(mock.history.get[0].url).toBe('/promotions/promo-0001')
  })

  it('create 调用 POST /promotions 并自动携带 Idempotency-Key', async () => {
    mock.onPost('/promotions').reply(200, { code: 200, message: 'OK', data: fakePromotion })

    const result = await promotionApi.create(saveBody)

    expect(result.id).toBe('promo-0001')
    expect(mock.history.post.length).toBe(1)
    const req = mock.history.post[0]
    expect(req.url).toBe('/promotions')
    expect(JSON.parse(req.data as string)).toEqual(saveBody)
    const headers = req.headers ?? {}
    expect(String(headers['Idempotency-Key'] ?? '')).not.toBe('')
  })

  it('update 调用 PUT /promotions/{activityId} 并自动携带 Idempotency-Key', async () => {
    mock.onPut('/promotions/promo-0001').reply(200, { code: 200, message: 'OK', data: fakePromotion })

    const result = await promotionApi.update('promo-0001', saveBody)

    expect(result.id).toBe('promo-0001')
    expect(mock.history.put.length).toBe(1)
    const req = mock.history.put[0]
    expect(req.url).toBe('/promotions/promo-0001')
    expect(JSON.parse(req.data as string)).toEqual(saveBody)
    const headers = req.headers ?? {}
    expect(String(headers['Idempotency-Key'] ?? '')).not.toBe('')
  })

  it.each([
    { method: 'activate', url: '/promotions/promo-0001/activate', call: () => promotionApi.activate('promo-0001') },
    { method: 'pause', url: '/promotions/promo-0001/pause', call: () => promotionApi.pause('promo-0001') },
    { method: 'close', url: '/promotions/promo-0001/close', call: () => promotionApi.close('promo-0001') },
  ] as const)('$method 调用 POST $url 并携带 Idempotency-Key', async ({ url, call }) => {
    mock.onPost(url).reply(200, { code: 200, message: 'OK', data: null })

    await call()

    expect(mock.history.post.length).toBe(1)
    const req = mock.history.post[0]
    expect(req.url).toBe(url)
    const headers = req.headers ?? {}
    expect(String(headers['Idempotency-Key'] ?? '')).not.toBe('')
  })

  it('状态冲突（HTTP 409）转换为 ConcurrencyError', async () => {
    mock
      .onPost('/promotions/promo-0001/activate')
      .reply(409, { code: 409, message: '活动状态已变更', data: null, currentVersion: 3 })

    await expect(promotionApi.activate('promo-0001')).rejects.toBeInstanceOf(ConcurrencyError)
    await expect(promotionApi.activate('promo-0001')).rejects.toThrowError('活动状态已变更')
  })

  it('业务错误（code !== 200）抛 BusinessError 并透传后端 message', async () => {
    mock.onGet('/promotions').reply(200, { code: 40010, message: '活动时间与现有活动重叠', data: null })

    await expect(promotionApi.list({ page: 1, pageSize: 20 })).rejects.toThrowError('活动时间与现有活动重叠')
  })
})
