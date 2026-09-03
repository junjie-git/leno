import { client } from '@/shared/http'
import type {
  CreatePaymentRequestDto,
  PaymentDto,
  PaymentResultDto,
} from '../types/payment.dto'

/**
 * 支付 API（Payment BC）
 *
 * - POST /payments                  发起支付
 * - GET  /payments/result/{orderId} 支付结果查询
 *
 * 响应拦截器已解包 ApiResponse.data，调用方拿到的就是业务负载。
 */
export const paymentApi = {
  /** 发起支付（创建支付单） */
  create(body: CreatePaymentRequestDto): Promise<PaymentDto> {
    return client.post<PaymentDto>('/payments', body).then((r) => r.data)
  },

  /** 支付结果查询（支付结果页轮询） */
  getResult(orderId: string): Promise<PaymentResultDto> {
    return client.get<PaymentResultDto>(`/payments/result/${orderId}`).then((r) => r.data)
  },
}
