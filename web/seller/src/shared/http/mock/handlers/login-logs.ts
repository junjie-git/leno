/* eslint-disable @typescript-eslint/no-explicit-any */
import type MockAdapter from 'axios-mock-adapter'
import { loadSeedData } from '../data/seed'

export function registerLoginLogHandlers(mock: MockAdapter): void {
  mock.onGet('/admin/login-logs/export').reply((config) => {
    const seed = loadSeedData()
    const logs = filterAndSortLogs(seed.loginLogs as any[], config.params || {})
    const csv = ['id,loginAt,username,ipAddress,geoLocation,browser,os,result,failureReason,durationMs,traceId']
    for (const l of logs) {
      csv.push([l.id, l.loginAt, l.username, l.ipAddress, l.geoLocation, l.browser, l.os, l.result, l.failureReason ?? '', l.durationMs, l.traceId].join(','))
    }
    return [200, csv.join('\n')]
  })

  mock.onGet(/\/admin\/login-logs\/[^/]+$/).reply((config) => {
    const id = config.url!.split('/').pop()!
    const seed = loadSeedData()
    const log = (seed.loginLogs as any[]).find((l) => l.id === id)
    if (!log) {
      return [200, { code: 40400, message: `日志 ${id} 不存在`, data: null }]
    }
    return [200, { code: 200, message: 'OK', data: log }]
  })

  mock.onGet('/admin/login-logs').reply((config) => {
    const seed = loadSeedData()
    const params = config.params || {}
    const logs = filterAndSortLogs(seed.loginLogs as any[], params)
    const page = Number(params.page) || 1
    const pageSize = Number(params.pageSize) || 20
    const total = logs.length
    const items = logs.slice((page - 1) * pageSize, page * pageSize)
    return [200, { code: 200, message: 'OK', data: { items, total, page, pageSize } }]
  })
}

function filterAndSortLogs(logs: any[], params: any): any[] {
  let result = [...logs]
  if (params.username) {
    result = result.filter((l) => l.username.includes(params.username))
  }
  if (params.result) {
    result = result.filter((l) => l.result === params.result)
  }
  if (params.loginAtFrom) {
    const from = new Date(params.loginAtFrom).getTime()
    result = result.filter((l) => new Date(l.loginAt).getTime() >= from)
  }
  if (params.loginAtTo) {
    const to = new Date(params.loginAtTo).getTime()
    result = result.filter((l) => new Date(l.loginAt).getTime() <= to)
  }
  // 按时间倒序
  result.sort((a, b) => new Date(b.loginAt).getTime() - new Date(a.loginAt).getTime())
  return result
}
