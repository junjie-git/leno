---
title: M4 gRPC 双轨方案剩余任务补齐设计
status: accepted
date: 2026-07-19
related_specs:
  - docs/superpowers/specs/2026-07-17-comprehensive-optimization-v2-design.md
  - docs/superpowers/specs/2026-07-19-m4-grpc-dual-track-design.md
related_plans:
  - docs/superpowers/plans/2026-07-19-m4-grpc-dual-track-implementation.md
  - docs/superpowers/plans/2026-07-17-slow-track-m4-communication-upgrade.md
---

# M4 gRPC 双轨方案剩余任务补齐设计

## 1. 背景与目标

### 1.1 背景

M4 gRPC 双轨方案（`2026-07-19-m4-grpc-dual-track-implementation.md`）已完成阶段 0-5 的代码实施，落地 6 个 gRPC 服务端 + 7 个防腐层双轨 + 7 个 DispatcherAdapter + 48 个单元测试。但实施过程中发现 3 类遗留问题：

1. **必做技术债务**：`ConsulConfigWatcher` 类已实现但未注册为 HostedService；Cart `ProductSnapshotAntiCorruptionService` 未双轨化且未继承 `AntiCorruptionBase`。
2. **3 个 BC gRPC 服务端未补全**：spec §4.7 列出 9 个 BC.Api 需实现 GrpcService，实际仅完成 6 个（Product/Promotion/PointsMembership/UserAuth/Order/Payment），Cart/SellerShop/ReviewAfterSales 缺失。
3. **既有 spec 未归档 + ADR 缺失**：3 份旧 spec 未标注 supersede 关系；M4 实施过程的关键决策未以 ADR 形式记录。

### 1.2 目标

本 spec 补齐上述 3 类遗留任务，使 M4 gRPC 双轨方案达到完整可验收状态：

- 工作流 A：必做技术债务修复（开发环境可完成）
- 工作流 B：3 个 BC gRPC 服务端补全（spec §4.7 完整覆盖）
- 工作流 C：既有 spec supersede 标注 + ADR 关键决策记录
- 工作流 D：Guid→string 迁移（生产化，采用新增 string 字段策略保持 .proto 向后兼容）

### 1.3 非目标

- **不实施运维观察期**：4 周稳定运行观察需运维团队执行，已记录在 `docs/runbooks/m4-grpc-poc-verification.md` 第 7 节，本 spec 不涉及。
- **不实施 Task 11 下线 HttpClient**：HttpClient 永久保留作为 fallback（硬约束）。
- **不实施 F1.4 越权校验集中化**：`SellerGrpcService.ValidateSellerOwnership` 抛 `Unimplemented`，F1.4 是独立任务。
- **不实施集成测试 + E2E 测试**：本 spec 仅覆盖单元测试，集成/E2E 测试待运维观察期阶段补充。

### 1.4 实施模式

采用 Subagent-Driven 实施模式：每个 Task 派发独立 subagent，主 agent 负责 review 与协调，subagent 仅 commit 不 push，由主 agent 统一推送。每个 Task 遵循 TDD 步骤（写测试→验证失败→实现→验证通过→提交）。

## 2. 整体架构与任务分组

### 2.1 工作流划分

本次实施分 4 个工作流，共 11 个 Task，按依赖链顺序执行：

#### 工作流 A：必做技术债务

| Task | 内容 | 影响范围 |
|---|---|---|
| **Task A1** | `ConsulConfigWatcher` 注册为 HostedService | 4 个调用方 BC（Order/Notification/Cart/ReviewAfterSales） |
| **Task A2** | Cart `ProductSnapshotAntiCorruptionService` 双轨化 | Cart.Infrastructure + Cart.Application（接口签名变更） |

#### 工作流 B：3 个 BC gRPC 服务端补全（spec §4.7）

| Task | 内容 | 新建文件 |
|---|---|---|
| **Task B1** | Cart.Api/CartGrpcService | `ICartInternalQueryService` + `CartGrpcService` + 单测 |
| **Task B2** | SellerShop.Api/SellerGrpcService | `ISellerInternalQueryService` + `SellerGrpcService` + 单测 |
| **Task B3** | ReviewAfterSales.Api/ReviewGrpcService | `IReviewInternalQueryService` + `ReviewGrpcService` + 单测 |

#### 工作流 C：spec 整合归档 + ADR

| Task | 内容 | 影响范围 |
|---|---|---|
| **Task C1** | 既有 3 份 spec supersede 标注 | 3 个 spec 文档头部 |
| **Task C2** | ADR 关键决策记录 | `docs/decisions/` 新建 7 个 ADR 文件 |

#### 工作流 D：Guid→string 迁移（生产化）

| Task | 内容 | 影响范围 |
|---|---|---|
| **Task D1** | 6 个 .proto 文件新增 string 字段 + buf generate | product/order/promotion/cart/seller/review.proto |
| **Task D2** | 9 个 GrpcService 更新 DTO→proto 映射（双写 int64 + string） | 9 个 BC.Api GrpcServices |
| **Task D3** | 7 个 GrpcClient 更新 proto→DTO 映射（优先读 string） | 7 个防腐层 gRPC 客户端 |

### 2.2 执行顺序与依赖

```
A1 ConsulConfigWatcher 注册 ──┐
                              ├──> B1 Cart GrpcService ──┐
A2 ProductSnapshot 双轨化 ────┘                          │
                                                         ├──> C1 spec supersede ──> C2 ADR ──┐
                              B2 SellerShop GrpcService ─┤                                   │
                              B3 ReviewAfterSales ───────┘                                   ├──> D1 .proto ──> D2 GrpcService ──> D3 GrpcClient
                                                                                              │
                                                                                              （D1 依赖 B1/B2/B3 完成后的 .proto 状态）
```

- **A1 + A2 可并行**：互不依赖
- **A2 → B1 串行**：A2 与 B1 都在 Cart BC，DI 注册文件 `Cart.Infrastructure/Dependencies/ServiceCollectionExtensions.cs` 两者都会修改。A2 先做避免 merge 冲突。
- **B1/B2/B3 串行执行**（建议按 B1→B2→B3 顺序便于 review）：3 个 BC 独立，可并行但建议串行
- **C1 + C2 串行**：C1 先标注 supersede 关系，C2 基于 C1 + 实际实施记录 ADR
- **D1 → D2 → D3 严格串行**：D1 修改 .proto + 生成 C# 代码，D2 依赖 D1 生成的新字段，D3 依赖 D2 的服务端双写
- **D1 依赖 B1/B2/B3 完成**：Task B 新建的 3 个 .proto（cart/seller/review）需先存在，D1 才能统一迁移

### 2.3 关键设计原则

