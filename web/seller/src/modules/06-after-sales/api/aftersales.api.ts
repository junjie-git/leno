import { client, withIdempotency } from '@/shared/http'
import type { PageResult } from '@/shared/types'
import type {
  AfterSalesListItemDto,
  AfterSalesDetailDto,
  RejectAfterSalesDto,
  ListAfterSalesParams,
} from '../types/aftersales.dto'

export const aftersalesApi = {
  list: (params: ListAfterSalesParams) => {
    const { page = 1, pageSize = 20, ...rest } = params
    return client.get<PageResult<AfterSalesListItemDto>>('/seller/after-sales', {
      params: { ...rest, page, pageSize },
    })
  },

  get: (id: string) =>
    client.get<AfterSalesDetailDto>(`/seller/after-sales/${id}`),

  approve: (id: string, version: number) =>
    client.post<AfterSalesDetailDto>(
      `/seller/after-sales/${id}/approve`,
      { version },
      withIdempotency(),
    ),

  reject: (id: string, body: RejectAfterSalesDto) =>
    client.post<AfterSalesDetailDto>(
      `/seller/after-sales/${id}/reject`,
      body,
      withIdempotency(),
    ),

  confirmReturn: (id: string, version: number) =>
    client.post<AfterSalesDetailDto>(
      `/seller/after-sales/${id}/confirm-return`,
      { version },
      withIdempotency(),
    ),
}
