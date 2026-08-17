import { describe, expect, it, beforeEach, afterEach } from 'vitest'
import MockAdapter from 'axios-mock-adapter'
import { client } from '@/shared/http'
import { notificationApi } from './notification.api'
import type { NotificationListResultDto } from '../types/account.dto'

/**
 * 通知中心 API 单测（axios-mock-adapter 挂载到真实 client 实例）
 *
 * 覆盖点：
 * - list 调用 GET /notifications 并透传 page/pageSize/isRead 查询参数
 * - getUnreadCount 调用 GET /notifications/unread-count
 * - markAsRead 调用 POST /notifications/read 传 { recordIds }
 * - markAllAsRead 调用 POST /notifications/read-all
 * - 写操作自动携带 Idempotency-Key
 * - 业务错误（code !== 200）抛 BusinessError
 */
describe('09-account notificationApi', () => {
  let mock: MockAdapter

  const fakeList: NotificationListResultDto = {
    items: [
      {
        id: 'n-0001',
        title: '支付通道「微信支付」掉单率异常',
        summary: '近 1 小时内掉单率达 3.2%',
        content: '系统检测到支付通道掉单率异常升高，请尽快排查。',
        type: 'Business',
        source: '支付监控告警系统',
        businessRef: '/payments/channels',
        isRead: false,
        createdAt: '2026-08-17T14:30:00Z',
      },
      {
        id: 'n-0002',
        title: '3 条商品待审核（高优先级）',
        summary: '卖家「星辰数码专营店」提交的商品待审核',
        content: '请尽快前往商品审核页处理。',
        type: 'Audit',
        source: '商品审核系统',
        businessRef: '/products/audit',
        isRead: false,
        createdAt: '2026-08-17T14:28:00Z',
      },
    ],
    total: 2,
    page: 1,
    pageSize: 20,
    unreadCount: 2,
  }

  function ok<T>(data: T) {
    return [200, { code: 200, message: 'OK', data }]
  }

  beforeEach(() => {
    mock = new MockAdapter(client)
    localStorage.clear()
  })

  afterEach(() => {
    mock.restore()
  })

  it('list 调用 GET /notifications 并解包 data', async () => {
    mock.onGet('/notifications').reply(...ok(fakeList))

    const result = await notificationApi.list({ page: 1, pageSize: 20 })

    expect(result.total).toBe(2)
    expect(result.unreadCount).toBe(2)
    expect(result.items[0].id).toBe('n-0001')
    expect(result.items[0].isRead).toBe(false)

    expect(mock.history.get.length).toBe(1)
    expect(mock.history.get[0].url).toBe('/notifications')
  })

  it('list 透传 isRead/type 查询参数', async () => {
    mock.onGet('/notifications').reply(...ok({ ...fakeList, items: [], total: 0, unreadCount: 0 }))

    await notificationApi.list({ page: 2, pageSize: 10, isRead: false, type: 'Audit' })

    const params = mock.history.get[0].params
    expect(params).toEqual({ page: 2, pageSize: 10, isRead: false, type: 'Audit' })
  })

  it('getUnreadCount 调用 GET /notifications/unread-count 并解包 data', async () => {
    mock.onGet('/notifications/unread-count').reply(...ok({ count: 8 }))

    const result = await notificationApi.getUnreadCount()

    expect(result.count).toBe(8)
    expect(mock.history.get[0].url).toBe('/notifications/unread-count')
  })

  it('markAsRead 调用 POST /notifications/read 传 recordIds 并携带幂等键', async () => {
    mock.onPost('/notifications/read').reply(...ok(null))

    await notificationApi.markAsRead({ recordIds: ['n-0001', 'n-0002'] })

    const req = mock.history.post[0]
    expect(req.url).toBe('/notifications/read')
    expect(JSON.parse(req.data as string)).toEqual({ recordIds: ['n-0001', 'n-0002'] })
    expect(String(req.headers?.['Idempotency-Key'] ?? '')).not.toBe('')
  })

  it('markAllAsRead 调用 POST /notifications/read-all 并携带幂等键', async () => {
    mock.onPost('/notifications/read-all').reply(...ok(null))

    await notificationApi.markAllAsRead()

    const req = mock.history.post[0]
    expect(req.url).toBe('/notifications/read-all')
    expect(String(req.headers?.['Idempotency-Key'] ?? '')).not.toBe('')
  })

  it('list 业务错误（code !== 200）抛出后端 message', async () => {
    mock.onGet('/notifications').reply(200, { code: 40300, message: '无权访问通知', data: null })

    await expect(notificationApi.list({ page: 1, pageSize: 20 })).rejects.toThrowError(
      '无权访问通知',
    )
  })
})
