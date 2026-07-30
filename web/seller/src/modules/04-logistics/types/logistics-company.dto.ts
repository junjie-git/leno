/**
 * 04-logistics 物流公司 DTO
 *
 * 与后端 LogisticsCompanyController 对接：
 * - GET /api/seller/logistics-companies  查询启用态物流公司（卖家只读）
 */

/** 物流公司（卖家只读视图） */
export interface LogisticsCompanyDto {
  id: string
  name: string
  code: string
  servicePhone?: string
  website?: string
  supportsTracking: boolean
  sortOrder: number
}
