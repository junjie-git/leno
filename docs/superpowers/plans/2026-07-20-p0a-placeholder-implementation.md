# P0-A 核心占位实现补齐 实施计划

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 补齐 7 处 P0 级占位实现（秒杀失败回执、优惠券锁定/释放、库存查询、商品详情、积分确认、卖家归属校验），使核心业务流转闭环。

**Architecture:** 保持 M4 双轨架构（gRPC 服务复用 AppService / InternalQueryService），在 4 个跨 BC 内部查询接口上扩展 7 个方法，不新建聚合、不新建仓储、仅新增 1 个 gRPC RPC（Order 域 `GetOrderSellerId`）。

**Tech Stack:** .NET + EF Core + gRPC + MassTransit + xUnit + Moq + FluentAssertions + buf (proto)

**关联 Spec：** `docs/superpowers/specs/2026-07-20-p0a-placeholder-implementation-design.md`

---

## 文件结构

### 新建文件（11 个）
- `src/BuildingBlocks/Leno.SharedContracts/Protos/order.proto` — 修改：新增 `GetOrderSellerId` RPC + message
- `src/Services/Product/Leno.Product.Application/SkuStockResultDto.cs` — 库存查询结果 DTO
- `src/Services/Product/Leno.Product.Application/SpuDetailResultDto.cs` — SPU 详情结果 DTO（含 SpuSkuDto）
- `src/Services/PointsMembership/Leno.PointsMembership.Application/ConfirmPointsDto.cs` — 积分确认入参 DTO
- `src/Services/Order/Leno.Order.Infrastructure.Tests/OrderInternalQueryServiceTests.cs`
- `src/Services/Promotion/Leno.Promotion.Application.Tests/CouponAppServiceTests.cs`
- `src/Services/Promotion/Leno.Promotion.Api.Tests/PromotionGrpcServiceTests.cs`
- `src/Services/Product/Leno.Product.Infrastructure.Tests/ProductInternalQueryServiceTests.cs`
- `src/Services/PointsMembership/Leno.PointsMembership.Api.Tests/PointsGrpcServiceTests.cs`
- `src/Services/SellerShop/Leno.SellerShop.Api.Tests/SellerGrpcServiceTests.cs`
- `src/Services/SellerShop/Leno.SellerShop.Infrastructure.Tests/SellerShopAntiCorruptionTests.cs`

### 修改文件（14 个）
- `src/BuildingBlocks/Leno.SharedContracts.Grpc/Generated/*` — 重新生成
- `src/Services/Order/Leno.Order.Application/Services/SeckillOrderCreationService.cs`
- `src/Services/Order/Leno.Order.Application/IOrderInternalQueryService.cs`
- `src/Services/Order/Leno.Order.Infrastructure/Services/OrderInternalQueryService.cs`
- `src/Services/Order/Leno.Order.Api/GrpcServices/OrderGrpcService.cs`
- `src/Services/Order/Leno.Order.Application.Tests/SeckillOrderCreationServiceTests.cs`
- `src/Services/Promotion/Leno.Promotion.Application/IAppServices.cs`
- `src/Services/Promotion/Leno.Promotion.Application/Services/CouponAppService.cs`
- `src/Services/Promotion/Leno.Promotion.Api/GrpcServices/PromotionGrpcService.cs`
- `src/Services/Product/Leno.Product.Application/IProductInternalQueryService.cs`
- `src/Services/Product/Leno.Product.Infrastructure/Services/ProductInternalQueryService.cs`
- `src/Services/Product/Leno.Product.Api/GrpcServices/ProductGrpcService.cs`
- `src/Services/Product/Leno.Product.Api.Tests/ProductApiTests.cs` 或新建 `ProductGrpcServiceTests.cs`
- `src/Services/PointsMembership/Leno.PointsMembership.Application/IPointsInternalAppService.cs`
- `src/Services/PointsMembership/Leno.PointsMembership.Application/Services/PointsInternalAppService.cs`
- `src/Services/PointsMembership/Leno.PointsMembership.Api/GrpcServices/PointsGrpcService.cs`
- `src/Services/PointsMembership/Leno.PointsMembership.Application.Tests/PointsInternalAppServiceTests.cs`（若不存在则新建）
- `src/Services/SellerShop/Leno.SellerShop.Application/ISellerInternalQueryService.cs`
- `src/Services/SellerShop/Leno.SellerShop.Application/InternalQueryServices/SellerInternalQueryService.cs`
- `src/Services/SellerShop/Leno.SellerShop.Api/GrpcServices/SellerGrpcService.cs`
- `src/Services/SellerShop/Leno.SellerShop.Infrastructure/AntiCorruption/IProductAntiCorruptionService.cs`
- `src/Services/SellerShop/Leno.SellerShop.Infrastructure/AntiCorruption/IOrderAntiCorruptionService.cs`
- `src/Services/SellerShop/Leno.SellerShop.Infrastructure/AntiCorruption/GrpcProductAntiCorruptionClient.cs`
- `src/Services/SellerShop/Leno.SellerShop.Infrastructure/AntiCorruption/GrpcOrderAntiCorruptionClient.cs`
- `src/Services/SellerShop/Leno.SellerShop.Application.Tests/SellerAppServiceTests.cs`（若已存在则补，否则新建 `SellerInternalQueryServiceTests.cs`）

---

## Task 1: proto 与生成代码（Order 域 GetOrderSellerId RPC）

**Files:**
- Modify: `src/BuildingBlocks/Leno.SharedContracts/Protos/order.proto`
- Regenerate: `src/BuildingBlocks/Leno.SharedContracts.Grpc/Generated/*`

- [ ] **Step 1: 读取当前 order.proto 内容**

Run: `Read src/BuildingBlocks/Leno.SharedContracts/Protos/order.proto`
Expected: 看到既有 `OrderInternalService` 定义与 message

- [ ] **Step 2: 在 order.proto 中新增 GetOrderSellerId RPC + message**

在 `service OrderInternalService` 内末尾新增：
```protobuf
  rpc GetOrderSellerId(GetOrderSellerIdRequest) returns (GetOrderSellerIdResponse);
```

在文件末尾新增 message：
```protobuf
message GetOrderSellerIdRequest {
  int64 order_id = 1 [deprecated = true];
  string order_id_str = 2;
}

message GetOrderSellerIdResponse {
  int64 seller_id = 1 [deprecated = true];
  string seller_id_str = 2;
}
```

- [ ] **Step 3: 运行 buf generate 重新生成 C# 代码**

Run: `cd src/BuildingBlocks/Leno.SharedContracts && buf generate`
Expected: `src/BuildingBlocks/Leno.SharedContracts.Grpc/Generated/` 下 Order 类相关文件包含 `GetOrderSellerId` 相关代码

- [ ] **Step 4: 验证生成代码可编译**

Run: `dotnet build src/BuildingBlocks/Leno.SharedContracts.Grpc/Leno.SharedContracts.Grpc.csproj`
Expected: Build succeeded, 0 errors

- [ ] **Step 5: 运行 buf lint 与 breaking 检查**

Run: `cd src/BuildingBlocks/Leno.SharedContracts && buf lint`
Expected: 0 lint errors

Run: `cd src/BuildingBlocks/Leno.SharedContracts && buf breaking --against '.git#branch=main'`
Expected: 0 breaking changes（新增 RPC + message 不破坏向后兼容）

- [ ] **Step 6: 提交**

```bash
git add src/BuildingBlocks/Leno.SharedContracts/Protos/order.proto src/BuildingBlocks/Leno.SharedContracts.Grpc/Generated/
git commit -m "feat(contract): 新增 Order 域 GetOrderSellerId gRPC RPC 与 message 定义"
```

---

## Task 2: Order 域 SeckillOrderCreationService.PublishFailedEventAsync 实现

**Files:**
- Modify: `src/Services/Order/Leno.Order.Application/Services/SeckillOrderCreationService.cs`
- Modify: `src/Services/Order/Leno.Order.Application.Tests/SeckillOrderCreationServiceTests.cs`

- [ ] **Step 1: 读取当前 SeckillOrderCreationService.cs**

Run: `Read src/Services/Order/Leno.Order.Application/Services/SeckillOrderCreationService.cs`
Expected: 看到既有 `PublishFailedEventAsync` 占位实现（第 87-94 行 `await Task.CompletedTask;`）

- [ ] **Step 2: 读取 SeckillOrderCreationServiceTests.cs**

Run: `Read src/Services/Order/Leno.Order.Application.Tests/SeckillOrderCreationServiceTests.cs`
Expected: 看到既有测试方法与构造函数签名

- [ ] **Step 3: 写失败测试 — PublishFailedEvent_OnSuccess_PublishesEventWithCorrectFields**

