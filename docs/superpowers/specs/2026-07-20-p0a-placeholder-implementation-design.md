# P0-A 核心占位实现补齐 设计文档

- **子项目编号**：P0-A
- **创建日期**：2026-07-20
- **作者**：brainstorming 流程
- **状态**：待实现
- **关联文档**：上轮《Leno 电商微服务项目代码质量与生产部署风险分析报告》

---

## 1. 背景与目标

### 1.1 背景

上轮代码质量分析报告发现 35 处占位符/未实现代码，其中 6 处 P0 级别占位直接导致核心业务（秒杀、优惠券、库存、积分、卖家归属校验）数据不一致或功能不可用，违反用户规则"零占位容忍度"红线。

### 1.2 目标

补齐以下 6 处 P0 占位实现，使核心业务流转闭环：

| # | 占位位置 | 业务影响 |
|---|---------|---------|
| 1 | `Order/SeckillOrderCreationService.PublishFailedEventAsync` | 秒杀失败时 Promotion 域无法释放预占秒杀库存 |
| 2 | `Promotion/PromotionGrpcService.LockCoupon` | 优惠券锁定无任何业务逻辑，可被重复使用 |
| 3 | `Promotion/PromotionGrpcService.ReleaseCoupons` | 优惠券释放无任何业务逻辑，取消订单后券永久锁定 |
| 4 | `Product/ProductGrpcService.GetSkuStock` | 库存查询返回 0/0 占位，下游得到错误库存信息 |
| 5 | `Product/ProductGrpcService.GetProductDetail` | 商品详情直接抛 Unimplemented |
| 6 | `Points/PointsGrpcService.Confirm` | 积分确认扣减直接抛 Unimplemented，支付成功后积分无法实际扣减 |
| 7 | `Seller/SellerGrpcService.ValidateSellerOwnership` | 卖家资源归属校验直接抛 Unimplemented，越权风险 |

> 注：表中 6 处占位对应 7 个具体方法（LockCoupon 与 ReleaseCoupons 在 #2/#3 分别计数）。

### 1.3 范围边界

**包含**：
- 7 个占位方法的真实实现
- 4 个跨 BC 内部查询接口的方法扩展
- 1 个新增 gRPC RPC（Order 域 `GetOrderSellerId`）+ proto 同步
- 2 个 SellerShop 域防腐层接口的新增方法
- 约 35 个新增单元测试

**不包含**：
- 秒杀预占记录超时清理后台服务（如缺失，作为 P1-A 任务）
- M4 双轨方案的 `GetHashCode()` 映射重构（保持向后兼容）
- 跨服务端到端集成测试
- CI/CD 流水线改造（P2-A 子项目）
- 密钥与凭证治理（P0-B 子项目）

---

## 2. 架构概览

### 2.1 整体思路

保持现有 M4 双轨架构（gRPC 服务复用 AppService / InternalQueryService），在 4 个跨 BC 内部查询接口上扩展 7 个方法，7 处占位改真实实现。所有改动**不新建聚合、不新建仓储、不修改既有 proto 字段**（仅新增 RPC + message）。

### 2.2 改动矩阵

| # | 占位位置 | 扩展点 | 实现策略 |
|---|---------|--------|---------|
| 1 | `Order/SeckillOrderCreationService.PublishFailedEventAsync` | 构造函数注入 `IEventBus` | 直接 `PublishAsync(SeckillOrderCreationFailedIntegrationEvent)`，不经 Outbox（失败路径无聚合可挂事件） |
| 2 | `Promotion/PromotionGrpcService.LockCoupon` | 注入 `ICouponAppService` | 调 `LockCouponAsync(userId, couponId, orderId)` |
| 3 | `Promotion/PromotionGrpcService.ReleaseCoupons` | `ICouponAppService` 新增 `ReleaseCouponsAsync(orderId)` | 内部按 orderId 反查 `IUserCouponRepository.GetByLockedOrderIdAsync` → `UserCoupon.Release()` → `SaveEntitiesAsync` |
| 4 | `Product/ProductGrpcService.GetSkuStock` | `IProductInternalQueryService` 新增 `GetSkuStockAsync(skuId)` | 查 `StockBaseline` 聚合的 `AvailableQty`/`ReservedQty` |
| 5 | `Product/ProductGrpcService.GetProductDetail` | `IProductInternalQueryService` 新增 `GetSpuDetailAsync(spuId)` | 查 SPU 聚合 + SKU 集合 |
| 6 | `Points/PointsGrpcService.Confirm` | `IPointsInternalAppService` 新增 `ConfirmAsync(ConfirmPointsDto)` | 复用 `PointsOffsetAppService.ConfirmDeductAsync` 模式：按 orderId 反查冻结 → `PointsAccount.ConfirmDeduct(orderId)` → `SaveEntitiesAsync` |
| 7 | `Seller/SellerGrpcService.ValidateSellerOwnership` | `ISellerInternalQueryService` 新增 `ValidateOwnershipAsync(sellerId, resourceType, resourceId)` | 按 resourceType 分支：`shop` 域内查 Shop.SellerId；`spu` 调 `IProductAntiCorruptionService.GetSpuSellerIdAsync`；`order` 调 `IOrderAntiCorruptionService.GetOrderSellerIdAsync` |

### 2.3 依赖关系图