1. **复用既有应用服务**：3 个 BC 的 GrpcService 复用既有 `ICartAppService`/`ISellerAppService`/`IReviewAppService`（或通过 `I*InternalQueryService` 间接复用），禁止在 GrpcService 中直接访问仓储。
2. **InternalQueryService 抽象**：在 Application 层新建 `I*InternalQueryService` 接口，仅暴露跨 BC 查询所需的方法子集（只读，不暴露写操作）。
3. **条件性 GrpcService 映射**：3 个 BC 的 `Program.cs` 添加 `if (UseGrpc) MapGrpcService<XxxGrpcService>()`，与既有 6 个 BC 保持一致。
4. **单元测试覆盖**：每个 GrpcService 配套 3 个测试场景（Success/Unavailable/NotFound），遵循既有模式。
5. **Guid→int64 POC 简化**：proto 中 int64 字段保留 `GetHashCode` 简化（与既有 6 个 GrpcService 一致），proto 中 string 字段直接 `Guid.Parse`。

## 3. Task A1: ConsulConfigWatcher 注册为 HostedService

### 3.1 问题分析

`ConsulConfigWatcher`（`src/BuildingBlocks/Leno.Infrastructure/Configuration/ConsulConfigWatcher.cs`）已实现：
- 监听 `leno/anticorruption/use-grpc/{bc}` KV
- 5 分钟长轮询（Consul 长轮询机制）+ 10 秒重试
- 直接写入 `IConfiguration["AntiCorruption:UseGrpc"]`

但全代码库无 `AddHostedService<ConsulConfigWatcher>()` 调用，导致调用方 BC 的 UseGrpc 热更新不生效。

既有临时替代方案 `AddLenoConsulConfig`（`leno/config` 前缀，30 秒轮询，Winton.Extensions.Configuration.Consul）存在不足：
- 30 秒延迟（vs ConsulConfigWatcher 1-2 秒）
- 需要在 `appsettings.json` 中配置 `AntiCorruption:UseGrpc` 节点映射
- 不如 ConsulConfigWatcher 直接写入 `IConfiguration["AntiCorruption:UseGrpc"]` 干净

### 3.2 实施方案

#### 3.2.1 注册位置

`src/BuildingBlocks/Leno.Infrastructure/Dependencies/WebApplicationExtensions.cs` 的 `AddLenoApi` 方法内（一站式注册，避免每个 BC 重复）。

#### 3.2.2 注册条件

通过配置开关 `AntiCorruption:EnableConsulConfigWatcher`（默认 true）控制，所有 BC 都注册。

**决策依据**：被调用方注册也无害。被调用方监听自己的 `leno/anticorruption/use-grpc/{bc}` KV，但被调用方的 UseGrpc 仅控制启动时 GrpcService 映射，运行时热更新不取消已映射端点，所以被调用方即使收到热更新也无副作用。

#### 3.2.3 代码变更

```csharp
// src/BuildingBlocks/Leno.Infrastructure/Dependencies/WebApplicationExtensions.cs
// 在 AddLenoApi 方法内追加：
if (builder.Configuration.GetValue<bool>("AntiCorruption:EnableConsulConfigWatcher", true))
{
    builder.Services.AddHostedService<ConsulConfigWatcher>();
}
```

#### 3.2.4 前置依赖检查

`ConsulConfigWatcher` 构造函数注入 `IConsulClient`，需确认 `AddLenoApi` 调用之前已注册 `IConsulClient`（由 `AddConsulServiceRegistration` 注册）。实施时需验证注册顺序。

#### 3.2.5 配置约定

在各 BC 的 `appsettings.json` 添加默认值（或通过 `AddLenoApi` 内置默认值）：

```json
{
  "AntiCorruption": {
    "EnableConsulConfigWatcher": true
  }
}
```

### 3.3 验证

- **启动验证**：启动任一调用方 BC，日志输出 `ConsulConfigWatcher 启动，监听 KV: leno/anticorruption/use-grpc/{bc}`
- **热更新验证**：在 Consul KV 修改值后 1-2 秒内日志输出 `UseGrpc 配置热更新为 {Value}（BC={BC}）`
- **单元测试**：既有 `ConsulConfigWatcherTests.cs` 可能已覆盖长轮询行为，实施时探查并补充注册相关测试

### 3.4 风险与缓解

| 风险 | 缓解 |
|---|---|
| `IConsulClient` 注册顺序不当导致启动失败 | 实施时验证 `AddLenoApi` 内 `AddConsulServiceRegistration` 调用顺序 |
| Consul 不可达时 `ConsulConfigWatcher` 异常 | 既有实现已捕获异常 + 10 秒重试，无影响 |
| 被调用方收到热更新但无法取消已映射端点 | 设计上接受此限制，运维需重启被调用方进程切换 UseGrpc |

## 4. Task A2: Cart ProductSnapshotAntiCorruptionService 双轨化

### 4.1 问题分析

当前 `ProductSnapshotAntiCorruptionService`（`src/Services/Cart/Leno.Cart.Infrastructure/Services/ProductSnapshotAntiCorruptionService.cs`）存在 3 个问题：

1. **未继承 `AntiCorruptionBase`**：不参与统一 try/catch + Metrics
2. **失败返回 null**：违反 M4.1 读操作抛异常原则
3. **未双轨化**：仅有 HttpClient 实现，无 gRPC 客户端 + Dispatcher

`IProductSnapshotAntiCorruption` 接口当前签名 `Task<SkuSnapshotDto?> GetSkuSnapshotAsync` 已声明返回 nullable，调用方（`CartAppService`）依赖 null 语义。本 Task 修复为抛异常。

### 4.2 实施方案

#### 4.2.1 接口签名变更（breaking change）

```csharp
// src/Services/Cart/Leno.Cart.Application/Abstractions/IProductSnapshotAntiCorruption.cs
public interface IProductSnapshotAntiCorruption
{
    /// <summary>
    /// 查询 SKU 当前快照（标题、图片、价格、在售状态）。
    /// </summary>
    /// <param name="skuId">商品 SKU 标识。</param>
    /// <param name="ct">取消令牌。</param>
    /// <returns>SKU 快照。</returns>
    /// <exception cref="AntiCorruptionException">查询失败抛 PRODUCT_UNAVAILABLE；SKU 不存在抛 PRODUCT_REMOTE_FAILED。</exception>
    Task<SkuSnapshotDto> GetSkuSnapshotAsync(Guid skuId, CancellationToken ct = default);
}
```

返回类型从 `SkuSnapshotDto?` 改为 `SkuSnapshotDto`（非空），失败抛 `AntiCorruptionException`。

#### 4.2.2 HttpClient 实现重构

`ProductSnapshotAntiCorruptionService` 改为继承 `AntiCorruptionBase`，ServiceName = "product"：

