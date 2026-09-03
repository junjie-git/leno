import type MockAdapter from 'axios-mock-adapter'
import type {
  CartDto,
  CartItemDto,
  CartPreviewRequestDto,
  CheckoutPreviewDto,
} from '@/modules/05-cart/types/cart.dto'
import type { AddressDto } from '@/modules/13-profile/types/profile.dto'
import {
  seedAddresses,
  seedCartItems,
  seedMyCoupons,
  seedPointsAccount,
  seedProductDetails,
} from '../data/seed'
import { fail, ok, parseBody } from './helpers'

/**
 * 购物车 handlers（Cart BC）
 *
 * - GET    /cart、POST /cart/items、PUT/DELETE /cart/items/{skuId}
 * - POST   /cart/items/select、PATCH /cart/selection、POST /cart/merge
 * - POST   /cart/preview（结算预览，与 /orders/preview 同构）
 */

/** 免邮门槛（分）与基础运费（分） */
const FREE_SHIPPING_THRESHOLD = 4900
const BASE_FREIGHT = 800
/** 积分抵扣规则：100 积分 = 1 元，最多抵应付金额的 50% */
const POINTS_PER_YUAN = 100
const MAX_POINTS_RATIO = 0.5

function cartView(): CartDto {
  const selected = seedCartItems.filter((i) => i.selected)
  return {
    items: seedCartItems,
    totalCount: seedCartItems.reduce((acc, i) => acc + i.quantity, 0),
    selectedCount: selected.reduce((acc, i) => acc + i.quantity, 0),
    selectedAmount: selected.reduce((acc, i) => acc + i.price * i.quantity, 0),
    hasInvalid: seedCartItems.some((i) => i.stock <= 0),
  }
}

/** 按请求构建结算预览（/cart/preview 与 /orders/preview 共用） */
export function buildCheckoutPreview(body: CartPreviewRequestDto): CheckoutPreviewDto {
  // 1. 组装结算条目
  let items: CartItemDto[]
  if (body.from === 'buyNow' && body.skuId) {
    const detail = seedProductDetails.find((p) => p.skus.some((s) => s.id === body.skuId))
    const sku = detail?.skus.find((s) => s.id === body.skuId)
    if (!detail || !sku) {
      throw new Error('SKU 不存在')
    }
    items = [
      {
        skuId: sku.id,
        spuId: detail.id,
        name: detail.name,
        image: sku.image,
        specs: sku.specs,
        price: sku.price,
        quantity: body.quantity ?? 1,
        selected: true,
        stock: sku.stock,
        shopId: detail.shopId,
        shopName: detail.shopName,
      },
    ]
  } else {
    items = seedCartItems.filter((i) => i.selected)
  }

  // 2. 按店铺分组
  const groups = new Map<string, { shopId: string; shopName: string; items: CartItemDto[] }>()
  for (const item of items) {
    const group = groups.get(item.shopId) ?? { shopId: item.shopId, shopName: item.shopName, items: [] }
    group.items.push(item)
    groups.set(item.shopId, group)
  }

  // 3. 金额计算
  const goodsAmount = items.reduce((acc, i) => acc + i.price * i.quantity, 0)
  const freight = goodsAmount >= FREE_SHIPPING_THRESHOLD || goodsAmount === 0 ? 0 : BASE_FREIGHT

  // 4. 可用优惠券（门槛 ≤ 商品总额；演示不区分类目范围）
  const usableCoupons = seedMyCoupons.filter((c) => c.status === 'Usable' && c.threshold <= goodsAmount)
  const selectedCoupon = body.couponId ? usableCoupons.find((c) => c.id === body.couponId || c.couponId === body.couponId) : undefined
  let couponDiscount = 0
  if (selectedCoupon) {
    if (selectedCoupon.type === 'Threshold') {
      couponDiscount = selectedCoupon.discount
    } else if (selectedCoupon.type === 'Shipping') {
      couponDiscount = freight
    }
  }

  // 5. 积分抵扣
  const maxDeductibleYuan = Math.floor((goodsAmount * MAX_POINTS_RATIO) / POINTS_PER_YUAN)
  const maxDeductiblePoints = Math.min(seedPointsAccount.balance, maxDeductibleYuan * POINTS_PER_YUAN)
  const pointsDiscount = body.usePoints ? Math.floor(maxDeductiblePoints / POINTS_PER_YUAN) * 100 : 0

  const payableAmount = Math.max(0, goodsAmount + freight - couponDiscount - pointsDiscount)

  // 6. 地址（显式指定 > 默认地址）
  const address: AddressDto | null =
    seedAddresses.find((a) => a.id === body.addressId) ?? seedAddresses.find((a) => a.isDefault) ?? null

  return {
    address,
    shopGroups: Array.from(groups.values()),
    amounts: { goodsAmount, freight, couponDiscount, pointsDiscount, payableAmount },
    availableCoupons: usableCoupons.map((c) => ({
      couponId: c.id,
      name: c.name,
      type: c.type,
      threshold: c.threshold,
      discount: c.discount,
      validTo: c.validTo,
    })),
    points: {
      available: seedPointsAccount.balance,
      maxDeductiblePoints,
      ruleText: `100 积分抵 1 元，最多可抵应付款的 50%`,
    },
  }
}