```
Order.SeckillOrderCreationService
  └── IEventBus → RabbitMQ → Promotion.SeckillOrderCreationFailedEventConsumer ✅ 已存在

Promotion.PromotionGrpcService
  └── ICouponAppService
      ├── LockCouponAsync ✅ 已实现
      └── ReleaseCouponsAsync 🆕 新增（依赖 IUserCouponRepository）

Product.ProductGrpcService
  └── IProductInternalQueryService
      ├── GetSkuStockAsync 🆕 新增（查 StockBaseline）
      └── GetSpuDetailAsync 🆕 新增（查 SPU + SKUs）

Points.PointsGrpcService
  └── IPointsInternalAppService
      └── ConfirmAsync 🆕 新增（复用 PointsOffsetAppService 模式）

Seller.SellerGrpcService
  └── ISellerInternalQueryService
      └── ValidateOwnershipAsync 🆕 新增
          ├── shop: ISellerInternalQueryService.GetShopInfoAsync ✅ 已实现
          ├── spu:  IProductAntiCorruptionService.GetSpuSellerIdAsync 🆕 同步新增
          └── order: IOrderAntiCorruptionService.GetOrderSellerIdAsync 🆕 同步新增

Order.OrderGrpcService
  └── GetOrderSellerId 🆕 新增 RPC（proto + 实现）
      └── IOrderInternalQueryService.GetOrderSellerIdAsync 🆕 新增
```

### 2.4 关键设计决策（brainstorming 阶段确认）

| 决策点 | 选择 | 理由 |
|---|---|---|
| Seller.ValidateSellerOwnership 的 resource_type 支持范围 | shop + spu + order 全部 | 完整支持卖家资源归属校验场景 |
| Promotion gRPC 服务依赖注入策略 | 注入 ICouponAppService | 与现有模式一致，业务逻辑可被 HTTP 路径复用 |
| Product.GetSkuStock 数据源 | DB StockBaseline | 与 GetSkuInfoAsync 数据源一致，事务一致性好 |
| 测试覆盖范围 | 单元测试为主 | 范围聚焦，集成测试在 P2-B 子项目 |
| 实现方案 | 方案 C：接口扩展 + 占位补齐 | 与 M4 双轨方案设计风格一致，范围可控 |

---

## 3. 组件详细设计

### 3.1 Order 域：SeckillOrderCreationService.PublishFailedEventAsync

**改动文件**：`Leno.Order.Application/Services/SeckillOrderCreationService.cs`

**变更点**：
1. 构造函数新增 `IEventBus eventBus` 参数
2. `PublishFailedEventAsync` 改为真实实现

```csharp
private async Task PublishFailedEventAsync(SeckillOrderCreatedIntegrationEvent evt, string reason, CancellationToken ct)
{
    var failedEvent = new SeckillOrderCreationFailedIntegrationEvent(
        evt.ActivityId, evt.SkuId, evt.UserId, evt.OrderId, evt.Quantity, reason);
    try
    {
        await _eventBus.PublishAsync(failedEvent, ct).ConfigureAwait(false);
        _logger.LogWarning("秒杀订单创建失败回执已发布 OrderId={OrderId} Reason={Reason}", evt.OrderId, reason);
    }
    catch (Exception ex)
    {
        // 失败回执发布失败仅记日志，不重抛（避免吞掉原始创建异常）
        _logger.LogError(ex, "秒杀失败回执发布失败 OrderId={OrderId}", evt.OrderId);
    }
}
```

**设计决策**：
- 失败回执发布自身的失败仅记日志不重抛（避免在 catch 块内再抛异常吞掉原始异常）
- 不经 Outbox：与原注释意图一致（失败路径无聚合状态变更需要同事务保证），且 IEventBus 实现本就是直接 RabbitMQ 发布
- Promotion 域消费者 `SeckillOrderCreationFailedEventConsumer` 已就绪，会回滚 Redis 库存 + DB 基线

### 3.2 Promotion 域：LockCoupon / ReleaseCoupons

**改动文件**：
- `Leno.Promotion.Application/IAppServices.cs` — `ICouponAppService` 新增 `ReleaseCouponsAsync`
- `Leno.Promotion.Application/Services/CouponAppService.cs` — 实现 `ReleaseCouponsAsync`
- `Leno.Promotion.Api/GrpcServices/PromotionGrpcService.cs` — 注入 `ICouponAppService`，改实现

**新增方法签名**：
```csharp
// ICouponAppService
Task ReleaseCouponsAsync(Guid orderId, CancellationToken ct = default);
```

**CouponAppService.ReleaseCouponsAsync 实现**：
```csharp
public async Task ReleaseCouponsAsync(Guid orderId, CancellationToken ct = default)
{
    var lockedCoupons = await _userCouponRepository.GetByLockedOrderIdAsync(orderId, ct)
        .ConfigureAwait(false);
    if (lockedCoupons is null || lockedCoupons.Count == 0)
    {
        return; // 无锁定券，幂等返回
    }
    foreach (var coupon in lockedCoupons)
    {
        coupon.Release(); // 领域方法：Locked → Unused（已过期则 → Expired）
    }
    await _unitOfWork.SaveEntitiesAsync(ct).ConfigureAwait(false);
}
```

**PromotionGrpcService 改动**：
```csharp
public override async Task<LockCouponResponse> LockCoupon(
    LockCouponRequest request, ServerCallContext context)
{
    var userId = Guid.Parse(request.UserId);
    var couponId = Guid.Parse(request.CouponId);
    var orderId = Guid.Parse(request.OrderId);
    await _couponAppService.LockCouponAsync(userId, couponId, orderId, context.CancellationToken)
        .ConfigureAwait(false);
    return new LockCouponResponse { Success = true };
}

public override async Task<ReleaseCouponsResponse> ReleaseCoupons(
    ReleaseCouponsRequest request, ServerCallContext context)
{
    var orderId = Guid.Parse(request.OrderId);
    await _couponAppService.ReleaseCouponsAsync(orderId, context.CancellationToken)
        .ConfigureAwait(false);
    return new ReleaseCouponsResponse { Success = true };
}
```

**错误处理**：领域异常（如状态机违反）由 gRPC 拦截器统一捕获转 `FailedPrecondition`，沿用现有 M4 模式。

### 3.3 Product 域：GetSkuStock / GetProductDetail

**改动文件**：
- `Leno.Product.Application/IProductInternalQueryService.cs` — 新增 2 个方法
- `Leno.Product.Application/SkuStockResultDto.cs`（新建）+ `SpuDetailResultDto.cs`（新建）
- `Leno.Product.Infrastructure/Services/ProductInternalQueryService.cs` — 实现 2 个方法
- `Leno.Product.Api/GrpcServices/ProductGrpcService.cs` — 改实现

