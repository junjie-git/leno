// web/system-admin/src/modules/07-monitoring/api/monitoring.api.spec.ts
import { describe, it, expect, vi, beforeEach } from 'vitest'
import { client } from '@/shared/http'
import { monitoringApi } from './monitoring.api'
import {
  MONITORING_CONFIG_KEYS,
  type PrometheusDashboardConfigDto,
} from '../types/monitoring.dto'

// 统一 mock shared/http 模块，client.get/post/put/delete 替换为 spy
vi.mock('@/shared/http', async () => {
  const actual = await vi.importActual<typeof import('@/shared/http')>('@/shared/http')
  return {
    ...actual,
    client: {
      get: vi.fn(),
      post: vi.fn(),
      put: vi.fn(),
      delete: vi.fn(),
    },
    withIdempotency: actual.withIdempotency,
  }
})

describe('monitoringApi', () => {
  beforeEach(() => {
    vi.clearAllMocks()
  })

  it('getPrometheusUrl 使用 /admin/system-configs/by-key/{key} 路径', async () => {
    const mockData: PrometheusDashboardConfigDto = {
      configId: 'cfg-prom-001',
      key: MONITORING_CONFIG_KEYS.PROMETHEUS_DASHBOARD_URL,
      value: 'http://grafana.leno.internal/d/system-overview',
    }
    ;(client.get as ReturnType<typeof vi.fn>).mockResolvedValue({ data: mockData })

    await monitoringApi.getPrometheusUrl()

    expect(client.get).toHaveBeenCalledWith(
      `/admin/system-configs/by-key/${MONITORING_CONFIG_KEYS.PROMETHEUS_DASHBOARD_URL}`,
    )
  })

  it('getPrometheusUrl 返回明文 URL 数据结构', async () => {
    const mockData: PrometheusDashboardConfigDto = {
      configId: 'cfg-prom-001',
      key: MONITORING_CONFIG_KEYS.PROMETHEUS_DASHBOARD_URL,
      value: 'http://grafana.leno.internal/d/system-overview',
    }
    ;(client.get as ReturnType<typeof vi.fn>).mockResolvedValue({ data: mockData })

    const res = await monitoringApi.getPrometheusUrl()

    expect(res.data).toEqual(mockData)
    expect(res.data?.value).toBe('http://grafana.leno.internal/d/system-overview')
  })

  it('getPrometheusUrl 路径前缀正确且包含完整配置键', async () => {
    ;(client.get as ReturnType<typeof vi.fn>).mockResolvedValue({ data: {} })

    await monitoringApi.getPrometheusUrl()

    const url = (client.get as ReturnType<typeof vi.fn>).mock.calls[0][0] as string
    expect(url.startsWith('/admin/system-configs/by-key/')).toBe(true)
    expect(url).toContain(MONITORING_CONFIG_KEYS.PROMETHEUS_DASHBOARD_URL)
    expect(url).toBe('/admin/system-configs/by-key/monitoring.prometheus.dashboard-url')
  })

  it('getPrometheusUrl 仅使用 GET 方法，不触发 POST/PUT/DELETE', async () => {
    ;(client.get as ReturnType<typeof vi.fn>).mockResolvedValue({ data: {} })

    await monitoringApi.getPrometheusUrl()

    expect(client.get).toHaveBeenCalledTimes(1)
    expect(client.post).not.toHaveBeenCalled()
    expect(client.put).not.toHaveBeenCalled()
    expect(client.delete).not.toHaveBeenCalled()
  })
})
