import { describe, expect, it, beforeEach, afterEach } from 'vitest'
import MockAdapter from 'axios-mock-adapter'
import { client } from '@/shared/http'
import { notificationConfigApi } from './config.api'
import type { NotificationConfigDto } from '../types/config.dto'

/**
 * 通知渠道配置 API 单测（axios-mock-adapter 挂载到真实 client 实例）
 *
 * 覆盖点：
 * - get 调用 GET /admin/notification-config?channel=x 并解包脱敏配置
 * - update 调用 PUT /admin/notification-config，敏感项空串跳过语义由前端组包保证
 * - test 调用 POST /admin/notification-config/test 并解包渠道响应
 */
describe('07-notification-ops notificationConfigApi', () => {
  let mock: MockAdapter

  const fakeConfig: NotificationConfigDto = {
    channel: 'Sms',
    configs: [
      { key: 'AccessKeyId', value: 'LTAI5t****', isSensitive: true, description: '阿里云 AccessKeyId' },
      { key: 'AccessKeySecret', value: '********', isSensitive: true, description: '阿里云 AccessKeySecret' },
      { key: 'SignName', value: '【Leno】', isSensitive: false, description: '短信签名' },
    ],
    updatedBy: '运营管理员',
    updatedAt: '2026-07-25T10:30:00.000Z',
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

  it('get 调用 GET /admin/notification-config 并携带 channel 查询参数', async () => {
    let capturedParams: Record<string, unknown> = {}
    mock.onGet('/admin/notification-config').reply((config) => {
      capturedParams = (config.params ?? {}) as Record<string, unknown>
      return ok(fakeConfig)
    })

    const { data } = await notificationConfigApi.get('Sms')

    expect(data.channel).toBe('Sms')
    expect(data.configs).toHaveLength(3)
    expect(mock.history.get[0].url).toBe('/admin/notification-config')
    expect(capturedParams).toEqual({ channel: 'Sms' })
  })

  it('update 调用 PUT /admin/notification-config 并携带 Idempotency-Key', async () => {
    mock.onPut('/admin/notification-config').reply(() => ok(fakeConfig))

    const { data } = await notificationConfigApi.update({
      channel: 'Sms',
      // 敏感项空串跳过语义：AccessKeySecret 留空不修改（直接缺省）
      configs: {
        AccessKeyId: 'LTAI5tNewKeyId',
        SignName: '【Leno商城】',
      },
    })

    expect(data.channel).toBe('Sms')
    const req = mock.history.put[0]
    expect(req.url).toBe('/admin/notification-config')
    expect(JSON.parse(req.data as string)).toEqual({
      channel: 'Sms',
      configs: {
        AccessKeyId: 'LTAI5tNewKeyId',
        SignName: '【Leno商城】',
      },
    })
    expect(String(req.headers?.['Idempotency-Key'] ?? '')).not.toBe('')
  })

  it('test 调用 POST /admin/notification-config/test 并解包渠道响应', async () => {
    const testResult = {
      success: true,
      message: '发送成功',
      providerResponse: { Code: 'OK', BizId: '9080199798765', RequestId: '4ABD-2026' },
    }
    mock.onPost('/admin/notification-config/test').reply(() => ok(testResult))

    const { data } = await notificationConfigApi.test({
      channel: 'Sms',
      recipient: '13800008888',
      content: '【Leno】您的验证码为 884726，5 分钟内有效。',
    })

    expect(data.success).toBe(true)
    expect(data.message).toBe('发送成功')
    const req = mock.history.post[0]
    expect(req.url).toBe('/admin/notification-config/test')
    expect(JSON.parse(req.data as string)).toEqual({
      channel: 'Sms',
      recipient: '13800008888',
      content: '【Leno】您的验证码为 884726，5 分钟内有效。',
    })
    expect(String(req.headers?.['Idempotency-Key'] ?? '')).not.toBe('')
  })

  it('test 失败结果（success=false）正常解包不抛错', async () => {
    const testResult = {
      success: false,
      message: 'API Key 无效',
      providerResponse: { Code: 'InvalidAccessKeyId' },
    }
    mock.onPost('/admin/notification-config/test').reply(() => ok(testResult))

    const { data } = await notificationConfigApi.test({
      channel: 'Email',
      recipient: 'test@example.com',
      content: '测试邮件',
    })

    expect(data.success).toBe(false)
    expect(data.message).toBe('API Key 无效')
  })

  it('update 业务错误（code !== 200）抛出 BusinessError', async () => {
    mock
      .onPut('/admin/notification-config')
      .reply(200, { code: 40902, message: '配置更新失败，请重试', data: null })

    await expect(
      notificationConfigApi.update({ channel: 'Sms', configs: {} }),
    ).rejects.toThrowError('配置更新失败，请重试')
  })
})