- 404 → 抛 `AntiCorruptionException("PRODUCT_REMOTE_FAILED", ...)`（SKU 不存在）
- 5xx/网络异常 → 抛 `AntiCorruptionException("PRODUCT_UNAVAILABLE", ...)`
- 保留 InnerException（与 GrpcAntiCorruptionClientBase 一致）

#### 4.2.3 新建 gRPC 客户端

`src/Services/Cart/Leno.Cart.Infrastructure/Services/Grpc/GrpcProductSnapshotAntiCorruptionClient.cs`：

- 继承 `GrpcAntiCorruptionClientBase`，实现 `IProductSnapshotAntiCorruption`
- ServiceName = "product"，TargetBc = "Product"
- 调用 `ProductInternalService.GetSkuInfoAsync`（既有 RPC）
- proto `SkuInfo` → `SkuSnapshotDto` 映射（仅 Title/MainImage/Price/Salable 字段，与既有 HttpClient 实现一致）

#### 4.2.4 新建 DispatcherAdapter

`src/Services/Cart/Leno.Cart.Infrastructure/Services/Grpc/ProductSnapshotDispatcherAdapter.cs`：

- 适配器，委托 `AntiCorruptionDispatcher<IProductSnapshotAntiCorruption>`

#### 4.2.5 DI 注册

`src/Services/Cart/Leno.Cart.Infrastructure/Dependencies/ServiceCollectionExtensions.cs`：

- HttpClient：`AddHttpClient<ProductSnapshotAntiCorruptionService>()`
- UseGrpc=true：gRPC 客户端 + KeyedSingleton CircuitBreakerState("product") + Dispatcher + Adapter
- UseGrpc=false：直接注册 HttpClient 实现

**熔断器复用**：Cart 已有 `CircuitBreakerState("product")` Keyed Singleton（M4 双轨方案 Task 22 CartPriceService 双轨时在 `Cart.Infrastructure/Dependencies/ServiceCollectionExtensions.cs` 注册），复用同一熔断器实例（同一目标 BC 的熔断状态应共享）。

#### 4.2.6 调用方适配

`CartAppService` 中调用 `GetSkuSnapshotAsync` 的地方：

- 移除 `?.` 链式调用与 null 检查
- 捕获 `AntiCorruptionException` 决定降级策略（移除该 SKU 或抛业务异常）

### 4.3 验证

- **单元测试** `GrpcProductSnapshotAntiCorruptionClientTests`：3 个场景（Success/Unavailable/NotFound）
- **既有测试适配**：`CartAppServiceTests` 更新（移除 null 检查假设）

### 4.4 Breaking Change 影响评估

- `IProductSnapshotAntiCorruption` 是 Cart BC 内部接口，不跨 BC
- 调用方仅 `CartAppService`（实施时探查确认）
- 风险可控，本工作流内修复

## 5. Task B1: Cart.Api/CartGrpcService

### 5.1 .proto 既有契约

```protobuf
// src/BuildingBlocks/Leno.SharedContracts/Protos/cart.proto
service CartInternalService {
  rpc GetCartSnapshot(GetCartSnapshotRequest) returns (CartSnapshot);
  rpc GetCheckoutPreview(GetCheckoutPreviewRequest) returns (CheckoutPreview);
}

message GetCartSnapshotRequest { string user_id = 1; }
message CartSnapshot {
  string cart_id = 1;
  repeated CartItem items = 2;
  int64 total_cents = 3;
}
message CartItem {
  int64 sku_id = 1;
  int32 quantity = 2;
  int64 unit_price_cents = 3;
}
message GetCheckoutPreviewRequest {
  string user_id = 1;
  repeated CartItem items = 2;
}
message CheckoutPreview {
  int64 subtotal_cents = 1;
  int64 discount_cents = 2;
  int64 shipping_cents = 3;
  int64 total_cents = 4;
}
```

### 5.2 ICartInternalQueryService 接口设计

```csharp
// src/Services/Cart/Leno.Cart.Application/ICartInternalQueryService.cs
public interface ICartInternalQueryService
{
    Task<CartSnapshotDto?> GetCartSnapshotAsync(Guid userId, CancellationToken ct = default);
    Task<CheckoutPreviewDto?> GetCheckoutPreviewAsync(Guid userId, CancellationToken ct = default);
}
```

新建 `CartSnapshotDto` + `CheckoutPreviewDto`（如不存在），仅含跨 BC 查询所需字段。

### 5.3 CartInternalQueryService 实现

委托既有 `ICartAppService.GetCartAsync` + `PreviewCheckoutAsync`，将 `CartDto`/`CheckoutPreviewDto` 映射为 `CartSnapshotDto`/`CheckoutPreviewDto`。

### 5.4 CartGrpcService 实现

```csharp
[Authorize]
public sealed class CartGrpcService : CartInternalService.CartInternalServiceBase
{
    public override async Task<CartSnapshot> GetCartSnapshot(
        GetCartSnapshotRequest request, ServerCallContext context)
    {
        var userId = Guid.Parse(request.UserId);  // proto 已是 string
        var dto = await _queryService.GetCartSnapshotAsync(userId, context.CancellationToken);
        if (dto is null) throw new RpcException(new Status(StatusCode.NotFound, $"Cart for user {request.UserId} not found"));
        return MapToProto(dto);
    }

    public override async Task<CheckoutPreview> GetCheckoutPreview(
        GetCheckoutPreviewRequest request, ServerCallContext context)
    {
        var userId = Guid.Parse(request.UserId);
        var dto = await _queryService.GetCheckoutPreviewAsync(userId, context.CancellationToken);
        if (dto is null) throw new RpcException(new Status(StatusCode.NotFound, ...));
        return MapToProto(dto);
    }
}
```

### 5.5 关键决策

- **user_id 用 string**：proto 已是 string，直接 `Guid.Parse`，无需 POC GetHashCode 简化
- **sku_id 仍是 int64**：proto 中 `CartItem.sku_id` 为 int64，POC 阶段保留 GetHashCode 简化（与既有 ProductGrpcService 一致）

## 6. Task B2: SellerShop.Api/SellerGrpcService

### 6.1 .proto 既有契约

```protobuf
// src/BuildingBlocks/Leno.SharedContracts/Protos/seller.proto
service SellerInternalService {
  rpc GetSellerInfo(GetSellerInfoRequest) returns (SellerInfo);
  rpc GetShopInfo(GetShopInfoRequest) returns (ShopInfo);
  rpc ValidateSellerOwnership(ValidateSellerOwnershipRequest) returns (ValidateSellerOwnershipResponse);
}

message GetSellerInfoRequest { string seller_id = 1; }
message SellerInfo {
  string seller_id = 1;
  string name = 2;
  string status = 3;
  int64 shop_id = 4;
}
message GetShopInfoRequest { int64 shop_id = 1; }
message ShopInfo {
  int64 shop_id = 1;
  string name = 2;
  string status = 3;
  string seller_id = 4;
}
message ValidateSellerOwnershipRequest {
  string seller_id = 1;
  string resource_type = 2;
  string resource_id = 3;
}
message ValidateSellerOwnershipResponse { bool is_valid = 1; }
```

