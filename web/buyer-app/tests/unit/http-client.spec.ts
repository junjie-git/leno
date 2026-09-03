import MockAdapter from 'axios-mock-adapter'
import { afterEach, describe, expect, it } from 'vitest'
import { client } from '@/shared/http'
import {
  BusinessError,
  ForbiddenError,
  NetworkError,
  NotFoundError,
  RateLimitedError,
  ServerError,
  UnauthorizedError,
} from '@/shared/http/errors'

/**
 * http 客户端契约测试：
 * - 成功响应解包 ApiResponse.data
 * - HTTP 层错误 → 类型化错误映射（401/403/404/429/5xx/网络层）
 * - 业务层错误（HTTP 200 + code !== 200）→ BusinessError
 * - 请求拦截器：Authorization 携带与写操作幂等键
 */

const mock = new MockAdapter(client)

afterEach(() => {
  mock.reset()
  localStorage.removeItem('auth')
})

describe('成功响应解包', () => {
  it('返回 ApiResponse.data 而非整个信封', async () => {
    mock.onGet('/ping').reply(200, { code: 200, message: 'OK', data: { value: 42 } })
    const res = await client.get<{ value: number }>('/ping')
    expect(res.data).toEqual({ value: 42 })
  })
})

describe('业务错误映射（HTTP 200 + code !== 200）', () => {
  it('转换为 BusinessError 并携带 code 与 message', async () => {
    mock.onGet('/biz-fail').reply(200, { code: 40404, message: '请先勾选要结算的商品', data: null })
    const err = await client.get('/biz-fail').catch((e: unknown) => e)
    expect(err).toBeInstanceOf(BusinessError)
    expect((err as BusinessError).code).toBe(40404)
    expect((err as BusinessError).message).toBe('请先勾选要结算的商品')
  })
})

describe('HTTP 层错误映射', () => {
  it('401 → UnauthorizedError', async () => {
    mock.onGet('/e401').reply(401, { message: 'token expired' })
    const err = await client.get('/e401').catch((e: unknown) => e)
    expect(err).toBeInstanceOf(UnauthorizedError)
  })

  it('403 → ForbiddenError', async () => {
    mock.onGet('/e403').reply(403, { message: 'forbidden' })
    const err = await client.get('/e403').catch((e: unknown) => e)
    expect(err).toBeInstanceOf(ForbiddenError)
  })

  it('404 → NotFoundError', async () => {
    mock.onGet('/e404').reply(404, { message: 'not found' })
    const err = await client.get('/e404').catch((e: unknown) => e)
    expect(err).toBeInstanceOf(NotFoundError)
  })

  it('429 → RateLimitedError 并读取 Retry-After', async () => {
    mock.onGet('/e429').reply(429, { message: 'too many requests' }, { 'Retry-After': '30' })
    const err = await client.get('/e429').catch((e: unknown) => e)
    expect(err).toBeInstanceOf(RateLimitedError)
    expect((err as RateLimitedError).retryAfter).toBe(30)
  })

  it('500 → ServerError', async () => {
    mock.onGet('/e500').reply(500, { message: 'boom' })
    const err = await client.get('/e500').catch((e: unknown) => e)
    expect(err).toBeInstanceOf(ServerError)
  })

  it('网络层断连 → NetworkError', async () => {
    mock.onGet('/network').networkError()
    const err = await client.get('/network').catch((e: unknown) => e)
    expect(err).toBeInstanceOf(NetworkError)
  })
})

describe('请求拦截器', () => {
  it('localStorage 存在 token 时自动携带 Authorization', async () => {
    localStorage.setItem(
      'auth',
      JSON.stringify({ token: 'jwt-token-abc', expiresAt: Date.now() + 60_000 }),
    )
    mock.onPost('/orders').reply((config) => {
      expect(config.headers?.Authorization).toBe('Bearer jwt-token-abc')
      return [200, { code: 200, message: 'OK', data: null }]
    })
    await client.post('/orders', {})
  })

  it('写操作自动附加 Idempotency-Key 与 X-Trace-Id', async () => {
    mock.onPost('/cart/items').reply((config) => {
      expect(config.headers?.['Idempotency-Key']).toBeTruthy()
      expect(config.headers?.['X-Trace-Id']).toBeTruthy()
      return [200, { code: 200, message: 'OK', data: null }]
    })
    await client.post('/cart/items', { skuId: 'spu-101-sku1', quantity: 1 })
  })

  it('token 过期后不再携带 Authorization', async () => {
    localStorage.setItem(
      'auth',
      JSON.stringify({ token: 'jwt-expired', expiresAt: Date.now() - 1_000 }),
    )
    mock.onPost('/cart/items').reply((config) => {
      expect(config.headers?.Authorization).toBeUndefined()
      return [200, { code: 200, message: 'OK', data: null }]
    })
    await client.post('/cart/items', {})
  })
})