**新增方法签名**：
```csharp
// IProductInternalQueryService
Task<SkuStockResultDto?> GetSkuStockAsync(Guid skuId, CancellationToken ct = default);
Task<SpuDetailResultDto?> GetSpuDetailAsync(Guid spuId, CancellationToken ct = default);
```

**DTO 定义**：
```csharp
public sealed record SkuStockResultDto(Guid SkuId, int Available, int Reserved);

public sealed record SpuDetailResultDto(
    Guid SpuId, Guid SellerId, Guid? ShopId, string Title, string Subtitle,
    string MainImageUrl, string Description,
    IReadOnlyList<SpuSkuDto> Skus);

public sealed record SpuSkuDto(
    Guid SkuId, string SkuCode, string Title, string MainImageUrl,
    decimal Price, string Currency, int Stock, string Status);
```

**ProductInternalQueryService 实现**：
```csharp
public async Task<SkuStockResultDto?> GetSkuStockAsync(Guid skuId, CancellationToken ct = default)
{
    var baseline = await _dbContext.StockBaselines
        .AsNoTracking()
        .FirstOrDefaultAsync(b => b.SkuId == skuId, ct)
        .ConfigureAwait(false);
    if (baseline is null) return null;
    return new SkuStockResultDto(skuId, baseline.AvailableQty, baseline.ReservedQty);
}

public async Task<SpuDetailResultDto?> GetSpuDetailAsync(Guid spuId, CancellationToken ct = default)
{
    var spu = await _dbContext.SPUs
        .AsNoTracking()
        .Include(s => s.SKUs)
        .FirstOrDefaultAsync(s => s.Id == spuId, ct)
        .ConfigureAwait(false);
    if (spu is null) return null;
    return new SpuDetailResultDto(
        spu.Id, spu.SellerId, spu.ShopId, spu.Title, spu.Subtitle,
        spu.MainImageUrl, spu.Description ?? string.Empty,
        spu.SKUs.Select(k => new SpuSkuDto(
            k.Id, k.SkuCode, k.Title, k.MainImageUrl,
            k.Price, "CNY", k.StockQty, k.Status.ToString())).ToList());
}
```

**ProductGrpcService 改动**：
```csharp
public override async Task<SkuStock> GetSkuStock(
    GetSkuStockRequest request, ServerCallContext context)
{
    var skuId = ResolveSkuId(request.SkuIdStr, request.SkuId);
    var dto = await _queryService.GetSkuStockAsync(skuId, context.CancellationToken)
        .ConfigureAwait(false);
    if (dto is null)
    {
        throw new RpcException(new Status(StatusCode.NotFound, $"SKU stock {skuId} not found"));
    }
    return new SkuStock
    {
        SkuId = request.SkuId,
        SkuIdStr = dto.SkuId.ToString(),
        Available = dto.Available,
        Reserved = dto.Reserved
    };
}

public override async Task<ProductDetail> GetProductDetail(
    GetProductDetailRequest request, ServerCallContext context)
{
    var spuId = ResolveSpuId(request.SpuIdStr, request.SpuId);
    var dto = await _queryService.GetSpuDetailAsync(spuId, context.CancellationToken)
        .ConfigureAwait(false);
    if (dto is null)
    {
        throw new RpcException(new Status(StatusCode.NotFound, $"SPU {spuId} not found"));
    }
    var detail = new ProductDetail
    {
        SpuId = request.SpuId,
        SpuIdStr = dto.SpuId.ToString(),
        Title = dto.Title,
        Description = dto.Description,
        SellerId = (long)dto.SellerId.GetHashCode(), // 保留 POC 简化映射（双轨向后兼容）
        SellerIdStr = dto.SellerId.ToString()
    };
    foreach (var sku in dto.Skus)
    {
        detail.Skus.Add(new SkuInfo
        {
            SkuId = (long)sku.SkuId.GetHashCode(),
            SkuIdStr = sku.SkuId.ToString(),
            Title = sku.Title,
            MainImage = sku.MainImageUrl,
            PriceCents = (long)(sku.Price * 100),
            Currency = sku.Currency,
            Stock = sku.Stock,
            Status = sku.Status
        });
    }
    return detail;
}
```

**设计决策**：保留 `(long)Guid.GetHashCode()` 映射以维持向后兼容（这是 M4 双轨方案既定模式，本子项目不重构）。

### 3.4 Points 域：PointsGrpcService.Confirm

**改动文件**：
- `Leno.PointsMembership.Application/IPointsInternalAppService.cs` — 新增 `ConfirmAsync`
- `Leno.PointsMembership.Application/ConfirmPointsDto.cs`（新建 DTO，复用 `ReleasePointsDto` 模式）
- `Leno.PointsMembership.Application/Services/PointsInternalAppService.cs` — 实现 `ConfirmAsync`
- `Leno.PointsMembership.Api/GrpcServices/PointsGrpcService.cs` — 改实现

**新增方法签名**：
```csharp
// IPointsInternalAppService
Task ConfirmAsync(ConfirmPointsDto input, CancellationToken ct = default);
```

**DTO 定义**（复用 ReleasePointsDto 单字段模式）：
```csharp
public sealed record ConfirmPointsDto(Guid OrderId);
```

**PointsInternalAppService.ConfirmAsync 实现**（范本：`PointsOffsetAppService.ConfirmDeductAsync`）：
```csharp
public async Task ConfirmAsync(ConfirmPointsDto input, CancellationToken ct = default)
{
    var account = await _accountRepository.GetByFrozenOrderIdAsync(input.OrderId, ct)
        .ConfigureAwait(false);
    if (account is null)
    {
        throw new PointsDomainException($"未找到订单 {input.OrderId} 对应的积分冻结记录");
    }
    account.ConfirmDeduct(input.OrderId);
    await _unitOfWork.SaveEntitiesAsync(ct).ConfigureAwait(false);
}
```

