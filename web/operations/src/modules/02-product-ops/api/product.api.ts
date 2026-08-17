import type { AxiosResponse } from 'axios'
import { client, withIdempotency } from '@/shared/http'
import type { PageResult } from '@/shared/types'
import type {
  ActionReasonDto,
  BatchOperationFailureDto,
  BatchOperationResultDto,
  ProductDto,
  ProductQueryParams,
  ReplenishSkuDto,
  SkuDto,
  UpdateStockDto,
} from '../types/product.dto'

/**
 * 商品审核 API
 *
 * 与 Product 域 AdminProductsController 对接（baseURL 已含 /api）。
 * 所有方法返回 AxiosResponse，调用方解构 .data 拿业务负载
 * （响应拦截器已完成 ApiResponse 信封解包）。
 *
 * 说明：md 未定义批量审核端点，batchApprove / batchReject 为前端串行
 * 循环单条接口并汇总 BatchOperationResultDto 的组合实现。
 */

/**
 * 串行执行批量动作并汇总成功 / 失败明细
 *
 * 单条失败不中断整体，失败原因取自后端错误 message。
 */
async function runBatch(
  ids: string[],
  action: (id: string) => Promise<AxiosResponse<unknown>>,
): Promise<BatchOperationResultDto> {
  const failures: BatchOperationFailureDto[] = []
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

export const productApi = {
  /**
   * 全量商品分页查询（跨店铺）
   *
   * 支持关键词 / 卖家 / 状态 / 分类与 PageQuery 组合筛选。
   */
  list(params: ProductQueryParams): Promise<AxiosResponse<PageResult<ProductDto>>> {
    return client.get<PageResult<ProductDto>>('/admin/products/all', { params })
  },

  /**
   * 审核通过并上架
   */
  approve(id: string): Promise<AxiosResponse<void>> {
    return client.post<void>(`/admin/products/${id}/approve`, null, withIdempotency())
  },

  /**
   * 审核驳回（原因必填，前端限制 5-200 字）
   */
  reject(id: string, body: ActionReasonDto): Promise<AxiosResponse<void>> {
    return client.post<void>(`/admin/products/${id}/reject`, body, withIdempotency())
  },

  /**
   * 调整 SKU 库存（delta 方式：正数补库存、负数扣库存）
   */
  updateSkuStock(
    id: string,
    skuId: string,
    body: UpdateStockDto,
  ): Promise<AxiosResponse<SkuDto | null>> {
    return client.post<SkuDto | null>(`/admin/products/${id}/skus/${skuId}/stock`, body, withIdempotency())
  },

  /**
   * 为指定 SKU 补货（数量必须大于 0）
   */
  replenishSku(skuId: string, body: ReplenishSkuDto): Promise<AxiosResponse<SkuDto | null>> {
    return client.post<SkuDto | null>(`/admin/products/skus/${skuId}/replenish`, body, withIdempotency())
  },

  /**
   * 批量审核通过：串行调用单条 approve 并汇总结果
   */
  async batchApprove(ids: string[]): Promise<BatchOperationResultDto> {
    return runBatch(ids, (id) => productApi.approve(id))
  },

  /**
   * 批量驳回：串行调用单条 reject（共用同一驳回原因）并汇总结果
   */
  async batchReject(ids: string[], body: ActionReasonDto): Promise<BatchOperationResultDto> {
    return runBatch(ids, (id) => productApi.reject(id, body))
  },
}
