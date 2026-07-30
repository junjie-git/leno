import type { MockSeed } from './types'
import { generateIdempotencyKey } from '../../idempotency'

const SEED_KEY = 'mock_seed_v1'

/**
 * 确保 localStorage 中存在种子数据；若不存在则初始化。
 *
 * 写入后所有 handler 共享同一份 MockSeed，写操作直接修改对应数组。
 */
export function ensureSeedData(): void {
  if (localStorage.getItem(SEED_KEY)) return
  const seed: MockSeed = {
    menus: buildMenuSeed(),
    onlineUsers: buildOnlineUserSeed(),
    loginLogs: buildLoginLogSeed(),
    redisKeys: buildRedisKeySeed(),
    redisInfo: buildRedisInfoSeed(),
    keyspaces: buildKeyspaceSeed(),
    serverSnapshot: buildServerSnapshotSeed(),
    serverHistory: { cpu: [], memory: [], diskIo: [] },
    shop: buildShopSeed(),
    qualifications: buildQualificationSeed(),
    freightTemplates: buildFreightTemplateSeed(),
    logisticsCompanies: buildLogisticsCompanySeed(),
    reviews: buildReviewSeed(),
    exportTasks: [],
    nextId: 1000,
  }
  // 初始化 server 历史滚动窗口（300 点）
  initServerHistory(seed)
  localStorage.setItem(SEED_KEY, JSON.stringify(seed))
}

export function loadSeedData(): MockSeed {
  ensureSeedData()
  return JSON.parse(localStorage.getItem(SEED_KEY)!) as MockSeed
}

export function saveSeedData(seed: MockSeed): void {
  localStorage.setItem(SEED_KEY, JSON.stringify(seed))
}

export function resetSeedData(): void {
  localStorage.removeItem(SEED_KEY)
  ensureSeedData()
}

export function nextId(seed: MockSeed, prefix: string): string {
  seed.nextId += 1
  return `${prefix}-${seed.nextId}`
}

// ===== 菜单种子（7 目录 × 34 菜单）=====