### 6.2 ISellerInternalQueryService 接口设计

```csharp
// src/Services/SellerShop/Leno.SellerShop.Application/ISellerInternalQueryService.cs
public interface ISellerInternalQueryService
{
    Task<SellerInfoDto?> GetSellerInfoAsync(Guid sellerId, CancellationToken ct = default);
    Task<ShopInfoDto?> GetShopInfoAsync(Guid shopId, CancellationToken ct = default);
    // ValidateSellerOwnership 延后（F1.4 独立任务）
}
```

### 6.3 SellerInternalQueryService 实现

- `GetSellerInfoAsync`：委托 `ISellerAppService.GetSellerProfileAsync(userId)`，但接口参数是 `userId`（用户域 ID），proto `seller_id` 语义需澄清。**决策**：M4 gRPC 双轨设计 spec §11.3 假设 `seller_id` = 用户域 UserId。实施时探查 `SellerProfileDto` 字段确认。
- `GetShopInfoAsync`：委托 `IShopAppService.GetShopInfoAsync(shopId)`

### 6.4 SellerGrpcService 实现

```csharp
[Authorize]
public sealed class SellerGrpcService : SellerInternalService.SellerInternalServiceBase
{
    public override async Task<SellerInfo> GetSellerInfo(
        GetSellerInfoRequest request, ServerCallContext context)
    {
        var sellerId = Guid.Parse(request.SellerId);
        var dto = await _queryService.GetSellerInfoAsync(sellerId, context.CancellationToken);
        if (dto is null) throw new RpcException(new Status(StatusCode.NotFound, ...));
        return MapToProto(dto);
    }

    public override async Task<ShopInfo> GetShopInfo(
        GetShopInfoRequest request, ServerCallContext context)
    {
        // proto shop_id 是 int64，POC 简化
        var shopId = new Guid(Convert.FromHexString(request.ShopId.ToString("X16")));
        var dto = await _queryService.GetShopInfoAsync(shopId, context.CancellationToken);
        if (dto is null) throw new RpcException(new Status(StatusCode.NotFound, ...));
        return MapToProto(dto);
    }

    public override Task<ValidateSellerOwnershipResponse> ValidateSellerOwnership(
        ValidateSellerOwnershipRequest request, ServerCallContext context)
    {
        // F1.4 独立任务，本次抛 Unimplemented
        throw new RpcException(new Status(StatusCode.Unimplemented,
            "ValidateSellerOwnership not implemented, see F1.4"));
    }
}
```

### 6.5 关键决策

- **seller_id 用 string**：proto 已是 string，直接 `Guid.Parse`
- **shop_id 仍是 int64**：proto 中为 int64，POC 阶段 GetHashCode 简化（与 ProductGrpcService 一致）
- **ValidateSellerOwnership 抛 Unimplemented**：F1.4 越权校验集中化是独立任务

## 7. Task B3: ReviewAfterSales.Api/ReviewGrpcService

### 7.1 .proto 既有契约

```protobuf
// src/BuildingBlocks/Leno.SharedContracts/Protos/review.proto
service ReviewInternalService {
  rpc GetProductRating(GetProductRatingRequest) returns (ProductRating);
  rpc GetOrderReviews(GetOrderReviewsRequest) returns (OrderReviews);
}

message GetProductRatingRequest { int64 spu_id = 1; }
message ProductRating {
  int64 spu_id = 1;
  double average_rating = 2;
  int32 total_count = 3;
  int32 positive_count = 4;
}
message GetOrderReviewsRequest { string order_id = 1; }
message OrderReviews { repeated ReviewSummary reviews = 1; }
message ReviewSummary {
  string review_id = 1;
  int64 spu_id = 2;
  int32 rating = 3;
  string content = 4;
  string created_at = 5;
}
```

### 7.2 IReviewInternalQueryService 接口设计

```csharp
// src/Services/ReviewAfterSales/Leno.ReviewAfterSales.Application/IReviewInternalQueryService.cs
public interface IReviewInternalQueryService
{
    Task<ProductRatingDto?> GetProductRatingAsync(Guid spuId, CancellationToken ct = default);
    Task<OrderReviewsDto?> GetOrderReviewsAsync(Guid orderId, CancellationToken ct = default);
}
```

### 7.3 ReviewInternalQueryService 实现

- `GetProductRatingAsync`：**既有 `IReviewAppService` 无聚合评分查询方法**。需在 `ReviewInternalQueryService` 内部直接查询仓储（`IReviewRepository`）聚合计算 average_rating/total_count/positive_count。
- `GetOrderReviewsAsync`：**既有 `IReviewAppService` 无按 orderId 查询方法**（仅有 `GetReviewByOrderLineAsync`）。需在 `ReviewInternalQueryService` 内部查询 `IReviewRepository` 按 orderId 聚合评价列表。

**设计权衡**：
- 这违反"GrpcService 不直接访问仓储"原则。但 `IReviewInternalQueryService` 是 Application 层接口，其实现可以访问仓储（属于 Application 层职责）。
- 替代方案：在 `IReviewAppService` 添加 `GetProductRatingAsync` 方法。但这会污染面向买家的 AppService。
- **决策**：`ReviewInternalQueryService` 直接访问 `IReviewRepository` 聚合查询，符合 Application 层职责（编排领域查询）。

### 7.4 ReviewGrpcService 实现

```csharp
[Authorize]
public sealed class ReviewGrpcService : ReviewInternalService.ReviewInternalServiceBase
{
    public override async Task<ProductRating> GetProductRating(
        GetProductRatingRequest request, ServerCallContext context)
    {
        // proto spu_id 是 int64，POC 简化
        var spuId = new Guid(Convert.FromHexString(request.SpuId.ToString("X16")));
        var dto = await _queryService.GetProductRatingAsync(spuId, context.CancellationToken);
        if (dto is null) throw new RpcException(new Status(StatusCode.NotFound, ...));
        return MapToProto(dto);
    }

    public override async Task<OrderReviews> GetOrderReviews(
        GetOrderReviewsRequest request, ServerCallContext context)
    {
        var orderId = Guid.Parse(request.OrderId);  // proto 已是 string
        var dto = await _queryService.GetOrderReviewsAsync(orderId, context.CancellationToken);
        if (dto is null) throw new RpcException(new Status(StatusCode.NotFound, ...));
        return MapToProto(dto);
    }
}
```

### 7.5 关键决策

