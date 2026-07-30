/* eslint-disable @typescript-eslint/no-explicit-any */
import type MockAdapter from 'axios-mock-adapter'
import { loadSeedData } from '../data/seed'

/**
 * 物流公司 handler 注册
 *
 * 端点（baseURL=/api，故拦截 /seller/logistics-companies）：
 * - GET /seller/logistics-companies  查询启用态物流公司（卖家只读）
 */
export function registerLogisticsHandlers(mock: MockAdapter): void {
  mock.onGet('/seller/logistics-companies').reply(() => {
    const seed = loadSeedData()
    const companies = [...(seed.logisticsCompanies as any[])].sort(
      (a, b) => a.sortOrder - b.sortOrder,
    )
    return [200, { code: 200, message: 'OK', data: companies }]
  })
}
