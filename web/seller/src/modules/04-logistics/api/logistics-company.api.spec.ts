import { describe, expect, it, vi, beforeEach } from 'vitest'
import type { AxiosResponse } from 'axios'
import { logisticsCompanyApi } from './logistics-company.api'
import { http } from '@/shared/http'

/**
 * logisticsCompanyApi 单元测试
 *
 * client 响应拦截器已 unwrap ApiResponse.data，故 mock http 方法返回
 * AxiosResponse 形态（{ data: 业务对象 }），api 函数内部 .then(r => r.data) 解包。
 */
vi.mock('@/shared/http', () => ({
  http: {
    get: vi.fn(),
  },
}))

function mockResponse<T>(data: T): AxiosResponse<T> {
  return { data } as AxiosResponse<T>
}

describe('logisticsCompanyApi', () => {
  beforeEach(() => {
    vi.clearAllMocks()
  })

  describe('listEnabled', () => {
    it('调用 GET /seller/logistics-companies', async () => {
      vi.mocked(http.get).mockResolvedValue(mockResponse([]))
      await logisticsCompanyApi.listEnabled()
      expect(http.get).toHaveBeenCalledWith('/seller/logistics-companies')
    })

    it('返回解包后的物流公司数组', async () => {
      const companies = [
        {
          id: 'lc-001',
          name: '顺丰速运',
          code: 'SF',
          servicePhone: '95338',
          website: 'https://www.sf-express.com',
          supportsTracking: true,
          sortOrder: 1,
        },
      ]
      vi.mocked(http.get).mockResolvedValue(mockResponse(companies))
      const result = await logisticsCompanyApi.listEnabled()
      expect(result).toEqual(companies)
    })
  })
})
