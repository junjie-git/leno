import type MockAdapter from 'axios-mock-adapter'
import type { CreatePaymentRequestDto, PaymentChannel, PaymentDto } from '@/modules/07-payment/types/payment.dto'
import { seedOrders, seedPayments } from '../data/seed'
import { fail, ok, parseBody } from './helpers'
import { markOrderPaid } from './order'

/**
 * 支付 handlers（Payment BC）
 *
 * - POST /payments（发起支付；演示 1.5s 内自动以「成功」结算 → 订单转 Paid）
 * - GET  /payments/result/{orderId}（轮询支付结果）
 */

export function registerPaymentHandlers(mock: MockAdapter): void {
  // 发起支付
  mock.onPost('/payments').reply((config) => {
    const body = parseBody<CreatePaymentRequestDto & { orderId?: string }>(config.data)
    // 兼容 query 形式：POST /payments?orderId=xxx
    const orderId = body.orderId ?? config.url?.match(/[?&]orderId=([\w-]+)/)?.[1]
    if (!orderId) {
      return fail(40430, '缺少订单号')
    }
    const order = seedOrders.find((o) => o.id === orderId)
    if (!order) {
      return fail(40410, '订单不存在')
    }
    if (order.status === 'Cancelled') {
      return fail(40431, '订单已取消，无法支付')
    }
    if (order.status !== 'PendingPayment') {
      // 已支付订单幂等返回已有支付单
      const existing = seedPayments.find((p) => p.orderId === orderId)
      if (existing) return ok(existing)
    }
    const channel = (body.channel ?? 'Alipay') as PaymentChannel
    const now = new Date()
    const payment: PaymentDto = {
      id: `pay-${Date.now()}`,
      orderId,
      channel,
      amount: order.amounts.payableAmount,
      status: 'Success',
      createdAt: now.toISOString(),
      expireAt: order.payDeadline ?? new Date(now.getTime() + 30 * 60_000).toISOString(),
      paidAt: now.toISOString(),
      channelTradeNo: `${channel.toUpperCase()}${Date.now()}`,
    }
    seedPayments.unshift(payment)
    // 演示：支付即成功（真实渠道为异步回调，此处直接联动订单状态）
    markOrderPaid(orderId)
    return ok(payment)
  })

  // 支付结果查询
  mock.onGet(/\/payments\/result\/[\w-]+$/).reply((config) => {
    const orderId = config.url?.match(/\/payments\/result\/([\w-]+)$/)?.[1] ?? ''
    const order = seedOrders.find((o) => o.id === orderId)
    if (!order) {
      return fail(40410, '订单不存在')
    }
    const payment = seedPayments.find((p) => p.orderId === orderId)
    if (order.status === 'PendingPayment') {
      return ok({
        orderId,
        orderStatus: order.status,
        paymentStatus: 'Pending',
        amount: order.amounts.payableAmount,
      })
    }
    if (order.status === 'Cancelled') {
      return ok({
        orderId,
        orderStatus: order.status,
        paymentStatus: 'Expired',
        amount: order.amounts.payableAmount,
        failReason: order.cancelReason ?? '订单已取消',
      })
    }
    return ok({
      orderId,
      orderStatus: order.status,
      paymentStatus: 'Success',
      amount: order.amounts.payableAmount,
      channel: payment?.channel,
      paidAt: payment?.paidAt ?? order.paidAt,
    })
  })
}
