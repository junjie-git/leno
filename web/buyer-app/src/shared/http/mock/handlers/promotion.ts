import type MockAdapter from 'axios-mock-adapter'
import type { CouponStatus, MyCouponDto, SeckillPlaceRequestDto } from '@/modules/08-promotion/types/promotion.dto'
import type { OrderItemDto } from '@/modules/06-order/types/order.dto'
import {
  runtime,
  seedAddresses,
  seedAvailableCoupons,
  seedMyCoupons,
  seedNotifications,
  seedOrders,
  seedProductDetails,
  seedSeckillActivities,
  seedUser,
} from '../data/seed'
import { fail, ok, parseBody, queryParams } from './helpers'

/**
 * 促销 handlers（Promotion BC 买家端：优惠券 + 秒杀）
 *
 * - GET  /coupons/available、/coupons/mine、/coupons/claimable
 * - POST /coupons/{couponId}/receive
 * - GET  /seckill/activities、/seckill/activities/{activityId}
 * - POST /seckill/activities/{activityId}/place（秒杀下单）
 */

export function registerPromotionHandlers(mock: MockAdapter): void {
  // 可领优惠券
  mock.onGet('/coupons/available').reply(() => ok(seedAvailableCoupons))

  // 我的优惠券（status 筛选 + 过期状态实时计算）
  mock.onGet('/coupons/mine').reply((config) => {
    const params = queryParams(config)
    const now = Date.now()
    const list = seedMyCoupons.map((c) => {
      if (c.status === 'Usable' && new Date(c.validTo).getTime() < now) {
        return { ...c, status: 'Expired' as CouponStatus }
      }
      return c
    })
    if (params.status) {
      return ok(list.filter((c) => c.status === params.status))
    }
    return ok(list)
  })

  // 积分可兑换券
  mock.onGet('/coupons/claimable').reply(() =>
    ok(seedAvailableCoupons.filter((c) => c.type === 'Threshold' && c.threshold >= 100)),
  )

  // 领取优惠券
  mock.onPost(/\/coupons\/[\w-]+\/receive$/).reply((config) => {
    const couponId = config.url?.match(/\/coupons\/([\w-]+)\/receive$/)?.[1] ?? ''
    const template = seedAvailableCoupons.find((c) => c.couponId === couponId)
    if (!template) {
      return fail(40440, '优惠券不存在或已下架')
    }
    if (template.remainCount <= 0) {
      return fail(40441, '来晚了，优惠券已被领完')
    }
    if (template.received) {
      return fail(40442, '您已领取过该优惠券')
    }
    template.remainCount -= 1
    template.received = true
    const coupon: MyCouponDto = {
      id: `mc-${Date.now()}`,
      couponId: template.couponId,
      name: template.name,
      type: template.type,
      threshold: template.threshold,
      discount: template.discount,
      status: 'Usable',
      validFrom: new Date().toISOString(),
      validTo: new Date(Date.now() + template.validDays * 86_400_000).toISOString(),
      scopeText: template.scopeText,
    }
    seedMyCoupons.unshift(coupon)
    return ok(null)
  })

  // 秒杀活动列表
  mock.onGet('/seckill/activities').reply(() => {
    // 实时计算状态（已到开始时间转 Active，已过结束时间转 Ended）
    const now = Date.now()
    const list = seedSeckillActivities.map((a) => {
      const status =
        now < new Date(a.startTime).getTime() ? 'Upcoming' : now > new Date(a.endTime).getTime() ? 'Ended' : 'Active'
      return { ...a, status }
    })
    return ok(list)
  })

  // 秒杀活动详情
  mock.onGet(/\/seckill\/activities\/[\w-]+$/).reply((config) => {
    const id = config.url?.match(/\/seckill\/activities\/([\w-]+)$/)?.[1] ?? ''
    const activity = seedSeckillActivities.find((a) => a.id === id)
    if (!activity) {
      return fail(40450, '秒杀活动不存在或已结束')
    }
    const now = Date.now()
    const status =
      now < new Date(activity.startTime).getTime()
        ? 'Upcoming'
        : now > new Date(activity.endTime).getTime()
          ? 'Ended'
          : 'Active'
    return ok({ ...activity, status })
  })

  // 秒杀下单
  mock.onPost(/\/seckill\/activities\/[\w-]+\/place$/).reply((config) => {
    const activityId = config.url?.match(/\/seckill\/activities\/([\w-]+)\/place$/)?.[1] ?? ''
    const activity = seedSeckillActivities.find((a) => a.id === activityId)
    if (!activity) {
      return fail(40450, '秒杀活动不存在或已结束')
    }
    const now = Date.now()
    if (now < new Date(activity.startTime).getTime()) {
      return fail(40451, '秒杀尚未开始，请准时蹲点')
    }
    if (now > new Date(activity.endTime).getTime()) {
      return fail(40452, '秒杀已结束')
    }
    const body = parseBody<SeckillPlaceRequestDto>(config.data)
    const item = activity.items.find((i) => i.skuId === body.skuId)
    if (!item) {
      return fail(40453, '秒杀商品不存在')
    }
    if (item.stock <= 0) {
      return fail(40454, '手慢了，该商品已售罄')
    }
    const quantity = Math.max(1, body.quantity ?? 1)
    if (quantity > item.limitPerUser) {
      return fail(40455, `每人限购 ${item.limitPerUser} 件`)
    }
    if (quantity > item.stock) {
      return fail(40456, `库存不足，仅剩 ${item.stock} 件`)
    }
    // 扣减秒杀库存
    item.stock -= quantity
    // 扣减 SKU 总库存
    const detail = seedProductDetails.find((p) => p.id === item.spuId)
    const sku = detail?.skus.find((s) => s.id === item.skuId)
    if (sku) {
      sku.stock = Math.max(0, sku.stock - quantity)
    }
    // 生成秒杀订单（秒杀价结算，不走优惠券/积分）
    const address =
      seedAddresses.find((a) => a.id === body.addressId) ?? seedAddresses.find((a) => a.isDefault)
    runtime.orderSeq += 1
    const orderItem: OrderItemDto = {
      orderLineId: `ol-seckill-${runtime.orderSeq}`,
      spuId: item.spuId,
      skuId: item.skuId,
      name: item.name,
      image: item.image,
      specs: item.specs,
      price: item.seckillPrice,
      quantity,
      reviewed: false,
    }
    const payable = item.seckillPrice * quantity
    const order = {
      id: `so-mock-${runtime.orderSeq}`,
      orderNo: nextOrderNo(),
      status: 'PendingPayment' as const,
      items: [orderItem],
      shopId: detail?.shopId ?? 'shop-1001',
      shopName: detail?.shopName ?? 'Leno 秒杀专区',
      amounts: { goodsAmount: payable, freight: 0, couponDiscount: 0, pointsDiscount: 0, payableAmount: payable },
      address: {
        receiver: address?.receiver ?? seedUser.nickname,
        phone: address ? `${address.phone.slice(0, 3)}****${address.phone.slice(7)}` : '',
        fullAddress: address ? `${address.province}${address.city}${address.district}${address.detail}` : '',
      },
      createdAt: new Date().toISOString(),
      payDeadline: new Date(Date.now() + 15 * 60_000).toISOString(),
      remark: `秒杀订单（${activity.name}）`,
    }
    seedOrders.unshift(order)
    runtime.notificationSeq += 1
    seedNotifications.unshift({
      id: `nt-${runtime.notificationSeq}`,
      type: 'Order',
      title: '秒杀订单创建成功',
      content: `恭喜！您已成功抢到「${item.name}」，请在 15 分钟内完成支付，超时订单将自动取消。`,
      isRead: false,
      createdAt: new Date().toISOString(),
      linkUrl: `/order/${order.id}`,
    })
    return ok(order)
  })
}

/** 生成秒杀订单号（与 order handler 独立，避免循环依赖） */
function nextOrderNo(): string {
  runtime.orderSeq += 1
  const now = new Date()
  const pad = (n: number, len = 2) => String(n).padStart(len, '0')
  const stamp = `${now.getFullYear()}${pad(now.getMonth() + 1)}${pad(now.getDate())}${pad(now.getHours())}${pad(now.getMinutes())}${pad(now.getSeconds())}`
  return `${stamp}${pad(runtime.orderSeq, 4)}`
}
