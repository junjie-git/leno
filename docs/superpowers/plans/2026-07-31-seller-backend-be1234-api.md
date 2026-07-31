# 卖家后台缺失后端 API 实施计划（BE-1/2/3/4）

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 实现 BE-1（Order 分页契约统一）、BE-2（低库存告警跨 BC）、BE-3（数据导出完整异步链路）、BE-4（通知前端接入），清理全部 BE 标记，全量测试通过并提交推送。

**Architecture:** 延续现有 DDD 分层与 CQRS 模式。BE-1 将 `OrderListQuery`/`OrderListResult` 迁移到共享 `PageRequest`/`PageResult<T>`。BE-2 经 SellerShop gRPC ACL 反查 Product 域 SPU+SKU 低库存数据（与现有 `GrpcProductAntiCorruptionClient` 模式一致）。BE-3 新建 `ExportTask` 聚合 + `ExportBackgroundService`（`HostedService`）+ ClosedXML 生成 Excel + `IFileStorageService`（新增 "export" 类别）落盘 + gRPC ACL 聚合跨 BC 数据。BE-4 纯前端接入后端已就绪的 4 个通知端点。

**Tech Stack:** .NET 8 + EF Core 8 + MassTransit + gRPC + ClosedXML 0.102 + xUnit + FluentAssertions + Moq；Vue 3.5 + TypeScript 5.7 + Vite 6 + Ant Design Vue 4.2 + axios-mock-adapter 2.1 + Vitest 2.1

---

## 关键设计决策（实施前必读）

1. **ACL 使用 gRPC 而非 HTTP**：现有 `IProductAntiCorruptionService`/`IOrderAntiCorruptionService` 均通过 gRPC 实现（`GrpcProductAntiCorruptionClient`/`GrpcOrderAntiCorruptionClient`）。BE-2/BE-3 跨 BC 数据查询沿用 gRPC 模式，新增 proto rpc 方法，不引入 HTTP 内部端点（与 spec 第 5/6 节描述的 "internal/v1 HTTP" 不同，实施以现有 gRPC 模式为准以保持一致性）。
2. **BE-2 低库存查询路径**：`StockBaseline` 聚合无 `ShopId` 字段，`ShopId` 在 `SPU` 聚合上。查询路径为：gRPC `GetLowStockByShop(shopId, threshold)` → Product 域 `ISPURepository.GetByShopIdAsync` → 遍历 SPU.Skus 过滤 `StockQty < threshold` → 返回 `LowStockSkuDto` 列表。
3. **`http` 别名**：前端 `shared/http/index.ts` 已导出 `http`（批次 1 已完成），本计划所有新前端 API 客户端使用 `import { http, withIdempotency } from '@/shared/http'`。
4. **写操作幂等**：`createTask`/`markAsRead` 等写操作注入 `withIdempotency()`。
5. **响应解包**：前端 API 函数内部 `.then(r => r.data)` 解包（响应拦截器已 unwrap `ApiResponse.data`）。
6. **验证命令工作目录**：后端命令在 `/workspace`（`dotnet test`/`dotnet build`），前端命令在 `/workspace/web/seller`（`pnpm`）。
7. **`PageRequest` 是 `record`**：`OrderListQuery` 改为 `public sealed record OrderListQuery : PageRequest`，保留业务字段作为 init 属性。注意 `PageRequest` 构造函数为 `protected`（`private set`），子类 record 用 `: base(page, pageSize)` 调用。
8. **`OrderListResult` 删除**：所有引用改为 `PageResult<OrderSummaryDto>`（共享类型）。`OrderSummaryDto` 保留（从 `OrderListResult.cs` 移到独立文件或保留原文件仅删 `OrderListResult` 类）。
9. **EF Core 迁移**：BE-3 新增 `ExportTasks` 表，需创建迁移 `dotnet ef migrations add AddExportTasks`。
10. **ClosedXML 依赖**：`Leno.SellerShop.Infrastructure.csproj` 新增 `<PackageReference Include="ClosedXML" Version="0.102.1" />`。
11. **`IFileStorageService` "export" 类别**：`LocalFileStorageService.AllowedCategories` 集合新增 `"export"`。
12. **`ExportBackgroundService` 轮询模式**：非消息队列，`BackgroundService` 每 5 秒轮询 `Status=Processing` 的任务，每次处理 1 个。用 `IServiceScopeFactory` 创建 scope 解析依赖（`IExportTaskRepository`/ACL/`IFileStorageService` 都是 Scoped）。

---

## File Structure

### 新建文件

| 文件 | 职责 |
|------|------|
| `src/Services/Order/Leno.Order.Application/Queries/OrderListQuery.cs` | 改为继承 PageRequest（覆盖原文件） |
| `src/Services/Order/Leno.Order.Application/Queries/OrderSummaryDto.cs` | 从 OrderListResult.cs 拆出 OrderSummaryDto（OrderListResult 删除） |
| `src/Contracts/Leno.SharedContracts/Grpc/Protos/product_internal.proto` | 新增 GetLowStockByShop rpc |
| `src/Contracts/Leno.SharedContracts/Grpc/Protos/order_internal.proto` | 新增 GetSalesSummary/GetOrderDetailForExport rpc |
| `src/Services/Product/Leno.Product.Application/LowStockSkuDto.cs` | 低库存 DTO |
| `src/Services/Product/Leno.Product.Application/IProductInternalQueryService.cs` | 新增 GetLowStockByShopAsync |
| `src/Services/Product/Leno.Product.Application/Services/ProductInternalQueryService.cs` | 实现低库存查询 |
| `src/Services/Product/Leno.Product.Api/GrpcServices/ProductGrpcService.cs` | 实现 GetLowStockByShop |
| `src/Services/SellerShop/Leno.SellerShop.Application/Services/IProductAntiCorruptionService.cs` | 新增 GetLowStockSkusAsync |
| `src/Services/SellerShop/Leno.SellerShop.Application/LowStockItemDto.cs` | 卖家域低库存 DTO |
| `src/Services/SellerShop/Leno.SellerShop.Application/ISellerDashboardAppService.cs` | 新增 GetLowStockAlertAsync |
| `src/Services/SellerShop/Leno.SellerShop.Application/Services/SellerDashboardAppService.cs` | 实现 |
| `src/Services/SellerShop/Leno.SellerShop.Infrastructure/Services/Grpc/GrpcProductAntiCorruptionClient.cs` | 实现低库存 gRPC 调用 |
| `src/Services/SellerShop/Leno.SellerShop.Api/Controllers/SellerDashboardController.cs` | 新增 low-stock 端点 |
| `src/Services/SellerShop/Leno.SellerShop.Domain/Aggregates/ExportTask.cs` | 导出任务聚合根 |
| `src/Services/SellerShop/Leno.SellerShop.Domain/Repositories/IExportTaskRepository.cs` | 仓储接口 |
| `src/Services/SellerShop/Leno.SellerShop.Application/IExportAppService.cs` | 导出应用服务接口 |
| `src/Services/SellerShop/Leno.SellerShop.Application/Services/ExportAppService.cs` | 实现 |
| `src/Services/SellerShop/Leno.SellerShop.Application/Dtos/ExportDtos.cs` | 导出 DTO |
| `src/Services/SellerShop/Leno.SellerShop.Application/Services/IOrderAntiCorruptionService.cs` | 新增导出数据查询 |
| `src/Services/SellerShop/Leno.SellerShop.Infrastructure/Repositories/ExportTaskRepository.cs` | EF Core 仓储 |
| `src/Services/SellerShop/Leno.SellerShop.Infrastructure/Export/ExportBackgroundService.cs` | 后台作业 |
| `src/Services/SellerShop/Leno.SellerShop.Infrastructure/Export/ExportFileGenerator.cs` | Excel/CSV 生成 |
| `src/Services/SellerShop/Leno.SellerShop.Api/Controllers/ExportController.cs` | 导出控制器 |
| `web/seller/src/modules/08-account/types/notification.dto.ts` | 通知 DTO |
| `web/seller/src/modules/08-account/api/notification.api.ts` | 通知 API 客户端 |
| `web/seller/src/modules/08-account/api/notification.api.spec.ts` | 通知 API 测试 |
| `web/seller/src/shared/http/mock/handlers/notification.ts` | 通知 mock handler |

### 修改文件

| 文件 | 改动 |
|------|------|
| `src/Services/Order/Leno.Order.Application/Queries/OrderListResult.cs` | 删除 OrderListResult 类，保留 OrderSummaryDto（或拆文件） |
| `src/Services/Order/Leno.Order.Application/Queries/OrderListQueryHandler.cs` | 返回类型改 PageResult<OrderSummaryDto> |
| `src/Services/Order/Leno.Order.Application/Queries/IOrderReadModelAccessor.cs` | ListAsync 返回 PageResult |
| `src/Services/Order/Leno.Order.Infrastructure/ReadModels/OrderReadModelAccessor.cs` | ListAsync 返回 PageResult，from 计算用 PageRequest.Skip |
| `src/Services/Order/Leno.Order.Api/Controllers/OrdersController.cs` | 3 处 page 默认值 0→1，PageIndex→Page |
| `src/Services/Order/Leno.Order.Application.Tests/Queries/OrderListQueryHandlerTests.cs` | PageIndex→Page, TotalCount→Total |
| `src/Services/Order/Leno.Order.Api.Tests/SellerOrdersApiTests.cs` | 同上 |
| `src/Services/Order/Leno.Order.Api.Tests/OrderApiTests.cs` | 同上 |
| `src/Services/SellerShop/Leno.SellerShop.Infrastructure/SellerShopDbContext.cs` | 新增 DbSet<ExportTask> |
| `src/Services/SellerShop/Leno.SellerShop.Infrastructure/Dependencies/ServiceCollectionExtensions.cs` | 注册 ExportTaskRepository/ExportAppService/ExportBackgroundService |
| `src/Services/SellerShop/Leno.SellerShop.Infrastructure/Services/Grpc/GrpcOrderAntiCorruptionClient.cs` | 实现导出数据查询 |
| `src/BuildingBlocks/Leno.Infrastructure/Storage/LocalFileStorageService.cs` | AllowedCategories 新增 "export" |
| `src/Services/SellerShop/Leno.SellerShop.Infrastructure/Leno.SellerShop.Infrastructure.csproj` | 新增 ClosedXML 包引用 |
| `web/seller/src/modules/05-order-fulfillment/api/order.api.ts` | page 默认 0→1，移除 BE-1 注释 |
| `web/seller/src/modules/05-order-fulfillment/types/order.dto.ts` | 移除 BE-1 注释 |
| `web/seller/src/modules/05-order-fulfillment/api/order.api.spec.ts` | page=0→1 |
| `web/seller/src/modules/05-order-fulfillment/views/PendingShipment.vue` | 移除 BE-1 适配代码 |
| `web/seller/src/modules/05-order-fulfillment/views/OrderList.vue` | 移除 BE-1 适配代码 |
| `web/seller/src/modules/02-dashboard/api/dashboard.api.ts` | 新增 getLowStock |
| `web/seller/src/modules/02-dashboard/views/LowStockAlert.vue` | 移除 mock，接入 API |
| `web/seller/src/modules/09-export/views/SalesExport.vue` | 移除 BE-3 提示，激活下载逻辑 |
| `web/seller/src/modules/09-export/api/export.api.ts` | 移除 BE-3 注释 |
| `web/seller/src/shared/http/mock/handlers/export.ts` | 移除 501 桩，真实 mock |
| `web/seller/src/shared/http/mock/data/types.ts` | MockSeed 新增 notifications |
| `web/seller/src/shared/http/mock/data/seed.ts` | 追加 notifications 种子 |
| `web/seller/src/shared/http/mock/index.ts` | 注册 notification handler |
| `web/seller/src/modules/08-account/views/Notifications.vue` | 接入真实 API |

---

## Task 1: BE-4 通知前端接入 — DTO 与 API 客户端

**Files:**
- Create: `web/seller/src/modules/08-account/types/notification.dto.ts`
- Create: `web/seller/src/modules/08-account/api/notification.api.ts`
- Create: `web/seller/src/modules/08-account/api/notification.api.spec.ts`

- [ ] **Step 1: 创建 notification.dto.ts**

创建 `web/seller/src/modules/08-account/types/notification.dto.ts`：

```typescript
/**
 * 08-account 通知 DTO
 *
 * 与后端 Notification BC 对接（BE-4 后端已就绪）：
 * - GET  /api/notifications            通知列表（isRead/page/pageSize）
 * - GET  /api/notifications/unread-count 未读计数
 * - POST /api/notifications/read        批量标记已读（recordIds）
 * - POST /api/notifications/read-all    全部标记已读
 */

/** 通知渠道 */
export type NotificationChannel = 'InApp' | 'Email' | 'Sms'

/** 通知状态 */
export type NotificationStatus = 'Pending' | 'Sent' | 'Failed' | 'DeadLetter'

/** 通知记录 DTO */
export interface NotificationRecordDto {
  recordId: string
  userId: string
  templateCode: string
  channel: NotificationChannel
  title: string
  content: string
  status: NotificationStatus
  isRead: boolean
  sentAt?: string
  createdAt: string
}

/** 通知列表结果（后端 NotificationListResultDto，比 PageResult 多 unreadCount） */
export interface NotificationListResultDto {
  items: NotificationRecordDto[]
  total: number
  unreadCount: number
  page: number
  pageSize: number
}

/** 批量标记已读请求 */
export interface MarkAsReadDto {
  recordIds: string[]
}

/** 列表查询参数 */
export interface NotificationListParams {
  isRead?: boolean
  page?: number
  pageSize?: number
}
```

- [ ] **Step 2: 先写 notification.api.spec.ts 失败测试**

创建 `web/seller/src/modules/08-account/api/notification.api.spec.ts`：

