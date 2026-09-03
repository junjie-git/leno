import type { BuyerUserDto } from '@/modules/01-auth/types/auth.dto'

/**
 * 个人中心域 DTO（Identity 域资料 + UserCenter 域地址/收藏/历史）
 *
 * 端点契约：
 * - GET  /api/users/me                          个人资料
 * - PUT  /api/users/me                          更新资料
 * - PUT  /api/users/me/password                 修改密码
 * - GET/POST /api/users/me/addresses            地址列表/新增
 * - PUT/DELETE /api/users/me/addresses/{id}     修改/删除地址
 * - POST /api/users/me/addresses/{id}/default   设为默认地址
 * - GET/POST /api/users/me/favorites            收藏列表/新增收藏
 * - DELETE /api/users/me/favorites/{spuId}      取消收藏
 * - POST /api/users/me/favorites/batch-delete   批量取消收藏
 * - GET  /api/users/me/favorites/count          收藏数量
 * - GET/POST /api/users/me/browse-history       浏览历史/上报浏览
 * - DELETE /api/users/me/browse-history/{id}    删除单条历史
 * - POST /api/users/me/browse-history/batch-delete 批量删除历史
 * - DELETE /api/users/me/browse-history         清空历史
 */

/** 买家完整资料 */
export interface BuyerProfileDto extends BuyerUserDto {
  gender: 'Male' | 'Female' | 'Unknown'
  birthday?: string
  createdAt: string
  /** 是否已开启双因子 */
  twoFactorEnabled: boolean
}

/** 更新资料请求 */
export interface UpdateProfileRequestDto {
  nickname: string
  avatar?: string
  gender?: 'Male' | 'Female' | 'Unknown'
  birthday?: string
  email?: string
}

/** 修改密码请求 */
export interface ChangePasswordRequestDto {
  oldPassword: string
  newPassword: string
}

/** 收货地址 */
export interface AddressDto {
  id: string
  receiver: string
  phone: string
  province: string
  city: string
  district: string
  detail: string
  isDefault: boolean
  /** 地址标签：家/公司/学校等 */
  tag?: string
}

/** 保存地址请求（id 为空表示新增） */
export interface SaveAddressRequestDto {
  id?: string
  receiver: string
  phone: string
  province: string
  city: string
  district: string
  detail: string
  isDefault: boolean
  tag?: string
}

/** 收藏条目 */
export interface FavoriteDto {
  spuId: string
  name: string
  mainImage: string
  price: number
  sales: number
  shopName: string
  favoritedAt: string
}

/** 浏览历史条目 */
export interface BrowseHistoryDto {
  id: string
  spuId: string
  name: string
  mainImage: string
  price: number
  shopName: string
  viewedAt: string
}
