# 卖家后台缺失后端 API 实现设计（BE-1/2/3/4）

**日期**：2026-07-31
**状态**：已批准（brainstorming 完成，待进入 writing-plans）
**方案**：方案 B — 统一重构 + 单 spec 单 plan

---

## 1. 背景与目标

seller 前端在 P0/P1 阶段交付中，对 4 个后端缺失或不一致的 API 端点埋设了 BE-1/2/3/4 标记，采用"仅 UI + 标记"或 mock 兜底策略。本次设计补齐这些后端 API 并清理前端 BE 标记。

| BE | 问题 | 归属服务 | 复杂度 |
|---|---|---|---|
| BE-1 | Order BC 分页 page 从 0 起，字段名 `TotalCount/PageIndex` 与共享 `PageResult<T>` 的 `Total/Page` 不一致 | Order | 低（机械重构） |
| BE-2 | 低库存告警端点缺失，前端走本地 mock | SellerShop（端点）+ Product（数据源） | 中（跨 BC） |
| BE-3 | 数据导出功能完全缺失，前端 mock 返回 501 | SellerShop | 高（完整新模块） |
| BE-4 | 通知端点后端已就绪，前端误判未就绪未接入 | Notification（已就绪）+ 前端 | 低（前端接入） |

**目标**：实现 BE-1/2/3 后端 API + BE-4 前端接入，清理全部 BE 标记，全量测试通过。

---

## 2. 共享契约基线

影响所有 BE 的分页与响应规范：

- `Leno.SharedKernel/ValueObjects/PageRequest.cs`：`Page` 默认 1（1 起），构造函数强制 `Page = page < 1 ? 1 : page`，`Skip = (Page - 1) * PageSize`
- `Leno.SharedContracts/Responses/PageResult.cs`：`Items`/`Total`/`Page`/`PageSize`/`TotalPages`/`HasNext`，`Page` 默认 1
- `Leno.SharedContracts/Responses/ApiResponse.cs`：`Code`/`Message`/`Data`/`TraceId`，工厂 `ApiResponse.Success(data)` / `ApiResponse.Fail(code, msg)`
- 前端 axios baseURL=`/api`，响应拦截器自动解包 `ApiResponse.data`，前端拿到裸业务负载

---

## 3. 架构总览与 BE 归属

```
┌─────────────────────────────────────────────────────────┐
│ 前端 (web/seller)                                         │
│  - BE-1: order.api.ts 默认值 0→1, 移除 +1/-1 适配          │
│  - BE-2: LowStockAlert.vue 移除 mock, 接入真实 API        │
│  - BE-3: SalesExport.vue 移除 BE-3 提示, 激活下载逻辑     │
│  - BE-4: 新建 notification.api.ts, 改造 Notifications.vue  │
├─────────────────────────────────────────────────────────┤
│ API 层 (Controllers)                                      │
│  - Order: OrdersController 3 端点 page 默认值 0→1          │
│  - Product: InternalProductsController 新增 low-stock     │
│  - SellerShop: SellerDashboardController 新增 low-stock   │
│  - SellerShop: 新建 ExportController (3 端点)             │
│  - Notification: 无改动 (已就绪)                           │
├─────────────────────────────────────────────────────────┤
│ Application 层                                            │
│  - Order: OrderListQuery/Result 迁移到 PageRequest/Result  │
│  - Product: IInventoryAppService + 查询方法               │
│  - SellerShop: IExportAppService + ILowStockQueryService  │
│  - SellerShop: IProductAntiCorruptionService 扩展 low-stock│
├─────────────────────────────────────────────────────────┤
│ Domain 层                                                 │
│  - SellerShop: ExportTask 聚合 + IExportTaskRepository    │
│  - Product: IStockBaselineRepository 新增 GetLowStockAsync │
├─────────────────────────────────────────────────────────┤
│ Infrastructure 层                                         │
│  - SellerShop: ExportTaskRepository + EF Core 映射        │
│  - SellerShop: ExportBackgroundService (HostedService)    │
│  - SellerShop: IFileStorageService 集成 (已存在)           │
│  - Product: StockBaselineRepository 实现 GetLowStockAsync │
└─────────────────────────────────────────────────────────┘
```

