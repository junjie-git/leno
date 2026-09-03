import { client } from '@/shared/http'
import type { ProductDetailDto, PriceHistoryPointDto, ProductQueryParams, ProductSearchResult } from '../types/product.dto'

/**
 * 商品 API（Product BC 买家端）
 *
 * - GET /products/search   商品搜索/列表
 * - GET /products/{id}     商品详情
 * - GET /products/{id}/price-history 价格历史
 *
 * 响应拦截器已解包 ApiResponse.data，调用方拿到的就是业务负载。
 */
export const productApi = {
  /** 商品搜索/列表 */
  search(params: ProductQueryParams): Promise<ProductSearchResult> {
    return client.get<ProductSearchResult>('/products/search', { params }).then((r) => r.data)
  },

  /** 商品详情（含 SKU/属性/价格历史/评价摘要） */
  getDetail(id: string): Promise<ProductDetailDto> {
    return client.get<ProductDetailDto>(`/products/${id}`).then((r) => r.data)
  },

  /** 价格历史（独立端点，与详情内嵌数据同构） */
  getPriceHistory(id: string): Promise<PriceHistoryPointDto[]> {
    return client.get<PriceHistoryPointDto[]>(`/products/${id}/price-history`).then((r) => r.data)
  },
}
