import { client } from '@/shared/http'
import type {
  AddCartItemRequestDto,
  CartDto,
  CartItemDto,
  CartPreviewRequestDto,
  CheckoutPreviewDto,
  MergeCartRequestDto,
  SelectCartItemsRequestDto,
  UpdateCartItemRequestDto,
} from '../types/cart.dto'

/**
 * 购物车 API（Cart BC）
 *
 * - GET    /cart                 购物车全量
 * - POST   /cart/items           加入购物车
 * - PUT    /cart/items/{skuId}   修改数量
 * - DELETE /cart/items/{skuId}   删除条目
 * - POST   /cart/items/select    勾选/取消勾选
 * - PATCH  /cart/selection       全选/取消全选
 * - POST   /cart/merge           匿名车合并
 * - POST   /cart/preview         结算预览
 *
 * 响应拦截器已解包 ApiResponse.data，调用方拿到的就是业务负载。
 */
export const cartApi = {
  /** 获取购物车 */
  getCart(): Promise<CartDto> {
    return client.get<CartDto>('/cart').then((r) => r.data)
  },

  /** 加入购物车 */
  addItem(body: AddCartItemRequestDto): Promise<CartItemDto> {
    return client.post<CartItemDto>('/cart/items', body).then((r) => r.data)
  },

  /** 修改数量 */
  updateQuantity(skuId: string, body: UpdateCartItemRequestDto): Promise<CartItemDto> {
    return client.put<CartItemDto>(`/cart/items/${skuId}`, body).then((r) => r.data)
  },

  /** 删除条目 */
  removeItem(skuId: string): Promise<null> {
    return client.delete<null>(`/cart/items/${skuId}`).then((r) => r.data)
  },

  /** 勾选/取消勾选（单个/批量） */
  selectItems(body: SelectCartItemsRequestDto): Promise<CartDto> {
    return client.post<CartDto>('/cart/items/select', body).then((r) => r.data)
  },

  /** 全选/取消全选（全量勾选） */
  updateSelection(selected: boolean): Promise<CartDto> {
    return client.patch<CartDto>('/cart/selection', { selected }).then((r) => r.data)
  },

  /** 匿名车合并（登录后调用） */
  merge(body: MergeCartRequestDto): Promise<CartDto> {
    return client.post<CartDto>('/cart/merge', body).then((r) => r.data)
  },

  /** 结算预览 */
  preview(body: CartPreviewRequestDto): Promise<CheckoutPreviewDto> {
    return client.post<CheckoutPreviewDto>('/cart/preview', body).then((r) => r.data)
  },
}
