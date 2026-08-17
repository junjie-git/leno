import type { AxiosResponse } from 'axios'
import { client, withIdempotency } from '@/shared/http'
import type { PageResult } from '@/shared/types'
import type {
  AfterSalesDto,
  AfterSalesQueryParams,
  AfterSalesStatus,
  ApproveAfterSalesDto,
  RejectAfterSalesDto,
} from '../types/afterSales.dto'

/**
 * 售后处理 API
 *
 * 与 AfterSales 域 AdminAfterSalesController 对接（baseURL 已含 /api）。
 * 所有方法返回 AxiosResponse，调用方解构 .data 拿业务负载。
 */
export const afterSalesApi = {
  /**
   * 全平台售后单分页查询
   *
   * 支持售后单号 / 订单 / 买家 / 卖家 / 状态 / 类型 / 时间范围与分页组合筛选。
   */
  list(params: AfterSalesQueryParams): Promise<AxiosResponse<PageResult<AfterSalesDto>>> {
    return client.get<PageResult<AfterSalesDto>>('/admin/after-sales', { params })
  },

  /**
   * 运营审核通过售后（触发退款流程）
   *
   * approvedAmount 缺省按申请金额全额退款；remark 可选。
   */
  approve(id: string, body: ApproveAfterSalesDto): Promise<AxiosResponse<void>> {
    return client.post<void>(`/admin/after-sales/${id}/approve`, body, withIdempotency())
  },

  /**
   * 运营驳回售后（reason 必填，前端限制 ≥5 字）
   */
  reject(id: string, body: RejectAfterSalesDto): Promise<AxiosResponse<void>> {
    return client.post<void>(`/admin/after-sales/${id}/reject`, body, withIdempotency())
  },
}

/**
 * 按状态统计售后单数量（统计概览卡数据源）
 *
 * md 未定义独立统计端点，基于列表端点按状态各取 pageSize=1 读取 total 聚合。
 * 单个状态查询失败时该状态计数记为 0，不阻塞其它状态。
 */
export async function countAfterSalesByStatus(
  statuses: AfterSalesStatus[],
): Promise<Partial<Record<AfterSalesStatus, number>>> {
  const results = await Promise.all(
    statuses.map(async (status) => {
      try {
        const { data } = await afterSalesApi.list({ status, page: 1, pageSize: 1 })
        return [status, data.total] as const
      } catch {
        return [status, 0] as const
      }
    }),
  )
  return Object.fromEntries(results) as Partial<Record<AfterSalesStatus, number>>
}
