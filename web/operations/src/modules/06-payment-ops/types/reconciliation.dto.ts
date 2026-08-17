import type { PageQuery, PageResult } from '@/shared/types'
import type { PaymentChannelType } from './payment.dto'

/**
 * 06-payment-ops 渠道对账 DTO
 *
 * 对接 Payment 域 AdminReconciliationController：
 * - GET  /api/admin/reconciliation/diffs            分页查询对账差异列表
 * - POST /api/admin/reconciliation/trigger?billDate 手动触发对账（异步任务，幂等）
 *
 * 状态机：PendingResolve（待处理）→ Resolved（已修复 / 已忽略）。
 */

/**
 * 对账差异类型
 * - LongAmount      长款：渠道有账但系统无记录（渠道多出款项）
 * - ShortAmount     短款：系统有记录但渠道无账（系统多出款项）
 * - AmountMismatch  金额不一致：两侧均有记录但金额不同
 * - MissingSystem   系统侧缺失（长款变体：回调丢失 / 第三方测试交易）
 * - MissingChannel  渠道侧缺失（短款变体：渠道账单延迟 / 支付未实际完成）
 */
export type ReconciliationDiffType =
  | 'LongAmount'
  | 'ShortAmount'
  | 'AmountMismatch'
  | 'MissingSystem'
  | 'MissingChannel'

/** 对账差异状态 */
export type ReconciliationDiffStatus = 'PendingResolve' | 'Resolved'

/** 对账差异状态时间线条目（详情抽屉 a-timeline 数据源） */
export interface DiffTimelineItemDto {
  /** 节点状态（如 Created / Resolved） */
  status: string
  /** 节点标题 */
  label: string
  /** 节点描述（可选，如处理备注） */
  description?: string
  /** 发生时间（ISO 8601） */
  occurredAt: string
}

/** 对账差异记录视图，列表行与详情抽屉共用 */
export interface ReconciliationDiffDto {
  id: string
  /** 账单日期（yyyy-MM-dd） */
  billDate: string
  /** 渠道 */
  channel: PaymentChannelType
  /** 差异类型 */
  diffType: ReconciliationDiffType
  /** 渠道流水号（点击复制到剪贴板） */
  channelTransactionNo?: string
  /** 渠道侧金额（元；渠道缺失时为空） */
  channelAmount?: number
  /** 渠道侧交易时间 */
  channelTransactionTime?: string
  /** 系统流水号（系统缺失时为空） */
  systemTransactionNo?: string
  /** 系统侧金额（元；系统缺失时为空） */
  systemAmount?: number
  /** 关联支付单 ID（点击跳转支付记录） */
  paymentId?: string
  /** 关联支付单号 */
  paymentNo?: string
  /** 备注（差异处理建议等） */
  remark?: string
  /** 差异状态 */
  status: ReconciliationDiffStatus
  /** 创建时间（ISO 8601，统计「近 7 天新增」依据） */
  createdAt: string
  /** 修复时间（Resolved 时有值） */
  resolvedAt?: string
  /** 处理人（Resolved 时有值） */
  resolvedBy?: string
  /** 状态时间线（详情抽屉展示；后端缺失时前端按字段合成） */
  timeline?: DiffTimelineItemDto[]
}

/** GET /api/admin/reconciliation/diffs 查询参数 */
export interface ReconciliationDiffQueryParams extends PageQuery {
  /** 账单日期（yyyy-MM-dd） */
  billDate?: string
  /** 渠道 */
  channel?: PaymentChannelType
  /** 差异类型 */
  diffType?: ReconciliationDiffType
  /** 差异状态 */
  status?: ReconciliationDiffStatus
}

/** GET /api/admin/reconciliation/diffs 响应（ReconciliationDiffListResultDto） */
export type ReconciliationDiffListResultDto = PageResult<ReconciliationDiffDto>
