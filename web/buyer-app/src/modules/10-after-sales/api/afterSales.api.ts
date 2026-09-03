import { client } from '@/shared/http'
import type {
  AfterSalesDto,
  ApplyAfterSalesRequestDto,
  RefundDto,
  ReturnGoodsRequestDto,
} from '../types/after-sales.dto'

/**
 * 售后 API（AfterSales 域 / 旧 ReviewAfterSales 双轨兜底）
 *
 * - GET  /after-sales/mine                我的售后列表
 * - GET  /after-sales/order/{orderId}     按订单查询售后
 * - POST /after-sales                     申请售后
 * - POST /after-sales/images              上传凭证图（返回 URL）
 * - POST /after-sales/{id}/cancel         撤销售后
 * - POST /after-sales/{id}/return-goods   提交退货物流
 * - GET  /refunds/{afterSalesId}          退款进度
 *
 * 响应拦截器已解包 ApiResponse.data，调用方拿到的就是业务负载。
 */
export const afterSalesApi = {
  /** 我的售后列表 */
  listMine(): Promise<AfterSalesDto[]> {
    return client.get<AfterSalesDto[]>('/after-sales/mine').then((r) => r.data)
  },

  /** 按订单查询售后单（售后详情页） */
  getByOrder(orderId: string): Promise<AfterSalesDto[]> {
    return client.get<AfterSalesDto[]>(`/after-sales/order/${orderId}`).then((r) => r.data)
  },

  /** 申请售后 */
  apply(body: ApplyAfterSalesRequestDto): Promise<AfterSalesDto> {
    return client.post<AfterSalesDto>('/after-sales', body).then((r) => r.data)
  },

  /** 上传售后凭证图（multipart，返回图片 URL 列表） */
  uploadImages(files: File[]): Promise<string[]> {
    const form = new FormData()
    files.forEach((f) => form.append('files', f))
    return client
      .post<string[]>('/after-sales/images', form, {
        headers: { 'Content-Type': 'multipart/form-data' },
      })
      .then((r) => r.data)
  },

  /** 撤销售后申请 */
  cancel(id: string): Promise<null> {
    return client.post<null>(`/after-sales/${id}/cancel`).then((r) => r.data)
  },

  /** 提交退货物流单号 */
  submitReturnLogistics(id: string, body: ReturnGoodsRequestDto): Promise<AfterSalesDto> {
    return client.post<AfterSalesDto>(`/after-sales/${id}/return-goods`, body).then((r) => r.data)
  },

  /** 退款进度查询 */
  getRefund(afterSalesId: string): Promise<RefundDto> {
    return client.get<RefundDto>(`/refunds/${afterSalesId}`).then((r) => r.data)
  },
}