function buildMenuSeed(): unknown[] {
  return [
    {
      id: 'm-01',
      parentId: null,
      name: '仪表盘',
      type: 'Directory',
      path: '/dashboard',
      component: null,
      icon: 'DashboardOutlined',
      sort: 1,
      permission: null,
      roles: ['Admin'],
      visible: true,
      cache: false,
      children: [
        { id: 'm-01-01', parentId: 'm-01', name: '运营总览', type: 'Menu', path: '/dashboard/operations-overview', component: '01-dashboard/views/OperationsOverview', icon: 'DashboardOutlined', sort: 1, permission: null, roles: ['Admin'], visible: true, cache: true },
        { id: 'm-01-02', parentId: 'm-01', name: '支付统计', type: 'Menu', path: '/dashboard/payment-stats', component: '01-dashboard/views/PaymentStats', icon: 'PayCircleOutlined', sort: 2, permission: null, roles: ['Admin'], visible: true, cache: true },
        { id: 'm-01-03', parentId: 'm-01', name: '积分统计', type: 'Menu', path: '/dashboard/points-stats', component: '01-dashboard/views/PointsStats', icon: 'GiftOutlined', sort: 3, permission: null, roles: ['Admin'], visible: true, cache: true },
        { id: 'm-01-04', parentId: 'm-01', name: '通知送达率', type: 'Menu', path: '/dashboard/notification-delivery', component: '01-dashboard/views/NotificationDelivery', icon: 'BellOutlined', sort: 4, permission: null, roles: ['Admin'], visible: true, cache: true },
        { id: 'm-01-05', parentId: 'm-01', name: '售后统计', type: 'Menu', path: '/dashboard/after-sales-stats', component: '01-dashboard/views/AfterSalesStats', icon: 'ToolOutlined', sort: 5, permission: null, roles: ['Admin'], visible: true, cache: true },
        { id: 'm-01-06', parentId: 'm-01', name: '店铺排行', type: 'Menu', path: '/dashboard/shop-ranking', component: '01-dashboard/views/ShopRanking', icon: 'ShopOutlined', sort: 6, permission: null, roles: ['Admin'], visible: true, cache: true },
        { id: 'm-01-07', parentId: 'm-01', name: '报表快照', type: 'Menu', path: '/dashboard/report-snapshots', component: '01-dashboard/views/ReportSnapshots', icon: 'FileTextOutlined', sort: 7, permission: null, roles: ['Admin'], visible: true, cache: false },
      ],
    },
    {
      id: 'm-02',
      parentId: null,
      name: '用户与权限',
      type: 'Directory',
      path: '/user-access',
      component: null,
      icon: 'TeamOutlined',
      sort: 2,
      permission: null,
      roles: ['Admin'],
      visible: true,
      cache: false,
      children: [
        { id: 'm-02-01', parentId: 'm-02', name: '用户管理', type: 'Menu', path: '/user-access/users', component: '02-user-access/views/UserManagement', icon: 'UserOutlined', sort: 1, permission: 'user:read', roles: ['Admin', 'Operator'], visible: true, cache: true },
        { id: 'm-02-02', parentId: 'm-02', name: '角色管理', type: 'Menu', path: '/user-access/roles', component: '02-user-access/views/RoleManagement', icon: 'SafetyOutlined', sort: 2, permission: 'role:read', roles: ['Admin'], visible: true, cache: true },
        { id: 'm-02-03', parentId: 'm-02', name: 'OAuth 客户端', type: 'Menu', path: '/user-access/oauth-clients', component: '02-user-access/views/OAuthClients', icon: 'SafetyOutlined', sort: 3, permission: 'oauth:read', roles: ['Admin'], visible: true, cache: true },
        { id: 'm-02-04', parentId: 'm-02', name: '运营人员', type: 'Menu', path: '/user-access/operators', component: '02-user-access/views/Operators', icon: 'TeamOutlined', sort: 4, permission: 'operator:read', roles: ['Admin', 'Operator'], visible: true, cache: true },
        { id: 'm-02-05', parentId: 'm-02', name: '菜单管理', type: 'Menu', path: '/user-access/menus', component: '02-user-access/views/MenuManagement', icon: 'MenuOutlined', sort: 5, permission: 'menu:write', roles: ['Admin'], visible: true, cache: false },
        { id: 'm-02-06', parentId: 'm-02', name: '在线用户', type: 'Menu', path: '/user-access/online-users', component: '02-user-access/views/OnlineUsers', icon: 'TeamOutlined', sort: 6, permission: 'online-user:read', roles: ['Admin'], visible: true, cache: false },
      ],
    },
    {
      id: 'm-03',
      parentId: null,
      name: '系统治理',
      type: 'Directory',
      path: '/system-governance',
      component: null,
      icon: 'SettingOutlined',
      sort: 3,
      permission: null,
      roles: ['Admin'],
      visible: true,
      cache: false,
      children: [
        { id: 'm-03-01', parentId: 'm-03', name: '功能开关', type: 'Menu', path: '/system-governance/feature-flags', component: '03-system-governance/views/FeatureFlags', icon: 'SwitcherOutlined', sort: 1, permission: null, roles: ['Admin'], visible: true, cache: true },
        { id: 'm-03-02', parentId: 'm-03', name: '系统配置', type: 'Menu', path: '/system-governance/system-configs', component: '03-system-governance/views/SystemConfigs', icon: 'SettingOutlined', sort: 2, permission: null, roles: ['Admin', 'Operator'], visible: true, cache: true },
        { id: 'm-03-03', parentId: 'm-03', name: '数据字典', type: 'Menu', path: '/system-governance/data-dictionaries', component: '03-system-governance/views/DataDictionaries', icon: 'BookOutlined', sort: 3, permission: null, roles: ['Admin', 'Operator'], visible: true, cache: true },
        { id: 'm-03-04', parentId: 'm-03', name: '公告管理', type: 'Menu', path: '/system-governance/announcements', component: '03-system-governance/views/Announcements', icon: 'NotificationOutlined', sort: 4, permission: null, roles: ['Admin', 'Operator'], visible: true, cache: true },
      ],
    },
    {
      id: 'm-04',
      parentId: null,
      name: '运行时运维',
      type: 'Directory',
      path: '/runtime-ops',
      component: null,
      icon: 'ToolOutlined',
      sort: 4,
      permission: null,
      roles: ['Admin'],
      visible: true,
      cache: false,
      children: [
        { id: 'm-04-01', parentId: 'm-04', name: '限流规则', type: 'Menu', path: '/runtime-ops/rate-limit-rules', component: '04-runtime-ops/views/RateLimitRules', icon: 'ThunderboltOutlined', sort: 1, permission: 'rate-limit:write', roles: ['Admin'], visible: true, cache: true },
        { id: 'm-04-02', parentId: 'm-04', name: '索引重建', type: 'Menu', path: '/runtime-ops/index-rebuild', component: '04-runtime-ops/views/IndexRebuild', icon: 'DatabaseOutlined', sort: 2, permission: 'index-rebuild:trigger', roles: ['Admin', 'Operator'], visible: true, cache: true },
        { id: 'm-04-03', parentId: 'm-04', name: '死信队列', type: 'Menu', path: '/runtime-ops/dead-letter-queue', component: '04-runtime-ops/views/DeadLetterQueue', icon: 'WarningOutlined', sort: 3, permission: 'dead-letter:dispose', roles: ['Admin', 'Operator'], visible: true, cache: true },
        { id: 'm-04-04', parentId: 'm-04', name: '定时任务', type: 'Menu', path: '/runtime-ops/scheduled-tasks', component: '04-runtime-ops/views/ScheduledTasks', icon: 'ClockCircleOutlined', sort: 4, permission: 'scheduled-task:write', roles: ['Admin', 'Operator'], visible: true, cache: true },
        { id: 'm-04-05', parentId: 'm-04', name: '健康监控', type: 'Menu', path: '/runtime-ops/health-monitoring', component: '04-runtime-ops/views/HealthMonitoring', icon: 'HeartOutlined', sort: 5, permission: null, roles: ['Admin', 'Operator'], visible: true, cache: true },
        { id: 'm-04-06', parentId: 'm-04', name: '告警管理', type: 'Menu', path: '/runtime-ops/alert-management', component: '04-runtime-ops/views/AlertManagement', icon: 'BellOutlined', sort: 6, permission: 'alert:manage', roles: ['Admin'], visible: true, cache: true },
        { id: 'm-04-07', parentId: 'm-04', name: '缓存监控', type: 'Menu', path: '/runtime-ops/cache-monitor', component: '04-runtime-ops/views/CacheMonitor', icon: 'DatabaseOutlined', sort: 7, permission: 'cache:read', roles: ['Admin'], visible: true, cache: false },
      ],
    },
    {
      id: 'm-05',
      parentId: null,
      name: '审计与对账',
      type: 'Directory',
      path: '/audit',
      component: null,
      icon: 'AuditOutlined',
      sort: 5,
      permission: null,
      roles: ['Admin'],
      visible: true,
      cache: false,
      children: [
        { id: 'm-05-01', parentId: 'm-05', name: '审计日志', type: 'Menu', path: '/audit/audit-logs', component: '05-audit/views/AuditLogs', icon: 'FileSearchOutlined', sort: 1, permission: 'audit-log:read', roles: ['Admin', 'Operator'], visible: true, cache: true },
        { id: 'm-05-02', parentId: 'm-05', name: '对账管理', type: 'Menu', path: '/audit/reconciliation', component: '05-audit/views/Reconciliation', icon: 'AuditOutlined', sort: 2, permission: 'reconciliation:trigger', roles: ['Admin', 'Operator'], visible: true, cache: true },
        { id: 'm-05-03', parentId: 'm-05', name: 'Outbox 监控', type: 'Menu', path: '/audit/outbox-monitor', component: '05-audit/views/OutboxMonitor', icon: 'InboxOutlined', sort: 3, permission: 'outbox:manage', roles: ['Admin'], visible: true, cache: true },
        { id: 'm-05-04', parentId: 'm-05', name: '登录日志', type: 'Menu', path: '/audit/login-logs', component: '05-audit/views/LoginLogs', icon: 'LoginOutlined', sort: 4, permission: 'login-log:read', roles: ['Admin', 'Operator'], visible: true, cache: false },
      ],
    },
    {
      id: 'm-06',
      parentId: null,
      name: '个人账号',
      type: 'Directory',
      path: '/account',
      component: null,
      icon: 'UserOutlined',
      sort: 6,
      permission: null,
      roles: ['Admin', 'Operator'],
      visible: true,
      cache: false,
      children: [
        { id: 'm-06-01', parentId: 'm-06', name: '个人中心', type: 'Menu', path: '/account/profile', component: '06-account/views/Profile', icon: 'UserOutlined', sort: 1, permission: null, roles: ['Admin', 'Operator'], visible: true, cache: false },
      ],
    },
    {
      id: 'm-07',
      parentId: null,
      name: '系统监控',
      type: 'Directory',
      path: '/monitoring',
      component: null,
      icon: 'MonitorOutlined',
      sort: 7,
      permission: null,
      roles: ['Admin', 'Operator'],
      visible: true,
      cache: false,
      children: [
        { id: 'm-07-01', parentId: 'm-07', name: 'Prometheus 监控看板', type: 'Menu', path: '/monitoring/prometheus-dashboard', component: '07-monitoring/views/PrometheusDashboard', icon: 'MonitorOutlined', sort: 1, permission: null, roles: ['Admin', 'Operator'], visible: true, cache: false },
        { id: 'm-07-02', parentId: 'm-07', name: '服务器监控', type: 'Menu', path: '/monitoring/server-monitor', component: '07-monitoring/views/ServerMonitor', icon: 'DesktopOutlined', sort: 2, permission: 'server-monitor:read', roles: ['Admin'], visible: true, cache: false },
      ],
    },
  ]
}