```typescript
/* eslint-disable @typescript-eslint/no-explicit-any */
import { describe, expect, it, vi, beforeEach } from 'vitest'
import { notificationApi } from './notification.api'
import { http } from '@/shared/http'

vi.mock('@/shared/http', () => ({
  http: { get: vi.fn(), post: vi.fn() },
  withIdempotency: vi.fn(() => ({ headers: { 'Idempotency-Key': 'mock-key' } })),
}))

describe('notificationApi', () => {
  beforeEach(() => vi.clearAllMocks())

  it('list 调用 GET /notifications 并透传参数', async () => {
    vi.mocked(http.get).mockResolvedValue({
      items: [],
      total: 0,
      unreadCount: 0,
      page: 1,
      pageSize: 20,
    } as any)
    await notificationApi.list({ isRead: false, page: 1, pageSize: 20 })
    expect(http.get).toHaveBeenCalledWith('/notifications', {
      params: { isRead: false, page: 1, pageSize: 20 },
    })
  })

  it('list 默认 page=1 pageSize=20', async () => {
    vi.mocked(http.get).mockResolvedValue({
      items: [],
      total: 0,
      unreadCount: 0,
      page: 1,
      pageSize: 20,
    } as any)
    await notificationApi.list({})
    expect(http.get).toHaveBeenCalledWith('/notifications', {
      params: expect.objectContaining({ page: 1, pageSize: 20 }),
    })
  })

  it('getUnreadCount 调用 GET /notifications/unread-count', async () => {
    vi.mocked(http.get).mockResolvedValue(5 as any)
    const count = await notificationApi.getUnreadCount()
    expect(http.get).toHaveBeenCalledWith('/notifications/unread-count')
    expect(count).toBe(5)
  })

  it('markAsRead 调用 POST /notifications/read 带 recordIds', async () => {
    vi.mocked(http.post).mockResolvedValue(undefined as any)
    await notificationApi.markAsRead(['r1', 'r2'])
    expect(http.post).toHaveBeenCalledWith('/notifications/read', { recordIds: ['r1', 'r2'] })
  })

  it('markAllAsRead 调用 POST /notifications/read-all', async () => {
    vi.mocked(http.post).mockResolvedValue(undefined as any)
    await notificationApi.markAllAsRead()
    expect(http.post).toHaveBeenCalledWith('/notifications/read-all')
  })
})
```

- [ ] **Step 3: 运行测试确认失败**

Run (cwd: `web/seller`): `pnpm test -- src/modules/08-account/api/notification.api.spec.ts`
Expected: FAIL（`Cannot find module './notification.api'`）

- [ ] **Step 4: 创建 notification.api.ts**

创建 `web/seller/src/modules/08-account/api/notification.api.ts`：

```typescript
import { http } from '@/shared/http'
import type {
  NotificationListParams,
  NotificationListResultDto,
} from '../types/notification.dto'

/**
 * 通知 API 客户端
 *
 * 与后端 Notification BC 对接（BE-4 已就绪）。响应拦截器已解包 ApiResponse.data。
 * - GET  /notifications            列表
 * - GET  /notifications/unread-count 未读计数
 * - POST /notifications/read        批量标记已读
 * - POST /notifications/read-all    全部标记已读
 */
export const notificationApi = {
  /** 查询通知列表（isRead 可空表示全部） */
  list(params: NotificationListParams): Promise<NotificationListResultDto> {
    const { isRead, page = 1, pageSize = 20 } = params
    return http
      .get<NotificationListResultDto>('/notifications', {
        params: { isRead, page, pageSize },
      })
      .then((r) => r.data)
  },

  /** 获取未读计数 */
  getUnreadCount(): Promise<number> {
    return http.get<number>('/notifications/unread-count').then((r) => r.data)
  },

  /** 批量标记已读 */
  markAsRead(recordIds: string[]): Promise<void> {
    return http
      .post<void>('/notifications/read', { recordIds })
      .then((r) => r.data)
  },

  /** 全部标记已读 */
  markAllAsRead(): Promise<void> {
    return http.post<void>('/notifications/read-all').then((r) => r.data)
  },
}
```

- [ ] **Step 5: 运行测试确认通过**

Run (cwd: `web/seller`): `pnpm test -- src/modules/08-account/api/notification.api.spec.ts`
Expected: PASS（5 tests passed）

- [ ] **Step 6: 提交**

```bash
git add web/seller/src/modules/08-account/types/notification.dto.ts web/seller/src/modules/08-account/api/notification.api.ts web/seller/src/modules/08-account/api/notification.api.spec.ts
git commit -m "feat(account): add notification API client + DTO (BE-4)"
```

---

## Task 2: BE-4 通知 mock handler + 种子数据

**Files:**
- Create: `web/seller/src/shared/http/mock/handlers/notification.ts`
- Modify: `web/seller/src/shared/http/mock/data/types.ts`
- Modify: `web/seller/src/shared/http/mock/data/seed.ts`
- Modify: `web/seller/src/shared/http/mock/index.ts`

- [ ] **Step 1: 扩展 MockSeed 类型**

修改 `web/seller/src/shared/http/mock/data/types.ts`，在 `exportTasks` 后追加 `notifications` 字段：

```typescript
export interface MockSeed {
  menus: unknown[]
  onlineUsers: unknown[]
  loginLogs: unknown[]
  redisKeys: unknown[]
  redisInfo: unknown
  keyspaces: unknown[]
  serverSnapshot: unknown
  serverHistory: { cpu: unknown[]; memory: unknown[]; diskIo: unknown[] }
  shop: unknown
  qualifications: unknown[]
  freightTemplates: unknown[]
  logisticsCompanies: unknown[]
  reviews: unknown[]
  exportTasks: unknown[]
  notifications: unknown[]
  nextId: number
}
```

- [ ] **Step 2: 追加 notifications 种子 builder**

修改 `web/seller/src/shared/http/mock/data/seed.ts`，在 `buildExportTaskSeed` 函数后（或文件末尾 builder 函数区）追加：

```typescript
function buildNotificationSeed(): unknown[] {
  const now = new Date()
  const iso = (offsetMs: number) => new Date(now.getTime() - offsetMs).toISOString()
  return [
    {
      recordId: 'n-001',
      userId: 'u-seller-001',
      templateCode: 'ORDER_PAID',
      channel: 'InApp',
      title: '新订单已支付',
      content: '订单 NO20260731001 已支付，请尽快发货。',
      status: 'Sent',
      isRead: false,
      sentAt: iso(3600_000),
      createdAt: iso(3600_000),
    },
    {
      recordId: 'n-002',
      userId: 'u-seller-001',
      templateCode: 'LOW_STOCK',
      channel: 'InApp',
      title: '库存预警',
      content: 'SKU 编码 SKU-001 库存低于阈值，请及时补货。',
      status: 'Sent',
      isRead: false,
      sentAt: iso(7200_000),
      createdAt: iso(7200_000),
    },
    {
      recordId: 'n-003',
      userId: 'u-seller-001',
      templateCode: 'REVIEW_SUBMITTED',
      channel: 'InApp',
      title: '收到新评价',
      content: '买家对订单 NO20260730005 提交了评价，请及时回复。',
      status: 'Sent',
      isRead: true,
      sentAt: iso(86400_000),
      createdAt: iso(86400_000),
    },
    {
      recordId: 'n-004',
      userId: 'u-seller-001',
      templateCode: 'QUALIFICATION_EXPIRE',
      channel: 'InApp',
      title: '资质即将到期',
      content: '营业执照将于 30 天后到期，请及时更新。',
      status: 'Sent',
      isRead: true,
      sentAt: iso(172800_000),
      createdAt: iso(172800_000),
    },
    {
      recordId: 'n-005',
      userId: 'u-seller-001',
      templateCode: 'SETTLEMENT',
      channel: 'InApp',
      title: '结算已完成',
      content: '2026-07 月度结算已完成，金额 ¥12,800.00。',
      status: 'Sent',
      isRead: true,
      sentAt: iso(259200_000),
      createdAt: iso(259200_000),
    },
  ]
}
```

然后在 `ensureSeedData` 的 seed 对象中，`exportTasks: []` 后追加 `notifications: buildNotificationSeed(),`。

- [ ] **Step 3: 创建 notification mock handler**

创建 `web/seller/src/shared/http/mock/handlers/notification.ts`：

```typescript
/* eslint-disable @typescript-eslint/no-explicit-any */
import type MockAdapter from 'axios-mock-adapter'
import { loadSeedData, saveSeedData } from '../data/seed'

/**
 * 通知 handler 注册（BE-4 后端已就绪，mock 用于前端开发联调）
 *
 * 端点（baseURL=/api，故拦截 /notifications/...）：
 * - GET  /notifications            列表（isRead/page/pageSize）
 * - GET  /notifications/unread-count 未读计数
 * - POST /notifications/read        批量标记已读
 * - POST /notifications/read-all    全部标记已读
 */
export function registerNotificationHandlers(mock: MockAdapter): void {
  // 通知列表
  mock.onGet('/notifications').reply((config) => {
    const seed = loadSeedData()
    const items = (seed.notifications as any[]) ?? []
    const isRead = config.params?.isRead
    const page = Number(config.params?.page ?? 1)
    const pageSize = Number(config.params?.pageSize ?? 20)

    let filtered = items
    if (isRead === true) filtered = items.filter((n) => n.isRead)
    if (isRead === false) filtered = items.filter((n) => !n.isRead)

    const unreadCount = items.filter((n) => !n.isRead).length
    const start = (page - 1) * pageSize
    const paged = filtered.slice(start, start + pageSize)

    return [
      200,
      {
        code: 200,
        message: 'OK',
        data: {
          items: paged,
          total: filtered.length,
          unreadCount,
          page,
          pageSize,
        },
      },
    ]
  })

  // 未读计数
  mock.onGet('/notifications/unread-count').reply(() => {
    const seed = loadSeedData()
    const items = (seed.notifications as any[]) ?? []
    const unreadCount = items.filter((n) => !n.isRead).length
    return [200, { code: 200, message: 'OK', data: unreadCount }]
  })

  // 批量标记已读
  mock.onPost('/notifications/read').reply((config) => {
    const seed = loadSeedData()
    const items = (seed.notifications as any[]) ?? []
    const body = JSON.parse(config.data || '{}')
    const ids: string[] = body.recordIds ?? []
    const idSet = new Set(ids)
    for (const n of items) {
      if (idSet.has(n.recordId)) n.isRead = true
    }
    seed.notifications = items
    saveSeedData(seed)
    return [200, { code: 200, message: 'OK', data: null }]
  })

  // 全部标记已读
  mock.onPost('/notifications/read-all').reply(() => {
    const seed = loadSeedData()
    const items = (seed.notifications as any[]) ?? []
    for (const n of items) n.isRead = true
    seed.notifications = items
    saveSeedData(seed)
    return [200, { code: 200, message: 'OK', data: null }]
  })
}
```

- [ ] **Step 4: 注册 handler**

修改 `web/seller/src/shared/http/mock/index.ts`：

在 import 区追加：
```typescript
import { registerNotificationHandlers } from './handlers/notification'
```

在 `registerExportHandlers(mock)` 后追加：
```typescript
registerNotificationHandlers(mock)
```

将启动日志改为 `11 个 handler，共 40 个 endpoint`。

- [ ] **Step 5: 提交**

```bash
git add web/seller/src/shared/http/mock/
git commit -m "feat(mock): add notification handlers + seed (BE-4)"
```

---

## Task 3: BE-4 改造 Notifications.vue 接入真实 API

**Files:**
- Modify: `web/seller/src/modules/08-account/views/Notifications.vue`

- [ ] **Step 1: 改造 Notifications.vue**

将 `web/seller/src/modules/08-account/views/Notifications.vue` 完整替换为：

