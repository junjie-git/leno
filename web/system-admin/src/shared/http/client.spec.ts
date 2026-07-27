import { describe, it, expect, beforeEach, afterEach, vi } from 'vitest'
import axios from 'axios'
import type { AxiosInstance, AxiosRequestConfig, AxiosResponse, InternalAxiosRequestConfig } from 'axios'
import { client, withIdempotency } from './client'
import {
  BusinessError,
  UnauthorizedError,
  ForbiddenError,
  NotFoundError,
  RateLimitedError,
  ServerError,
  ConcurrencyError,
  NetworkError,
} from './errors'

// 用真实 axios 实例 + adapter mock，验证拦截器链
function mockAdapter(response: Partial<AxiosResponse>): (config: InternalAxiosRequestConfig) => Promise<AxiosResponse> {
  return (config) =>
    Promise.resolve({
      data: response.data,
      status: response.status ?? 200,
      statusText: response.statusText ?? 'OK',
      headers: response.headers ?? {},
      config,
    } as AxiosResponse)
}

function mockAdapterReject(error: { response?: Partial<AxiosResponse>; request?: unknown; message: string }): (config: InternalAxiosRequestConfig) => Promise<AxiosResponse> {
  return () => Promise.reject(error as unknown)
}

describe('shared/http/client', () => {
  let originalAdapter: AxiosInstance['defaults']['adapter']

  beforeEach(() => {
    originalAdapter = client.defaults.adapter
    localStorage.clear()
    sessionStorage.clear()
  })

  afterEach(() => {
    client.defaults.adapter = originalAdapter
    vi.restoreAllMocks()
  })

  it('baseURL 为 /api', () => {
    expect(client.defaults.baseURL).toBe('/api')
  })

  it('timeout 为 15000ms', () => {
    expect(client.defaults.timeout).toBe(15_000)
  })

  it('成功响应解包 ApiResponse.data', async () => {
    client.defaults.adapter = mockAdapter({
      data: { code: 0, message: 'ok', data: { id: 1, name: 'alice' }, traceId: 't-1' },
    }) as AxiosInstance['defaults']['adapter']
    const resp = await client.get('/admin/users/1')
    expect(resp.data).toEqual({ id: 1, name: 'alice' })
  })

  it('code !== 0 抛 BusinessError', async () => {
    client.defaults.adapter = mockAdapter({
      data: { code: 40001, message: '账号已禁用', data: null, traceId: 't-2' },
    }) as AxiosInstance['defaults']['adapter']
    await expect(client.get('/admin/users/1')).rejects.toMatchObject({
      kind: 'BusinessError',
      code: 40001,
      message: '账号已禁用',
      traceId: 't-2',
    })
  })

  it('HTTP 401 抛 UnauthorizedError', async () => {
    client.defaults.adapter = mockAdapterReject({
      response: { status: 401, data: { message: '未登录' }, headers: { 'x-trace-id': 't-3' } },
      message: 'Request failed with status code 401',
    }) as AxiosInstance['defaults']['adapter']
    await expect(client.get('/admin/users/1')).rejects.toMatchObject({
      kind: 'UnauthorizedError',
    })
  })

  it('HTTP 403 抛 ForbiddenError', async () => {
    client.defaults.adapter = mockAdapterReject({
      response: { status: 403, data: { message: '禁止访问' } },
      message: 'Request failed',
    }) as AxiosInstance['defaults']['adapter']
    await expect(client.get('/admin/users/1')).rejects.toMatchObject({
      kind: 'ForbiddenError',
    })
  })

  it('HTTP 404 抛 NotFoundError', async () => {
    client.defaults.adapter = mockAdapterReject({
      response: { status: 404, data: { message: '不存在' } },
      message: 'Request failed',
    }) as AxiosInstance['defaults']['adapter']
    await expect(client.get('/admin/users/1')).rejects.toMatchObject({
      kind: 'NotFoundError',
    })
  })

  it('HTTP 409 抛 ConcurrencyError 携带 currentVersion', async () => {
    client.defaults.adapter = mockAdapterReject({
      response: { status: 409, data: { message: '冲突', currentVersion: 7 } },
      message: 'Request failed',
    }) as AxiosInstance['defaults']['adapter']
    await expect(client.get('/admin/rate-limit-rules/1')).rejects.toMatchObject({
      kind: 'ConcurrencyError',
      currentVersion: 7,
    })
  })

  it('HTTP 429 抛 RateLimitedError 携带 retryAfter', async () => {
    client.defaults.adapter = mockAdapterReject({
      response: { status: 429, data: { message: '限流' }, headers: { 'retry-after': '15' } },
      message: 'Request failed',
    }) as AxiosInstance['defaults']['adapter']
    await expect(client.post('/admin/dead-letters/1/retry')).rejects.toMatchObject({
      kind: 'RateLimitedError',
      retryAfter: 15,
    })
  })

  it('HTTP 500 抛 ServerError', async () => {
    client.defaults.adapter = mockAdapterReject({
      response: { status: 500, data: { message: '内部错误' } },
      message: 'Request failed',
    }) as AxiosInstance['defaults']['adapter']
    await expect(client.get('/admin/users/1')).rejects.toMatchObject({
      kind: 'ServerError',
    })
  })

  it('网络错误（无 response）抛 NetworkError', async () => {
    client.defaults.adapter = mockAdapterReject({
      request: {},
      message: 'Network Error',
    }) as AxiosInstance['defaults']['adapter']
    await expect(client.get('/admin/users/1')).rejects.toMatchObject({
      kind: 'NetworkError',
    })
  })

  it('请求拦截器从 localStorage 注入 Authorization', async () => {
    localStorage.setItem('auth', JSON.stringify({ token: 'tok-xyz', expiresAt: Date.now() + 10_000 }))
    let captured: AxiosRequestConfig | undefined
    client.defaults.adapter = ((config: InternalAxiosRequestConfig) => {
      captured = config
      return Promise.resolve({ data: { code: 0, message: 'ok', data: null }, status: 200, statusText: 'OK', headers: {}, config } as AxiosResponse)
    }) as AxiosInstance['defaults']['adapter']
    await client.get('/admin/users')
    expect((captured as AxiosRequestConfig).headers?.Authorization).toBe('Bearer tok-xyz')
  })

  it('请求拦截器注入 X-Request-Id', async () => {
    let captured: AxiosRequestConfig | undefined
    client.defaults.adapter = ((config: InternalAxiosRequestConfig) => {
      captured = config
      return Promise.resolve({ data: { code: 0, message: 'ok', data: null }, status: 200, statusText: 'OK', headers: {}, config } as AxiosResponse)
    }) as AxiosInstance['defaults']['adapter']
    await client.get('/admin/users')
    const requestId = (captured as AxiosRequestConfig).headers?.['X-Request-Id']
    expect(typeof requestId).toBe('string')
    expect((requestId as string)).toHaveLength(36)
  })

  it('withIdempotency 注入 Idempotency-Key 头', async () => {
    let captured: AxiosRequestConfig | undefined
    client.defaults.adapter = ((config: InternalAxiosRequestConfig) => {
      captured = config
      return Promise.resolve({ data: { code: 0, message: 'ok', data: null }, status: 200, statusText: 'OK', headers: {}, config } as AxiosResponse)
    }) as AxiosInstance['defaults']['adapter']
    await client.post('/admin/dead-letters/1/retry', null, withIdempotency())
    expect((captured as AxiosRequestConfig).headers?.['Idempotency-Key']).toMatch(/^[0-9a-f-]{36}$/i)
  })
})
