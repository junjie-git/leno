export interface OnlineUserDto {
  sessionId: string
  userId: string
  username: string
  roles: string[]
  ipAddress: string
  geoLocation: string
  browser: string
  os: string
  loginAt: string
  lastActivityAt: string
  sessionDurationMs: number
  tokenPreview: string
  deviceFingerprint: string
  requestCount: number
  isAnomaly: boolean
}

export interface OnlineUserStatsDto {
  total: number
  logins24h: number
  anomalies: number
}

export interface OnlineUserQueryDto {
  username?: string
  ipAddress?: string
  loginAtFrom?: string
  loginAtTo?: string
  page: number
  pageSize: number
}
