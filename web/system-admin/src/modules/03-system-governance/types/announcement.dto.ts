// web/system-admin/src/modules/03-system-governance/types/announcement.dto.ts
// 公告 DTO 类型定义（对应后端 AnnouncementsController）

// 公告类型：SystemMaintenance 系统维护 / ActivityNotification 活动通知 / PolicyChange 政策变更 / Urgent 紧急公告
export type AnnouncementType = 'SystemMaintenance' | 'ActivityNotification' | 'PolicyChange' | 'Urgent'

// 公告状态：Draft 草稿 / Published 已发布 / Unpublished 已撤回
export type AnnouncementStatus = 'Draft' | 'Published' | 'Unpublished'

// 公告受众范围：Buyer 买家 / Seller 卖家 / Operator 运营
export type AnnouncementAudience = 'Buyer' | 'Seller' | 'Operator'

// 公告响应 DTO
export interface AnnouncementDto {
  announcementId: string
  title: string
  type: AnnouncementType
  status: AnnouncementStatus
  audiences: AnnouncementAudience[]   // 发布范围多选
  effectiveFrom: string               // 生效起始 ISO 8601
  effectiveTo: string                 // 生效结束 ISO 8601
  content: string                     // 正文（HTML 字符串）
  isPinned: boolean                   // 是否置顶
  createdAt: string                   // 创建时间 ISO 8601
  publishedAt: string | null          // 发布时间，草稿态为 null
}

// 创建/更新公告请求 DTO（POST/PUT /admin/announcements[/{announcementId}]）
export interface SaveAnnouncementDto {
  title: string
  type: AnnouncementType
  audiences: AnnouncementAudience[]
  effectiveFrom: string
  effectiveTo: string
  content: string
  isPinned: boolean
}

// 列表查询参数（GET /admin/announcements）
export interface ListAnnouncementsParams {
  type?: AnnouncementType[]       // 类型多选
  status?: AnnouncementStatus[]   // 状态多选
}

// 公告类型中文标签映射（视图层下拉与表格展示复用）
export const ANNOUNCEMENT_TYPE_LABELS: Record<AnnouncementType, string> = {
  SystemMaintenance: '系统维护',
  ActivityNotification: '活动通知',
  PolicyChange: '政策变更',
  Urgent: '紧急公告',
}

// 公告状态中文标签映射
export const ANNOUNCEMENT_STATUS_LABELS: Record<AnnouncementStatus, string> = {
  Draft: '草稿',
  Published: '已发布',
  Unpublished: '已撤回',
}

// 公告受众中文标签映射
export const ANNOUNCEMENT_AUDIENCE_LABELS: Record<AnnouncementAudience, string> = {
  Buyer: '买家',
  Seller: '卖家',
  Operator: '运营',
}