```vue
<script setup lang="ts">
import { ref, computed, onMounted } from 'vue'
import {
  Breadcrumb,
  BreadcrumbItem,
  Card,
  Tabs,
  TabPane,
  List,
  ListItem,
  ListItemMeta,
  Button,
  Tag,
  Space,
  Skeleton,
  message,
} from 'ant-design-vue'
import { BellOutlined, CheckOutlined } from '@ant-design/icons-vue'
import { h } from 'vue'
import { EmptyState } from '@/shared/components'
import { notificationApi } from '../api/notification.api'
import type { NotificationRecordDto } from '../types/notification.dto'
import { logger } from '@/shared/utils/logger'
import { formatDateTime } from '@/shared/utils/format'

/**
 * 消息通知页
 *
 * 路由 /account/notifications，权限 notification:list
 * 后端 4 端点已就绪（BE-4 清理），接入真实 API：
 * - 列表 GET /notifications?isRead=&page=&pageSize=
 * - 未读计数 GET /notifications/unread-count
 * - 批量标记已读 POST /notifications/read
 * - 全部标记已读 POST /notifications/read-all
 */

type TabKey = 'all' | 'unread' | 'read'

const activeTab = ref<TabKey>('all')
const loading = ref(true)
const submitting = ref(false)
const notifications = ref<NotificationRecordDto[]>([])
const unreadCount = ref(0)

const filtered = computed<NotificationRecordDto[]>(() => {
  if (activeTab.value === 'unread') return notifications.value.filter((n) => !n.isRead)
  if (activeTab.value === 'read') return notifications.value.filter((n) => n.isRead)
  return notifications.value
})

function isReadParam(tab: TabKey): boolean | undefined {
  if (tab === 'unread') return false
  if (tab === 'read') return true
  return undefined
}

async function loadList(): Promise<void> {
  loading.value = true
  try {
    const res = await notificationApi.list({
      isRead: isReadParam(activeTab.value),
      page: 1,
      pageSize: 50,
    })
    notifications.value = res.items
    unreadCount.value = res.unreadCount
  } catch (e) {
    logger.error('加载通知列表失败', e)
    message.error('加载通知列表失败')
  } finally {
    loading.value = false
  }
}

async function loadUnreadCount(): Promise<void> {
  try {
    unreadCount.value = await notificationApi.getUnreadCount()
  } catch (e) {
    logger.warn('获取未读计数失败', e)
  }
}

async function onMarkAllRead(): Promise<void> {
  submitting.value = true
  try {
    await notificationApi.markAllAsRead()
    message.success('已全部标记为已读')
    await loadList()
    await loadUnreadCount()
  } catch (e) {
    logger.error('标记全部已读失败', e)
    message.error('标记全部已读失败')
  } finally {
    submitting.value = false
  }
}

async function onMarkOneRead(item: NotificationRecordDto): Promise<void> {
  if (item.isRead) return
  try {
    await notificationApi.markAsRead([item.recordId])
    item.isRead = true
    unreadCount.value = Math.max(0, unreadCount.value - 1)
  } catch (e) {
    logger.error('标记已读失败', e)
    message.error('标记已读失败')
  }
}

onMounted(() => {
  void loadList()
  void loadUnreadCount()
})
</script>

<template>
  <div class="account-notifications-page">
    <Breadcrumb class="account-notifications-breadcrumb">
      <BreadcrumbItem>个人账号</BreadcrumbItem>
      <BreadcrumbItem>消息通知</BreadcrumbItem>
    </Breadcrumb>

    <Card class="account-notifications-card" :bordered="true">
      <template #title>
        <Space>
          <BellOutlined />
          <span class="account-notifications-title">消息通知</span>
          <Tag v-if="unreadCount > 0" color="red">{{ unreadCount }} 未读</Tag>
        </Space>
      </template>
      <template #extra>
        <Button
          :icon="h(CheckOutlined)"
          size="small"
          :loading="submitting"
          :disabled="unreadCount === 0"
          @click="onMarkAllRead"
        >
          全部标记已读
        </Button>
      </template>

      <Tabs v-model:active-key="activeTab" @change="loadList">
        <TabPane key="all" tab="全部" />
        <TabPane key="unread" tab="未读" />
        <TabPane key="read" tab="已读" />
      </Tabs>

      <Skeleton v-if="loading" active :paragraph="{ rows: 4 }" />
      <EmptyState v-else-if="filtered.length === 0" description="暂无通知" />
      <List v-else :data-source="filtered" item-layout="horizontal">
        <template #renderItem="{ item }">
          <ListItem>
            <ListItemMeta>
              <template #title>
                <Space>
                  <span>{{ item.title }}</span>
                  <Tag v-if="!item.isRead" color="red">未读</Tag>
                </Space>
              </template>
              <template #description>
                <div class="account-notifications-desc">
                  <span>{{ item.content }}</span>
                  <span class="account-notifications-time">{{ formatDateTime(item.createdAt) }}</span>
                </div>
              </template>
            </ListItemMeta>
            <template #actions>
              <Button
                v-if="!item.isRead"
                type="link"
                size="small"
                @click="onMarkOneRead(item as NotificationRecordDto)"
              >
                标记已读
              </Button>
            </template>
          </ListItem>
        </template>
      </List>
    </Card>
  </div>
</template>

<style scoped>
.account-notifications-page {
  display: flex;
  flex-direction: column;
  gap: 16px;
}
.account-notifications-breadcrumb {
  font-size: 14px;
}
.account-notifications-card {
  border-radius: 8px;
}
.account-notifications-title {
  font-size: 15px;
  font-weight: 500;
}
.account-notifications-desc {
  display: flex;
  flex-direction: column;
  gap: 4px;
}
.account-notifications-time {
  font-size: 12px;
  color: #8c8c8c;
}
</style>
```

- [ ] **Step 2: 运行前端验证**

Run (cwd: `web/seller`): `pnpm typecheck && pnpm lint -- src/modules/08-account/views/Notifications.vue`
Expected: 0 errors

- [ ] **Step 3: 提交**

```bash
git add web/seller/src/modules/08-account/views/Notifications.vue
git commit -m "feat(account): integrate notification API into Notifications.vue (BE-4)"
```

---

## Task 4: BE-1 Order 分页契约统一 — 后端 Query/Result 重构

**Files:**
- Modify: `src/Services/Order/Leno.Order.Application/Queries/OrderListQuery.cs`
- Modify: `src/Services/Order/Leno.Order.Application/Queries/OrderListResult.cs`
- Modify: `src/Services/Order/Leno.Order.Application/Queries/OrderListQueryHandler.cs`
- Modify: `src/Services/Order/Leno.Order.Application/Queries/IOrderReadModelAccessor.cs`

- [ ] **Step 1: 重构 OrderListQuery 继承 PageRequest**

将 `src/Services/Order/Leno.Order.Application/Queries/OrderListQuery.cs` 完整替换为：

```csharp
using Leno.SharedKernel.ValueObjects;

namespace Leno.Order.Application.Queries;

/// <summary>
/// 订单列表分页查询参数（CQRS 读侧 Query）。
/// 由 <see cref="OrderListQueryHandler"/> 处理，经 <c>IOrderReadModelAccessor</c> 走 ES 读模型。
/// 继承 <see cref="PageRequest"/> 统一分页契约（Page 从 1 起，PageSize 默认 20、最大 100）。
/// </summary>
public sealed record OrderListQuery : PageRequest
{
    /// <summary>买家标识过滤，可空表示不限。</summary>
    public Guid? UserId { get; init; }

    /// <summary>卖家（店铺）标识过滤，可空表示不限。</summary>
    public Guid? SellerId { get; init; }

    /// <summary>订单状态名称过滤（如 "Paid"、"Shipped"），可空表示不限。与 <c>OrderReadModel.Status</c> 字符串匹配。</summary>
    public string? Status { get; init; }

    /// <summary>订单号模糊搜索过滤，可空表示不限。非空时对 <c>OrderReadModel.OrderNo</c> 做 MatchQuery 模糊匹配。</summary>
    public string? OrderNo { get; init; }

    /// <summary>创建起始时间（UTC）过滤，可空表示不限。</summary>
    public DateTime? StartDate { get; init; }

    /// <summary>创建结束时间（UTC）过滤，可空表示不限。</summary>
    public DateTime? EndDate { get; init; }

    /// <summary>构造订单列表查询，分页参数走 PageRequest 基类（Page 从 1 起）。</summary>
    public OrderListQuery(int page = 1, int pageSize = PageRequest.DefaultPageSize) : base(page, pageSize) { }
}
```

- [ ] **Step 2: 删除 OrderListResult，保留 OrderSummaryDto**

将 `src/Services/Order/Leno.Order.Application/Queries/OrderListResult.cs` 完整替换为（删除 `OrderListResult` 类，仅保留 `OrderSummaryDto`）：

```csharp
namespace Leno.Order.Application.Queries;

/// <summary>
/// 订单摘要 DTO（基于 ES 读模型字段），用于列表查询场景。
/// 原 <c>OrderListResult</c> 已删除，统一使用共享 <c>PageResult&lt;OrderSummaryDto&gt;</c>。
/// </summary>
public sealed class OrderSummaryDto
{
    public Guid OrderId { get; init; }

    public string OrderNo { get; init; } = string.Empty;

    public Guid UserId { get; init; }

    /// <summary>卖家（店铺）标识，会员订阅订单可为空。</summary>
    public Guid? SellerId { get; init; }

    public decimal TotalAmount { get; init; }

    public string Currency { get; init; } = "CNY";

    /// <summary>订单状态名称（如 "PendingPayment"、"Paid"、"Shipped"）。</summary>
    public string Status { get; init; } = string.Empty;

    public DateTime CreatedAt { get; init; }

    public DateTime? PaidAt { get; init; }

    public DateTime? ShippedAt { get; init; }
}
```

- [ ] **Step 3: 重构 OrderListQueryHandler 返回 PageResult**

将 `src/Services/Order/Leno.Order.Application/Queries/OrderListQueryHandler.cs` 完整替换为：

```csharp
using Leno.Infrastructure.Abstractions.Cqrs;
using Leno.SharedContracts.Responses;

namespace Leno.Order.Application.Queries;

/// <summary>
/// 订单列表查询处理器。
/// 经 <see cref="IOrderReadModelAccessor"/>（端口由 Infrastructure 层 <c>OrderReadModelAccessor</c> 实现）
/// 查询 ES 读模型并返回 <see cref="PageResult{T}"/>（统一分页契约）。
/// </summary>
public sealed class OrderListQueryHandler : IQueryHandler<OrderListQuery, PageResult<OrderSummaryDto>>
{
    private readonly IOrderReadModelAccessor _readModelAccessor;

    public OrderListQueryHandler(IOrderReadModelAccessor readModelAccessor)
    {
        ArgumentNullException.ThrowIfNull(readModelAccessor);
        _readModelAccessor = readModelAccessor;
    }

    /// <inheritdoc />
    public Task<PageResult<OrderSummaryDto>> HandleAsync(OrderListQuery query, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(query);

        return _readModelAccessor.ListAsync(query, ct);
    }
}
```

- [ ] **Step 4: 重构 IOrderReadModelAccessor 签名**

将 `src/Services/Order/Leno.Order.Application/Queries/IOrderReadModelAccessor.cs` 的 `ListAsync` 签名改为返回 `PageResult<OrderSummaryDto>`（保留 `GetDetailAsync` 不变）：

```csharp
using Leno.SharedContracts.Responses;

namespace Leno.Order.Application.Queries;

/// <summary>
/// 订单读模型访问器抽象（CQRS 读侧端口）。
/// 定义在 Application 层以保持分层洁癖：Application 不直接引用 Infrastructure 层的
/// <c>IEsReadModelRepository&lt;OrderReadModel&gt;</c>，由 Infrastructure 层实现。
/// </summary>
public interface IOrderReadModelAccessor
{
    /// <summary>
    /// 按订单标识查询 ES 读模型并映射为 <see cref="OrderDetailResult"/>。
    /// </summary>
    Task<OrderDetailResult?> GetDetailAsync(Guid orderId, CancellationToken ct = default);

    /// <summary>
    /// 分页条件查询订单 ES 读模型并映射为 <see cref="PageResult{T}"/>。
    /// </summary>
    /// <param name="query">列表查询参数（继承 PageRequest，Page 从 1 起）。</param>
    /// <param name="ct">取消令牌。</param>
    /// <returns>分页结果，无命中时返回空列表与 0 总数。</returns>
    Task<PageResult<OrderSummaryDto>> ListAsync(OrderListQuery query, CancellationToken ct = default);
}
```

- [ ] **Step 5: 提交（编译会失败，下一步修复 Infrastructure）**

```bash
git add src/Services/Order/Leno.Order.Application/Queries/
git commit -m "refactor(order): migrate OrderListQuery to PageRequest, delete OrderListResult (BE-1)"
```

---

## Task 5: BE-1 Order ReadModelAccessor + Controller + 测试适配

**Files:**
- Modify: `src/Services/Order/Leno.Order.Infrastructure/ReadModels/OrderReadModelAccessor.cs`
- Modify: `src/Services/Order/Leno.Order.Api/Controllers/OrdersController.cs`
- Modify: `src/Services/Order/Leno.Order.Application.Tests/Queries/OrderListQueryHandlerTests.cs`
- Modify: `src/Services/Order/Leno.Order.Api.Tests/SellerOrdersApiTests.cs`
- Modify: `src/Services/Order/Leno.Order.Api.Tests/OrderApiTests.cs`

- [ ] **Step 1: 重构 OrderReadModelAccessor.ListAsync**

将 `src/Services/Order/Leno.Order.Infrastructure/ReadModels/OrderReadModelAccessor.cs` 的 `ListAsync` 方法（第 39-63 行）替换为：

```csharp
    /// <inheritdoc />
    public async Task<PageResult<OrderSummaryDto>> ListAsync(OrderListQuery query, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(query);

        // PageRequest 基类已归一化 Page/PageSize，直接用 Skip
        var from = query.Skip;
        var safePageSize = query.PageSize;

        var (items, total) = await _repository.SearchAsync(
            OrderIndexName,
            _ => BuildQuery(query),
            from,
            safePageSize,
            ct);

        var summaries = items.Select(ToSummaryDto).ToList();
        return new PageResult<OrderSummaryDto>(summaries, (int)total, query.Page, safePageSize);
    }
```

同时在文件顶部追加 `using Leno.SharedContracts.Responses;`。

- [ ] **Step 2: 重构 OrdersController 三处端点**

修改 `src/Services/Order/Leno.Order.Api/Controllers/OrdersController.cs`：

1. 字段 `_orderListQueryHandler` 类型与构造参数改为 `IQueryHandler<OrderListQuery, PageResult<OrderSummaryDto>>`
2. `ListMineAsync`（第 76-89 行）：`int page = 0` → `int page = 1`；`ProducesResponseType` 的 `OrderListResult` → `PageResult<OrderSummaryDto>`；构造 query 时 `PageIndex = page` → `Page = page, PageSize = pageSize`（用构造函数 `new OrderListQuery(page, pageSize) { ... }`）
3. `ListSellerOrdersAsync`（第 144-167 行）：同上
4. `ListAsync`（第 173-199 行）：同上

具体替换 `ListMineAsync` 为：

```csharp
    /// <summary>分页查询当前用户的订单（按状态可选过滤）。走 CQRS 读侧 ES 读模型。</summary>
    [Authorize(Roles = "Buyer")]
    [HttpGet("api/orders")]
    [ProducesResponseType(typeof(ApiResponse<PageResult<OrderSummaryDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> ListMineAsync([FromQuery] OrderStatus? status, [FromQuery] int page = 1, [FromQuery] int pageSize = 20, CancellationToken ct = default)
    {
        var query = new OrderListQuery(page, pageSize)
        {
            UserId = GetCurrentUserId(),
            Status = status?.ToString()
        };
        var result = await _orderListQueryHandler.HandleAsync(query, ct);
        return Ok(ApiResponse.Success(result));
    }
```

替换 `ListSellerOrdersAsync` 为：

