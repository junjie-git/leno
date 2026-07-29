import type MockAdapter from 'axios-mock-adapter'
import { loadSeedData, saveSeedData } from '../data/seed'

export function registerOnlineUserHandlers(mock: MockAdapter): void {
  mock.onGet('/admin/online-users/stats').reply(() => {
    const seed = loadSeedData()
    const users = seed.onlineUsers as any[]
    const now = Date.now()
    const logins24h = users.filter((u) => now - new Date(u.loginAt).getTime() < 24 * 3600_000).length
    const anomalies = users.filter((u) => u.isAnomaly).length
    return [200, { code: 200, message: 'OK', data: { total: users.length, logins24h, anomalies } }]
  })

  mock.onGet('/admin/online-users').reply((config) => {
    const seed = loadSeedData()
    const params = config.params || {}
    let users = seed.onlineUsers as any[]
    // 筛选
    if (params.username) {
      users = users.filter((u) => u.username.includes(params.username))
    }
    if (params.ipAddress) {
      users = users.filter((u) => u.ipAddress.includes(params.ipAddress))
    }
    // 实时计算 sessionDurationMs 与 lastActivityAt 滚动
    const now = Date.now()
    users = users.map((u) => ({
      ...u,
      lastActivityAt: new Date(now - Math.floor(Math.random() * 5 * 60_000)).toISOString(),
      sessionDurationMs: now - new Date(u.loginAt).getTime(),
    }))
    // 分页
    const page = Number(params.page) || 1
    const pageSize = Number(params.pageSize) || 20
    const total = users.length
    const items = users.slice((page - 1) * pageSize, page * pageSize)
    return [200, { code: 200, message: 'OK', data: { items, total, page, pageSize } }]
  })

  mock.onGet(/\/admin\/online-users\/[^/]+$/).reply((config) => {
    const id = config.url!.split('/').pop()!
    const seed = loadSeedData()
    const user = (seed.onlineUsers as any[]).find((u) => u.id === id)
    if (!user) {
      return [200, { code: 40400, message: `会话 ${id} 不存在`, data: null }]
    }
    return [200, { code: 200, message: 'OK', data: { ...user, sessionDurationMs: Date.now() - new Date(user.loginAt).getTime() } }]
  })

  mock.onDelete(/\/admin\/online-users\/[^/]+$/).reply((config) => {
    const id = config.url!.split('/').pop()!
    const seed = loadSeedData()
    const idx = (seed.onlineUsers as any[]).findIndex((u) => u.id === id)
    if (idx < 0) {
      return [200, { code: 40400, message: `会话 ${id} 不存在`, data: null }]
    }
    // 禁止下线自己（mock 用 admin 标记）
    if (seed.onlineUsers[idx].username === 'admin') {
      return [200, { code: 40003, message: '不能下线自己', data: null }]
    }
    seed.onlineUsers.splice(idx, 1)
    saveSeedData(seed)
    return [200, { code: 200, message: 'OK', data: { success: true } }]
  })
}
