import type MockAdapter from 'axios-mock-adapter'
import type { CheckoutAmountsDto } from '@/modules/05-cart/types/cart.dto'
import type {
  BuyNowRequestDto,
  CreateOrderRequestDto,
  OrderDto,
  OrderItemDto,
  OrderPreviewRequestDto,
  OrderStatus,
} from '@/modules/06-order/types/order.dto'
import type { ReviewDto, SubmitReviewsRequestDto } from '@/modules/09-review/types/review.dto'
import {
  runtime,
  seedAddresses,
  seedCartItems,
  seedLogisticsTraces,
  seedMyCoupons,
  seedMyReviews,
  seedNotifications,
  seedOrders,
  seedPointsAccount,
  seedPointsLedger,
  seedProductDetails,
  seedUser,
} from '../data/seed'
import { fail, ok, paginate, parseBody, queryParams } from './helpers'
import { buildCheckoutPreview } from './cart'

/**
 * 订单 handlers（Order BC 买家端）
 *
 * - GET  /orders（状态筛选 + 分页）、/orders/{id}、/orders/{id}/logistics
 * - POST /orders（购物车下单）、/orders/buy-now、/orders/preview
 * - POST /orders/{id}/cancel、/orders/{id}/confirm
 * - GET  /addresses（下单地址别名端点）
 * - POST /orders/{orderId}/reviews（提交订单评价）
 */

/** 状态筛选 → 订单状态集合（待收货 = 已发货；售后 = 售后中/退款中） */
const STATUS_FILTER: Record<string, OrderStatus[]> = {
  PendingPayment: ['PendingPayment'],
  Paid: ['Paid'],
  Shipped: ['Shipped'],
  Completed: ['Completed'],
  AfterSales: ['AfterSales', 'Refunding', 'Refunded'],
}

/** 生成订单号：日期时间 + 4 位流水 */
function nextOrderNo(): string {
  runtime.orderSeq += 1
  const now = new Date()
  const pad = (n: number, len = 2) => String(n).padStart(len, '0')
  const stamp = `${now.getFullYear()}${pad(now.getMonth() + 1)}${pad(now.getDate())}${pad(now.getHours())}${pad(now.getMinutes())}${pad(now.getSeconds())}`
  return `${stamp}${pad(runtime.orderSeq, 4)}`
}

/** 创建订单（购物车 / 立即购买共用） */
function createOrder(
  items: OrderItemDto[],
  amounts: CheckoutAmountsDto,
  addressId: string,
  remark?: string,
): OrderDto {
  const address = seedAddresses.find((a) => a.id === addressId) ?? seedAddresses.find((a) => a.isDefault)
  const first = items[0]
  const now = new Date()
  const order: OrderDto = {
    id: `so-mock-${runtime.orderSeq}`,
    orderNo: nextOrderNo(),
    status: 'PendingPayment',
    items,
    shopId: first.spuId === 'spu-102' || first.spuId === 'spu-111' ? 'shop-1001' : 'shop-1005',
    shopName: items.length === 1
      ? seedProductDetails.find((p) => p.id === first.spuId)?.shopName ?? 'Leno 自营'
      : 'Leno 多商品订单',
    amounts,
    address: {
      receiver: address?.receiver ?? seedUser.nickname,
      phone: address ? `${address.phone.slice(0, 3)}****${address.phone.slice(7)}` : '',
      fullAddress: address ? `${address.province}${address.city}${address.district}${address.detail}` : '',
    },
    createdAt: now.toISOString(),
    payDeadline: new Date(now.getTime() + 30 * 60_000).toISOString(),
    remark,
  }
  seedOrders.unshift(order)
  return order
}

/** 支付成功联动：订单转 Paid + 积分赠送 + 通知（payment handler 调用） */
export function markOrderPaid(orderId: string): OrderDto | undefined {
  const order = seedOrders.find((o) => o.id === orderId)
  if (!order || order.status !== 'PendingPayment') return order
  order.status = 'Paid'
  order.paidAt = new Date().toISOString()
  // 赠送积分：每满 10 元赠 1 分
  const earned = Math.floor(order.amounts.payableAmount / 1000)
  if (earned > 0) {
    seedPointsAccount.balance += earned
    seedPointsAccount.totalEarned += earned
    seedUser.points = seedPointsAccount.balance
    seedPointsLedger.unshift({
      id: `pl-${Date.now()}`,
      type: 'Earn',
      points: earned,
      balanceAfter: seedPointsAccount.balance,
      description: `订单 ${order.orderNo} 支付成功赠送`,
      createdAt: new Date().toISOString(),
    })
  }
  runtime.notificationSeq += 1
  seedNotifications.unshift({
    id: `nt-${runtime.notificationSeq}`,
    type: 'Order',
    title: '支付成功',
    content: `订单 ${order.orderNo} 支付成功，实付 ¥${(order.amounts.payableAmount / 100).toFixed(2)}。商家将在 48 小时内为您发货。`,
    isRead: false,
    createdAt: new Date().toISOString(),
    linkUrl: `/order/${order.id}`,
  })
  return order
}

