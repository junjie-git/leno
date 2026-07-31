/* eslint-disable @typescript-eslint/no-explicit-any */
import type MockAdapter from 'axios-mock-adapter'
import { loadSeedData, saveSeedData } from '../data/seed'

/**
 * 通知 handler 注册（BE-4 后端已就绪，mock 用于前端开发联调）
 *
 * 端点（baseURL=/api，故拦截 /notifications/...）：
 * - GET  /notifications            列表（isRead/page/pageSize）
 * - GET  /notifications/unread-count 未读计数
 * - POST /notifications/read        批量标记已读
 * - POST /notifications/read-all    全部标记已读
 */
export function registerNotificationHandlers(mock: MockAdapter): void {
  // 通知列表
  mock.onGet('/notifications').reply((config) => {
    const seed = loadSeedData()
    const items = (seed.notifications as any[]) ?? []
    const isRead = config.params?.isRead
    const page = Number(config.params?.page ?? 1)
    const pageSize = Number(config.params?.pageSize ?? 20)

    let filtered = items
    if (isRead === true) filtered = items.filter((n) => n.isRead)
    if (isRead === false) filtered = items.filter((n) => !n.isRead)

    const unreadCount = items.filter((n) => !n.isRead).length
    const start = (page - 1) * pageSize
    const paged = filtered.slice(start, start + pageSize)

    return [
      200,
      {
        code: 200,
        message: 'OK',
        data: {
          items: paged,
          total: filtered.length,
          unreadCount,
          page,
          pageSize,
        },
      },
    ]
  })

  // 未读计数
  mock.onGet('/notifications/unread-count').reply(() => {
    const seed = loadSeedData()
    const items = (seed.notifications as any[]) ?? []
    const unreadCount = items.filter((n) => !n.isRead).length
    return [200, { code: 200, message: 'OK', data: unreadCount }]
  })

  // 批量标记已读
  mock.onPost('/notifications/read').reply((config) => {
    const seed = loadSeedData()
    const items = (seed.notifications as any[]) ?? []
    const body = JSON.parse(config.data || '{}')
    const ids: string[] = body.recordIds ?? []
    const idSet = new Set(ids)
    for (const n of items) {
      if (idSet.has(n.recordId)) n.isRead = true
    }
    seed.notifications = items
    saveSeedData(seed)
    return [200, { code: 200, message: 'OK', data: null }]
  })

  // 全部标记已读
  mock.onPost('/notifications/read-all').reply(() => {
    const seed = loadSeedData()
    const items = (seed.notifications as any[]) ?? []
    for (const n of items) n.isRead = true
    seed.notifications = items
    saveSeedData(seed)
    return [200, { code: 200, message: 'OK', data: null }]
  })
}
