// web/system-admin/src/modules/03-system-governance/api/announcements.api.ts
// 公告管理 API（SystemAdmin 域 AnnouncementsController）

import { client, withIdempotency } from '@/shared/http'
import type { PageResult, PageQuery } from '@/shared/types'
import type {
  AnnouncementDto,
  SaveAnnouncementDto,
  ListAnnouncementsParams,
} from '../types/announcement.dto'

// 公告 API：list/create/update/publish/unpublish
export const announcementsApi = {
  // 分页查询公告
  list: (params: ListAnnouncementsParams & PageQuery): Promise<PageResult<AnnouncementDto>> =>
    client.get<PageResult<AnnouncementDto>>('/admin/announcements', { params }).then((r) => r.data),

  // 创建公告（初始草稿态，幂等）
  create: (body: SaveAnnouncementDto): Promise<AnnouncementDto> =>
    client.post<AnnouncementDto>('/admin/announcements', body, withIdempotency()).then((r) => r.data),

  // 更新公告（仅草稿态可更新，幂等）
  update: (announcementId: string, body: SaveAnnouncementDto): Promise<AnnouncementDto> =>
    client.put<AnnouncementDto>(`/admin/announcements/${announcementId}`, body, withIdempotency()).then((r) => r.data),

  // 发布公告（仅草稿态可发布，幂等）
  publish: (announcementId: string): Promise<AnnouncementDto> =>
    client.post<AnnouncementDto>(`/admin/announcements/${announcementId}/publish`, null, withIdempotency()).then((r) => r.data),

  // 撤回公告（仅已发布态可撤回，幂等）
  unpublish: (announcementId: string): Promise<AnnouncementDto> =>
    client.post<AnnouncementDto>(`/admin/announcements/${announcementId}/unpublish`, null, withIdempotency()).then((r) => r.data),
}
