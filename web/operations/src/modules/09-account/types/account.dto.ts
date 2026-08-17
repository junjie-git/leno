import type { PageQuery } from '@/shared/types'

/**
 * 09-account 模块个人中心 DTO（profile / 通知 / 待办聚合）
 *
 * 命名惯例：XxxDto / ListXxxParams；可空字段统一 string|null；时间均为 ISO8601 字符串。
 */

/* ============================== 个人资料 ============================== */

/**
 * 当前账号完整资料（GET /users/me 响应）
 *
 * 含双因子状态与外部登录绑定列表，供个人资料页四个分区共用。
 */
export interface AccountProfileDto {
  id: string
  username: string
  fullName: string | null
  email: string | null
  phone: string | null
  avatarUrl: string | null
  roles: string[]
  /** 是否已设置登录密码（解绑最后一个外部登录前校验） */
  hasPassword: boolean
  twoFactorEnabled: boolean
  /** 双因子启用时间（未启用为 null） */
  twoFactorEnabledAt: string | null
  externalLogins: ExternalLoginDto[]
}

/**
 * 修改资料请求体（PUT /users/me）
 */
export interface UpdateProfileDto {
  fullName: string
  email: string
  phone: string | null
  avatarUrl: string | null
}

/**
 * 修改密码请求体（PUT /users/me/password）
 */
export interface ChangePasswordDto {
  oldPassword: string
  newPassword: string
}

/**
 * 启用双因子响应体（POST /users/me/two-factor/enable）
 *
 * qrCodeUri 供 Authenticator 扫码；无法扫码时回退 manualEntryKey 手动输入。
 */
export interface TwoFactorEnableResultDto {
  qrCodeUri: string
  manualEntryKey: string
}

/**
 * 确认双因子请求体（POST /users/me/two-factor/confirm）
 */
export interface TwoFactorConfirmDto {
  totpCode: string
}

/**
 * 外部登录绑定项
 */
export interface ExternalLoginDto {
  /** 提供商标识：Google / GitHub / WeChat */
  provider: string
  /** 第三方侧账号名 */
  externalUserName: string | null
  /** 绑定时间（ISO8601） */
  boundAt: string | null
}

/**
 * 绑定外部登录请求体（POST /account/external-logins）
 */
export interface BindExternalLoginDto {
  provider: string
  /** OAuth 回调授权码 */
  authorizationCode: string
}

/* ============================== 通知中心 ============================== */

/** 通知类型：系统 / 业务 / 审核 */
export type NotificationType = 'System' | 'Business' | 'Audit'

/**
 * 通知列表查询参数（GET /notifications）
 */
export interface ListNotificationsParams extends PageQuery {
  /** 已读状态筛选，不传为全部 */
  isRead?: boolean
  /** 类型筛选，不传为全部 */
  type?: NotificationType
}

/**
 * 站内信记录项
 */
export interface NotificationRecordDto {
  id: string
  title: string
  summary: string | null
  /** 正文全文（详情抽屉展示） */
  content: string | null
  type: NotificationType
  /** 来源系统（如「商品审核系统」） */
  source: string | null
  /** 关联业务跳转路径（站内路径，如 /products/audit） */
  businessRef: string | null
  isRead: boolean
  createdAt: string
}

/**
 * 通知列表响应体（GET /notifications）
 */
export interface NotificationListResultDto {
  items: NotificationRecordDto[]
  total: number
  page: number
  pageSize: number
  unreadCount: number
}

/**
 * 未读计数响应体（GET /notifications/unread-count）
 */
export interface UnreadCountResultDto {
  count: number
}

/**
 * 批量标记已读请求体（POST /notifications/read）
 */
export interface MarkAsReadDto {
  recordIds: string[]
}

/* ============================== 待办工作台 ============================== */

/**
 * 待办项视图（由各业务域列表项归一化）
 */
export interface TodoItemDto {
  id: string
  title: string
  /** 来源（卖家名 / 系统名） */
  source: string | null
  /** 提交时间（ISO8601），用于 24 小时超时判定 */
  submittedAt: string | null
}

/**
 * 单个待办分类（Total 计数 + Top10 条目）
 */
export interface TodoCategoryDto {
  total: number
  items: TodoItemDto[]
  /** 该端点请求失败时 true（卡片显示 -- 并允许重试） */
  failed: boolean
}

/**
 * 待办面板聚合结果（5 个业务域分类）
 */
export interface TodoBoardDto {
  /** 待审核商品（/admin/products/all?status=PendingAudit） */
  products: TodoCategoryDto
  /** 待审核入驻（/admin/shops?status=PendingReview） */
  shops: TodoCategoryDto
  /** 待介入售后（/admin/after-sales?status=PendingIntervention） */
  afterSales: TodoCategoryDto
  /** 待审核评价（/admin/reviews?status=Pending） */
  reviews: TodoCategoryDto
  /** 死信通知（/notifications/records?status=DeadLetter） */
  notifications: TodoCategoryDto
}