```csharp
    /// <summary>
    /// 分页查询当前卖家的订单（按订单号、状态、下单时间范围可选过滤）。
    /// 走 CQRS 读侧 ES 读模型，SellerId 取自 JWT 强制过滤，不可查看他店订单。
    /// </summary>
    [Authorize(Roles = "Seller")]
    [HttpGet("api/seller/orders")]
    [ProducesResponseType(typeof(ApiResponse<PageResult<OrderSummaryDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> ListSellerOrdersAsync(
        [FromQuery] OrderStatus? status,
        [FromQuery] string? orderNo,
        [FromQuery] DateTime? startDate,
        [FromQuery] DateTime? endDate,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken ct = default)
    {
        var query = new OrderListQuery(page, pageSize)
        {
            SellerId = GetCurrentUserId(),
            Status = status?.ToString(),
            OrderNo = orderNo,
            StartDate = startDate,
            EndDate = endDate
        };
        var result = await _orderListQueryHandler.HandleAsync(query, ct);
        return Ok(ApiResponse.Success(result));
    }
```

替换 `ListAsync` 为：

```csharp
    /// <summary>分页查询全部订单（按用户、卖家、订单号、状态、下单时间范围可选过滤）。走 CQRS 读侧 ES 读模型。</summary>
    [Authorize(Roles = "Operator,Admin")]
    [HttpGet("api/admin/orders")]
    [ProducesResponseType(typeof(ApiResponse<PageResult<OrderSummaryDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> ListAsync(
        [FromQuery] Guid? userId,
        [FromQuery] Guid? sellerId,
        [FromQuery] OrderStatus? status,
        [FromQuery] string? orderNo,
        [FromQuery] DateTime? startDate,
        [FromQuery] DateTime? endDate,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken ct = default)
    {
        var query = new OrderListQuery(page, pageSize)
        {
            UserId = userId,
            SellerId = sellerId,
            Status = status?.ToString(),
            OrderNo = orderNo,
            StartDate = startDate,
            EndDate = endDate
        };
        var result = await _orderListQueryHandler.HandleAsync(query, ct);
        return Ok(ApiResponse.Success(result));
    }
```

并更新文件顶部 `using Leno.SharedContracts.Responses;`（已有则跳过）。

- [ ] **Step 3: 适配 OrderListQueryHandlerTests**

修改 `src/Services/Order/Leno.Order.Application.Tests/Queries/OrderListQueryHandlerTests.cs`：

1. 顶部追加 `using Leno.SharedContracts.Responses;`
2. 所有 `new OrderListQuery { ... PageIndex = X, PageSize = Y }` → `new OrderListQuery(page, pageSize) { ... }`（注意 Page 从 1 起，原 PageIndex=0 现在传 page=1）
3. 所有 `_accessorMock.Setup(a => a.ListAsync(...))` 返回的 `new OrderListResult { Items=..., TotalCount=..., PageIndex=..., PageSize=... }` → `new PageResult<OrderSummaryDto>(items, totalCount, page, pageSize)`
4. 断言 `result.TotalCount.Should().Be(X)` → `result.Total.Should().Be(X)`；`result.PageIndex.Should().Be(X)` → `result.Page.Should().Be(X)`

具体第 17-75 行 `HandleAsync_ShouldDelegateToReadModelAccessorAndReturnResult` 替换为：

```csharp
    [Fact]
    public async Task HandleAsync_ShouldDelegateToReadModelAccessorAndReturnResult()
    {
        // Arrange
        var query = new OrderListQuery(page: 1, pageSize: 20)
        {
            UserId = Guid.NewGuid(),
            SellerId = Guid.NewGuid(),
            Status = "Paid",
            StartDate = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc),
            EndDate = new DateTime(2026, 7, 19, 0, 0, 0, DateTimeKind.Utc)
        };

        var summary = new OrderSummaryDto
        {
            OrderId = Guid.NewGuid(),
            OrderNo = "ORD-001",
            UserId = query.UserId!.Value,
            SellerId = query.SellerId,
            TotalAmount = 199.00m,
            Currency = "CNY",
            Status = "Paid",
            CreatedAt = new DateTime(2026, 7, 1, 0, 0, 0, DateTimeKind.Utc),
            PaidAt = new DateTime(2026, 7, 1, 1, 0, 0, DateTimeKind.Utc),
            ShippedAt = null
        };

        var expectedResult = new PageResult<OrderSummaryDto>(
            new List<OrderSummaryDto> { summary }, 1, 1, 20);

        _accessorMock
            .Setup(a => a.ListAsync(query, It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedResult);

        // Act
        var result = await _sut.HandleAsync(query);

        // Assert
        result.Should().NotBeNull();
        result.Items.Should().HaveCount(1);
        result.Total.Should().Be(1);
        result.Page.Should().Be(1);
        result.PageSize.Should().Be(20);

        var first = result.Items[0];
        first.OrderId.Should().Be(summary.OrderId);
        first.OrderNo.Should().Be("ORD-001");
        first.Status.Should().Be("Paid");
        first.TotalAmount.Should().Be(199.00m);
        first.Currency.Should().Be("CNY");

        _accessorMock.Verify(a => a.ListAsync(query, It.IsAny<CancellationToken>()), Times.Once);
    }
```

第 77-103 行 `HandleAsync_EmptyResult_ShouldReturnEmptyItems` 替换为：

```csharp
    [Fact]
    public async Task HandleAsync_EmptyResult_ShouldReturnEmptyItems()
    {
        // Arrange
        var query = new OrderListQuery(page: 6, pageSize: 10);

        var expectedResult = new PageResult<OrderSummaryDto>(
            new List<OrderSummaryDto>(), 0, 6, 10);

        _accessorMock
            .Setup(a => a.ListAsync(query, It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedResult);

        // Act
        var result = await _sut.HandleAsync(query);

        // Assert
        result.Items.Should().BeEmpty();
        result.Total.Should().Be(0);
        result.Page.Should().Be(6);
        result.PageSize.Should().Be(10);
    }
```

第 113-147 行 `HandleAsync_WithOrderNo_ShouldPassOrderNoToReadModelAccessor` 替换 query 与 expectedResult：

```csharp
        var query = new OrderListQuery(page: 1, pageSize: 20)
        {
            OrderNo = "ORD-2026-001"
        };

        OrderListQuery? capturedQuery = null;
        var expectedResult = new PageResult<OrderSummaryDto>(
            new List<OrderSummaryDto>(), 0, 1, 20);
```

第 149-198 行 `HandleAsync_WithOrderNoAndOtherFilters_ShouldPreserveAllFields` 替换 query 与 expectedResult，并将末尾断言 `PageIndex` → `Page`：

```csharp
        var query = new OrderListQuery(page: 3, pageSize: 15)
        {
            UserId = userId,
            SellerId = sellerId,
            Status = "Paid",
            OrderNo = "ORD-2026-ABC",
            StartDate = startDate,
            EndDate = endDate
        };

        OrderListQuery? capturedQuery = null;
        var expectedResult = new PageResult<OrderSummaryDto>(
            new List<OrderSummaryDto>(), 0, 3, 15);
```

末尾断言：
```csharp
        capturedQuery.Page.Should().Be(3);
        capturedQuery.PageSize.Should().Be(15);
```

- [ ] **Step 4: 适配 SellerOrdersApiTests 与 OrderApiTests**

对 `src/Services/Order/Leno.Order.Api.Tests/SellerOrdersApiTests.cs` 与 `OrderApiTests.cs`：
- 搜索所有 `PageIndex` 引用改为 `Page`，值从 0 改为 1
- 搜索所有 `TotalCount` 引用改为 `Total`
- 搜索所有 `OrderListResult` 引用改为 `PageResult<OrderSummaryDto>`
- 顶部追加 `using Leno.SharedContracts.Responses;`（若用到 PageResult）

Run (cwd: `/workspace`): `dotnet build src/Services/Order/Leno.Order.Api/Leno.Order.Api.csproj`
Expected: BUILD SUCCEEDED

- [ ] **Step 5: 运行 Order 测试**

Run (cwd: `/workspace`): `dotnet test src/Services/Order/Leno.Order.Application.Tests/Leno.Order.Application.Tests.csproj`
Expected: ALL PASS

Run (cwd: `/workspace`): `dotnet test src/Services/Order/Leno.Order.Api.Tests/Leno.Order.Api.Tests.csproj`
Expected: ALL PASS

- [ ] **Step 6: 提交**

```bash
git add src/Services/Order/
git commit -m "refactor(order): adapt ReadModelAccessor + Controller + tests to PageResult (BE-1)"
```

---

## Task 6: BE-1 前端 Order API 与视图清理 BE-1 标记

**Files:**
- Modify: `web/seller/src/modules/05-order-fulfillment/api/order.api.ts`
- Modify: `web/seller/src/modules/05-order-fulfillment/types/order.dto.ts`
- Modify: `web/seller/src/modules/05-order-fulfillment/api/order.api.spec.ts`
- Modify: `web/seller/src/modules/05-order-fulfillment/views/PendingShipment.vue`
- Modify: `web/seller/src/modules/05-order-fulfillment/views/OrderList.vue`

- [ ] **Step 1: 修改 order.api.ts**

将 `web/seller/src/modules/05-order-fulfillment/api/order.api.ts` 第 11-19 行替换为：

```typescript
export const orderApi = {
  list: (params: ListOrdersParams) => {
    const { page = 1, pageSize = 20, ...rest } = params
    return client.get<PageResult<OrderListItemDto>>('/seller/orders', {
      params: { ...rest, page, pageSize },
    })
  },
```

删除文件顶部的 `// TODO(backend): BE-1 ...` 注释块（第 11-12 行）。

- [ ] **Step 2: 修改 order.dto.ts**

删除 `web/seller/src/modules/05-order-fulfillment/types/order.dto.ts` 中的两处 BE-1 注释（第 94-95 行、第 102 行注释），`page` 字段注释改为：

```typescript
  page?: number    // 后端从 1 起
```

- [ ] **Step 3: 修改 order.api.spec.ts**

将 `web/seller/src/modules/05-order-fulfillment/api/order.api.spec.ts` 中所有 `page: 0` 改为 `page: 1`，测试名 `list 默认 page=0（BE-1 待统一为 1）` 改为 `list 默认 page=1`。具体：

- 第 28 行 `page: 0,` → `page: 1,`
- 第 33 行 `page: 0,` → `page: 1,`
- 第 40 行 `page: 0,` → `page: 1,`
- 第 46-57 行测试名与断言改为 page=1
- 第 66 行 `page: 2,` 保持不变
- 第 73-83 行 `list 不传 page 时默认 0` → `list 不传 page 时默认 1`，断言 `page: 1`

- [ ] **Step 4: 修改 PendingShipment.vue**

修改 `web/seller/src/modules/05-order-fulfillment/views/PendingShipment.vue`：

1. 第 37-38 行：`// BE-1: 后端 Order 列表 page 从 0 起，首页传 0` 删除，`const page = ref(0)` → `const page = ref(1)`
2. 第 49-50 行：`// a-table 的 current 为 1 起，BE-1 后端 page 为 0 起，做 +1 适配展示` 删除，`current: page.value + 1,` → `current: page.value,`
3. 第 74-75 行：`// BE-1: 后端 Order 列表 page 从 0 起` 删除，`page: page.value,` 保持（现已是 1 起）
4. 第 99-100 行：`// a-table current 为 1 起，BE-1 后端 page 为 0 起，做 -1 适配` 删除，`page.value = (pag.current ?? 1) - 1` → `page.value = pag.current ?? 1`
5. 第 106 行：`// BE-1: 后端 Order 列表 page 从 0 起，搜索时回到首页 0` 删除，`page.value = 0` → `page.value = 1`
6. 第 115 行：`page.value = 0` → `page.value = 1`
7. 第 32 行文档注释 `分页：BE-1 待 Order BC 统一 page 从 1 起（当前从 0 起）。` 删除

- [ ] **Step 5: 修改 OrderList.vue**

对 `web/seller/src/modules/05-order-fulfillment/views/OrderList.vue` 做与 PendingShipment.vue 相同的修改（搜索所有 `BE-1`、`page.value + 1`、`page.value - 1`、`page.value = 0`、`(pag.current ?? 1) - 1` 并按上述规则替换）。

- [ ] **Step 6: 运行前端验证**

Run (cwd: `web/seller`): `pnpm test -- src/modules/05-order-fulfillment/`
Expected: ALL PASS

Run (cwd: `web/seller`): `pnpm typecheck && pnpm lint`
Expected: 0 errors

- [ ] **Step 7: 提交**

```bash
git add web/seller/src/modules/05-order-fulfillment/
git commit -m "refactor(order-fe): unify page to 1-based, remove BE-1 markers (BE-1)"
```

---

## Task 7: BE-2 Product 域低库存查询（DTO + 应用服务）

**Files:**
- Create: `src/Services/Product/Leno.Product.Application/LowStockSkuDto.cs`
- Modify: `src/Services/Product/Leno.Product.Application/IProductInternalQueryService.cs`

- [ ] **Step 1: 创建 LowStockSkuDto**

创建 `src/Services/Product/Leno.Product.Application/LowStockSkuDto.cs`：

```csharp
namespace Leno.Product.Application;

/// <summary>
/// 低库存 SKU 查询结果 DTO（商品域内部，供跨 BC ACL 调用）。
/// 数据来自 SPU 聚合内 SKU 实体的 StockQty 字段。
/// </summary>
public sealed class LowStockSkuDto
{
    public Guid SkuId { get; init; }
    public Guid ProductId { get; init; }
    public string ProductName { get; init; } = string.Empty;
    public string SkuName { get; init; } = string.Empty;
    public int Stock { get; init; }
    public int Threshold { get; init; }
    public Guid ShopId { get; init; }
}
```

- [ ] **Step 2: 扩展 IProductInternalQueryService**

