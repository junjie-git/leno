import { describe, expect, it, vi, beforeEach } from 'vitest'
import type { AxiosResponse } from 'axios'
import { freightTemplateApi } from './freight-template.api'
import { http, withIdempotency } from '@/shared/http'

/**
 * freightTemplateApi 单元测试
 *
 * client 响应拦截器已 unwrap ApiResponse.data，故 mock http 方法返回
 * AxiosResponse 形态（{ data: 业务对象 }），api 函数内部 .then(r => r.data) 解包。
 */
vi.mock('@/shared/http', () => ({
  http: {
    get: vi.fn(),
    post: vi.fn(),
    put: vi.fn(),
  },
  withIdempotency: vi.fn(() => ({ headers: { 'Idempotency-Key': 'mock-key' } })),
}))

function mockResponse<T>(data: T): AxiosResponse<T> {
  return { data } as AxiosResponse<T>
}

describe('freightTemplateApi', () => {
  beforeEach(() => {
    vi.clearAllMocks()
    vi.mocked(withIdempotency).mockReturnValue({
      headers: { 'Idempotency-Key': 'mock-key' },
    })
  })

  describe('listMine', () => {
    it('调用 GET /seller/freight-templates/mine', async () => {
      vi.mocked(http.get).mockResolvedValue(mockResponse([]))
      await freightTemplateApi.listMine()
      expect(http.get).toHaveBeenCalledWith('/seller/freight-templates/mine')
    })

    it('返回解包后的运费模板数组', async () => {
      const templates = [
        {
          id: 'ft-001',
          name: '全国统一运费',
          pricingType: 'Fixed',
          fixedFee: 10,
          regionRules: [],
          isEnabled: true,
          version: 1,
          createdAt: '2026-02-01T00:00:00Z',
          updatedAt: '2026-02-01T00:00:00Z',
        },
      ]
      vi.mocked(http.get).mockResolvedValue(mockResponse(templates))
      const result = await freightTemplateApi.listMine()
      expect(result).toEqual(templates)
    })
  })

  describe('create', () => {
    it('调用 POST /seller/freight-templates 带 Idempotency-Key', async () => {
      vi.mocked(http.post).mockResolvedValue(
        mockResponse({ id: 'ft-003', name: '新模板', pricingType: 'Fixed', regionRules: [], isEnabled: true, version: 1, createdAt: '', updatedAt: '' }),
      )
      const body = { name: '新模板', pricingType: 'Fixed' as const, fixedFee: 15 }
      await freightTemplateApi.create(body)

      expect(http.post).toHaveBeenCalledWith('/seller/freight-templates', body, {
        headers: { 'Idempotency-Key': 'mock-key' },
      })
      expect(withIdempotency).toHaveBeenCalled()
    })
  })

  describe('updateRules', () => {
    it('调用 PUT /seller/freight-templates/{id}/rules 带 version 乐观锁', async () => {
      vi.mocked(http.put).mockResolvedValue(
        mockResponse({ id: 'ft-001', name: '模板', pricingType: 'ByWeight', regionRules: [], isEnabled: true, version: 2, createdAt: '', updatedAt: '' }),
      )
      const body = {
        regionRules: [
          { id: 'r-001', regionCode: 'CN', regionName: '全国', firstUnit: 1, firstPrice: 8, nextUnit: 1, nextPrice: 2 },
        ],
        version: 1,
      }
      await freightTemplateApi.updateRules('ft-001', body)

      expect(http.put).toHaveBeenCalledWith('/seller/freight-templates/ft-001/rules', body)
    })
  })

  describe('enable', () => {
    it('调用 POST /seller/freight-templates/{id}/enable 带 Idempotency-Key', async () => {
      vi.mocked(http.post).mockResolvedValue(mockResponse(undefined))
      await freightTemplateApi.enable('ft-001')

      expect(http.post).toHaveBeenCalledWith(
        '/seller/freight-templates/ft-001/enable',
        {},
        { headers: { 'Idempotency-Key': 'mock-key' } },
      )
      expect(withIdempotency).toHaveBeenCalled()
    })
  })

  describe('disable', () => {
    it('调用 POST /seller/freight-templates/{id}/disable 带 Idempotency-Key', async () => {
      vi.mocked(http.post).mockResolvedValue(mockResponse(undefined))
      await freightTemplateApi.disable('ft-001')

      expect(http.post).toHaveBeenCalledWith(
        '/seller/freight-templates/ft-001/disable',
        {},
        { headers: { 'Idempotency-Key': 'mock-key' } },
      )
      expect(withIdempotency).toHaveBeenCalled()
    })
  })
})