export function registerCartHandlers(mock: MockAdapter): void {
  // 购物车全量
  mock.onGet('/cart').reply(() => ok(cartView()))

  // 加入购物车（同 SKU 数量累加）
  mock.onPost('/cart/items').reply((config) => {
    const body = parseBody<{ skuId: string; quantity: number }>(config.data)
    const detail = seedProductDetails.find((p) => p.skus.some((s) => s.id === body.skuId))
    const sku = detail?.skus.find((s) => s.id === body.skuId)
    if (!detail || !sku) {
      return fail(40401, '商品不存在或已下架')
    }
    const quantity = Math.max(1, body.quantity ?? 1)
    const existing = seedCartItems.find((i) => i.skuId === body.skuId)
    if (existing) {
      existing.quantity = Math.min(existing.quantity + quantity, sku.stock)
      return ok(existing)
    }
    const item: CartItemDto = {
      skuId: sku.id,
      spuId: detail.id,
      name: detail.name,
      image: sku.image,
      specs: sku.specs,
      price: sku.price,
      quantity: Math.min(quantity, sku.stock),
      selected: true,
      stock: sku.stock,
      shopId: detail.shopId,
      shopName: detail.shopName,
    }
    seedCartItems.push(item)
    return ok(item)
  })

  // 修改数量
  mock.onPut(/\/cart\/items\/[\w-]+$/).reply((config) => {
    const skuId = config.url?.match(/\/cart\/items\/([\w-]+)$/)?.[1] ?? ''
    const body = parseBody<{ quantity: number }>(config.data)
    const item = seedCartItems.find((i) => i.skuId === skuId)
    if (!item) {
      return fail(40402, '购物车中不存在该商品')
    }
    const quantity = Math.max(1, body.quantity ?? 1)
    if (quantity > item.stock) {
      return fail(40403, `库存不足，最多可购买 ${item.stock} 件`)
    }
    item.quantity = quantity
    return ok(item)
  })

  // 删除条目
  mock.onDelete(/\/cart\/items\/[\w-]+$/).reply((config) => {
    const skuId = config.url?.match(/\/cart\/items\/([\w-]+)$/)?.[1] ?? ''
    const idx = seedCartItems.findIndex((i) => i.skuId === skuId)
    if (idx < 0) {
      return fail(40402, '购物车中不存在该商品')
    }
    seedCartItems.splice(idx, 1)
    return ok(null)
  })

  // 勾选/取消勾选
  mock.onPost('/cart/items/select').reply((config) => {
    const body = parseBody<{ skuIds: string[]; selected: boolean }>(config.data)
    for (const skuId of body.skuIds ?? []) {
      const item = seedCartItems.find((i) => i.skuId === skuId)
      if (item) {
        item.selected = body.selected
      }
    }
    return ok(cartView())
  })

  // 全选/取消全选
  mock.onPatch('/cart/selection').reply((config) => {
    const body = parseBody<{ selected: boolean }>(config.data)
    seedCartItems.forEach((i) => {
      i.selected = body.selected
    })
    return ok(cartView())
  })

  // 匿名车合并
  mock.onPost('/cart/merge').reply((config) => {
    const body = parseBody<{ items: Array<{ skuId: string; quantity: number }> }>(config.data)
    for (const entry of body.items ?? []) {
      const detail = seedProductDetails.find((p) => p.skus.some((s) => s.id === entry.skuId))
      const sku = detail?.skus.find((s) => s.id === entry.skuId)
      if (!detail || !sku) continue
      const existing = seedCartItems.find((i) => i.skuId === entry.skuId)
      if (existing) {
        existing.quantity = Math.min(existing.quantity + entry.quantity, sku.stock)
      } else {
        seedCartItems.push({
          skuId: sku.id,
          spuId: detail.id,
          name: detail.name,
          image: sku.image,
          specs: sku.specs,
          price: sku.price,
          quantity: Math.min(entry.quantity, sku.stock),
          selected: true,
          stock: sku.stock,
          shopId: detail.shopId,
          shopName: detail.shopName,
        })
      }
    }
    return ok(cartView())
  })

  // 结算预览
  mock.onPost('/cart/preview').reply((config) => {
    const body = parseBody<CartPreviewRequestDto>(config.data)
    const preview = buildCheckoutPreview({ ...body, from: 'cart' })
    if (preview.shopGroups.length === 0) {
      return fail(40404, '请先勾选要结算的商品')
    }
    return ok(preview)
  })
}