修改 `src/Services/Product/Leno.Product.Application/IProductInternalQueryService.cs`，在末尾 `}` 前追加方法：

```csharp
    /// <summary>
    /// 按店铺标识查询低库存 SKU（StockQty &lt; threshold），返回按库存升序排列的列表。
    /// 数据来自 SPU 聚合内 SKU 实体的 StockQty 字段。
    /// </summary>
    /// <param name="shopId">店铺标识。</param>
    /// <param name="threshold">低库存阈值，StockQty 小于此值视为低库存。</param>
    /// <param name="ct">取消令牌。</param>
    /// <returns>低库存 SKU 列表，无命中返回空列表。</returns>
    Task<List<LowStockSkuDto>> GetLowStockByShopAsync(Guid shopId, int threshold, CancellationToken ct = default);
```

- [ ] **Step 3: 实现低库存查询**

找到 `ProductInternalQueryService` 实现类（在 `src/Services/Product/Leno.Product.Application/Services/` 或 `src/Services/Product/Leno.Product.Infrastructure/` 下，用 Grep 搜索 `class ProductInternalQueryService`）。

在实现类中注入 `ISPURepository`（若未注入则追加构造参数），实现 `GetLowStockByShopAsync`：

```csharp
    public async Task<List<LowStockSkuDto>> GetLowStockByShopAsync(Guid shopId, int threshold, CancellationToken ct = default)
    {
        if (shopId == Guid.Empty)
        {
            return new List<LowStockSkuDto>();
        }

        var spus = await _spuRepository.GetByShopIdAsync(shopId, ct);

        return spus
            .SelectMany(spu => spu.Skus.Select(sku => new LowStockSkuDto
            {
                SkuId = sku.Id,
                ProductId = spu.Id,
                ProductName = spu.Title,
                SkuName = string.Join("/", sku.SpecAttributes?.Select(a => a.Value) ?? Array.Empty<string>()),
                Stock = sku.StockQty,
                Threshold = threshold,
                ShopId = shopId
            }))
            .Where(x => x.Stock < threshold)
            .OrderBy(x => x.Stock)
            .ToList();
    }
```

注意：`SKU.SpecAttributes` 为 `SkuSpec` 集合，字段名以实际为准（用 Grep 确认 `SkuSpec` 的属性名）。若 `SkuSpec` 无 `Value` 字段，改用 `sku.SpecAttributes?.Select(a => a.ToString())`。

- [ ] **Step 4: 编译验证**

Run (cwd: `/workspace`): `dotnet build src/Services/Product/Leno.Product.Application/Leno.Product.Application.csproj`
Expected: BUILD SUCCEEDED

- [ ] **Step 5: 提交**

```bash
git add src/Services/Product/Leno.Product.Application/LowStockSkuDto.cs src/Services/Product/Leno.Product.Application/IProductInternalQueryService.cs src/Services/Product/Leno.Product.Application/Services/ProductInternalQueryService.cs
git commit -m "feat(product): add low-stock query to internal query service (BE-2)"
```

---

## Task 8: BE-2 Product gRPC 端点 + SellerShop ACL + 卖家端点

**Files:**
- Modify: `src/Contracts/Leno.SharedContracts/Grpc/Protos/product_internal.proto`
- Modify: `src/Services/Product/Leno.Product.Api/GrpcServices/ProductGrpcService.cs`
- Modify: `src/Services/SellerShop/Leno.SellerShop.Application/Services/IProductAntiCorruptionService.cs`
- Create: `src/Services/SellerShop/Leno.SellerShop.Application/LowStockItemDto.cs`
- Modify: `src/Services/SellerShop/Leno.SellerShop.Infrastructure/Services/Grpc/GrpcProductAntiCorruptionClient.cs`
- Modify: `src/Services/SellerShop/Leno.SellerShop.Application/ISellerDashboardAppService.cs`
- Modify: `src/Services/SellerShop/Leno.SellerShop.Application/Services/SellerDashboardAppService.cs`
- Modify: `src/Services/SellerShop/Leno.SellerShop.Api/Controllers/SellerDashboardController.cs`

- [ ] **Step 1: 扩展 product_internal.proto**

在 `src/Contracts/Leno.SharedContracts/Grpc/Protos/product_internal.proto` 的 `ProductInternalService` 中追加 rpc 与消息（参考现有 proto 格式）：

```protobuf
  rpc GetLowStockByShop(GetLowStockByShopRequest) returns (GetLowStockByShopResponse);

message GetLowStockByShopRequest {
  string shop_id = 1;
  int32 threshold = 2;
}

message LowStockSkuItem {
  string sku_id = 1;
  string product_id = 2;
  string product_name = 3;
  string sku_name = 4;
  int32 stock = 5;
  int32 threshold = 6;
  string shop_id = 7;
}

message GetLowStockByShopResponse {
  repeated LowStockSkuItem items = 1;
}
```

- [ ] **Step 2: 实现 Product gRPC 端点**

修改 `src/Services/Product/Leno.Product.Api/GrpcServices/ProductGrpcService.cs`，新增 `GetLowStockByShop` 方法：

```csharp
    public override async Task<GetLowStockByShopResponse> GetLowStockByShop(
        GetLowStockByShopRequest request, ServerCallContext context)
    {
        if (!Guid.TryParse(request.ShopId, out var shopId))
        {
            return new GetLowStockByShopResponse();
        }

        var items = await _queryService.GetLowStockByShopAsync(shopId, request.Threshold, context.CancellationToken);
        var response = new GetLowStockByShopResponse();
        response.Items.AddRange(items.Select(x => new LowStockSkuItem
        {
            SkuId = x.SkuId.ToString(),
            ProductId = x.ProductId.ToString(),
            ProductName = x.ProductName ?? string.Empty,
            SkuName = x.SkuName ?? string.Empty,
            Stock = x.Stock,
            Threshold = x.Threshold,
            ShopId = x.ShopId.ToString()
        }));
        return response;
    }
```

- [ ] **Step 3: 创建卖家域 LowStockItemDto**

创建 `src/Services/SellerShop/Leno.SellerShop.Application/LowStockItemDto.cs`：

```csharp
namespace Leno.SellerShop.Application;

/// <summary>
/// 低库存商品 DTO（卖家域视角），由 ACL 从商品域 LowStockSkuDto 映射。
/// </summary>
public sealed class LowStockItemDto
{
    public Guid SkuId { get; init; }
    public Guid ProductId { get; init; }
    public string ProductName { get; init; } = string.Empty;
    public string SkuName { get; init; } = string.Empty;
    public int Stock { get; init; }
    public int Threshold { get; init; }
}
```

- [ ] **Step 4: 扩展 IProductAntiCorruptionService**

修改 `src/Services/SellerShop/Leno.SellerShop.Application/Services/IProductAntiCorruptionService.cs`，追加方法：

```csharp
    /// <summary>
    /// 查询指定店铺的低库存 SKU 列表（StockQty &lt; threshold）。
    /// 经 gRPC 调商品域 ProductInternalService.GetLowStockByShop。
    /// </summary>
    /// <param name="shopId">店铺标识。</param>
    /// <param name="threshold">低库存阈值。</param>
    /// <param name="ct">取消令牌。</param>
    /// <returns>低库存 SKU 列表；ACL 调用失败时返回空列表（fail-soft，避免工作台白屏）。</returns>
    Task<List<LowStockItemDto>> GetLowStockSkusAsync(Guid shopId, int threshold, CancellationToken ct = default);
```

- [ ] **Step 5: 实现 GrpcProductAntiCorruptionClient.GetLowStockSkusAsync**

修改 `src/Services/SellerShop/Leno.SellerShop.Infrastructure/Services/Grpc/GrpcProductAntiCorruptionClient.cs`，追加方法：

```csharp
    /// <inheritdoc />
    public async Task<List<LowStockItemDto>> GetLowStockSkusAsync(Guid shopId, int threshold, CancellationToken ct = default)
    {
        try
        {
            return await ExecuteAsync("get_low_stock", async token =>
            {
                var request = new GetLowStockByShopRequest
                {
                    ShopId = shopId.ToString(),
                    Threshold = threshold
                };
                var metadata = BuildMetadata();
                var response = await _client.GetLowStockByShopAsync(request, metadata, cancellationToken: token)
                    .ConfigureAwait(false);
                return response.Items.Select(x => new LowStockItemDto
                {
                    SkuId = Guid.TryParse(x.SkuId, out var sid) ? sid : Guid.Empty,
                    ProductId = Guid.TryParse(x.ProductId, out var pid) ? pid : Guid.Empty,
                    ProductName = x.ProductName,
                    SkuName = x.SkuName,
                    Stock = x.Stock,
                    Threshold = x.Threshold
                }).ToList();
            }, ct).ConfigureAwait(false);
        }
        catch (AntiCorruptionException ex)
        {
            // fail-soft：跨域调用失败时返回空列表，工作台显示"暂无低库存商品"
            AntiCorruptionMetrics.RecordFailure(ServiceName, "get_low_stock", "fail-soft");
            _logger.LogWarning(ex, "商品域 GetLowStockByShop 调用失败，fail-soft 返回空列表 ShopId={ShopId}", shopId);
            return new List<LowStockItemDto>();
        }
    }
```

- [ ] **Step 6: 扩展 ISellerDashboardAppService**

修改 `src/Services/SellerShop/Leno.SellerShop.Application/ISellerDashboardAppService.cs`，追加方法：

```csharp
    /// <summary>
    /// 查询当前卖家店铺的低库存 SKU 列表（经 ACL 调商品域）。
    /// </summary>
    /// <param name="sellerId">卖家标识（取自 JWT）。</param>
    /// <param name="threshold">低库存阈值。</param>
    /// <param name="ct">取消令牌。</param>
    /// <returns>低库存 SKU 列表。</returns>
    Task<List<LowStockItemDto>> GetLowStockAlertAsync(Guid sellerId, int threshold, CancellationToken ct = default);
```

- [ ] **Step 7: 实现 SellerDashboardAppService.GetLowStockAlertAsync**

修改 `src/Services/SellerShop/Leno.SellerShop.Application/Services/SellerDashboardAppService.cs`，注入 `IShopAppService`（已有则跳过）与 `IProductAntiCorruptionService`，实现方法：

```csharp
    public async Task<List<LowStockItemDto>> GetLowStockAlertAsync(Guid sellerId, int threshold, CancellationToken ct = default)
    {
        var shop = await _shopAppService.GetMyShopAsync(sellerId, ct);
        return await _productAntiCorruptionService.GetLowStockSkusAsync(shop.Id, threshold, ct);
    }
```

- [ ] **Step 8: 新增 SellerDashboardController low-stock 端点**

修改 `src/Services/SellerShop/Leno.SellerShop.Api/Controllers/SellerDashboardController.cs`，追加方法（在 `GetMetricsAsync` 后）：

```csharp
    /// <summary>查询当前卖家店铺的低库存 SKU 列表（经 ACL 调商品域）。</summary>
    [HttpGet("dashboard/low-stock")]
    [ProducesResponseType(typeof(ApiResponse<List<LowStockItemDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetLowStockAlertAsync(
        [FromQuery] int threshold = 10,
        CancellationToken ct = default)
    {
        var sellerId = GetCurrentUserId();
        var items = await _dashboardAppService.GetLowStockAlertAsync(sellerId, threshold, ct);
        return Ok(ApiResponse.Success(items));
    }
```

并在文件顶部追加 `using Leno.SellerShop.Application;` 若 `LowStockItemDto` 命名空间为 `Leno.SellerShop.Application`。

- [ ] **Step 9: 编译验证**

Run (cwd: `/workspace`): `dotnet build src/Services/SellerShop/Leno.SellerShop.Api/Leno.SellerShop.Api.csproj`
Expected: BUILD SUCCEEDED

- [ ] **Step 10: 提交**

```bash
git add src/Contracts/ src/Services/Product/ src/Services/SellerShop/
git commit -m "feat(seller-shop): add low-stock alert endpoint via gRPC ACL (BE-2)"
```

---

## Task 9: BE-2 前端 LowStockAlert 接入真实 API

**Files:**
- Modify: `web/seller/src/modules/02-dashboard/api/dashboard.api.ts`
- Modify: `web/seller/src/modules/02-dashboard/views/LowStockAlert.vue`

- [ ] **Step 1: 扩展 dashboard.api.ts**

修改 `web/seller/src/modules/02-dashboard/api/dashboard.api.ts`，追加 `getLowStock`：

```typescript
import { client } from '@/shared/http'
import type { SellerDashboardDto, SalesTrendItemDto, DateRangeParams, LowStockItemDto } from '../types/dashboard.dto'

export const dashboardApi = {
  getDashboard: () =>
    client.get<SellerDashboardDto>('/seller/dashboard').then((r) => r.data),

  getSalesTrend: (params: DateRangeParams) =>
    client.get<SalesTrendItemDto[]>('/seller/sales-trend', { params }).then((r) => r.data),

  getLowStock: (threshold: number) =>
    client.get<LowStockItemDto[]>('/seller/dashboard/low-stock', {
      params: { threshold },
    }).then((r) => r.data),
}
```

- [ ] **Step 2: 改造 LowStockAlert.vue**

将 `web/seller/src/modules/02-dashboard/views/LowStockAlert.vue` 完整替换为（移除 mock 数据与 BE-2 标记，接入真实 API）：

