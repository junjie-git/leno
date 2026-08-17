import { describe, expect, it, beforeEach, afterEach } from 'vitest'
import MockAdapter from 'axios-mock-adapter'
import { client } from '@/shared/http'
import { channelApi } from './channel.api'
import type { ChannelConfigItemDto } from '../types/channel.dto'

/**
 * channelApi 单测（axios-mock-adapter 挂载到真实 client 实例）
 *
 * 覆盖点：
 * - list 调用 GET /admin/payment-channels 并解包数组
 * - get 调用 GET /admin/payment-channels/{id} 并解包单项
 * - update 调用 PUT /admin/payment-channels/{id}，configs 键值对 body（敏感字段空字符串跳过）+ Idempotency-Key
 * - enable / disable 调用状态端点 + Idempotency-Key
 * - 业务错误（code !== 200）抛 BusinessError 并透传后端 message
 */
describe('06-payment-ops channelApi', () => {
  let mock: MockAdapter

  const fakeAppId: ChannelConfigItemDto = {
    id: 'cfg-wechat-001',
    channel: 'WeChat',
    key: 'AppId',
    value: 'wx1234****',
    isSensitive: false,
    enabled: true,
    description: '微信开放平台应用 ID',
    updatedBy: '运营管理员',
    updatedAt: '2026-08-15T16:32:00.000Z',
  }

  const fakeApiKey: ChannelConfigItemDto = {
    id: 'cfg-wechat-002',
    channel: 'WeChat',
    key: 'ApiKey',
    value: '••••1234',
    isSensitive: true,
    enabled: true,
    description: 'API v3 密钥（敏感）',
    updatedBy: '运营管理员',
    updatedAt: '2026-08-15T16:32:00.000Z',
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

  it('list 调用 GET /admin/payment-channels 并解包配置项数组', async () => {
    mock.onGet('/admin/payment-channels').reply(() => ok([fakeAppId, fakeApiKey]))

    const { data } = await channelApi.list()

    expect(data).toHaveLength(2)
    expect(data[0].key).toBe('AppId')
    expect(data[1].isSensitive).toBe(true)
    expect(data[1].value).toBe('••••1234')
    expect(mock.history.get.length).toBe(1)
    expect(mock.history.get[0].url).toBe('/admin/payment-channels')
  })

  it('get 调用 GET /admin/payment-channels/{id} 并解包单项', async () => {
    mock.onGet('/admin/payment-channels/cfg-wechat-001').reply(() => ok(fakeAppId))

    const { data } = await channelApi.get('cfg-wechat-001')

    expect(data.id).toBe('cfg-wechat-001')
    expect(data.key).toBe('AppId')
    expect(mock.history.get.length).toBe(1)
    expect(mock.history.get[0].url).toBe('/admin/payment-channels/cfg-wechat-001')
  })

  it('update 调用 PUT 端点提交键值对（敏感字段空字符串跳过）并携带 Idempotency-Key', async () => {
    mock.onPut('/admin/payment-channels/cfg-wechat-001').reply(() => ok(fakeAppId))

    const { data } = await channelApi.update('cfg-wechat-001', {
      configs: { AppId: 'wx1a2b3c4d5e6f7890' },
    })

    expect(data.id).toBe('cfg-wechat-001')
    expect(mock.history.put.length).toBe(1)
    const req = mock.history.put[0]
    expect(req.url).toBe('/admin/payment-channels/cfg-wechat-001')
    expect(JSON.parse(req.data as string)).toEqual({
      configs: { AppId: 'wx1a2b3c4d5e6f7890' },
    })
    expect(String(req.headers?.['Idempotency-Key'] ?? '')).not.toBe('')
  })

  it('update 敏感字段留空传空字符串（不修改语义）', async () => {
    mock.onPut('/admin/payment-channels/cfg-wechat-002').reply(() => ok(fakeApiKey))

    await channelApi.update('cfg-wechat-002', {
      configs: { ApiKey: '' },
      description: 'API v3 密钥（敏感）',
    })

    expect(mock.history.put.length).toBe(1)
    const req = mock.history.put[0]
    expect(JSON.parse(req.data as string)).toEqual({
      configs: { ApiKey: '' },
      description: 'API v3 密钥（敏感）',
    })
  })

  it.each([
    {
      method: 'enable',
      url: '/admin/payment-channels/cfg-wechat-001/enable',
      call: () => channelApi.enable('cfg-wechat-001'),
    },
    {
      method: 'disable',
      url: '/admin/payment-channels/cfg-wechat-001/disable',
      call: () => channelApi.disable('cfg-wechat-001'),
    },
  ] as const)('$method 调用 POST $url 并携带 Idempotency-Key', async ({ url, call }) => {
    mock.onPost(url).reply(() => ok(null))

    await call()

    expect(mock.history.post.length).toBe(1)
    const req = mock.history.post[0]
    expect(req.url).toBe(url)
    expect(String(req.headers?.['Idempotency-Key'] ?? '')).not.toBe('')
  })

  it('update 业务错误（code !== 200）抛出 BusinessError 并透传 message', async () => {
    mock
      .onPut('/admin/payment-channels/cfg-wechat-001')
      .reply(200, { code: 40901, message: '配置已被他人修改，请刷新', data: null })

    await expect(
      channelApi.update('cfg-wechat-001', { configs: { AppId: 'wx-new' } }),
    ).rejects.toThrowError('配置已被他人修改，请刷新')
  })
})