// ===== 在线用户种子（12 条）=====

function buildOnlineUserSeed(): unknown[] {
  const users = ['admin', 'operator', 'test01', 'test02', 'test03', 'test04', 'test05', 'test06', 'test07', 'test08', 'test09', 'test10']
  const ips = ['192.168.1.100', '192.168.1.101', '10.0.0.50', '172.16.0.20', '114.114.114.114', '8.8.8.8']
  const geos = ['内网·本地', '内网·本地', '内网·本地', '内网·本地', '中国·上海', '美国·加州']
  const browsers = ['Chrome 120', 'Firefox 121', 'Safari 17', 'Edge 120']
  const oses = ['Windows 11', 'macOS 14', 'Ubuntu 22.04', 'CentOS 7']
  const now = Date.now()
  return users.map((username, i) => {
    const ipIdx = i % ips.length
    const isAnomaly = username === 'test03' || username === 'test07'
    const roles = username === 'admin' ? ['Admin'] : username === 'operator' ? ['Operator'] : []
    return {
      id: `ou-${i + 1}`,
      userId: `u-${i + 1}`,
      username,
      roles,
      ipAddress: ips[ipIdx],
      geoLocation: geos[ipIdx],
      browser: browsers[i % browsers.length],
      os: oses[i % oses.length],
      loginAt: new Date(now - (1 + i) * 3600_000).toISOString(),
      lastActivityAt: new Date(now - Math.floor(Math.random() * 5 * 60_000)).toISOString(),
      sessionDurationMs: 0, // 派生字段，handler 中实时计算
      tokenPreview: `tok${(i + 1).toString().padStart(4, '0')}`.slice(0, 8),
      deviceFingerprint: `fp-${i + 1}-${Math.random().toString(36).slice(2, 10)}`,
      requestCount: Math.floor(Math.random() * 500) + 10,
      isAnomaly,
    }
  })
}

