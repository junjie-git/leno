import { client } from '@/shared/http'
import type { ServerSnapshotDto, MetricName, MetricHistoryDto } from '../types/server-monitor.dto'

export const serverMonitorApi = {
  snapshot(): Promise<ServerSnapshotDto> {
    return client.get<ServerSnapshotDto>('/admin/server-monitor/snapshot').then((r) => r.data)
  },

  history(metric: MetricName, rangeSeconds = 300): Promise<MetricHistoryDto> {
    return client.get<MetricHistoryDto>('/admin/server-monitor/history', { params: { metric, rangeSeconds } }).then((r) => r.data)
  },
}