```vue
<script setup lang="ts">
import { ref, computed, watch, onMounted } from 'vue'
import {
  Breadcrumb,
  BreadcrumbItem,
  Card,
  Table,
  Tag,
  Alert,
  Skeleton,
  InputNumber,
  Space,
  message,
} from 'ant-design-vue'
import type { TableColumnsType } from 'ant-design-vue'
import { WarningOutlined } from '@ant-design/icons-vue'
import type { LowStockItemDto } from '../types/dashboard.dto'
import { dashboardApi } from '../api/dashboard.api'
import { EmptyState } from '@/shared/components'
import { logger } from '@/shared/utils/logger'

/**
 * 库存预警页
 *
 * 路由 /dashboard/low-stock，调真实后端 GET /api/seller/dashboard/low-stock?threshold=
 * 数据经 SellerShop ACL 从 Product 域 gRPC 获取。
 */

const loading = ref(true)
const threshold = ref(10)
const dataSource = ref<LowStockItemDto[]>([])

interface StockStatus {
  label: string
  color: string
}

function deriveStatus(stock: number, thresholdVal: number): StockStatus {
  if (stock < 5) return { label: '紧急', color: 'error' }
  if (stock < 10) return { label: '警告', color: 'warning' }
  if (stock < thresholdVal) return { label: '偏低', color: 'processing' }
  return { label: '正常', color: 'success' }
}

const filteredData = computed<LowStockItemDto[]>(() => {
  return [...dataSource.value].sort((a, b) => a.stock - b.stock)
})

const alertCount = computed(() => filteredData.value.length)

const columns: TableColumnsType = [
  { title: '商品名称', dataIndex: 'productName', key: 'productName', width: 200, ellipsis: true },
  { title: 'SKU', dataIndex: 'skuName', key: 'skuName', width: 140 },
  {
    title: '当前库存',
    dataIndex: 'stock',
    key: 'stock',
    width: 120,
    sorter: (a: LowStockItemDto, b: LowStockItemDto) => a.stock - b.stock,
    defaultSortOrder: 'ascend',
  },
  { title: '预警阈值', dataIndex: 'threshold', key: 'threshold', width: 120 },
  { title: '状态', key: 'status', width: 100 },
]

async function loadData(): Promise<void> {
  loading.value = true
  try {
    dataSource.value = await dashboardApi.getLowStock(threshold.value)
  } catch (e) {
    logger.error('加载低库存列表失败', e)
    message.error('加载低库存列表失败')
    dataSource.value = []
  } finally {
    loading.value = false
  }
}

watch(threshold, () => {
  void loadData()
})

onMounted(() => {
  void loadData()
})
</script>

<template>
  <div class="low-stock-page">
    <Breadcrumb class="low-stock-breadcrumb">
      <BreadcrumbItem>首页</BreadcrumbItem>
      <BreadcrumbItem>工作台</BreadcrumbItem>
      <BreadcrumbItem>库存预警</BreadcrumbItem>
    </Breadcrumb>

    <Alert
      v-if="!loading && alertCount > 0"
      type="warning"
      show-icon
      :message="`当前有 ${alertCount} 个 SKU 库存低于阈值 ${threshold}，建议尽快补货`"
      class="low-stock-alert"
    />

    <Card class="low-stock-filter" :bordered="true">
      <Space :size="16" align="center">
        <span class="low-stock-filter-label">
          <WarningOutlined class="low-stock-filter-icon" />
          预警阈值
        </span>
        <InputNumber
          v-model:value="threshold"
          :min="1"
          :max="999"
          :step="1"
          aria-label="库存预警阈值"
        />
      </Space>
    </Card>

    <Card class="low-stock-table-card" :bordered="true">
      <template #title>
        <span class="low-stock-table-title">低库存商品列表</span>
      </template>
      <Skeleton v-if="loading" :title="{ width: '100%' }" :paragraph="{ rows: 8 }" active />
      <EmptyState
        v-else-if="alertCount === 0"
        description="库存充足，当前无低库存商品"
      />
      <Table
        v-else
        :columns="columns"
        :data-source="filteredData"
        :row-key="(record: LowStockItemDto) => record.skuId"
        :pagination="{ pageSize: 10, showSizeChanger: false }"
        size="middle"
        aria-label="低库存商品表格"
        class="low-stock-table"
      >
        <template #bodyCell="{ column, record }">
          <template v-if="column.key === 'status'">
            <Tag :color="deriveStatus(record.stock, threshold).color">
              {{ deriveStatus(record.stock, threshold).label }}
            </Tag>
          </template>
        </template>
      </Table>
    </Card>
  </div>
</template>

<style scoped>
.low-stock-page {
  display: flex;
  flex-direction: column;
  gap: 16px;
}
.low-stock-breadcrumb {
  font-size: 14px;
}
.low-stock-alert {
  border-radius: 8px;
}
.low-stock-filter {
  border-radius: 8px;
}
.low-stock-filter-label {
  display: inline-flex;
  align-items: center;
  gap: 6px;
  font-size: 14px;
  color: #595959;
}
.low-stock-filter-icon {
  color: #faad14;
  font-size: 16px;
}
.low-stock-table-card {
  border-radius: 8px;
}
.low-stock-table-title {
  font-size: 16px;
  font-weight: 500;
}
.low-stock-table {
  width: 100%;
}
</style>
```

- [ ] **Step 3: 运行前端验证**

Run (cwd: `web/seller`): `pnpm typecheck && pnpm lint -- src/modules/02-dashboard/`
Expected: 0 errors

- [ ] **Step 4: 提交**

```bash
git add web/seller/src/modules/02-dashboard/
git commit -m "feat(dashboard): integrate low-stock API into LowStockAlert.vue (BE-2)"
```

---

## Task 10: BE-3 ExportTask 聚合 + 仓储 + DTO

**Files:**
- Create: `src/Services/SellerShop/Leno.SellerShop.Domain/Aggregates/ExportTask.cs`
- Create: `src/Services/SellerShop/Leno.SellerShop.Domain/Repositories/IExportTaskRepository.cs`
- Create: `src/Services/SellerShop/Leno.SellerShop.Application/Dtos/ExportDtos.cs`
- Modify: `src/Services/SellerShop/Leno.SellerShop.Infrastructure/SellerShopDbContext.cs`

- [ ] **Step 1: 创建 ExportTask 聚合**

创建 `src/Services/SellerShop/Leno.SellerShop.Domain/Aggregates/ExportTask.cs`：

```csharp
using Leno.SharedKernel.Abstractions;

namespace Leno.SellerShop.Domain.Aggregates;

/// <summary>
/// 导出任务聚合根，记录卖家数据导出任务的生命周期。
/// 状态机：Processing → Completed | Failed。
/// 由 ExportAppService 创建（Processing），由 ExportBackgroundService 处理后标记终态。
/// </summary>
public sealed class ExportTask : AggregateRoot
{
    /// <summary>所属店铺标识。</summary>
    public Guid ShopId { get; private set; }

    /// <summary>卖家标识。</summary>
    public Guid SellerId { get; private set; }

    /// <summary>报表类型（SalesSummary/OrderDetail/ProductSales）。</summary>
    public string ReportType { get; private set; } = string.Empty;

    /// <summary>导出起始日期（UTC）。</summary>
    public DateTime StartDate { get; private set; }

    /// <summary>导出结束日期（UTC）。</summary>
    public DateTime EndDate { get; private set; }

    /// <summary>导出格式（Excel/CSV）。</summary>
    public string Format { get; private set; } = string.Empty;

    /// <summary>任务状态（Processing/Completed/Failed）。</summary>
    public string Status { get; private set; } = "Processing";

    /// <summary>记录数（完成后填充）。</summary>
    public int? RecordCount { get; private set; }

    /// <summary>文件大小（字节，完成后填充）。</summary>
    public long? FileSize { get; private set; }

    /// <summary>文件路径（完成后填充，相对 IFileStorageService 路径）。</summary>
    public string? FilePath { get; private set; }

    /// <summary>错误信息（失败时填充）。</summary>
    public string? ErrorMessage { get; private set; }

    /// <summary>创建时间（UTC）。</summary>
    public DateTime CreatedAt { get; private set; }

    /// <summary>完成时间（UTC）。</summary>
    public DateTime? CompletedAt { get; private set; }

    /// <summary>EF Core 无参构造。</summary>
    private ExportTask() { }

    private ExportTask(Guid id) : base(id) { }

    /// <summary>
    /// 工厂方法，创建导出任务（初始状态 Processing）。
    /// </summary>
    public static ExportTask Create(
        Guid taskId,
        Guid shopId,
        Guid sellerId,
        string reportType,
        DateTime startDate,
        DateTime endDate,
        string format)
    {
        if (taskId == Guid.Empty)
            throw new ArgumentException("任务标识不可为空", nameof(taskId));
        if (shopId == Guid.Empty)
            throw new ArgumentException("店铺标识不可为空", nameof(shopId));
        if (sellerId == Guid.Empty)
            throw new ArgumentException("卖家标识不可为空", nameof(sellerId));
        if (string.IsNullOrWhiteSpace(reportType))
            throw new ArgumentException("报表类型不可为空", nameof(reportType));
        if (string.IsNullOrWhiteSpace(format))
            throw new ArgumentException("导出格式不可为空", nameof(format));
        if (endDate < startDate)
            throw new ArgumentException("结束时间不能早于开始时间");

        return new ExportTask(taskId)
        {
            ShopId = shopId,
            SellerId = sellerId,
            ReportType = reportType,
            StartDate = startDate,
            EndDate = endDate,
            Format = format,
            Status = "Processing",
            CreatedAt = DateTime.UtcNow
        };
    }

    /// <summary>标记任务完成。</summary>
    public void MarkCompleted(int recordCount, long fileSize, string filePath)
    {
        if (Status != "Processing")
            throw new InvalidOperationException($"任务已处于终态 {Status}，不可标记完成");

        Status = "Completed";
        RecordCount = recordCount;
        FileSize = fileSize;
        FilePath = filePath ?? throw new ArgumentNullException(nameof(filePath));
        CompletedAt = DateTime.UtcNow;
    }

    /// <summary>标记任务失败。</summary>
    public void MarkFailed(string errorMessage)
    {
        if (Status != "Processing")
            throw new InvalidOperationException($"任务已处于终态 {Status}，不可标记失败");

        Status = "Failed";
        ErrorMessage = errorMessage ?? "未知错误";
        CompletedAt = DateTime.UtcNow;
    }
}
```

- [ ] **Step 2: 创建 IExportTaskRepository**

创建 `src/Services/SellerShop/Leno.SellerShop.Domain/Repositories/IExportTaskRepository.cs`：

```csharp
using Leno.SellerShop.Domain.Aggregates;
using Leno.SharedKernel.Abstractions;

namespace Leno.SellerShop.Domain.Repositories;

/// <summary>
/// 导出任务仓储接口，定义在领域层，由基础设施层实现。
/// </summary>
public interface IExportTaskRepository : IRepository<ExportTask>
{
    /// <summary>按标识查询导出任务。</summary>
    Task<ExportTask?> GetByIdAsync(Guid id, CancellationToken ct = default);

    /// <summary>按店铺分页查询导出任务（按状态可选过滤，按创建时间倒序）。</summary>
    Task<(IReadOnlyList<ExportTask> Items, int Total)> ListByShopAsync(
        Guid shopId,
        string? status,
        int page,
        int pageSize,
        CancellationToken ct = default);

    /// <summary>查询最早的处理中任务（供后台作业轮询）。</summary>
    Task<ExportTask?> GetOldestProcessingAsync(CancellationToken ct = default);
}
```

- [ ] **Step 3: 创建 ExportDtos**

创建 `src/Services/SellerShop/Leno.SellerShop.Application/Dtos/ExportDtos.cs`：

```csharp
namespace Leno.SellerShop.Application.Dtos;

/// <summary>
/// 创建导出任务请求 DTO。
/// </summary>
public sealed class CreateExportTaskDto
{
    public string ReportType { get; set; } = string.Empty;
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
    public string Format { get; set; } = string.Empty;
}

/// <summary>
/// 导出任务 DTO（API 响应）。
/// </summary>
public sealed class ExportTaskDto
{
    public Guid Id { get; set; }
    public string ReportType { get; set; } = string.Empty;
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
    public string Format { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public int? RecordCount { get; set; }
    public long? FileSize { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public string? ErrorMessage { get; set; }
}

/// <summary>
/// 导出任务列表查询参数。
/// </summary>
public sealed class ExportTaskQueryParams
{
    public string? Status { get; set; }
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 20;
}
```

- [ ] **Step 4: 扩展 SellerShopDbContext**

修改 `src/Services/SellerShop/Leno.SellerShop.Infrastructure/SellerShopDbContext.cs`，在 `ShopQualifications` 后追加：

```csharp
    /// <summary>导出任务聚合根。</summary>
    public DbSet<ExportTask> ExportTasks => Set<ExportTask>();
```

- [ ] **Step 5: 提交**

```bash
git add src/Services/SellerShop/Leno.SellerShop.Domain/Aggregates/ExportTask.cs src/Services/SellerShop/Leno.SellerShop.Domain/Repositories/IExportTaskRepository.cs src/Services/SellerShop/Leno.SellerShop.Application/Dtos/ExportDtos.cs src/Services/SellerShop/Leno.SellerShop.Infrastructure/SellerShopDbContext.cs
git commit -m "feat(seller-shop): add ExportTask aggregate + repository + DTOs (BE-3)"
```

---

## Task 11: BE-3 ExportTaskRepository + EF Core 迁移 + IFileStorageService 扩展

**Files:**
- Create: `src/Services/SellerShop/Leno.SellerShop.Infrastructure/Repositories/ExportTaskRepository.cs`
- Modify: `src/Services/SellerShop/Leno.SellerShop.Infrastructure/Leno.SellerShop.Infrastructure.csproj`
- Modify: `src/BuildingBlocks/Leno.Infrastructure/Storage/LocalFileStorageService.cs`

- [ ] **Step 1: 创建 ExportTaskRepository**

创建 `src/Services/SellerShop/Leno.SellerShop.Infrastructure/Repositories/ExportTaskRepository.cs`：