**PointsGrpcService.Confirm 改动**：
```csharp
public override async Task<ConfirmResponse> Confirm(
    ConfirmRequest request, ServerCallContext context)
{
    if (!Guid.TryParse(request.OrderId, out var orderId))
    {
        throw new RpcException(new Status(StatusCode.InvalidArgument, $"Invalid order_id: {request.OrderId}"));
    }
    var input = new ConfirmPointsDto(orderId);
    await _internalAppService.ConfirmAsync(input, context.CancellationToken)
        .ConfigureAwait(false);
    return new ConfirmResponse { Success = true };
}
```

**错误处理**：
- `PointsDomainException`（未找到冻结记录）→ gRPC 拦截器转 `NotFound`
- `account.ConfirmDeduct` 内部状态校验（非冻结状态拒绝确认）→ 转 `FailedPrecondition`

### 3.5 Seller 域：SellerGrpcService.ValidateSellerOwnership

**改动文件**：
- `Leno.SellerShop.Application/ISellerInternalQueryService.cs` — 新增 `ValidateOwnershipAsync`
- `Leno.SellerShop.Application/InternalQueryServices/SellerInternalQueryService.cs` — 实现
- `Leno.SellerShop.Api/GrpcServices/SellerGrpcService.cs` — 改实现
- `Leno.SellerShop.Infrastructure/AntiCorruption/IProductAntiCorruptionService.cs` — 新增 `GetSpuSellerIdAsync`
- `Leno.SellerShop.Infrastructure/AntiCorruption/IOrderAntiCorruptionService.cs` — 新增 `GetOrderSellerIdAsync`
- `Leno.SellerShop.Infrastructure/AntiCorruption/GrpcProductAntiCorruptionClient.cs` — 实现 `GetSpuSellerIdAsync`
- `Leno.SellerShop.Infrastructure/AntiCorruption/GrpcOrderAntiCorruptionClient.cs` — 实现 `GetOrderSellerIdAsync`

**新增方法签名**：
```csharp
// ISellerInternalQueryService
Task<bool> ValidateOwnershipAsync(Guid sellerId, string resourceType, Guid resourceId, CancellationToken ct = default);
```

**resourceType 分支策略**：

| resourceType | 数据源 | 校验逻辑 |
|---|---|---|
| `shop` | `IShopAppService.GetMyShopAsync(sellerId)` 域内查询 | `shop != null && shop.SellerId == sellerId && shop.Id == resourceId` |
| `spu` | `IProductAntiCorruptionService.GetSpuSellerIdAsync(resourceId)` 跨域 | 反查 SPU 的 SellerId 与传入 sellerId 比对 |
| `order` | `IOrderAntiCorruptionService.GetOrderSellerIdAsync(resourceId)` 跨域 | 反查 Order 的 SellerId 与传入 sellerId 比对 |
| 其他 | — | 返回 `false` 并记 warning 日志 |

**SellerInternalQueryService 实现**：
```csharp
public async Task<bool> ValidateOwnershipAsync(
    Guid sellerId, string resourceType, Guid resourceId, CancellationToken ct = default)
{
    return resourceType switch
    {
        "shop" => await ValidateShopOwnershipAsync(sellerId, resourceId, ct),
        "spu"  => await ValidateSpuOwnershipAsync(sellerId, resourceId, ct),
        "order"=> await ValidateOrderOwnershipAsync(sellerId, resourceId, ct),
        _ => LogUnknownResourceType(resourceType)
    };
}

private async Task<bool> ValidateShopOwnershipAsync(Guid sellerId, Guid shopId, CancellationToken ct)
{
    var shop = await _shopAppService.GetMyShopAsync(sellerId, ct).ConfigureAwait(false);
    return shop is not null && shop.Id == shopId;
}

private async Task<bool> ValidateSpuOwnershipAsync(Guid sellerId, Guid spuId, CancellationToken ct)
{
    var spuSellerId = await _productAntiCorruption.GetSpuSellerIdAsync(spuId, ct).ConfigureAwait(false);
    return spuSellerId == sellerId;
}

private async Task<bool> ValidateOrderOwnershipAsync(Guid sellerId, Guid orderId, CancellationToken ct)
{
    var orderSellerId = await _orderAntiCorruption.GetOrderSellerIdAsync(orderId, ct).ConfigureAwait(false);
    return orderSellerId == sellerId;
}

private bool LogUnknownResourceType(string resourceType)
{
    _logger.LogWarning("未知 resource_type: {ResourceType}", resourceType);
    return false;
}
```

**防腐层实现**（基于 M4 双轨方案的 GrpcXxxAntiCorruptionClient 模式）：
```csharp
// GrpcProductAntiCorruptionClient 新增方法
public async Task<Guid?> GetSpuSellerIdAsync(Guid spuId, CancellationToken ct = default)
{
    return await ExecuteAsync("get_spu_seller", async token =>
    {
        var request = new GetProductDetailRequest { SpuIdStr = spuId.ToString() };
        var response = await _client.GetProductDetailAsync(request, metadata, cancellationToken: token)
            .ConfigureAwait(false);
        return Guid.TryParse(response.SellerIdStr, out var sellerId) ? sellerId : (Guid?)null;
    }, ct).ConfigureAwait(false);
}

// GrpcOrderAntiCorruptionClient 新增方法
public async Task<Guid?> GetOrderSellerIdAsync(Guid orderId, CancellationToken ct = default)
{
    return await ExecuteAsync("get_order_seller", async token =>
    {
        var request = new GetOrderSellerIdRequest { OrderIdStr = orderId.ToString() };
        var response = await _client.GetOrderSellerIdAsync(request, metadata, cancellationToken: token)
            .ConfigureAwait(false);
        return Guid.TryParse(response.SellerIdStr, out var sellerId) ? sellerId : (Guid?)null;
    }, ct).ConfigureAwait(false);
}
```

