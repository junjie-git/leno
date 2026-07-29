/**
 * 08-account 模块鉴权相关 DTO
 *
 * - LoginDto：登录请求体
 * - LoginResultDto：登录响应体（与后端 AuthController.Login 返回结构对齐）
 * - ProfileResultDto：GET /api/users/me 响应体
 *
 * 与 shared/auth/auth.store.ts 中的同名接口保持结构一致（SellerUser 视图含店铺字段）。
 */
export interface LoginDto {
  username: string
  password: string
}

export interface LoginResultDto {
  token: string
  expiresIn: number
  user: {
    id: string
    username: string
    email: string
    phone?: string
    nickname?: string
    avatar?: string
    shopId?: string
    shopName?: string
    shopStatus?: string
    status: string
    roles: string[]
  }
  roles: string[]
  permissions: string[]
}

export interface ProfileResultDto {
  profile: {
    id: string
    username: string
    email: string
    phone?: string
    nickname?: string
    avatar?: string
    shopId?: string
    shopName?: string
    shopStatus?: string
    status: string
    roles: string[]
  }
  permissions: string[]
}