- **spu_id 仍是 int64**：proto 中为 int64，POC 简化（与 ProductGrpcService 一致）
- **order_id 用 string**：proto 已是 string，直接 `Guid.Parse`
- **ReviewInternalQueryService 访问仓储**：符合 Application 层职责（编排领域查询），不污染面向买家的 IReviewAppService
- **created_at 用 string**：proto 中为 string，DTO→proto 直接 `DateTime.ToString("O")`（ISO 8601）

## 8. 3 个 BC 共性约束

### 8.1 GrpcService 复用模式

3 个 BC 的 GrpcService 构造函数注入 `I{BC}InternalQueryService` + `ILogger<{BC}GrpcService>`，与既有 6 个 GrpcService 一致。

### 8.2 错误码映射

- 业务 NotFound → `StatusCode.NotFound`
- 参数无效 → `StatusCode.InvalidArgument`
- 权限缺失 → 由 `GrpcInternalKeyInterceptor` + `[Authorize]` 统一处理
- 未实现 → `StatusCode.Unimplemented`（如 `ValidateSellerOwnership`）

错误消息应包含请求参数与失败原因（如 `$"Cart for user {request.UserId} not found"`），便于调用方排查。

### 8.3 Guid→int64 映射

proto 中 int64 字段保留 POC 阶段 `GetHashCode` 简化（与既有 6 个 GrpcService 一致）。Task 27 跳过该迁移（见 ADR-0006）。

### 8.4 单元测试

每个 GrpcService 配套 3 个测试场景，mock `I{BC}InternalQueryService`：

- **Success**：查询返回 DTO，验证 proto 映射正确
- **Unavailable**：查询抛 `RpcException(Unavailable)`，验证错误码 `{BC}_UNAVAILABLE`
- **NotFound**：查询返回 null，验证抛 `RpcException(NotFound)`

### 8.5 文件清单

每个 BC 新增/修改文件：

```
src/Services/{BC}/Leno.{BC}.Application/
  ├── I{BC}InternalQueryService.cs          # 新建
  └── InternalQueryServices/
      └── {BC}InternalQueryService.cs       # 新建
src/Services/{BC}/Leno.{BC}.Api/
  ├── GrpcServices/
  │   └── {BC}GrpcService.cs                # 新建
  ├── Program.cs                             # 修改（条件性 MapGrpcService）
  └── Leno.{BC}.Api.csproj                  # 修改（Grpc.AspNetCore + Leno.SharedContracts.Grpc）
src/Services/{BC}/Leno.{BC}.Infrastructure.Tests/
  └── Grpc/
      └── {BC}GrpcServiceTests.cs           # 新建
```

## 9. Task C1: 既有 spec supersede 标注

### 9.1 待标注 spec 清单

| Spec 文件 | 状态标注 | 说明 |
|---|---|---|
| `docs/superpowers/specs/2026-07-13-comprehensive-optimization-design.md` | superseded | V1 整体设计，已被 `2026-07-17-comprehensive-optimization-v2-design.md` 取代 |
| `docs/superpowers/specs/2026-07-14-api-gateway-enhancement-design.md` | partially_superseded | API 网关增强设计，部分内容（CORS/限流/JWT 黑名单）仍有效，gRPC/双轨部分被 M4 spec 取代 |
| `.trae/specs/fix-critical-business-vulnerabilities/` | partially_superseded | F1/F2 修复，部分内容（输入校验/SQL 注入防护）仍有效，鉴权集中化被 F1.4 后续任务取代 |

### 9.2 标注格式（YAML frontmatter）

在每个 spec 文件**最顶部**追加 supersede 声明块：

```yaml
---
status: superseded
superseded_by: docs/superpowers/specs/2026-07-17-comprehensive-optimization-v2-design.md
superseded_date: 2026-07-19
superseded_reason: |
  V1 整体设计已被 V2 全面取代。V2 在以下方面有重大调整：
  - 重新规划快轨 F1-F4 + 慢轨 M1-M6 优先级
  - 引入 Subagent-Driven 实施模式
  - M4 改为 gRPC 双轨方案（保留 HttpClient fallback）
  - 新增 M5 可观测性 + M6 CQRS/BFF/文档
  V1 中未实施的部分（如 F1.4 越权校验集中化）仍可作为后续任务参考，
  但实施时需遵循 V2 的架构约束。
---
```

### 9.3 部分取代的标注

对于 `2026-07-14-api-gateway-enhancement-design.md` 和 `.trae/specs/fix-critical-business-vulnerabilities/`，使用 `partially_superseded` 状态，并列出具体被取代的章节：

```yaml
---
status: partially_superseded
partially_superseded_by:
  - docs/superpowers/specs/2026-07-19-m4-grpc-dual-track-design.md  # gRPC 双轨部分
  - docs/superpowers/specs/2026-07-17-comprehensive-optimization-v2-design.md  # 整体架构
partially_superseded_date: 2026-07-19
partially_superseded_reason: |
  本 spec 中以下章节已被新 spec 取代：
  - 第 X 节 gRPC 服务端实现 → 2026-07-19-m4-grpc-dual-track-design.md §4
  - 第 Y 节 ...
  以下章节仍有效：
  - 第 A 节 CORS 配置
  - 第 B 节 限流策略
  - ...
---
```

### 9.4 执行步骤

1. 读取 3 份 spec，识别被取代 vs 仍有效的章节
2. 在每份 spec 顶部追加 frontmatter 声明块
3. 不修改原 spec 内容（保留历史可追溯）

## 10. Task C2: ADR 关键决策记录

### 10.1 ADR 文件位置

`docs/decisions/`（新建目录），采用 Nygard ADR 格式：

```markdown
# ADR-XXXX: 标题

## 状态
已接受 / 已取代 / 已弃用

## 上下文
（决策背景、约束、问题）

## 决策
（选择方案 + 理由）

## 后果
（正面/负面后果、风险缓解）
```

### 10.2 ADR 清单

| ADR 编号 | 标题 | 来源 |
|---|---|---|
| **ADR-0001** | gRPC 双轨方案（保留 HttpClient fallback） | M4 spec §1.3 |
| **ADR-0002** | 熔断器三状态机（Closed/Open/HalfOpen） | M4 spec §3.4 |
| **ADR-0003** | AntiCorruptionDispatcher 适配器模式（不实现 TService） | Task 15 实施发现 |
| **ADR-0004** | IOrderStatusProvider 重构（分离远程查询与业务规则） | Task 23 实施发现 |
| **ADR-0005** | .proto 向后兼容约束（只能新增字段） | 项目硬约束 |
| **ADR-0006** | Guid→int64 POC 简化（GetHashCode）的历史决策与修正 | Task 27 POC 阶段决策 |
| **ADR-0007** | Guid→string 迁移采用新增 string 字段策略 | 工作流 D 决策 |

