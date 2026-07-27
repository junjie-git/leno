export type LoginResult = 'Success' | 'Failed'

export interface LoginLogDto {
  id: string
  username: string
  ipAddress: string
  geoLocation: string
  browser: string
  os: string
  result: LoginResult
  failureReason: string | null
  durationMs: number
  userAgent: string
  deviceFingerprint: string
  refererUrl: string | null
  traceId: string
  loginAt: string
}

export interface LoginLogQueryDto {
  username?: string
  result?: LoginResult
  loginAtFrom?: string
  loginAtTo?: string
  page: number
  pageSize: number
}
