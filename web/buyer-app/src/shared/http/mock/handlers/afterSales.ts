import type MockAdapter from 'axios-mock-adapter'
import type {
  AfterSalesDto,
  ApplyAfterSalesRequestDto,
  ReturnGoodsRequestDto,
} from '@/modules/10-after-sales/types/after-sales.dto'
import {
  runtime,
  seedAfterSales,
  seedNotifications,
  seedOrders,
  seedRefunds,
} from '../data/seed'
import { fail, ok, parseBody } from './helpers'

/**
 * 售后 handlers（AfterSales 域）
 *
 * - GET  /after-sales/mine（我的售后）
 * - GET  /after-sales/order/{orderId}（按订单查询）
 * - POST /after-sales（申请售后）
 * - POST /after-sales/images（凭证图上传，返回生成的图片 URL）
 * - POST /after-sales/{id}/cancel（撤销）
 * - POST /after-sales/{id}/return-goods（提交退货物流）
 * - GET  /refunds/{afterSalesId}（退款进度）
 */

export function registerAfterSalesHandlers(mock: MockAdapter): void {
  // 我的售后列表
  mock.onGet('/after-sales/mine').reply(() => ok(seedAfterSales))

  // 按订单查询售后单
  mock.onGet(/\/after-sales\/order\/[\w-]+$/).reply((config) => {
    const orderId = config.url?.match(/\/after-sales\/order\/([\w-]+)$/)?.[1] ?? ''
    const list = seedAfterSales.filter((a) => a.orderId === orderId)
    if (list.length === 0) {
      return fail(40470, '该订单暂无售后记录')
    }
    return ok(list)
  })

  // 申请售后
  mock.onPost('/after-sales').reply((config) => {
    const body = parseBody<ApplyAfterSalesRequestDto>(config.data)
    const order = seedOrders.find((o) => o.items.some((i) => i.orderLineId === body.orderLineId))
    const line = order?.items.find((i) => i.orderLineId === body.orderLineId)
    if (!order || !line) {
      return fail(40471, '订单行不存在，无法申请售后')
    }
    if (!['RefundOnly', 'ReturnRefund', 'Exchange'].includes(body.type)) {
      return fail(40472, '售后类型不合法')
    }
    if (!body.reason) {
      return fail(40473, '请选择售后原因')
    }
    if (body.description && body.description.length > 200) {
      return fail(40474, '问题描述不能超过 200 字')
    }
    const existing = seedAfterSales.find((a) => a.orderLineId === body.orderLineId && !['Completed', 'Cancelled'].includes(a.status))
    if (existing) {
      return fail(40475, '该商品已有进行中的售后申请')
    }
    runtime.notificationSeq += 1
    const afterSales: AfterSalesDto = {
      id: `as-${Date.now()}`,
      orderId: order.id,
      orderNo: order.orderNo,
      orderLineId: line.orderLineId,
      spuId: line.spuId,
      skuId: line.skuId,
      name: line.name,
      image: line.image,
      specs: line.specs,
      price: line.price,
      quantity: line.quantity,
      type: body.type,
      status: 'PendingReview',
      reason: body.reason,
      description: body.description ?? '',
      images: body.images ?? [],
      refundAmount: body.refundAmount ?? line.price * line.quantity,
      applyAt: new Date().toISOString(),
    }
    seedAfterSales.unshift(afterSales)
    // 订单进入售后中
    if (order.status === 'Completed' || order.status === 'Shipped') {
      order.status = 'AfterSales'
    }
    seedNotifications.unshift({
      id: `nt-${runtime.notificationSeq}`,
      type: 'AfterSales',
      title: '售后申请已提交',
      content: `您的售后申请（${line.name.slice(0, 12)}…）已提交，商家将在 48 小时内审核，审核结果将通过消息通知您。`,
      isRead: false,
      createdAt: new Date().toISOString(),
      linkUrl: `/after-sales/${afterSales.id}`,
    })
    return ok(afterSales)
  })

  // 凭证图上传（演示：返回原图引用的占位 URL；真实后端上传对象存储）
  mock.onPost('/after-sales/images').reply((config) => {
    const form = config.data as FormData
    const files = form.getAll('files') as File[]
    if (!files || files.length === 0) {
      return fail(40476, '请选择要上传的凭证图片')
    }
    if (files.length > 6) {
      return fail(40477, '凭证图片最多 6 张')
    }
    return ok(files.map((_, i) => `mock://after-sales/${Date.now()}-${i + 1}.jpg`))
  })

  // 撤销售后
  mock.onPost(/\/after-sales\/[\w-]+\/cancel$/).reply((config) => {
    const id = config.url?.match(/\/after-sales\/([\w-]+)\/cancel$/)?.[1] ?? ''
    const afterSales = seedAfterSales.find((a) => a.id === id)
    if (!afterSales) {
      return fail(40470, '售后单不存在')
    }
    if (afterSales.status !== 'PendingReview' && afterSales.status !== 'Approved') {
      return fail(40478, '当前状态不可撤销')
    }
    afterSales.status = 'Cancelled'
    return ok(null)
  })

  // 提交退货物流
  mock.onPost(/\/after-sales\/[\w-]+\/return-goods$/).reply((config) => {
    const id = config.url?.match(/\/after-sales\/([\w-]+)\/return-goods$/)?.[1] ?? ''
    const afterSales = seedAfterSales.find((a) => a.id === id)
    if (!afterSales) {
      return fail(40470, '售后单不存在')
    }
    if (afterSales.status !== 'Approved') {
      return fail(40479, '商家审核通过后才能寄回商品')
    }
    const body = parseBody<ReturnGoodsRequestDto>(config.data)
    if (!body.company || !body.logisticsNo) {
      return fail(40480, '请填写快递公司与物流单号')
    }
    afterSales.status = 'Returning'
    afterSales.returnLogistics = {
      company: body.company,
      logisticsNo: body.logisticsNo,
      shippedAt: new Date().toISOString(),
    }
    return ok(afterSales)
  })

  // 退款进度
  mock.onGet(/\/refunds\/[\w-]+$/).reply((config) => {
    const afterSalesId = config.url?.match(/\/refunds\/([\w-]+)$/)?.[1] ?? ''
    const refund = seedRefunds[afterSalesId]
    if (!refund) {
      // 进行中的售后返回处理中退款单（演示：Returning/Refunding 状态）
      const afterSales = seedAfterSales.find((a) => a.id === afterSalesId)
      if (!afterSales) {
        return fail(40481, '退款单不存在')
      }
      return ok({
        id: `rf-${afterSalesId}`,
        afterSalesId,
        amount: afterSales.refundAmount,
        status: afterSales.status === 'Refunding' ? 'Processing' : 'Processing',
        channel: '原路退回',
        appliedAt: afterSales.applyAt,
      })
    }
    return ok(refund)
  })
}
