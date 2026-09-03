/**
 * 支付域 DTO（Payment BC）
 *
 * 端点契约：
 * - POST /api/payments                 发起支付（创建支付单）
 * - GET  /api/payments/result/{orderId} 支付结果查询（轮询）
 */

/** 支付渠道 */
export type PaymentChannel = 'Alipay' | 'WeChatPay' | 'UnionPay'

/** 支付单状态 */
export type PaymentStatus = 'Pending' | 'Processing' | 'Success' | 'Failed' | 'Expired' | 'Refunded'

/** 创建支付请求 */
export interface CreatePaymentRequestDto {
  orderId: string
  channel: PaymentChannel
}

/** 支付单 */
export interface PaymentDto {
  id: string
  orderId: string
  channel: PaymentChannel
  amount: number
  status: PaymentStatus
  createdAt: string
  /** 支付单过期时间（待支付倒计时） */
  expireAt: string
  paidAt?: string
  /** 支付渠道流水号（支付成功后回填） */
  channelTradeNo?: string
}

/** 支付结果 */
export interface PaymentResultDto {
  orderId: string
  /** 订单当前状态 */
  orderStatus: string
  /** 支付状态 */
  paymentStatus: PaymentStatus
  amount: number
  channel?: PaymentChannel
  paidAt?: string
  failReason?: string
}

/** 可用支付渠道描述（发起支付页展示） */
export interface PaymentChannelOptionDto {
  channel: PaymentChannel
  name: string
  description: string
  /** 渠道徽标（SVG data URI） */
  icon: string
}
