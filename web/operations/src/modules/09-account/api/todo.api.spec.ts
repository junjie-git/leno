import { describe, expect, it, beforeEach, afterEach } from 'vitest'
import MockAdapter from 'axios-mock-adapter'
import { client } from '@/shared/http'
import {
  fetchTodoBoard,
  fetchPendingProducts,
  fetchPendingShops,
  fetchPendingAfterSales,
  fetchPendingReviews,
  fetchDeadLetterNotifications,
} from './todo.api'

/**
 * 待办工作台聚合 API 单测（axios-mock-adapter 挂载到真实 client 实例）
 *
 * 覆盖点：
 * - fetchTodoBoard 并行请求 5 个端点，各取 Total + Top10
 * - 各端点携带正确的 status/page/pageSize 查询参数
 * - 列表项归一化为 TodoItemDto（id/title/source/submittedAt）
 * - 单端点失败降级为该分类空数据（failed=true），不影响其他分类
 * - 全部端点失败时整体仍正常返回
 */
describe('09-account todoApi', () => {
  let mock: MockAdapter

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

  it('fetchTodoBoard 并行请求 5 个端点并归一化结果', async () => {
    mock.onGet('/admin/products/all').reply(
      ...ok({
        items: [{ id: 'p-001', name: '无线耳机 Pro', shopName: '星辰数码专营店', submittedAt: '2026-08-16T10:00:00Z' }],
        total: 12,
        page: 1,
        pageSize: 10,
      }),
    )
    mock.onGet('/admin/shops').reply(
      ...ok({
        items: [{ id: 'sp-001', shopName: '优选生活馆', submittedAt: '2026-08-16T09:00:00Z' }],
        total: 8,
        page: 1,
        pageSize: 10,
      }),
    )
    mock.onGet('/admin/after-sales').reply(
      ...ok({
        items: [{ id: 'as-001', orderNo: 'AS20260816008', buyerName: '李用户', createdAt: '2026-08-15T20:00:00Z' }],
        total: 5,
        page: 1,
        pageSize: 10,
      }),
    )
    mock.onGet('/admin/reviews').reply(
      ...ok({
        items: [{ id: 'r-001', productName: '手机壳', memberName: '王会员', createdAt: '2026-08-16T11:00:00Z' }],
        total: 3,
        page: 1,
        pageSize: 10,
      }),
    )
    mock.onGet('/notifications/records').reply(
      ...ok({
        items: [{ id: 'dl-001', title: '订单发货通知发送失败', channel: 'SMS', createdAt: '2026-08-16T08:00:00Z' }],
        total: 3,
        page: 1,
        pageSize: 10,
      }),
    )

    const board = await fetchTodoBoard()

    expect(board.products.total).toBe(12)
    expect(board.products.failed).toBe(false)
    expect(board.products.items[0]).toEqual({
      id: 'p-001',
      title: '无线耳机 Pro 待审核',
      source: '星辰数码专营店',
      submittedAt: '2026-08-16T10:00:00Z',
    })

    expect(board.shops.total).toBe(8)
    expect(board.shops.items[0].title).toBe('店铺「优选生活馆」入驻待审核')

    expect(board.afterSales.total).toBe(5)
    expect(board.afterSales.items[0].title).toBe('售后单 AS20260816008 待介入')
    expect(board.afterSales.items[0].source).toBe('李用户')

    expect(board.reviews.total).toBe(3)
    expect(board.reviews.items[0].title).toBe('「手机壳」评价待审核')

    expect(board.notifications.total).toBe(3)
    expect(board.notifications.items[0].title).toBe('订单发货通知发送失败')
    expect(board.notifications.items[0].source).toBe('SMS')

    // 共发起 5 个 GET
    expect(mock.history.get.length).toBe(5)
  })

  it('各端点携带 status/page/pageSize 查询参数', async () => {
    mock.onGet('/admin/products/all').reply(...ok({ items: [], total: 0, page: 1, pageSize: 10 }))
    mock.onGet('/admin/shops').reply(...ok({ items: [], total: 0, page: 1, pageSize: 10 }))
    mock.onGet('/admin/after-sales').reply(...ok({ items: [], total: 0, page: 1, pageSize: 10 }))
    mock.onGet('/admin/reviews').reply(...ok({ items: [], total: 0, page: 1, pageSize: 10 }))
    mock.onGet('/notifications/records').reply(...ok({ items: [], total: 0, page: 1, pageSize: 10 }))

    await fetchTodoBoard()

    const byUrl = new Map(mock.history.get.map((r) => [r.url as string, r.params]))
    expect(byUrl.get('/admin/products/all')).toEqual({
      status: 'PendingAudit',
      page: 1,
      pageSize: 10,
    })
    expect(byUrl.get('/admin/shops')).toEqual({ status: 'PendingReview', page: 1, pageSize: 10 })
    expect(byUrl.get('/admin/after-sales')).toEqual({
      status: 'PendingIntervention',
      page: 1,
      pageSize: 10,
    })
    expect(byUrl.get('/admin/reviews')).toEqual({ status: 'Pending', page: 1, pageSize: 10 })
    expect(byUrl.get('/notifications/records')).toEqual({
      status: 'DeadLetter',
      page: 1,
      pageSize: 10,
    })
  })

  it('单端点失败降级为该分类空数据，不影响其他分类', async () => {
    mock.onGet('/admin/products/all').reply(...ok({ items: [], total: 0, page: 1, pageSize: 10 }))
    // 入驻端点 500
    mock.onGet('/admin/shops').reply(500)
    mock.onGet('/admin/after-sales').reply(...ok({ items: [], total: 5, page: 1, pageSize: 10 }))
    // 评价端点业务错误（code !== 200）
    mock
      .onGet('/admin/reviews')
      .reply(200, { code: 40300, message: '无权访问', data: null })
    mock.onGet('/notifications/records').reply(...ok({ items: [], total: 0, page: 1, pageSize: 10 }))

    const board = await fetchTodoBoard()

    expect(board.products.failed).toBe(false)
    expect(board.products.total).toBe(0)

    expect(board.shops.failed).toBe(true)
    expect(board.shops.total).toBe(0)
    expect(board.shops.items).toEqual([])

    expect(board.afterSales.failed).toBe(false)
    expect(board.afterSales.total).toBe(5)

    expect(board.reviews.failed).toBe(true)

    expect(board.notifications.failed).toBe(false)
  })

  it('全部端点失败时整体仍正常返回（全部 failed）', async () => {
    mock.onGet().networkError()

    const board = await fetchTodoBoard()

    expect(board.products.failed).toBe(true)
    expect(board.shops.failed).toBe(true)
    expect(board.afterSales.failed).toBe(true)
    expect(board.reviews.failed).toBe(true)
    expect(board.notifications.failed).toBe(true)
  })

  it('fetchPendingProducts 空名称时回退默认标题', async () => {
    mock.onGet('/admin/products/all').reply(
      ...ok({
        items: [{ id: 'p-002', name: null, shopName: null, submittedAt: null }],
        total: 1,
        page: 1,
        pageSize: 10,
      }),
    )

    const category = await fetchPendingProducts()

    expect(category.items[0].title).toBe('商品 p-002 待审核')
    expect(category.items[0].source).toBeNull()
    expect(category.items[0].submittedAt).toBeNull()
  })

  it('单分类取数函数可独立调用', async () => {
    mock.onGet('/admin/shops').reply(
      ...ok({
        items: [{ id: 'sp-002', shopName: '极速达3C', submittedAt: '2026-08-17T09:00:00Z' }],
        total: 1,
        page: 1,
        pageSize: 10,
      }),
    )
    mock.onGet('/admin/reviews').reply(...ok({ items: [], total: 0, page: 1, pageSize: 10 }))
    mock.onGet('/notifications/records').reply(...ok({ items: [], total: 0, page: 1, pageSize: 10 }))
    mock.onGet('/admin/after-sales').reply(...ok({ items: [], total: 0, page: 1, pageSize: 10 }))
    mock.onGet('/admin/products/all').reply(...ok({ items: [], total: 0, page: 1, pageSize: 10 }))

    const [shops, afterSales, reviews, deadLetters, products] = await Promise.all([
      fetchPendingShops(),
      fetchPendingAfterSales(),
      fetchPendingReviews(),
      fetchDeadLetterNotifications(),
      fetchPendingProducts(),
    ])

    expect(shops.total).toBe(1)
    expect(afterSales.total).toBe(0)
    expect(reviews.total).toBe(0)
    expect(deadLetters.total).toBe(0)
    expect(products.total).toBe(0)
  })
})
