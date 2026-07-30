/* eslint-disable @typescript-eslint/no-explicit-any */
import type MockAdapter from 'axios-mock-adapter'
import { loadSeedData } from '../data/seed'

/**
 * 数据导出 handler 注册
 *
 * 端点（baseURL=/api，故拦截 /seller/export/...）：
 * - POST /seller/export/sales                创建导出任务 → 501（BE-3）
 * - GET  /seller/export/tasks                查询任务列表 → 200 空列表占位
 * - GET  /seller/export/tasks/{id}/download  下载导出文件 → 501（BE-3）
 *
 * BE-3 策略：createTask 与 download 返回 HTTP 501 + BE-3 标记，
 * 响应拦截器转为 ServerError；listTasks 返回 200 + 空列表，
 * 页面据此展示 EmptyState 并不触发轮询。
 */
export function registerExportHandlers(mock: MockAdapter): void {
  // 创建导出任务（BE-3）
  mock.onPost('/seller/export/sales').reply(() => {
    return [
      501,
      {
        code: 'BE-3',
        message: 'BE-3 待后端实现：创建导出任务',
      },
    ]
  })

  // 查询导出任务列表（BE-3：返回空列表占位，便于页面渲染空状态）
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
        },
      },
    ]
  })

  // 下载导出文件（BE-3）
  mock.onGet(/\/seller\/export\/tasks\/[^/]+\/download$/).reply(() => {
    return [
      501,
      {
        code: 'BE-3',
        message: 'BE-3 待后端实现：下载导出文件',
      },
    ]
  })
}
