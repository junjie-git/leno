import { describe, expect, it, beforeEach, afterEach, vi } from 'vitest'
import MockAdapter from 'axios-mock-adapter'
import { client } from '@/shared/http'
import type { PageResult } from '@/shared/types'
import {
  addExportTask,
  buildCsv,
  clearExpiredExportTasks,
  csvEscape,
  downloadTaskCsv,
  fetchExportRows,
  generateExportTaskId,
  hasRecentDuplicate,
  loadExportTasks,
  removeExportTask,
  updateExportTask,
} from './export.api'
import type { ExportTaskRecord } from '../types/export.dto'

/**
 * 数据导出 API 单测（axios-mock-adapter 挂载到真实 client 实例）
 *
 * 覆盖点：
 * - fetchExportRows 分页聚合（跨页拼接 / 进度回调 / 查询参数映射 / 截断保护）
 * - 各业务类型状态与标签序列化（订单 / 支付 / 通知）
 * - buildCsv / csvEscape 转义（BOM + CRLF + 引号逗号包裹）
 * - localStorage 任务历史（增删改查 / 5 分钟防重复 / 7 天过期清理）
 * - downloadTaskCsv 空文件（过期）不触发下载
 */

function pageOf<T>(items: T[], total: number, page: number, pageSize: number): PageResult<T> {
  return { items, total, page, pageSize }
}

function ok<T>(data: T): [number, { code: number; message: string; data: T }] {
  return [200, { code: 200, message: 'OK', data }]
}