**实施顺序**（按依赖与风险递增）：BE-4 → BE-1 → BE-2 → BE-3

---

## 4. BE-1 Order 分页契约统一

### 4.1 问题现状

Order BC 三处不一致：
1. **默认值**：`OrdersController` 三个端点 `int page = 0`（0 起），共享 `PageRequest.Page` 默认 1（1 起）
2. **字段名**：`OrderListResult` 用 `TotalCount/PageIndex`，共享 `PageResult<T>` 用 `Total/Page/TotalPages/HasNext`
3. **类型**：`OrderListQuery`/`OrderListResult` 自定义类型，未复用 `PageRequest`/`PageResult<T>`

涉及端点：
- `GET /api/orders`（Buyer）
- `GET /api/seller/orders`（Seller）
- `GET /api/admin/orders`（Operator/Admin）

### 4.2 改动范围

**Order.Application 层**（核心重构）：
- `Queries/OrderListQuery.cs`：删除 `PageIndex` 字段，改为继承 `PageRequest`（已有 `Page`/`PageSize`/`Skip`），保留业务字段 `SellerId`/`Status`/`Keyword`/`DateFrom`/`DateTo`
- `Queries/OrderListResult.cs`：删除整个自定义类型，改为 `PageResult<OrderSummaryDto>`（共享类型已含 `Items`/`Total`/`Page`/`PageSize`/`TotalPages`/`HasNext`）
- `Queries/OrderListQueryHandler.cs`：`Skip` 计算从 `PageIndex * PageSize` 改为 `(Page - 1) * PageSize`（即直接用 `PageRequest.Skip`），返回类型改为 `PageResult<OrderSummaryDto>`

**Order.Api 层**：
- `Controllers/OrdersController.cs`：三处 `int page = 0` → `int page = 1`；`new OrderListQuery { PageIndex = page }` → `new OrderListQuery { Page = page, PageSize = pageSize }`

**Order.Infrastructure 层**（ReadModel 适配）：
- `IOrderReadModelAccessor.ListAsync`：签名从 `OrderListResult` 改为 `PageResult<OrderSummaryDto>`；ES 查询 `From` 计算从 `pageIndex * pageSize` 改为 `(page - 1) * pageSize`
- ReadModel 返回需补 `TotalPages = (int)Math.Ceiling(total / (double)pageSize)` 与 `HasNext = page < totalPages`

**测试**：
- `SellerOrdersApiTests.cs` / `OrderApiTests.cs`：`PageIndex.Should().Be(0)` → `Page.Should().Be(1)`；`TotalCount` 断言改为 `Total`

**前端**：
- `order.api.ts`：`page = 0` 默认值 → `page = 1`，移除 `// TODO BE-1` 注释
- `order.api.spec.ts`：`list 默认 page=0` 测试 → `page=1`
- `PendingShipment.vue` / `OrderList.vue`：移除所有 `// BE-1` 注释与 `+1`/`-1` 适配代码（`a-table` 的 `current` 与后端 `page` 现在都是 1 起，无需转换）

### 4.3 数据流

```
前端 (page=1, pageSize=20)
  ↓
OrdersController (page=1 默认, 透传)
  ↓
OrderListQuery : PageRequest (Page=1, PageSize=20, Skip=0)
  ↓
OrderListQueryHandler → IOrderReadModelAccessor.ListAsync
  ↓ (From = PageRequest.Skip = 0)
ES/DB 查询
  ↓
PageResult<OrderSummaryDto> (Page=1, Total=150, TotalPages=8, HasNext=true)
  ↓
ApiResponse<PageResult<...>> → 前端
```

### 4.4 兼容性处理

- **无破坏性 API 变更**：`page` 参数仍是 int，默认值从 0→1。若旧客户端传 page=0，`PageRequest` 构造函数会归一化为 1（`page < 1 ? 1 : page`），不会报错
- **响应字段变更**：`TotalCount→Total`、`PageIndex→Page` 是破坏性的，但当前仅 seller 前端消费这些端点，前端同步改动即可。无其他 BC 通过 ACL 消费订单列表

