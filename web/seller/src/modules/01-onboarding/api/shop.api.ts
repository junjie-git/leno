import { http, withIdempotency } from '@/shared/http'
import type {
  ShopApplicationDto,
  ShopInfoDto,
  UpdateShopInfoDto,
  QualificationDto,
  UploadQualificationDto,
} from '../types/shop.dto'

/**
 * 店铺 API 客户端
 *
 * 与后端 ShopController 对接（响应拦截器已解包 ApiResponse.data，
 * 调用方拿到的就是业务负载）：
 * - POST /api/shops/application          提交入驻申请（幂等）
 * - GET  /api/shops/me                   查询当前卖家店铺资料
 * - PUT  /api/shops/me                   更新店铺资料（幂等 + version 乐观锁）
 * - GET  /api/shops/me/qualifications    资质列表
 * - POST /api/shops/me/qualifications    上传资质（multipart，幂等）
 */
export const shopApi = {
  /** 提交入驻申请 */
  submitApplication(body: ShopApplicationDto): Promise<ShopInfoDto> {
    return http
      .post<ShopInfoDto>('/shops/application', body, withIdempotency())
      .then((r) => r.data)
  },

  /** 查询当前卖家店铺资料 */
  getMyShop(): Promise<ShopInfoDto> {
    return http.get<ShopInfoDto>('/shops/me').then((r) => r.data)
  },

  /** 更新店铺基础信息（含客服联系方式 + version 乐观锁） */
  updateMyShop(body: UpdateShopInfoDto): Promise<ShopInfoDto> {
    return http
      .put<ShopInfoDto>('/shops/me', body, withIdempotency())
      .then((r) => r.data)
  },

  /** 资质列表 */
  listQualifications(): Promise<QualificationDto[]> {
    return http
      .get<QualificationDto[]>('/shops/me/qualifications')
      .then((r) => r.data)
  },

  /** 上传店铺资质（multipart/form-data） */
  uploadQualification(body: UploadQualificationDto): Promise<QualificationDto> {
    const formData = new FormData()
    formData.append('file', body.file)
    formData.append('type', body.type)
    return http
      .post<QualificationDto>('/shops/me/qualifications', formData, withIdempotency())
      .then((r) => r.data)
  },
}
