import type { AxiosResponse } from 'axios'
import { client, withIdempotency } from '@/shared/http'
import type {
  ReconciliationDiffListResultDto,
  ReconciliationDiffQueryParams,
} from '../types/reconciliation.dto'

/**
 * 渠道对账 API
 *
 * 与 Payment 域 AdminReconciliationController 对接（baseURL 已含 /api）：
 * - GET  /admin/reconciliation/diffs              分页查询对账差异列表
 * - POST /admin/reconciliation/trigger?billDate=  手动触发对账（异步任务，幂等）
 *
 * 触发对账为异步任务：同一账单日期重复触发时后端幂等返回
 * 「对账任务进行中，请勿重复触发」；billDate 缺省时后端默认取前一天。
 */
export const reconciliationApi = {
  /**
   * 分页查询对账差异
   *
   * 支持账单日期 / 渠道 / 差异类型 / 状态组合筛选。
   */
  listDiffs(
    params: ReconciliationDiffQueryParams,
  ): Promise<AxiosResponse<ReconciliationDiffListResultDto>> {
    return client.get<ReconciliationDiffListResultDto>('/admin/reconciliation/diffs', { params })
  },

  /**
   * 手动触发指定日期对账（幂等）
   *
   * @param billDate 账单日期（yyyy-MM-dd），缺省时后端默认取前一天
   */
  trigger(billDate?: string): Promise<AxiosResponse<void>> {
    const url = billDate
      ? `/admin/reconciliation/trigger?billDate=${encodeURIComponent(billDate)}`
      : '/admin/reconciliation/trigger'
    return client.post<void>(url, null, withIdempotency())
  },
}