// ===== 登录日志种子（100 条）=====

function buildLoginLogSeed(): unknown[] {
  const usernames = ['admin', 'operator', 'test01', 'test02', 'test03', 'unknown']
  const ips = ['192.168.1.100', '192.168.1.101', '10.0.0.50', '172.16.0.20', '114.114.114.114', '8.8.8.8']
  const geos = ['内网·本地', '内网·本地', '内网·本地', '内网·本地', '中国·上海', '美国·加州']
  const browsers = ['Chrome 120', 'Firefox 121', 'Safari 17', 'Edge 120']
  const oses = ['Windows 11', 'macOS 14', 'Ubuntu 22.04', 'CentOS 7']
  const failureReasons = ['密码错误', '账号锁定', '验证码错误', 'IP 黑名单']
  const failureWeights = [0.6, 0.15, 0.2, 0.05]
  const now = Date.now()
  const logs: unknown[] = []
  for (let i = 0; i < 100; i++) {
    const rand = Math.random()
    let hoursAgo: number
    if (rand < 0.4) hoursAgo = Math.random() * 24
    else if (rand < 0.75) hoursAgo = 24 + Math.random() * 48
    else hoursAgo = 72 + Math.random() * 96
    const loginAt = new Date(now - hoursAgo * 3600_000).toISOString()
    const username = usernames[Math.floor(Math.random() * usernames.length)]
    const ipIdx = Math.floor(Math.random() * ips.length)
    const isSuccess = Math.random() < 0.8
    const result = isSuccess ? 'Success' : 'Failed'
    const failureReason = isSuccess ? null : weightedPick(failureReasons, failureWeights)
    const durationMs = isSuccess ? 80 + Math.floor(Math.random() * 220) : 50 + Math.floor(Math.random() * 100)
    logs.push({
      id: `ll-${i + 1}`,
      username,
      ipAddress: ips[ipIdx],
      geoLocation: geos[ipIdx],
      browser: browsers[Math.floor(Math.random() * browsers.length)],
      os: oses[Math.floor(Math.random() * oses.length)],
      result,
      failureReason,
      durationMs,
      userAgent: `Mozilla/5.0 (${oses[Math.floor(Math.random() * oses.length)]}) ${browsers[Math.floor(Math.random() * browsers.length)]}`,
      deviceFingerprint: `fp-${Math.random().toString(36).slice(2, 12)}`,
      refererUrl: 'https://admin.leno.com/login',
      traceId: generateIdempotencyKey().replace(/-/g, '').slice(0, 16),
      loginAt,
    })
  }
  logs.sort((a, b) => {
    const ta = new Date((a as { loginAt: string }).loginAt).getTime()
    const tb = new Date((b as { loginAt: string }).loginAt).getTime()
    return tb - ta
  })
  return logs
}

function weightedPick(items: string[], weights: number[]): string {
  const total = weights.reduce((s, w) => s + w, 0)
  let r = Math.random() * total
  for (let i = 0; i < items.length; i++) {
    r -= weights[i]
    if (r <= 0) return items[i]
  }
  return items[items.length - 1]
}

