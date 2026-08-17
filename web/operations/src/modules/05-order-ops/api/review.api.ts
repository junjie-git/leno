import type { AxiosResponse } from 'axios'
import { client, withIdempotency } from '@/shared/http'
import type { PageResult } from '@/shared/types'
import type {
  BatchReviewFailureDto,
  BatchReviewResultDto,
  ModerateReviewDto,
  ReviewDto,
  ReviewQueryParams,
} from '../types/review.dto'

/**
 * 评价审核 API
 *
 * 与 Review 域 AdminReviewsController 对接（baseURL 已含 /api）。
 * 所有方法返回 AxiosResponse，调用方解构 .data 拿业务负载。
 *
 * 说明：md 未定义批量审核端点，batchApprove / batchHide 为前端串行
 * 循环单条接口并汇总 BatchReviewResultDto 的组合实现。
 */

/**
 * 串行执行批量动作并汇总成功 / 失败明细
 *
 * 单条失败不中断整体，失败原因取自后端错误 message。
 */
async function runBatch(
  ids: string[],
  action: (id: string) => Promise<AxiosResponse<unknown>>,
): Promise<BatchReviewResultDto> {
  const failures: BatchReviewFailureDto[] = []
  let succeeded = 0

  for (const id of ids) {
    try {
      await action(id)
      succeeded += 1
    } catch (e) {
      failures.push({ id, reason: e instanceof Error ? e.message : '操作失败' })
    }
  }

  return {
    total: ids.length,
    succeeded,
    failed: failures.length,
    failures,
  }
}

export const reviewApi = {
  /**
   * 全平台评价分页查询
   *
   * 支持商品名称 / 状态 / 评分 / 时间范围与分页组合筛选。
   */
  list(params: ReviewQueryParams): Promise<AxiosResponse<PageResult<ReviewDto>>> {
    return client.get<PageResult<ReviewDto>>('/admin/reviews', { params })
  },

  /**
   * 审核通过评价（隐藏态可重新通过，隐藏可逆）
   */
  approve(id: string): Promise<AxiosResponse<void>> {
    return client.post<void>(`/admin/reviews/${id}/approve`, null, withIdempotency())
  },

  /**
   * 隐藏违规评价（reasonCategory 必选：Spam/Abuse/Fake/Other）
   */
  hide(id: string, body: ModerateReviewDto): Promise<AxiosResponse<void>> {
    return client.post<void>(`/admin/reviews/${id}/hide`, body, withIdempotency())
  },

  /**
   * 批量通过：串行调用单条 approve 并汇总结果
   */
  async batchApprove(ids: string[]): Promise<BatchReviewResultDto> {
    return runBatch(ids, (id) => reviewApi.approve(id))
  },

  /**
   * 批量隐藏：串行调用单条 hide（共用同一隐藏原因）并汇总结果
   */
  async batchHide(ids: string[], body: ModerateReviewDto): Promise<BatchReviewResultDto> {
    return runBatch(ids, (id) => reviewApi.hide(id, body))
  },
}
