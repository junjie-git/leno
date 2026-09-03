import { client } from '@/shared/http'
import type { BrandDto, CategoryDto } from '../types/product.dto'

/**
 * 分类与品牌 API（Product BC 买家端）
 *
 * - GET /categories/tree 分类树
 * - GET /brands         品牌列表
 */
export const categoryApi = {
  /** 分类树（一级 + 二级） */
  getTree(): Promise<CategoryDto[]> {
    return client.get<CategoryDto[]>('/categories/tree').then((r) => r.data)
  },
}

export const brandApi = {
  /** 品牌列表 */
  list(): Promise<BrandDto[]> {
    return client.get<BrandDto[]>('/brands').then((r) => r.data)
  },
}