---

## 5. BE-2 低库存告警

### 5.1 问题现状

Product 域有库存数据（`IStockBaselineRepository`），但仅暴露 `ReplenishAsync` 补货接口，无低库存查询。卖家工作台需要"低库存 SKU 列表"告警，前端目前走本地 mock。

前端期望端点：`GET /api/seller/dashboard/low-stock?threshold={int}&page={int}&pageSize={int}`

### 5.2 改动范围

**Product 域（数据源）**：

Domain 层：
- `IStockBaselineRepository`：新增 `GetLowStockAsync(Guid shopId, int threshold, int page, int pageSize, CancellationToken ct)` 返回 `PageResult<LowStockSkuDto>`
- `LowStockSkuDto`：`SkuId`/`ProductId`/`ProductName`/`SkuName`/`Stock`/`Threshold`/`ShopId`

Application 层：
- `IInventoryAppService`：新增 `GetLowStockSkusAsync(Guid shopId, int threshold, int page, int pageSize, CancellationToken ct)`
- `InventoryAppService`：实现，委托 `IStockBaselineRepository.GetLowStockAsync`

Infrastructure 层：
- `StockBaselineRepository`：实现 `GetLowStockAsync`，EF Core 查询 `WHERE ShopId = @shopId AND Stock < @threshold` 分页

Api 层：
- `InternalProductsController`：新增 `GET internal/v1/products/low-stock?shopId={guid}&threshold={int}&page={int}&pageSize={int}`（仅内部调用，受 `InternalApiKeyMiddleware` 保护）

**SellerShop 域（端点 + ACL）**：

Application 层：
- `IProductAntiCorruptionService`（ACL 端口，已存在）：新增 `GetLowStockSkusAsync(Guid shopId, int threshold, int page, int pageSize, CancellationToken ct)` 返回 `List<LowStockItemDto>`
- `ProductAntiCorruptionService`（ACL 实现）：HTTP 调 Product 域 `internal/v1/products/low-stock`，映射 DTO
- `ISellerDashboardAppService`：新增 `GetLowStockAlertAsync(int threshold, int page, int pageSize, CancellationToken ct)`

Api 层：
- `SellerDashboardController`：新增 `[HttpGet("dashboard/low-stock")]`，参数 `threshold`（默认 10）/`page`（默认 1）/`pageSize`（默认 20），返回 `ApiResponse<PageResult<LowStockItemDto>>`

**前端**：
- `dashboard.dto.ts`：`LowStockItemDto` 已定义，保持不变
- `dashboard.api.ts`：新增 `getLowStock(threshold, page, pageSize)` 调 `GET /api/seller/dashboard/low-stock`
- `LowStockAlert.vue`：移除本地 mock 数据与 `BE-2` 标记，改为调 `dashboardApi.getLowStock`，阈值 `InputNumber` 变更时重新请求后端

### 5.3 数据流

```
前端 LowStockAlert.vue (threshold=10, page=1)
  ↓
GET /api/seller/dashboard/low-stock?threshold=10&page=1&pageSize=20
  ↓
SellerDashboardController → ISellerDashboardAppService.GetLowStockAlertAsync
  ↓
IProductAntiCorruptionService.GetLowStockSkusAsync (ACL)
  ↓ HTTP
GET internal/v1/products/low-stock?shopId=xxx&threshold=10&page=1&pageSize=20
  ↓
InternalProductsController → IInventoryAppService.GetLowStockSkusAsync
  ↓
IStockBaselineRepository.GetLowStockAsync
  ↓ EF Core
SELECT * FROM StockBaselines WHERE ShopId=@shopId AND Stock < @threshold
  ↓
PageResult<LowStockSkuDto> → ACL 映射 → PageResult<LowStockItemDto>
  ↓
ApiResponse<PageResult<...>> → 前端
```

### 5.4 ACL 降级策略