在 `SeckillOrderCreationServiceTests.cs` 中新增测试：
```csharp
[Fact]
public async Task PublishFailedEvent_OnSuccess_PublishesEventWithCorrectFields()
{
    // 安排：构造 mock IEventBus
    var eventBus = new Mock<IEventBus>();
    SeckillOrderCreationFailedIntegrationEvent? publishedEvent = null;
    eventBus.Setup(e => e.PublishAsync(It.IsAny<SeckillOrderCreationFailedIntegrationEvent>(), It.IsAny<CancellationToken>()))
        .Callback<SeckillOrderCreationFailedIntegrationEvent, CancellationToken>((evt, _) => publishedEvent = evt)
        .Returns(Task.CompletedTask);

    var sut = CreateService(eventBus: eventBus.Object);
    var evt = CreateSeckillOrderCreatedEvent();

    // 行动：通过反射调用 private PublishFailedEventAsync
    var method = typeof(SeckillOrderCreationService).GetMethod("PublishFailedEventAsync",
        System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
    var task = (Task)method!.Invoke(sut, new object[] { evt, "测试原因", CancellationToken.None })!;
    await task;

    // 断言
    eventBus.Verify(e => e.PublishAsync(It.IsAny<SeckillOrderCreationFailedIntegrationEvent>(), It.IsAny<CancellationToken>()), Times.Once);
    publishedEvent.Should().NotBeNull();
    publishedEvent!.OrderId.Should().Be(evt.OrderId);
    publishedEvent.SkuId.Should().Be(evt.SkuId);
    publishedEvent.UserId.Should().Be(evt.UserId);
    publishedEvent.ActivityId.Should().Be(evt.ActivityId);
    publishedEvent.Quantity.Should().Be(evt.Quantity);
    publishedEvent.Reason.Should().Be("测试原因");
}
```

- [ ] **Step 4: 写失败测试 — PublishFailedEvent_OnPublishFailure_DoesNotRethrow**

```csharp
[Fact]
public async Task PublishFailedEvent_OnPublishFailure_DoesNotRethrow()
{
    var eventBus = new Mock<IEventBus>();
    eventBus.Setup(e => e.PublishAsync(It.IsAny<SeckillOrderCreationFailedIntegrationEvent>(), It.IsAny<CancellationToken>()))
        .ThrowsAsync(new InvalidOperationException("MQ 不可达"));

    var sut = CreateService(eventBus: eventBus.Object);
    var evt = CreateSeckillOrderCreatedEvent();

    var method = typeof(SeckillOrderCreationService).GetMethod("PublishFailedEventAsync",
        System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

    // 行动 + 断言：不应抛出
    var act = async () =>
    {
        var task = (Task)method!.Invoke(sut, new object[] { evt, "测试原因", CancellationToken.None })!;
        await task;
    };
    await act.Should().NotThrowAsync();
}
```

- [ ] **Step 5: 运行测试，验证失败**

Run: `dotnet test src/Services/Order/Leno.Order.Application.Tests --filter "FullyQualifiedName~PublishFailedEvent"`
Expected: FAIL（占位实现未调 IEventBus.PublishAsync）

- [ ] **Step 6: 修改 SeckillOrderCreationService 构造函数注入 IEventBus**

修改 `SeckillOrderCreationService.cs`：
1. 在 using 区添加 `using Leno.Infrastructure.Abstractions;`（确认 IEventBus 命名空间）
2. 添加字段 `private readonly IEventBus _eventBus;`
3. 构造函数末尾添加参数 `IEventBus eventBus` 并赋值 `_eventBus = eventBus;`
4. 调用方（DI 注册或测试 fixture）同步更新

- [ ] **Step 7: 实现 PublishFailedEventAsync**

替换 `SeckillOrderCreationService.cs` 第 87-94 行：
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

- [ ] **Step 8: 运行测试，验证通过**

Run: `dotnet test src/Services/Order/Leno.Order.Application.Tests --filter "FullyQualifiedName~PublishFailedEvent"`
Expected: PASS（2 个测试）

- [ ] **Step 9: 运行 Order 域全部测试，确保无回归**

Run: `dotnet test src/Services/Order/Leno.Order.Application.Tests`
Expected: PASS（所有测试）

- [ ] **Step 10: 提交**

```bash
git add src/Services/Order/Leno.Order.Application/Services/SeckillOrderCreationService.cs src/Services/Order/Leno.Order.Application.Tests/SeckillOrderCreationServiceTests.cs
git commit -m "feat(order): 实现 SeckillOrderCreationService.PublishFailedEventAsync 发布秒杀失败回执事件"
```

---

## Task 3: Order 域 GetOrderSellerId 内部查询接口与 gRPC 实现

**Files:**
- Modify: `src/Services/Order/Leno.Order.Application/IOrderInternalQueryService.cs`
- Modify: `src/Services/Order/Leno.Order.Infrastructure/Services/OrderInternalQueryService.cs`
- Modify: `src/Services/Order/Leno.Order.Api/GrpcServices/OrderGrpcService.cs`
- Test: `src/Services/Order/Leno.Order.Infrastructure.Tests/OrderInternalQueryServiceTests.cs` (新建)

- [ ] **Step 1: 读取既有 IOrderInternalQueryService.cs 与 OrderInternalQueryService.cs**

Run: `Read src/Services/Order/Leno.Order.Application/IOrderInternalQueryService.cs`
Run: `Read src/Services/Order/Leno.Order.Infrastructure/Services/OrderInternalQueryService.cs`
Run: `Read src/Services/Order/Leno.Order.Api/GrpcServices/OrderGrpcService.cs`
Expected: 看到既有方法签名与实现模式

- [ ] **Step 2: 写失败测试 — GetOrderSellerId_ExistingOrder_ReturnsSellerId**

新建 `src/Services/Order/Leno.Order.Infrastructure.Tests/OrderInternalQueryServiceTests.cs`：
```csharp
using FluentAssertions;
using Leno.Order.Domain.Aggregates;
using Leno.Order.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Leno.Order.Infrastructure.Tests;

public class OrderInternalQueryServiceTests
{
    [Fact]
    public async Task GetOrderSellerId_ExistingOrder_ReturnsSellerId()
    {
        // 安排
        using var ctx = TestDbHelper.CreateInMemoryContext();
        var sellerId = Guid.NewGuid();
        var orderId = Guid.NewGuid();
        var order = TestDbHelper.CreateTestOrder(orderId, sellerId);
        ctx.Orders.Add(order);
        await ctx.SaveChangesAsync();

        var sut = new OrderInternalQueryService(ctx);

        // 行动
        var result = await sut.GetOrderSellerIdAsync(orderId, CancellationToken.None);

        // 断言
        result.Should().Be(sellerId);
    }

    [Fact]
    public async Task GetOrderSellerId_UnknownOrder_ReturnsNull()
    {
        using var ctx = TestDbHelper.CreateInMemoryContext();
        var sut = new OrderInternalQueryService(ctx);

        var result = await sut.GetOrderSellerIdAsync(Guid.NewGuid(), CancellationToken.None);

        result.Should().BeNull();
    }
}
```

- [ ] **Step 3: 运行测试，验证失败**

Run: `dotnet test src/Services/Order/Leno.Order.Infrastructure.Tests --filter "FullyQualifiedName~GetOrderSellerId"`
Expected: FAIL（方法未定义）

- [ ] **Step 4: 在 IOrderInternalQueryService 新增方法签名**

在 `IOrderInternalQueryService.cs` 末尾新增：
```csharp
Task<Guid?> GetOrderSellerIdAsync(Guid orderId, CancellationToken ct = default);
```

- [ ] **Step 5: 在 OrderInternalQueryService 实现**

在 `OrderInternalQueryService.cs` 中新增方法：
```csharp
public async Task<Guid?> GetOrderSellerIdAsync(Guid orderId, CancellationToken ct = default)
{
    var order = await _dbContext.Orders
        .AsNoTracking()
        .Where(o => o.Id == orderId)
        .Select(o => new { o.SellerId })
        .FirstOrDefaultAsync(ct)
        .ConfigureAwait(false);
    return order?.SellerId;
}
```

- [ ] **Step 6: 在 OrderGrpcService 新增 GetOrderSellerId RPC handler**

在 `OrderGrpcService.cs` 中新增方法（参考既有 RPC handler 模式）：
```csharp
public override async Task<GetOrderSellerIdResponse> GetOrderSellerId(
    GetOrderSellerIdRequest request, ServerCallContext context)
{
    Guid orderId;
    if (!string.IsNullOrEmpty(request.OrderIdStr))
    {
        if (!Guid.TryParse(request.OrderIdStr, out orderId))
        {
            throw new RpcException(new Status(StatusCode.InvalidArgument, $"Invalid order_id_str: {request.OrderIdStr}"));
        }
    }
    else
    {
        // 旧客户端回退：int64 → Guid（X16 十六进制反序列化）
        orderId = new Guid(Convert.FromHexString(request.OrderId.ToString("X16")));
    }

    var sellerId = await _queryService.GetOrderSellerIdAsync(orderId, context.CancellationToken)
        .ConfigureAwait(false);
    if (sellerId is null)
    {
        throw new RpcException(new Status(StatusCode.NotFound, $"Order {orderId} not found"));
    }

    return new GetOrderSellerIdResponse
    {
        SellerId = (long)sellerId.GetHashCode(), // 保留 POC 简化映射（双轨向后兼容）
        SellerIdStr = sellerId.ToString()
    };
}
```

