import { client } from '@/shared/http'
import type { PageResult } from '@/shared/types'
import type { LoginLogDto, LoginLogQueryDto } from '../types/login-log.dto'

export const loginLogsApi = {
  list(params: LoginLogQueryDto): Promise<PageResult<LoginLogDto>> {
    return client.get<PageResult<LoginLogDto>>('/admin/login-logs', { params }).then((r) => r.data)
  },

  get(id: string): Promise<LoginLogDto> {
    return client.get<LoginLogDto>(`/admin/login-logs/${id}`).then((r) => r.data)
  },

  exportCsv(params: LoginLogQueryDto): Promise<string> {
    return client.get<string>('/admin/login-logs/export', { params, responseType: 'text' }).then((r) => r.data)
  },
}
