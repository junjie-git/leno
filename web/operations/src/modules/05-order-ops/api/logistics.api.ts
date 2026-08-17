import type { AxiosResponse } from 'axios'
import { client, withIdempotency } from '@/shared/http'
import type { PageResult } from '@/shared/types'
import type {
  CreateLogisticsCompanyDto,
  LogisticsCompanyDto,
  LogisticsCompanyQueryParams,
  UpdateLogisticsCompanyDto,
} from '../types/logistics.dto'

/**
 * 物流公司管理 API
 *
 * 与 Logistics 域 AdminLogisticsCompaniesController 对接（baseURL 已含 /api）。
 * 所有方法返回 AxiosResponse，调用方解构 .data 拿业务负载。
 * 公司代码重复时后端返回 409（ConcurrencyError，message「公司代码已存在」透出）。
 */
export const logisticsApi = {
  /**
   * 分页查询物流公司（keyword 按名称 / 代码模糊匹配）
   */
  list(
    params: LogisticsCompanyQueryParams,
  ): Promise<AxiosResponse<PageResult<LogisticsCompanyDto>>> {
    return client.get<PageResult<LogisticsCompanyDto>>('/admin/logistics-companies', { params })
  },

  /**
   * 创建物流公司（代码唯一，重复返回 409）
   */
  create(body: CreateLogisticsCompanyDto): Promise<AxiosResponse<LogisticsCompanyDto>> {
    return client.post<LogisticsCompanyDto>('/admin/logistics-companies', body, withIdempotency())
  },

  /**
   * 更新物流公司可编辑字段
   */
  update(id: string, body: UpdateLogisticsCompanyDto): Promise<AxiosResponse<LogisticsCompanyDto>> {
    return client.put<LogisticsCompanyDto>(`/admin/logistics-companies/${id}`, body)
  },

  /**
   * 启用物流公司（停用 ↔ 启用双向切换）
   */
  enable(id: string): Promise<AxiosResponse<void>> {
    return client.post<void>(`/admin/logistics-companies/${id}/enable`, null, withIdempotency())
  },

  /**
   * 停用物流公司（历史订单不受影响，新订单不可选）
   */
  disable(id: string): Promise<AxiosResponse<void>> {
    return client.post<void>(`/admin/logistics-companies/${id}/disable`, null, withIdempotency())
  },
}
