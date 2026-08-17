import { describe, expect, it, beforeEach, afterEach } from 'vitest'
import MockAdapter from 'axios-mock-adapter'
import { client } from '@/shared/http'
import { ConcurrencyError } from '@/shared/http/errors'
import { seckillApi } from './seckill.api'
import type { CreateSeckillActivityDto, SeckillActivityDto } from '../types/seckill.dto'

/**
 * seckillApi 单测（axios-mock-adapter 挂载到真实 client 实例）
 *
 * 覆盖点：
 * - list 调用 GET /seckill/activities 并透传 status + 分页参数
 * - create 调用 POST /seckill/activities 提交 SKU 配置数组并携带 Idempotency-Key
 * - activate/close 调用状态端点并携带 Idempotency-Key
 * - 409 重复状态流转转换为 ConcurrencyError
 */
describe('03-promotion-ops seckillApi', () => {
  let mock: MockAdapter

  const fakeSeckill: SeckillActivityDto = {
    id: 'seckill-0001',
    name: '12点整点秒杀',
    status: 'Pending',
    startTime: '2026-08-20T12:00:00Z',
    endTime: '2026-08-20T14:00:00Z',
    items: [
      {
        skuId: 'sku-1001',
        skuName: 'iPhone 15 128G 黑色',
        seckillPrice: 3999,
        originalPrice: 4999,
        stock: 200,
        remainingStock: 200,
        perUserLimit: 1,
        redisInitialized: false,
      },
      {
        skuId: 'sku-1002',
        skuName: '蓝牙耳机 标准版',
        seckillPrice: 199,
        originalPrice: 299,
        stock: 500,
        remainingStock: 500,
        perUserLimit: 2,
        redisInitialized: false,
      },
    ],
    createdAt: '2026-08-15T09:00:00Z',
  }

  const createBody: CreateSeckillActivityDto = {
    name: '12点整点秒杀',
    startTime: '2026-08-20T12:00:00Z',
    endTime: '2026-08-20T14:00:00Z',
    items: [
      {
        skuId: 'sku-1001',
        skuName: 'iPhone 15 128G 黑色',
        originalPrice: 4999,
        seckillPrice: 3999,
        stock: 200,
        perUserLimit: 1,
      },
    ],
  }

  beforeEach(() => {
    mock = new MockAdapter(client)
    localStorage.clear()
  })

  afterEach(() => {
    mock.restore()
  })

  it('list 调用 GET /seckill/activities 并透传状态与分页参数', async () => {
    mock
      .onGet('/seckill/activities')
      .reply(200, { code: 200, message: 'OK', data: { items: [fakeSeckill], total: 1, page: 1, pageSize: 20 } })

    const result = await seckillApi.list({ page: 1, pageSize: 20, status: 'Pending' })

    expect(result.items).toHaveLength(1)
    expect(result.items[0].items).toHaveLength(2)
    expect(result.items[0].items[0].skuId).toBe('sku-1001')
    expect(result.total).toBe(1)

    expect(mock.history.get.length).toBe(1)
    const req = mock.history.get[0]
    expect(req.url).toBe('/seckill/activities')
    expect(req.params).toEqual({ page: 1, pageSize: 20, status: 'Pending' })
  })

  it('create 调用 POST /seckill/activities 提交 SKU 配置数组并携带 Idempotency-Key', async () => {
    mock.onPost('/seckill/activities').reply(200, { code: 200, message: 'OK', data: fakeSeckill })

    const result = await seckillApi.create(createBody)

    expect(result.id).toBe('seckill-0001')
    expect(result.status).toBe('Pending')

    expect(mock.history.post.length).toBe(1)
    const req = mock.history.post[0]
    expect(req.url).toBe('/seckill/activities')
    expect(JSON.parse(req.data as string)).toEqual(createBody)
    const headers = req.headers ?? {}
    expect(String(headers['Idempotency-Key'] ?? '')).not.toBe('')
  })

  it('update 调用 PUT /seckill/activities/{activityId} 并携带 Idempotency-Key', async () => {
    mock
      .onPut('/seckill/activities/seckill-0001')
      .reply(200, { code: 200, message: 'OK', data: fakeSeckill })

    const result = await seckillApi.update('seckill-0001', createBody)

    expect(result.id).toBe('seckill-0001')
    expect(mock.history.put.length).toBe(1)
    const req = mock.history.put[0]
    expect(req.url).toBe('/seckill/activities/seckill-0001')
    expect(JSON.parse(req.data as string)).toEqual(createBody)
    const headers = req.headers ?? {}
    expect(String(headers['Idempotency-Key'] ?? '')).not.toBe('')
  })

  it.each([
    {
      method: 'activate',
      url: '/seckill/activities/seckill-0001/activate',
      call: () => seckillApi.activate('seckill-0001'),
    },
    {
      method: 'close',
      url: '/seckill/activities/seckill-0001/close',
      call: () => seckillApi.close('seckill-0001'),
    },
  ] as const)('$method 调用 POST $url 并携带 Idempotency-Key', async ({ url, call }) => {
    mock.onPost(url).reply(200, { code: 200, message: 'OK', data: null })

    await call()

    expect(mock.history.post.length).toBe(1)
    const req = mock.history.post[0]
    expect(req.url).toBe(url)
    const headers = req.headers ?? {}
    expect(String(headers['Idempotency-Key'] ?? '')).not.toBe('')
  })

  it('重复状态流转（HTTP 409）转换为 ConcurrencyError 并透传 message', async () => {
    mock
      .onPost('/seckill/activities/seckill-0001/activate')
      .reply(409, { code: 409, message: '活动状态已变更', data: null })

    await expect(seckillApi.activate('seckill-0001')).rejects.toBeInstanceOf(ConcurrencyError)
    await expect(seckillApi.activate('seckill-0001')).rejects.toThrowError('活动状态已变更')
  })

  it('业务错误（code !== 200）抛 BusinessError 并透传后端 message', async () => {
    mock
      .onPost('/seckill/activities')
      .reply(200, { code: 40030, message: '秒杀价不得高于原价', data: null })

    await expect(seckillApi.create(createBody)).rejects.toThrowError('秒杀价不得高于原价')
  })
})