当 Product 域不可用时，`ProductAntiCorruptionService` 返回空 `PageResult`（`Items=空, Total=0`），而非抛异常。前端显示"暂无低库存商品"空状态，避免工作台因依赖服务故障而白屏。这与项目已有的 ACL 降级模式一致（`SellerShopAntiCorruptionTests.cs` 已验证此模式）。

---

## 6. BE-3 数据导出

### 6.1 问题现状

后端完全无导出功能。前端已实现完整 UX（左右两栏、3 秒轮询 Processing→Completed、Blob 下载），但 mock 返回 501。

前端期望 3 个端点：
- `POST /api/seller/export/sales` — 创建导出任务（幂等），返回 `ExportTaskDto`
- `GET /api/seller/export/tasks` — 查询任务列表，返回 `PageResult<ExportTaskDto>`
- `GET /api/seller/export/tasks/{id}/download` — 下载文件，返回 Blob

### 6.2 改动范围

**SellerShop.Domain 层**（新建聚合）：
- `ExportTask` 聚合根：`Id`/`ShopId`/`ReportType`/`StartDate`/`EndDate`/`Format`/`Status`(Processing/Completed/Failed)/`RecordCount`/`FileSize`/`FilePath`/`ErrorMessage`/`CreatedAt`/`CompletedAt`
- 状态机方法：`MarkCompleted(recordCount, fileSize, filePath)` / `MarkFailed(errorMessage)`
- `IExportTaskRepository`：`AddAsync` / `GetByIdAsync` / `ListByShopAsync(shopId, status, page, pageSize)`

**SellerShop.Application 层**（新建服务）：
- `IExportAppService`：
  - `CreateTaskAsync(CreateExportTaskDto, ct)` → `ExportTaskDto`（幂等，创建后入队）
  - `ListTasksAsync(shopId, status, page, pageSize, ct)` → `PageResult<ExportTaskDto>`
  - `GetDownloadAsync(taskId, ct)` → 返回文件路径（Controller 负责读取 stream）
- `ExportAppService`：实现上述接口
- `DTOs/ExportDtos.cs`：`CreateExportTaskDto` / `ExportTaskDto` / `ExportTaskQueryParams`

**SellerShop.Infrastructure 层**（新建持久化 + 后台作业）：
- `ExportTaskRepository`：EF Core 实现 `IExportTaskRepository`
- `SellerShopDbContext`：新增 `DbSet<ExportTask>` + 配置映射
- EF Core 迁移：新增 `ExportTasks` 表
- `ExportBackgroundService`（`BackgroundService`）：
  - 轮询 `IExportTaskRepository` 获取 `Status=Processing` 的任务
  - 根据 `ReportType` 经 ACL 聚合数据（Order 域 SalesSummary/OrderDetail，Product 域 ProductSales）
  - 生成 Excel/CSV（用 `ClosedXML` NuGet 包生成 .xlsx，CSV 手动拼接）
  - 落盘 `IFileStorageService.SaveAsync`，更新 `ExportTask.MarkCompleted`
  - 异常时 `ExportTask.MarkFailed`

**SellerShop.Api 层**（新建 Controller）：
- `ExportController`（`[Authorize] [Route("api/seller/export")]`）：
  - `POST /sales` — 创建导出任务（幂等 `withIdempotency()`），返回 `ExportTaskDto`
  - `GET /tasks` — 查询任务列表（`status`/`page`/`pageSize`），返回 `PageResult<ExportTaskDto>`
  - `GET /tasks/{id}/download` — 下载文件，返回 `FileStreamResult`（`Content-Type` 按 Format 设 `application/vnd.openxmlformats-officedocument.spreadsheetml.sheet` 或 `text/csv`）

**ACL 数据聚合**：
- `IOrderAntiCorruptionService`（已存在）：新增 `GetSalesSummaryAsync(shopId, startDate, endDate, ct)` / `GetOrderDetailAsync(shopId, startDate, endDate, ct)`
- `IProductAntiCorruptionService`：新增 `GetProductSalesAsync(shopId, startDate, endDate, ct)`
- Order/Product 域各新增 `internal/v1/orders/export/sales-summary` 等 3 个内部端点