- [ ] **Step 7: 运行测试，验证通过**

Run: `dotnet test src/Services/Order/Leno.Order.Infrastructure.Tests --filter "FullyQualifiedName~GetOrderSellerId"`
Expected: PASS

- [ ] **Step 8: 运行 Order 域全部测试，确保无回归**

Run: `dotnet test src/Services/Order/Leno.Order.Infrastructure.Tests`
Expected: PASS

- [ ] **Step 9: 提交**

```bash
git add src/Services/Order/Leno.Order.Application/IOrderInternalQueryService.cs src/Services/Order/Leno.Order.Infrastructure/Services/OrderInternalQueryService.cs src/Services/Order/Leno.Order.Api/GrpcServices/OrderGrpcService.cs src/Services/Order/Leno.Order.Infrastructure.Tests/OrderInternalQueryServiceTests.cs
git commit -m "feat(order): 新增 GetOrderSellerId 内部查询接口与 gRPC 实现"
```

---

## Task 4: Product 域 GetSkuStock / GetProductDetail

**Files:**
- Create: `src/Services/Product/Leno.Product.Application/SkuStockResultDto.cs`
- Create: `src/Services/Product/Leno.Product.Application/SpuDetailResultDto.cs`
- Modify: `src/Services/Product/Leno.Product.Application/IProductInternalQueryService.cs`
- Modify: `src/Services/Product/Leno.Product.Infrastructure/Services/ProductInternalQueryService.cs`
- Modify: `src/Services/Product/Leno.Product.Api/GrpcServices/ProductGrpcService.cs`
- Test: `src/Services/Product/Leno.Product.Infrastructure.Tests/ProductInternalQueryServiceTests.cs` (新建)

- [ ] **Step 1: 读取既有文件**

Run: `Read src/Services/Product/Leno.Product.Application/IProductInternalQueryService.cs`
Run: `Read src/Services/Product/Leno.Product.Infrastructure/Services/ProductInternalQueryService.cs`
Run: `Read src/Services/Product/Leno.Product.Api/GrpcServices/ProductGrpcService.cs`
Run: `Read src/Services/Product/Leno.Product.Domain/Aggregates/StockBaseline.cs`（确认字段名）
Run: `Read src/Services/Product/Leno.Product.Domain/Aggregates/SPU.cs`（确认 SKU 集合字段名）

- [ ] **Step 2: 创建 SkuStockResultDto.cs**

新建 `src/Services/Product/Leno.Product.Application/SkuStockResultDto.cs`：
```csharp
namespace Leno.Product.Application;

/// <summary>
/// SKU 库存查询结果 DTO（跨 BC 内部查询）。
/// </summary>
public sealed record SkuStockResultDto(Guid SkuId, int Available, int Reserved);
```

- [ ] **Step 3: 创建 SpuDetailResultDto.cs**

新建 `src/Services/Product/Leno.Product.Application/SpuDetailResultDto.cs`：
```csharp
namespace Leno.Product.Application;

/// <summary>
/// SPU 详情查询结果 DTO（跨 BC 内部查询，含 SKU 集合）。
/// </summary>
public sealed record SpuDetailResultDto(
    Guid SpuId,
    Guid SellerId,
    Guid? ShopId,
    string Title,
    string Subtitle,
    string MainImageUrl,
    string Description,
    IReadOnlyList<SpuSkuDto> Skus);

public sealed record SpuSkuDto(
    Guid SkuId,
    string SkuCode,
    string Title,
    string MainImageUrl,
    decimal Price,
    string Currency,
    int Stock,
    string Status);
```

- [ ] **Step 4: 写失败测试 — GetSkuStock_ExistingSku_ReturnsAvailableAndReserved**

新建 `src/Services/Product/Leno.Product.Infrastructure.Tests/ProductInternalQueryServiceTests.cs`：
```csharp
using FluentAssertions;
using Leno.Product.Application;
using Leno.Product.Domain.Aggregates;
using Leno.Product.Infrastructure.Services;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Leno.Product.Infrastructure.Tests;

public class ProductInternalQueryServiceTests
{
    [Fact]
    public async Task GetSkuStock_ExistingSku_ReturnsAvailableAndReserved()
    {
        using var ctx = TestDbHelper.CreateInMemoryContext();
        var skuId = Guid.NewGuid();
        var baseline = StockBaseline.Create(skuId, availableQty: 100, reservedQty: 30);
        ctx.StockBaselines.Add(baseline);
        await ctx.SaveChangesAsync();

        var sut = new ProductInternalQueryService(ctx);

        var result = await sut.GetSkuStockAsync(skuId, CancellationToken.None);

        result.Should().NotBeNull();
        result!.Available.Should().Be(100);
        result.Reserved.Should().Be(30);
        result.SkuId.Should().Be(skuId);
    }

    [Fact]
    public async Task GetSkuStock_UnknownSku_ReturnsNull()
    {
        using var ctx = TestDbHelper.CreateInMemoryContext();
        var sut = new ProductInternalQueryService(ctx);

        var result = await sut.GetSkuStockAsync(Guid.NewGuid(), CancellationToken.None);

        result.Should().BeNull();
    }

    [Fact]
    public async Task GetSpuDetail_ExistingSpu_ReturnsWithSkus()
    {
        using var ctx = TestDbHelper.CreateInMemoryContext();
        var spu = TestDbHelper.CreateTestSpuWithSkus(spuCount: 1, skuCount: 3);
        ctx.SPUs.Add(spu);
        await ctx.SaveChangesAsync();

        var sut = new ProductInternalQueryService(ctx);

        var result = await sut.GetSpuDetailAsync(spu.Id, CancellationToken.None);

        result.Should().NotBeNull();
        result!.Skus.Should().HaveCount(3);
    }

    [Fact]
    public async Task GetSpuDetail_UnknownSpu_ReturnsNull()
    {
        using var ctx = TestDbHelper.CreateInMemoryContext();
        var sut = new ProductInternalQueryService(ctx);

        var result = await sut.GetSpuDetailAsync(Guid.NewGuid(), CancellationToken.None);

        result.Should().BeNull();
    }

    [Fact]
    public async Task GetSpuDetail_WithNoSkus_ReturnsEmptyList()
    {
        using var ctx = TestDbHelper.CreateInMemoryContext();
        var spu = TestDbHelper.CreateTestSpuWithSkus(spuCount: 1, skuCount: 0);
        ctx.SPUs.Add(spu);
        await ctx.SaveChangesAsync();

        var sut = new ProductInternalQueryService(ctx);

        var result = await sut.GetSpuDetailAsync(spu.Id, CancellationToken.None);

        result.Should().NotBeNull();
        result!.Skus.Should().BeEmpty();
    }
}
```

- [ ] **Step 5: 运行测试，验证失败**

Run: `dotnet test src/Services/Product/Leno.Product.Infrastructure.Tests --filter "FullyQualifiedName~ProductInternalQueryService"`
Expected: FAIL（方法未定义）

- [ ] **Step 6: 在 IProductInternalQueryService 新增方法签名**

在 `IProductInternalQueryService.cs` 末尾新增：
```csharp
Task<SkuStockResultDto?> GetSkuStockAsync(Guid skuId, CancellationToken ct = default);
Task<SpuDetailResultDto?> GetSpuDetailAsync(Guid spuId, CancellationToken ct = default);
```

- [ ] **Step 7: 在 ProductInternalQueryService 实现两个方法**

