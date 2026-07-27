export interface RedisInfoDto {
  redisVersion: string
  redisMode: string
  os: string
  archBits: string
  tcpPort: number
  uptimeInDays: number
  connectedClients: number
  usedMemoryHuman: string
  usedMemoryPeakHuman: string
  maxmemoryHuman: string
  memFragmentationRatio: number
  totalConnectionsReceived: number
  totalCommandsProcessed: number
  keyspaceHits: number
  keyspaceMisses: number
  evictedKeys: number
}

export interface KeyspaceDto {
  db: number
  keys: number
  expires: number
  avgTtl: number
}

export type RedisKeyType = 'string' | 'hash' | 'list' | 'set' | 'zset'

export interface RedisKeyDto {
  key: string
  type: RedisKeyType
  size: number
  ttl: number
}

export interface RedisKeyDetailDto extends RedisKeyDto {
  value: unknown
  db: number
}

export interface CacheKeyQueryDto {
  db: number
  pattern: string
  type?: RedisKeyType
  page: number
  pageSize: number
}