**前端**：
- `SalesExport.vue`：移除 `BE-3` Alert 与 `message.warning('后端接口未就绪（BE-3）')`，激活 `createTask` 成功后的列表刷新、`download` 的 Blob 下载、轮询逻辑（已有代码，解除 BE-3 分支即可）
- `export.api.ts`：移除 `// BE-3 待后端实现` 注释
- mock handler `handlers/export.ts`：移除 501 桩，替换为真实 mock 数据（用于前端测试）

### 6.3 数据流

```
前端 POST /api/seller/export/sales (CreateExportTaskDto + 幂等key)
  ↓
ExportController.CreateTask → ExportAppService.CreateTaskAsync
  ↓
ExportTask(status=Processing) → IExportTaskRepository.AddAsync → DB
  ↓ 入队 (IExportTaskQueue)
ExportBackgroundService 轮询取出
  ↓
根据 ReportType:
  - SalesSummary → IOrderAntiCorruptionService.GetSalesSummaryAsync
  - OrderDetail → IOrderAntiCorruptionService.GetOrderDetailAsync
  - ProductSales → IProductAntiCorruptionService.GetProductSalesAsync
  ↓
生成 Excel(ClosedXML) / CSV → IFileStorageService.SaveAsync
  ↓
ExportTask.MarkCompleted(recordCount, fileSize, filePath) → DB
  ↓
前端轮询 GET /api/seller/export/tasks → 返回 status=Completed + downloadUrl
  ↓
前端 GET /api/seller/export/tasks/{id}/download → FileStreamResult → Blob 下载
```

### 6.4 幂等处理

`CreateTaskAsync` 检查幂等 key（前端通过 `withIdempotency()` 传入 `X-Idempotency-Key` header）。若 key 已存在，返回已创建的 `ExportTask`，避免重复入队。复用已有的 `IIdempotencyStore`（`Leno.Infrastructure.Abstractions`）。

### 6.5 文件存储

- 路径规则：`exports/{shopId}/{taskId}.{xlsx|csv}`
- 过期清理：文件保留 7 天，`ExportBackgroundService` 兼任清理过期 Completed 任务（可选，初版不做自动清理，手动运维）
- `IFileStorageService` 已存在，直接复用

### 6.6 ClosedXML 依赖

新增 `ClosedXML` NuGet 包到 `Leno.SellerShop.Infrastructure.csproj`。选择 ClosedXML 而非 EPPlus，因为 ClosedXML 是 MIT 许可（EPPlus 商用需付费许可，polyfill 非嵌入式不涉及商用但保持一致）。

### 6.7 90 天约束

后端 `CreateExportTaskDto` 校验 `endDate - startDate <= 90 天`，超出返回 `ApiResponse.Fail("EXPORT_RANGE_EXCEEDED", "时间范围不能超过 90 天")`。前端已有相同校验，后端做防御性二次校验。

---

## 7. BE-4 通知前端接入

### 7.1 问题现状

后端 Notification 服务 4 个端点已全部就绪（`page=1` 起），但前端误判未就绪，采用"仅 UI + BE-4 标记"策略，不调 API 展示空列表。

### 7.2 后端现状（无需改动）

| 端点 | 方法 | 路由 | 状态 |
|---|---|---|---|
| 通知列表 | GET | `/api/notifications?isRead=&page=1&pageSize=20` | ✅ 已就绪 |
| 未读计数 | GET | `/api/notifications/unread-count` | ✅ 已就绪 |
| 批量标记已读 | POST | `/api/notifications/read` body=`{RecordIds}` | ✅ 已就绪 |
| 全部标记已读 | POST | `/api/notifications/read-all` | ✅ 已就绪 |

后端返回 `NotificationListResultDto`：`Items`/`Total`/`UnreadCount`/`Page`/`PageSize`（注意比 `PageResult<T>` 多了 `UnreadCount` 字段）。

### 7.3 改动范围（纯前端）

**新建文件**：
- `08-account/types/notification.dto.ts`：对齐后端 DTO
  - `NotificationRecordDto`：`recordId`/`userId`/`templateCode`/`channel`/`title`/`content`/`status`/`isRead`/`sentAt`/`createdAt`
  - `NotificationListResultDto`：`items`/`total`/`unreadCount`/`page`/`pageSize`
  - `MarkAsReadDto`：`{ recordIds: string[] }`
