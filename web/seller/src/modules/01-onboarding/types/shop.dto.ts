/**
 * 01-onboarding 店铺设置 DTO
 *
 * 与后端 ShopController 对接：
 * - POST /api/shops/application          提交入驻申请
 * - GET  /api/shops/me                   查询当前卖家店铺资料
 * - PUT  /api/shops/me                   更新店铺资料（含客服联系方式 + version 乐观锁）
 * - GET  /api/shops/me/qualifications    资质列表
 * - POST /api/shops/me/qualifications    上传资质（multipart/form-data）
 */

/** 店铺状态 */
export type ShopStatus = 'Pending' | 'Active' | 'Suspended' | 'Closed'

/** 客服联系方式 */
export interface CustomerServiceDto {
  phone: string
  email?: string
  onlineAccount?: string
}

/** 店铺基础信息 */
export interface ShopInfoDto {
  id: string
  name: string
  logo?: string
  description?: string
  status: ShopStatus
  mainCategory?: string
  customerService: CustomerServiceDto
  version: number
  createdAt: string
  updatedAt: string
}

/** 入驻申请 DTO */
export interface ShopApplicationDto {
  name: string
  mainCategory: string
  description?: string
  contactPhone: string
  contactEmail?: string
}

/** 更新店铺信息 DTO（含乐观锁 version） */
export interface UpdateShopInfoDto {
  name: string
  logo?: string
  description?: string
  customerService: CustomerServiceDto
  version: number
}

/** 资质类型 */
export type QualificationType = 'BusinessLicense' | 'IdCard' | 'BankAccount' | 'Other'

/** 资质状态 */
export type QualificationStatus = 'Pending' | 'Approved' | 'Rejected'

/** 资质文件 */
export interface QualificationDto {
  id: string
  type: QualificationType
  fileName: string
  fileUrl: string
  status: QualificationStatus
  submittedAt: string
  auditedAt?: string
  rejectReason?: string
}

/** 上传资质 DTO */
export interface UploadQualificationDto {
  file: File
  type: QualificationType
}