### 10.3 ADR-0001 示例（gRPC 双轨方案）

```markdown
# ADR-0001: gRPC 双轨方案（保留 HttpClient fallback）

## 状态
已接受（2026-07-19）

## 上下文
M4.3 通信升级需要降低跨 BC 同步调用的延迟。gRPC 相比 HTTP/1.1 + JSON 有显著性能优势
（Protobuf 二进制 + HTTP/2 多路复用）。但直接迁移到 gRPC 存在风险：
- 服务端故障时无降级路径
- 灰度切换困难
- 运维复杂度高

## 决策
采用 gRPC + HttpClient 双轨方案：
1. AntiCorruptionDispatcher<TService> 在运行时选择传输方式
2. UseGrpc 配置开关通过 Consul KV 热更新（1-2 秒生效）
3. 熔断器三状态机自动降级（3 次连续失败 Open，30 秒后 HalfOpen 探测）
4. HttpClient 代码永久保留作为 fallback（不实施 Task 11 下线）

## 后果
**正面：**
- 风险可控：gRPC 故障时自动降级到 HttpClient
- 灵活灰度：按 BC 独立切换
- 性能提升：gRPC P99 延迟显著低于 HttpClient

**负面：**
- 代码复杂度增加：需维护两套实现 + 适配器
- 测试覆盖成本：需覆盖双轨 + 降级场景
- 运维成本：需监控 gRPC 调用指标 + 熔断器状态

**风险缓解：**
- 适配器模式隔离复杂度（DispatcherAdapter 实现 TService 接口）
- 单元测试覆盖核心场景
- Prometheus 指标 + Grafana 仪表盘监控
- Runbook 提供应急回滚操作
```

### 10.4 ADR-0003 示例（适配器模式）

```markdown
# ADR-0003: AntiCorruptionDispatcher 适配器模式

## 状态
已接受（2026-07-19，Task 15 实施时发现）

## 上下文
M4 spec 原设计 `AntiCorruptionDispatcher<TService>` 应实现 `TService` 接口，
业务层直接注入 Dispatcher。但实施时发现：
- Dispatcher.ExecuteAsync<TResult>(Func<TService, Task<TResult>>...) 需要返回值
- TService 中返回 Task（非 Task<T>）的方法无法直接适配
- Dispatcher 需要管理熔断器 + 降级逻辑，职责过重

## 决策
Dispatcher 仅实现 IDisposable，不实现 TService 接口。
为每个防腐层创建 {Service}DispatcherAdapter：
- 适配器实现 TService 接口
- 每个方法委托 dispatcher.ExecuteAsync(s => s.MethodAsync(...), ct)
- 对返回 Task（非 Task<T>）的方法，使用 ExecuteAsync<int> + return 0 包装

## 后果
**正面：**
- Dispatcher 职责单一（仅调度）
- 适配器可独立测试
- 业务层无感知（注入 TService 接口）

**负面：**
- 每个防腐层多一个文件（7 个 DispatcherAdapter）
- void 方法包装为 ExecuteAsync<int> + return 0 略显 hacky
```

### 10.5 执行步骤

1. 新建 `docs/decisions/README.md`（ADR 索引 + 格式说明）
2. 逐个编写 7 个 ADR 文件（含 ADR-0007 Guid→string 迁移决策）
3. 每个 ADR 基于实际实施结果（commit 历史 + spec），而非假设

## 11. Task D1: 6 个 .proto 文件新增 string 字段

### 11.1 迁移策略

采用**新增 string 字段 + 标记 int64 字段 deprecated**策略，符合项目硬约束（.proto 只能新增字段，不能修改/删除）。

- 对每个 `int64 xxx_id` 字段，新增 `string xxx_id_str = N;`（N 为新字段号）
- 在原 `int64 xxx_id` 字段添加 `[deprecated = true]` 选项，表达迁移意图
- 保留 int64 字段（永久向后兼容，旧客户端仍可读取）
- buf breaking 校验通过（仅新增字段 + 添加 deprecated 选项，不触发 breaking）

### 11.2 待迁移 .proto 清单

经代码探查，6 个 .proto 文件含 int64 ID 字段：

| .proto 文件 | 待迁移 int64 字段 | 新增 string 字段 |
|---|---|---|
| `product.proto` | `GetSkuInfoRequest.sku_id`、`SkuInfo.sku_id/spu_id/seller_id`、`BatchGetSkuInfoRequest.sku_ids`、`GetSkuStockRequest.sku_id`、`SkuStock.sku_id`、`GetProductDetailRequest.spu_id`、`ProductDetail.spu_id/seller_id` | 对应 `sku_id_str/spu_id_str/seller_id_str/sku_ids_str` |
| `order.proto` | `OrderItem.sku_id` | `sku_id_str` |
| `promotion.proto` | `OrderItem.sku_id` | `sku_id_str` |
| `cart.proto` | `CartItem.sku_id` | `sku_id_str` |
| `seller.proto` | `GetShopInfoRequest.shop_id`、`ShopInfo.shop_id` | `shop_id_str` |
| `review.proto` | `GetProductRatingRequest.spu_id`、`ProductRating.spu_id`、`ReviewSummary.spu_id` | `spu_id_str` |

**无需迁移的 .proto**：`payment.proto`、`user.proto`、`points.proto`（已全部使用 string）

### 11.3 .proto 修改示例（product.proto）

```protobuf
syntax = "proto3";
package leno.product.v1;
option csharp_namespace = "Leno.SharedContracts.Grpc.Product.V1";

service ProductInternalService {
  rpc GetSkuInfo(GetSkuInfoRequest) returns (SkuInfo);
  rpc BatchGetSkuInfo(BatchGetSkuInfoRequest) returns (BatchGetSkuInfoResponse);
  rpc GetSkuStock(GetSkuStockRequest) returns (SkuStock);
  rpc GetProductDetail(GetProductDetailRequest) returns (ProductDetail);
}

message GetSkuInfoRequest {
  int64 sku_id = 1 [deprecated = true];
  string sku_id_str = 13;  // Guid→string 迁移新增字段
}
message SkuInfo {
  int64 sku_id = 1 [deprecated = true];
  int64 spu_id = 2 [deprecated = true];
  string title = 3;
  string main_image = 4;
  int64 price_cents = 5;
  string currency = 6;
  bool salable = 7;
  int64 seller_id = 8 [deprecated = true];
  int32 stock = 9;
  optional string status = 10;
  optional string shop_id = 11;
  optional int64 updated_at = 12;
  // Guid→string 迁移新增字段
  string sku_id_str = 13;
  string spu_id_str = 14;
  string seller_id_str = 15;
}
message BatchGetSkuInfoRequest {
  repeated int64 sku_ids = 1 [deprecated = true];
  repeated string sku_ids_str = 2;  // Guid→string 迁移新增字段
}
// ... 其余 message 同理
```

