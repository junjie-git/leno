/* eslint-disable @typescript-eslint/no-explicit-any */
import type MockAdapter from 'axios-mock-adapter'
import { loadSeedData, saveSeedData } from '../data/seed'

export function registerCacheHandlers(mock: MockAdapter): void {
  mock.onGet('/admin/cache/info').reply(() => {
    const seed = loadSeedData()
    return [200, { code: 200, message: 'OK', data: seed.redisInfo }]
  })

  mock.onGet('/admin/cache/keyspaces').reply(() => {
    const seed = loadSeedData()
    return [200, { code: 200, message: 'OK', data: seed.keyspaces }]
  })

  mock.onGet('/admin/cache/keys').reply((config) => {
    const seed = loadSeedData()
    const params = config.params || {}
    const db = Number(params.db) || 0
    let keys = (seed.redisKeys as any[]).filter((k) => k.db === db)
    if (params.pattern && params.pattern !== '*') {
      const regex = new RegExp('^' + params.pattern.replace(/\*/g, '.*').replace(/\?/g, '.') + '$')
      keys = keys.filter((k) => regex.test(k.key))
    }
    if (params.type) {
      keys = keys.filter((k) => k.type === params.type)
    }
    const page = Number(params.page) || 1
    const pageSize = Number(params.pageSize) || 20
    const total = keys.length
    const items = keys.slice((page - 1) * pageSize, page * pageSize).map((k) => ({ key: k.key, type: k.type, size: k.size, ttl: k.ttl }))
    return [200, { code: 200, message: 'OK', data: { items, total, page, pageSize } }]
  })

  mock.onGet(/\/admin\/cache\/keys\/.+$/).reply((config) => {
    const url = config.url!
    const key = decodeURIComponent(url.replace('/admin/cache/keys/', ''))
    const db = Number(config.params?.db) || 0
    const seed = loadSeedData()
    const k = (seed.redisKeys as any[]).find((x) => x.key === key && x.db === db)
    if (!k) {
      return [200, { code: 40400, message: `Key ${key} 不存在`, data: null }]
    }
    return [200, { code: 200, message: 'OK', data: k }]
  })

  mock.onDelete(/\/admin\/cache\/keys\/.+$/).reply((config) => {
    const url = config.url!
    const key = decodeURIComponent(url.replace('/admin/cache/keys/', ''))
    const db = Number(config.params?.db) || 0
    const seed = loadSeedData()
    const idx = (seed.redisKeys as any[]).findIndex((x) => x.key === key && x.db === db)
    if (idx < 0) {
      return [200, { code: 40400, message: `Key ${key} 不存在`, data: null }]
    }
    seed.redisKeys.splice(idx, 1)
    saveSeedData(seed)
    return [200, { code: 200, message: 'OK', data: { success: true } }]
  })
}
