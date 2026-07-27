import { client, withIdempotency } from '@/shared/http'
import type { PageResult } from '@/shared/types'
import type { RedisInfoDto, KeyspaceDto, RedisKeyDto, RedisKeyDetailDto, CacheKeyQueryDto } from '../types/cache.dto'

export const cacheApi = {
  info(): Promise<RedisInfoDto> {
    return client.get<RedisInfoDto>('/admin/cache/info').then((r) => r.data)
  },

  keyspaces(): Promise<KeyspaceDto[]> {
    return client.get<KeyspaceDto[]>('/admin/cache/keyspaces').then((r) => r.data)
  },

  listKeys(params: CacheKeyQueryDto): Promise<PageResult<RedisKeyDto>> {
    return client.get<PageResult<RedisKeyDto>>('/admin/cache/keys', { params }).then((r) => r.data)
  },

  getKey(key: string, db: number): Promise<RedisKeyDetailDto> {
    return client.get<RedisKeyDetailDto>(`/admin/cache/keys/${encodeURIComponent(key)}`, { params: { db } }).then((r) => r.data)
  },

  deleteKey(key: string, db: number): Promise<void> {
    return client.delete<void>(`/admin/cache/keys/${encodeURIComponent(key)}`, { params: { db }, ...withIdempotency() }).then(() => undefined)
  },
}