### 11.4 字段编号约定

- 新增 string 字段编号紧接既有最大字段号 +1（如 product.proto SkuInfo 既有最大 12，新增 13/14/15）
- 重复字段（repeated）使用新字段号

### 11.5 执行步骤

1. 修改 6 个 .proto 文件，新增 string 字段 + 标记 int64 deprecated
2. 运行 `buf generate` 重新生成 C# 代码
3. 运行 `buf breaking` 校验（应通过，仅新增字段）
4. 编译 `Leno.SharedContracts.Grpc` 项目验证生成代码正确

### 11.6 验证

- `buf breaking` 校验通过
- `dotnet build Leno.SharedContracts.Grpc` 成功
- 新增 string 字段在生成代码中可访问

## 12. Task D2: 9 个 GrpcService 更新 DTO→proto 映射

### 12.1 映射策略

采用**双写**策略：GrpcService 同时填充 int64 字段（GetHashCode，向后兼容）和 string 字段（Guid.ToString()，新客户端优先读）。

### 12.2 GrpcService 修改清单

| GrpcService | 文件 | 修改内容 |
|---|---|---|
| `ProductGrpcService` | `Product.Api/GrpcServices/ProductGrpcService.cs` | MapToProto 双写 sku_id/spu_id/seller_id |
| `PromotionGrpcService` | `Promotion.Api/GrpcServices/PromotionGrpcService.cs` | MapToProto 双写 sku_id（OrderItem） |
| `PointsGrpcService` | `PointsMembership.Api/GrpcServices/PointsGrpcService.cs` | 无 int64 ID 字段，无需修改 |
| `UserAuthGrpcService` | `UserAuth.Api/GrpcServices/UserAuthGrpcService.cs` | 无 int64 ID 字段，无需修改 |
| `OrderGrpcService` | `Order.Api/GrpcServices/OrderGrpcService.cs` | MapToProto 双写 sku_id（OrderItem） |
| `PaymentGrpcService` | `Payment.Api/GrpcServices/PaymentGrpcService.cs` | 无 int64 ID 字段，无需修改 |
| `CartGrpcService` | `Cart.Api/GrpcServices/CartGrpcService.cs`（Task B1 新建） | MapToProto 双写 sku_id（CartItem） |
| `SellerGrpcService` | `SellerShop.Api/GrpcServices/SellerGrpcService.cs`（Task B2 新建） | MapToProto 双写 shop_id |
| `ReviewGrpcService` | `ReviewAfterSales.Api/GrpcServices/ReviewGrpcService.cs`（Task B3 新建） | MapToProto 双写 spu_id |

实际需修改 6 个 GrpcService（Points/UserAuth/Payment 无 int64 ID 字段）。

### 12.3 代码示例（ProductGrpcService.MapToProto）

```csharp
private static SkuInfo MapToProto(SkuInfoResultDto dto) => new()
{
    // 既有 int64 字段（向后兼容，标记 deprecated）
    SkuId = (long)dto.SkuId.GetHashCode(),
    SpuId = (long)dto.SpuId.GetHashCode(),
    SellerId = (long)dto.SellerId.GetHashCode(),
    // ... 其他字段不变
    Title = dto.Title,
    MainImage = dto.MainImageUrl,
    PriceCents = (long)(dto.Price * 100),
    // ... 新增 string 字段（Guid→string 迁移）
    SkuIdStr = dto.SkuId.ToString(),
    SpuIdStr = dto.SpuId.ToString(),
    SellerIdStr = dto.SellerId.ToString(),
};
```

### 12.4 请求参数解析更新

既有 GrpcService 中 `Guid.Parse` 或 `GetHashCode` 反向解析需更新为优先读 string 字段：

```csharp
public override async Task<SkuInfo> GetSkuInfo(GetSkuInfoRequest request, ServerCallContext context)
{
    // 优先读 string 字段，回退到 int64（向后兼容旧客户端）
    Guid skuId;
    if (!string.IsNullOrEmpty(request.SkuIdStr))
    {
        skuId = Guid.Parse(request.SkuIdStr);
    }
    else
    {
        // 旧客户端回退（GetHashCode 无法反向解析，仅用于 POC 阶段兼容）
        skuId = new Guid(Convert.FromHexString(request.SkuId.ToString("X16")));
    }
    // ...
}
```

### 12.5 验证

- 6 个 GrpcService 单元测试更新：验证 string 字段正确填充
- 既有 3 个测试场景（Success/Unavailable/NotFound）通过
- 新增 1 个测试场景：旧客户端（仅 int64）仍可工作（向后兼容验证）

## 13. Task D3: 7 个 GrpcClient 更新 proto→DTO 映射

### 13.1 映射策略

采用**优先读 string**策略：GrpcClient 优先读取 string 字段，为空时回退到 int64（GetHashCode 反向不安全，仅用于 POC 阶段兼容）。

### 13.2 GrpcClient 修改清单

| GrpcClient | 文件 | 修改内容 |
|---|---|---|
| `GrpcProductAntiCorruptionClient` | `Order.Infrastructure/Services/Grpc/` | MapToDto 优先读 sku_id_str/spu_id_str/seller_id_str |
| `GrpcPromotionAntiCorruptionClient` | `Order.Infrastructure/Services/Grpc/` | MapToDto 优先读 sku_id_str（OrderItem） |
| `GrpcPointsAntiCorruptionClient` | `Order.Infrastructure/Services/Grpc/` | 无 int64 ID 字段，无需修改 |
| `GrpcUserContactAntiCorruptionClient` | `Notification.Infrastructure/Services/Grpc/` | 无 int64 ID 字段，无需修改 |
| `GrpcCartPriceService` | `Cart.Infrastructure/Services/Grpc/` | MapToDto 优先读 sku_id_str/spu_id_str/seller_id_str |
| `GrpcProductSnapshotAntiCorruptionClient` | `Cart.Infrastructure/Services/Grpc/`（Task A2 新建） | MapToDto 优先读 sku_id_str/spu_id_str/seller_id_str |
| `GrpcOrderStatusProvider` | `ReviewAfterSales.Infrastructure/Services/Grpc/` | MapToDto 优先读 sku_id_str（OrderItem） |
| `GrpcPaymentInfoQueryService` | `ReviewAfterSales.Infrastructure/Services/Grpc/` | 无 int64 ID 字段，无需修改 |

实际需修改 5 个 GrpcClient（Points/UserContact/Payment 无 int64 ID 字段）。

### 13.3 代码示例（GrpcProductAntiCorruptionClient.MapToDto）