// ===== Redis 信息与 Keyspace 种子 =====

function buildRedisInfoSeed(): unknown {
  return {
    redisVersion: '7.2.3',
    redisMode: 'standalone',
    os: 'Linux 6.5.0-14-generic x86_64',
    archBits: '64',
    tcpPort: 6379,
    uptimeInDays: 45,
    connectedClients: 24,
    usedMemoryHuman: '512.45M',
    usedMemoryPeakHuman: '780.12M',
    maxmemoryHuman: '2.00G',
    memFragmentationRatio: 1.12,
    totalConnectionsReceived: 152340,
    totalCommandsProcessed: 1859234,
    keyspaceHits: 1245890,
    keyspaceMisses: 45320,
    evictedKeys: 12,
  }
}

function buildKeyspaceSeed(): unknown[] {
  return Array.from({ length: 16 }, (_, db) => {
    if (db === 0) return { db, keys: 1243, expires: 120, avgTtl: 3600000 }
    if (db === 1) return { db, keys: 87, expires: 50, avgTtl: 7200000 }
    if (db === 2) return { db, keys: 12, expires: 0, avgTtl: 0 }
    return { db, keys: 0, expires: 0, avgTtl: 0 }
  })
}

// ===== Redis Key 种子（50 个）=====

function buildRedisKeySeed(): unknown[] {
  const prefixes = [
    { prefix: 'user', count: 15 },
    { prefix: 'cart', count: 10 },
    { prefix: 'order', count: 8 },
    { prefix: 'rate_limit', count: 7 },
    { prefix: 'feature_flag', count: 5 },
    { prefix: 'lock', count: 5 },
  ]
  const types = ['string', 'string', 'string', 'string', 'string', 'string', 'string', 'string', 'string', 'string', 'string', 'string', 'string', 'string', 'string', 'string', 'string', 'string', 'string', 'hash', 'hash', 'hash', 'hash', 'hash', 'list', 'list', 'list', 'list', 'list', 'set', 'set']
  const ttls = [-1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, 3600, 3600, 3600, 3600, 3600, 3600, 3600, 3600, 3600, 3600, 3600, 3600, 3600, 3600, 3600, 86400, 86400, 86400, 86400, 86400, 86400, 86400, 86400, 86400, 86400, 86400, 60, 60, 60, 60, 60]
  const keys: unknown[] = []
  let idx = 0
  for (const { prefix, count } of prefixes) {
    for (let i = 0; i < count; i++) {
      const key = `${prefix}:${(idx + 1).toString().padStart(4, '0')}`
      const type = types[idx % types.length]
      const ttl = ttls[idx % ttls.length]
      const value = generateRedisValue(type, idx)
      const size = computeRedisSize(type, value)
      keys.push({ key, type, value, ttl, size, db: 0 })
      idx++
    }
  }
  return keys
}

function generateRedisValue(type: string, seed: number): unknown {
  if (type === 'string') {
    if (seed % 3 === 0) return JSON.stringify({ id: seed, name: `user-${seed}`, email: `user${seed}@example.com`, roles: ['Admin'] })
    if (seed % 3 === 1) return `simple-value-${seed}`
    return JSON.stringify({ count: seed, lastAccess: new Date().toISOString() })
  }
  if (type === 'hash') {
    const obj: Record<string, string> = {}
    const fieldCount = 5 + (seed % 6)
    for (let i = 0; i < fieldCount; i++) obj[`field${i}`] = `value${seed}-${i}`
    return obj
  }
  if (type === 'list') {
    return Array.from({ length: 5 + (seed % 16) }, (_, i) => `item-${seed}-${i}`)
  }
  if (type === 'set') {
    return Array.from({ length: 3 + (seed % 8) }, (_, i) => `member-${seed}-${i}`)
  }
  return null
}

function computeRedisSize(type: string, value: unknown): number {
  if (type === 'string') return String(value).length
  if (type === 'hash') return Object.keys(value as Record<string, unknown>).length
  if (type === 'list' || type === 'set') return (value as unknown[]).length
  return 0
}

// ===== 服务器监控种子 =====