export function registerOrderHandlers(mock: MockAdapter): void {
  // 订单列表
  mock.onGet('/orders').reply((config) => {
    const params = queryParams(config)
    let list = [...seedOrders]
    if (params.status) {
      const statuses = STATUS_FILTER[params.status] ?? [params.status as OrderStatus]
      list = list.filter((o) => statuses.includes(o.status))
    }
    // 列表默认倒序（新订单在前）
    return ok(paginate(list, Number(params.page ?? 1), Number(params.pageSize ?? 10)))
  })

  // 订单详情
  mock.onGet(/\/orders\/[\w-]+$/).reply((config) => {
    const id = config.url?.match(/\/orders\/([\w-]+)$/)?.[1] ?? ''
    const order = seedOrders.find((o) => o.id === id)
    if (!order) {
      return fail(40410, '订单不存在')
    }
    return ok(order)
  })

  // 下单预览（与 /cart/preview 同构）
  mock.onPost('/orders/preview').reply((config) => {
    const body = parseBody<OrderPreviewRequestDto>(config.data)
    const preview = buildCheckoutPreview({
      from: body.from ?? 'cart',
      skuId: body.skuId,
      quantity: body.quantity,
      addressId: body.addressId,
      couponId: body.couponId,
      usePoints: body.usePoints,
    })
    if (preview.shopGroups.length === 0) {
      return fail(40404, '请先勾选要结算的商品')
    }
    return ok(preview)
  })

  // 购物车创建订单
  mock.onPost('/orders').reply((config) => {
    const body = parseBody<CreateOrderRequestDto>(config.data)
    const preview = buildCheckoutPreview({
      from: 'cart',
      addressId: body.addressId,
      couponId: body.couponId,
      usePoints: body.usePoints,
    })
    if (preview.shopGroups.length === 0) {
      return fail(40404, '请先勾选要结算的商品')
    }
    // 组装订单行
    const items: OrderItemDto[] = []
    for (const group of preview.shopGroups) {
      for (const item of group.items) {
        items.push({
          orderLineId: `ol-${runtime.orderSeq}-${items.length + 1}`,
          spuId: item.spuId,
          skuId: item.skuId,
          name: item.name,
          image: item.image,
          specs: item.specs,
          price: item.price,
          quantity: item.quantity,
          reviewed: false,
        })
      }
    }
    // 扣减积分抵扣
    if (body.usePoints && preview.amounts.pointsDiscount > 0) {
      const spend = preview.points.maxDeductiblePoints
      seedPointsAccount.balance -= spend
      seedPointsAccount.totalSpent += spend
      seedUser.points = seedPointsAccount.balance
      seedPointsLedger.unshift({
        id: `pl-${Date.now()}`,
        type: 'Spend',
        points: -spend,
        balanceAfter: seedPointsAccount.balance,
        description: `订单下单积分抵扣`,
        createdAt: new Date().toISOString(),
      })
    }
    const order = createOrder(items, preview.amounts, body.addressId, body.remark)
    // 移除已结算的购物车条目
    const settledSkuIds = new Set(items.map((i) => i.skuId))
    for (let i = seedCartItems.length - 1; i >= 0; i--) {
      if (settledSkuIds.has(seedCartItems[i].skuId)) {
        seedCartItems.splice(i, 1)
      }
    }
    // 使用过的优惠券置为 Used
    if (body.couponId) {
      const coupon = seedMyCoupons.find((c) => c.id === body.couponId || c.couponId === body.couponId)
      if (coupon) coupon.status = 'Used'
    }
    return ok(order)
  })

  // 立即购买下单
  mock.onPost('/orders/buy-now').reply((config) => {
    const body = parseBody<BuyNowRequestDto>(config.data)
    const preview = buildCheckoutPreview({
      from: 'buyNow',
      skuId: body.skuId,
      quantity: body.quantity,
      addressId: body.addressId,
      couponId: body.couponId,
      usePoints: body.usePoints,
    })
    const item = preview.shopGroups[0]?.items[0]
    if (!item) {
      return fail(40401, '商品不存在或已下架')
    }
    const orderItem: OrderItemDto = {
      orderLineId: `ol-${runtime.orderSeq}-1`,
      spuId: item.spuId,
      skuId: item.skuId,
      name: item.name,
      image: item.image,
      specs: item.specs,
      price: item.price,
      quantity: item.quantity,
      reviewed: false,
    }
    if (body.usePoints && preview.amounts.pointsDiscount > 0) {
      const spend = preview.points.maxDeductiblePoints
      seedPointsAccount.balance -= spend
      seedPointsAccount.totalSpent += spend
      seedUser.points = seedPointsAccount.balance
      seedPointsLedger.unshift({
        id: `pl-${Date.now()}`,
        type: 'Spend',
        points: -spend,
        balanceAfter: seedPointsAccount.balance,
        description: `订单下单积分抵扣`,
        createdAt: new Date().toISOString(),
      })
    }
    const order = createOrder([orderItem], preview.amounts, body.addressId, body.remark)
    if (body.couponId) {
      const coupon = seedMyCoupons.find((c) => c.id === body.couponId || c.couponId === body.couponId)
      if (coupon) coupon.status = 'Used'
    }
    return ok(order)
  })

  // 取消订单
  mock.onPost(/\/orders\/[\w-]+\/cancel$/).reply((config) => {
    const id = config.url?.match(/\/orders\/([\w-]+)\/cancel$/)?.[1] ?? ''
    const order = seedOrders.find((o) => o.id === id)
    if (!order) {
      return fail(40410, '订单不存在')
    }
    if (order.status !== 'PendingPayment') {
      return fail(40411, '当前状态不可取消')
    }
    order.status = 'Cancelled'
    order.cancelledAt = new Date().toISOString()
    order.cancelReason = '买家主动取消'
    return ok(null)
  })

  // 确认收货
  mock.onPost(/\/orders\/[\w-]+\/confirm$/).reply((config) => {
    const id = config.url?.match(/\/orders\/([\w-]+)\/confirm$/)?.[1] ?? ''
    const order = seedOrders.find((o) => o.id === id)
    if (!order) {
      return fail(40410, '订单不存在')
    }
    if (order.status !== 'Shipped') {
      return fail(40412, '当前状态不可确认收货')
    }
    order.status = 'Completed'
    order.completedAt = new Date().toISOString()
    return ok(order)
  })

  // 物流轨迹
  mock.onGet(/\/orders\/[\w-]+\/logistics$/).reply((config) => {
    const id = config.url?.match(/\/orders\/([\w-]+)\/logistics$/)?.[1] ?? ''
    const order = seedOrders.find((o) => o.id === id)
    if (!order) {
      return fail(40410, '订单不存在')
    }
    const trace = seedLogisticsTraces[id]
    if (trace) {
      return ok(trace)
    }
    // 未发货订单返回占位轨迹（已下单节点）
    return ok({
      logisticsCompany: order.logisticsCompany ?? '待分配',
      logisticsNo: order.logisticsNo ?? '',
      traces: [
        {
          time: order.paidAt ?? order.createdAt,
          description: '商家已接单，等待拣货发货',
          status: '已下单',
        },
      ],
    })
  })

  // 下单可用地址（别名端点）
  mock.onGet('/addresses').reply(() => ok(seedAddresses))

  // 提交订单评价（按订单行批量）
  mock.onPost(/\/orders\/[\w-]+\/reviews$/).reply((config) => {
    const orderId = config.url?.match(/\/orders\/([\w-]+)\/reviews$/)?.[1] ?? ''
    const order = seedOrders.find((o) => o.id === orderId)
    if (!order) {
      return fail(40410, '订单不存在')
    }
    const body = parseBody<SubmitReviewsRequestDto>(config.data)
    if (!body.reviews || body.reviews.length === 0) {
      return fail(40420, '请至少评价一件商品')
    }
    const created: ReviewDto[] = []
    for (const r of body.reviews) {
      const line = order.items.find((i) => i.orderLineId === r.orderLineId)
      if (!line) {
        return fail(40421, `订单行 ${r.orderLineId} 不存在`)
      }
      if (r.rating < 1 || r.rating > 5) {
        return fail(40422, '评分需为 1-5 星')
      }
      if (!r.content || r.content.trim().length < 5) {
        return fail(40423, '评价内容至少 5 个字')
      }
      line.reviewed = true
      const review: ReviewDto = {
        id: `rev-${Date.now()}-${created.length}`,
        orderLineId: r.orderLineId,
        spuId: line.spuId,
        nickname: r.isAnonymous ? `${seedUser.nickname.charAt(0)}**` : seedUser.nickname,
        avatar: '',
        skuSpecs: line.specs,
        rating: r.rating,
        content: r.content,
        images: r.images ?? [],
        createdAt: new Date().toISOString(),
      }
      seedMyReviews.unshift(review)
      created.push(review)
      // 评价奖励积分
      seedPointsAccount.balance += 10
      seedPointsAccount.totalEarned += 10
      seedUser.points = seedPointsAccount.balance
      seedPointsLedger.unshift({
        id: `pl-${Date.now()}-${created.length}`,
        type: 'Earn',
        points: 10,
        balanceAfter: seedPointsAccount.balance,
        description: `评价晒单奖励（${line.name.slice(0, 10)}…）`,
        createdAt: new Date().toISOString(),
      })
    }
    return ok(created)
  })
}