- `08-account/api/notification.api.ts`：封装 4 个端点
  - `list(params: { isRead?: boolean; page?: number; pageSize?: number })`
  - `getUnreadCount()`
  - `markAsRead(recordIds: string[])`
  - `markAllAsRead()`

**改造文件**：
- `Notifications.vue`：
  - 移除 `BE-4` Alert 与 `message.warning('后端通知接口未就绪（BE-4）')`
  - 移除本地空数组，改用 `notificationApi.list()` 加载
  - Tab 切换映射：全部→不传 `isRead`，未读→`isRead=false`，已读→`isRead=true`
  - `onMarkAllRead` 改调 `notificationApi.markAllAsRead()`
  - 单条标记已读：调 `notificationApi.markAsRead([recordId])`
  - 顶部徽标显示 `unreadCount`（从列表响应或独立端点获取）
  - 分页：`a-list` 分页，`page` 从 1 起

**mock 层**：
- 新建 `handlers/notification.ts`：mock 5 条通知数据（2 未读/3 已读），拦截 4 个端点
- `mock/data/seed.ts`：`MockSeed` 新增 `notifications` 字段
- `mock/index.ts`：注册 notification handler

### 7.4 数据流

```
前端 Notifications.vue
  ↓ onMounted + tab 切换
GET /api/notifications?isRead=false&page=1&pageSize=20
  ↓
NotificationListResultDto { items, total, unreadCount, page, pageSize }
  ↓ 渲染列表 + 未读徽标

用户点"全部标记已读"
  ↓
POST /api/notifications/read-all
  ↓ 成功
刷新列表 + 更新 unreadCount=0
```

---

## 8. 错误处理与验证

### 8.1 错误处理

- **ACL 降级**：BE-2/BE-3 跨 BC 调用失败时返回空结果而非抛异常，避免工作台白屏
- **导出失败**：`ExportBackgroundService` 捕获异常后 `ExportTask.MarkFailed`，前端轮询看到 `Failed` 状态展示错误信息
- **幂等冲突**：重复创建导出任务返回已存在的 `ExportTask`
- **参数校验**：BE-3 时间范围 90 天约束，后端防御性校验

### 8.2 测试策略

- **后端单元测试**：每个新 AppService / Repository / Controller 方法有对应测试
- **后端集成测试**：跨 BC ACL 调用链路（SellerShop → Product internal endpoint）
- **前端组件测试**：API 客户端测试 + 页面渲染测试
- **全量验证**：`dotnet test` + `pnpm lint` + `pnpm typecheck` + `pnpm test` + `pnpm build`

---

## 9. 实施顺序

按依赖关系与风险递增：

1. **BE-4**（前端接入，零后端改动）— 快速清债，验证前端接入模式
2. **BE-1**（机械重构，低风险）— Order 分页统一，机械改动 + 测试更新
3. **BE-2**（跨 BC 新增，中风险）— Product 数据源 + SellerShop 端点 + ACL
4. **BE-3**（完整新模块，高风险）— ExportTask 聚合 + BackgroundService + 跨 BC 聚合

---

## 10. 不包含的范围（YAGNI）

- **ExportBackgroundService 自动清理过期文件**：初版不做，手动运维
- **BE-4 后端端点微调**（补 `TotalPages`/`HasNext`）：后端 `NotificationListResultDto` 当前只有 `Total`，前端可正常分页，不强制对齐 `PageResult<T>`
- **导出任务取消**：前端未实现取消按钮，后端不实现 `Cancel` 端点
- **导出任务重试**：前端有"重试"按钮但后端初版不实现，保持 501 mock（标记为 BE-3.1 后续）
- **Order BC ReadModel 迁移到 ES**：本次仅改字段名与默认值，不重构 ReadModel 存储

---

## 11. 关键文件清单

