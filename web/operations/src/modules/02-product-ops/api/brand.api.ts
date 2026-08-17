import type { AxiosResponse } from 'axios'
import { client, withIdempotency } from '@/shared/http'
import type { PageResult } from '@/shared/types'
import type {
  BrandDto,
  BrandQueryParams,
  CreateBrandDto,
  UpdateBrandDto,
} from '../types/brand.dto'

/**
 * 品牌管理 API
 *
 * 与 Product 域 BrandController / AdminBrandsController 对接（baseURL 已含 /api）。
 * - GET /brands、GET /brands/{id}：共享字典（已认证用户）
 * - POST/PUT /admin/brands*：管理端写操作（Admin, Operator）
 * - 停用被商品引用的品牌时后端返回 409（ConcurrencyError，message 透出）
 */
export const brandApi = {
  /**
   * 分页查询品牌列表（共享字典）
   */
  list(params: BrandQueryParams): Promise<AxiosResponse<PageResult<BrandDto>>> {
    return client.get<PageResult<BrandDto>>('/brands', { params })
  },

  /**
   * 品牌详情（编辑时回填完整字段）
   */
  get(id: string): Promise<AxiosResponse<BrandDto>> {
    return client.get<BrandDto>(`/brands/${id}`)
  },

  /**
   * 创建品牌
   */
  create(body: CreateBrandDto): Promise<AxiosResponse<BrandDto>> {
    return client.post<BrandDto>('/admin/brands', body, withIdempotency())
  },

  /**
   * 更新品牌
   */
  update(id: string, body: UpdateBrandDto): Promise<AxiosResponse<BrandDto>> {
    return client.put<BrandDto>(`/admin/brands/${id}`, body)
  },

  /**
   * 启用品牌
   */
  enable(id: string): Promise<AxiosResponse<void>> {
    return client.post<void>(`/admin/brands/${id}/enable`, null, withIdempotency())
  },

  /**
   * 停用品牌（被商品引用时后端返回 409）
   */
  disable(id: string): Promise<AxiosResponse<void>> {
    return client.post<void>(`/admin/brands/${id}/disable`, null, withIdempotency())
  },
}
