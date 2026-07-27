import { client, withIdempotency } from '@/shared/http'
import type { PageResult } from '@/shared/types'
import type { OnlineUserDto, OnlineUserStatsDto, OnlineUserQueryDto } from '../types/online-user.dto'

export const onlineUsersApi = {
  list(params: OnlineUserQueryDto): Promise<PageResult<OnlineUserDto>> {
    return client.get<PageResult<OnlineUserDto>>('/admin/online-users', { params }).then((r) => r.data)
  },

  get(id: string): Promise<OnlineUserDto> {
    return client.get<OnlineUserDto>(`/admin/online-users/${id}`).then((r) => r.data)
  },

  kick(id: string): Promise<void> {
    return client.delete<void>(`/admin/online-users/${id}`, withIdempotency()).then(() => undefined)
  },

  stats(): Promise<OnlineUserStatsDto> {
    return client.get<OnlineUserStatsDto>('/admin/online-users/stats').then((r) => r.data)
  },
}
