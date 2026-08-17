import { describe, expect, it, beforeEach, afterEach } from 'vitest'
import MockAdapter from 'axios-mock-adapter'
import { client } from '@/shared/http'
import { templateApi } from './template.api'
import type { NotificationTemplateDto } from '../types/template.dto'

/**
 * 通知模板 API 单测（axios-mock-adapter 挂载到真实 client 实例）
 *
 * 覆盖点：
 * - list 调用 GET /admin/notification-templates，筛选 + 分页组合传参并解包 data
 * - detail 调用 GET /admin/notification-templates/{templateId}
 * - create / update 调用对应端点，body 正确且自动携带 Idempotency-Key
 * - enable / disable / preview 调用对应 POST 子端点并解包响应
 */
describe('07-notification-ops templateApi', () => {
  let mock: MockAdapter

  const fakeTemplate: NotificationTemplateDto = {
    templateId: 'tpl-0001',
    code: 'ORDER_PAID',
    name: '订单已支付通知',
    eventType: 'Order',
    channel: 'Sms',
    variables: [
      { name: 'orderTime', description: '下单时间', example: '2026-07-26 15:23' },
      { name: 'orderNo', description: '订单编号', example: 'NO202607261523001' },
      { name: 'amount', description: '支付金额', example: '¥299.00' },
    ],
    titleTemplate: '【Leno】您的订单已支付成功',
    bodyTemplate:
      '【Leno】您于{{orderTime}}提交的订单{{orderNo}}已支付成功，支付金额{{amount}}。我们将尽快为您安排发货，请留意物流通知。',
    status: 'Active',
    updatedBy: '运营管理员',
    updatedAt: '2026-07-26T14:30:00.000Z',
  }

  const fakePage = { items: [fakeTemplate], total: 1, page: 1, pageSize: 20 }

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

  it('list 调用 GET /admin/notification-templates 组合查询参数并解包 data', async () => {
    let capturedParams: Record<string, unknown> = {}
    mock.onGet('/admin/notification-templates').reply((config) => {
      capturedParams = (config.params ?? {}) as Record<string, unknown>
      return ok(fakePage)
    })

    const { data } = await templateApi.list({
      page: 1,
      pageSize: 20,
      keyword: 'ORDER',
      eventType: 'Order',
      channel: 'Sms',
      status: 'Active',
    })

    expect(data.items[0].code).toBe('ORDER_PAID')
    expect(data.total).toBe(1)
    expect(mock.history.get.length).toBe(1)
    expect(mock.history.get[0].url).toBe('/admin/notification-templates')
    expect(capturedParams).toMatchObject({
      page: 1,
      pageSize: 20,
      keyword: 'ORDER',
      eventType: 'Order',
      channel: 'Sms',
      status: 'Active',
    })
  })

  it('list 仅传分页参数时也正常工作', async () => {
    mock.onGet('/admin/notification-templates').reply(() => ok(fakePage))

    const { data } = await templateApi.list({ page: 2, pageSize: 10 })

    expect(data.page).toBe(1)
    expect(mock.history.get.length).toBe(1)
  })

  it('detail 调用 GET /admin/notification-templates/{templateId} 并解包模板', async () => {
    mock.onGet('/admin/notification-templates/tpl-0001').reply(() => ok(fakeTemplate))

    const { data } = await templateApi.detail('tpl-0001')

    expect(data.templateId).toBe('tpl-0001')
    expect(data.variables).toHaveLength(3)
    expect(mock.history.get[0].url).toBe('/admin/notification-templates/tpl-0001')
  })

  it('create 调用 POST /admin/notification-templates 并携带 Idempotency-Key', async () => {
    mock.onPost('/admin/notification-templates').reply(() => ok(fakeTemplate))

    const { data } = await templateApi.create({
      code: 'ORDER_PAID',
      name: '订单已支付通知',
      eventType: 'Order',
      channel: 'Sms',
      variables: [{ name: 'orderNo', description: '订单编号' }],
      titleTemplate: '【Leno】您的订单已支付成功',
      bodyTemplate: '您的订单{{orderNo}}已支付成功',
      status: 'Active',
    })

    expect(data.code).toBe('ORDER_PAID')
    const req = mock.history.post[0]
    expect(req.url).toBe('/admin/notification-templates')
    expect(JSON.parse(req.data as string)).toMatchObject({ code: 'ORDER_PAID', channel: 'Sms' })
    expect(String(req.headers?.['Idempotency-Key'] ?? '')).not.toBe('')
  })

  it('update 调用 PUT /admin/notification-templates/{templateId} 并传 SaveDto', async () => {
    mock.onPut('/admin/notification-templates/tpl-0001').reply(() => ok(fakeTemplate))

    const { data } = await templateApi.update('tpl-0001', {
      code: 'ORDER_PAID',
      name: '订单已支付通知（改）',
      eventType: 'Order',
      channel: 'Sms',
      variables: [],
      titleTemplate: '【Leno】您的订单已支付成功',
      bodyTemplate: '您的订单已支付成功',
      status: 'Active',
    })

    expect(data.name).toBe('订单已支付通知')
    const req = mock.history.put[0]
    expect(req.url).toBe('/admin/notification-templates/tpl-0001')
    expect(JSON.parse(req.data as string).name).toBe('订单已支付通知（改）')
    expect(String(req.headers?.['Idempotency-Key'] ?? '')).not.toBe('')
  })

  it('enable / disable 调用对应 POST 子端点并携带 Idempotency-Key', async () => {
    mock.onPost('/admin/notification-templates/tpl-0001/enable').reply(() => ok(null))
    mock.onPost('/admin/notification-templates/tpl-0001/disable').reply(() => ok(null))

    await templateApi.enable('tpl-0001')
    await templateApi.disable('tpl-0001')

    expect(mock.history.post.map((r) => r.url)).toEqual([
      '/admin/notification-templates/tpl-0001/enable',
      '/admin/notification-templates/tpl-0001/disable',
    ])
    for (const req of mock.history.post) {
      expect(String(req.headers?.['Idempotency-Key'] ?? '')).not.toBe('')
    }
  })

  it('preview 调用 POST /admin/notification-templates/{templateId}/preview 并解包渲染结果', async () => {
    const rendered = {
      title: '【Leno】您的订单已支付成功',
      body: '【Leno】您于2026-07-26 15:23提交的订单NO202607261523001已支付成功，支付金额¥299.00。',
    }
    mock.onPost('/admin/notification-templates/tpl-0001/preview').reply(() => ok(rendered))

    const { data } = await templateApi.preview('tpl-0001', {
      variables: {
        orderTime: '2026-07-26 15:23',
        orderNo: 'NO202607261523001',
        amount: '¥299.00',
      },
    })

    expect(data.title).toBe('【Leno】您的订单已支付成功')
    expect(data.body).toContain('NO202607261523001')
    const req = mock.history.post[0]
    expect(req.url).toBe('/admin/notification-templates/tpl-0001/preview')
    expect(JSON.parse(req.data as string)).toEqual({
      variables: {
        orderTime: '2026-07-26 15:23',
        orderNo: 'NO202607261523001',
        amount: '¥299.00',
      },
    })
  })

  it('list 业务错误（code !== 200）抛出 BusinessError', async () => {
    mock
      .onGet('/admin/notification-templates')
      .reply(200, { code: 40301, message: '无通知模板查询权限', data: null })

    await expect(templateApi.list({ page: 1, pageSize: 20 })).rejects.toThrowError('无通知模板查询权限')
  })
})