function buildServerSnapshotSeed(): unknown {
  return {
    hostname: 'leno-prod-systemadmin-01',
    os: 'Linux 6.5.0-14-generic',
    kernelVersion: '6.5.0-14-generic',
    cpuModel: 'Intel Xeon E5-2680 v4 @ 2.40GHz',
    cpuCores: 8,
    cpuUsagePercent: 32.5,
    memoryTotalBytes: 17179869184,
    memoryUsedBytes: 8589934592,
    memoryCachedBytes: 2147483648,
    diskTotalBytes: 107374182400,
    diskUsedBytes: 53687091200,
    diskReadBytesPerSec: 1048576,
    diskWriteBytesPerSec: 2097152,
    loadAvg1: 1.25,
    loadAvg5: 1.1,
    loadAvg15: 0.95,
    processCount: 184,
    uptimeSeconds: 3888000,
    bootTime: '2026-06-12T08:00:00Z',
    dotnetRuntimeVersion: '8.0.11',
    gcTotalCollections: 12450,
    sampledAt: new Date().toISOString(),
  }
}

function initServerHistory(seed: MockSeed): void {
  const now = Date.now()
  const points = 300
  let cpu = 30
  let memUsed = 8589934592
  let diskRead = 1048576
  let diskWrite = 2097152
  for (let i = points - 1; i >= 0; i--) {
    const t = new Date(now - i * 1000).toISOString()
    cpu = nextCpuValue(cpu)
    memUsed = nextMemoryValue(memUsed)
    diskRead = nextDiskIoValue(diskRead)
    diskWrite = nextDiskIoValue(diskWrite)
    seed.serverHistory.cpu.push({ t, v: cpu })
    seed.serverHistory.memory.push({ t, v: memUsed })
    seed.serverHistory.diskIo.push({ t, v: diskRead + diskWrite })
  }
}

function nextCpuValue(prev: number): number {
  const base = prev + (Math.random() - 0.5) * 10
  const sine = Math.sin(Date.now() / 60000) * 5
  return Math.max(5, Math.min(95, base + sine))
}

function nextMemoryValue(prev: number): number {
  const delta = (Math.random() - 0.5) * 200_000_000
  return Math.max(4_000_000_000, Math.min(12_000_000_000, prev + delta))
}

function nextDiskIoValue(prev: number): number {
  const delta = (Math.random() - 0.5) * 500_000
  return Math.max(100_000, Math.min(5_000_000, prev + delta))
}

/**
 * 推进服务器监控历史窗口：追加一个新点，移除最旧点（保持 300 点）
 *
 * 供 server handler 的 GET /snapshot 调用。
 */
export function advanceServerHistory(seed: MockSeed): void {
  const lastCpu = seed.serverHistory.cpu[seed.serverHistory.cpu.length - 1]?.v ?? 30
  const lastMem = seed.serverHistory.memory[seed.serverHistory.memory.length - 1]?.v ?? 8589934592
  const lastDisk = seed.serverHistory.diskIo[seed.serverHistory.diskIo.length - 1]?.v ?? 3145728
  const t = new Date().toISOString()
  const newCpu = nextCpuValue(lastCpu)
  const newMem = nextMemoryValue(lastMem)
  const newDisk = nextDiskIoValue(lastDisk - 1048576) + nextDiskIoValue(lastDisk - 2097152)
  seed.serverHistory.cpu.push({ t, v: newCpu })
  seed.serverHistory.memory.push({ t, v: newMem })
  seed.serverHistory.diskIo.push({ t, v: newDisk })
  // 保持 300 点滚动窗口
  while (seed.serverHistory.cpu.length > 300) seed.serverHistory.cpu.shift()
  while (seed.serverHistory.memory.length > 300) seed.serverHistory.memory.shift()
  while (seed.serverHistory.diskIo.length > 300) seed.serverHistory.diskIo.shift()
  // 同步更新 snapshot
  const snap = seed.serverSnapshot as Record<string, unknown>
  snap.cpuUsagePercent = newCpu
  snap.memoryUsedBytes = newMem
  snap.diskReadBytesPerSec = Math.max(100_000, newDisk * 0.4)
  snap.diskWriteBytesPerSec = Math.max(100_000, newDisk * 0.6)
  snap.sampledAt = t
}

// ===== 店铺种子（双形态：兼容 P0 shop.store 与 P1 ShopInfoDto）=====

function buildShopSeed(): unknown {
  return {
    // P1 ShopInfoDto 形态
    id: 'shop-001',
    name: '示例服饰旗舰店',
    logo: '',
    description: '专注高品质男女装，20年匠心工艺',
    status: 'Active',
    mainCategory: '服装',
    customerService: {
      phone: '13800138000',
      email: 'service@example.com',
      onlineAccount: 'wx_shop001',
    },
    version: 1,
    createdAt: '2026-01-15T10:00:00Z',
    updatedAt: '2026-07-01T12:00:00Z',
    // P0 shop.store ShopDto 形态（兼容字段）
    shopId: 'shop-001',
    shopName: '示例服饰旗舰店',
    qualificationsStatus: {
      BusinessLicense: 'Approved',
      IdCard: 'Approved',
      BankAccount: 'Pending',
    },
  }
}

// ===== 资质种子（3 条）=====

