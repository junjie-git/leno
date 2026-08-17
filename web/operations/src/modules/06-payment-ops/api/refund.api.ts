import type { AxiosResponse } from 'axios'
import { client } from '@/shared/http'
import type { RefundListResultDto, RefundQueryParams } from '../types/refund.dto'

/**
 * 退款记录 API
 *
 * 与 Payment 域 AdminRefundsController 对接（baseURL 已含 /api）：
 * - GET /admin/refunds 运营端分页查询全平台退款记录
 *
 * md 未定义退款重试端点，失败退款需人工处理，本模块不提供重试方法。
 * 所有方法返回 AxiosResponse，调用方解构 .data 拿业务负载。
 */
export const refundApi = {
  /**
   * 分页查询全平台退款记录
   *
   * 支持退款编号 / 订单 / 状态 / 申请时间范围组合筛选，
   * 响应含各状态计数与退款成功率（统计概览数据源）。
   */
  list(params: RefundQueryParams): Promise<AxiosResponse<RefundListResultDto>> {
    return client.get<RefundListResultDto>('/admin/refunds', { params })
  },
}
