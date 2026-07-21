# ReviewAfterSales（评价与售后域）修复实施计划

## 元数据
- 审计报告：[file:///workspace/docs/superpowers/specs/2026-07-21-code-audit/06-reviewaftersales.md]
- 问题总数：🔴 11 / 🟡 12 / 🟢 8
- 已修复（跳过）：0 项
- 本计划覆盖：31 项

## 问题清单总表
| # | 严重度 | 问题标题 | 审计位置 | 优先级 | 状态 |
|---|--------|---------|---------|--------|------|
| 2.1 | 🔴 | 买家提交售后申请时 SellerId 完全由客户端伪造 | [file:///workspace/src/Services/ReviewAfterSales/Leno.ReviewAfterSales.Application/Services/AfterSalesAppService.cs#L52-L67] | P0 | 待修复 |
| 2.2 | 🔴 | 评价提交 SpuId / SkuId 由客户端伪造，可污染商品评分 | [file:///workspace/src/Services/ReviewAfterSales/Leno.ReviewAfterSales.Application/Services/ReviewAppService.cs#L40-L54] | P0 | 待修复 |
| 2.3 | 🔴 | AfterSales.Cancel / MarkRefundFailed 领域事件缺失，下游无感知 | [file:///workspace/src/Services/ReviewAfterSales/Leno.ReviewAfterSales.Domain/Aggregates/AfterSales.cs#L388-L399] | P0 | 待修复 |
| 2.4 | 🔴 | RefundSucceededEventConsumer 未保存渠道退款单号 ChannelRefundNo | [file:///workspace/src/Services/ReviewAfterSales/Leno.ReviewAfterSales.Infrastructure/Consumers/RefundSucceededEventConsumer.cs#L67] | P0 | 待修复 |
| 2.5 | 🔴 | ReviewGrpcService Guid→long 转换使用 GetHashCode 严重失真 | [file:///workspace/src/Services/ReviewAfterSales/Leno.ReviewAfterSales.Api/GrpcServices/ReviewGrpcService.cs#L75-L103] | P0 | 待修复 |
| 2.6 | 🔴 | 买家撤销售后/买家退货/卖家驳回售后均缺失申请人/卖家归属校验 | [file:///workspace/src/Services/ReviewAfterSales/Leno.ReviewAfterSales.Application/Services/AfterSalesAppService.cs#L105-L113] | P0 | 待修复 |
| 2.7 | 🔴 | SellerReply 完全缺失卖家归属校验，任意卖家可回复任意评价 | [file:///workspace/src/Services/ReviewAfterSales/Leno.ReviewAfterSales.Api/Controllers/ReviewsController.cs#L125-L133] | P0 | 待修复 |
| 2.8 | 🔴 | 聚合内部 List 通过 Images 属性直接暴露，外部可绕过聚合方法修改状态 | [file:///workspace/src/Services/ReviewAfterSales/Leno.ReviewAfterSales.Domain/Aggregates/AfterSales.cs#L43-L44] | P0 | 待修复 |
| 2.9 | 🔴 | HasActiveByOrderLineAsync 活跃状态过滤不全，允许同订单行重复售后 | [file:///workspace/src/Services/ReviewAfterSales/Leno.ReviewAfterSales.Infrastructure/Repositories/EfCoreAfterSalesRepository.cs#L34-L45] | P0 | 待修复 |
| 2.10 | 🔴 | 买家按订单查询售后单/按订单行查询评价均缺失订单归属校验 | [file:///workspace/src/Services/ReviewAfterSales/Leno.ReviewAfterSales.Api/Controllers/AfterSalesController.cs#L69-L77] | P0 | 待修复 |
| 2.11 | 🔴 | RefundCompleted 事件回环：本 BC 发布的 RefundCompletedEvent 会被自身消费者重复消费 | [file:///workspace/src/Services/ReviewAfterSales/Leno.ReviewAfterSales.Infrastructure/EventBus/ReviewAfterSalesIntegrationEventMapper.cs#L43-L46] | P0 | 待修复 |
| 3.1 | 🟡 | AfterSales.Reject 误用 ApprovedAt 字段记录驳回时间 | [file:///workspace/src/Services/ReviewAfterSales/Leno.ReviewAfterSales.Domain/Aggregates/AfterSales.cs#L241-L270] | P1 | 待修复 |
| 3.2 | 🟡 | AfterSales.ConfirmReturn 未记录操作人，审计缺失 | [file:///workspace/src/Services/ReviewAfterSales/Leno.ReviewAfterSales.Domain/Aggregates/AfterSales.cs#L311-L324] | P1 | 待修复 |
| 3.3 | 🟡 | 整单售后（orderLineId 为 null）不做重复申请校验 | [file:///workspace/src/Services/ReviewAfterSales/Leno.ReviewAfterSales.Infrastructure/Services/AfterSalesEligibilityChecker.cs#L65-L72] | P1 | 待修复 |
| 3.4 | 🟡 | ReviewInternalQueryService.GetProductRatingAsync 加载全部 Approved 评价到内存计算聚合 | [file:///workspace/src/Services/ReviewAfterSales/Leno.ReviewAfterSales.Application/InternalQueryServices/ReviewInternalQueryService.cs#L21-L41] | P1 | 待修复 |
| 3.5 | 🟡 | GrpcOrderStatusProvider 返回 OrderLineId=Guid.Empty 且 SkuId 可能丢失 | [file:///workspace/src/Services/ReviewAfterSales/Leno.ReviewAfterSales.Infrastructure/Services/Grpc/GrpcOrderStatusProvider.cs#L60-L90] | P1 | 待修复 |
| 3.6 | 🟡 | ReviewReadModelSyncConsumer 未实现 EventId 幂等去重 | [file:///workspace/src/Services/ReviewAfterSales/Leno.ReviewAfterSales.Infrastructure/ReadModels/ReviewReadModelSyncConsumer.cs#L14-L57] | P1 | 待修复 |
| 3.7 | 🟡 | ApproveAfterSalesAsync 在数据库事务内执行远程支付查询，长事务持锁 | [file:///workspace/src/Services/ReviewAfterSales/Leno.ReviewAfterSales.Application/Services/AfterSalesAppService.cs#L70-L102] | P1 | 待修复 |
| 3.8 | 🟡 | 仓储层全部未使用 AsNoTracking，只读查询进入 Change Tracker | [file:///workspace/src/Services/ReviewAfterSales/Leno.ReviewAfterSales.Infrastructure/Repositories/EfCoreAfterSalesRepository.cs#L22-L127] | P1 | 待修复 |
| 3.9 | 🟡 | 订单状态硬编码（OrderStatusShipped=2 / OrderStatusCompleted=3），跨 BC 契约脆弱 | [file:///workspace/src/Services/ReviewAfterSales/Leno.ReviewAfterSales.Infrastructure/Services/AfterSalesEligibilityChecker.cs#L17-L18] | P1 | 待修复 |
| 3.10 | 🟡 | 上传图片流未 using，依赖框架兜底 | [file:///workspace/src/Services/ReviewAfterSales/Leno.ReviewAfterSales.Api/Controllers/AfterSalesController.cs#L129] | P1 | 待修复 |
| 3.11 | 🟡 | 图片上传仅校验扩展名，未校验文件内容/Magic Number | [file:///workspace/src/Services/ReviewAfterSales/Leno.ReviewAfterSales.Api/Controllers/AfterSalesController.cs#L90-L134] | P1 | 待修复 |
| 3.12 | 🟡 | ReviewReadModelSyncConsumer 不处理评价被删除场景 | [file:///workspace/src/Services/ReviewAfterSales/Leno.ReviewAfterSales.Infrastructure/ReadModels/ReviewReadModelSyncConsumer.cs#L14-L107] | P1 | 待修复 |
| 4.1 | 🟢 | OrderCompletedEventConsumer 仅打日志无副作用 | [file:///workspace/src/Services/ReviewAfterSales/Leno.ReviewAfterSales.Infrastructure/Consumers/OrderCompletedEventConsumer.cs#L14-L33] | P2 | 待修复 |
| 4.2 | 🟢 | MarkRefundFailed 与 Cancel 未校验 reason 是否为 null/空 | [file:///workspace/src/Services/ReviewAfterSales/Leno.ReviewAfterSales.Domain/Aggregates/AfterSales.cs#L388-L399] | P2 | 待修复 |
| 4.3 | 🟢 | AfterSales.Create 接收 images 列表共享引用给内部 _images | [file:///workspace/src/Services/ReviewAfterSales/Leno.ReviewAfterSales.Domain/Aggregates/AfterSales.cs#L169-L194] | P2 | 待修复 |
| 4.4 | 🟢 | AntiCorruptionOptions 解析在 UseGrpc=true 时硬抛异常，无降级 | [file:///workspace/src/Services/ReviewAfterSales/Leno.ReviewAfterSales.Infrastructure/Dependencies/ServiceCollectionExtensions.cs#L66-L105] | P2 | 待修复 |
| 4.5 | 🟢 | RefundFailedEventConsumer 失败原因未做长度校验 | [file:///workspace/src/Services/ReviewAfterSales/Leno.ReviewAfterSales.Infrastructure/Consumers/RefundFailedEventConsumer.cs#L34-L74] | P2 | 待修复 |
| 4.6 | 🟢 | ApplyFilters 中 status.HasValue 与 status.Value 的冗余判断 | [file:///workspace/src/Services/ReviewAfterSales/Leno.ReviewAfterSales.Infrastructure/Repositories/EfCoreAfterSalesRepository.cs#L99-L127] | P2 | 待修复 |
| 4.7 | 🟢 | ReviewInternalQueryService.GetOrderReviewsAsync 返回 null 而非空集合 | [file:///workspace/src/Services/ReviewAfterSales/Leno.ReviewAfterSales.Application/InternalQueryServices/ReviewInternalQueryService.cs#L44-L64] | P2 | 待修复 |
| 4.8 | 🟢 | RefundCompletedEvent 契约中 AfterSalesId 默认 Guid.Empty 兼容旧版 | [file:///workspace/src/BuildingBlocks/Leno.SharedContracts/Events/PaymentEvents.cs#L107-L163] | P2 | 待修复 |

---

## P0 详细修复计划（TDD bite-sized 格式，5 步：测试→验证失败→实现→验证通过→提交）

### P0-2.1 修复买家提交售后申请时 SellerId 客户端伪造

**审计位置**：[file:///workspace/src/Services/ReviewAfterSales/Leno.ReviewAfterSales.Application/Services/AfterSalesAppService.cs#L52-L67]、[file:///workspace/src/Services/ReviewAfterSales/Leno.ReviewAfterSales.Application/DTOs/AfterSalesDtos.cs#L8-L19]、[file:///workspace/src/Services/ReviewAfterSales/Leno.ReviewAfterSales.Domain/Services/IAfterSalesEligibilityChecker.cs#L9-L19]、[file:///workspace/src/Services/ReviewAfterSales/Leno.ReviewAfterSales.Infrastructure/Services/AfterSalesEligibilityChecker.cs#L38-L73]、[file:///workspace/src/Services/ReviewAfterSales/Leno.ReviewAfterSales.Domain/Services/IOrderStatusProvider.cs#L17-L34]

**根因**：`SubmitAfterSalesDto.SellerId` 为请求体字段，应用层直接透传给 `AfterSalesAggregate.Create`。`IAfterSalesEligibilityChecker.EnsureEligibleAsync` 签名无 `sellerId` 参数，`OrderStatusInfo` 无 `SellerId` 字段可供校验。

**步骤 1：编写失败测试**

测试文件：`tests/Services/ReviewAfterSales/Leno.ReviewAfterSales.UnitTests/Services/AfterSalesEligibilityCheckerTests.cs`

```csharp
using Leno.ReviewAfterSales.Domain.Exceptions;
using Leno.ReviewAfterSales.Domain.Repositories;
using Leno.ReviewAfterSales.Domain.Services;
using Leno.ReviewAfterSales.Domain.ValueObjects;
using Leno.ReviewAfterSales.Infrastructure.Services;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace Leno.ReviewAfterSales.UnitTests.Services;

public sealed class AfterSalesEligibilityCheckerTests
{
    private static readonly Guid OrderId = Guid.NewGuid();
    private static readonly Guid UserId = Guid.NewGuid();
    private static readonly Guid RealSellerId = Guid.NewGuid();
    private static readonly Guid ForgedSellerId = Guid.NewGuid();

    private readonly Mock<IOrderStatusProvider> _orderProviderMock = new();
    private readonly Mock<IAfterSalesRepository> _repoMock = new();
    private readonly AfterSalesEligibilityChecker _checker;

    public AfterSalesEligibilityCheckerTests()
    {
        _checker = new AfterSalesEligibilityChecker(
            _orderProviderMock.Object,
            _repoMock.Object,
            NullLogger<AfterSalesEligibilityChecker>.Instance);
    }

    [Fact]
    public async Task EnsureEligibleAsync_Should_Throw_When_SellerId_Mismatches_Order_SellerId()
    {
        _orderProviderMock.Setup(p => p.GetOrderStatusAsync(OrderId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new OrderStatusInfo
            {
                OrderId = OrderId,
                Status = 2,
                UserId = UserId,
                SellerId = RealSellerId,
                Items = new List<OrderItemStatusInfo>()
            });
        _repoMock.Setup(r => r.HasActiveByOrderLineAsync(It.IsAny<Guid>(), It.IsAny<AfterSalesType>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        var act = async () => await _checker.EnsureEligibleAsync(
            OrderId, orderLineId: null, UserId, ForgedSellerId, AfterSalesType.RefundOnly);

        var ex = await Assert.ThrowsAsync<ReviewDomainException>(act);
        Assert.Equal("AFTERSALES_SELLER_MISMATCH", ex.ErrorCode);
    }

    [Fact]
    public async Task EnsureEligibleAsync_Should_Pass_When_SellerId_Matches_Order_SellerId()
    {
        _orderProviderMock.Setup(p => p.GetOrderStatusAsync(OrderId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new OrderStatusInfo
            {
                OrderId = OrderId,
                Status = 2,
                UserId = UserId,
                SellerId = RealSellerId,
                Items = new List<OrderItemStatusInfo>()
            });

        await _checker.EnsureEligibleAsync(
            OrderId, orderLineId: null, UserId, RealSellerId, AfterSalesType.RefundOnly);
    }
}
```

测试文件：`tests/Services/ReviewAfterSales/Leno.ReviewAfterSales.UnitTests/Application/AfterSalesAppServiceSubmitTests.cs`

```csharp
using Leno.ReviewAfterSales.Application.DTOs;
using Leno.ReviewAfterSales.Application.Services;
using Leno.ReviewAfterSales.Domain.Aggregates;
using Leno.ReviewAfterSales.Domain.Repositories;
using Leno.ReviewAfterSales.Domain.Services;
using Leno.ReviewAfterSales.Domain.ValueObjects;
using Leno.SharedKernel.Abstractions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace Leno.ReviewAfterSales.UnitTests.Application;

public sealed class AfterSalesAppServiceSubmitTests
{
    private static readonly Guid RealSellerId = Guid.NewGuid();

    [Fact]
    public async Task SubmitAfterSalesAsync_Should_Ignore_Dto_SellerId_And_Use_Order_SellerId()
    {
        var userId = Guid.NewGuid();
        var orderId = Guid.NewGuid();
        var forgedSellerId = Guid.NewGuid();
        var captured = (Guid?)null;

        var eligibilityMock = new Mock<IAfterSalesEligibilityChecker>();
        eligibilityMock.Setup(c => c.EnsureEligibleAsync(
                orderId, It.IsAny<Guid?>(), userId, RealSellerId, AfterSalesType.RefundOnly, It.IsAny<CancellationToken>()))
            .Callback((Guid oid, Guid? ol, Guid u, Guid s, AfterSalesType t, CancellationToken ct) => captured = s)
            .Returns(Task.CompletedTask);

        var repoMock = new Mock<IAfterSalesRepository>();
        repoMock.Setup(r => r.AddAsync(It.IsAny<AfterSales>(), It.IsAny<CancellationToken>()))
            .Callback((AfterSales a, CancellationToken ct) => Assert.Equal(RealSellerId, a.SellerId))
            .Returns(Task.CompletedTask);
        var uowMock = new Mock<IUnitOfWork>();
        var eventBusMock = new Mock<IEventBus>();
        var paymentMock = new Mock<IPaymentInfoQueryService>();

        var svc = new AfterSalesAppService(
            repoMock.Object, eligibilityMock.Object, paymentMock.Object,
            eventBusMock.Object, uowMock.Object, NullLogger<AfterSalesAppService>.Instance);

        var dto = new SubmitAfterSalesDto
        {
            OrderId = orderId,
            SellerId = forgedSellerId,
            Type = AfterSalesType.RefundOnly,
            ReasonCategory = "quality",
            Reason = "broken item",
            RequestedAmount = 10m
        };

        await svc.SubmitAfterSalesAsync(userId, dto);

        Assert.Equal(RealSellerId, captured);
    }
}
```

**步骤 2：验证失败**

```bash
dotnet test tests/Services/ReviewAfterSales/Leno.ReviewAfterSales.UnitTests --filter "FullyQualifiedName~AfterSalesEligibilityCheckerTests|AfterSalesAppServiceSubmitTests"
```

预期：编译失败（`IAfterSalesEligibilityChecker.EnsureEligibleAsync` 签名无 `sellerId` 参数，`OrderStatusInfo` 无 `SellerId` 字段，`AfterSalesAppService.SubmitAfterSalesAsync` 仍透传 `dto.SellerId`）。

**步骤 3：实现修复**

3.1 修改 `IOrderStatusProvider.cs`，给 `OrderStatusInfo` 增加 `SellerId` 字段：

```csharp
public sealed class OrderStatusInfo
{
    public Guid OrderId { get; init; }
    public int Status { get; init; }
    public Guid UserId { get; init; }
    public Guid SellerId { get; init; }      // 新增
    public DateTime CompletedAt { get; init; }
    public DateTime CreatedAt { get; init; }
    public List<OrderItemStatusInfo> Items { get; init; } = [];
}
```

3.2 修改 `IAfterSalesEligibilityChecker.cs`，签名增加 `sellerId`：

```csharp
Task EnsureEligibleAsync(Guid orderId, Guid? orderLineId, Guid userId, Guid sellerId, AfterSalesType type, CancellationToken ct = default);
```

3.3 修改 `AfterSalesEligibilityChecker.cs`，实现 SellerId 校验（在第 51 行 `userId != userId` 校验之后插入）：

```csharp
public async Task EnsureEligibleAsync(Guid orderId, Guid? orderLineId, Guid userId, Guid sellerId, AfterSalesType type, CancellationToken ct = default)
{
    var order = await _orderStatusProvider.GetOrderStatusAsync(orderId, ct).ConfigureAwait(false);
    if (order is null)
    {
        throw new ReviewDomainException("订单不存在或不可访问", "AFTERSALES_ORDER_NOT_FOUND");
    }
    if (order.UserId != userId)
    {
        throw new ReviewDomainException("无权操作此订单", "AFTERSALES_FORBIDDEN");
    }
    if (order.SellerId != sellerId)
    {
        throw new ReviewDomainException("SellerId 与订单实际卖家不符", "AFTERSALES_SELLER_MISMATCH");
    }
    // ... 后续状态校验、窗口校验、重复校验保持不变
}
```

3.4 修改 `AfterSalesAppService.SubmitAfterSalesAsync`，先查订单取得真实 SellerId：

```csharp
public async Task<AfterSalesDto> SubmitAfterSalesAsync(Guid userId, SubmitAfterSalesDto dto, CancellationToken ct = default)
{
    // 先校验用户身份与状态，并获得真实卖家
    var order = await _orderStatusProvider.GetOrderStatusAsync(dto.OrderId, ct)
        ?? throw new InvalidOperationException($"订单不存在 OrderId={dto.OrderId}");
    if (order.UserId != userId)
    {
        throw new ReviewDomainException("无权操作此订单", "AFTERSALES_FORBIDDEN");
    }

    await _eligibilityChecker.EnsureEligibleAsync(dto.OrderId, dto.OrderLineId, userId, order.SellerId, dto.Type, ct);

    var afterSalesId = Guid.NewGuid();
    var afterSales = AfterSalesAggregate.Create(
        afterSalesId, dto.OrderId, dto.OrderLineId, userId, order.SellerId,    // 忽略 dto.SellerId，使用 order.SellerId
        dto.Type, dto.ReasonCategory, dto.Reason, dto.Images,
        dto.RequestedAmount, dto.Currency);

    await _afterSalesRepository.AddAsync(afterSales, ct);
    await _unitOfWork.SaveEntitiesAsync(ct);

    _logger.LogInformation("售后申请已提交 AfterSalesId={AfterSalesId} OrderId={OrderId} Type={Type}", afterSalesId, dto.OrderId, dto.Type);
    return ToDto(afterSales);
}
```

> 备注：需要在 `AfterSalesAppService` 构造函数注入 `IOrderStatusProvider`。`SubmitAfterSalesDto.SellerId` 保留以兼容旧客户端反序列化，但在服务端忽略其值。

3.5 同步修改 `HttpOrderStatusProvider.cs` 与 `GrpcOrderStatusProvider.cs` 的 `MapToInfo`，填充 `SellerId`。HTTP 实现需在 `OrderStatusResponse` / proto 响应中读取 `SellerId` 字段（若订单域未提供，则回退 `Guid.Empty` 并打 Warning 日志）。`GrpcOrderStatusProvider.MapToInfo` 第 73 行 OrderId 解析失败抛 `AntiCorruptionException`，避免静默 `Guid.Empty`。

**步骤 4：验证通过**

```bash
dotnet test tests/Services/ReviewAfterSales/Leno.ReviewAfterSales.UnitTests --filter "FullyQualifiedName~AfterSalesEligibilityCheckerTests|AfterSalesAppServiceSubmitTests"
```

预期：所有用例通过。

**步骤 5：提交**

```bash
git add -A && git commit -m "fix(reviewaftersales): 修复售后申请 SellerId 客户端伪造（审计 2.1）

- IOrderStatusProvider.OrderStatusInfo 增加 SellerId 字段
- IAfterSalesEligibilityChecker.EnsureEligibleAsync 签名增加 sellerId 参数
- AfterSalesEligibilityChecker 校验 order.SellerId == sellerId
- AfterSalesAppService.SubmitAfterSalesAsync 忽略 dto.SellerId，使用 order.SellerId
- HttpOrderStatusProvider / GrpcOrderStatusProvider MapToInfo 填充 SellerId"
```

---

### P0-2.2 修复评价提交 SpuId/SkuId 客户端伪造

**审计位置**：[file:///workspace/src/Services/ReviewAfterSales/Leno.ReviewAfterSales.Application/Services/ReviewAppService.cs#L40-L54]、[file:///workspace/src/Services/ReviewAfterSales/Leno.ReviewAfterSales.Application/DTOs/ReviewDtos.cs#L8-L17]、[file:///workspace/src/Services/ReviewAfterSales/Leno.ReviewAfterSales.Domain/Services/IReviewEligibilityChecker.cs#L7-L16]、[file:///workspace/src/Services/ReviewAfterSales/Leno.ReviewAfterSales.Infrastructure/Services/ReviewEligibilityChecker.cs#L36-L67]、[file:///workspace/src/Services/ReviewAfterSales/Leno.ReviewAfterSales.Domain/Services/IOrderStatusProvider.cs#L28-L34]

**根因**：`SubmitReviewDto.SpuId/SkuId` 由客户端传入，`ReviewAppService.SubmitReviewAsync` 直接透传给 `ReviewAggregate.Create`。`IReviewEligibilityChecker` 签名无 `spuId/skuId`，实现未从 `OrderItemStatusInfo.SkuId` 反查比对。`OrderItemStatusInfo` 缺 `SpuId` 字段。

**步骤 1：编写失败测试**

测试文件：`tests/Services/ReviewAfterSales/Leno.ReviewAfterSales.UnitTests/Services/ReviewEligibilityCheckerTests.cs`

```csharp
using Leno.ReviewAfterSales.Domain.Exceptions;
using Leno.ReviewAfterSales.Domain.Repositories;
using Leno.ReviewAfterSales.Domain.Services;
using Leno.ReviewAfterSales.Infrastructure.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace Leno.ReviewAfterSales.UnitTests.Services;

public sealed class ReviewEligibilityCheckerTests
{
    private static readonly Guid OrderId = Guid.NewGuid();
    private static readonly Guid UserId = Guid.NewGuid();
    private static readonly Guid OrderLineId = Guid.NewGuid();
    private static readonly Guid RealSkuId = Guid.NewGuid();
    private static readonly Guid RealSpuId = Guid.NewGuid();
    private static readonly Guid ForgedSkuId = Guid.NewGuid();

    private readonly Mock<IOrderStatusProvider> _orderProviderMock = new();
    private readonly Mock<IReviewRepository> _repoMock = new();
    private readonly ReviewEligibilityChecker _checker;

    public ReviewEligibilityCheckerTests()
    {
        _checker = new ReviewEligibilityChecker(
            _orderProviderMock.Object, _repoMock.Object, NullLogger<ReviewEligibilityChecker>.Instance);
    }

    [Fact]
    public async Task EnsureEligibleAsync_Should_Throw_When_SkuId_Mismatches_OrderLine()
    {
        _orderProviderMock.Setup(p => p.GetOrderStatusAsync(OrderId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new OrderStatusInfo
            {
                OrderId = OrderId,
                Status = 3,
                UserId = UserId,
                CompletedAt = DateTime.UtcNow.AddDays(-1),
                Items = new List<OrderItemStatusInfo>
                {
                    new() { OrderLineId = OrderLineId, SkuId = RealSkuId, SpuId = RealSpuId, Quantity = 1 }
                }
            });
        _repoMock.Setup(r => r.ExistsByOrderLineAsync(OrderLineId, It.IsAny<CancellationToken>())).ReturnsAsync(false);

        var act = async () => await _checker.EnsureEligibleAsync(
            OrderId, OrderLineId, UserId, RealSpuId, ForgedSkuId);

        var ex = await Assert.ThrowsAsync<ReviewDomainException>(act);
        Assert.Equal("REVIEW_SKU_MISMATCH", ex.ErrorCode);
    }

    [Fact]
    public async Task EnsureEligibleAsync_Should_Throw_When_OrderLine_NotFound()
    {
        _orderProviderMock.Setup(p => p.GetOrderStatusAsync(OrderId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new OrderStatusInfo
            {
                OrderId = OrderId, Status = 3, UserId = UserId,
                CompletedAt = DateTime.UtcNow.AddDays(-1),
                Items = new List<OrderItemStatusInfo>()
            });

        var act = async () => await _checker.EnsureEligibleAsync(
            OrderId, OrderLineId, UserId, RealSpuId, RealSkuId);

        var ex = await Assert.ThrowsAsync<ReviewDomainException>(act);
        Assert.Equal("REVIEW_ORDER_LINE_NOT_FOUND", ex.ErrorCode);
    }
}
```

**步骤 2：验证失败**

```bash
dotnet test tests/Services/ReviewAfterSales/Leno.ReviewAfterSales.UnitTests --filter "FullyQualifiedName~ReviewEligibilityCheckerTests"
```

预期：编译失败（`IReviewEligibilityChecker.EnsureEligibleAsync` 签名无 `spuId/skuId`，`OrderItemStatusInfo` 无 `SpuId` 字段）。

**步骤 3：实现修复**

3.1 修改 `IOrderStatusProvider.cs`，给 `OrderItemStatusInfo` 增加 `SpuId` 字段：

```csharp
public sealed class OrderItemStatusInfo
{
    public Guid OrderLineId { get; init; }
    public Guid SkuId { get; init; }
    public Guid SpuId { get; init; }     // 新增
    public int Quantity { get; init; }
    public int AfterSalesStatus { get; init; }
}
```

3.2 修改 `IReviewEligibilityChecker.cs`，签名增加 `spuId/skuId`：

```csharp
Task EnsureEligibleAsync(Guid orderId, Guid orderLineId, Guid userId, Guid spuId, Guid skuId, CancellationToken ct = default);
```

3.3 修改 `ReviewEligibilityChecker.cs`，实现 SkuId/SpuId 校验（在 `ExistsByOrderLineAsync` 之前插入）：

```csharp
public async Task EnsureEligibleAsync(Guid orderId, Guid orderLineId, Guid userId, Guid spuId, Guid skuId, CancellationToken ct = default)
{
    var order = await _orderStatusProvider.GetOrderStatusAsync(orderId, ct).ConfigureAwait(false);
    if (order is null) throw new ReviewDomainException("订单不存在或不可访问", "REVIEW_ORDER_NOT_FOUND");
    if (order.UserId != userId) throw new ReviewDomainException("无权操作此订单", "REVIEW_FORBIDDEN");
    if (order.Status != OrderStatusCompleted) throw new ReviewDomainException("订单未完成，不可评价", "REVIEW_ORDER_NOT_COMPLETED");
    if (order.CompletedAt != default && DateTime.UtcNow - order.CompletedAt > TimeSpan.FromDays(ReviewWindowDays))
        throw new ReviewDomainException("评价已超过期限", "REVIEW_WINDOW_EXPIRED");

    var lineItem = order.Items.FirstOrDefault(i => i.OrderLineId == orderLineId)
        ?? throw new ReviewDomainException("订单行不存在", "REVIEW_ORDER_LINE_NOT_FOUND");
    if (lineItem.SkuId != skuId)
        throw new ReviewDomainException("SkuId 与订单行不符", "REVIEW_SKU_MISMATCH");
    if (lineItem.SpuId != spuId)
        throw new ReviewDomainException("SpuId 与订单行不符", "REVIEW_SPU_MISMATCH");

    var exists = await _reviewRepository.ExistsByOrderLineAsync(orderLineId, ct);
    if (exists) throw new ReviewDomainException("该订单行已评价", "REVIEW_DUPLICATE");
}
```

3.4 修改 `ReviewAppService.SubmitReviewAsync`，先查订单取得真实 SpuId/SkuId：

```csharp
public async Task<ReviewDto> SubmitReviewAsync(Guid userId, SubmitReviewDto dto, CancellationToken ct = default)
{
    var order = await _orderStatusProvider.GetOrderStatusAsync(dto.OrderId, ct)
        ?? throw new InvalidOperationException($"订单不存在 OrderId={dto.OrderId}");
    if (order.UserId != userId)
        throw new ReviewDomainException("无权操作此订单", "REVIEW_FORBIDDEN");

    var lineItem = order.Items.FirstOrDefault(i => i.OrderLineId == dto.OrderLineId)
        ?? throw new ReviewDomainException("订单行不存在", "REVIEW_ORDER_LINE_NOT_FOUND");

    await _eligibilityChecker.EnsureEligibleAsync(
        dto.OrderId, dto.OrderLineId, userId, lineItem.SpuId, lineItem.SkuId, ct);

    var reviewId = Guid.NewGuid();
    var review = ReviewAggregate.Create(
        reviewId, dto.OrderId, dto.OrderLineId, lineItem.SpuId, lineItem.SkuId,    // 使用订单行真实 SpuId/SkuId
        userId, dto.Rating, dto.Content, dto.Images);

    await _reviewRepository.AddAsync(review, ct);
    await _unitOfWork.SaveEntitiesAsync(ct);
    return ToDto(review);
}
```

> 备注：需在 `ReviewAppService` 构造函数注入 `IOrderStatusProvider`。`SubmitReviewDto.SpuId/SkuId` 保留以兼容客户端反序列化，但服务端忽略。

3.5 同步修改 `HttpOrderStatusProvider.MapToInfo` 与 `GrpcOrderStatusProvider.MapToInfo`，从订单响应/proto 中读取 `SpuId` 字段并填充 `OrderItemStatusInfo.SpuId`。HTTP 路径需在 `OrderItemStatusResponse` 增加 `SpuId` 字段。

**步骤 4：验证通过**

```bash
dotnet test tests/Services/ReviewAfterSales/Leno.ReviewAfterSales.UnitTests --filter "FullyQualifiedName~ReviewEligibilityCheckerTests"
```

**步骤 5：提交**

```bash
git add -A && git commit -m "fix(reviewaftersales): 修复评价提交 SpuId/SkuId 客户端伪造（审计 2.2）

- OrderItemStatusInfo 增加 SpuId 字段
- IReviewEligibilityChecker.EnsureEligibleAsync 签名增加 spuId/skuId
- ReviewEligibilityChecker 从订单行反查比对 SpuId/SkuId
- ReviewAppService.SubmitReviewAsync 使用订单行真实 SpuId/SkuId
- HttpOrderStatusProvider / GrpcOrderStatusProvider 填充 SpuId"
```

---

### P0-2.3 修复 AfterSales.Cancel/MarkRefundFailed 领域事件缺失

**审计位置**：[file:///workspace/src/Services/ReviewAfterSales/Leno.ReviewAfterSales.Domain/Aggregates/AfterSales.cs#L388-L399]、[file:///workspace/src/Services/ReviewAfterSales/Leno.ReviewAfterSales.Domain/Aggregates/AfterSales.cs#L445-L462]、[file:///workspace/src/Services/ReviewAfterSales/Leno.ReviewAfterSales.Infrastructure/EventBus/ReviewAfterSalesIntegrationEventMapper.cs#L11-L73]

**根因**：`MarkRefundFailed` 与 `Cancel` 方法均未调用 `AddDomainEvent`，下游通知域、促销域、订单域无法感知售后单撤销/退款失败。

**步骤 1：编写失败测试**

测试文件：`tests/Services/ReviewAfterSales/Leno.ReviewAfterSales.UnitTests/Domain/AfterSalesEventsTests.cs`

```csharp
using Leno.ReviewAfterSales.Domain.Aggregates;
using Leno.ReviewAfterSales.Domain.Events;
using Leno.ReviewAfterSales.Domain.ValueObjects;
using Xunit;

namespace Leno.ReviewAfterSales.UnitTests.Domain;

public sealed class AfterSalesEventsTests
{
    private static AfterSales CreateRefunding()
    {
        var a = AfterSales.Create(
            Guid.NewGuid(), Guid.NewGuid(), null, Guid.NewGuid(), Guid.NewGuid(),
            AfterSalesType.RefundOnly, "quality", "broken", null, 10m, "CNY");
        a.Approve(Guid.NewGuid(), 10m);
        a.MarkRefunding();
        return a;
    }

    [Fact]
    public void MarkRefundFailed_Should_Raise_AfterSalesRefundFailedDomainEvent()
    {
        var a = CreateRefunding();
        a.MarkRefundFailed("channel timeout");
        Assert.Contains(a.DomainEvents, e => e is AfterSalesRefundFailedDomainEvent);
        var ev = (AfterSalesRefundFailedDomainEvent)a.DomainEvents.First(e => e is AfterSalesRefundFailedDomainEvent);
        Assert.Equal("channel timeout", ev.Reason);
    }

    [Fact]
    public void Cancel_Should_Raise_AfterSalesCancelledDomainEvent()
    {
        var userId = Guid.NewGuid();
        var sellerId = Guid.NewGuid();
        var a = AfterSales.Create(
            Guid.NewGuid(), Guid.NewGuid(), null, userId, sellerId,
            AfterSalesType.RefundOnly, "quality", "broken", null, 10m, "CNY");
        a.Cancel(userId, "changed mind");
        Assert.Contains(a.DomainEvents, e => e is AfterSalesCancelledDomainEvent);
        var ev = (AfterSalesCancelledDomainEvent)a.DomainEvents.First(e => e is AfterSalesCancelledDomainEvent);
        Assert.Equal(sellerId, ev.SellerId);
        Assert.Equal("changed mind", ev.Reason);
    }
}
```

**步骤 2：验证失败**

```bash
dotnet test tests/Services/ReviewAfterSales/Leno.ReviewAfterSales.UnitTests --filter "FullyQualifiedName~AfterSalesEventsTests"
```

预期：编译失败（`AfterSalesRefundFailedDomainEvent` / `AfterSalesCancelledDomainEvent` 类不存在）。

**步骤 3：实现修复**

3.1 在 `ReviewAfterSalesDomainEvents.cs` 增加 `AfterSalesRefundFailedDomainEvent` 与 `AfterSalesCancelledDomainEvent`：

```csharp
public sealed class AfterSalesRefundFailedDomainEvent : DomainEventBase
{
    public Guid AfterSalesId { get; init; }
    public Guid OrderId { get; init; }
    public Guid UserId { get; init; }
    public string Reason { get; init; } = string.Empty;

    public AfterSalesRefundFailedDomainEvent(Guid afterSalesId, Guid orderId, Guid userId, string reason)
        : base(afterSalesId)
    {
        AfterSalesId = afterSalesId;
        OrderId = orderId;
        UserId = userId;
        Reason = reason ?? string.Empty;
    }
}

public sealed class AfterSalesCancelledDomainEvent : DomainEventBase
{
    public Guid AfterSalesId { get; init; }
    public Guid OrderId { get; init; }
    public Guid UserId { get; init; }
    public Guid SellerId { get; init; }
    public string Reason { get; init; } = string.Empty;

    public AfterSalesCancelledDomainEvent(Guid afterSalesId, Guid orderId, Guid userId, Guid sellerId, string reason)
        : base(afterSalesId)
    {
        AfterSalesId = afterSalesId;
        OrderId = orderId;
        UserId = userId;
        SellerId = sellerId;
        Reason = reason ?? string.Empty;
    }
}
```

3.2 修改 `AfterSales.MarkRefundFailed`（与 4.2 合并修复：增加 reason 校验）：

```csharp
public void MarkRefundFailed(string reason)
{
    if (Status != AfterSalesStatus.Refunding)
    {
        throw new ReviewDomainException(
            $"当前状态 {Status} 不可标记退款失败，仅 Refunding 可标记",
            "AFTERSALES_REFUND_FAILED_STATUS_INVALID");
    }
    if (string.IsNullOrWhiteSpace(reason))
    {
        throw new ReviewDomainException("失败原因不可为空", "AFTERSALES_FAIL_REASON_EMPTY");
    }
    if (reason.Length > 512)
    {
        throw new ReviewDomainException("失败原因不可超过 512 字", "AFTERSALES_FAIL_REASON_TOO_LONG");
    }
    Status = AfterSalesStatus.Failed;
    FailReason = reason;
    AddDomainEvent(new AfterSalesRefundFailedDomainEvent(Id, OrderId, UserId, reason));
}
```

3.3 修改 `AfterSales.Cancel`（与 2.6 / 4.2 合并修复：增加 reason 校验与申请人归属校验）：

```csharp
public void Cancel(Guid userId, string reason)
{
    if (Status != AfterSalesStatus.Pending && Status != AfterSalesStatus.Approved)
    {
        throw new ReviewDomainException(
            $"当前状态 {Status} 不可撤销，仅 Pending 或 Approved 可撤销",
            "AFTERSALES_CANCEL_STATUS_INVALID");
    }
    if (userId == Guid.Empty)
    {
        throw new ReviewDomainException("UserId 不可为空", "AFTERSALES_USER_EMPTY");
    }
    if (userId != UserId)
    {
        throw new ReviewDomainException("仅申请人可撤销售后单", "AFTERSALES_CANCEL_NOT_OWNER");
    }
    if (string.IsNullOrWhiteSpace(reason))
    {
        throw new ReviewDomainException("撤销原因不可为空", "AFTERSALES_CANCEL_REASON_EMPTY");
    }
    if (reason.Length > 200)
    {
        throw new ReviewDomainException("撤销原因不可超过 200 字", "AFTERSALES_CANCEL_REASON_TOO_LONG");
    }
    Status = AfterSalesStatus.Cancelled;
    CancelledAt = DateTime.UtcNow;
    CancelReason = reason;
    AddDomainEvent(new AfterSalesCancelledDomainEvent(Id, OrderId, UserId, SellerId, reason));
}
```

3.4 修改 `ReviewAfterSalesIntegrationEventMapper`，注册两个新事件到对应集成事件。需先在 `Leno.SharedContracts/Events/AfterSalesEvents.cs` 新增 `AfterSalesRefundFailedEvent` 与 `AfterSalesCancelledEvent` 集成事件契约（包含 `AfterSalesId/OrderId/UserId/SellerId/Reason` 字段）。然后在 mapper 中：

```csharp
RegisterHandler<AfterSalesRefundFailedDomainEvent, AfterSalesRefundFailedEvent>(e =>
    new AfterSalesRefundFailedEvent(e.AfterSalesId, e.OrderId, e.UserId, e.Reason));

RegisterHandler<AfterSalesCancelledDomainEvent, AfterSalesCancelledEvent>(e =>
    new AfterSalesCancelledEvent(e.AfterSalesId, e.OrderId, e.UserId, e.SellerId, e.Reason));
```

**步骤 4：验证通过**

```bash
dotnet test tests/Services/ReviewAfterSales/Leno.ReviewAfterSales.UnitTests --filter "FullyQualifiedName~AfterSalesEventsTests"
```

**步骤 5：提交**

```bash
git add -A && git commit -m "fix(reviewaftersales): 补全 Cancel/MarkRefundFailed 领域事件（审计 2.3）

- 新增 AfterSalesRefundFailedDomainEvent / AfterSalesCancelledDomainEvent 领域事件
- AfterSales.MarkRefundFailed / Cancel 方法内 AddDomainEvent
- 新增 AfterSalesRefundFailedEvent / AfterSalesCancelledEvent 集成事件契约
- mapper 注册新事件翻译规则
- 同步合并 4.2：reason 非空与长度校验
- 同步合并 2.6（部分）：Cancel 校验 userId == UserId"
```

---

### P0-2.4 修复 RefundSucceededEventConsumer 未保存 ChannelRefundNo

**审计位置**：[file:///workspace/src/Services/ReviewAfterSales/Leno.ReviewAfterSales.Infrastructure/Consumers/RefundSucceededEventConsumer.cs#L67]、[file:///workspace/src/BuildingBlocks/Leno.SharedContracts/Events/PaymentEvents.cs#L107-L163]、[file:///workspace/src/Services/ReviewAfterSales/Leno.ReviewAfterSales.Domain/Aggregates/AfterSales.cs#L355-L382]

**根因**：`RefundCompletedEvent` 契约无 `ChannelRefundNo` 字段，Consumer 第 67 行硬编码 `null`。

**步骤 1：编写失败测试**

测试文件：`tests/Services/ReviewAfterSales/Leno.ReviewAfterSales.UnitTests/Consumers/RefundSucceededEventConsumerTests.cs`

```csharp
using Leno.Infrastructure.Abstractions;
using Leno.ReviewAfterSales.Domain.Aggregates;
using Leno.ReviewAfterSales.Domain.Repositories;
using Leno.ReviewAfterSales.Domain.ValueObjects;
using Leno.ReviewAfterSales.Infrastructure.Consumers;
using Leno.SharedContracts.Events;
using Leno.SharedKernel.Abstractions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace Leno.ReviewAfterSales.UnitTests.Consumers;

public sealed class RefundSucceededEventConsumerTests
{
    [Fact]
    public async Task HandleAsync_Should_Persist_ChannelRefundNo_From_Event()
    {
        var userId = Guid.NewGuid();
        var sellerId = Guid.NewGuid();
        var afterSalesId = Guid.NewGuid();
        var refundId = Guid.NewGuid();
        var channelRefundNo = "WX-REFUND-20260722001";

        var afterSales = AfterSales.Create(
            afterSalesId, Guid.NewGuid(), null, userId, sellerId,
            AfterSalesType.RefundOnly, "quality", "broken", null, 10m, "CNY");
        afterSales.Approve(sellerId, 10m);
        afterSales.MarkRefunding();

        var repoMock = new Mock<IAfterSalesRepository>();
        repoMock.Setup(r => r.GetByIdAsync(afterSalesId, It.IsAny<CancellationToken>())).ReturnsAsync(afterSales);
        repoMock.Setup(r => r.UpdateAsync(It.IsAny<AfterSales>(), It.IsAny<CancellationToken>()))
            .Callback((AfterSales a, CancellationToken ct) => Assert.Equal(channelRefundNo, a.ChannelRefundNo))
            .Returns(Task.CompletedTask);
        var uowMock = new Mock<IUnitOfWork>();
        var idempotencyMock = new Mock<IIdempotencyStore>();
        idempotencyMock.Setup(s => s.IsProcessedAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(false);
        idempotencyMock.Setup(s => s.MarkAsProcessedAsync(It.IsAny<string>(), It.IsAny<TimeSpan>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);

        var consumer = new RefundSucceededEventConsumer(
            repoMock.Object, uowMock.Object, NullLogger<RefundSucceededEventConsumer>.Instance, idempotencyMock.Object);

        var evt = new RefundCompletedEvent(
            Guid.NewGuid(), userId, refundId, afterSalesId, 10m, "CNY", DateTime.UtcNow)
        {
            ChannelRefundNo = channelRefundNo
        };

        await consumer.Consume(new MassTransit.Testing.TestConsumeContext<RefundCompletedEvent>(evt));

        Assert.Equal(AfterSalesStatus.Completed, afterSales.Status);
        Assert.Equal(channelRefundNo, afterSales.ChannelRefundNo);
    }
}
```

> 备注：若项目未引用 `MassTransit.Testing`，可改用 `ConsumeContext<RefundCompletedEvent>` 的 Mock 构造。重点是断言 `afterSales.ChannelRefundNo == channelRefundNo`。

**步骤 2：验证失败**

预期：编译失败（`RefundCompletedEvent` 无 `ChannelRefundNo` 字段）。

**步骤 3：实现修复**

3.1 修改 `Leno.SharedContracts/Events/PaymentEvents.cs`，给 `RefundCompletedEvent` 增加 `ChannelRefundNo` 字段（保持向后兼容，默认 `string.Empty`），并更新两个构造重载：

```csharp
public sealed class RefundCompletedEvent : IntegrationEventBase
{
    public Guid OrderId { get; init; }
    public Guid UserId { get; init; }
    public Guid RefundId { get; init; }
    public decimal RefundAmount { get; init; }
    public string Currency { get; init; } = "CNY";
    public DateTime CompletedAt { get; init; }
    public Guid AfterSalesId { get; init; }
    public string ChannelRefundNo { get; init; } = string.Empty;     // 新增

    public Guid AggregateId => RefundId;

    public RefundCompletedEvent() : base() { }

    public RefundCompletedEvent(Guid orderId, Guid userId, Guid refundId, decimal refundAmount, string currency, DateTime completedAt)
        : this(orderId, userId, refundId, afterSalesId: Guid.Empty, refundAmount, currency, completedAt, channelRefundNo: string.Empty) { }

    public RefundCompletedEvent(Guid orderId, Guid userId, Guid refundId, Guid afterSalesId, decimal refundAmount, string currency, DateTime completedAt)
        : this(orderId, userId, refundId, afterSalesId, refundAmount, currency, completedAt, channelRefundNo: string.Empty) { }

    public RefundCompletedEvent(Guid orderId, Guid userId, Guid refundId, Guid afterSalesId, decimal refundAmount, string currency, DateTime completedAt, string channelRefundNo)
        : base()
    {
        OrderId = orderId;
        UserId = userId;
        RefundId = refundId;
        AfterSalesId = afterSalesId;
        RefundAmount = refundAmount;
        Currency = string.IsNullOrWhiteSpace(currency) ? "CNY" : currency;
        CompletedAt = completedAt;
        ChannelRefundNo = channelRefundNo ?? string.Empty;
    }
}
```

3.2 修改 `RefundSucceededEventConsumer.HandleAsync` 第 67 行，透传 `ChannelRefundNo`：

```csharp
afterSales.MarkRefundCompleted(integrationEvent.RefundId, integrationEvent.RefundAmount, integrationEvent.ChannelRefundNo);
```

3.3 Payment BC 在 `RefundOrder` 聚合发布 `RefundCompletedEvent` 时填充渠道退款单号（本计划不修改 Payment BC 代码，由 Payment BC 修复计划负责；本 BC 修复仅依赖契约字段存在）。

**步骤 4：验证通过**

```bash
dotnet test tests/Services/ReviewAfterSales/Leno.ReviewAfterSales.UnitTests --filter "FullyQualifiedName~RefundSucceededEventConsumerTests"
```

**步骤 5：提交**

```bash
git add -A && git commit -m "fix(reviewaftersales): 保存渠道退款单号 ChannelRefundNo（审计 2.4）

- RefundCompletedEvent 契约增加 ChannelRefundNo 字段（默认 string.Empty 向后兼容）
- RefundSucceededEventConsumer 透传 integrationEvent.ChannelRefundNo
- 同步合并 4.8：AfterSalesId 默认值通过构造重载显式表达"
```

---

### P0-2.5 修复 ReviewGrpcService Guid→long 转换使用 GetHashCode 严重失真

**审计位置**：[file:///workspace/src/Services/ReviewAfterSales/Leno.ReviewAfterSales.Api/GrpcServices/ReviewGrpcService.cs#L75-L103]、[file:///workspace/src/Services/ReviewAfterSales/Leno.ReviewAfterSales.Api/GrpcServices/ReviewGrpcService.cs#L40-L43]

**根因**：`MapToProto` 使用 `(long)dto.SpuId.GetHashCode()` 转 long，跨进程不一致且碰撞；请求路径把 long 强转 int 嵌入 Guid，与响应路径非互逆。

**步骤 1：编写失败测试**

测试文件：`tests/Services/ReviewAfterSales/Leno.ReviewAfterSales.UnitTests/Api/ReviewGrpcServiceMappingTests.cs`

```csharp
using Leno.ReviewAfterSales.Api.GrpcServices;
using Leno.ReviewAfterSales.Application;
using Leno.SharedContracts.Grpc.Review.V1;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace Leno.ReviewAfterSales.UnitTests.Api;

public sealed class ReviewGrpcServiceMappingTests
{
    [Fact]
    public async Task GetProductRating_Should_Return_SpuIdStr_Matching_Input()
    {
        var spuId = Guid.NewGuid();
        var queryMock = new Mock<IReviewInternalQueryService>();
        queryMock.Setup(q => q.GetProductRatingAsync(spuId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ProductRatingDto
            {
                SpuId = spuId,
                AverageRating = 4.5,
                TotalCount = 10,
                PositiveCount = 8
            });

        var svc = new ReviewGrpcService(queryMock.Object, NullLogger<ReviewGrpcService>.Instance);
        var request = new GetProductRatingRequest { SpuIdStr = spuId.ToString() };

        var response = await svc.GetProductRating(request, new MockTestCallContext());

        Assert.Equal(spuId.ToString(), response.SpuIdStr);
        Assert.NotEqual((long)spuId.GetHashCode(), response.SpuId);   // GetHashCode 值不可作为权威 ID
    }

    [Fact]
    public async Task GetProductRating_Should_Return_SpuIdStr_0_When_String_Field_Only()
    {
        var spuId = Guid.NewGuid();
        var queryMock = new Mock<IReviewInternalQueryService>();
        queryMock.Setup(q => q.GetProductRatingAsync(spuId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ProductRatingDto
            {
                SpuId = spuId, AverageRating = 4.0, TotalCount = 1, PositiveCount = 1
            });

        var svc = new ReviewGrpcService(queryMock.Object, NullLogger<ReviewGrpcService>.Instance);
        var response = await svc.GetProductRating(new GetProductRatingRequest { SpuIdStr = spuId.ToString() }, new MockTestCallContext());

        Assert.Equal(0, response.SpuId);    // 旧 int64 字段强制 0，新客户端必须读 SpuIdStr
        Assert.Equal(spuId.ToString(), response.SpuIdStr);
    }
}
```

**步骤 2：验证失败**

预期：第二个用例失败（`response.SpuId` 仍为 `GetHashCode` 值而非 0）。

**步骤 3：实现修复**

修改 `ReviewGrpcService.MapToProto` 两个方法，移除 `GetHashCode`，旧 int64 字段返回 0：

```csharp
private static ProductRating MapToProto(ProductRatingDto dto) => new()
{
    SpuId = 0,                                  // 已 deprecated，强制新客户端读 SpuIdStr
    SpuIdStr = dto.SpuId.ToString(),
    AverageRating = dto.AverageRating,
    TotalCount = dto.TotalCount,
    PositiveCount = dto.PositiveCount
};

private static OrderReviews MapToProto(OrderReviewsDto dto)
{
    var proto = new OrderReviews();
    foreach (var r in dto.Reviews)
    {
        proto.Reviews.Add(new ReviewSummary
        {
            ReviewId = r.ReviewId.ToString(),
            SpuId = 0,                            // 已 deprecated
            SpuIdStr = r.SpuId.ToString(),
            Rating = r.Rating,
            Content = r.Content,
            CreatedAt = r.CreatedAt.ToString("O")
        });
    }
    return proto;
}
```

同时修改请求路径 `GetProductRating` 第 39-43 行：当 `SpuIdStr` 为空且 `SpuId != 0` 时返回 `InvalidArgument`，拒绝旧客户端：

```csharp
public override async Task<ProductRating> GetProductRating(GetProductRatingRequest request, ServerCallContext context)
{
    Guid spuId;
    if (!string.IsNullOrEmpty(request.SpuIdStr))
    {
        if (!Guid.TryParse(request.SpuIdStr, out spuId))
            throw new RpcException(new Status(StatusCode.InvalidArgument, $"Invalid spu_id_str: {request.SpuIdStr}"));
    }
    else if (request.SpuId != 0)
    {
        throw new RpcException(new Status(StatusCode.InvalidArgument,
            "SpuId int64 field is deprecated, please use SpuIdStr (Guid string) instead"));
    }
    else
    {
        throw new RpcException(new Status(StatusCode.InvalidArgument, "Either SpuId or SpuIdStr must be provided"));
    }
    // ...
}
```

**步骤 4：验证通过**

```bash
dotnet test tests/Services/ReviewAfterSales/Leno.ReviewAfterSales.UnitTests --filter "FullyQualifiedName~ReviewGrpcServiceMappingTests"
```

**步骤 5：提交**

```bash
git add -A && git commit -m "fix(reviewaftersales): 移除 ReviewGrpcService Guid.GetHashCode 不可逆映射（审计 2.5）

- MapToProto 旧 int64 字段强制返回 0，新客户端必须读 SpuIdStr
- 请求路径拒绝非零 SpuId 旧客户端，要求 SpuIdStr
- 配套 ADR-0007 Guid→string 迁移策略"
```

---

### P0-2.6 修复买家撤销售后/买家退货/卖家驳回售后均缺失申请人/卖家归属校验

**审计位置**：[file:///workspace/src/Services/ReviewAfterSales/Leno.ReviewAfterSales.Application/Services/AfterSalesAppService.cs#L105-L113]、[file:///workspace/src/Services/ReviewAfterSales/Leno.ReviewAfterSales.Application/Services/AfterSalesAppService.cs#L183-L202]、[file:///workspace/src/Services/ReviewAfterSales/Leno.ReviewAfterSales.Domain/Aggregates/AfterSales.cs#L276-L306]、[file:///workspace/src/Services/ReviewAfterSales/Leno.ReviewAfterSales.Domain/Aggregates/AfterSales.cs#L445-L462]

**根因**：
- `RejectAfterSalesAsync` 未调用 `RequireOwnedAfterSales`，卖家 A 可驳回卖家 B 的售后单。
- `ReturnGoodsAsync` 接收 `userId` 但未校验，聚合 `ReturnGoods` 也不接收 `userId`。
- `Cancel` 只校验 `userId != Guid.Empty`，未校验 `userId == this.UserId`（与 2.3 一并修复）。

**步骤 1：编写失败测试**

测试文件：`tests/Services/ReviewAfterSales/Leno.ReviewAfterSales.UnitTests/Application/AfterSalesOwnershipTests.cs`

```csharp
using Leno.ReviewAfterSales.Application.Services;
using Leno.ReviewAfterSales.Domain.Aggregates;
using Leno.ReviewAfterSales.Domain.Exceptions;
using Leno.ReviewAfterSales.Domain.Repositories;
using Leno.ReviewAfterSales.Domain.Services;
using Leno.ReviewAfterSales.Domain.ValueObjects;
using Leno.SharedKernel.Abstractions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace Leno.ReviewAfterSales.UnitTests.Application;

public sealed class AfterSalesOwnershipTests
{
    private static AfterSales CreatePending()
        => AfterSales.Create(
            Guid.NewGuid(), Guid.NewGuid(), null,
            userId: Guid.NewGuid(), sellerId: Guid.NewGuid(),
            AfterSalesType.RefundOnly, "quality", "broken", null, 10m, "CNY");

    [Fact]
    public async Task RejectAfterSalesAsync_Should_Throw_When_Operator_Not_Owner_Seller()
    {
        var afterSales = CreatePending();
        var repoMock = new Mock<IAfterSalesRepository>();
        repoMock.Setup(r => r.GetByIdAsync(afterSales.Id, It.IsAny<CancellationToken>())).ReturnsAsync(afterSales);
        var uowMock = new Mock<IUnitOfWork>();
        var eventBusMock = new Mock<IEventBus>();
        var paymentMock = new Mock<IPaymentInfoQueryService>();
        var eligibilityMock = new Mock<IAfterSalesEligibilityChecker>();

        var svc = new AfterSalesAppService(
            repoMock.Object, eligibilityMock.Object, paymentMock.Object,
            eventBusMock.Object, uowMock.Object, NullLogger<AfterSalesAppService>.Instance);

        var nonOwnerOperator = Guid.NewGuid();
        var act = async () => await svc.RejectAfterSalesAsync(afterSales.Id, nonOwnerOperator, "rejected");

        var ex = await Assert.ThrowsAsync<ReviewDomainException>(act);
        Assert.Equal("AFTERSALES_NOT_OWNED", ex.ErrorCode);
    }

    [Fact]
    public async Task ReturnGoodsAsync_Should_Throw_When_UserId_Not_Owner()
    {
        var sellerId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var afterSales = AfterSales.Create(
            Guid.NewGuid(), Guid.NewGuid(), null, userId, sellerId,
            AfterSalesType.ReturnRefund, "quality", "broken", null, 10m, "CNY");
        afterSales.Approve(sellerId, 10m);

        var repoMock = new Mock<IAfterSalesRepository>();
        repoMock.Setup(r => r.GetByIdAsync(afterSales.Id, It.IsAny<CancellationToken>())).ReturnsAsync(afterSales);
        var uowMock = new Mock<IUnitOfWork>();
        var eventBusMock = new Mock<IEventBus>();
        var paymentMock = new Mock<IPaymentInfoQueryService>();
        var eligibilityMock = new Mock<IAfterSalesEligibilityChecker>();

        var svc = new AfterSalesAppService(
            repoMock.Object, eligibilityMock.Object, paymentMock.Object,
            eventBusMock.Object, uowMock.Object, NullLogger<AfterSalesAppService>.Instance);

        var attacker = Guid.NewGuid();
        var act = async () => await svc.ReturnGoodsAsync(afterSales.Id, attacker, "TRACK001");

        var ex = await Assert.ThrowsAsync<ReviewDomainException>(act);
        Assert.Equal("AFTERSALES_NOT_OWNED", ex.ErrorCode);
    }

    [Fact]
    public void Cancel_Should_Throw_When_UserId_Not_Owner()
    {
        var userId = Guid.NewGuid();
        var afterSales = AfterSales.Create(
            Guid.NewGuid(), Guid.NewGuid(), null, userId, Guid.NewGuid(),
            AfterSalesType.RefundOnly, "quality", "broken", null, 10m, "CNY");

        var attacker = Guid.NewGuid();
        var ex = Assert.Throws<ReviewDomainException>(() => afterSales.Cancel(attacker, "changed mind"));
        Assert.Equal("AFTERSALES_CANCEL_NOT_OWNER", ex.ErrorCode);
    }
}
```

**步骤 2：验证失败**

预期：用例 1 与用例 2 失败（`RejectAfterSalesAsync` 与 `ReturnGoodsAsync` 未校验归属，调用成功）；用例 3 失败（`Cancel` 不校验 `userId == UserId`）。

**步骤 3：实现修复**

3.1 修改 `AfterSalesAppService.RejectAfterSalesAsync`，在 `Reject` 前调用 `RequireOwnedAfterSales`：

```csharp
public async Task RejectAfterSalesAsync(Guid afterSalesId, Guid operatorId, string reason, CancellationToken ct = default)
{
    var afterSales = await _afterSalesRepository.GetByIdAsync(afterSalesId, ct)
        ?? throw new InvalidOperationException($"售后单不存在 AfterSalesId={afterSalesId}");

    RequireOwnedAfterSales(afterSales, operatorId);   // 新增

    afterSales.Reject(operatorId, reason);
    await _afterSalesRepository.UpdateAsync(afterSales, ct);
    await _unitOfWork.SaveEntitiesAsync(ct);
}
```

3.2 修改 `AfterSalesAppService.ReturnGoodsAsync`，校验 `userId == afterSales.UserId`：

```csharp
public async Task ReturnGoodsAsync(Guid afterSalesId, Guid userId, string trackingNo, CancellationToken ct = default)
{
    var afterSales = await _afterSalesRepository.GetByIdAsync(afterSalesId, ct)
        ?? throw new InvalidOperationException($"售后单不存在 AfterSalesId={afterSalesId}");

    if (afterSales.UserId != userId)
        throw new ReviewDomainException("无权操作此售后单", "AFTERSALES_NOT_OWNED");

    afterSales.ReturnGoods(trackingNo);
    await _afterSalesRepository.UpdateAsync(afterSales, ct);
    await _unitOfWork.SaveEntitiesAsync(ct);
}
```

3.3 `AfterSales.Cancel` 已在 2.3 步骤 3.3 中校验 `userId == UserId`。

**步骤 4：验证通过**

```bash
dotnet test tests/Services/ReviewAfterSales/Leno.ReviewAfterSales.UnitTests --filter "FullyQualifiedName~AfterSalesOwnershipTests"
```

**步骤 5：提交**

```bash
git add -A && git commit -m "fix(reviewaftersales): 补全 Reject/ReturnGoods/Cancel 归属校验（审计 2.6）

- RejectAfterSalesAsync 调用 RequireOwnedAfterSales 校验卖家归属
- ReturnGoodsAsync 校验 afterSales.UserId == userId
- AfterSales.Cancel 校验 userId == UserId（与 2.3 合并修复）"
```

---

### P0-2.7 修复 SellerReply 完全缺失卖家归属校验

**审计位置**：[file:///workspace/src/Services/ReviewAfterSales/Leno.ReviewAfterSales.Api/Controllers/ReviewsController.cs#L125-L133]、[file:///workspace/src/Services/ReviewAfterSales/Leno.ReviewAfterSales.Application/Services/ReviewAppService.cs#L57-L65]、[file:///workspace/src/Services/ReviewAfterSales/Leno.ReviewAfterSales.Domain/Aggregates/Review.cs#L174-L194]

**根因**：`ReviewsController.SellerReplyAsync` 不传当前卖家标识，应用服务与聚合 `SellerReply` 也不接收 `sellerId`，`Review` 聚合无 `SellerId` 字段。

**步骤 1：编写失败测试**

测试文件：`tests/Services/ReviewAfterSales/Leno.ReviewAfterSales.UnitTests/Domain/ReviewSellerReplyTests.cs`

```csharp
using Leno.ReviewAfterSales.Domain.Aggregates;
using Leno.ReviewAfterSales.Domain.Exceptions;
using Xunit;

namespace Leno.ReviewAfterSales.UnitTests.Domain;

public sealed class ReviewSellerReplyTests
{
    private static Review CreateApprovedReview(Guid sellerId)
    {
        var r = Review.Create(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            Guid.NewGuid(), rating: 5, "good", null);
        r.Approve(Guid.NewGuid());
        // 反射设置 SellerId（生产由 Create 工厂注入；测试先模拟）
        return r;
    }

    [Fact]
    public void SellerReply_Should_Throw_When_SellerId_Mismatch()
    {
        var sellerId = Guid.NewGuid();
        var review = Review.Create(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            Guid.NewGuid(), 5, "good", null, sellerId: sellerId);
        review.Approve(Guid.NewGuid());

        var attacker = Guid.NewGuid();
        var ex = Assert.Throws<ReviewDomainException>(() => review.SellerReply(attacker, "reply content"));
        Assert.Equal("REVIEW_NOT_OWNED", ex.ErrorCode);
    }

    [Fact]
    public void SellerReply_Should_Record_SellerReplyBy_When_Match()
    {
        var sellerId = Guid.NewGuid();
        var review = Review.Create(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            Guid.NewGuid(), 5, "good", null, sellerId: sellerId);
        review.Approve(Guid.NewGuid());

        review.SellerReply(sellerId, "thanks");

        Assert.Equal("thanks", review.SellerReplyContent);
        Assert.Equal(sellerId, review.SellerReplyBy);
        Assert.True(review.SellerReplyAt.HasValue);
    }
}
```

**步骤 2：验证失败**

预期：编译失败（`Review.Create` 无 `sellerId` 参数，`SellerReply` 无 `sellerId` 参数，`Review` 无 `SellerId/SellerReplyBy/SellerReplyAt` 字段）。

**步骤 3：实现修复**

3.1 修改 `Review.cs` 聚合根，增加 `SellerId` / `SellerReplyBy` / `SellerReplyAt` 字段，更新 `Create` 工厂签名：

```csharp
public Guid SellerId { get; private set; }
public Guid? SellerReplyBy { get; private set; }
public DateTime? SellerReplyAt { get; private set; }

public static Review Create(
    Guid reviewId, Guid orderId, Guid orderLineId, Guid spuId, Guid skuId,
    Guid userId, int rating, string content, List<string> images,
    Guid sellerId, double newScore = 0, int reviewCount = 0)
{
    if (sellerId == Guid.Empty)
        throw new ReviewDomainException("SellerId 不可为空", "REVIEW_SELLER_EMPTY");
    // ...其余校验不变
    var review = new Review(reviewId)
    {
        // ...
        SellerId = sellerId,
        // ...
    };
    review.AddDomainEvent(new ReviewSubmittedDomainEvent(reviewId, userId, spuId, rating, newScore, reviewCount));
    return review;
}

public void SellerReply(Guid sellerId, string content)
{
    if (Status != ReviewStatus.Approved)
        throw new ReviewDomainException($"当前状态 {Status} 不可回复，仅 Approved 可回复", "REVIEW_REPLY_STATUS_INVALID");
    if (sellerId != SellerId)
        throw new ReviewDomainException("无权回复此评价", "REVIEW_NOT_OWNED");
    if (string.IsNullOrWhiteSpace(content))
        throw new ReviewDomainException("回复内容不可为空", "REVIEW_REPLY_EMPTY");
    if (content.Length > 500)
        throw new ReviewDomainException("回复内容不可超过 500 字", "REVIEW_REPLY_TOO_LONG");

    SellerReplyContent = content;
    SellerReplyBy = sellerId;
    SellerReplyAt = DateTime.UtcNow;
}
```

3.2 修改 `ReviewConfiguration.cs`，增加 `seller_id` / `seller_reply_by` / `seller_reply_at` 列映射。

3.3 修改 `ReviewAppService.SellerReplyAsync`，接收并透传 `sellerId`：

```csharp
public async Task SellerReplyAsync(Guid reviewId, Guid sellerId, string content, CancellationToken ct = default)
{
    var review = await _reviewRepository.GetByIdAsync(reviewId, ct)
        ?? throw new InvalidOperationException($"评价不存在 ReviewId={reviewId}");

    review.SellerReply(sellerId, content);
    await _reviewRepository.UpdateAsync(review, ct);
    await _unitOfWork.SaveEntitiesAsync(ct);
}
```

3.4 修改 `IReviewAppService` 接口签名增加 `sellerId`。

3.5 修改 `ReviewsController.SellerReplyAsync`，读取 `GetCurrentUserId()` 透传：

```csharp
[Authorize(Roles = "Seller")]
[HttpPost("api/reviews/{id:guid}/reply")]
public async Task<IActionResult> SellerReplyAsync(Guid id, [FromBody] SellerReplyDto dto, CancellationToken ct)
{
    var sellerId = GetCurrentUserId();
    await _reviewAppService.SellerReplyAsync(id, sellerId, dto.Content, ct);
    return Ok(ApiResponse.Success());
}
```

3.6 修改 `ReviewAppService.SubmitReviewAsync`，调用 `Review.Create` 时传入从 `OrderStatusInfo.SellerId`（已在 2.1 中新增）取得的真实 SellerId。

3.7 新增 EF Core 迁移：`dotnet ef migrations add AddReviewSellerFields --project src/Services/ReviewAfterSales/Leno.ReviewAfterSales.Infrastructure --startup-project src/Services/ReviewAfterSales/Leno.ReviewAfterSales.Api`。

**步骤 4：验证通过**

```bash
dotnet test tests/Services/ReviewAfterSales/Leno.ReviewAfterSales.UnitTests --filter "FullyQualifiedName~ReviewSellerReplyTests"
```

**步骤 5：提交**

```bash
git add -A && git commit -m "fix(reviewaftersales): 补全 SellerReply 卖家归属校验（审计 2.7）

- Review 聚合增加 SellerId/SellerReplyBy/SellerReplyAt 字段
- Review.Create 工厂接收 sellerId 参数
- Review.SellerReply 校验 sellerId == SellerId
- IReviewAppService / ReviewAppService / ReviewsController 透传 sellerId
- ReviewConfiguration 增加 seller_id/seller_reply_by/seller_reply_at 列映射
- 新增 EF Core 迁移 AddReviewSellerFields"
```

---

### P0-2.8 修复聚合内部 List 通过 Images 属性直接暴露

**审计位置**：[file:///workspace/src/Services/ReviewAfterSales/Leno.ReviewAfterSales.Domain/Aggregates/AfterSales.cs#L43-L44]、[file:///workspace/src/Services/ReviewAfterSales/Leno.ReviewAfterSales.Domain/Aggregates/Review.cs#L40-L41]、[file:///workspace/src/Services/ReviewAfterSales/Leno.ReviewAfterSales.Application/Services/AfterSalesAppService.cs#L262]、[file:///workspace/src/Services/ReviewAfterSales/Leno.ReviewAfterSales.Application/Services/ReviewAppService.cs#L132]

**根因**：`Images` getter 直接返回内部 `_images` 引用；`ToDto` 把同一引用赋给 DTO。

**步骤 1：编写失败测试**

测试文件：`tests/Services/ReviewAfterSales/Leno.ReviewAfterSales.UnitTests/Domain/AggregateImagesEncapsulationTests.cs`

```csharp
using Leno.ReviewAfterSales.Domain.Aggregates;
using Leno.ReviewAfterSales.Domain.ValueObjects;
using Xunit;

namespace Leno.ReviewAfterSales.UnitTests.Domain;

public sealed class AggregateImagesEncapsulationTests
{
    [Fact]
    public void AfterSales_Images_Should_Be_ReadOnly_And_Not_Mutable_From_Outside()
    {
        var afterSales = AfterSales.Create(
            Guid.NewGuid(), Guid.NewGuid(), null, Guid.NewGuid(), Guid.NewGuid(),
            AfterSalesType.RefundOnly, "quality", "broken",
            new List<string> { "url1" }, 10m, "CNY");

        // 不应编译通过：afterSales.Images.Add(...) 需被禁止。IReadOnlyList 无 Add 方法。
        Assert.IsType<IReadOnlyList<string>>(afterSales.Images);
        Assert.Single(afterSales.Images);

        // 防御性拷贝验证：外部传入的 images 列表 mutate 不影响聚合（与 4.3 合并验证）
        var externalImages = new List<string> { "url1" };
        var a2 = AfterSales.Create(
            Guid.NewGuid(), Guid.NewGuid(), null, Guid.NewGuid(), Guid.NewGuid(),
            AfterSalesType.RefundOnly, "quality", "broken", externalImages, 10m, "CNY");
        externalImages.Add("url2");
        Assert.Single(a2.Images);
    }

    [Fact]
    public void Review_Images_Should_Be_ReadOnly_And_Not_Mutable_From_Outside()
    {
        var review = Review.Create(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            Guid.NewGuid(), 5, "good", new List<string> { "url1" }, Guid.NewGuid());

        Assert.IsType<IReadOnlyList<string>>(review.Images);
        Assert.Single(review.Images);
    }
}
```

**步骤 2：验证失败**

预期：编译失败（`AfterSales.Images` / `Review.Images` 类型为 `List<string>` 而非 `IReadOnlyList<string>`，外部可 `Add`；`Create` 工厂未做防御性拷贝）。

**步骤 3：实现修复**

3.1 修改 `AfterSales.cs` 第 43-44 行：

```csharp
private List<string> _images = [];
public IReadOnlyList<string> Images => _images.AsReadOnly();
```

> 备注：保留 `private List<string> _images` 字段供 EF Core backing field 配置使用。删除原 `private set`。

3.2 修改 `AfterSales.Create` 工厂第 169 行（与 4.3 合并修复：防御性拷贝）：

```csharp
var imageList = (images ?? []).ToList();   // 防御性拷贝
if (imageList.Count > 5)
    throw new ReviewDomainException($"凭证图片数量超限：{imageList.Count}，最多 5 张", "AFTERSALES_IMAGES_TOO_MANY");
// ...
_images = imageList    // 直接赋 backing field（不再经 private setter）
```

> 备注：因移除了 `Images` 的 `private set`，工厂方法直接赋值 `_images` 字段。

3.3 修改 `Review.cs` 第 40-41 行：

```csharp
private List<string> _images = [];
public IReadOnlyList<string> Images => _images.AsReadOnly();
```

3.4 修改 `Review.Create` 第 144 行：`var imageList = (images ?? []).ToList();`，直接赋 `_images`。

3.5 修改 `AfterSalesConfiguration.cs` 第 51-56 行，使用 backing field 配置而非 `Property(a => a.Images)`：

```csharp
builder.Property<List<string>>("_images")
    .HasColumnName("images")
    .HasColumnType("nvarchar(max)")
    .HasConversion(
        v => JsonSerializer.Serialize(v, (JsonSerializerOptions?)null),
        v => JsonSerializer.Deserialize<List<string>>(v, (JsonSerializerOptions?)null) ?? new List<string>())
    .Metadata.SetValueComparer(new ValueComparer<List<string>>(
        (a, b) => (a == null && b == null) || (a != null && b != null && a.SequenceEqual(b)),
        c => c == null ? 0 : c.Aggregate(0, (h, v) => h ^ v.GetHashCode()),
        c => c == null ? new List<string>() : c.ToList()));
```

3.6 修改 `ReviewConfiguration.cs` 同样模式配置 `_images`。

3.7 修改 `AfterSalesAppService.ToDto` 第 262 行与 `ReviewAppService.ToDto` 第 132 行，使用防御性拷贝：

```csharp
Images = afterSales.Images.ToList(),
```

```csharp
Images = review.Images.ToList(),
```

3.8 修改 `AfterSalesDto.Images` / `ReviewDto.Images` 类型保持 `List<string>`（DTO 接受可变），但服务端赋值时是新 List。

**步骤 4：验证通过**

```bash
dotnet test tests/Services/ReviewAfterSales/Leno.ReviewAfterSales.UnitTests --filter "FullyQualifiedName~AggregateImagesEncapsulationTests"
```

**步骤 5：提交**

```bash
git add -A && git commit -m "fix(reviewaftersales): 封装 Images 集合防止外部 mutate（审计 2.8 / 4.3）

- AfterSales/Review Images 属性改为 IReadOnlyList<string>，getter 返回 AsReadOnly()
- Create 工厂对入参 images 做防御性拷贝 ToList()
- EF Core 配置改为 backing field _images 模式 + ValueComparer
- ToDto 使用 Images.ToList() 防御性拷贝避免聚合与 DTO 共享引用"
```

---

### P0-2.9 修复 HasActiveByOrderLineAsync 活跃状态过滤不全

**审计位置**：[file:///workspace/src/Services/ReviewAfterSales/Leno.ReviewAfterSales.Infrastructure/Repositories/EfCoreAfterSalesRepository.cs#L34-L45]、[file:///workspace/src/Services/ReviewAfterSales/Leno.ReviewAfterSales.Domain/ValueObjects/AfterSalesEnums.cs#L24-L52]

**根因**：`activeStatuses` 仅含 `[Pending, Approved, Refunding]`，遗漏 `ReturnGoods=7` 与 `ConfirmReturn=8`。

**步骤 1：编写失败测试**

测试文件：`tests/Services/ReviewAfterSales/Leno.ReviewAfterSales.UnitTests/Infrastructure/AfterSalesRepositoryActiveStatusTests.cs`

```csharp
using Leno.ReviewAfterSales.Domain.Aggregates;
using Leno.ReviewAfterSales.Domain.Repositories;
using Leno.ReviewAfterSales.Domain.ValueObjects;
using Leno.ReviewAfterSales.Infrastructure;
using Leno.ReviewAfterSales.Infrastructure.Repositories;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Leno.ReviewAfterSales.UnitTests.Infrastructure;

public sealed class AfterSalesRepositoryActiveStatusTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly ReviewAfterSalesDbContext _context;
    private readonly EfCoreAfterSalesRepository _repo;

    public AfterSalesRepositoryActiveStatusTests()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();
        var options = new DbContextOptionsBuilder<ReviewAfterSalesDbContext>()
            .UseSqlite(_connection)
            .Options;
        _context = new ReviewAfterSalesDbContext(options);
        _context.Database.EnsureCreated();
        _repo = new EfCoreAfterSalesRepository(_context);
    }

    [Theory]
    [InlineData(AfterSalesStatus.ReturnGoods)]
    [InlineData(AfterSalesStatus.ConfirmReturn)]
    public async Task HasActiveByOrderLineAsync_Should_Return_True_For_Active_Status(AfterSalesStatus status)
    {
        var orderLineId = Guid.NewGuid();
        var afterSales = AfterSales.Create(
            Guid.NewGuid(), Guid.NewGuid(), orderLineId, Guid.NewGuid(), Guid.NewGuid(),
            AfterSalesType.ReturnRefund, "quality", "broken", null, 10m, "CNY");
        // 反射强制推进到指定状态
        typeof(AfterSales).GetProperty("Status")!.SetValue(afterSales, status);

        await _repo.AddAsync(afterSales, default);
        await _context.SaveChangesAsync();

        var hasActive = await _repo.HasActiveByOrderLineAsync(orderLineId, AfterSalesType.ReturnRefund, default);

        Assert.True(hasActive);
    }

    public void Dispose()
    {
        _context.Dispose();
        _connection.Dispose();
    }
}
```

**步骤 2：验证失败**

预期：两个用例失败（`ReturnGoods` 与 `ConfirmReturn` 状态未被纳入 activeStatuses，返回 `false`）。

**步骤 3：实现修复**

修改 `EfCoreAfterSalesRepository.HasActiveByOrderLineAsync` 第 36-41 行：

```csharp
public async Task<bool> HasActiveByOrderLineAsync(Guid orderLineId, AfterSalesType type, CancellationToken ct = default)
{
    var activeStatuses = new List<AfterSalesStatus>
    {
        AfterSalesStatus.Pending,
        AfterSalesStatus.Approved,
        AfterSalesStatus.ReturnGoods,        // 新增
        AfterSalesStatus.ConfirmReturn,      // 新增
        AfterSalesStatus.Refunding
    };

    return await _context.AfterSales
        .AsNoTracking()
        .AnyAsync(a => a.OrderLineId == orderLineId && a.Type == type && activeStatuses.Contains(a.Status), ct);
}
```

**步骤 4：验证通过**

```bash
dotnet test tests/Services/ReviewAfterSales/Leno.ReviewAfterSales.UnitTests --filter "FullyQualifiedName~AfterSalesRepositoryActiveStatusTests"
```

**步骤 5：提交**

```bash
git add -A && git commit -m "fix(reviewaftersales): 补全 HasActiveByOrderLineAsync 活跃状态过滤（审计 2.9）

- activeStatuses 新增 ReturnGoods=7 与 ConfirmReturn=8
- 防止同订单行 ReturnGoods/ConfirmReturn 状态下重复提交售后单
- 同步合并 3.8（部分）：HasActiveByOrderLineAsync 加 AsNoTracking"
```

---

### P0-2.10 修复买家按订单查询售后单/按订单行查询评价均缺失订单归属校验

**审计位置**：[file:///workspace/src/Services/ReviewAfterSales/Leno.ReviewAfterSales.Api/Controllers/AfterSalesController.cs#L69-L77]、[file:///workspace/src/Services/ReviewAfterSales/Leno.ReviewAfterSales.Api/Controllers/ReviewsController.cs#L47-L55]、[file:///workspace/src/Services/ReviewAfterSales/Leno.ReviewAfterSales.Application/Services/AfterSalesAppService.cs#L205-L209]、[file:///workspace/src/Services/ReviewAfterSales/Leno.ReviewAfterSales.Application/Services/ReviewAppService.cs#L97-L102]

**根因**：`GetAfterSalesByOrderAsync` 与 `GetReviewByOrderLineAsync` 仅按 ID 查询，不校验当前用户是否拥有该订单/订单行。

**步骤 1：编写失败测试**

测试文件：`tests/Services/ReviewAfterSales/Leno.ReviewAfterSales.UnitTests/Application/AfterSalesQueryOwnershipTests.cs`

```csharp
using Leno.ReviewAfterSales.Application.Services;
using Leno.ReviewAfterSales.Domain.Exceptions;
using Leno.ReviewAfterSales.Domain.Repositories;
using Leno.ReviewAfterSales.Domain.Services;
using Leno.SharedKernel.Abstractions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace Leno.ReviewAfterSales.UnitTests.Application;

public sealed class AfterSalesQueryOwnershipTests
{
    [Fact]
    public async Task GetByOrderIdForUserAsync_Should_Throw_When_User_Not_Order_Owner()
    {
        var orderId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var orderProviderMock = new Mock<IOrderStatusProvider>();
        orderProviderMock.Setup(p => p.GetOrderStatusAsync(orderId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new OrderStatusInfo { OrderId = orderId, UserId = Guid.NewGuid() });
        var repoMock = new Mock<IAfterSalesRepository>();
        var uowMock = new Mock<IUnitOfWork>();
        var eventBusMock = new Mock<IEventBus>();
        var paymentMock = new Mock<IPaymentInfoQueryService>();
        var eligibilityMock = new Mock<IAfterSalesEligibilityChecker>();

        var svc = new AfterSalesAppService(
            repoMock.Object, eligibilityMock.Object, paymentMock.Object,
            eventBusMock.Object, uowMock.Object, NullLogger<AfterSalesAppService>.Instance);

        var act = async () => await svc.GetByOrderIdForUserAsync(orderId, userId);

        var ex = await Assert.ThrowsAsync<ReviewDomainException>(act);
        Assert.Equal("AFTERSALES_FORBIDDEN", ex.ErrorCode);
    }
}
```

**步骤 2：验证失败**

预期：编译失败（`AfterSalesAppService` 无 `GetByOrderIdForUserAsync` 方法，未注入 `IOrderStatusProvider`）。

**步骤 3：实现修复**

3.1 修改 `AfterSalesAppService` 构造函数注入 `IOrderStatusProvider`。

3.2 新增 `GetByOrderIdForUserAsync` 方法：

```csharp
public async Task<List<AfterSalesDto>> GetByOrderIdForUserAsync(Guid orderId, Guid userId, CancellationToken ct = default)
{
    var order = await _orderStatusProvider.GetOrderStatusAsync(orderId, ct)
        ?? throw new InvalidOperationException($"订单不存在 OrderId={orderId}");
    if (order.UserId != userId)
        throw new ReviewDomainException("无权查询此订单售后", "AFTERSALES_FORBIDDEN");

    var items = await _afterSalesRepository.GetByOrderIdAsync(orderId, ct);
    return items.ConvertAll(ToDto);
}
```

3.3 修改 `IAfterSalesAppService` 接口增加 `GetByOrderIdForUserAsync`。

3.4 修改 `AfterSalesController.GetAfterSalesByOrderAsync`：

```csharp
[Authorize(Roles = "Buyer")]
[HttpGet("api/after-sales/order/{orderId:guid}")]
public async Task<IActionResult> GetAfterSalesByOrderAsync(Guid orderId, CancellationToken ct)
{
    var userId = GetCurrentUserId();
    var result = await _afterSalesAppService.GetByOrderIdForUserAsync(orderId, userId, ct);
    return Ok(ApiResponse.Success(result));
}
```

3.5 同样模式修改 `ReviewAppService`，新增 `GetReviewByOrderLineForUserAsync(orderLineId, userId)` 方法。`ReviewsController.GetReviewByOrderLineAsync` 调用新方法。该方法内通过 `IOrderStatusProvider` 反查订单行归属（需先在 `IReviewRepository` 新增 `GetByOrderLineWithOrderAsync` 或在 `IOrderStatusProvider` 新增 `GetOrderLineOwnerAsync`，按实际便利选其一）。

> 简化方案：在 `IOrderStatusProvider` 增加按 `orderLineId` 反查订单的方法，或在 `ReviewRepository` 增加 `GetByOrderLineAsync` 同时返回 `OrderId`，再调用 `IOrderStatusProvider.GetOrderStatusAsync(orderId)` 校验 `UserId`。本计划采用后者，避免修改订单域接口。

**步骤 4：验证通过**

```bash
dotnet test tests/Services/ReviewAfterSales/Leno.ReviewAfterSales.UnitTests --filter "FullyQualifiedName~AfterSalesQueryOwnershipTests"
```

**步骤 5：提交**

```bash
git add -A && git commit -m "fix(reviewaftersales): 补全按订单/订单行查询的归属校验（审计 2.10）

- AfterSalesAppService 新增 GetByOrderIdForUserAsync，通过 IOrderStatusProvider 校验订单归属
- ReviewAppService 新增 GetReviewByOrderLineForUserAsync，反查订单校验归属
- AfterSalesController.GetAfterSalesByOrderAsync / ReviewsController.GetReviewByOrderLineAsync 调用新方法
- 防止买家 A 越权查询买家 B 的售后/评价详情"
```

---

### P0-2.11 修复 RefundCompleted 事件回环

**审计位置**：[file:///workspace/src/Services/ReviewAfterSales/Leno.ReviewAfterSales.Infrastructure/EventBus/ReviewAfterSalesIntegrationEventMapper.cs#L43-L46]、[file:///workspace/src/Services/ReviewAfterSales/Leno.ReviewAfterSales.Infrastructure/Consumers/RefundSucceededEventConsumer.cs#L15-L74]、[file:///workspace/src/Services/ReviewAfterSales/Leno.ReviewAfterSales.Domain/Aggregates/AfterSales.cs#L355-L382]

**根因**：本 BC 消费 `RefundCompletedEvent` 后又发布 `RefundCompletedEvent`，造成自身与其他 BC 重复消费。

**步骤 1：编写失败测试**

测试文件：`tests/Services/ReviewAfterSales/Leno.ReviewAfterSales.UnitTests/EventBus/ReviewAfterSalesIntegrationEventMapperTests.cs`

```csharp
using Leno.ReviewAfterSales.Domain.Events;
using Leno.ReviewAfterSales.Infrastructure.EventBus;
using Leno.SharedContracts.Events;
using Xunit;

namespace Leno.ReviewAfterSales.UnitTests.EventBus;

public sealed class ReviewAfterSalesIntegrationEventMapperTests
{
    [Fact]
    public void Mapper_Should_Not_Register_RefundCompletedEvent_For_AfterSalesRefundCompletedDomainEvent()
    {
        var mapper = new ReviewAfterSalesIntegrationEventMapper();

        // AfterSalesRefundCompletedDomainEvent 应映射为 AfterSalesRefundCompletedEvent（独立集成事件），而非 RefundCompletedEvent
        var domainEvent = new AfterSalesRefundCompletedDomainEvent(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), 10m, "CNY", DateTime.UtcNow);

        var integrationEvents = mapper.Map(domainEvent).ToList();

        Assert.NotEmpty(integrationEvents);
        Assert.DoesNotContain(integrationEvents, e => e is RefundCompletedEvent);
        Assert.Contains(integrationEvents, e => e is AfterSalesRefundCompletedEvent);
    }
}
```

**步骤 2：验证失败**

预期：用例失败（mapper 第 43-46 行将 `AfterSalesRefundCompletedDomainEvent` 映射为 `RefundCompletedEvent`）。

**步骤 3：实现修复**

3.1 在 `Leno.SharedContracts/Events/AfterSalesEvents.cs` 新增独立集成事件 `AfterSalesRefundCompletedEvent`：

```csharp
public sealed class AfterSalesRefundCompletedEvent : IntegrationEventBase
{
    public Guid AfterSalesId { get; init; }
    public Guid OrderId { get; init; }
    public Guid UserId { get; init; }
    public Guid RefundId { get; init; }
    public decimal RefundAmount { get; init; }
    public string Currency { get; init; } = "CNY";
    public DateTime CompletedAt { get; init; }
    public string ChannelRefundNo { get; init; } = string.Empty;

    public Guid AggregateId => AfterSalesId;

    public AfterSalesRefundCompletedEvent() : base() { }

    public AfterSalesRefundCompletedEvent(
        Guid afterSalesId, Guid orderId, Guid userId, Guid refundId,
        decimal refundAmount, string currency, DateTime completedAt, string channelRefundNo)
        : base()
    {
        AfterSalesId = afterSalesId;
        OrderId = orderId;
        UserId = userId;
        RefundId = refundId;
        RefundAmount = refundAmount;
        Currency = string.IsNullOrWhiteSpace(currency) ? "CNY" : currency;
        CompletedAt = completedAt;
        ChannelRefundNo = channelRefundNo ?? string.Empty;
    }
}
```

3.2 修改 `ReviewAfterSalesIntegrationEventMapper` 第 43-46 行，改用独立事件：

```csharp
RegisterHandler<AfterSalesRefundCompletedDomainEvent, AfterSalesRefundCompletedEvent>(e =>
    new AfterSalesRefundCompletedEvent(
        e.AfterSalesId, e.OrderId, e.UserId, e.RefundId,
        e.RefundAmount, e.Currency, e.CompletedAt, channelRefundNo: string.Empty));
```

> 备注：`ChannelRefundNo` 字段需要 `AfterSalesRefundCompletedDomainEvent` 携带。需修改 `AfterSales.MarkRefundCompleted` 在 `AddDomainEvent` 时传入 `channelRefundNo`，并扩展领域事件字段集。

3.3 通知 Order/Promotion/Notification BC 消费方迁移：从订阅 `RefundCompletedEvent`（仅由 Payment BC 发布）改为额外订阅 `AfterSalesRefundCompletedEvent`（由 ReviewAfterSales BC 发布），或保留订阅 `RefundCompletedEvent` 仅由 Payment BC 发布。具体迁移由各 BC 修复计划负责。

**步骤 4：验证通过**

```bash
dotnet test tests/Services/ReviewAfterSales/Leno.ReviewAfterSales.UnitTests --filter "FullyQualifiedName~ReviewAfterSalesIntegrationEventMapperTests"
```

**步骤 5：提交**

```bash
git add -A && git commit -m "fix(reviewaftersales): 解除 RefundCompleted 事件回环（审计 2.11）

- 新增 AfterSalesRefundCompletedEvent 独立集成事件
- mapper 将 AfterSalesRefundCompletedDomainEvent 映射为独立事件而非 RefundCompletedEvent
- 避免 ReviewAfterSales BC 自身与其他 BC 重复消费 RefundCompletedEvent
- AfterSalesRefundCompletedDomainEvent 扩展 ChannelRefundNo 字段
- AfterSales.MarkRefundCompleted 在 AddDomainEvent 时传入 channelRefundNo"
```

---

## P1 修复清单（任务清单格式：审计位置/代码位置/根因/修复步骤/影响范围/验证方法）

### P1-3.1 AfterSales.Reject 误用 ApprovedAt 字段记录驳回时间

- **审计位置**：[file:///workspace/src/Services/ReviewAfterSales/Leno.ReviewAfterSales.Domain/Aggregates/AfterSales.cs#L241-L270]
- **代码位置**：`AfterSales.Reject` 第 268 行 `ApprovedAt = DateTime.UtcNow;`
- **根因**：驳回时复用 `ApprovedAt` 字段，导致审计无法区分审核同意时间与驳回时间
- **修复步骤**：
  1. 新增 `RejectedAt` 字段（DateTime?），并在 `Reject` 方法中赋值 `RejectedAt = DateTime.UtcNow;`
  2. 删除 `Reject` 方法中 `ApprovedAt = DateTime.UtcNow;`，确保 `ApprovedAt` 仅在 `Approve` 路径下填充
  3. `AfterSalesConfiguration` 增加 `rejected_at` 列映射
  4. `AfterSalesDto` 增加 `RejectedAt` 字段
  5. 新增 EF Core 迁移 `AddAfterSalesRejectedAt`
- **影响范围**：AfterSales 聚合、Configuration、DTO、数据库 schema；下游消费 `AfterSalesRejectedEvent` 的 Notification BC 无影响（事件字段不变）
- **验证方法**：单元测试验证 `Reject` 后 `ApprovedAt == null` 且 `RejectedAt.HasValue`；`Approve` 后 `ApprovedAt.HasValue` 且 `RejectedAt == null`

### P1-3.2 AfterSales.ConfirmReturn 未记录操作人，审计缺失

- **审计位置**：[file:///workspace/src/Services/ReviewAfterSales/Leno.ReviewAfterSales.Domain/Aggregates/AfterSales.cs#L311-L324]、[file:///workspace/src/Services/ReviewAfterSales/Leno.ReviewAfterSales.Application/Services/AfterSalesAppService.cs#L116-L141]
- **根因**：`ConfirmReturn()` 方法签名无 `operatorId` 参数，聚合无 `ReturnConfirmedBy` 字段
- **修复步骤**：
  1. AfterSales 聚合新增 `ReturnConfirmedBy`（Guid?）字段
  2. `ConfirmReturn` 方法签名改为 `ConfirmReturn(Guid operatorId)`，内部校验 `operatorId != Guid.Empty`，赋值 `ReturnConfirmedBy = operatorId;`
  3. `AfterSalesAppService.ConfirmReturnAsync` 调用 `afterSales.ConfirmReturn(operatorId);`（`RequireOwnedAfterSales` 已校验卖家归属，operatorId 即卖家）
  4. `AfterSalesConfiguration` 增加 `return_confirmed_by` 列映射
  5. `AfterSalesDto` 增加 `ReturnConfirmedBy` 字段
  6. 新增 EF Core 迁移
- **影响范围**：AfterSales 聚合、AppService、Configuration、DTO、数据库 schema
- **验证方法**：单元测试验证 `ConfirmReturn(sellerId)` 后 `ReturnConfirmedBy == sellerId`；`operatorId == Guid.Empty` 抛 `ReviewDomainException`

### P1-3.3 整单售后（orderLineId 为 null）不做重复申请校验

- **审计位置**：[file:///workspace/src/Services/ReviewAfterSales/Leno.ReviewAfterSales.Infrastructure/Services/AfterSalesEligibilityChecker.cs#L65-L72]、[file:///workspace/src/Services/ReviewAfterSales/Leno.ReviewAfterSales.Domain/Aggregates/AfterSales.cs#L21-L22]
- **根因**：`EnsureEligibleAsync` 仅在 `orderLineId.HasValue` 时调用 `HasActiveByOrderLineAsync`，整单售后跳过去重
- **修复步骤**：
  1. `IAfterSalesRepository` 新增 `HasActiveByOrderAsync(Guid orderId, AfterSalesType type, CancellationToken ct)` 方法
  2. `EfCoreAfterSalesRepository` 实现新方法，过滤条件 `a.OrderId == orderId && a.OrderLineId == null && a.Type == type && activeStatuses.Contains(a.Status)`
  3. `AfterSalesEligibilityChecker.EnsureEligibleAsync` 在 `orderLineId == null` 分支调用 `HasActiveByOrderAsync`，存在则抛 `AFTERSALES_DUPLICATE`
- **影响范围**：IAfterSalesRepository、EfCoreAfterSalesRepository、AfterSalesEligibilityChecker
- **验证方法**：单元测试模拟同订单两条整单售后申请，第二条抛 `AFTERSALES_DUPLICATE`

### P1-3.4 ReviewInternalQueryService.GetProductRatingAsync 加载全部 Approved 评价到内存计算聚合

- **审计位置**：[file:///workspace/src/Services/ReviewAfterSales/Leno.ReviewAfterSales.Application/InternalQueryServices/ReviewInternalQueryService.cs#L21-L41]、[file:///workspace/src/Services/ReviewAfterSales/Leno.ReviewAfterSales.Domain/Repositories/IReviewRepository.cs#L40-L46]、[file:///workspace/src/Services/ReviewAfterSales/Leno.ReviewAfterSales.Infrastructure/Repositories/EfCoreReviewRepository.cs#L64-L75]
- **根因**：`GetBySpuIdAsync` 加载全部评价到内存，再在内存 `Count`/`Average`/`Count(r => r.Rating >= 4)`
- **修复步骤**：
  1. `IReviewRepository` 新增 `GetRatingSnapshotAsync(Guid spuId, CancellationToken ct)` 方法，返回 `ProductRatingSnapshot?` 值对象
  2. `EfCoreReviewRepository` 实现使用 SQL 聚合：`_context.Reviews.AsNoTracking().Where(r => r.SpuId == spuId && r.Status == ReviewStatus.Approved && !r.IsDeleted).GroupBy(r => r.SpuId).Select(g => new ProductRatingSnapshot { TotalCount = g.Count(), AverageRating = g.Average(r => (double)r.Rating), PositiveCount = g.Count(r => r.Rating >= 4) }).FirstOrDefaultAsync(ct)`
  3. `ReviewInternalQueryService.GetProductRatingAsync` 改调 `GetRatingSnapshotAsync`，无快照返回 null
- **影响范围**：IReviewRepository、EfCoreReviewRepository、ReviewInternalQueryService
- **验证方法**：单元测试验证 `GetRatingSnapshotAsync` 生成的 SQL 不含 `ToList`；集成测试验证万条评价下查询时间 < 100ms

### P1-3.5 GrpcOrderStatusProvider 返回 OrderLineId=Guid.Empty 且 SkuId 可能丢失

- **审计位置**：[file:///workspace/src/Services/ReviewAfterSales/Leno.ReviewAfterSales.Infrastructure/Services/Grpc/GrpcOrderStatusProvider.cs#L60-L90]、[file:///workspace/src/Services/ReviewAfterSales/Leno.ReviewAfterSales.Infrastructure/Services/HttpOrderStatusProvider.cs#L68-L82]
- **根因**：proto OrderItem 无 `order_line_id` 字段，gRPC 路径静默填 `Guid.Empty`；关键字段解析失败静默 `Guid.Empty`
- **修复步骤**：
  1. 修改 `order.proto` OrderItem 消息增加 `string order_line_id = N;` 与 `string spu_id = M;` 字段
  2. `GrpcOrderStatusProvider.MapToInfo` 解析 `OrderLineId` 与 `SpuId`，解析失败抛 `AntiCorruptionException("订单域返回无效 OrderLineId/SpuId", "ORDER_REMOTE_FAILED")`
  3. `OrderId` / `UserId` 解析失败同样抛异常而非静默 `Guid.Empty`
  4. `HttpOrderStatusProvider.OrderItemStatusResponse` 增加 `SpuId` 字段（与 2.2 修复对齐）
- **影响范围**：order.proto、GrpcOrderStatusProvider、HttpOrderStatusProvider、订单域 gRPC 服务端（需配合填充新字段）
- **验证方法**：单元测试 mock proto 响应缺字段时抛 `AntiCorruptionException`；集成测试验证 gRPC 路径返回完整 OrderLineId/SpuId

### P1-3.6 ReviewReadModelSyncConsumer 未实现 EventId 幂等去重

- **审计位置**：[file:///workspace/src/Services/ReviewAfterSales/Leno.ReviewAfterSales.Infrastructure/ReadModels/ReviewReadModelSyncConsumer.cs#L14-L57]、[file:///workspace/src/BuildingBlocks/Leno.Infrastructure/EventBus/IntegrationEventConsumerBase.cs#L16-L74]
- **根因**：直接实现 `IConsumer<T>`，未继承 `IntegrationEventConsumerBase<T>`，未注入 `IIdempotencyStore`
- **修复步骤**：
  1. `ReviewReadModelSyncConsumer` 改为继承 `IntegrationEventConsumerBase<ReviewSubmittedEvent>` 并同时实现 `IConsumer<ReviewApprovedEvent>` / `IConsumer<ReviewHiddenEvent>`（基类仅支持单事件类型，需在子类中手动委托 `IsProcessedAsync`/`MarkAsProcessedAsync` 给 `IIdempotencyStore`）
  2. 或拆分为三个独立 Consumer 类，各自继承 `IntegrationEventConsumerBase<T>`
  3. 注入 `IIdempotencyStore`，在每个 `Consume` 方法入口检查 `EventId` 是否已处理
- **影响范围**：ReviewReadModelSyncConsumer、DI 注册（ServiceCollectionExtensions.AddReviewAfterSalesConsumers）
- **验证方法**：单元测试模拟同一 EventId 重复投递，验证 `BuildReadModelAsync` 仅调用一次

### P1-3.7 ApproveAfterSalesAsync 在数据库事务内执行远程支付查询，长事务持锁

- **审计位置**：[file:///workspace/src/Services/ReviewAfterSales/Leno.ReviewAfterSales.Application/Services/AfterSalesAppService.cs#L70-L102]、[file:///workspace/src/Services/ReviewAfterSales/Leno.ReviewAfterSales.Application/Services/AfterSalesAppService.cs#L116-L141]、[file:///workspace/src/Services/ReviewAfterSales/Leno.ReviewAfterSales.Application/Services/AfterSalesAppService.cs#L144-L169]
- **根因**：`Approve` 后立即在同一事务内调用 `_paymentInfoQueryService.GetByOrderIdAsync`（远程调用），整个远程调用期间 `after_sales` 行被锁
- **修复步骤**：
  1. 拆分事务：先 `Approve + MarkRefunding + SaveEntitiesAsync` 提交事务
  2. 然后在事务外查询 Payment 信息（独立调用）
  3. 用 `AddRefundRequestedEvent` 写入第二次事务（仅更新聚合的待发事件队列）
  4. 或在资格校验阶段预先缓存 PaymentId，Approve 时直接使用缓存值
- **影响范围**：AfterSalesAppService.ApproveAfterSalesAsync / ConfirmReturnAsync / AdminApproveAfterSalesAsync
- **验证方法**：集成测试验证 `Approve` 期间 `paymentInfoQueryService` 调用不阻塞其他事务对该售后单的查询

### P1-3.8 仓储层全部未使用 AsNoTracking，只读查询进入 Change Tracker

- **审计位置**：[file:///workspace/src/Services/ReviewAfterSales/Leno.ReviewAfterSales.Infrastructure/Repositories/EfCoreAfterSalesRepository.cs#L22-L127]、[file:///workspace/src/Services/ReviewAfterSales/Leno.ReviewAfterSales.Infrastructure/Repositories/EfCoreReviewRepository.cs#L22-L134]
- **根因**：所有 `GetBy*` / `QueryAsync` / `CountAsync` / `HasActive*` / `ExistsBy*` 直接 `_context.AfterSales.Where(...)` 未 `.AsNoTracking()`
- **修复步骤**：
  1. 只读查询路径全部加 `.AsNoTracking()`：`GetByIdAsync` / `GetByOrderIdAsync` / `QueryAsync` / `CountAsync` / `HasActiveByOrderLineAsync` / `ExistsByOrderLineAsync` / `GetBySpuIdAsync` / `GetByOrderLineAsync`
  2. `ReviewReadModelSyncConsumer.BuildReadModelAsync` 调用的 `GetByIdAsync` 也加 `.AsNoTracking()`
  3. 写路径（`AddAsync` / `UpdateAsync` / `RemoveAsync`）保持 tracked
- **影响范围**：EfCoreAfterSalesRepository、EfCoreReviewRepository、ReviewReadModelSyncConsumer
- **验证方法**：单元测试验证只读查询后 `_context.ChangeTracker.Entries().Count() == 0`

### P1-3.9 订单状态硬编码（OrderStatusShipped=2 / OrderStatusCompleted=3），跨 BC 契约脆弱

- **审计位置**：[file:///workspace/src/Services/ReviewAfterSales/Leno.ReviewAfterSales.Infrastructure/Services/AfterSalesEligibilityChecker.cs#L17-L18]、[file:///workspace/src/Services/ReviewAfterSales/Leno.ReviewAfterSales.Infrastructure/Services/ReviewEligibilityChecker.cs#L16]
- **根因**：用 `private const int OrderStatusShipped = 2;` 硬编码魔法数，订单域调整状态枚举后本域静默错配
- **修复步骤**：
  1. 在 `Leno.SharedContracts/Enums/` 新增 `OrderStatusEnum`（与订单域枚举值对齐）
  2. `AfterSalesEligibilityChecker` / `ReviewEligibilityChecker` 引用共享枚举替代魔法数
  3. `OrderStatusInfo.Status` 类型从 `int` 改为 `OrderStatusEnum`（或保留 `int` 但消费方显式转换）
- **影响范围**：Leno.SharedContracts、AfterSalesEligibilityChecker、ReviewEligibilityChecker、IOrderStatusProvider
- **验证方法**：单元测试验证枚举值与订单域一致；订单域调整状态码后本域编译期报错

### P1-3.10 上传图片流未 using，依赖框架兜底

- **审计位置**：[file:///workspace/src/Services/ReviewAfterSales/Leno.ReviewAfterSales.Api/Controllers/AfterSalesController.cs#L129]、[file:///workspace/src/Services/ReviewAfterSales/Leno.ReviewAfterSales.Api/Controllers/ReviewsController.cs#L116]
- **根因**：`file.OpenReadStream()` 返回的 Stream 未包裹 `using`
- **修复步骤**：
  1. `AfterSalesController.UploadAfterSalesImagesAsync` 第 129 行改为 `await using var stream = file.OpenReadStream();`，传入 `stream`
  2. `ReviewsController.UploadReviewImagesAsync` 第 116 行同样修改
- **影响范围**：AfterSalesController、ReviewsController
- **验证方法**：代码审查 grep `OpenReadStream` 确认全部被 `await using` 包裹

### P1-3.11 图片上传仅校验扩展名，未校验文件内容/Magic Number

- **审计位置**：[file:///workspace/src/Services/ReviewAfterSales/Leno.ReviewAfterSales.Api/Controllers/AfterSalesController.cs#L90-L134]、[file:///workspace/src/Services/ReviewAfterSales/Leno.ReviewAfterSales.Api/Controllers/ReviewsController.cs#L77-L121]
- **根因**：仅用 `Path.GetExtension` 校验扩展名白名单，未读取文件头部 Magic Number 验证实际是图片
- **修复步骤**：
  1. 在 `Leno.Infrastructure.Abstractions` 新增 `IFileSignatureDetector` 接口与实现（读取前 512 字节，匹配 JPEG/PNG/WebP magic number）
  2. `AfterSalesController` / `ReviewsController` 注入 `IFileSignatureDetector`，校验扩展名后追加 magic number 校验
  3. CDN 直链响应头强制设置 `Content-Disposition: attachment` 与 `X-Content-Type-Options: nosniff`（由 `IFileStorageService` 上传时设置）
- **影响范围**：Leno.Infrastructure.Abstractions、AfterSalesController、ReviewsController、IFileStorageService 实现
- **验证方法**：单元测试上传伪装成 .jpg 的 SVG/HTML，返回 400；上传真实 JPEG 通过

### P1-3.12 ReviewReadModelSyncConsumer 不处理评价被删除场景

- **审计位置**：[file:///workspace/src/Services/ReviewAfterSales/Leno.ReviewAfterSales.Infrastructure/ReadModels/ReviewReadModelSyncConsumer.cs#L14-L107]
- **根因**：消费者仅订阅 Submitted/Approved/Hidden 事件，Hidden 后 ES 文档仍保留可被搜索
- **修复步骤**：
  1. `ReviewReadModelSyncConsumer.Consume(ReviewHiddenEvent)` 中调用 `_repository.DeleteAsync(reviewId.ToString(), IndexName, ct)` 从 ES 删除文档
  2. 或更新 ES 文档 `Status` 字段为 "Hidden"，并要求 ES 查询默认过滤 `Status != "Hidden"`
  3. 若后续支持评价删除，新增 `ReviewDeletedEvent` 订阅，调用 `DeleteAsync`
- **影响范围**：ReviewReadModelSyncConsumer、ES 查询DSL（若采用过滤方案）
- **验证方法**：集成测试提交评价→审核通过→隐藏，验证 ES 索引中无该文档或 Status=Hidden

---

## P2 修复清单（任务清单格式，可简化）

### P2-4.1 OrderCompletedEventConsumer 仅打日志无副作用

- **审计位置**：[file:///workspace/src/Services/ReviewAfterSales/Leno.ReviewAfterSales.Infrastructure/Consumers/OrderCompletedEventConsumer.cs#L14-L33]
- **修复步骤**：删除该消费者（评价资格校验在提交时执行，消费者无业务价值），从 `ServiceCollectionExtensions.AddReviewAfterSalesConsumers` 移除注册
- **验证方法**：grep `OrderCompletedEventConsumer` 在源码零命中

### P2-4.2 MarkRefundFailed 与 Cancel 未校验 reason 是否为 null/空

- **审计位置**：[file:///workspace/src/Services/ReviewAfterSales/Leno.ReviewAfterSales.Domain/Aggregates/AfterSales.cs#L388-L399]、[file:///workspace/src/Services/ReviewAfterSales/Leno.ReviewAfterSales.Domain/Aggregates/AfterSales.cs#L445-L462]
- **修复步骤**：已与 P0-2.3 合并修复（reason 非空 + 长度校验）
- **状态**：[MERGED-INTO-P0-2.3]

### P2-4.3 AfterSales.Create 接收 images 列表共享引用给内部 _images

- **审计位置**：[file:///workspace/src/Services/ReviewAfterSales/Leno.ReviewAfterSales.Domain/Aggregates/AfterSales.cs#L169-L194]
- **修复步骤**：已与 P0-2.8 合并修复（防御性拷贝 `ToList()`）
- **状态**：[MERGED-INTO-P0-2.8]

### P2-4.4 AntiCorruptionOptions 解析在 UseGrpc=true 时硬抛异常，无降级

- **审计位置**：[file:///workspace/src/Services/ReviewAfterSales/Leno.ReviewAfterSales.Infrastructure/Dependencies/ServiceCollectionExtensions.cs#L66-L105]
- **修复步骤**：`paymentGrpcEndpoint` / `orderGrpcEndpoint` 缺失时记录 `LogWarning` 并降级到 HttpClient 模式（跳过 gRPC 注册，仅注册 HttpClient 实现）
- **验证方法**：单元测试模拟配置缺失，应用启动不抛异常，日志含 Warning

### P2-4.5 RefundFailedEventConsumer 失败原因未做长度校验

- **审计位置**：[file:///workspace/src/Services/ReviewAfterSales/Leno.ReviewAfterSales.Infrastructure/Consumers/RefundFailedEventConsumer.cs#L34-L74]、[file:///workspace/src/Services/ReviewAfterSales/Leno.ReviewAfterSales.Infrastructure/Configurations/AfterSalesConfiguration.cs#L38]
- **修复步骤**：已与 P0-2.3 合并修复（`MarkRefundFailed` 校验 `reason.Length <= 512`）
- **状态**：[MERGED-INTO-P0-2.3]

### P2-4.6 ApplyFilters 中 status.HasValue 与 status.Value 的冗余判断

- **审计位置**：[file:///workspace/src/Services/ReviewAfterSales/Leno.ReviewAfterSales.Infrastructure/Repositories/EfCoreAfterSalesRepository.cs#L99-L127]、[file:///workspace/src/Services/ReviewAfterSales/Leno.ReviewAfterSales.Infrastructure/Repositories/EfCoreReviewRepository.cs#L112-L134]
- **修复步骤**：简化为 `if (sellerId is not null) query = query.Where(a => a.SellerId == sellerId.Value);` 或直接传非可空 `Guid` 重载
- **验证方法**：代码审查确认无冗余 HasValue 判断

### P2-4.7 ReviewInternalQueryService.GetOrderReviewsAsync 返回 null 而非空集合

- **审计位置**：[file:///workspace/src/Services/ReviewAfterSales/Leno.ReviewAfterSales.Application/InternalQueryServices/ReviewInternalQueryService.cs#L44-L64]
- **修复步骤**：将 `if (reviews is null || reviews.Count == 0) return null;` 改为返回空 `OrderReviewsDto { Reviews = [] }`；`ReviewGrpcService.GetOrderReviews` 不再抛 NotFound，返回空列表
- **验证方法**：单元测试验证无评价订单返回空 Reviews 列表而非 NotFound

### P2-4.8 RefundCompletedEvent 契约中 AfterSalesId 默认 Guid.Empty 兼容旧版

- **审计位置**：[file:///workspace/src/BuildingBlocks/Leno.SharedContracts/Events/PaymentEvents.cs#L107-L163]
- **修复步骤**：
  1. Payment BC 必须填充 `AfterSalesId`（即使没有也用 `Guid.Empty` 但需记录告警）
  2. 或本 BC 用 `OrderId + RefundId` 反查关联售后单作为兜底
  3. 已与 P0-2.4 合并修复（契约增加 ChannelRefundNo，AfterSalesId 默认值通过构造重载显式表达）
- **状态**：[PARTIALLY-MERGED-INTO-P0-2.4]（Payment BC 强制填充由 Payment BC 修复计划负责）

---

## 已修复项

本 BC 暂无 [ALREADY-FIXED] 项。所有 31 项审计问题经代码校验确认仍存在，均纳入本修复计划。

**校验方法说明**：
- 已 Read 全部审计引用的源码文件（共 22 个），逐项核对问题特征代码仍存在
- 已 Grep 关键模式（`GetHashCode`、`dto.SellerId`、`dto.SpuId`、`ApprovedAt = DateTime.UtcNow` 等）确认匹配位置与审计位置一致
- 已 Read `Leno.SharedContracts/Events/PaymentEvents.cs` 确认 `RefundCompletedEvent` 仍无 `ChannelRefundNo` 字段
- 已 Read `ReviewAfterSalesIntegrationEventMapper.cs` 确认 `AfterSalesRefundCompletedDomainEvent` 仍映射为 `RefundCompletedEvent`（事件回环未解除）
- 已 Read `AfterSalesEnums.cs` 确认 `ReturnGoods=7` / `ConfirmReturn=8` 状态存在但 `HasActiveByOrderLineAsync` 未纳入

---

## 附录：修复优先级与依赖关系

### P0 修复顺序（按依赖关系）

```
2.1（SellerId 校验）──┐
                       ├──> 2.7（SellerReply 校验，依赖 Review.SellerId 字段）
2.2（SpuId/SkuId 校验）┘
2.3（领域事件 + 4.2/4.5 合并）── 独立
2.4（ChannelRefundNo + 4.8 合并）── 独立
2.5（gRPC GetHashCode）── 独立
2.6（归属校验，部分依赖 2.3 的 Cancel 修复）── 依赖 2.3
2.8（Images 封装 + 4.3 合并）── 独立
2.9（活跃状态过滤）── 独立
2.10（查询归属校验，依赖 2.1 的 OrderStatusProvider 注入）── 依赖 2.1
2.11（事件回环，依赖 2.4 的契约变更）── 依赖 2.4
```

### 共享契约层变更清单（需跨 BC 协调）

1. `Leno.SharedContracts/Events/PaymentEvents.cs`：`RefundCompletedEvent` 增加 `ChannelRefundNo`（P0-2.4）
2. `Leno.SharedContracts/Events/AfterSalesEvents.cs`：新增 `AfterSalesRefundFailedEvent` / `AfterSalesCancelledEvent` / `AfterSalesRefundCompletedEvent`（P0-2.3 / P0-2.11）
3. `Leno.SharedContracts/Enums/OrderStatusEnum.cs`：新增共享枚举（P1-3.9）
4. `Leno.SharedContracts/Grpc/Order/V1/order.proto`：OrderItem 增加 `order_line_id` / `spu_id` 字段（P1-3.5）

### 数据库迁移清单

1. `AddReviewSellerFields`：Review 表增加 `seller_id` / `seller_reply_by` / `seller_reply_at`（P0-2.7）
2. `AddAfterSalesRejectedAt`：AfterSales 表增加 `rejected_at`（P1-3.1）
3. `AddAfterSalesReturnConfirmedBy`：AfterSales 表增加 `return_confirmed_by`（P1-3.2）

### 跨 BC 协调清单

1. Payment BC：发布 `RefundCompletedEvent` 时填充 `ChannelRefundNo`（P0-2.4 依赖）
2. Order BC：gRPC `OrderStatus` 响应增加 `SellerId` / OrderItem 增加 `SpuId`（P0-2.1 / P0-2.2 依赖）
3. Order/Promotion/Notification BC：消费方迁移到订阅 `AfterSalesRefundCompletedEvent`（P0-2.11 依赖）
4. SellerShop BC：消费 `ReviewSubmittedEvent` 时使用 `ShopId` 字段（与 SellerShop BC 修复计划协调）
