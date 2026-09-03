import type MockAdapter from 'axios-mock-adapter'
import type {
  ExchangeCouponRequestDto,
  PointsLedgerType,
} from '@/modules/11-points-membership/types/points.dto'
import {
  runtime,
  seedMyCoupons,
  seedNotifications,
  seedPointsAccount,
  seedPointsLedger,
  seedPointsTasks,
  seedUser,
} from '../data/seed'
import { fail, ok, parseBody, queryParams } from './helpers'

/**
 * 积分 handlers（Points 域）
 *
 * - GET  /points/account、/points/ledger
 * - POST /points/check-in
 * - GET  /points/tasks、POST /points/tasks/{taskId}/complete
 * - POST /points/exchange-coupon
 */

export function registerPointsHandlers(mock: MockAdapter): void {
  // 积分账户
  mock.onGet('/points/account').reply(() => ok(seedPointsAccount))

  // 积分流水（type 筛选）
  mock.onGet('/points/ledger').reply((config) => {
    const params = queryParams(config)
    let list = [...seedPointsLedger]
    if (params.type) {
      list = list.filter((l) => l.type === (params.type as PointsLedgerType))
    }
    return ok(list)
  })

  // 每日签到
  mock.onPost('/points/check-in').reply(() => {
    if (seedPointsAccount.checkedInToday) {
      return fail(40490, '今天已签到，明天再来吧')
    }
    const streak = seedPointsAccount.checkInStreakDays + 1
    const earned = 5 + (streak % 7 === 0 ? 20 : 0)
    seedPointsAccount.checkedInToday = true
    seedPointsAccount.checkInStreakDays = streak
    seedPointsAccount.balance += earned
    seedPointsAccount.totalEarned += earned
    seedUser.points = seedPointsAccount.balance
    seedPointsLedger.unshift({
      id: `pl-${Date.now()}`,
      type: 'Earn',
      points: earned,
      balanceAfter: seedPointsAccount.balance,
      description: `每日签到（连续 ${streak} 天）`,
      createdAt: new Date().toISOString(),
    })
    // 同步签到任务状态
    const checkInTask = seedPointsTasks.find((t) => t.action === 'CheckIn')
    if (checkInTask && checkInTask.status === 'Pending') {
      checkInTask.status = 'Completed'
      checkInTask.completedAt = new Date().toISOString()
    }
    return ok({
      earnedPoints: earned,
      streakDays: streak,
      balanceAfter: seedPointsAccount.balance,
    })
  })

  // 任务中心
  mock.onGet('/points/tasks').reply(() => ok(seedPointsTasks))

  // 完成任务
  mock.onPost(/\/points\/tasks\/[\w-]+\/complete$/).reply((config) => {
    const taskId = config.url?.match(/\/points\/tasks\/([\w-]+)\/complete$/)?.[1] ?? ''
    const task = seedPointsTasks.find((t) => t.id === taskId)
    if (!task) {
      return fail(40491, '任务不存在')
    }
    if (task.status === 'Completed') {
      return fail(40492, '任务已完成，请勿重复领取')
    }
    task.status = 'Completed'
    task.completedAt = new Date().toISOString()
    seedPointsAccount.balance += task.points
    seedPointsAccount.totalEarned += task.points
    seedUser.points = seedPointsAccount.balance
    seedPointsLedger.unshift({
      id: `pl-${Date.now()}`,
      type: 'Earn',
      points: task.points,
      balanceAfter: seedPointsAccount.balance,
      description: `${task.name}任务奖励`,
      createdAt: new Date().toISOString(),
    })
    return ok(task)
  })

  // 积分兑换优惠券
  mock.onPost('/points/exchange-coupon').reply((config) => {
    const body = parseBody<ExchangeCouponRequestDto>(config.data)
    // 可兑换券面值（演示规则：满 N 减 M 券消耗 M × 25 积分）
    const catalog: Record<string, { name: string; threshold: number; discount: number; points: number }> = {
      'ct-99-15': { name: '食品生鲜满 99 减 15 券', threshold: 9900, discount: 1500, points: 375 },
      'ct-300-50': { name: '满 300 减 50 元券', threshold: 30000, discount: 5000, points: 1250 },
      'ct-500-80': { name: '数码专享满 500 减 80 券', threshold: 50000, discount: 8000, points: 2000 },
    }
    const coupon = catalog[body.couponId]
    if (!coupon) {
      return fail(40493, '该优惠券暂不支持积分兑换')
    }
    if (body.points !== coupon.points) {
      return fail(40494, `兑换该券需要 ${coupon.points} 积分`)
    }
    if (seedPointsAccount.balance < coupon.points) {
      return fail(40495, '积分不足，去看看任务中心赚积分吧')
    }
    seedPointsAccount.balance -= coupon.points
    seedPointsAccount.totalSpent += coupon.points
    seedUser.points = seedPointsAccount.balance
    seedPointsLedger.unshift({
      id: `pl-${Date.now()}`,
      type: 'Spend',
      points: -coupon.points,
      balanceAfter: seedPointsAccount.balance,
      description: `兑换优惠券「${coupon.name}」`,
      createdAt: new Date().toISOString(),
    })
    const validTo = new Date(Date.now() + 30 * 86_400_000).toISOString()
    seedMyCoupons.unshift({
      id: `mc-${Date.now()}`,
      couponId: body.couponId,
      name: coupon.name,
      type: 'Threshold',
      threshold: coupon.threshold,
      discount: coupon.discount,
      status: 'Usable',
      validFrom: new Date().toISOString(),
      validTo,
      scopeText: '积分兑换 · 全场通用',
    })
    runtime.notificationSeq += 1
    seedNotifications.unshift({
      id: `nt-${runtime.notificationSeq}`,
      type: 'Points',
      title: '积分兑换成功',
      content: `您已用 ${coupon.points} 积分兑换「${coupon.name}」，有效期至 ${validTo.slice(0, 10)}，可在我的优惠券中查看。`,
      isRead: false,
      createdAt: new Date().toISOString(),
      linkUrl: '/coupons/mine',
    })
    return ok({
      success: true,
      couponName: coupon.name,
      validTo,
      balanceAfter: seedPointsAccount.balance,
    })
  })
}
