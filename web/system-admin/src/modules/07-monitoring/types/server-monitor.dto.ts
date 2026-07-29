export interface ServerSnapshotDto {
  hostname: string
  os: string
  kernelVersion: string
  cpuModel: string
  cpuCores: number
  cpuUsagePercent: number
  memoryTotalBytes: number
  memoryUsedBytes: number
  memoryCachedBytes: number
  diskTotalBytes: number
  diskUsedBytes: number
  diskReadBytesPerSec: number
  diskWriteBytesPerSec: number
  loadAvg1: number
  loadAvg5: number
  loadAvg15: number
  processCount: number
  uptimeSeconds: number
  bootTime: string
  dotnetRuntimeVersion: string
  gcTotalCollections: number
  sampledAt: string
}

export type MetricName = 'cpu' | 'memory' | 'disk-io'

export interface MetricPointDto {
  timestamp: string
  value: number
}

export interface MetricHistoryDto {
  metric: MetricName
  points: MetricPointDto[]
}
