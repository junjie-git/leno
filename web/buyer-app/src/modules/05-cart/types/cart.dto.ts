import type { AddressDto } from '@/modules/13-profile/types/profile.dto'

/**
 * 购物车域 DTO（Cart BC）
 *
 * 端点契约：
 * - GET    /api/cart                     购物车全量（按卖家分组由前端聚合）
 * - POST   /api/cart/items               加入购物车
 * - PUT    /api/cart/items/{skuId}       修改数量
 * - DELETE /api/cart/items/{skuId}       删除条目
 * - POST   /api/cart/items/select        单个/批量勾选
 * - PATCH  /api/cart/selection           全选/取消全选
 * - POST   /api/cart/merge               匿名车合并（登录后）
 * - POST   /api/cart/preview             结算预览（地址/分组/金额/优惠）
 */

/** 购物车条目 */
export interface CartItemDto {
  skuId: string
  spuId: string
  name: string
  image: string
  specs: string
  /** 单价（分） */
  price: number
  quantity: number
  selected: boolean
  stock: number
  shopId: string
  shopName: string
}

/** 购物车 */
export interface CartDto {
  items: CartItemDto[]
  /** 总件数（含未勾选） */
  totalCount: number
  /** 勾选件数 */
  selectedCount: number
  /** 勾选商品总额（分） */
  selectedAmount: number
  /** 是否存在失效条目（下架/超库存） */
  hasInvalid: boolean
}

/** 加入购物车请求 */
export interface AddCartItemRequestDto {
  skuId: string
  quantity: number
}

/** 修改数量请求 */
export interface UpdateCartItemRequestDto {
  quantity: number
}

/** 勾选请求（单个/批量） */
export interface SelectCartItemsRequestDto {
  skuIds: string[]
  selected: boolean
}

/** 匿名车合并请求（登录后） */
export interface MergeCartRequestDto {
  items: Array<{ skuId: string; quantity: number }>
}

/** 结算预览中的金额明细 */
export interface CheckoutAmountsDto {
  /** 商品总额（分） */
  goodsAmount: number
  /** 运费（分） */
  freight: number
  /** 优惠券抵扣（分） */
  couponDiscount: number
  /** 积分抵扣（分） */
  pointsDiscount: number
  /** 应付总额（分） */
  payableAmount: number
}

/** 结算预览中的积分抵扣信息 */
export interface PointsDeductionDto {
  /** 当前可用积分 */
  available: number
  /** 本次最多可抵扣积分 */
  maxDeductiblePoints: number
  /** 抵扣规则文案，如「100 积分抵 1 元」 */
  ruleText: string
}

/** 结算预览（POST /cart/preview 与 POST /orders/preview 同构） */
export interface CheckoutPreviewDto {
  address: AddressDto | null
  shopGroups: Array<{
    shopId: string
    shopName: string
    items: CartItemDto[]
  }>
  amounts: CheckoutAmountsDto
  /** 可用优惠券（模板） */
  availableCoupons: Array<{
    couponId: string
    name: string
    type: 'Threshold' | 'Shipping' | 'Discount'
    threshold: number
    discount: number
    validTo: string
  }>
  points: PointsDeductionDto
}

/** 结算预览请求 */
export interface CartPreviewRequestDto {
  /** 购物车结算（勾选项）或立即购买 */
  from: 'cart' | 'buyNow'
  /** from = buyNow 时必填 */
  skuId?: string
  quantity?: number
  /** 指定收货地址（默认取默认地址） */
  addressId?: string
  /** 指定使用优惠券 */
  couponId?: string | null
  /** 是否使用积分抵扣 */
  usePoints?: boolean
}