```csharp
private static SkuInfo MapToDto(SkuInfo proto) => new()
{
    // 优先读 string 字段，回退到 int64（向后兼容）
    SkuId = !string.IsNullOrEmpty(proto.SkuIdStr)
        ? Guid.Parse(proto.SkuIdStr)
        : new Guid(Convert.FromHexString(proto.SkuId.ToString("X16"))),
    SpuId = !string.IsNullOrEmpty(proto.SpuIdStr)
        ? Guid.Parse(proto.SpuIdStr)
        : new Guid(Convert.FromHexString(proto.SpuId.ToString("X16"))),
    SellerId = !string.IsNullOrEmpty(proto.SellerIdStr)
        ? Guid.Parse(proto.SellerIdStr)
        : new Guid(Convert.FromHexString(proto.SellerId.ToString("X16"))),
    // ... 其他字段不变
};
```

### 13.4 请求构造更新

既有 GrpcClient 中构造请求时需同时填充 int64 和 string 字段：

```csharp
public async Task<SkuInfo?> GetSkuInfoAsync(Guid skuId, CancellationToken ct)
{
    var request = new GetSkuInfoRequest
    {
        SkuId = (long)skuId.GetHashCode(),       // 既有 int64（向后兼容）
        SkuIdStr = skuId.ToString(),             // 新增 string
    };
    // ...
}
```

### 13.5 验证

- 5 个 GrpcClient 单元测试更新：验证优先读 string 字段
- 既有 3 个测试场景（Success/Unavailable/NotFound）通过
- 新增 1 个测试场景：服务端仅返回 string 字段时正确解析（新服务端兼容）

## 14. 验收标准

### 14.1 工作流 A 验收

- [ ] `ConsulConfigWatcher` 在所有 BC 启动时注册（日志输出 `ConsulConfigWatcher 启动`）
- [ ] Consul KV 修改 UseGrpc 后 1-2 秒内日志输出热更新
- [ ] Cart `ProductSnapshotAntiCorruptionService` 继承 `AntiCorruptionBase`
- [ ] `IProductSnapshotAntiCorruption.GetSkuSnapshotAsync` 返回非空，失败抛 `AntiCorruptionException`
- [ ] Cart `GrpcProductSnapshotAntiCorruptionClient` 实现 + 3 个单元测试通过
- [ ] Cart `ProductSnapshotDispatcherAdapter` 实现
- [ ] Cart DI 注册支持 UseGrpc 切换

### 14.2 工作流 B 验收

- [ ] Cart.Api/CartGrpcService 实现 + 3 个单元测试通过
- [ ] SellerShop.Api/SellerGrpcService 实现（GetSellerInfo + GetShopInfo）+ 3 个单元测试通过
- [ ] ReviewAfterSales.Api/ReviewGrpcService 实现 + 3 个单元测试通过
- [ ] 3 个 BC.Api 的 `Program.cs` 条件性 `MapGrpcService`
- [ ] 3 个 BC.Api 的 `.csproj` 引用 `Grpc.AspNetCore 2.65.0` + `Leno.SharedContracts.Grpc`
- [ ] 9 个 BC.Api GrpcService 完整覆盖 spec §4.7 清单

### 14.3 工作流 C 验收

- [ ] 3 份旧 spec 顶部有 frontmatter supersede 声明，原内容不变
- [ ] `docs/decisions/README.md` 含 ADR 索引 + 格式说明
- [ ] `docs/decisions/` 含 7 个 ADR 文件（含 ADR-0007 Guid→string 迁移决策）
- [ ] 每个 ADR 基于实际实施结果，引用 commit 或 spec 章节

### 14.4 工作流 D 验收

- [ ] 6 个 .proto 文件新增 string 字段 + int64 字段标记 `[deprecated = true]`
- [ ] `buf breaking` 校验通过
- [ ] `buf generate` 重新生成 C# 代码，`Leno.SharedContracts.Grpc` 编译通过
- [ ] 6 个 GrpcService 更新 DTO→proto 映射（双写 int64 + string）
- [ ] 5 个 GrpcClient 更新 proto→DTO 映射（优先读 string）
- [ ] 既有单元测试全部通过（Success/Unavailable/NotFound）
- [ ] 新增向后兼容单元测试通过（旧客户端 int64 + 新服务端 string）

### 14.5 整体验收

- [ ] 所有单元测试通过（既有 48 个 + 新增 12 个 + D2/D3 新增 11 个 = 71 个）
- [ ] 所有 commit 推送到远程仓库（中文 commit message）
- [ ] 无回归（既有功能不受影响）

## 15. 风险与缓解

| 风险 | 概率 | 影响 | 缓解 |
|---|---|---|---|
| `IConsulClient` 注册顺序不当导致启动失败 | 低 | 高 | 实施时验证 `AddLenoApi` 内 `AddConsulServiceRegistration` 调用顺序 |
| `IProductSnapshotAntiCorruption` 签名变更导致调用方编译失败 | 高 | 中 | 本工作流内修复 `CartAppService`，探查确认无其他调用方 |
| 3 个 BC 既有应用服务不满足 GrpcService 需求（如 ReviewAfterSales 无聚合评分查询） | 已知 | 中 | `IReviewInternalQueryService` 直接访问仓储聚合查询 |
| `seller_id` 语义歧义（用户域 ID vs 卖家档案 ID） | 中 | 中 | 实施时探查 `SellerProfileDto` 字段，与既有调用方约定一致 |
| ADR 内容与实际实施不符 | 低 | 低 | 每个 ADR 引用 commit hash + spec 章节，便于追溯 |
| Guid→string 迁移后 int64 字段误用（GetHashCode 碰撞） | 中 | 中 | GrpcClient 优先读 string，int64 仅向后兼容；D2/D3 新增向后兼容测试 |
| `buf breaking` 误报新增字段为 breaking | 低 | 低 | 新增字段 + deprecated 选项符合 proto3 向后兼容规则 |

## 16. 相关文档

- M4 gRPC 双轨设计 spec：`docs/superpowers/specs/2026-07-19-m4-grpc-dual-track-design.md`
- M4 gRPC 双轨实施计划：`docs/superpowers/plans/2026-07-19-m4-grpc-dual-track-implementation.md`
- 全面优化 V2 设计 spec：`docs/superpowers/specs/2026-07-17-comprehensive-optimization-v2-design.md`
- Plan 8（M4 通信升级）：`docs/superpowers/plans/2026-07-17-slow-track-m4-communication-upgrade.md`
- Runbook：`docs/runbooks/m4-grpc-poc-verification.md`
- 内部 API 契约：`docs/contracts/internal-api-contracts.md`（第 9 节）
- 编码规范：`docs/编码规范.md`（第 18 节）
- 防腐层模式：`docs/architecture/anticorruption-pattern.md`
