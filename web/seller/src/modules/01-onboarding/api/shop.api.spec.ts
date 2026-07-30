import { describe, expect, it, vi, beforeEach } from 'vitest'
import type { AxiosResponse } from 'axios'
import { shopApi } from './shop.api'
import { http, withIdempotency } from '@/shared/http'

/**
 * shopApi 单元测试
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

describe('shopApi', () => {
  beforeEach(() => {
    vi.clearAllMocks()
    vi.mocked(withIdempotency).mockReturnValue({
      headers: { 'Idempotency-Key': 'mock-key' },
    })
  })

  describe('submitApplication', () => {
    it('调用 POST /shops/application 带 Idempotency-Key', async () => {
      vi.mocked(http.post).mockResolvedValue(mockResponse({ id: 'shop-001', version: 1 }))
      const body = { name: '示例店', mainCategory: '服装', contactPhone: '13800138000' }
      await shopApi.submitApplication(body)

      expect(http.post).toHaveBeenCalledWith('/shops/application', body, {
        headers: { 'Idempotency-Key': 'mock-key' },
      })
      expect(withIdempotency).toHaveBeenCalled()
    })

    it('返回解包后的 ShopInfoDto', async () => {
      const shop = {
        id: 'shop-001',
        name: '示例店',
        status: 'Pending',
        customerService: { phone: '13800138000' },
        version: 1,
        createdAt: '2026-01-15T10:00:00Z',
        updatedAt: '2026-01-15T10:00:00Z',
      }
      vi.mocked(http.post).mockResolvedValue(mockResponse(shop))
      const result = await shopApi.submitApplication({
        name: '示例店',
        mainCategory: '服装',
        contactPhone: '13800138000',
      })
      expect(result).toEqual(shop)
    })
  })

  describe('getMyShop', () => {
    it('调用 GET /shops/me', async () => {
      vi.mocked(http.get).mockResolvedValue(mockResponse({ id: 'shop-001' }))
      await shopApi.getMyShop()
      expect(http.get).toHaveBeenCalledWith('/shops/me')
    })
  })

  describe('updateMyShop', () => {
    it('调用 PUT /shops/me 带 Idempotency-Key 与 version', async () => {
      vi.mocked(http.put).mockResolvedValue(mockResponse({ id: 'shop-001', version: 2 }))
      const body = {
        name: '示例店',
        customerService: { phone: '13800138000' },
        version: 1,
      }
      await shopApi.updateMyShop(body)

      expect(http.put).toHaveBeenCalledWith('/shops/me', body, {
        headers: { 'Idempotency-Key': 'mock-key' },
      })
      expect(withIdempotency).toHaveBeenCalled()
    })
  })

  describe('listQualifications', () => {
    it('调用 GET /shops/me/qualifications', async () => {
      vi.mocked(http.get).mockResolvedValue(mockResponse([]))
      await shopApi.listQualifications()
      expect(http.get).toHaveBeenCalledWith('/shops/me/qualifications')
    })

    it('返回解包后的资质数组', async () => {
      const list = [
        {
          id: 'qual-001',
          type: 'BusinessLicense',
          fileName: '营业执照.pdf',
          fileUrl: '',
          status: 'Approved',
          submittedAt: '2026-01-15T10:00:00Z',
        },
      ]
      vi.mocked(http.get).mockResolvedValue(mockResponse(list))
      const result = await shopApi.listQualifications()
      expect(result).toEqual(list)
    })
  })

  describe('uploadQualification', () => {
    it('调用 POST /shops/me/qualifications 带 FormData 与 Idempotency-Key', async () => {
      vi.mocked(http.post).mockResolvedValue(
        mockResponse({
          id: 'qual-004',
          type: 'IdCard',
          fileName: '身份证.jpg',
          fileUrl: '',
          status: 'Pending',
          submittedAt: '2026-07-30T10:00:00Z',
        }),
      )
      const file = new File(['x'], '身份证.jpg', { type: 'image/jpeg' })
      await shopApi.uploadQualification({ file, type: 'IdCard' })

      expect(http.post).toHaveBeenCalledTimes(1)
      const [url, data, config] = vi.mocked(http.post).mock.calls[0]
      expect(url).toBe('/shops/me/qualifications')
      expect(data).toBeInstanceOf(FormData)
      expect(config).toEqual({ headers: { 'Idempotency-Key': 'mock-key' } })
      expect(withIdempotency).toHaveBeenCalled()
    })
  })
})
