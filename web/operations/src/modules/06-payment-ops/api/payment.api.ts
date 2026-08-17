import type { AxiosResponse } from 'axios'
import { client } from '@/shared/http'
import type { PaymentListResultDto, PaymentQueryParams } from '../types/payment.dto'

/**
 * 支付记录 API
 *
 * 与 Payment 域 AdminPaymentsController 对接（baseURL 已含 /api）：
 * - GET /admin/payments 运营端分页查询全平台支付记录
 *
 * 所有方法返回 AxiosResponse，调用方解构 .data 拿业务负载
 * （响应拦截器已完成 ApiResponse 信封解包）。
 */
export const paymentApi = {
  /**
   * 分页查询全平台支付记录
   *
   * 支持支付单号 / 订单 / 用户 / 渠道 / 状态 / 创建时间范围组合筛选，
   * 响应含各状态计数与支付成功率（统计概览数据源）。
   */
  list(params: PaymentQueryParams): Promise<AxiosResponse<PaymentListResultDto>> {
    return client.get<PaymentListResultDto>('/admin/payments', { params })
  },
}
