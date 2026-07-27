// web/system-admin/src/modules/07-monitoring/api/monitoring.api.ts
// 07-monitoring 模块 API：从 SystemConfigsController 读取 Prometheus 看板 URL 配置项
// 端点对齐 03-system-governance 的 SystemConfigsController by-key 路径
// 本模块为只读看板，无写操作，故无需 Idempotency-Key

import { client } from '@/shared/http'
import type { PrometheusDashboardConfigDto } from '../types/monitoring.dto'
import { MONITORING_CONFIG_KEYS } from '../types/monitoring.dto'

export const monitoringApi = {
  /**
   * 获取 Prometheus / Grafana 看板 URL（明文）
   * 调用 GET /api/admin/system-configs/by-key/{key}，key 为 monitoring.prometheus.dashboard-url
   * 返回的 value 字段为完整看板 URL，供前端 iframe 嵌入或「在新窗口打开」使用
   */
  getPrometheusUrl: () =>
    client.get<PrometheusDashboardConfigDto>(
      `/admin/system-configs/by-key/${MONITORING_CONFIG_KEYS.PROMETHEUS_DASHBOARD_URL}`,
    ),
}
