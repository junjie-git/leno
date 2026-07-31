import { describe, expect, it, beforeEach, afterAll } from 'vitest'
import MockAdapter from 'axios-mock-adapter'
import { client, ServerError } from '@/shared/http'
import { exportApi } from './export.api'
import type { CreateExportTaskDto, ExportTaskDto } from '../types/export.dto'

/**
 * exportApi 单元测试
 *
 * 使用 axios-mock-adapter 挂载到真实 client 实例（含响应拦截器），
 * 验证 URL / method / params / body / Idempotency-Key 头 / 响应解包 / 501 错误转换。
 *
 * mock reply 体须为 { code, message, data } envelope，响应拦截器 unwrap data 后
 * 由 api 函数内部 .then(r => r.data) 二次解包。
 */
const mock = new MockAdapter(client, { onNoMatch: 'throwException' })

const sampleTask: ExportTaskDto = {
  id: 'et-001',
  reportType: 'SalesSummary',
  startDate: '2026-07-01',
  endDate: '2026-07-30',
  format: 'Excel',
  status: 'Processing',
  createdAt: '2026-07-30T10:00:00Z',
}

beforeEach(() => {
  mock.reset()
})

afterAll(() => {
  mock.restore()
})

describe('exportApi.createTask', () => {
  const body: CreateExportTaskDto = {
    reportType: 'SalesSummary',
    startDate: '2026-07-01',
    endDate: '2026-07-30',
    format: 'Excel',
  }

  it('调用 POST /seller/export/sales 带 Idempotency-Key 并解包 data', async () => {
    mock
      .onPost('/seller/export/sales')
      .reply(200, { code: 200, message: 'OK', data: sampleTask })

    const result = await exportApi.createTask(body)

    expect(result).toEqual(sampleTask)
    expect(mock.history.post).toHaveLength(1)
    expect(mock.history.post[0].url).toBe('/seller/export/sales')
    expect(mock.history.post[0].data).toBe(JSON.stringify(body))
    expect(mock.history.post[0].headers['Idempotency-Key']).toBeTruthy()
  })

  it('后端返回 501 抛 ServerError', async () => {
    mock.onPost('/seller/export/sales').reply(501, {
      code: 'SERVER_ERROR',
      message: '服务器内部错误',
    })

    await expect(exportApi.createTask(body)).rejects.toBeInstanceOf(ServerError)
  })
})

describe('exportApi.listTasks', () => {
  it('调用 GET /seller/export/tasks 带 params 并解包', async () => {
    const payload = { items: [sampleTask], total: 1 }
    mock
      .onGet('/seller/export/tasks')
      .reply(200, { code: 200, message: 'OK', data: payload })

    const result = await exportApi.listTasks({ page: 1, pageSize: 20 })

    expect(result).toEqual(payload)
    expect(mock.history.get).toHaveLength(1)
    expect(mock.history.get[0].url).toBe('/seller/export/tasks')
    expect(mock.history.get[0].params).toEqual({ page: 1, pageSize: 20 })
  })

  it('支持 status 筛选参数', async () => {
    mock
      .onGet('/seller/export/tasks')
      .reply(200, { code: 200, message: 'OK', data: { items: [], total: 0 } })

    await exportApi.listTasks({ page: 1, pageSize: 20, status: 'Processing' })

    expect(mock.history.get[0].params).toEqual({
      page: 1,
      pageSize: 20,
      status: 'Processing',
    })
  })

  it('后端返回空列表时正确解包', async () => {
    mock
      .onGet('/seller/export/tasks')
      .reply(200, { code: 200, message: 'OK', data: { items: [], total: 0 } })

    const result = await exportApi.listTasks({ page: 1, pageSize: 50 })

    expect(result).toEqual({ items: [], total: 0 })
  })
})

describe('exportApi.getDownloadUrl', () => {
  it('返回完整下载 URL 字符串（含 /api 前缀）', () => {
    const url = exportApi.getDownloadUrl('et-001')
    expect(url).toBe('/api/seller/export/tasks/et-001/download')
  })

  it('同步返回字符串，非 Promise', () => {
    const url = exportApi.getDownloadUrl('et-002')
    expect(typeof url).toBe('string')
    expect(url).not.toBeInstanceOf(Promise)
  })

  it('不同 taskId 生成不同 URL', () => {
    expect(exportApi.getDownloadUrl('a')).toBe(
      '/api/seller/export/tasks/a/download',
    )
    expect(exportApi.getDownloadUrl('b')).toBe(
      '/api/seller/export/tasks/b/download',
    )
  })
})
