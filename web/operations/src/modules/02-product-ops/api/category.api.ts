import type { AxiosResponse } from 'axios'
import { client, withIdempotency } from '@/shared/http'
import type {
  CategoryDto,
  CategoryTreeParams,
  CreateCategoryDto,
  UpdateCategoryDto,
} from '../types/category.dto'

/**
 * 分类管理 API
 *
 * 与 Product 域 CategoryController / AdminCategoriesController 对接（baseURL 已含 /api）。
 * - GET /categories/tree、GET /categories/{id}：共享字典（已认证用户）
 * - POST/PUT /admin/categories*：管理端写操作（Admin, Operator）
 * - 停用含启用子分类或被商品引用的分类时后端返回 409（ConcurrencyError，message 透出）
 */
export const categoryApi = {
  /**
   * 查询分类树
   *
   * @param params.keyword 非空时只返回名称包含 keyword 的节点及其祖先节点（构建父链）
   */
  tree(params?: CategoryTreeParams): Promise<AxiosResponse<CategoryDto[]>> {
    return client.get<CategoryDto[]>('/categories/tree', { params })
  },

  /**
   * 分类详情（含 productCount）
   */
  get(id: string): Promise<AxiosResponse<CategoryDto>> {
    return client.get<CategoryDto>(`/categories/${id}`)
  },

  /**
   * 创建分类
   */
  create(body: CreateCategoryDto): Promise<AxiosResponse<CategoryDto>> {
    return client.post<CategoryDto>('/admin/categories', body, withIdempotency())
  },

  /**
   * 更新分类
   */
  update(id: string, body: UpdateCategoryDto): Promise<AxiosResponse<CategoryDto>> {
    return client.put<CategoryDto>(`/admin/categories/${id}`, body)
  },

  /**
   * 启用分类
   */
  enable(id: string): Promise<AxiosResponse<void>> {
    return client.post<void>(`/admin/categories/${id}/enable`, null, withIdempotency())
  },

  /**
   * 停用分类（含启用子分类或被商品引用时后端返回 409）
   */
  disable(id: string): Promise<AxiosResponse<void>> {
    return client.post<void>(`/admin/categories/${id}/disable`, null, withIdempotency())
  },
}