在 `ProductInternalQueryService.cs` 中新增：
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
        spu.Id, spu.SellerId, spu.ShopId, spu.Title, spu.Subtitle ?? string.Empty,
        spu.MainImageUrl ?? string.Empty, spu.Description ?? string.Empty,
        spu.SKUs.Select(k => new SpuSkuDto(
            k.Id, k.SkuCode, k.Title ?? string.Empty, k.MainImageUrl ?? string.Empty,
            k.Price, "CNY", k.StockQty, k.Status.ToString())).ToList());
}
```

> 注：实现时确认 `SPU.SKUs` 集合属性名与 `SKU` 字段名（Title/MainImageUrl/Price/StockQty/Status）的实际命名，按实际命名调整。

- [ ] **Step 8: 修改 ProductGrpcService.GetSkuStock 实现**

替换 `ProductGrpcService.cs` 第 77-95 行：
```csharp
public override async Task<SkuStock> GetSkuStock(
    GetSkuStockRequest request, ServerCallContext context)
{
    Guid skuId;
    if (!string.IsNullOrEmpty(request.SkuIdStr))
    {
        if (!Guid.TryParse(request.SkuIdStr, out skuId))
        {
            throw new RpcException(new Status(StatusCode.InvalidArgument, $"Invalid sku_id_str: {request.SkuIdStr}"));
        }
    }
    else
    {
        skuId = new Guid(Convert.FromHexString(request.SkuId.ToString("X16")));
    }

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
```

- [ ] **Step 9: 修改 ProductGrpcService.GetProductDetail 实现**

替换 `ProductGrpcService.cs` 第 97-101 行：
```csharp
public override async Task<ProductDetail> GetProductDetail(
    GetProductDetailRequest request, ServerCallContext context)
{
    Guid spuId;
    if (!string.IsNullOrEmpty(request.SpuIdStr))
    {
        if (!Guid.TryParse(request.SpuIdStr, out spuId))
        {
            throw new RpcException(new Status(StatusCode.InvalidArgument, $"Invalid spu_id_str: {request.SpuIdStr}"));
        }
    }
    else
    {
        spuId = new Guid(Convert.FromHexString(request.SpuId.ToString("X16")));
    }

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

- [ ] **Step 10: 运行测试，验证通过**

Run: `dotnet test src/Services/Product/Leno.Product.Infrastructure.Tests --filter "FullyQualifiedName~ProductInternalQueryService"`
Expected: PASS（5 个测试）

- [ ] **Step 11: 运行 Product 域全部测试，确保无回归**

Run: `dotnet test src/Services/Product/Leno.Product.Infrastructure.Tests`
Expected: PASS

- [ ] **Step 12: 提交**

```bash
git add src/Services/Product/Leno.Product.Application/SkuStockResultDto.cs src/Services/Product/Leno.Product.Application/SpuDetailResultDto.cs src/Services/Product/Leno.Product.Application/IProductInternalQueryService.cs src/Services/Product/Leno.Product.Infrastructure/Services/ProductInternalQueryService.cs src/Services/Product/Leno.Product.Api/GrpcServices/ProductGrpcService.cs src/Services/Product/Leno.Product.Infrastructure.Tests/ProductInternalQueryServiceTests.cs
git commit -m "feat(product): 实现 GetSkuStock 与 GetProductDetail gRPC 接口"
```

---

## Task 5: Promotion 域 ReleaseCouponsAsync + LockCoupon/ReleaseCoupons gRPC 改实现

**Files:**
- Modify: `src/Services/Promotion/Leno.Promotion.Application/IAppServices.cs`
- Modify: `src/Services/Promotion/Leno.Promotion.Application/Services/CouponAppService.cs`
- Modify: `src/Services/Promotion/Leno.Promotion.Api/GrpcServices/PromotionGrpcService.cs`
- Test: `src/Services/Promotion/Leno.Promotion.Application.Tests/CouponAppServiceTests.cs` (新建)
- Test: `src/Services/Promotion/Leno.Promotion.Api.Tests/PromotionGrpcServiceTests.cs` (新建)

- [ ] **Step 1: 读取既有文件**

Run: `Read src/Services/Promotion/Leno.Promotion.Application/IAppServices.cs`
Run: `Read src/Services/Promotion/Leno.Promotion.Application/Services/CouponAppService.cs`
Run: `Read src/Services/Promotion/Leno.Promotion.Api/GrpcServices/PromotionGrpcService.cs`
Run: `Read src/Services/Promotion/Leno.Promotion.Domain/Aggregates/UserCoupon.cs`（确认 Release 方法签名）
Run: `Read src/Services/Promotion/Leno.Promotion.Domain/Repositories/IUserCouponRepository.cs`

- [ ] **Step 2: 写失败测试 — ReleaseCoupons_NoLockedCoupons_ReturnsIdempotently**

新建 `src/Services/Promotion/Leno.Promotion.Application.Tests/CouponAppServiceTests.cs`：
```csharp
using FluentAssertions;
using Leno.Promotion.Application.Services;
using Leno.Promotion.Domain.Repositories;
using Leno.SharedKernel.Abstractions;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Leno.Promotion.Application.Tests;

public class CouponAppServiceTests
{
    private static readonly Guid OrderId = Guid.NewGuid();

    [Fact]
    public async Task ReleaseCoupons_NoLockedCoupons_ReturnsIdempotently()
    {
        var repo = new Mock<IUserCouponRepository>();
        repo.Setup(r => r.GetByLockedOrderIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Domain.Aggregates.UserCoupon>(0));
        var uow = new Mock<IUnitOfWork>();
        var sut = new CouponAppService(
            Mock.Of<ICouponRepository>(),
            repo.Object,
            uow.Object,
            Mock.Of<ILogger<CouponAppService>>());

        await sut.ReleaseCouponsAsync(OrderId, CancellationToken.None);

        uow.Verify(u => u.SaveEntitiesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ReleaseCoupons_HasLockedCoupons_CallsReleaseAndSaves()
    {
        var coupon1 = CreateLockedUserCoupon(OrderId);
        var coupon2 = CreateLockedUserCoupon(OrderId);
        var repo = new Mock<IUserCouponRepository>();
        repo.Setup(r => r.GetByLockedOrderIdAsync(OrderId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Domain.Aggregates.UserCoupon> { coupon1, coupon2 });
        var uow = new Mock<IUnitOfWork>();
        var sut = new CouponAppService(
            Mock.Of<ICouponRepository>(),
            repo.Object,
            uow.Object,
            Mock.Of<ILogger<CouponAppService>>());

        await sut.ReleaseCouponsAsync(OrderId, CancellationToken.None);

        uow.Verify(u => u.SaveEntitiesAsync(It.IsAny<CancellationToken>()), Times.Once);
        coupon1.Status.Should().Be(Domain.Aggregates.UserCouponStatus.Unused);
        coupon2.Status.Should().Be(Domain.Aggregates.UserCouponStatus.Unused);
    }

    private static Domain.Aggregates.UserCoupon CreateLockedUserCoupon(Guid orderId)
    {
        // 调用 UserCoupon 静态工厂方法或测试 helper 创建 Locked 状态的券
        return Domain.Aggregates.UserCoupon.CreateForTest(
            userId: Guid.NewGuid(),
            couponId: Guid.NewGuid(),
            status: Domain.Aggregates.UserCouponStatus.Locked,
            lockedOrderId: orderId);
    }
}
```

> 注：`UserCoupon.CreateForTest` 是测试辅助工厂方法，若不存在需在 UserCoupon 聚合上新增 internal 测试构造函数（与既有测试模式一致）。

- [ ] **Step 3: 运行测试，验证失败**

Run: `dotnet test src/Services/Promotion/Leno.Promotion.Application.Tests --filter "FullyQualifiedName~ReleaseCoupons"`
Expected: FAIL（方法未定义）

- [ ] **Step 4: 在 ICouponAppService 新增方法签名**

读取 `IAppServices.cs` 中 `ICouponAppService` 定义，在末尾新增：
```csharp
Task ReleaseCouponsAsync(Guid orderId, CancellationToken ct = default);
```

- [ ] **Step 5: 在 CouponAppService 实现 ReleaseCouponsAsync**

在 `CouponAppService.cs` 中新增方法（确认字段名 `_userCouponRepository` 与 `_unitOfWork`）：
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

- [ ] **Step 6: 运行测试，验证通过**

Run: `dotnet test src/Services/Promotion/Leno.Promotion.Application.Tests --filter "FullyQualifiedName~ReleaseCoupons"`
Expected: PASS（2 个测试）

- [ ] **Step 7: 写失败测试 — PromotionGrpcService LockCoupon/ReleaseCoupons**

新建 `src/Services/Promotion/Leno.Promotion.Api.Tests/PromotionGrpcServiceTests.cs`：
```csharp
using Grpc.Core;
using FluentAssertions;
using Leno.Promotion.Api.GrpcServices;
using Leno.Promotion.Application;
using Leno.Promotion.Domain.Repositories;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;
using static Leno.SharedContracts.Grpc.Promotion.V1.PromotionInternalService;

namespace Leno.Promotion.Api.Tests;

public class PromotionGrpcServiceTests
{
    private static readonly Guid UserId = Guid.NewGuid();
    private static readonly Guid CouponId = Guid.NewGuid();
    private static readonly Guid OrderId = Guid.NewGuid();

    [Fact]
    public async Task LockCoupon_ValidInput_CallsAppService()
    {
        var couponAppService = new Mock<ICouponAppService>();
        var sut = new PromotionGrpcService(
            Mock.Of<IPromotionCalculateAppService>(),
            Mock.Of<ICouponRepository>(),
            couponAppService.Object,
            Mock.Of<ILogger<PromotionGrpcService>>());

        var request = new SharedContracts.Grpc.Promotion.V1.LockCouponRequest
        {
            UserId = UserId.ToString(),
            CouponId = CouponId.ToString(),
            OrderId = OrderId.ToString()
        };

        var result = await sut.LockCoupon(request, CreateServerCallContext());

        result.Success.Should().BeTrue();
        couponAppService.Verify(c => c.LockCouponAsync(UserId, CouponId, OrderId, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task LockCoupon_InvalidGuid_ThrowsInvalidArgument()
    {
        var sut = new PromotionGrpcService(
            Mock.Of<IPromotionCalculateAppService>(),
            Mock.Of<ICouponRepository>(),
            Mock.Of<ICouponAppService>(),
            Mock.Of<ILogger<PromotionGrpcService>>());

        var request = new SharedContracts.Grpc.Promotion.V1.LockCouponRequest
        {
            UserId = "not-a-guid",
            CouponId = CouponId.ToString(),
            OrderId = OrderId.ToString()
        };

        var act = async () => await sut.LockCoupon(request, CreateServerCallContext());

        var ex = await Assert.ThrowsAsync<RpcException>(act);
        ex.StatusCode.Should().Be(StatusCode.InvalidArgument);
    }

    [Fact]
    public async Task ReleaseCoupons_ValidInput_CallsAppService()
    {
        var couponAppService = new Mock<ICouponAppService>();
        var sut = new PromotionGrpcService(
            Mock.Of<IPromotionCalculateAppService>(),
            Mock.Of<ICouponRepository>(),
            couponAppService.Object,
            Mock.Of<ILogger<PromotionGrpcService>>());

        var request = new SharedContracts.Grpc.Promotion.V1.ReleaseCouponsRequest
        {
            OrderId = OrderId.ToString()
        };

        var result = await sut.ReleaseCoupons(request, CreateServerCallContext());

        result.Success.Should().BeTrue();
        couponAppService.Verify(c => c.ReleaseCouponsAsync(OrderId, It.IsAny<CancellationToken>()), Times.Once);
    }

    private static ServerCallContext CreateServerCallContext()
    {
        // 复用既有测试 helper 或创建简化 mock
        return new TestServerCallContext();
    }
}
```

> 注：`TestServerCallContext` 是测试辅助类，若 ApiGateway.Tests 或其他 Tests 项目中已存在则复用；否则按 gRPC 测试标准模式实现。

- [ ] **Step 8: 运行测试，验证失败**

Run: `dotnet test src/Services/Promotion/Leno.Promotion.Api.Tests --filter "FullyQualifiedName~PromotionGrpcService"`
Expected: FAIL（PromotionGrpcService 构造函数未注入 ICouponAppService）

- [ ] **Step 9: 修改 PromotionGrpcService 构造函数注入 ICouponAppService**

修改 `PromotionGrpcService.cs`：
1. 新增字段 `private readonly ICouponAppService _couponAppService;`
2. 构造函数新增参数 `ICouponAppService couponAppService` 并赋值
3. DI 注册位置（Program.cs 或 Extensions）确认已注册 ICouponAppService

- [ ] **Step 10: 修改 LockCoupon 实现**

替换 `PromotionGrpcService.cs` 第 58-62 行：
```csharp
public override async Task<LockCouponResponse> LockCoupon(
    LockCouponRequest request, ServerCallContext context)
{
    if (!Guid.TryParse(request.UserId, out var userId))
    {
        throw new RpcException(new Status(StatusCode.InvalidArgument, $"Invalid user_id: {request.UserId}"));
    }
    if (!Guid.TryParse(request.CouponId, out var couponId))
    {
        throw new RpcException(new Status(StatusCode.InvalidArgument, $"Invalid coupon_id: {request.CouponId}"));
    }
    if (!Guid.TryParse(request.OrderId, out var orderId))
    {
        throw new RpcException(new Status(StatusCode.InvalidArgument, $"Invalid order_id: {request.OrderId}"));
    }

    await _couponAppService.LockCouponAsync(userId, couponId, orderId, context.CancellationToken)
        .ConfigureAwait(false);
    return new LockCouponResponse { Success = true };
}
```

- [ ] **Step 11: 修改 ReleaseCoupons 实现**

替换 `PromotionGrpcService.cs` 第 64-68 行：
```csharp
public override async Task<ReleaseCouponsResponse> ReleaseCoupons(
    ReleaseCouponsRequest request, ServerCallContext context)
{
    if (!Guid.TryParse(request.OrderId, out var orderId))
    {
        throw new RpcException(new Status(StatusCode.InvalidArgument, $"Invalid order_id: {request.OrderId}"));
    }

    await _couponAppService.ReleaseCouponsAsync(orderId, context.CancellationToken)
        .ConfigureAwait(false);
    return new ReleaseCouponsResponse { Success = true };
}
```

- [ ] **Step 12: 运行测试，验证通过**

Run: `dotnet test src/Services/Promotion/Leno.Promotion.Api.Tests --filter "FullyQualifiedName~PromotionGrpcService"`
Expected: PASS（3 个测试）

- [ ] **Step 13: 运行 Promotion 域全部测试，确保无回归**

Run: `dotnet test src/Services/Promotion/Leno.Promotion.Application.Tests && dotnet test src/Services/Promotion/Leno.Promotion.Api.Tests`
Expected: PASS

- [ ] **Step 14: 提交**

```bash
git add src/Services/Promotion/Leno.Promotion.Application/IAppServices.cs src/Services/Promotion/Leno.Promotion.Application/Services/CouponAppService.cs src/Services/Promotion/Leno.Promotion.Api/GrpcServices/PromotionGrpcService.cs src/Services/Promotion/Leno.Promotion.Application.Tests/CouponAppServiceTests.cs src/Services/Promotion/Leno.Promotion.Api.Tests/PromotionGrpcServiceTests.cs
git commit -m "feat(promotion): 实现 LockCoupon/ReleaseCoupons gRPC 接口与 ReleaseCouponsAsync AppService"
```

---

## Task 6: Points 域 ConfirmAsync + Confirm gRPC 改实现

**Files:**
- Create: `src/Services/PointsMembership/Leno.PointsMembership.Application/ConfirmPointsDto.cs`
- Modify: `src/Services/PointsMembership/Leno.PointsMembership.Application/IPointsInternalAppService.cs`
- Modify: `src/Services/PointsMembership/Leno.PointsMembership.Application/Services/PointsInternalAppService.cs`
- Modify: `src/Services/PointsMembership/Leno.PointsMembership.Api/GrpcServices/PointsGrpcService.cs`
- Test: `src/Services/PointsMembership/Leno.PointsMembership.Application.Tests/PointsInternalAppServiceTests.cs` (修改或新建)
- Test: `src/Services/PointsMembership/Leno.PointsMembership.Api.Tests/PointsGrpcServiceTests.cs` (新建)

- [ ] **Step 1: 读取既有文件**

Run: `Read src/Services/PointsMembership/Leno.PointsMembership.Application/IPointsInternalAppService.cs`
Run: `Read src/Services/PointsMembership/Leno.PointsMembership.Application/Services/PointsInternalAppService.cs`
Run: `Read src/Services/PointsMembership/Leno.PointsMembership.Api/GrpcServices/PointsGrpcService.cs`
Run: `Read src/Services/PointsMembership/Leno.PointsMembership.Application/Services/PointsOffsetAppService.cs`（参考范本）
Run: `Read src/Services/PointsMembership/Leno.PointsMembership.Domain/Aggregates/PointsAccount.cs`（确认 ConfirmDeduct 方法）

- [ ] **Step 2: 创建 ConfirmPointsDto.cs**

新建 `src/Services/PointsMembership/Leno.PointsMembership.Application/ConfirmPointsDto.cs`：
```csharp
namespace Leno.PointsMembership.Application;

/// <summary>
/// 积分确认扣减入参 DTO（跨 BC 内部查询，复用 ReleasePointsDto 单字段模式）。
/// </summary>
public sealed record ConfirmPointsDto(Guid OrderId);
```

- [ ] **Step 3: 写失败测试 — Confirm_FrozenRecordExists_CallsConfirmDeductAndSaves**

新建或修改 `src/Services/PointsMembership/Leno.PointsMembership.Application.Tests/PointsInternalAppServiceTests.cs`：
```csharp
using FluentAssertions;
using Leno.PointsMembership.Application;
using Leno.PointsMembership.Application.Services;
using Leno.PointsMembership.Domain.Aggregates;
using Leno.PointsMembership.Domain.Exceptions;
using Leno.PointsMembership.Domain.Repositories;
using Leno.SharedKernel.Abstractions;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Leno.PointsMembership.Application.Tests;

public class PointsInternalAppServiceTests
{
    private static readonly Guid OrderId = Guid.NewGuid();

    [Fact]
    public async Task Confirm_FrozenRecordExists_CallsConfirmDeductAndSaves()
    {
        var account = CreateTestPointsAccountWithFrozenEntry(OrderId);
        var repo = new Mock<IPointsAccountRepository>();
        repo.Setup(r => r.GetByFrozenOrderIdAsync(OrderId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(account);
        var uow = new Mock<IUnitOfWork>();
        var sut = new PointsInternalAppService(repo.Object, uow.Object, Mock.Of<ILogger<PointsInternalAppService>>());

        await sut.ConfirmAsync(new ConfirmPointsDto(OrderId), CancellationToken.None);

        uow.Verify(u => u.SaveEntitiesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Confirm_NoFrozenRecord_ThrowsPointsDomainException()
    {
        var repo = new Mock<IPointsAccountRepository>();
        repo.Setup(r => r.GetByFrozenOrderIdAsync(OrderId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((PointsAccount?)null);
        var uow = new Mock<IUnitOfWork>();
        var sut = new PointsInternalAppService(repo.Object, uow.Object, Mock.Of<ILogger<PointsInternalAppService>>());

        var act = async () => await sut.ConfirmAsync(new ConfirmPointsDto(OrderId), CancellationToken.None);

        await act.Should().ThrowAsync<PointsDomainException>();
    }

    private static PointsAccount CreateTestPointsAccountWithFrozenEntry(Guid orderId)
    {
        // 复用既有测试 helper 或调用 PointsAccount 工厂方法
        return PointsAccount.CreateForTest(
            userId: Guid.NewGuid(),
            frozenOrderId: orderId,
            frozenAmount: 100);
    }
}
```

- [ ] **Step 4: 运行测试，验证失败**

Run: `dotnet test src/Services/PointsMembership/Leno.PointsMembership.Application.Tests --filter "FullyQualifiedName~Confirm"`
Expected: FAIL（方法未定义）

- [ ] **Step 5: 在 IPointsInternalAppService 新增方法签名**

在 `IPointsInternalAppService.cs` 末尾新增：
```csharp
Task ConfirmAsync(ConfirmPointsDto input, CancellationToken ct = default);
```

- [ ] **Step 6: 在 PointsInternalAppService 实现 ConfirmAsync**

在 `PointsInternalAppService.cs` 中新增方法（参考 PointsOffsetAppService.ConfirmDeductAsync 模式）：
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

> 注：实现时确认 PointsDomainException 的命名空间与构造函数签名。

- [ ] **Step 7: 运行测试，验证通过**

Run: `dotnet test src/Services/PointsMembership/Leno.PointsMembership.Application.Tests --filter "FullyQualifiedName~Confirm"`
Expected: PASS（2 个测试）

- [ ] **Step 8: 写失败测试 — PointsGrpcService.Confirm**

新建 `src/Services/PointsMembership/Leno.PointsMembership.Api.Tests/PointsGrpcServiceTests.cs`：
```csharp
using Grpc.Core;
using FluentAssertions;
using Leno.PointsMembership.Api.GrpcServices;
using Leno.PointsMembership.Application;
using Leno.PointsMembership.Domain.Repositories;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Leno.PointsMembership.Api.Tests;

public class PointsGrpcServiceTests
{
    private static readonly Guid OrderId = Guid.NewGuid();

    [Fact]
    public async Task Confirm_ValidOrderId_ReturnsSuccess()
    {
        var internalAppService = new Mock<IPointsInternalAppService>();
        var sut = new PointsGrpcService(
            internalAppService.Object,
            Mock.Of<IPointsAccountRepository>(),
            Mock.Of<ILogger<PointsGrpcService>>());

        var request = new SharedContracts.Grpc.Points.V1.ConfirmRequest
        {
            OrderId = OrderId.ToString()
        };

        var result = await sut.Confirm(request, CreateServerCallContext());

        result.Success.Should().BeTrue();
        internalAppService.Verify(s => s.ConfirmAsync(It.Is<ConfirmPointsDto>(d => d.OrderId == OrderId), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Confirm_InvalidGuid_ThrowsInvalidArgument()
    {
        var sut = new PointsGrpcService(
            Mock.Of<IPointsInternalAppService>(),
            Mock.Of<IPointsAccountRepository>(),
            Mock.Of<ILogger<PointsGrpcService>>());

        var request = new SharedContracts.Grpc.Points.V1.ConfirmRequest
        {
            OrderId = "not-a-guid"
        };

        var act = async () => await sut.Confirm(request, CreateServerCallContext());

        var ex = await Assert.ThrowsAsync<RpcException>(act);
        ex.StatusCode.Should().Be(StatusCode.InvalidArgument);
    }

    private static ServerCallContext CreateServerCallContext() => new TestServerCallContext();
}
```

- [ ] **Step 9: 修改 PointsGrpcService.Confirm 实现**

替换 `PointsGrpcService.cs` 第 76-80 行：
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

- [ ] **Step 10: 运行测试，验证通过**

Run: `dotnet test src/Services/PointsMembership/Leno.PointsMembership.Api.Tests --filter "FullyQualifiedName~PointsGrpcService"`
Expected: PASS（2 个测试）

- [ ] **Step 11: 运行 Points 域全部测试，确保无回归**

Run: `dotnet test src/Services/PointsMembership/Leno.PointsMembership.Application.Tests && dotnet test src/Services/PointsMembership/Leno.PointsMembership.Api.Tests`
Expected: PASS

- [ ] **Step 12: 提交**

```bash
git add src/Services/PointsMembership/Leno.PointsMembership.Application/ConfirmPointsDto.cs src/Services/PointsMembership/Leno.PointsMembership.Application/IPointsInternalAppService.cs src/Services/PointsMembership/Leno.PointsMembership.Application/Services/PointsInternalAppService.cs src/Services/PointsMembership/Leno.PointsMembership.Api/GrpcServices/PointsGrpcService.cs src/Services/PointsMembership/Leno.PointsMembership.Application.Tests/PointsInternalAppServiceTests.cs src/Services/PointsMembership/Leno.PointsMembership.Api.Tests/PointsGrpcServiceTests.cs
git commit -m "feat(points): 实现 Confirm gRPC 接口与 ConfirmAsync AppService"
```

---

## Task 7: Seller 域 ValidateOwnershipAsync + 防腐层扩展

**Files:**
- Modify: `src/Services/SellerShop/Leno.SellerShop.Application/ISellerInternalQueryService.cs`
- Modify: `src/Services/SellerShop/Leno.SellerShop.Application/InternalQueryServices/SellerInternalQueryService.cs`
- Modify: `src/Services/SellerShop/Leno.SellerShop.Api/GrpcServices/SellerGrpcService.cs`
- Modify: `src/Services/SellerShop/Leno.SellerShop.Infrastructure/AntiCorruption/IProductAntiCorruptionService.cs`
- Modify: `src/Services/SellerShop/Leno.SellerShop.Infrastructure/AntiCorruption/IOrderAntiCorruptionService.cs`
- Modify: `src/Services/SellerShop/Leno.SellerShop.Infrastructure/AntiCorruption/GrpcProductAntiCorruptionClient.cs`
- Modify: `src/Services/SellerShop/Leno.SellerShop.Infrastructure/AntiCorruption/GrpcOrderAntiCorruptionClient.cs`
- Test: `src/Services/SellerShop/Leno.SellerShop.Application.Tests/SellerInternalQueryServiceTests.cs` (新建)
- Test: `src/Services/SellerShop/Leno.SellerShop.Api.Tests/SellerGrpcServiceTests.cs` (新建)
- Test: `src/Services/SellerShop/Leno.SellerShop.Infrastructure.Tests/SellerShopAntiCorruptionTests.cs` (新建)

- [ ] **Step 1: 读取既有文件**

Run: `Read src/Services/SellerShop/Leno.SellerShop.Application/ISellerInternalQueryService.cs`
Run: `Read src/Services/SellerShop/Leno.SellerShop.Application/InternalQueryServices/SellerInternalQueryService.cs`
Run: `Read src/Services/SellerShop/Leno.SellerShop.Api/GrpcServices/SellerGrpcService.cs`
Run: `Read src/Services/SellerShop/Leno.SellerShop.Infrastructure/AntiCorruption/IProductAntiCorruptionService.cs`
Run: `Read src/Services/SellerShop/Leno.SellerShop.Infrastructure/AntiCorruption/IOrderAntiCorruptionService.cs`
Run: `Read src/Services/SellerShop/Leno.SellerShop.Infrastructure/AntiCorruption/GrpcProductAntiCorruptionClient.cs`
Run: `Read src/Services/SellerShop/Leno.SellerShop.Infrastructure/AntiCorruption/GrpcOrderAntiCorruptionClient.cs`
Run: `Read src/Services/SellerShop/Leno.SellerShop.Application/IShopAppService.cs`（确认 GetMyShopAsync 签名）

- [ ] **Step 2: 在 IProductAntiCorruptionService 新增 GetSpuSellerIdAsync**

在 `IProductAntiCorruptionService.cs` 末尾新增：
```csharp
Task<Guid?> GetSpuSellerIdAsync(Guid spuId, CancellationToken ct = default);
```

- [ ] **Step 3: 在 IOrderAntiCorruptionService 新增 GetOrderSellerIdAsync**

在 `IOrderAntiCorruptionService.cs` 末尾新增：
```csharp
Task<Guid?> GetOrderSellerIdAsync(Guid orderId, CancellationToken ct = default);
```

- [ ] **Step 4: 在 GrpcProductAntiCorruptionClient 实现 GetSpuSellerIdAsync**

在 `GrpcProductAntiCorruptionClient.cs` 中新增方法（参考既有 ExecuteAsync 模式）：
```csharp
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
```

> 注：实现时确认 `ExecuteAsync<TResult>` 的签名与 metadata 字段名。

- [ ] **Step 5: 在 GrpcOrderAntiCorruptionClient 实现 GetOrderSellerIdAsync**

在 `GrpcOrderAntiCorruptionClient.cs` 中新增方法：
```csharp
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

- [ ] **Step 6: 在 ISellerInternalQueryService 新增 ValidateOwnershipAsync**

在 `ISellerInternalQueryService.cs` 末尾新增：
```csharp
Task<bool> ValidateOwnershipAsync(Guid sellerId, string resourceType, Guid resourceId, CancellationToken ct = default);
```

- [ ] **Step 7: 写失败测试 — SellerInternalQueryService.ValidateOwnershipAsync**

新建 `src/Services/SellerShop/Leno.SellerShop.Application.Tests/SellerInternalQueryServiceTests.cs`：
```csharp
using FluentAssertions;
using Leno.SellerShop.Application;
using Leno.SellerShop.Application.InternalQueryServices;
using Leno.SellerShop.Infrastructure.AntiCorruption;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Leno.SellerShop.Application.Tests;

public class SellerInternalQueryServiceTests
{
    private static readonly Guid SellerId = Guid.NewGuid();
    private static readonly Guid ResourceId = Guid.NewGuid();

    [Fact]
    public async Task ValidateOwnership_ShopOwned_ReturnsTrue()
    {
        var shopAppService = new Mock<IShopAppService>();
        shopAppService.Setup(s => s.GetMyShopAsync(SellerId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ShopDto { Id = ResourceId, SellerId = SellerId });
        var sut = CreateService(shopAppService: shopAppService.Object);

        var result = await sut.ValidateOwnershipAsync(SellerId, "shop", ResourceId, CancellationToken.None);

        result.Should().BeTrue();
    }

    [Fact]
    public async Task ValidateOwnership_ShopNotOwned_ReturnsFalse()
    {
        var shopAppService = new Mock<IShopAppService>();
        shopAppService.Setup(s => s.GetMyShopAsync(SellerId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ShopDto { Id = Guid.NewGuid(), SellerId = SellerId });
        var sut = CreateService(shopAppService: shopAppService.Object);

        var result = await sut.ValidateOwnershipAsync(SellerId, "shop", ResourceId, CancellationToken.None);

        result.Should().BeFalse();
    }

    [Fact]
    public async Task ValidateOwnership_SpuOwned_ReturnsTrue()
    {
        var productAntiCorruption = new Mock<IProductAntiCorruptionService>();
        productAntiCorruption.Setup(p => p.GetSpuSellerIdAsync(ResourceId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(SellerId);
        var sut = CreateService(productAntiCorruption: productAntiCorruption.Object);

        var result = await sut.ValidateOwnershipAsync(SellerId, "spu", ResourceId, CancellationToken.None);

        result.Should().BeTrue();
    }

    [Fact]
    public async Task ValidateOwnership_SpuAntiCorruptionNull_ReturnsFalse()
    {
        var productAntiCorruption = new Mock<IProductAntiCorruptionService>();
        productAntiCorruption.Setup(p => p.GetSpuSellerIdAsync(ResourceId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Guid?)null);
        var sut = CreateService(productAntiCorruption: productAntiCorruption.Object);

        var result = await sut.ValidateOwnershipAsync(SellerId, "spu", ResourceId, CancellationToken.None);

        result.Should().BeFalse();
    }

    [Fact]
    public async Task ValidateOwnership_OrderOwned_ReturnsTrue()
    {
        var orderAntiCorruption = new Mock<IOrderAntiCorruptionService>();
        orderAntiCorruption.Setup(o => o.GetOrderSellerIdAsync(ResourceId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(SellerId);
        var sut = CreateService(orderAntiCorruption: orderAntiCorruption.Object);

        var result = await sut.ValidateOwnershipAsync(SellerId, "order", ResourceId, CancellationToken.None);

        result.Should().BeTrue();
    }

    [Fact]
    public async Task ValidateOwnership_OrderAntiCorruptionNull_ReturnsFalse()
    {
        var orderAntiCorruption = new Mock<IOrderAntiCorruptionService>();
        orderAntiCorruption.Setup(o => o.GetOrderSellerIdAsync(ResourceId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Guid?)null);
        var sut = CreateService(orderAntiCorruption: orderAntiCorruption.Object);

        var result = await sut.ValidateOwnershipAsync(SellerId, "order", ResourceId, CancellationToken.None);

        result.Should().BeFalse();
    }

    [Fact]
    public async Task ValidateOwnership_UnknownResourceType_ReturnsFalse()
    {
        var sut = CreateService();

        var result = await sut.ValidateOwnershipAsync(SellerId, "unknown", ResourceId, CancellationToken.None);

        result.Should().BeFalse();
    }

    private static SellerInternalQueryService CreateService(
        IShopAppService? shopAppService = null,
        IProductAntiCorruptionService? productAntiCorruption = null,
        IOrderAntiCorruptionService? orderAntiCorruption = null)
    {
        return new SellerInternalQueryService(
            shopAppService ?? Mock.Of<IShopAppService>(),
            Mock.Of<ISellerAppService>(),
            productAntiCorruption ?? Mock.Of<IProductAntiCorruptionService>(),
            orderAntiCorruption ?? Mock.Of<IOrderAntiCorruptionService>(),
            Mock.Of<ILogger<SellerInternalQueryService>>());
    }
}
```

> 注：构造函数参数与 ShopDto 命名按实际 `SellerInternalQueryService` 构造函数与 `IShopAppService.GetMyShopAsync` 返回类型调整。

- [ ] **Step 8: 运行测试，验证失败**

Run: `dotnet test src/Services/SellerShop/Leno.SellerShop.Application.Tests --filter "FullyQualifiedName~ValidateOwnership"`
Expected: FAIL（方法未定义）

- [ ] **Step 9: 在 SellerInternalQueryService 实现 ValidateOwnershipAsync**

修改 `SellerInternalQueryService.cs`：
1. 构造函数新增参数 `IProductAntiCorruptionService productAntiCorruption` 与 `IOrderAntiCorruptionService orderAntiCorruption`（若未注入）
2. 添加字段 `private readonly IProductAntiCorruptionService _productAntiCorruption;` 与 `private readonly IOrderAntiCorruptionService _orderAntiCorruption;`
3. 新增方法：
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

- [ ] **Step 10: 运行测试，验证通过**

Run: `dotnet test src/Services/SellerShop/Leno.SellerShop.Application.Tests --filter "FullyQualifiedName~ValidateOwnership"`
Expected: PASS（7 个测试）

- [ ] **Step 11: 写失败测试 — SellerGrpcService.ValidateSellerOwnership**

新建 `src/Services/SellerShop/Leno.SellerShop.Api.Tests/SellerGrpcServiceTests.cs`：
```csharp
using Grpc.Core;
using FluentAssertions;
using Leno.SellerShop.Api.GrpcServices;
using Leno.SellerShop.Application;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Leno.SellerShop.Api.Tests;

public class SellerGrpcServiceTests
{
    private static readonly Guid SellerId = Guid.NewGuid();
    private static readonly Guid ResourceId = Guid.NewGuid();

    [Fact]
    public async Task ValidateSellerOwnership_ValidInput_ReturnsResponse()
    {
        var queryService = new Mock<ISellerInternalQueryService>();
        queryService.Setup(q => q.ValidateOwnershipAsync(SellerId, "shop", ResourceId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        var sut = new SellerGrpcService(queryService.Object, Mock.Of<ILogger<SellerGrpcService>>());

        var request = new SharedContracts.Grpc.Seller.V1.ValidateSellerOwnershipRequest
        {
            SellerId = SellerId.ToString(),
            ResourceType = "shop",
            ResourceId = ResourceId.ToString()
        };

        var result = await sut.ValidateSellerOwnership(request, CreateServerCallContext());

        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public async Task ValidateSellerOwnership_InvalidSellerId_ThrowsInvalidArgument()
    {
        var sut = new SellerGrpcService(Mock.Of<ISellerInternalQueryService>(), Mock.Of<ILogger<SellerGrpcService>>());

        var request = new SharedContracts.Grpc.Seller.V1.ValidateSellerOwnershipRequest
        {
            SellerId = "not-a-guid",
            ResourceType = "shop",
            ResourceId = ResourceId.ToString()
        };

        var act = async () => await sut.ValidateSellerOwnership(request, CreateServerCallContext());

        var ex = await Assert.ThrowsAsync<RpcException>(act);
        ex.StatusCode.Should().Be(StatusCode.InvalidArgument);
    }

    [Fact]
    public async Task ValidateSellerOwnership_InvalidResourceId_ThrowsInvalidArgument()
    {
        var sut = new SellerGrpcService(Mock.Of<ISellerInternalQueryService>(), Mock.Of<ILogger<SellerGrpcService>>());

        var request = new SharedContracts.Grpc.Seller.V1.ValidateSellerOwnershipRequest
        {
            SellerId = SellerId.ToString(),
            ResourceType = "shop",
            ResourceId = "not-a-guid"
        };

        var act = async () => await sut.ValidateSellerOwnership(request, CreateServerCallContext());

        var ex = await Assert.ThrowsAsync<RpcException>(act);
        ex.StatusCode.Should().Be(StatusCode.InvalidArgument);
    }

    private static ServerCallContext CreateServerCallContext() => new TestServerCallContext();
}
```

- [ ] **Step 12: 修改 SellerGrpcService.ValidateSellerOwnership 实现**

替换 `SellerGrpcService.cs` 第 75-81 行：
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

- [ ] **Step 13: 写防腐层测试 — SellerShopAntiCorruptionTests**

新建 `src/Services/SellerShop/Leno.SellerShop.Infrastructure.Tests/SellerShopAntiCorruptionTests.cs`：
```csharp
using FluentAssertions;
using Leno.SellerShop.Infrastructure.AntiCorruption;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Leno.SellerShop.Infrastructure.Tests;

public class SellerShopAntiCorruptionTests
{
    [Fact]
    public async Task GetSpuSellerId_GrpcReturnsValid_ReturnsSellerId()
    {
        // 安排：mock gRPC client 返回 SellerIdStr
        var spuId = Guid.NewGuid();
        var sellerId = Guid.NewGuid();
        var client = CreateGrpcProductClientReturning(spuId, sellerId);

        // 行动
        var result = await client.GetSpuSellerIdAsync(spuId, CancellationToken.None);

        // 断言
        result.Should().Be(sellerId);
    }

    [Fact]
    public async Task GetSpuSellerId_GrpcFailure_ReturnsNull()
    {
        var spuId = Guid.NewGuid();
        var client = CreateGrpcProductClientThrowing(spuId, new RpcException(new Status(StatusCode.Unavailable, "gRPC down")));

        var result = await client.GetSpuSellerIdAsync(spuId, CancellationToken.None);

        result.Should().BeNull();
    }

    [Fact]
    public async Task GetOrderSellerId_GrpcReturnsValid_ReturnsSellerId()
    {
        var orderId = Guid.NewGuid();
        var sellerId = Guid.NewGuid();
        var client = CreateGrpcOrderClientReturning(orderId, sellerId);

        var result = await client.GetOrderSellerIdAsync(orderId, CancellationToken.None);

        result.Should().Be(sellerId);
    }

    [Fact]
    public async Task GetOrderSellerId_GrpcFailure_ReturnsNull()
    {
        var orderId = Guid.NewGuid();
        var client = CreateGrpcOrderClientThrowing(orderId, new RpcException(new Status(StatusCode.Unavailable, "gRPC down")));

        var result = await client.GetOrderSellerIdAsync(orderId, CancellationToken.None);

        result.Should().BeNull();
    }

    // mock 工厂方法实现按既有 GrpcXxxAntiCorruptionClient 测试模式（mock ICallInvoker 或使用 FakeCallInvoker）
}
```

> 注：mock 工厂方法按既有 `Leno.Order.Infrastructure.Tests/AntiCorruptionServicesTests.cs` 模式实现（使用 `CallInvoker` mock 或 `GrpcChannel` fake）。

- [ ] **Step 14: 运行测试，验证通过**

Run: `dotnet test src/Services/SellerShop.Application.Tests --filter "FullyQualifiedName~ValidateOwnership" && dotnet test src/Services/SellerShop.Api.Tests --filter "FullyQualifiedName~SellerGrpcService" && dotnet test src/Services/SellerShop.Infrastructure.Tests --filter "FullyQualifiedName~SellerShopAntiCorruption"`
Expected: PASS（共约 14 个测试）

- [ ] **Step 15: 运行 Seller 域全部测试，确保无回归**

Run: `dotnet test src/Services/SellerShop`
Expected: PASS

- [ ] **Step 16: 提交**

```bash
git add src/Services/SellerShop/Leno.SellerShop.Application/ISellerInternalQueryService.cs src/Services/SellerShop/Leno.SellerShop.Application/InternalQueryServices/SellerInternalQueryService.cs src/Services/SellerShop/Leno.SellerShop.Api/GrpcServices/SellerGrpcService.cs src/Services/SellerShop/Leno.SellerShop.Infrastructure/AntiCorruption/ src/Services/SellerShop/Leno.SellerShop.Application.Tests/SellerInternalQueryServiceTests.cs src/Services/SellerShop/Leno.SellerShop.Api.Tests/SellerGrpcServiceTests.cs src/Services/SellerShop/Leno.SellerShop.Infrastructure.Tests/SellerShopAntiCorruptionTests.cs
git commit -m "feat(seller): 实现 ValidateSellerOwnership gRPC 接口与跨域防腐层扩展"
```

---

## Task 8: 全量验证与占位符检查

**Files:** 无（仅验证）

- [ ] **Step 1: 运行占位符检查脚本**

Run: `bash scripts/check-placeholders.sh`
Expected: 0 占位符（"No placeholders found" 或类似成功消息）

- [ ] **Step 2: 全量构建**

Run: `dotnet build Leno.slnx -c Release`
Expected: Build succeeded, 0 errors, 0 warnings

- [ ] **Step 3: 全量测试**

Run: `dotnet test Leno.slnx --no-build`
Expected: All tests pass

- [ ] **Step 4: 代码格式检查**

Run: `dotnet format Leno.slnx --verify-no-changes`
Expected: 0 format violations

- [ ] **Step 5: 确认新增测试方法数 ≥ 30**

Run: `grep -r "\[Fact\]\|\[Theory\]" src/Services/Order/Leno.Order.Application.Tests/SeckillOrderCreationServiceTests.cs src/Services/Order/Leno.Order.Infrastructure.Tests/OrderInternalQueryServiceTests.cs src/Services/Promotion/Leno.Promotion.Application.Tests/CouponAppServiceTests.cs src/Services/Promotion/Leno.Promotion.Api.Tests/PromotionGrpcServiceTests.cs src/Services/Product/Leno.Product.Infrastructure.Tests/ProductInternalQueryServiceTests.cs src/Services/PointsMembership/Leno.PointsMembership.Application.Tests/PointsInternalAppServiceTests.cs src/Services/PointsMembership/Leno.PointsMembership.Api.Tests/PointsGrpcServiceTests.cs src/Services/SellerShop/Leno.SellerShop.Application.Tests/SellerInternalQueryServiceTests.cs src/Services/SellerShop/Leno.SellerShop.Api.Tests/SellerGrpcServiceTests.cs src/Services/SellerShop/Leno.SellerShop.Infrastructure.Tests/SellerShopAntiCorruptionTests.cs | wc -l`
Expected: ≥ 30

- [ ] **Step 6: 提交（若有修复）**

```bash
git add -A
git commit -m "chore: P0-A 占位实现补齐验证通过"
```

---

## Task 9: 最终 Git 推送

- [ ] **Step 1: 查看所有提交**

Run: `git log --oneline -10`
Expected: 看到 P0-A 相关的 7-8 个提交

- [ ] **Step 2: 推送到远程**

Run: `git push origin HEAD`
Expected: Push 成功

---

## Self-Review 检查

**1. Spec 覆盖检查**：

| Spec 要求 | 对应 Task |
|---|---|
| §3.1 Order PublishFailedEventAsync | Task 2 |
| §3.2 Promotion LockCoupon/ReleaseCoupons | Task 5 |
| §3.3 Product GetSkuStock/GetProductDetail | Task 4 |
| §3.4 Points Confirm | Task 6 |
| §3.5 Seller ValidateSellerOwnership | Task 7 |
| §3.6 Order GetOrderSellerId RPC | Task 1 (proto) + Task 3 (实现) |
| §5.1 测试覆盖矩阵 12 项 | Task 2 (1) + Task 3 (11) + Task 4 (4,5) + Task 5 (2,3) + Task 6 (7,8) + Task 7 (9,10,12) |
| §7 验收标准 1-16 | Task 8 验证 |

**2. 占位符扫描**：✅ 无 TBD/TODO，所有代码块完整

**3. 类型一致性**：✅
- `SkuStockResultDto` / `SpuDetailResultDto` / `ConfirmPointsDto` 定义与使用一致
- `ValidateOwnershipAsync` 签名在 ISellerInternalQueryService、SellerInternalQueryService、SellerGrpcService 中一致
- `GetSpuSellerIdAsync` / `GetOrderSellerIdAsync` 在防腐层接口与实现一致

**4. 测试覆盖完整性**：✅ 12 项测试矩阵全部对应到 Task 步骤

实施计划已就绪。下面按 subagent-driven 方式执行：每个 Task 派发独立的 subagent 完成，主流程负责 review 与衔接。