```csharp
using Leno.SellerShop.Domain.Aggregates;
using Leno.SellerShop.Domain.Repositories;
using Microsoft.EntityFrameworkCore;

namespace Leno.SellerShop.Infrastructure.Repositories;

/// <summary>
/// 导出任务仓储 EF Core 实现。
/// </summary>
public sealed class ExportTaskRepository : IExportTaskRepository
{
    private readonly SellerShopDbContext _db;

    public ExportTaskRepository(SellerShopDbContext db)
    {
        _db = db ?? throw new ArgumentNullException(nameof(db));
    }

    public async Task<ExportTask?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        return await _db.ExportTasks.FindAsync(new object?[] { id }, ct);
    }

    public async Task AddAsync(ExportTask task, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(task);
        await _db.ExportTasks.AddAsync(task, ct);
    }

    public async Task<(IReadOnlyList<ExportTask> Items, int Total)> ListByShopAsync(
        Guid shopId,
        string? status,
        int page,
        int pageSize,
        CancellationToken ct = default)
    {
        var query = _db.ExportTasks.Where(t => t.ShopId == shopId);
        if (!string.IsNullOrEmpty(status))
        {
            query = query.Where(t => t.Status == status);
        }

        var total = await query.CountAsync(ct);
        var safePage = page < 1 ? 1 : page;
        var safePageSize = pageSize is <= 0 or > 100 ? 20 : pageSize;
        var items = await query
            .OrderByDescending(t => t.CreatedAt)
            .Skip((safePage - 1) * safePageSize)
            .Take(safePageSize)
            .ToListAsync(ct);

        return (items, total);
    }

    public async Task<ExportTask?> GetOldestProcessingAsync(CancellationToken ct = default)
    {
        return await _db.ExportTasks
            .Where(t => t.Status == "Processing")
            .OrderBy(t => t.CreatedAt)
            .FirstOrDefaultAsync(ct);
    }
}
```

- [ ] **Step 2: 新增 ClosedXML 包引用**

修改 `src/Services/SellerShop/Leno.SellerShop.Infrastructure/Leno.SellerShop.Infrastructure.csproj`，在 `ItemGroup` 中追加：

```xml
    <PackageReference Include="ClosedXML" Version="0.102.1" />
```

- [ ] **Step 3: 扩展 LocalFileStorageService AllowedCategories**

修改 `src/BuildingBlocks/Leno.Infrastructure/Storage/LocalFileStorageService.cs`，将 `AllowedCategories` 集合（第 17-18 行）替换为：

```csharp
    private static readonly HashSet<string> AllowedCategories =
        new(StringComparer.OrdinalIgnoreCase) { "avatar", "product", "review", "aftersales", "credential", "export", "misc" };
```

- [ ] **Step 4: 创建 EF Core 迁移**

Run (cwd: `/workspace`):
```bash
dotnet ef migrations add AddExportTasks \
  --project src/Services/SellerShop/Leno.SellerShop.Infrastructure \
  --startup-project src/Services/SellerShop/Leno.SellerShop.Api \
  --output-dir Migrations
```
Expected: 迁移文件 `AddExportTasks` 创建成功

- [ ] **Step 5: 编译验证**

Run (cwd: `/workspace`): `dotnet build src/Services/SellerShop/Leno.SellerShop.Infrastructure/Leno.SellerShop.Infrastructure.csproj`
Expected: BUILD SUCCEEDED

- [ ] **Step 6: 提交**

```bash
git add src/Services/SellerShop/Leno.SellerShop.Infrastructure/Repositories/ExportTaskRepository.cs src/Services/SellerShop/Leno.SellerShop.Infrastructure/Leno.SellerShop.Infrastructure.csproj src/Services/SellerShop/Leno.SellerShop.Infrastructure/Migrations/ src/BuildingBlocks/Leno.Infrastructure/Storage/LocalFileStorageService.cs
git commit -m "feat(seller-shop): add ExportTaskRepository + migration + export file category (BE-3)"
```

---

## Task 12: BE-3 ExportAppService + ExportFileGenerator + ExportBackgroundService

**Files:**
- Create: `src/Services/SellerShop/Leno.SellerShop.Application/IExportAppService.cs`
- Create: `src/Services/SellerShop/Leno.SellerShop.Application/Services/ExportAppService.cs`
- Create: `src/Services/SellerShop/Leno.SellerShop.Infrastructure/Export/ExportFileGenerator.cs`
- Create: `src/Services/SellerShop/Leno.SellerShop.Infrastructure/Export/ExportBackgroundService.cs`

- [ ] **Step 1: 创建 IExportAppService**

创建 `src/Services/SellerShop/Leno.SellerShop.Application/IExportAppService.cs`：

```csharp
using Leno.SellerShop.Application.Dtos;
using Leno.SharedContracts.Responses;

namespace Leno.SellerShop.Application;

/// <summary>
/// 数据导出应用服务，创建导出任务、查询任务列表、获取下载文件路径。
/// 实际文件生成由 ExportBackgroundService 异步完成。
/// </summary>
public interface IExportAppService
{
    /// <summary>创建导出任务（状态 Processing，等待后台作业处理）。</summary>
    Task<ExportTaskDto> CreateTaskAsync(Guid sellerId, CreateExportTaskDto dto, CancellationToken ct = default);

    /// <summary>分页查询导出任务列表。</summary>
    Task<PageResult<ExportTaskDto>> ListTasksAsync(Guid sellerId, ExportTaskQueryParams queryParams, CancellationToken ct = default);

    /// <summary>获取导出任务文件路径（供 Controller 读取 stream 返回）。</summary>
    Task<(string FilePath, string ContentType, string FileName)?> GetDownloadAsync(Guid sellerId, Guid taskId, CancellationToken ct = default);
}
```

- [ ] **Step 2: 创建 ExportAppService**

创建 `src/Services/SellerShop/Leno.SellerShop.Application/Services/ExportAppService.cs`：

```csharp
using Leno.SellerShop.Application.Dtos;
using Leno.SellerShop.Domain.Aggregates;
using Leno.SellerShop.Domain.Repositories;
using Leno.SharedContracts.Responses;
using Leno.SharedKernel.Abstractions;

namespace Leno.SellerShop.Application.Services;

/// <summary>
/// 导出应用服务实现。创建任务时校验 90 天范围约束，查询任务列表映射 DTO。
/// </summary>
public sealed class ExportAppService : IExportAppService
{
    private const int MaxRangeDays = 90;
    private readonly IExportTaskRepository _taskRepository;
    private readonly IShopAppService _shopAppService;
    private readonly IUnitOfWork _unitOfWork;

    public ExportAppService(
        IExportTaskRepository taskRepository,
        IShopAppService shopAppService,
        IUnitOfWork unitOfWork)
    {
        _taskRepository = taskRepository ?? throw new ArgumentNullException(nameof(taskRepository));
        _shopAppService = shopAppService ?? throw new ArgumentNullException(nameof(shopAppService));
        _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
    }

    public async Task<ExportTaskDto> CreateTaskAsync(Guid sellerId, CreateExportTaskDto dto, CancellationToken ct = default)
    {
        ValidateDto(dto);

        var shop = await _shopAppService.GetMyShopAsync(sellerId, ct);
        var task = ExportTask.Create(
            Guid.NewGuid(),
            shop.Id,
            sellerId,
            dto.ReportType,
            dto.StartDate,
            dto.EndDate,
            dto.Format);

        await _taskRepository.AddAsync(task, ct);
        await _unitOfWork.SaveChangesAsync(ct);

        return ToDto(task);
    }

    public async Task<PageResult<ExportTaskDto>> ListTasksAsync(Guid sellerId, ExportTaskQueryParams queryParams, CancellationToken ct = default)
    {
        var shop = await _shopAppService.GetMyShopAsync(sellerId, ct);
        var (items, total) = await _taskRepository.ListByShopAsync(
            shop.Id, queryParams.Status, queryParams.Page, queryParams.PageSize, ct);

        var dtos = items.Select(ToDto).ToList();
        return new PageResult<ExportTaskDto>(dtos, total, queryParams.Page, queryParams.PageSize);
    }

    public async Task<(string FilePath, string ContentType, string FileName)?> GetDownloadAsync(Guid sellerId, Guid taskId, CancellationToken ct = default)
    {
        var task = await _taskRepository.GetByIdAsync(taskId, ct);
        if (task is null || task.SellerId != sellerId)
        {
            return null;
        }
        if (task.Status != "Completed" || string.IsNullOrEmpty(task.FilePath))
        {
            return null;
        }

        var ext = task.Format == "Excel" ? "xlsx" : "csv";
        var contentType = task.Format == "Excel"
            ? "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet"
            : "text/csv";
        var fileName = $"{task.ReportType}-{task.Id}.{ext}";

        return (task.FilePath, contentType, fileName);
    }

    private static void ValidateDto(CreateExportTaskDto dto)
    {
        if (string.IsNullOrWhiteSpace(dto.ReportType))
            throw new ArgumentException("报表类型不可为空");
        if (string.IsNullOrWhiteSpace(dto.Format))
            throw new ArgumentException("导出格式不可为空");
        if (dto.EndDate < dto.StartDate)
            throw new ArgumentException("结束时间不能早于开始时间");
        if ((dto.EndDate - dto.StartDate).TotalDays > MaxRangeDays)
            throw new ArgumentException($"时间范围不能超过 {MaxRangeDays} 天");
    }

    private static ExportTaskDto ToDto(ExportTask task) => new()
    {
        Id = task.Id,
        ReportType = task.ReportType,
        StartDate = task.StartDate,
        EndDate = task.EndDate,
        Format = task.Format,
        Status = task.Status,
        RecordCount = task.RecordCount,
        FileSize = task.FileSize,
        CreatedAt = task.CreatedAt,
        CompletedAt = task.CompletedAt,
        ErrorMessage = task.ErrorMessage
    };
}
```

- [ ] **Step 3: 创建 ExportFileGenerator**

创建 `src/Services/SellerShop/Leno.SellerShop.Infrastructure/Export/ExportFileGenerator.cs`：

```csharp
using ClosedXML.Excel;

namespace Leno.SellerShop.Infrastructure.Export;

/// <summary>
/// 导出文件生成器，将数据行渲染为 Excel（ClosedXML）或 CSV。
/// </summary>
public sealed class ExportFileGenerator
{
    /// <summary>生成 Excel 字节流。</summary>
    public byte[] GenerateExcel(string sheetName, IReadOnlyList<string> headers, IReadOnlyList<IReadOnlyDictionary<string, object?>> rows)
    {
        using var wb = new XLWorkbook();
        var ws = wb.AddWorksheet(sheetName);

        for (var i = 0; i < headers.Count; i++)
        {
            ws.Cell(1, i + 1).Value = headers[i];
        }
        ws.Row(1).Style.Font.Bold = true;

        for (var r = 0; r < rows.Count; r++)
        {
            var row = rows[r];
            for (var c = 0; c < headers.Count; c++)
            {
                var key = headers[c];
                var value = row.TryGetValue(key, out var v) ? v : null;
                ws.Cell(r + 2, c + 1).Value = value switch
                {
                    null => string.Empty,
                    int iv => iv,
                    long lv => lv,
                    decimal dv => dv,
                    double dv2 => dv2,
                    DateTime dt => dt,
                    bool bv => bv,
                    _ => value.ToString()
                };
            }
        }

        ws.Columns().AdjustToContents();
        using var ms = new MemoryStream();
        wb.SaveAs(ms);
        return ms.ToArray();
    }

    /// <summary>生成 CSV 字节流（UTF-8 with BOM）。</summary>
    public byte[] GenerateCsv(IReadOnlyList<string> headers, IReadOnlyList<IReadOnlyDictionary<string, object?>> rows)
    {
        using var ms = new MemoryStream();
        using var writer = new StreamWriter(ms, new System.Text.UTF8Encoding(true));
        writer.WriteLine(string.Join(",", headers.Select(EscapeCsv)));
        foreach (var row in rows)
        {
            var values = headers.Select(h => row.TryGetValue(h, out var v) ? v?.ToString() ?? string.Empty : string.Empty);
            writer.WriteLine(string.Join(",", values.Select(EscapeCSV)));
        }
        writer.Flush();
        return ms.ToArray();
    }

    private static string EscapeCSV(string? value)
    {
        if (value is null) return string.Empty;
        if (value.Contains(',') || value.Contains('"') || value.Contains('\n'))
        {
            return $"\"{value.Replace("\"", "\"\"")}\"";
        }
        return value;
    }

    private static string EscapeCSV(object? value) => EscapeCSV(value?.ToString());
}
```

- [ ] **Step 4: 创建 ExportBackgroundService**

创建 `src/Services/SellerShop/Leno.SellerShop.Infrastructure/Export/ExportBackgroundService.cs`：

