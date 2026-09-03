import type MockAdapter from 'axios-mock-adapter'
import {
  seedMemberLevels,
  seedMemberProfile,
  seedMembershipPackages,
  seedNotifications,
  runtime,
} from '../data/seed'
import { fail, ok } from './helpers'

/**
 * 会员 handlers（Membership 域）
 *
 * - GET  /members/me、/members/levels
 * - GET  /membership-packages
 * - POST /membership-packages/{packageId}/subscribe（订阅：生成待支付订单）
 */

export function registerMemberHandlers(mock: MockAdapter): void {
  // 我的会员信息
  mock.onGet('/members/me').reply(() => ok(seedMemberProfile))

  // 会员等级体系
  mock.onGet('/members/levels').reply(() => ok(seedMemberLevels))

  // 付费会员套餐
  mock.onGet('/membership-packages').reply(() => ok(seedMembershipPackages))

  // 订阅会员套餐
  mock.onPost(/\/membership-packages\/[\w-]+\/subscribe$/).reply((config) => {
    const packageId = config.url?.match(/\/membership-packages\/([\w-]+)\/subscribe$/)?.[1] ?? ''
    const pkg = seedMembershipPackages.find((p) => p.id === packageId)
    if (!pkg) {
      return fail(40496, '会员套餐不存在或已下架')
    }
    // 生成订阅订单（待支付），支付成功后开通（演示：直接开通）
    runtime.orderSeq += 1
    const orderNo = `${new Date().toISOString().replace(/\D/g, '').slice(0, 14)}${String(runtime.orderSeq).padStart(4, '0')}`
    const expireAt = new Date(Date.now() + pkg.durationDays * 86_400_000).toISOString()
    seedMemberProfile.isPremium = true
    seedMemberProfile.premiumExpireAt = expireAt
    seedMemberProfile.benefits = Array.from(new Set([...seedMemberProfile.benefits, ...pkg.benefits]))
    runtime.notificationSeq += 1
    seedNotifications.unshift({
      id: `nt-${runtime.notificationSeq}`,
      type: 'System',
      title: '会员开通成功',
      content: `您已成功订阅「${pkg.name}」，订单号 ${orderNo}。会员权益已生效，有效期至 ${expireAt.slice(0, 10)}。`,
      isRead: false,
      createdAt: new Date().toISOString(),
      linkUrl: '/member/level',
    })
    return ok({
      success: true,
      orderId: `so-mock-${runtime.orderSeq}`,
      premiumExpireAt: expireAt,
    })
  })
}
