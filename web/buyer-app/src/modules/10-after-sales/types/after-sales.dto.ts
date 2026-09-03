/**
 * 售后域 DTO（AfterSales 域 / 旧 ReviewAfterSales 双轨兜底）
 *
 * 端点契约：
 * - GET  /api/after-sales/mine                  我的售后列表
 * - GET  /api/after-sales/order/{orderId}       按订单查询售后（售后详情页）
 * - POST /api/after-sales                       申请售后
 * - POST /api/after-sales/images                售后凭证图上传（返回 URL 列表）
 * - POST /api/after-sales/{id}/cancel           撤销售后申请
 * - POST /api/after-sales/{id}/return-goods     提交退货物流单号
 * - GET  /api/refunds/{afterSalesId}            退款进度查询
 */

/** 售后类型：仅退款 / 退货退款 / 换货 */
export type AfterSalesType = 'RefundOnly' | 'ReturnRefund' | 'Exchange'

/** 售后状态 */
export type AfterSalesStatus =
  | 'PendingReview'
  | 'Approved'
  | 'Rejected'
  | 'Returning'
  | 'Refunding'
  | 'Completed'
  | 'Cancelled'

/** 售后单 */
export interface AfterSalesDto {
  id: string
  orderId: string
  orderNo: string
  orderLineId: string
  spuId: string
  skuId: string
  name: string
  image: string
  specs: string
  price: number
  quantity: number
  type: AfterSalesType
  status: AfterSalesStatus
  /** 申请原因（不想要了/质量问题/发错货等） */
  reason: string
  /** 问题描述 */
  description: string
  /** 凭证图片 URL 列表 */
  images: string[]
  /** 预计/协议退款金额（分） */
  refundAmount: number
  applyAt: string
  handleAt?: string
  /** 驳回原因 */
  rejectReason?: string
  /** 退货物流信息（退货退款流程） */
  returnLogistics?: {
    company: string
    logisticsNo: string
    shippedAt: string
  }
}

/** 申请售后请求 */
export interface ApplyAfterSalesRequestDto {
  orderLineId: string
  type: AfterSalesType
  reason: string
  description: string
  images: string[]
  /** 仅退款时可申请的最大金额（分） */
  refundAmount?: number
}

/** 退款单 */
export interface RefundDto {
  id: string
  afterSalesId: string
  amount: number
  /** 退款状态 */
  status: 'Processing' | 'Success' | 'Failed'
  /** 原路退回渠道 */
  channel: string
  appliedAt: string
  refundedAt?: string
}

/** 提交退货物流请求 */
export interface ReturnGoodsRequestDto {
  company: string
  logisticsNo: string
}