```csharp
using Leno.Infrastructure.Abstractions;
using Leno.SellerShop.Application.Services;
using Leno.SellerShop.Domain.Aggregates;
using Leno.SellerShop.Domain.Repositories;
using Leno.SharedKernel.Abstractions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Leno.SellerShop.Infrastructure.Export;

/// <summary>
/// 导出后台作业，轮询 Processing 状态任务并生成文件。
/// 每 5 秒轮询一次，每次处理 1 个任务。
/// </summary>
public sealed class ExportBackgroundService : BackgroundService
{
    private static readonly TimeSpan PollInterval = TimeSpan.FromSeconds(5);
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<ExportBackgroundService> _logger;

    public ExportBackgroundService(IServiceProvider serviceProvider, ILogger<ExportBackgroundService> logger)
    {
        _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("ExportBackgroundService 启动");
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = _serviceProvider.CreateScope();
                var taskRepository = scope.ServiceProvider.GetRequiredService<IExportTaskRepository>();
                var unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
                var fileGenerator = scope.ServiceProvider.GetRequiredService<ExportFileGenerator>();
                var fileStorage = scope.ServiceProvider.GetRequiredService<IFileStorageService>();
                var productAcl = scope.ServiceProvider.GetRequiredService<IProductAntiCorruptionService>();
                var orderAcl = scope.ServiceProvider.GetRequiredService<IOrderAntiCorruptionService>();

                var task = await taskRepository.GetOldestProcessingAsync(stoppingToken);
                if (task is not null)
                {
                    await ProcessTaskAsync(task, taskRepository, unitOfWork, fileGenerator, fileStorage, productAcl, orderAcl, stoppingToken);
                }
            }
            catch (Exception ex) when (!stoppingToken.IsCancellationRequested)
            {
                _logger.LogError(ex, "ExportBackgroundService 轮询异常");
            }

            try
            {
                await Task.Delay(PollInterval, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }
        _logger.LogInformation("ExportBackgroundService 停止");
    }

    private async Task ProcessTaskAsync(
        ExportTask task,
        IExportTaskRepository taskRepository,
        IUnitOfWork unitOfWork,
        ExportFileGenerator fileGenerator,
        IFileStorageService fileStorage,
        IProductAntiCorruptionService productAcl,
        IOrderAntiCorruptionService orderAcl,
        CancellationToken ct)
    {
        try
        {
            _logger.LogInformation("开始处理导出任务 TaskId={TaskId} Type={ReportType}", task.Id, task.ReportType);

            var (headers, rows) = task.ReportType switch
            {
                "SalesSummary" => await orderAcl.GetSalesSummaryAsync(task.ShopId, task.StartDate, task.EndDate, ct),
                "OrderDetail" => await orderAcl.GetOrderDetailForExportAsync(task.ShopId, task.StartDate, task.EndDate, ct),
                "ProductSales" => await productAcl.GetProductSalesAsync(task.ShopId, task.StartDate, task.EndDate, ct),
                _ => (new List<string>(), new List<IReadOnlyDictionary<string, object?>>())
            };

            var bytes = task.Format == "Excel"
                ? fileGenerator.GenerateExcel(task.ReportType, headers, rows)
                : fileGenerator.GenerateCsv(headers, rows);

            var ext = task.Format == "Excel" ? "xlsx" : "csv";
            var fileName = $"{task.Id}.{ext}";
            var contentType = task.Format == "Excel"
                ? "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet"
                : "text/csv";

            using var stream = new MemoryStream(bytes);
            var uploadResult = await fileStorage.UploadAsync(stream, fileName, contentType, "export", ct);

            task.MarkCompleted(rows.Count, uploadResult.Size, uploadResult.Url);
            await unitOfWork.SaveChangesAsync(ct);

            _logger.LogInformation("导出任务完成 TaskId={TaskId} Records={Count}", task.Id, rows.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "导出任务失败 TaskId={TaskId}", task.Id);
            task.MarkFailed(ex.Message);
            await unitOfWork.SaveChangesAsync(ct);
        }
    }
}
```

- [ ] **Step 5: 提交**

```bash
git add src/Services/SellerShop/Leno.SellerShop.Application/IExportAppService.cs src/Services/SellerShop/Leno.SellerShop.Application/Services/ExportAppService.cs src/Services/SellerShop/Leno.SellerShop.Infrastructure/Export/
git commit -m "feat(seller-shop): add ExportAppService + file generator + background service (BE-3)"
```

---

## Task 13: BE-3 扩展 ACL 接口 + gRPC proto + ExportController + DI 注册

**Files:**
- Modify: `src/Services/SellerShop/Leno.SellerShop.Application/Services/IOrderAntiCorruptionService.cs`
- Modify: `src/Services/SellerShop/Leno.SellerShop.Application/Services/IProductAntiCorruptionService.cs`
- Modify: `src/Contracts/Leno.SharedContracts/Grpc/Protos/order_internal.proto`
- Modify: `src/Contracts/Leno.SharedContracts/Grpc/Protos/product_internal.proto`
- Modify: `src/Services/SellerShop/Leno.SellerShop.Infrastructure/Services/Grpc/GrpcOrderAntiCorruptionClient.cs`
- Modify: `src/Services/SellerShop/Leno.SellerShop.Infrastructure/Services/Grpc/GrpcProductAntiCorruptionClient.cs`
- Create: `src/Services/SellerShop/Leno.SellerShop.Api/Controllers/ExportController.cs`
- Modify: `src/Services/SellerShop/Leno.SellerShop.Infrastructure/Dependencies/ServiceCollectionExtensions.cs`

- [ ] **Step 1: 扩展 ACL 接口签名**

修改 `src/Services/SellerShop/Leno.SellerShop.Application/Services/IOrderAntiCorruptionService.cs`，追加导出数据查询方法：

```csharp
    /// <summary>
    /// 查询销售汇总数据（用于导出），返回表头与数据行。
    /// </summary>
    Task<(List<string> Headers, List<IReadOnlyDictionary<string, object?>> Rows)> GetSalesSummaryAsync(
        Guid shopId, DateTime startDate, DateTime endDate, CancellationToken ct = default);

    /// <summary>
    /// 查询订单明细数据（用于导出），返回表头与数据行。
    /// </summary>
    Task<(List<string> Headers, List<IReadOnlyDictionary<string, object?>> Rows)> GetOrderDetailForExportAsync(
        Guid shopId, DateTime startDate, DateTime endDate, CancellationToken ct = default);
```

修改 `src/Services/SellerShop/Leno.SellerShop.Application/Services/IProductAntiCorruptionService.cs`，追加：

```csharp
    /// <summary>
    /// 查询商品销量数据（用于导出），返回表头与数据行。
    /// </summary>
    Task<(List<string> Headers, List<IReadOnlyDictionary<string, object?>> Rows)> GetProductSalesAsync(
        Guid shopId, DateTime startDate, DateTime endDate, CancellationToken ct = default);
```

- [ ] **Step 2: 扩展 gRPC proto**

在 `order_internal.proto` 的 `OrderInternalService` 追加（参考现有格式）：

```protobuf
  rpc GetSalesSummaryForExport(ExportDataRequest) returns (ExportDataResponse);
  rpc GetOrderDetailForExport(ExportDataRequest) returns (ExportDataResponse);

message ExportDataRequest {
  string shop_id = 1;
  string start_date = 2;
  string end_date = 3;
}

message ExportDataRow {
  map<string, string> values = 1;
}

message ExportDataResponse {
  repeated string headers = 1;
  repeated ExportDataRow rows = 2;
}
```

在 `product_internal.proto` 追加 `GetProductSalesForExport` rpc，消息复用上述 `ExportDataRequest`/`ExportDataRow`/`ExportDataResponse`（放共享 proto 或复制到 product proto）。

- [ ] **Step 3: 实现 ACL 客户端方法**

在 `GrpcOrderAntiCorruptionClient.cs` 与 `GrpcProductAntiCorruptionClient.cs` 中实现新方法，调用 gRPC 并将 `ExportDataRow` 的 map 映射为 `Dictionary<string, object?>`。参考已有 `GetSpuSellerIdAsync` 的 try/catch + fail-soft 模式（导出场景失败返回空表头与空行）。

- [ ] **Step 4: 创建 ExportController**

创建 `src/Services/SellerShop/Leno.SellerShop.Api/Controllers/ExportController.cs`：

```csharp
using Leno.Infrastructure.Auth;
using Leno.SellerShop.Application;
using Leno.SellerShop.Application.Dtos;
using Leno.SharedContracts.Responses;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Leno.SellerShop.Api.Controllers;

/// <summary>
/// 数据导出控制器，提供创建导出任务、查询任务列表、下载文件端点。
/// </summary>
[Authorize]
[ApiController]
[Route("api/seller/export")]
public sealed class ExportController : SellerShopControllerBase
{
    private readonly IExportAppService _exportAppService;
    private readonly IFileStorageService _fileStorageService;

    public ExportController(
        ICurrentUserContext currentUser,
        IExportAppService exportAppService,
        IFileStorageService fileStorageService)
        : base(currentUser)
    {
        _exportAppService = exportAppService ?? throw new ArgumentNullException(nameof(exportAppService));
        _fileStorageService = fileStorageService ?? throw new ArgumentNullException(nameof(fileStorageService));
    }

    /// <summary>创建导出任务（幂等）。</summary>
    [HttpPost("sales")]
    [ProducesResponseType(typeof(ApiResponse<ExportTaskDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> CreateTaskAsync([FromBody] CreateExportTaskDto dto, CancellationToken ct)
    {
        var sellerId = GetCurrentUserId();
        var task = await _exportAppService.CreateTaskAsync(sellerId, dto, ct);
        return Ok(ApiResponse.Success(task));
    }

    /// <summary>查询导出任务列表。</summary>
    [HttpGet("tasks")]
    [ProducesResponseType(typeof(ApiResponse<PageResult<ExportTaskDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> ListTasksAsync([FromQuery] ExportTaskQueryParams queryParams, CancellationToken ct)
    {
        var sellerId = GetCurrentUserId();
        var result = await _exportAppService.ListTasksAsync(sellerId, queryParams, ct);
        return Ok(ApiResponse.Success(result));
    }

    /// <summary>下载导出文件。</summary>
    [HttpGet("tasks/{id:guid}/download")]
    [ProducesResponseType(typeof(FileStreamResult), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DownloadAsync(Guid id, CancellationToken ct)
    {
        var sellerId = GetCurrentUserId();
        var download = await _exportAppService.GetDownloadAsync(sellerId, id, ct);
        if (download is null)
        {
            return NotFound(ApiResponse.Fail(StatusCodes.Status404NotFound, "文件不存在或任务未完成"));
        }

        var stream = await _fileStorageService.DownloadAsync(download.Value.FilePath, ct);
        return File(stream, download.Value.ContentType, download.Value.FileName);
    }
}
```

- [ ] **Step 5: DI 注册**

修改 `src/Services/SellerShop/Leno.SellerShop.Infrastructure/Dependencies/ServiceCollectionExtensions.cs`，在 `services.AddHostedService<QualificationExpiryReminder>();` 后追加：

```csharp
        // BE-3 导出功能
        services.AddScoped<IExportTaskRepository, ExportTaskRepository>();
        services.AddScoped<IExportAppService, ExportAppService>();
        services.AddSingleton<ExportFileGenerator>();
        services.AddHostedService<ExportBackgroundService>();
```

并在文件顶部追加 `using Leno.SellerShop.Infrastructure.Export;` 与 `using Leno.SellerShop.Application.Dtos;`（若需要）。

- [ ] **Step 6: 编译验证**

Run (cwd: `/workspace`): `dotnet build src/Services/SellerShop/Leno.SellerShop.Api/Leno.SellerShop.Api.csproj`
Expected: BUILD SUCCEEDED

- [ ] **Step 7: 提交**

```bash
git add src/Services/SellerShop/ src/Contracts/
git commit -m "feat(seller-shop): add ExportController + ACL export queries + DI (BE-3)"
```

---

## Task 14: BE-3 前端清理 BE-3 标记 + mock handler 真实化

**Files:**
- Modify: `web/seller/src/modules/09-export/api/export.api.ts`
- Modify: `web/seller/src/modules/09-export/views/SalesExport.vue`
- Modify: `web/seller/src/shared/http/mock/handlers/export.ts`

- [ ] **Step 1: 清理 export.api.ts BE-3 注释**

修改 `web/seller/src/modules/09-export/api/export.api.ts`，删除文件中所有 `BE-3 待后端实现`/`BE-3 就绪后`/`mock 返回空列表占位` 注释，保留方法实现不变。

- [ ] **Step 2: 清理 SalesExport.vue BE-3 提示**

修改 `web/seller/src/modules/09-export/views/SalesExport.vue`：

1. 删除第 43-48 行的 BE-3 注释块，替换为：
```typescript
/**
 * 销售报表导出页
 *
 * 路由 /export/sales，权限 export:sales
 * 3 个 API 端点已就绪：创建任务、查询列表、下载文件。
 * 轮询：有 Processing 状态任务时每 3 秒刷新列表。
 */
```

2. `onSubmit` 的 catch 块（第 171-176 行）替换为：
```typescript
  } catch (e) {
    logger.error('创建导出任务失败', e)
    message.error('创建导出任务失败')
  } finally {
```

3. `onDownload` 的 catch 块（第 196-199 行）替换为：
```typescript
  } catch (e) {
    logger.error('下载导出文件失败', e)
    message.error('下载导出文件失败')
  }
```

4. `onRetry` 的 catch 块（第 213-217 行）替换为：
```typescript
  } catch (e) {
    logger.error('重试导出任务失败', e)
    message.error('重试导出任务失败')
  } finally {
```

5. 删除模板中的 `<div class="sales-export-be3-tip">...</div>`（第 277-279 行）及其样式（第 391-400 行）。

- [ ] **Step 3: 真实化 mock handler**

将 `web/seller/src/shared/http/mock/handlers/export.ts` 完整替换为：

```typescript
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
```

- [ ] **Step 4: 运行前端验证**

Run (cwd: `web/seller`): `pnpm typecheck && pnpm lint`
Expected: 0 errors

- [ ] **Step 5: 提交**

```bash
git add web/seller/src/modules/09-export/ web/seller/src/shared/http/mock/handlers/export.ts
git commit -m "feat(export-fe): activate real export API, remove BE-3 markers (BE-3)"
```

---

## Task 15: 全量验证 + 提交推送

- [ ] **Step 1: 后端编译**

Run (cwd: `/workspace`): `dotnet build`
Expected: BUILD SUCCEEDED（0 errors）

- [ ] **Step 2: 后端测试**

Run (cwd: `/workspace`): `dotnet test`
Expected: ALL PASS

- [ ] **Step 3: 前端验证**

Run (cwd: `web/seller`): `pnpm lint && pnpm typecheck && pnpm test && pnpm build`
Expected: 全部通过

- [ ] **Step 4: 推送**

```bash
git push origin dev
```

- [ ] **Step 5: 完成开发分支**

按 superpowers:finishing-a-development-branch 技能流程完成收尾。
```

现在让我对计划进行自查。<tool_call>TodoWrite<arg_key>merge</arg_key><arg_value>true