**SellerGrpcService.ValidateSellerOwnership 改动**：
```csharp
public override async Task<ValidateSellerOwnershipResponse> ValidateSellerOwnership(
    ValidateSellerOwnershipRequest request, ServerCallContext context)
{
    if (!Guid.TryParse(request.SellerId, out var sellerId))
    {
        throw new RpcException(new Status(StatusCode.InvalidArgument, $"Invalid seller_id: {request.SellerId}"));
    }
    if (!Guid.TryParse(request.ResourceId, out var resourceId))
    {
        throw new RpcException(new Status(StatusCode.InvalidArgument, $"Invalid resource_id: {request.ResourceId}"));
    }
    var isValid = await _queryService.ValidateOwnershipAsync(
        sellerId, request.ResourceType, resourceId, context.CancellationToken)
        .ConfigureAwait(false);
    return new ValidateSellerOwnershipResponse { IsValid = isValid };
}
```

**设计决策**：
- 防腐层调用失败时返回 null（不抛异常），由 SellerShop 域判 false，避免跨域故障导致卖家长时间无法操作
- 未知 `resourceType` 返回 false 并记 warning（fail-closed，安全优先）

### 3.6 Order 域：GetOrderSellerId RPC（为支持 Seller.ValidateSellerOwnership 同步新增）

**proto 改动**（`Leno.SharedContracts/Protos/order.proto`）：
```protobuf
service OrderInternalService {
  // ... 既有 RPC ...
  rpc GetOrderSellerId(GetOrderSellerIdRequest) returns (GetOrderSellerIdResponse);
}

message GetOrderSellerIdRequest {
  int64 order_id = 1 [deprecated = true];
  string order_id_str = 2;
}

message GetOrderSellerIdResponse {
  int64 seller_id = 1 [deprecated = true];
  string seller_id_str = 2;
}
```

**Order 域同步扩展**：
- `IOrderInternalQueryService` 新增 `GetOrderSellerIdAsync(Guid orderId)` 返回 `Guid?`
- `OrderInternalQueryService` 实现：查 `Order` 聚合的 `SellerId` 字段（已存在）
- `OrderGrpcService` 新增 `GetOrderSellerId` RPC handler

**buf breaking 检查**：新增 RPC + 新增 message 不破坏向后兼容，CI 中 `proto-lint-breaking` Job 会自动验证。

---

## 4. 数据流与错误处理

### 4.1 完整数据流（以秒杀失败回执为例）

```
[Order 域] SeckillOrderCreationService.CreateSeckillOrderAsync
  ├── SKU 不存在 / 异常 → PublishFailedEventAsync
  │     └── IEventBus.PublishAsync(SeckillOrderCreationFailedIntegrationEvent)
  │           └── RabbitMQ.Topic: leno.promotion.seckill.failed
  │                 └── [Promotion 域] SeckillOrderCreationFailedEventConsumer.ConsumeAsync
  │                       ├── MarkRolledBack() — 标记预占记录
  │                       ├── _stockService.RestoreAsync — 回滚 Redis 库存
  │                       ├── activity.RestoreStock — 回滚 DB 基线
  │                       └── SaveEntitiesAsync — 持久化 + Outbox
  └── 成功 → Order.MarkSeckillOrderCreated(evt.ActivityId) → Outbox 同事务发布
```

**幂等性**：
- Order 侧：`evt.OrderId` 由 Promotion 域预生成，重复消费时 `_orderRepository.AddAsync` 因主键冲突抛异常 → catch 块发布失败回执
- Promotion 侧：`SeckillOrderCreationFailedEventConsumer` 内部 `MarkRolledBack()` 是状态机方法，重复消费时已是 RolledBack 状态 → 状态校验失败 → 消费者抛异常 → MassTransit 重试至死信队列

### 4.2 错误处理矩阵

| 场景 | gRPC 状态码 | 处理位置 | 调用方感知 |
|---|---|---|---|
| `Guid.Parse` 失败（user_id/coupon_id/order_id 非法） | `InvalidArgument` | gRPC 服务方法 | 直接抛 `RpcException` |
| 资源不存在（SKU/SPU/Coupon/Order/Points 冻结记录） | `NotFound` | AppService / InternalQueryService 抛领域异常 → gRPC 拦截器转 | 调用方按 NotFound 处理 |
| 状态机违反（UserCoupon 非 Unused 锁定、PointsAccount 非冻结确认） | `FailedPrecondition` | 领域方法抛 `XxxDomainException` → gRPC 拦截器转 | 调用方按业务冲突处理 |
| 跨域防腐层调用失败（Product/Order gRPC 不可达） | — | 防腐层 ExecuteAsync 内部熔断器 → 返回 null | SellerShop `ValidateOwnershipAsync` 返回 false（fail-closed） |
| 失败回执发布自身失败（MQ 不可达） | — | `PublishFailedEventAsync` catch 块仅记日志 | 调用方不感知（避免吞原始异常） |
| 未知 `resourceType` | — | `LogUnknownResourceType` 返回 false | 调用方按"无权限"处理 |

### 4.3 事务边界

| 操作 | 事务边界 | Outbox |
|---|---|---|
| `SeckillOrderCreationService` 成功路径 | `_unitOfWork.SaveEntitiesAsync` 同事务 | ✅ Order.MarkSeckillOrderCreated 事件经 Outbox |
| `SeckillOrderCreationService` 失败路径 | 无聚合可挂事件 | ❌ 直接 `IEventBus.PublishAsync`（尽力而为） |
| `CouponAppService.LockCouponAsync` / `ReleaseCouponsAsync` | `_unitOfWork.SaveEntitiesAsync` 同事务 | ✅ UserCoupon 领域事件经 Outbox |
| `ProductInternalQueryService.GetSkuStock/GetSpuDetail` | `AsNoTracking` 只读查询 | ❌ 无写入 |
| `PointsInternalAppService.ConfirmAsync` | `_unitOfWork.SaveEntitiesAsync` 同事务 | ✅ PointsAccount.ConfirmDeduct 事件经 Outbox |
| `SellerInternalQueryService.ValidateOwnershipAsync` | 无写入（纯校验） | ❌ |
| `OrderInternalQueryService.GetOrderSellerIdAsync` | `AsNoTracking` 只读查询 | ❌ |