**Order（BE-1）**：
- `src/Services/Order/Leno.Order.Api/Controllers/OrdersController.cs`
- `src/Services/Order/Leno.Order.Application/Queries/OrderListQuery.cs`
- `src/Services/Order/Leno.Order.Application/Queries/OrderListResult.cs`（删除）
- `src/Services/Order/Leno.Order.Application/Queries/OrderListQueryHandler.cs`
- `src/Services/Order/Leno.Order.Infrastructure/IOrderReadModelAccessor.cs`
- `src/Services/Order/Leno.Order.Api.Tests/SellerOrdersApiTests.cs`
- `src/Services/Order/Leno.Order.Api.Tests/OrderApiTests.cs`
- `web/seller/src/modules/05-order-fulfillment/api/order.api.ts`
- `web/seller/src/modules/05-order-fulfillment/api/order.api.spec.ts`
- `web/seller/src/modules/05-order-fulfillment/views/PendingShipment.vue`
- `web/seller/src/modules/05-order-fulfillment/views/OrderList.vue`

**Product / SellerShop（BE-2）**：
- `src/Services/Product/Leno.Product.Domain/IStockBaselineRepository.cs`
- `src/Services/Product/Leno.Product.Application/IInventoryAppService.cs`
- `src/Services/Product/Leno.Product.Application/Services/InventoryAppService.cs`
- `src/Services/Product/Leno.Product.Infrastructure/StockBaselineRepository.cs`
- `src/Services/Product/Leno.Product.Api/Controllers/InternalProductsController.cs`
- `src/Services/SellerShop/Leno.SellerShop.Application/IProductAntiCorruptionService.cs`
- `src/Services/SellerShop/Leno.SellerShop.Application/ISellerDashboardAppService.cs`
- `src/Services/SellerShop/Leno.SellerShop.Api/Controllers/SellerDashboardController.cs`
- `web/seller/src/modules/02-dashboard/views/LowStockAlert.vue`
- `web/seller/src/modules/02-dashboard/api/dashboard.api.ts`

**SellerShop（BE-3）**：
- `src/Services/SellerShop/Leno.SellerShop.Domain/ExportTask.cs`（新建）
- `src/Services/SellerShop/Leno.SellerShop.Domain/IExportTaskRepository.cs`（新建）
- `src/Services/SellerShop/Leno.SellerShop.Application/IExportAppService.cs`（新建）
- `src/Services/SellerShop/Leno.SellerShop.Application/Services/ExportAppService.cs`（新建）
- `src/Services/SellerShop/Leno.SellerShop.Application/DTOs/ExportDtos.cs`（新建）
- `src/Services/SellerShop/Leno.SellerShop.Infrastructure/ExportTaskRepository.cs`（新建）
- `src/Services/SellerShop/Leno.SellerShop.Infrastructure/ExportBackgroundService.cs`（新建）
- `src/Services/SellerShop/Leno.SellerShop.Api/Controllers/ExportController.cs`（新建）
- `src/Services/SellerShop/Leno.SellerShop.Infrastructure/SellerShopDbContext.cs`（修改）
- `web/seller/src/modules/09-export/views/SalesExport.vue`
- `web/seller/src/modules/09-export/api/export.api.ts`
- `web/seller/src/shared/http/mock/handlers/export.ts`

**Notification（BE-4，纯前端）**：
- `web/seller/src/modules/08-account/types/notification.dto.ts`（新建）
- `web/seller/src/modules/08-account/api/notification.api.ts`（新建）
- `web/seller/src/modules/08-account/views/Notifications.vue`（改造）
- `web/seller/src/shared/http/mock/handlers/notification.ts`（新建）
- `web/seller/src/shared/http/mock/data/seed.ts`（修改）
- `web/seller/src/shared/http/mock/index.ts`（修改）

**共享契约**：
- `src/BuildingBlocks/Leno.SharedKernel/ValueObjects/PageRequest.cs`（参考，不修改）
- `src/BuildingBlocks/Leno.SharedContracts/Responses/PageResult.cs`（参考，不修改）
- `src/BuildingBlocks/Leno.SharedContracts/Responses/ApiResponse.cs`（参考，不修改）