function buildQualificationSeed(): unknown[] {
  return [
    {
      id: 'qual-001',
      type: 'BusinessLicense',
      fileName: '营业执照.pdf',
      fileUrl: '',
      status: 'Approved',
      submittedAt: '2026-01-15T10:00:00Z',
      auditedAt: '2026-01-16T09:00:00Z',
    },
    {
      id: 'qual-002',
      type: 'IdCard',
      fileName: '身份证.jpg',
      fileUrl: '',
      status: 'Approved',
      submittedAt: '2026-01-15T10:00:00Z',
      auditedAt: '2026-01-16T09:00:00Z',
    },
    {
      id: 'qual-003',
      type: 'BankAccount',
      fileName: '银行账户信息.pdf',
      fileUrl: '',
      status: 'Pending',
      submittedAt: '2026-07-20T14:00:00Z',
    },
  ]
}

// ===== 运费模板种子（2 个：固定运费 + 按重量）=====

function buildFreightTemplateSeed(): unknown[] {
  return [
    {
      id: 'ft-001',
      name: '全国统一运费',
      pricingType: 'Fixed',
      fixedFee: 10,
      freeShippingThreshold: undefined,
      regionRules: [],
      isEnabled: true,
      version: 1,
      createdAt: '2026-02-01T00:00:00Z',
      updatedAt: '2026-02-01T00:00:00Z',
    },
    {
      id: 'ft-002',
      name: '按重量计费',
      pricingType: 'ByWeight',
      fixedFee: undefined,
      freeShippingThreshold: 99,
      regionRules: [
        {
          id: 'r-001',
          regionCode: 'CN',
          regionName: '全国',
          firstUnit: 1,
          firstPrice: 8,
          nextUnit: 1,
          nextPrice: 2,
        },
      ],
      isEnabled: true,
      version: 1,
      createdAt: '2026-02-01T00:00:00Z',
      updatedAt: '2026-02-01T00:00:00Z',
    },
  ]
}

// ===== 物流公司种子（5 个）=====

function buildLogisticsCompanySeed(): unknown[] {
  return [
    { id: 'lc-001', name: '顺丰速运', code: 'SF', servicePhone: '95338', website: 'https://www.sf-express.com', supportsTracking: true, sortOrder: 1 },
    { id: 'lc-002', name: '中通快递', code: 'ZTO', servicePhone: '95311', website: 'https://www.zto.com', supportsTracking: true, sortOrder: 2 },
    { id: 'lc-003', name: '圆通速递', code: 'YTO', servicePhone: '95554', website: 'https://www.yto.net.cn', supportsTracking: true, sortOrder: 3 },
    { id: 'lc-004', name: '韵达快递', code: 'YUNDA', servicePhone: '95546', website: 'https://www.yundaex.com', supportsTracking: true, sortOrder: 4 },
    { id: 'lc-005', name: 'EMS', code: 'EMS', servicePhone: '11183', website: 'https://www.ems.com.cn', supportsTracking: true, sortOrder: 5 },
  ]
}

// ===== 评价种子（10 条：5 已回复 + 5 未回复，评分 1-5 星分布）=====