### 4.4 失败回执的"尽力而为"语义说明

`PublishFailedEventAsync` 直接调 `IEventBus.PublishAsync` 不经 Outbox，意味着：
- 若 Order 域进程在 `await _eventBus.PublishAsync` 之前崩溃 → 失败回执丢失，秒杀库存预占记录永久残留
- 这是**有意识的权衡**：失败路径无聚合可挂领域事件，强行 Outbox 化需引入"幽灵聚合"违反 DDD
- **缓解措施**：Promotion 域若有 `SeckillPreOccupationRecord` 表 + 超时清理后台服务则可兜底；若无则作为 P1-A 子项目的后续任务

---

## 5. 测试策略

### 5.1 单元测试覆盖矩阵

| # | 测试目标 | 测试项目 | 测试方法 |
|---|---------|---------|---------|
| 1 | `SeckillOrderCreationService.PublishFailedEventAsync` | `Leno.Order.Application.Tests` | `PublishFailedEvent_OnSuccess_PublishesEventWithCorrectFields`、`PublishFailedEvent_OnPublishFailure_DoesNotRethrow` |
| 2 | `CouponAppService.ReleaseCouponsAsync` | `Leno.Promotion.Application.Tests` | `ReleaseCoupons_NoLockedCoupons_ReturnsIdempotently`、`ReleaseCoupons_HasLockedCoupons_CallsReleaseAndSaves`、`ReleaseCoupons_MixedStatus_SkipsNonLocked` |
| 3 | `PromotionGrpcService.LockCoupon` / `ReleaseCoupons` | `Leno.Promotion.Api.Tests` | `LockCoupon_ValidInput_CallsAppService`、`LockCoupon_InvalidGuid_ThrowsInvalidArgument`、`ReleaseCoupons_ValidInput_CallsAppService` |
| 4 | `ProductInternalQueryService.GetSkuStockAsync` | `Leno.Product.Infrastructure.Tests` | `GetSkuStock_ExistingSku_ReturnsAvailableAndReserved`、`GetSkuStock_UnknownSku_ReturnsNull` |
| 5 | `ProductInternalQueryService.GetSpuDetailAsync` | `Leno.Product.Infrastructure.Tests` | `GetSpuDetail_ExistingSpu_ReturnsWithSkus`、`GetSpuDetail_UnknownSpu_ReturnsNull`、`GetSpuDetail_WithNoSkus_ReturnsEmptyList` |
| 6 | `ProductGrpcService.GetSkuStock` / `GetProductDetail` | `Leno.Product.Api.Tests` | `GetSkuStock_Found_ReturnsStock`、`GetSkuStock_NotFound_ThrowsRpcNotFound`、`GetProductDetail_Found_ReturnsWithSkus`、`GetProductDetail_InvalidGuid_ThrowsInvalidArgument` |
| 7 | `PointsInternalAppService.ConfirmAsync` | `Leno.PointsMembership.Application.Tests` | `Confirm_FrozenRecordExists_CallsConfirmDeductAndSaves`、`Confirm_NoFrozenRecord_ThrowsPointsDomainException` |
| 8 | `PointsGrpcService.Confirm` | `Leno.PointsMembership.Api.Tests` | `Confirm_ValidOrderId_ReturnsSuccess`、`Confirm_InvalidGuid_ThrowsInvalidArgument`、`Confirm_NotFound_PropagatesException` |
| 9 | `SellerInternalQueryService.ValidateOwnershipAsync` | `Leno.SellerShop.Application.Tests` | `ValidateOwnership_ShopOwned_ReturnsTrue`、`ValidateOwnership_ShopNotOwned_ReturnsFalse`、`ValidateOwnership_SpuOwned_ReturnsTrue`、`ValidateOwnership_SpuAntiCorruptionNull_ReturnsFalse`、`ValidateOwnership_OrderOwned_ReturnsTrue`、`ValidateOwnership_OrderAntiCorruptionNull_ReturnsFalse`、`ValidateOwnership_UnknownResourceType_ReturnsFalse` |
| 10 | `SellerGrpcService.ValidateSellerOwnership` | `Leno.SellerShop.Api.Tests` | `ValidateSellerOwnership_ValidInput_ReturnsResponse`、`ValidateSellerOwnership_InvalidSellerId_ThrowsInvalidArgument`、`ValidateSellerOwnership_InvalidResourceId_ThrowsInvalidArgument` |
| 11 | Order 域 `GetOrderSellerIdAsync` | `Leno.Order.Infrastructure.Tests` | `GetOrderSellerId_ExistingOrder_ReturnsSellerId`、`GetOrderSellerId_UnknownOrder_ReturnsNull` |
| 12 | 防腐层 `GrpcProductAntiCorruptionClient.GetSpuSellerIdAsync` / `GrpcOrderAntiCorruptionClient.GetOrderSellerIdAsync` | `Leno.SellerShop.Infrastructure.Tests` | `GetSpuSellerId_GrpcReturnsValid_CachesSellerId`、`GetSpuSellerId_GrpcFailure_ReturnsNull`、熔断器降级测试 |

**合计**：约 35 个新增测试方法，覆盖 7 处占位实现 + 4 处接口扩展 + 2 处防腐层新增。

### 5.2 测试模式

**AppService 层**：使用 xUnit + Moq，mock Repository/UnitOfWork/EventBus
```csharp
[Fact]
public async Task ReleaseCoupons_NoLockedCoupons_ReturnsIdempotently()
{
    var repo = new Mock<IUserCouponRepository>();
    repo.Setup(r => r.GetByLockedOrderIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
        .ReturnsAsync(new List<UserCoupon>(0));
    var uow = new Mock<IUnitOfWork>();
    var sut = new CouponAppService(repo.Object, uow.Object, Mock.Of<ILogger<CouponAppService>>());

    await sut.ReleaseCouponsAsync(OrderId, CancellationToken.None);

    uow.Verify(u => u.SaveEntitiesAsync(It.IsAny<CancellationToken>()), Times.Never);
}
```

