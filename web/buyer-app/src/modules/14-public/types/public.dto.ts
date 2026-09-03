/**
 * 公共域 DTO（SystemAdmin BC 对外公开端点）
 *
 * 端点契约：
 * - GET /api/announcements      公告列表（首页公告条 / 公告页）
 * - GET /api/dictionaries/{code} 数据字典（按 code 查询）
 */

/** 公告类型 */
export type AnnouncementType = 'Promotion' | 'System' | 'Maintenance'

/** 公告 */
export interface AnnouncementDto {
  id: string
  title: string
  content: string
  type: AnnouncementType
  publishedAt: string
  /** 是否置顶 */
  pinned: boolean
}

/** 字典条目 */
export interface DictionaryItemDto {
  label: string
  value: string
}

/** 字典 */
export interface DictionaryDto {
  code: string
  name: string
  items: DictionaryItemDto[]
}