function buildReviewSeed(): unknown[] {
  return [
    { reviewId: 'rev-001', orderId: 'ord-101', orderLineId: 'ol-101', spuId: 'spu-001', skuId: 'sku-001', userId: 'u-001', userMaskedName: '13****5678', rating: 5, content: '质量非常好，面料舒适，做工精细，物流也很快！', images: [], status: 'Approved', sellerReplyContent: '感谢您的支持，欢迎再次光临！', sellerReplyBy: 'seller-001', sellerReplyAt: '2026-07-15T10:00:00Z', submittedAt: '2026-07-14T15:30:00Z', auditedAt: '2026-07-14T16:00:00Z', productName: '纯棉圆领T恤 白色 L', productImage: '', skuSpec: '白色 / L' },
    { reviewId: 'rev-002', orderId: 'ord-102', orderLineId: 'ol-102', spuId: 'spu-002', skuId: 'sku-002', userId: 'u-002', userMaskedName: '18****1234', rating: 4, content: '整体不错，就是尺码偏小，建议买大一码。', images: ['img-001.jpg'], status: 'Approved', sellerReplyContent: '感谢反馈，我们会优化尺码表。', sellerReplyBy: 'seller-001', sellerReplyAt: '2026-07-16T09:00:00Z', submittedAt: '2026-07-15T20:00:00Z', auditedAt: '2026-07-15T21:00:00Z', productName: '修身衬衫 蓝色 M', productImage: '', skuSpec: '蓝色 / M' },
    { reviewId: 'rev-003', orderId: 'ord-103', orderLineId: 'ol-103', spuId: 'spu-001', skuId: 'sku-003', userId: 'u-003', userMaskedName: '15****8888', rating: 5, content: '回购第三次了，一如既往的好！', images: [], status: 'Approved', sellerReplyContent: '感恩老客户，已为您发放优惠券！', sellerReplyBy: 'seller-001', sellerReplyAt: '2026-07-17T14:00:00Z', submittedAt: '2026-07-16T11:00:00Z', auditedAt: '2026-07-16T12:00:00Z', productName: '纯棉圆领T恤 黑色 XL', productImage: '', skuSpec: '黑色 / XL' },
    { reviewId: 'rev-004', orderId: 'ord-104', orderLineId: 'ol-104', spuId: 'spu-003', skuId: 'sku-004', userId: 'u-004', userMaskedName: '19****6666', rating: 3, content: '一般般，性价比还行，但颜色和图片有点色差。', images: ['img-002.jpg', 'img-003.jpg'], status: 'Approved', sellerReplyContent: '抱歉给您带来不便，我们会改进拍摄。', sellerReplyBy: 'seller-001', sellerReplyAt: '2026-07-18T10:00:00Z', submittedAt: '2026-07-17T16:00:00Z', auditedAt: '2026-07-17T17:00:00Z', productName: '雪纺连衣裙 粉色 S', productImage: '', skuSpec: '粉色 / S' },
    { reviewId: 'rev-005', orderId: 'ord-105', orderLineId: 'ol-105', spuId: 'spu-002', skuId: 'sku-005', userId: 'u-005', userMaskedName: '17****3333', rating: 4, content: '衬衫质量不错，包装也很好。', images: [], status: 'Approved', sellerReplyContent: '谢谢好评！', sellerReplyBy: 'seller-001', sellerReplyAt: '2026-07-19T08:00:00Z', submittedAt: '2026-07-18T09:00:00Z', auditedAt: '2026-07-18T10:00:00Z', productName: '修身衬衫 白色 L', productImage: '', skuSpec: '白色 / L' },
    { reviewId: 'rev-006', orderId: 'ord-106', orderLineId: 'ol-106', spuId: 'spu-001', skuId: 'sku-001', userId: 'u-006', userMaskedName: '13****9999', rating: 2, content: '面料有点硬，洗了一次就起球了，不太满意。', images: ['img-004.jpg'], status: 'Approved', submittedAt: '2026-07-20T13:00:00Z', auditedAt: '2026-07-20T14:00:00Z', productName: '纯棉圆领T恤 白色 L', productImage: '', skuSpec: '白色 / L' },
    { reviewId: 'rev-007', orderId: 'ord-107', orderLineId: 'ol-107', spuId: 'spu-003', skuId: 'sku-006', userId: 'u-007', userMaskedName: '18****7777', rating: 5, content: '裙子很漂亮，版型好，朋友都说好看！', images: [], status: 'Approved', submittedAt: '2026-07-21T10:00:00Z', auditedAt: '2026-07-21T11:00:00Z', productName: '雪纺连衣裙 蓝色 M', productImage: '', skuSpec: '蓝色 / M' },
    { reviewId: 'rev-008', orderId: 'ord-108', orderLineId: 'ol-108', spuId: 'spu-002', skuId: 'sku-002', userId: 'u-008', userMaskedName: '15****2222', rating: 1, content: '扣子掉了两个，质量太差了，要求退款。', images: ['img-005.jpg', 'img-006.jpg', 'img-007.jpg'], status: 'Approved', submittedAt: '2026-07-22T17:00:00Z', auditedAt: '2026-07-22T18:00:00Z', productName: '修身衬衫 蓝色 M', productImage: '', skuSpec: '蓝色 / M' },
    { reviewId: 'rev-009', orderId: 'ord-109', orderLineId: 'ol-109', spuId: 'spu-001', skuId: 'sku-003', userId: 'u-009', userMaskedName: '19****4444', rating: 4, content: '不错，穿着很舒服，就是快递有点慢。', images: [], status: 'Approved', submittedAt: '2026-07-23T08:00:00Z', auditedAt: '2026-07-23T09:00:00Z', productName: '纯棉圆领T恤 黑色 XL', productImage: '', skuSpec: '黑色 / XL' },
    { reviewId: 'rev-010', orderId: 'ord-110', orderLineId: 'ol-110', spuId: 'spu-003', skuId: 'sku-004', userId: 'u-010', userMaskedName: '17****0000', rating: 3, content: '裙子颜色不错但偏短，身高170穿刚好。', images: [], status: 'Approved', submittedAt: '2026-07-24T12:00:00Z', auditedAt: '2026-07-24T13:00:00Z', productName: '雪纺连衣裙 粉色 S', productImage: '', skuSpec: '粉色 / S' },
  ]
}
