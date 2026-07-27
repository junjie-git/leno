// web/system-admin/src/modules/05-audit/api/reconciliation.api.ts
// 对账管理 API：对齐 SystemAdmin BC StatisticsReconciliationService 端点
// 触发对账（POST）注入 Idempotency-Key 头；查询接口只读

import { client, withIdempotency } from '@/shared/http'
import type { PageResult, PageQuery } from '@/shared/types'
import type {
  ReconciliationStatusDto,
  ReconciliationRecordDto,
  TriggerReconciliationParams,
  ListReconciliationRecordsParams,
} from '../types/reconciliation.dto'

/** 对账记录列表请求（合并分页） */
export type ListReconciliationRecordsRequest = ListReconciliationRecordsParams & PageQuery

export const reconciliationApi = {
  /** 获取最近一次对账状态（顶部 4 个统计卡片数据源） */
  getStatus: () =>
    client.get<ReconciliationStatusDto>('/admin/statistics/reconciliation-status'),

  /** 分页查询对账记录列表（按报表类型与时间范围） */
  listRecords: (params: ListReconciliationRecordsRequest) =>
    client.get<PageResult<ReconciliationRecordDto>>('/admin/statistics/reconciliation-records', { params }),

  /** 手动触发对账（按报表类型与时间范围，幂等）
   *  reportType 未传则对账全部类型，返回多条记录；指定类型返回单条记录数组（长度 1）
   */
  trigger: (params: TriggerReconciliationParams) =>
    client.post<ReconciliationRecordDto[]>(
      '/admin/statistics/reconcile',
      null,
      { params, ...withIdempotency() },
    ),
}
