// web/system-admin/src/modules/05-audit/api/audit-logs.api.spec.ts
import { describe, it, expect, vi, beforeEach } from 'vitest'
import { client } from '@/shared/http'
import { auditLogsApi } from './audit-logs.api'
import type {
  ListAuditLogsParams,
  ListOperationLogsParams,
  ListAuditLogEntriesParams,
  ExportAuditLogsParams,
} from '../types/audit-log.dto'

// 统一 mock shared/http 模块，client.get 替换为 spy（审计日志只读，无 post/put/delete）
vi.mock('@/shared/http', async () => {
  const actual = await vi.importActual<typeof import('@/shared/http')>('@/shared/http')
  return {
    ...actual,
    client: { get: vi.fn() },
    withIdempotency: actual.withIdempotency,
  }
})

describe('auditLogsApi', () => {
  beforeEach(() => {
    vi.clearAllMocks()
  })

  it('list 使用 /admin/audit-logs + params', async () => {
    ;(client.get as ReturnType<typeof vi.fn>).mockResolvedValue({
      data: { items: [], total: 0, page: 1, pageSize: 20 },
    })
    const params: ListAuditLogsParams = {
      operatorId: 'u-1',
      resourceType: 'Shop',
      action: 'Create',
      fromTime: '2026-07-27T00:00:00Z',
      toTime: '2026-07-27T23:59:59Z',
      page: 1,
      pageSize: 20,
    }
    await auditLogsApi.list(params)
    expect(client.get).toHaveBeenCalledWith('/admin/audit-logs', { params })
  })

  it('get 使用 /admin/audit-logs/{id} 路径', async () => {
    ;(client.get as ReturnType<typeof vi.fn>).mockResolvedValue({ data: {} })
    await auditLogsApi.get('log-1')
    expect(client.get).toHaveBeenCalledWith('/admin/audit-logs/log-1')
  })

  it('export 使用 responseType: blob 与导出参数', async () => {
    ;(client.get as ReturnType<typeof vi.fn>).mockResolvedValue({ data: new Blob() })
    const params: ExportAuditLogsParams = {
      resourceType: 'Shop',
      fromTime: '2026-07-27T00:00:00Z',
      toTime: '2026-07-27T23:59:59Z',
    }
    await auditLogsApi.export(params)
    expect(client.get).toHaveBeenCalledWith('/admin/audit-logs/export', {
      params,
      responseType: 'blob',
    })
  })

  it('listOperationLogs 使用 /admin/operation-logs + params', async () => {
    ;(client.get as ReturnType<typeof vi.fn>).mockResolvedValue({
      data: { items: [], total: 0, page: 1, pageSize: 20 },
    })
    const params: ListOperationLogsParams = {
      operatorId: 'u-1',
      module: 'Order',
      fromTime: '2026-07-27T00:00:00Z',
      toTime: '2026-07-27T23:59:59Z',
      page: 1,
      pageSize: 20,
    }
    await auditLogsApi.listOperationLogs(params)
    expect(client.get).toHaveBeenCalledWith('/admin/operation-logs', { params })
  })

  it('listAuditLogEntries 使用 /admin/audit-log-entries + params', async () => {
    ;(client.get as ReturnType<typeof vi.fn>).mockResolvedValue({
      data: { items: [], total: 0, page: 1, pageSize: 20 },
    })
    const params: ListAuditLogEntriesParams = {
      module: 'Order',
      action: 'Create',
      fromTime: '2026-07-27T00:00:00Z',
      toTime: '2026-07-27T23:59:59Z',
      page: 1,
      pageSize: 20,
    }
    await auditLogsApi.listAuditLogEntries(params)
    expect(client.get).toHaveBeenCalledWith('/admin/audit-log-entries', { params })
  })
})
