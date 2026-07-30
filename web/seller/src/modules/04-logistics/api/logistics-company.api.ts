import { http } from '@/shared/http'
import type { LogisticsCompanyDto } from '../types/logistics-company.dto'

/**
 * 物流公司 API 客户端（卖家只读）
 *
 * 与后端 LogisticsCompanyController 对接：
 * - GET /seller/logistics-companies  查询启用态物流公司
 */
export const logisticsCompanyApi = {
  /** 查询启用态物流公司（卖家只读） */
  listEnabled(): Promise<LogisticsCompanyDto[]> {
    return http
      .get<LogisticsCompanyDto[]>('/seller/logistics-companies')
      .then((r) => r.data)
  },
}