**gRPC 服务层**：mock AppService / InternalQueryService，验证 `RpcException` 状态码
```csharp
[Fact]
public async Task GetProductDetail_NotFound_ThrowsRpcNotFound()
{
    var queryService = new Mock<IProductInternalQueryService>();
    queryService.Setup(q => q.GetSpuDetailAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
        .ReturnsAsync((SpuDetailResultDto?)null);
    var sut = new ProductGrpcService(queryService.Object, Mock.Of<ILogger<ProductGrpcService>>());

    var act = async () => await sut.GetProductDetail(
        new GetProductDetailRequest { SpuIdStr = Guid.NewGuid().ToString() },
        CreateServerCallContext());

    var ex = await Assert.ThrowsAsync<RpcException>(act);
    ex.StatusCode.Should().Be(StatusCode.NotFound);
}
```

**Infrastructure 层**：使用 EF Core InMemory provider（与既有 `Leno.Order.Infrastructure.Tests` 模式一致）
```csharp
[Fact]
public async Task GetSkuStock_ExistingSku_ReturnsAvailableAndReserved()
{
    using var ctx = CreateInMemoryContext();
    ctx.StockBaselines.Add(new StockBaseline(skuId, available: 100, reserved: 30));
    await ctx.SaveChangesAsync();
    var sut = new ProductInternalQueryService(ctx);

    var result = await sut.GetSkuStockAsync(skuId, CancellationToken.None);

    result.Should().NotBeNull();
    result!.Available.Should().Be(100);
    result.Reserved.Should().Be(30);
}
```

### 5.3 不在本子项目测试范围

- 跨服务集成测试（Testcontainers + 真实 RabbitMQ/SQL Server）
- 端到端秒杀流程测试
- proto breaking 检查（CI `proto-lint-breaking` Job 已覆盖）
- 占位符检查（CI `check-placeholders.sh` 已覆盖）

---

## 6. 改动文件清单

### 6.1 源代码改动（按域分组）

#### Order 域（4 文件）
| 文件 | 变更类型 |
|---|---|
| `Leno.Order.Application/Services/SeckillOrderCreationService.cs` | 修改：注入 `IEventBus`、实现 `PublishFailedEventAsync` |
| `Leno.Order.Application/IOrderInternalQueryService.cs` | 修改：新增 `GetOrderSellerIdAsync` 方法 |
| `Leno.Order.Infrastructure/Services/OrderInternalQueryService.cs` | 修改：实现 `GetOrderSellerIdAsync` |
| `Leno.Order.Api/GrpcServices/OrderGrpcService.cs` | 修改：新增 `GetOrderSellerId` RPC handler |

#### Promotion 域（3 文件）
| 文件 | 变更类型 |
|---|---|
| `Leno.Promotion.Application/IAppServices.cs` | 修改：`ICouponAppService` 新增 `ReleaseCouponsAsync` |
| `Leno.Promotion.Application/Services/CouponAppService.cs` | 修改：实现 `ReleaseCouponsAsync` |
| `Leno.Promotion.Api/GrpcServices/PromotionGrpcService.cs` | 修改：注入 `ICouponAppService`、改 `LockCoupon`/`ReleaseCoupons` 实现 |

#### Product 域（5 文件）
| 文件 | 变更类型 |
|---|---|
| `Leno.Product.Application/IProductInternalQueryService.cs` | 修改：新增 `GetSkuStockAsync` + `GetSpuDetailAsync` |
| `Leno.Product.Application/SkuStockResultDto.cs` | 新建 |
| `Leno.Product.Application/SpuDetailResultDto.cs` | 新建 |
| `Leno.Product.Infrastructure/Services/ProductInternalQueryService.cs` | 修改：实现 2 个新方法 |
| `Leno.Product.Api/GrpcServices/ProductGrpcService.cs` | 修改：改 `GetSkuStock`/`GetProductDetail` 实现 |

#### Points 域（4 文件）
| 文件 | 变更类型 |
|---|---|
| `Leno.PointsMembership.Application/IPointsInternalAppService.cs` | 修改：新增 `ConfirmAsync` |
| `Leno.PointsMembership.Application/ConfirmPointsDto.cs` | 新建 |
| `Leno.PointsMembership.Application/Services/PointsInternalAppService.cs` | 修改：实现 `ConfirmAsync` |
| `Leno.PointsMembership.Api/GrpcServices/PointsGrpcService.cs` | 修改：改 `Confirm` 实现 |

#### Seller 域（7 文件）
| 文件 | 变更类型 |
|---|---|
| `Leno.SellerShop.Application/ISellerInternalQueryService.cs` | 修改：新增 `ValidateOwnershipAsync` |
| `Leno.SellerShop.Application/InternalQueryServices/SellerInternalQueryService.cs` | 修改：实现 + 注入跨域防腐层 |
| `Leno.SellerShop.Api/GrpcServices/SellerGrpcService.cs` | 修改：改 `ValidateSellerOwnership` 实现 |
| `Leno.SellerShop.Infrastructure/AntiCorruption/IProductAntiCorruptionService.cs` | 修改：新增 `GetSpuSellerIdAsync` |
| `Leno.SellerShop.Infrastructure/AntiCorruption/IOrderAntiCorruptionService.cs` | 修改：新增 `GetOrderSellerIdAsync` |
| `Leno.SellerShop.Infrastructure/AntiCorruption/GrpcProductAntiCorruptionClient.cs` | 修改：实现 `GetSpuSellerIdAsync` |
| `Leno.SellerShop.Infrastructure/AntiCorruption/GrpcOrderAntiCorruptionClient.cs` | 修改：实现 `GetOrderSellerIdAsync` |

