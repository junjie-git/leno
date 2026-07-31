/* eslint-disable @typescript-eslint/no-explicit-any */
import type MockAdapter from 'axios-mock-adapter'
import { loadSeedData, saveSeedData, nextId } from '../data/seed'

/**
 * 数据导出 handler 注册
 *
 * 端点（baseURL=/api，故拦截 /seller/export/...）：
 * - POST /seller/export/sales                创建导出任务（模拟异步：1.5s 后转 Completed）
 * - GET  /seller/export/tasks                查询任务列表
 * - GET  /seller/export/tasks/{id}/download  下载导出文件（返回 CSV 占位）
 */
export function registerExportHandlers(mock: MockAdapter): void {
  // 创建导出任务
  mock.onPost('/seller/export/sales').reply((config) => {
    const seed = loadSeedData()
    const body = JSON.parse(config.data || '{}')
    const now = new Date().toISOString()
    const task = {
      id: nextId(seed, 'export'),
      reportType: body.reportType || 'SalesSummary',
      startDate: body.startDate,
      endDate: body.endDate,
      format: body.format || 'Excel',
      status: 'Processing',
      recordCount: null,
      fileSize: null,
      createdAt: now,
      completedAt: null,
      errorMessage: null,
    }
    ;(seed.exportTasks as any[]).unshift(task)
    saveSeedData(seed)

    // 模拟后台作业 1.5s 后完成
    setTimeout(() => {
      const s = loadSeedData()
      const t = (s.exportTasks as any[]).find((x) => x.id === task.id)
      if (t) {
        t.status = 'Completed'
        t.recordCount = 42
        t.fileSize = 2048
        t.completedAt = new Date().toISOString()
        saveSeedData(s)
      }
    }, 1500)

    return [200, { code: 200, message: 'OK', data: task }]
  })

  // 查询导出任务列表
  mock.onGet('/seller/export/tasks').reply(() => {
    const seed = loadSeedData()
    const items = (seed.exportTasks as any[]) ?? []
    return [
      200,
      {
        code: 200,
        message: 'OK',
        data: {
          items,
          total: items.length,
          page: 1,
          pageSize: 50,
        },
      },
    ]
  })

  // 下载导出文件（返回 CSV 占位）
  mock.onGet(/\/seller\/export\/tasks\/[^/]+\/download$/).reply((config) => {
    const match = config.url?.match(/\/tasks\/([^/]+)\/download$/)
    const taskId = match?.[1] ?? 'unknown'
    const csv = `ReportType,StartDate,EndDate,RecordCount\n${taskId},2026-07-01,2026-07-31,42\n`
    return [200, csv, { 'Content-Type': 'text/csv' }]
  })
}
