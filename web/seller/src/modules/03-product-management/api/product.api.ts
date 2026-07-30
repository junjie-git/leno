import { client, withIdempotency } from '@/shared/http'
import type { PageResult } from '@/shared/types'
import type {
  ProductListItemDto,
  ProductDetailDto,
  CreateProductDto,
  UpdateProductDto,
  AddSkuDto,
  AdjustPriceDto,
  PriceChangeRecordDto,
  ActionReasonDto,
  ListProductsParams,
} from '../types/product.dto'

/**
 * 商品模块 API
 *
 * 与后端 ProductController / SkuController / PriceHistoryController 对接：
 * - GET    /products                       商品列表（分页 + 筛选）
 * - GET    /products/{id}                  商品详情
 * - POST   /products                       创建商品（带 Idempotency-Key）
 * - PUT    /products/{id}                  更新商品（带 Idempotency-Key + version 乐观锁）
 * - POST   /products/{id}/skus             新增 SKU（带 Idempotency-Key）
 * - POST   /products/{id}/skus/{skuId}/price  调整 SKU 价格（带 Idempotency-Key）
 * - POST   /products/{id}/submit           提交审核（带 Idempotency-Key）
 * - POST   /products/{id}/take-down        下架（带 Idempotency-Key + reason + version）
 * - POST   /products/{id}/republish        重新上架（带 Idempotency-Key）
 * - GET    /products/{id}/price-history    价格变更记录（可选 skuId 筛选）
 *
 * 响应拦截器已解包 ApiResponse.data，调用方拿到的就是业务负载，
 * 与 dashboard.api / auth.api 保持一致：api 函数内部通过 .then(r => r.data) 解包。
 */
export const productApi = {
  /**
   * 商品列表
   */
  list: (params: ListProductsParams): Promise<PageResult<ProductListItemDto>> =>
    client
      .get<PageResult<ProductListItemDto>>('/products', { params })
      .then((r) => r.data),

  /**
   * 商品详情
   */
  get: (id: string): Promise<ProductDetailDto> =>
    client.get<ProductDetailDto>(`/products/${id}`).then((r) => r.data),

  /**
   * 创建商品（幂等）
   */
  create: (body: CreateProductDto): Promise<ProductDetailDto> =>
    client
      .post<ProductDetailDto>('/products', body, withIdempotency())
      .then((r) => r.data),

  /**
   * 更新商品（幂等 + 乐观锁 version）
   */
  update: (id: string, body: UpdateProductDto): Promise<ProductDetailDto> =>
    client
      .put<ProductDetailDto>(`/products/${id}`, body, withIdempotency())
      .then((r) => r.data),

  /**
   * 新增 SKU（幂等）
   */
  addSku: (productId: string, body: AddSkuDto): Promise<ProductDetailDto> =>
    client
      .post<ProductDetailDto>(`/products/${productId}/skus`, body, withIdempotency())
      .then((r) => r.data),

  /**
   * 调整 SKU 价格（幂等）
   */
  adjustPrice: (
    productId: string,
    skuId: string,
    body: AdjustPriceDto,
  ): Promise<ProductDetailDto> =>
    client
      .post<ProductDetailDto>(
        `/products/${productId}/skus/${skuId}/price`,
        body,
        withIdempotency(),
      )
      .then((r) => r.data),

  /**
   * 提交审核（幂等）
   */
  submitForReview: (id: string): Promise<void> =>
    client
      .post<void>(`/products/${id}/submit`, null, withIdempotency())
      .then((r) => r.data),

  /**
   * 下架（幂等 + reason + version 乐观锁）
   */
  takeDown: (id: string, body: ActionReasonDto): Promise<void> =>
    client
      .post<void>(`/products/${id}/take-down`, body, withIdempotency())
      .then((r) => r.data),

  /**
   * 重新上架（幂等）
   */
  republish: (id: string): Promise<void> =>
    client
      .post<void>(`/products/${id}/republish`, null, withIdempotency())
      .then((r) => r.data),

  /**
   * 价格变更记录（可选 skuId 筛选）
   */
  getPriceHistory: (
    id: string,
    skuId?: string,
  ): Promise<PriceChangeRecordDto[]> =>
    client
      .get<PriceChangeRecordDto[]>(`/products/${id}/price-history`, {
        params: { skuId },
      })
      .then((r) => r.data),
}
