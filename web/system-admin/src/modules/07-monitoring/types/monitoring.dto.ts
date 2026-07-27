// web/system-admin/src/modules/07-monitoring/types/monitoring.dto.ts
// 07-monitoring 模块 DTO 与常量定义
// Prometheus 看板 URL 通过 SystemConfigsController 的 by-key 端点读取明文值
// （与 03-system-governance 的 SystemConfigRevealDto 字段对齐，本模块专用 DTO 保持独立性）

/**
 * Prometheus 看板 URL 配置项明文响应 DTO
 * 对应后端 GET /api/admin/system-configs/by-key/{key} 返回结构
 */
export interface PrometheusDashboardConfigDto {
  /** 配置项 ID */
  configId: string
  /** 配置键，固定为 monitoring.prometheus.dashboard-url */
  key: string
  /** 明文看板 URL，如 http://grafana.leno.internal/d/system-overview */
  value: string
}

/**
 * 监控相关 SystemConfigs 配置键集中管理
 * 与后端 SystemConfigsController 存储的 key 字符串完全一致
 */
export const MONITORING_CONFIG_KEYS = {
  /** Prometheus / Grafana 看板嵌入 URL 配置键 */
  PROMETHEUS_DASHBOARD_URL: 'monitoring.prometheus.dashboard-url',
} as const

/**
 * sessionStorage 缓存 key（spec §3.7：Prometheus iframe URL 缓存 5 分钟）
 * 缓存结构：{ url: string, cachedAt: number(ms timestamp) }
 */
export const PROMETHEUS_URL_CACHE_KEY = 'monitoring.prometheus.dashboard-url.cache'

/**
 * sessionStorage 缓存 TTL：5 分钟（spec §3.7）
 */
export const PROMETHEUS_URL_CACHE_TTL_MS = 5 * 60 * 1000

/**
 * iframe 加载超时阈值：10 秒未触发 load 事件视为加载失败
 * 用于跨域场景下 @error 事件可能不触发的兜底检测
 */
export const IFRAME_LOAD_TIMEOUT_MS = 10 * 1000