#### 共享契约（2 文件）
| 文件 | 变更类型 |
|---|---|
| `Leno.SharedContracts/Protos/order.proto` | 修改：新增 `GetOrderSellerId` RPC + message |
| `Leno.SharedContracts.Grpc/Generated/*` | 重新生成（`buf generate`） |

#### 测试项目（11 文件）
| 文件 | 变更类型 |
|---|---|
| `Leno.Order.Application.Tests/SeckillOrderCreationServiceTests.cs` | 修改：补 `PublishFailedEvent` 测试 |
| `Leno.Order.Infrastructure.Tests/OrderInternalQueryServiceTests.cs` | 新建 |
| `Leno.Promotion.Application.Tests/CouponAppServiceTests.cs` | 新建 |
| `Leno.Promotion.Api.Tests/PromotionGrpcServiceTests.cs` | 新建 |
| `Leno.Product.Infrastructure.Tests/ProductInternalQueryServiceTests.cs` | 新建 |
| `Leno.Product.Api.Tests/ProductGrpcServiceTests.cs` | 修改：补 `GetSkuStock`/`GetProductDetail` 测试 |
| `Leno.PointsMembership.Application.Tests/PointsInternalAppServiceTests.cs` | 修改：补 `ConfirmAsync` 测试 |
| `Leno.PointsMembership.Api.Tests/PointsGrpcServiceTests.cs` | 新建 |
| `Leno.SellerShop.Application.Tests/SellerInternalQueryServiceTests.cs` | 修改：补 `ValidateOwnershipAsync` 测试 |
| `Leno.SellerShop.Api.Tests/SellerGrpcServiceTests.cs` | 新建 |
| `Leno.SellerShop.Infrastructure.Tests/SellerShopAntiCorruptionTests.cs` | 新建（替换 SmokeTests） |

**合计**：约 25 个文件改动（11 个新建 + 14 个修改），新增约 35 个测试方法。

---

## 7. 验收标准

### 7.1 功能验收
1. ✅ `bash scripts/check-placeholders.sh` 通过，0 占位符
2. ✅ 所有 7 处原占位实现已被真实业务逻辑替换
3. ✅ gRPC 调用方可成功调用 7 个 RPC（LockCoupon/ReleaseCoupons/GetSkuStock/GetProductDetail/Confirm/ValidateSellerOwnership/GetOrderSellerId）
4. ✅ Order 域 `GetOrderSellerId` RPC 可被 SellerShop 防腐层成功调用

### 7.2 质量验收
5. ✅ `dotnet build Leno.slnx` 在 Release 配置下零警告零错误
6. ✅ `dotnet test` 全部通过
7. ✅ 新增测试方法数 ≥ 30
8. ✅ `dotnet format Leno.slnx --verify-no-changes` 通过
9. ✅ CI `proto-lint-breaking` Job 通过（新增 RPC 不破坏向后兼容）
10. ✅ `buf generate` 生成的代码与 `Leno.SharedContracts.Grpc/Generated/` 一致

### 7.3 设计约束验收
11. ✅ 不新建聚合根、不新建仓储接口
12. ✅ 不修改既有 proto 字段（仅新增 RPC + message）
13. ✅ 失败回执发布失败仅记日志不重抛（§4.2）
14. ✅ 跨域防腐层失败返回 null，`ValidateOwnershipAsync` 返回 false（fail-closed，§4.2）

### 7.4 Git 提交规范
15. ✅ 实现完成后提交到 git 仓库，提交说明采用中文
16. ✅ 推送到远程仓库

---

## 8. 风险与权衡

### 8.1 已知风险

| 风险 | 影响 | 缓解措施 |
|---|---|---|
| 失败回执不经 Outbox，进程崩溃时丢失 | 秒杀库存预占记录残留 | P1-A 子项目评估引入 `SeckillPreOccupationRecord` 超时清理后台服务 |
| 跨域防腐层失败返回 null 导致归属校验 false | 卖家在下游故障时无法操作自身资源 | fail-closed 是安全优先策略，下游恢复后自动恢复 |
| 保留 `GetHashCode()` 映射 | int64 字段值不可逆，旧客户端回退时得到错误 Guid | M4 双轨方案既有问题，本子项目不重构；新客户端优先读 string 字段 |
| 新增 `GetOrderSellerId` RPC 扩大攻击面 | 内部 RPC 鉴权依赖 `GrpcInternalKeyInterceptor` | 沿用现有 M4 鉴权模式，不引入新风险 |

### 8.2 不在本子项目范围的后续工作

- 秒杀预占记录超时清理后台服务（P1-A）
- M4 双轨方案 `GetHashCode()` 映射重构（P2）
- 跨服务端到端集成测试（P2-B）
- CI/CD 流水线改造（P2-A）
- 密钥与凭证治理（P0-B）

---

## 9. 实现顺序建议

按依赖关系建议实现顺序：

1. **proto 与生成代码**：`order.proto` 新增 RPC + `buf generate`
2. **Order 域**：`SeckillOrderCreationService` + `OrderInternalQueryService.GetOrderSellerIdAsync` + `OrderGrpcService.GetOrderSellerId`
3. **Product 域**：`ProductInternalQueryService` 2 个新方法 + `ProductGrpcService` 2 个改实现
4. **Promotion 域**：`CouponAppService.ReleaseCouponsAsync` + `PromotionGrpcService` 2 个改实现
5. **Points 域**：`PointsInternalAppService.ConfirmAsync` + `PointsGrpcService.Confirm`
6. **Seller 域**：防腐层 2 个新方法 + `SellerInternalQueryService.ValidateOwnershipAsync` + `SellerGrpcService.ValidateSellerOwnership`
7. **测试**：按 §5.1 矩阵逐项补齐
8. **验证**：`check-placeholders.sh` + `dotnet build` + `dotnet test` + `dotnet format --verify-no-changes`
9. **Git 提交与推送**