describe('10-data-export export.api', () => {
  let mock: MockAdapter

  beforeEach(() => {
    mock = new MockAdapter(client)
    localStorage.clear()
  })

  afterEach(() => {
    mock.restore()
    localStorage.clear()
    vi.restoreAllMocks()
  })

  // ---------- fetchExportRows：订单跨页聚合 ----------

  it('fetchExportRows 订单类型跨页聚合并回调进度', async () => {
    const orderPage1 = pageOf(
      [
        {
          id: 'o1',
          orderNo: 'NO20260817001',
          userId: 'U1001',
          sellerId: 'S2001',
          itemSummary: '无线耳机 x1',
          totalAmount: 299,
          paymentMethod: 'Alipay',
          status: 'Completed',
          createdAt: '2026-08-16T10:00:00.000Z',
        },
      ],
      2,
      1,
      1,
    )
    const orderPage2 = pageOf(
      [
        {
          id: 'o2',
          orderNo: 'NO20260817002',
          userId: 'U1002',
          sellerId: 'S2002',
          itemSummary: '手机壳 x2',
          totalAmount: 59.5,
          paymentMethod: undefined,
          status: 'PendingPayment',
          createdAt: '2026-08-17T02:30:00.000Z',
        },
      ],
      2,
      2,
      1,
    )

    mock
      .onGet('/admin/orders')
      .reply((config) => {
        const params = config.params as Record<string, unknown>
        if (params.page === 1) return ok(orderPage1)
        return ok(orderPage2)
      })

    const progress = vi.fn()
    const result = await fetchExportRows('Order', {
      fromTime: '2026-08-10T00:00:00.000Z',
      toTime: '2026-08-17T23:59:59.000Z',
      filters: { keyword: 'NO2026', status: 'Completed' },
      pageSize: 1,
      onProgress: progress,
    })

    // 跨页拼接完成
    expect(result.rows).toHaveLength(2)
    expect(result.total).toBe(2)
    expect(result.truncated).toBe(false)
    // 表头与首行序列化（状态译为中文、金额两位小数、空支付方式兜底）
    expect(result.header).toEqual(['订单号', '买家ID', '卖家ID', '商品摘要', '总金额(元)', '支付方式', '状态', '下单时间'])
    expect(result.rows[0]).toEqual([
      'NO20260817001', 'U1001', 'S2001', '无线耳机 x1', '299.00', 'Alipay', '已完成', '2026-08-16T10:00:00.000Z',
    ])
    expect(result.rows[1][5]).toBe('—')
    expect(result.rows[1][6]).toBe('待支付')
    // 进度回调：每页一次
    expect(progress).toHaveBeenCalledTimes(2)
    expect(progress).toHaveBeenNthCalledWith(1, 1, 2)
    expect(progress).toHaveBeenNthCalledWith(2, 2, 2)
    // 查询参数映射：keyword→orderNo、状态与时间范围透传
    const firstReq = mock.history.get[0]
    expect(firstReq.params).toMatchObject({
      page: 1,
      pageSize: 1,
      orderNo: 'NO2026',
      status: 'Completed',
      fromTime: '2026-08-10T00:00:00.000Z',
      toTime: '2026-08-17T23:59:59.000Z',
    })
  })

  it('fetchExportRows 超过 maxRows 截断并标记 truncated', async () => {
    const items = Array.from({ length: 5 }, (_, i) => ({
      id: `p${i}`,
      paymentNo: `PAY2026${i}`,
      orderId: `order-${i}`,
      orderNo: `NO2026${i}`,
      userId: `U${i}`,
      userName: `用户${i}`,
      amount: 10 + i,
      channel: i % 2 === 0 ? 'WeChat' : 'Alipay',
      status: i % 3 === 0 ? 'Failed' : 'Success',
      createdAt: '2026-08-15T08:00:00.000Z',
      abnormal: false,
    }))
    mock.onGet('/admin/payments').reply(() => ok({ ...pageOf(items, 5, 1, 5), statusCounts: {}, successRate: 1 }))

    const result = await fetchExportRows('Payment', {
      fromTime: '2026-08-01T00:00:00.000Z',
      toTime: '2026-08-17T00:00:00.000Z',
      maxRows: 3,
      pageSize: 5,
    })

    expect(result.rows).toHaveLength(3)
    expect(result.total).toBe(5)
    expect(result.truncated).toBe(true)
    // 渠道与状态中文标签
    expect(result.rows[0][4]).toBe('微信支付')
    expect(result.rows[0][5]).toBe('支付失败')
    expect(result.rows[1][5]).toBe('已支付')
  })

  it('fetchExportRows 通知类型映射渠道 / 状态标签', async () => {
    mock
      .onGet('/notifications/records')
      .reply(() => ok(pageOf(
        [
          {
            id: 'n1',
            userId: 'U1001',
            recipient: '138****1234',
            channel: 'Sms',
            templateCode: 'ORDER_PAID',
            status: 'Delivered',
            businessRef: 'NO20260817001',
            retryCount: 0,
            createdAt: '2026-08-17T09:00:00.000Z',
          },
        ],
        1,
        1,
        10,
      )))

    const result = await fetchExportRows('Notification', {
      fromTime: '2026-08-17T00:00:00.000Z',
      toTime: '2026-08-17T23:59:59.000Z',
      filters: { keyword: 'U1001' },
      pageSize: 10,
    })

    expect(result.rows[0]).toEqual([
      'n1', 'U1001', '138****1234', '短信', 'ORDER_PAID', '已送达', 'NO20260817001', '0', '2026-08-17T09:00:00.000Z',
    ])
    // keyword → userId 映射
    expect(mock.history.get[0].params).toMatchObject({ userId: 'U1001' })
  })

  // ---------- CSV 构建 ----------

  it('csvEscape 转义引号 / 逗号 / 换行，buildCsv 输出 BOM + CRLF', () => {
    expect(csvEscape('plain')).toBe('plain')
    expect(csvEscape('a,b')).toBe('"a,b"')
    expect(csvEscape('say "hi"')).toBe('"say ""hi"""')
    expect(csvEscape('line1\nline2')).toBe('"line1\nline2"')

    const csv = buildCsv(['列A', '列B'], [['1', 'x,y'], ['2', 'he said "ok"']])
    expect(csv.startsWith('\uFEFF')).toBe(true)
    expect(csv).toBe('\uFEFF列A,列B\r\n1,"x,y"\r\n2,"he said ""ok"""')
  })

  // ---------- localStorage 任务历史 ----------

  function makeTask(overrides: Partial<ExportTaskRecord> = {}): ExportTaskRecord {
    return {
      id: generateExportTaskId(),
      taskName: '订单导出 08-10 ~ 08-17',
      businessType: 'Order',
      fromTime: '2026-08-10T00:00:00.000Z',
      toTime: '2026-08-17T23:59:59.000Z',
      filters: {},
      status: 'Completed',
      recordCount: 2,
      progress: 100,
      csv: '\uFEFF订单号\r\nNO1',
      createdBy: 'op-admin',
      createdAt: '2026-08-17T10:00:00.000Z',
      completedAt: '2026-08-17T10:00:05.000Z',
      ...overrides,
    }
  }

  it('任务历史增删改查闭环（localStorage 持久化）', () => {
    const task = makeTask()
    addExportTask(task)
    expect(loadExportTasks()).toHaveLength(1)
    expect(loadExportTasks()[0].id).toBe(task.id)

    updateExportTask({ ...task, status: 'Failed', errorMessage: '网络异常' })
    const updated = loadExportTasks()
    expect(updated).toHaveLength(1)
    expect(updated[0].status).toBe('Failed')
    expect(updated[0].errorMessage).toBe('网络异常')

    removeExportTask(task.id)
    expect(loadExportTasks()).toHaveLength(0)
  })

  it('任务列表按创建时间倒序排列', () => {
    addExportTask(makeTask({ id: 't1', createdAt: '2026-08-15T10:00:00.000Z' }))
    addExportTask(makeTask({ id: 't2', createdAt: '2026-08-17T10:00:00.000Z' }))
    const tasks = loadExportTasks()
    expect(tasks.map((t) => t.id)).toEqual(['t2', 't1'])
  })

  it('hasRecentDuplicate 同业务同时间范围 5 分钟内命中，超窗或不同范围不命中', () => {
    const from = '2026-08-10T00:00:00.000Z'
    const to = '2026-08-17T23:59:59.000Z'
    const now = new Date('2026-08-17T10:04:00.000Z')
    addExportTask(makeTask({ createdAt: '2026-08-17T10:00:00.000Z' }))

    expect(hasRecentDuplicate('Order', from, to, now)).toBe(true)
    // 超出 5 分钟窗口
    expect(hasRecentDuplicate('Order', from, to, new Date('2026-08-17T10:06:00.000Z'))).toBe(false)
    // 不同业务类型 / 不同时间范围
    expect(hasRecentDuplicate('Refund', from, to, now)).toBe(false)
    expect(hasRecentDuplicate('Order', from, '2026-08-18T23:59:59.000Z', now)).toBe(false)
  })

  it('clearExpiredExportTasks 清理 7 天前的任务并持久化', () => {
    addExportTask(makeTask({ id: 'old', createdAt: '2026-08-01T10:00:00.000Z' }))
    addExportTask(makeTask({ id: 'fresh', createdAt: '2026-08-17T10:00:00.000Z' }))

    const survivors = clearExpiredExportTasks(new Date('2026-08-17T12:00:00.000Z'))

    expect(survivors.map((t) => t.id)).toEqual(['fresh'])
    expect(loadExportTasks().map((t) => t.id)).toEqual(['fresh'])
  })

  it('localStorage 数据损坏时降级为空列表', () => {
    localStorage.setItem('operations.data-export.tasks', '{broken json')
    expect(loadExportTasks()).toEqual([])
  })

  // ---------- 下载 ----------

  it('downloadTaskCsv 文件缺失（过期）返回 false，不触发下载', () => {
    expect(downloadTaskCsv(makeTask({ csv: '' }))).toBe(false)
  })

  it('downloadTaskCsv 正常触发浏览器下载并使用业务类型文件名', () => {
    const createObjectURL = vi.fn(() => 'blob:mock-url')
    const revokeObjectURL = vi.fn()
    Object.defineProperty(URL, 'createObjectURL', { value: createObjectURL, configurable: true })
    Object.defineProperty(URL, 'revokeObjectURL', { value: revokeObjectURL, configurable: true })
    // 点击后链接即被移除，在 click 回调中捕获 download 属性以断言文件名
    let clickedDownload = ''
    const clickSpy = vi.fn(function (this: HTMLAnchorElement) {
      clickedDownload = this.download
    })
    vi.spyOn(HTMLAnchorElement.prototype, 'click').mockImplementation(clickSpy)

    const task = makeTask({ businessType: 'Refund' })
    const result = downloadTaskCsv(task)

    expect(result).toBe(true)
    expect(createObjectURL).toHaveBeenCalledTimes(1)
    expect(clickSpy).toHaveBeenCalledTimes(1)
    expect(clickedDownload).toBe(`退款导出_${task.createdAt.replace(/[:.]/g, '-')}.csv`)
    expect(revokeObjectURL).toHaveBeenCalledWith('blob:mock-url')
  })
})
