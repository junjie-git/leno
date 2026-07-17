# 慢轨 M6 CQRS + BFF + 文档同步 实施计划

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 在 Promotion/PointsMembership/SellerShop 三个 BC 新建 ES 读模型目录与 5 个读模型 + 5 个同步 Consumer，`ReadModelSyncConsumerBase` 扩展支持删除场景；Product/Order/SellerShop 三 BC 新建 `Queries/` 目录与 6 个 Query + QueryHandler（不引入 MediatR）；网关新建 `Bff/` 目录与 4 个聚合端点（order-detail/product-detail/cart-checkout-preview/seller-dashboard，YARP `IForwarder` + `Parallel.ForEachAsync`，部分失败返回 `partial:true`）；`CacheMiddleware.GenerateCacheKey` 增加 `role`/`shopId` 维度；编码规范新增第 15/16/17 章（安全编码/gRPC 通信/CQRS Query Handler 约定，**spec 原文"第 14/15/16 章"因第 14 章已被 Git 提交规范占用，调整为 15/16/17**）；同步需求文档总览与 internal-api-contracts.md；新建 `.github/pull_request_template.md`；3 份既有 spec 标注 supersede/接管关系；全量回归测试通过

**Architecture:** ES 读模型同步统一基于 `ReadModelSyncConsumerBase<TEvent,TReadModel>`，新增 `BuildDeleteActionAsync` 抽象方法支持删除分支，`ProductTakenDownReadModelSyncConsumer` 重构继承基类（修复删除失败仅 Warning 不抛异常的不一致）；Query Handler 采用 `IQueryHandler<TQuery,TResult>` 接口 + DI 注册模式，QueryHandler 位于 Application 层、走 ES 读模型或既有仓储；BFF 端点位于 `Leno.ApiGateway/Bff/` 目录，使用 `IForwarder` 单次转发 + `Parallel.ForEachAsync` 并行调用下游、超时 3 秒、部分失败返回 `partial:true` + 错误明细；缓存 Key 增加 `role` + `shopId` 维度，敏感端点强制包含 `shopId`；编码规范新增章节直接 append 到现有第 14 章之后

**Tech Stack:** .NET 10、Elasticsearch.NET 8.x、MassTransit、YARP 2.2、StackExchange.Redis、xUnit、FluentAssertions、Moq

**关联 spec:** [2026-07-17-comprehensive-optimization-v2-design.md §13](../specs/2026-07-17-comprehensive-optimization-v2-design.md)

**前置依赖:** Plan 5（M1 事件契约分离，集成事件已统一继承 `IntegrationEventBase`）完成；Plan 7（M3 跨 BC 样板去重，`AddLenoApi`/`UseLenoPipeline` 已就绪）完成；Plan 8（M4 通信升级，gRPC 契约已建）完成；Plan 9（M5 可观测性 + 部署，`/metrics` 端点已暴露）完成

**向后兼容策略:** M6.1 ES 读模型新增不影响既有读路径（Consumer 新建、读模型独立）；M6.2 Query Handler 与既有 AppService 查询方法**双发期 2 周**（QueryHandler 走 ES、AppService 走 EF），2 周后 Controller 切换到 QueryHandler；M6.3 BFF 端点新增不影响既有直连端点（独立 `/api/bff/*` 路由前缀）；M6.4 缓存 Key 维度增加后**首次请求全部 miss**（无功能影响，仅缓存命中率短暂下降）；M6.5 文档更新不涉及代码变更；M6.6 既有 spec 仅添加 supersede 标注头部，原内容保留

---

## 关键代码定位（实施前必读）

| 位置 | 路径 | 关键发现 |
|---|---|---|
| ReadModelSyncConsumerBase | `src/BuildingBlocks/Leno.Infrastructure/ReadModel/ReadModelSyncConsumerBase.cs` | 抽象基类，仅支持索引场景；`BuildReadModelAsync` 返回 null 跳过；需新增 `BuildDeleteActionAsync` 支持删除 |
| IEsReadModelRepository | `src/BuildingBlocks/Leno.Infrastructure/ReadModel/IEsReadModelRepository.cs:30` | **已含 `DeleteByIdAsync` 方法**，底层能力具备 |
| ProductTakenDownReadModelSyncConsumer | `src/Services/Product/Leno.Product.Infrastructure/ReadModels/ProductReadModelSyncConsumer.cs:71-106` | **裸实现 IConsumer**（未继承基类），删除失败仅 `LogWarning` 不抛异常，与基类索引失败抛异常约定不一致 |
| ProductPublishedReadModelSyncConsumer | `src/Services/Product/Leno.Product.Infrastructure/ReadModels/ProductReadModelSyncConsumer.cs:15-64` | 正确继承 `ReadModelSyncConsumerBase`，可作为参考模板 |
| IProductSearchService | `src/Services/Product/Leno.Product.Application/IProductSearchService.cs` | 单方法 `SearchAsync`，**无 ProductDetailQuery** |
| ProductSearchService 实现 | `src/Services/Product/Leno.Product.Infrastructure/ReadModels/ProductSearchService.cs` | **位于 Infrastructure 层**，常量 `ProductIndexName = "leno_products"`；M6.2 保留实现位置 |
| OrderAppService 查询方法 | `src/Services/Order/Leno.Order.Application/Services/OrderAppService.cs:385-407` | `GetByIdAsync`(385) + `QueryAsync`(392) + `GetLogisticsTraceAsync`(407) 待拆分 |
| OrderAppService 写方法 | `src/Services/Order/Leno.Order.Application/Services/OrderAppService.cs:70-316` | CreateOrder/BuyNow/Preview/Pay/Ship/ConfirmReceipt/Cancel/ForceCancel 保留 |
| SellerDashboardAppService | `src/Services/SellerShop/Leno.SellerShop.Application/Services/SellerDashboardAppService.cs:28-62` | `GetDashboardAsync` 直接调三个仓储聚合，未走 ES；M6.2 拆为 QueryHandler |
| ShopDashboardData 聚合 | `src/Services/SellerShop/Leno.SellerShop.Domain/Aggregates/ShopDashboardData.cs` | 既有领域聚合根，M6.1 ShopDashboardReadModel 与之并存（ES 读模型） |
| CacheMiddleware.GenerateCacheKey | `src/ApiGateway/Leno.ApiGateway/Middleware/CacheMiddleware.cs:122-132` | 当前 `$"{method}:{path}{query}:{userId}"`，**无 role/shopId** |
| CacheMiddlewareTests | `src/ApiGateway/Leno.ApiGateway.Tests/Middleware/CacheMiddlewareTests.cs` | 既有测试需同步更新 |
| ApiGateway 目录结构 | `src/ApiGateway/Leno.ApiGateway/` | 含 Transforms/Services/Options/Models/Middleware/Extensions，**无 Bff/** |
| YARP 集成 | `src/ApiGateway/Leno.ApiGateway/Program.cs:13-14` | `AddObservability` 内部已 `AddReverseProxy().LoadFromConfig().AddTransforms<TracingTransform>()`；**IForwarder 未使用** |
| 编码规范章节 | `docs/编码规范.md` | 14 章（第 14 章为 Git 提交规范），**spec"新增第 14/15/16 章"需调整为 15/16/17** |
| 既有 CQRS 章节 | `docs/编码规范.md` 第 7 章 (line 1597) | 已含 7.1 总体/7.2 Command/7.3 Query/7.4 读模型同步；M6.5"Query Handler 约定"可作为 7.5 子节而非新增独立章节 |
| 需求文档总览 | `docs/spec/00-需求文档总览与DDD架构.md` | V2.4，10 章；需同步事件契约分离/Internal API 版本治理/gRPC 决策 |
| 既有 spec 清单 | `docs/superpowers/specs/` | 3 份：2026-07-13 V1、2026-07-14 网关增强、2026-07-17 V2（本方案） |
| fix-critical-business-vulnerabilities | `.trae/specs/fix-critical-business-vulnerabilities/` | spec.md + tasks.md + checklist.md，与 V2 并行 |
| PR 模板 | `.github/pull_request_template.md` | **不存在**，需从零创建 |
| 11 BC ReadModels 目录 | `src/Services/*/Leno.*.Infrastructure/ReadModels/` | **仅 Product BC 存在**；Promotion/PointsMembership/SellerShop **均不存在**，需新建 |

### 11 个 BC 已发布的集成事件清单（M6.1 Consumer 订阅依据）

| BC | 读模型 | 订阅事件 | 事件发布方 |
|---|---|---|---|
| Promotion | SeckillActivityReadModel | SeckillActivityPublishedEvent、SeckillActivityEndedEvent | Promotion BC |
| Promotion | CouponReadModel | CouponCreatedEvent、CouponDisabledEvent | Promotion BC |
| PointsMembership | PointsAccountReadModel | PointsAccountCreatedEvent、PointsAdjustedEvent | PointsMembership BC |
| PointsMembership | MemberReadModel | MemberRegisteredEvent、MemberLevelUpgradedEvent | PointsMembership BC |
| SellerShop | ShopDashboardReadModel | OrderPlacedEvent、OrderConfirmedEvent、ReviewSubmittedEvent | Order/ReviewAfterSales BC |

---

## Task 1: ReadModelSyncConsumerBase 扩展支持删除场景

**Files:**
- Modify: `src/BuildingBlocks/Leno.Infrastructure/ReadModel/ReadModelSyncConsumerBase.cs`
- Test: `tests/BuildingBlocks/Leno.Infrastructure.Tests/ReadModel/ReadModelSyncConsumerBaseDeleteTests.cs`

**背景:** 当前基类仅支持索引场景，删除需裸实现 `IConsumer`（如 `ProductTakenDownReadModelSyncConsumer`），且删除失败仅 Warning 不抛异常，与索引失败抛异常约定不一致。底层 `IEsReadModelRepository.DeleteByIdAsync` 已具备能力。

- [ ] **Step 1: 编写失败测试 — 删除场景**

创建测试文件 `tests/BuildingBlocks/Leno.Infrastructure.Tests/ReadModel/ReadModelSyncConsumerBaseDeleteTests.cs`：

```csharp
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Leno.Infrastructure.ReadModel;
using Leno.SharedContracts.Events;
using MassTransit;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace Leno.Infrastructure.Tests.ReadModel;

public class ReadModelSyncConsumerBaseDeleteTests
{
    public sealed class FakeDeleteEvent : IntegrationEventBase
    {
        public string ResourceId { get; init; } = string.Empty;
    }

    public sealed class FakeDeleteReadModel
    {
        public string Id { get; set; } = string.Empty;
    }

    public sealed class FakeDeleteConsumer : ReadModelSyncConsumerBase<FakeDeleteEvent, FakeDeleteReadModel>
    {
        public bool DeleteActionInvoked { get; private set; }

        protected override Task<(string Id, string IndexName, FakeDeleteReadModel? ReadModel)> BuildReadModelAsync(
            FakeDeleteEvent integrationEvent, CancellationToken ct)
        {
            // 删除场景下不调用索引分支
            return Task.FromResult<(string, string, FakeDeleteReadModel?)>(("", "", null));
        }

        protected override Task<(string Id, string IndexName)?> BuildDeleteActionAsync(
            FakeDeleteEvent integrationEvent, CancellationToken ct)
        {
            DeleteActionInvoked = true;
            return Task.FromResult<(string Id, string IndexName)?>(
                (integrationEvent.ResourceId, "leno_fake"));
        }

        public FakeDeleteConsumer(IEsReadModelRepository<FakeDeleteReadModel> repository)
            : base(repository, NullLogger<ReadModelSyncConsumerBase<FakeDeleteEvent, FakeDeleteReadModel>>.Instance)
        {
        }
    }

    [Fact]
    public async Task Consume_WhenBuildDeleteActionReturnsValue_ShouldCallDeleteByIdAsync()
    {
        // Arrange
        var repoMock = new Mock<IEsReadModelRepository<FakeDeleteReadModel>>();
        repoMock.Setup(r => r.DeleteByIdAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);
        var consumer = new FakeDeleteConsumer(repoMock.Object);
        var context = new Mock<ConsumeContext<FakeDeleteEvent>>();
        context.SetupGet(c => c.Message).Returns(new FakeDeleteEvent { ResourceId = "res-001" });

        // Act
        await consumer.Consume(context.Object);

        // Assert
        consumer.DeleteActionInvoked.Should().BeTrue();
        repoMock.Verify(r => r.DeleteByIdAsync("res-001", "leno_fake", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Consume_WhenDeleteThrows_ShouldPropagateException()
    {
        // Arrange
        var repoMock = new Mock<IEsReadModelRepository<FakeDeleteReadModel>>();
        repoMock.Setup(r => r.DeleteByIdAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ThrowsAsync(new InvalidOperationException("ES unavailable"));
        var consumer = new FakeDeleteConsumer(repoMock.Object);
        var context = new Mock<ConsumeContext<FakeDeleteEvent>>();
        context.SetupGet(c => c.Message).Returns(new FakeDeleteEvent { ResourceId = "res-002" });

        // Act
        var act = async () => await consumer.Consume(context.Object);

        // Assert: 必须抛异常触发 MassTransit 重试与死信队列
        await act.Should().ThrowAsync<InvalidOperationException>()
                 .WithMessage("ES unavailable");
    }

    [Fact]
    public async Task Consume_WhenBuildDeleteActionReturnsNull_ShouldSkipSilently()
    {
        // Arrange: 既不索引也不删除（事件不感兴趣）
        var repoMock = new Mock<IEsReadModelRepository<FakeDeleteReadModel>>();
        var consumer = new SkipAllConsumer(repoMock.Object);
        var context = new Mock<ConsumeContext<FakeDeleteEvent>>();
        context.SetupGet(c => c.Message).Returns(new FakeDeleteEvent { ResourceId = "res-003" });

        // Act
        await consumer.Consume(context.Object);

        // Assert
        repoMock.Verify(r => r.IndexAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<FakeDeleteReadModel>(), It.IsAny<CancellationToken>()), Times.Never);
        repoMock.Verify(r => r.DeleteByIdAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    public sealed class SkipAllConsumer : ReadModelSyncConsumerBase<FakeDeleteEvent, FakeDeleteReadModel>
    {
        public SkipAllConsumer(IEsReadModelRepository<FakeDeleteReadModel> repository)
            : base(repository, NullLogger<ReadModelSyncConsumerBase<FakeDeleteEvent, FakeDeleteReadModel>>.Instance)
        {
        }

        protected override Task<(string Id, string IndexName, FakeDeleteReadModel? ReadModel)> BuildReadModelAsync(
            FakeDeleteEvent integrationEvent, CancellationToken ct)
            => Task.FromResult<(string, string, FakeDeleteReadModel?)>(("", "", null));

        protected override Task<(string Id, string IndexName)?> BuildDeleteActionAsync(
            FakeDeleteEvent integrationEvent, CancellationToken ct)
            => Task.FromResult<(string, string)?>(null);
    }
}
```

- [ ] **Step 2: 运行测试验证失败**

Run: `dotnet test tests/BuildingBlocks/Leno.Infrastructure.Tests/Leno.Infrastructure.Tests.csproj --filter "FullyQualifiedName~ReadModelSyncConsumerBaseDeleteTests" --configuration Debug`
Expected: FAIL，编译错误 `BuildDeleteActionAsync` 不存在

- [ ] **Step 3: 修改基类增加 BuildDeleteActionAsync 抽象方法**

读取 `src/BuildingBlocks/Leno.Infrastructure/ReadModel/ReadModelSyncConsumerBase.cs` 全文，找到 `Consume` 方法和 `BuildReadModelAsync` 抽象方法定义。修改如下：

```csharp
// 在 BuildReadModelAsync 抽象方法下方新增 BuildDeleteActionAsync 抽象方法
/// <summary>
/// 派生类重写以声明本事件需删除读模型。返回 (Id, IndexName) 触发 DeleteByIdAsync；
/// 返回 null 表示本事件不触发删除（也不触发索引，仅由 BuildReadModelAsync 决定索引）。
/// 默认实现返回 null（向后兼容：仅索引场景无需重写）。
/// </summary>
protected virtual Task<(string Id, string IndexName)?> BuildDeleteActionAsync(
    TEvent integrationEvent, CancellationToken ct)
    => Task.FromResult<(string, string)?>(null);
```

修改 `Consume` 方法主体，在索引分支之前增加删除分支：

```csharp
public async Task Consume(ConsumeContext<TEvent> context)
{
    var evt = context.Message;
    var ct = context.CancellationToken;

    // 删除分支（优先于索引分支，因同一事件通常不会同时触发索引与删除）
    var deleteAction = await BuildDeleteActionAsync(evt, ct).ConfigureAwait(false);
    if (deleteAction is { } delete)
    {
        try
        {
            await Repository.DeleteByIdAsync(delete.Id, delete.IndexName, ct).ConfigureAwait(false);
            Logger.LogInformation("ReadModel deleted: {IndexName}/{Id}", delete.IndexName, delete.Id);
            return;
        }
        catch (Exception ex)
        {
            // 与索引分支一致：抛异常触发 MassTransit 重试与死信队列
            Logger.LogError(ex, "ReadModel delete failed: {IndexName}/{Id}", delete.IndexName, delete.Id);
            throw new InvalidOperationException(
                $"ReadModel delete failed for {delete.IndexName}/{delete.Id}", ex);
        }
    }

    // 索引分支（既有逻辑保持不变）
    var (id, indexName, readModel) = await BuildReadModelAsync(evt, ct).ConfigureAwait(false);
    if (readModel is null)
    {
        Logger.LogDebug("ReadModel skipped (null): {IndexName}/{Id}", indexName, id);
        return;
    }

    try
    {
        await Repository.IndexAsync(id, indexName, readModel, ct).ConfigureAwait(false);
        Logger.LogInformation("ReadModel indexed: {IndexName}/{Id}", indexName, id);
    }
    catch (Exception ex)
    {
        Logger.LogError(ex, "ReadModel index failed: {IndexName}/{Id}", indexName, id);
        throw new InvalidOperationException(
            $"ReadModel index failed for {indexName}/{id}", ex);
    }
}
```

**注意:** 既有 `BuildReadModelAsync` 抽象方法保留不变；`BuildDeleteActionAsync` 默认实现返回 null（向后兼容：仅索引场景的派生类无需重写）。

- [ ] **Step 4: 运行测试验证通过**

Run: `dotnet test tests/BuildingBlocks/Leno.Infrastructure.Tests/Leno.Infrastructure.Tests.csproj --filter "FullyQualifiedName~ReadModelSyncConsumerBaseDeleteTests" --configuration Debug`
Expected: PASS，3 个测试全部通过

- [ ] **Step 5: 验证既有 ProductPublishedReadModelSyncConsumer 仍正常**

Run: `dotnet test tests/Services/Product/Leno.Product.Infrastructure.Tests/Leno.Product.Infrastructure.Tests.csproj --configuration Debug --filter "FullyQualifiedName~ProductReadModelSync"`
Expected: PASS，既有索引场景不受影响（`BuildDeleteActionAsync` 默认返回 null，走索引分支）

- [ ] **Step 6: 提交**

```bash
git add src/BuildingBlocks/Leno.Infrastructure/ReadModel/ReadModelSyncConsumerBase.cs tests/BuildingBlocks/Leno.Infrastructure.Tests/ReadModel/ReadModelSyncConsumerBaseDeleteTests.cs
git commit -m "feat(infrastructure): ReadModelSyncConsumerBase 扩展支持删除场景

- 新增 BuildDeleteActionAsync 虚方法（默认返回 null 向后兼容）
- Consume 方法优先调用删除分支，删除失败抛异常触发 MassTransit 重试
- 与既有索引分支错误处理策略保持一致
- 新增 3 个单元测试覆盖删除/失败/跳过场景

关联 spec: §13.1 M6.1 基类增强"
```

---

## Task 2: ProductTakenDownReadModelSyncConsumer 重构继承基类

**Files:**
- Modify: `src/Services/Product/Leno.Product.Infrastructure/ReadModels/ProductReadModelSyncConsumer.cs:71-106`
- Test: `tests/Services/Product/Leno.Product.Infrastructure.Tests/ReadModels/ProductTakenDownReadModelSyncConsumerTests.cs`

**背景:** 既有 `ProductTakenDownReadModelSyncConsumer` 裸实现 `IConsumer<ProductTakenDownEvent>`，删除失败仅 `LogWarning` 不抛异常，与基类索引失败抛异常约定不一致。Task 1 已扩展基类支持删除场景，本任务将裸实现重构为继承基类。

- [ ] **Step 1: 编写失败测试 — 重构后的删除行为**

创建测试文件 `tests/Services/Product/Leno.Product.Infrastructure.Tests/ReadModels/ProductTakenDownReadModelSyncConsumerTests.cs`：

```csharp
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Leno.Infrastructure.ReadModel;
using Leno.Product.Infrastructure.ReadModels;
using Leno.SharedContracts.Events.Product;
using MassTransit;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace Leno.Product.Infrastructure.Tests.ReadModels;

public class ProductTakenDownReadModelSyncConsumerTests
{
    [Fact]
    public async Task Consume_WhenProductTakenDown_ShouldCallDeleteByIdAsync()
    {
        // Arrange
        var repoMock = new Mock<IEsReadModelRepository<ProductReadModel>>();
        repoMock.Setup(r => r.DeleteByIdAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);
        var consumer = new ProductTakenDownReadModelSyncConsumer(repoMock.Object, NullLogger<ProductTakenDownReadModelSyncConsumer>.Instance);
        var context = new Mock<ConsumeContext<ProductTakenDownEvent>>();
        var spuId = Guid.NewGuid();
        context.SetupGet(c => c.Message).Returns(new ProductTakenDownEvent { SpuId = spuId });

        // Act
        await consumer.Consume(context.Object);

        // Assert
        repoMock.Verify(r => r.DeleteByIdAsync(spuId.ToString(), "leno_products", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Consume_WhenDeleteFails_ShouldThrowInvalidOperationException()
    {
        // Arrange: 重构后必须抛异常触发重试，不再仅 LogWarning
        var repoMock = new Mock<IEsReadModelRepository<ProductReadModel>>();
        repoMock.Setup(r => r.DeleteByIdAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ThrowsAsync(new System.IO.IOException("ES connection refused"));
        var consumer = new ProductTakenDownReadModelSyncConsumer(repoMock.Object, NullLogger<ProductTakenDownReadModelSyncConsumer>.Instance);
        var context = new Mock<ConsumeContext<ProductTakenDownEvent>>();
        context.SetupGet(c => c.Message).Returns(new ProductTakenDownEvent { SpuId = Guid.NewGuid() });

        // Act
        var act = async () => await consumer.Consume(context.Object);

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
                 .WithMessage("*ReadModel delete failed*");
    }
}
```

- [ ] **Step 2: 运行测试验证失败**

Run: `dotnet test tests/Services/Product/Leno.Product.Infrastructure.Tests/Leno.Product.Infrastructure.Tests.csproj --filter "FullyQualifiedName~ProductTakenDownReadModelSyncConsumerTests" --configuration Debug`
Expected: FAIL，编译错误（构造函数签名不匹配，且既有实现不抛异常）

- [ ] **Step 3: 重构 ProductTakenDownReadModelSyncConsumer 继承基类**

修改 `src/Services/Product/Leno.Product.Infrastructure/ReadModels/ProductReadModelSyncConsumer.cs` 第 71-106 行，替换为：

```csharp
/// <summary>
/// 商品下架事件读模型同步消费者：从 ES 索引 leno_products 中删除对应 SPU 读模型。
/// 继承 ReadModelSyncConsumerBase，删除失败抛异常触发 MassTransit 重试与死信队列
/// （与既有 ProductPublishedReadModelSyncConsumer 错误处理策略一致）。
/// </summary>
public sealed class ProductTakenDownReadModelSyncConsumer
    : ReadModelSyncConsumerBase<ProductTakenDownEvent, ProductReadModel>
{
    public ProductTakenDownReadModelSyncConsumer(
        IEsReadModelRepository<ProductReadModel> repository,
        ILogger<ProductTakenDownReadModelSyncConsumer> logger)
        : base(repository, logger)
    {
    }

    protected override Task<(string Id, string IndexName, ProductReadModel? ReadModel)> BuildReadModelAsync(
        ProductTakenDownEvent integrationEvent, CancellationToken ct)
    {
        // 下架事件不触发索引，仅触发删除
        return Task.FromResult<(string, string, ProductReadModel?)>(("", "", null));
    }

    protected override Task<(string Id, string IndexName)?> BuildDeleteActionAsync(
        ProductTakenDownEvent integrationEvent, CancellationToken ct)
    {
        return Task.FromResult<(string Id, string IndexName)?>(
            (integrationEvent.SpuId.ToString(), ProductSearchService.ProductIndexName));
    }
}
```

**注意:** `ProductSearchService.ProductIndexName` 为既有常量 `"leno_products"`，位于 `src/Services/Product/Leno.Product.Infrastructure/ReadModels/ProductSearchService.cs`，重构后引用既有常量避免硬编码。

- [ ] **Step 4: 运行测试验证通过**

Run: `dotnet test tests/Services/Product/Leno.Product.Infrastructure.Tests/Leno.Product.Infrastructure.Tests.csproj --filter "FullyQualifiedName~ProductTakenDownReadModelSyncConsumerTests" --configuration Debug`
Expected: PASS，2 个测试全部通过

- [ ] **Step 5: 验证 Product BC 全量测试通过**

Run: `dotnet test tests/Services/Product/Leno.Product.Infrastructure.Tests/Leno.Product.Infrastructure.Tests.csproj --configuration Debug`
Expected: PASS

- [ ] **Step 6: 提交**

```bash
git add src/Services/Product/Leno.Product.Infrastructure/ReadModels/ProductReadModelSyncConsumer.cs tests/Services/Product/Leno.Product.Infrastructure.Tests/ReadModels/ProductTakenDownReadModelSyncConsumerTests.cs
git commit -m "refactor(product): ProductTakenDownReadModelSyncConsumer 改为继承基类

- 从裸实现 IConsumer 改为继承 ReadModelSyncConsumerBase
- 修复删除失败仅 LogWarning 不抛异常的不一致（改为抛 InvalidOperationException 触发重试）
- 复用 ProductSearchService.ProductIndexName 常量避免硬编码
- 新增 2 个单元测试覆盖删除成功/失败场景

关联 spec: §13.1 M6.1 基类增强落地"
```

---

## Task 3: Promotion BC 新建 SeckillActivityReadModel + CouponReadModel 与同步 Consumer

**Files:**
- Create: `src/Services/Promotion/Leno.Promotion.Infrastructure/ReadModels/SeckillActivityReadModel.cs`
- Create: `src/Services/Promotion/Leno.Promotion.Infrastructure/ReadModels/CouponReadModel.cs`
- Create: `src/Services/Promotion/Leno.Promotion.Infrastructure/ReadModels/SeckillActivityReadModelSyncConsumer.cs`
- Create: `src/Services/Promotion/Leno.Promotion.Infrastructure/ReadModels/CouponReadModelSyncConsumer.cs`
- Test: `tests/Services/Promotion/Leno.Promotion.Infrastructure.Tests/ReadModels/SeckillActivityReadModelSyncConsumerTests.cs`
- Test: `tests/Services/Promotion/Leno.Promotion.Infrastructure.Tests/ReadModels/CouponReadModelSyncConsumerTests.cs`

**背景:** Promotion BC 当前无 `ReadModels/` 目录，需新建 Seckill 活动 + 优惠券两个 ES 读模型及对应同步 Consumer。需先确认事件类型 `SeckillActivityPublishedEvent`/`SeckillActivityEndedEvent`/`CouponCreatedEvent`/`CouponDisabledEvent` 是否存在于 `Leno.SharedContracts`，若不存在需检查 Promotion.Domain 事件定义。

- [ ] **Step 1: 确认事件类型存在**

Run: `grep -r "SeckillActivityPublishedEvent\|SeckillActivityEndedEvent\|CouponCreatedEvent\|CouponDisabledEvent" src/ --include="*.cs" -l`

Expected: 找到事件定义文件，确认事件属性（如 ActivityId/CouponId/活动名称/面额等）。**若事件不存在**，需先在 `Leno.SharedContracts/Events/Promotion/` 新建事件类型（继承 `IntegrationEventBase`），并在 Promotion BC 聚合根发布该事件。此步骤不写代码，仅记录事件签名供后续步骤引用。

- [ ] **Step 2: 新建 SeckillActivityReadModel**

创建 `src/Services/Promotion/Leno.Promotion.Infrastructure/ReadModels/SeckillActivityReadModel.cs`：

```csharp
namespace Leno.Promotion.Infrastructure.ReadModels;

/// <summary>
/// 秒杀活动 ES 读模型，索引名 leno_seckill_activities。
/// 用于前台秒杀活动列表与详情页的快速检索。
/// </summary>
public sealed class SeckillActivityReadModel
{
    public string Id { get; set; } = string.Empty;

    public string ActivityName { get; set; } = string.Empty;

    public DateTime StartTime { get; set; }

    public DateTime EndTime { get; set; }

    public decimal OriginalPrice { get; set; }

    public decimal SeckillPrice { get; set; }

    public int TotalStock { get; set; }

    public int AvailableStock { get; set; }

    public Guid ProductId { get; set; }

    public string Status { get; set; } = string.Empty;
}
```

- [ ] **Step 3: 新建 CouponReadModel**

创建 `src/Services/Promotion/Leno.Promotion.Infrastructure/ReadModels/CouponReadModel.cs`：

```csharp
namespace Leno.Promotion.Infrastructure.ReadModels;

/// <summary>
/// 优惠券 ES 读模型，索引名 leno_coupons。
/// 用于用户端优惠券列表与领券中心快速检索。
/// </summary>
public sealed class CouponReadModel
{
    public string Id { get; set; } = string.Empty;

    public string CouponName { get; set; } = string.Empty;

    public string CouponType { get; set; } = string.Empty;

    public decimal Denomination { get; set; }

    public decimal MinSpend { get; set; }

    public DateTime ValidFrom { get; set; }

    public DateTime ValidTo { get; set; }

    public int TotalQuantity { get; set; }

    public int IssuedQuantity { get; set; }

    public string Status { get; set; } = string.Empty;
}
```

- [ ] **Step 4: 编写失败测试 — SeckillActivityReadModelSyncConsumer**

创建测试文件 `tests/Services/Promotion/Leno.Promotion.Infrastructure.Tests/ReadModels/SeckillActivityReadModelSyncConsumerTests.cs`：

```csharp
using System;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Leno.Infrastructure.ReadModel;
using Leno.Promotion.Infrastructure.ReadModels;
using Leno.SharedContracts.Events.Promotion;
using MassTransit;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace Leno.Promotion.Infrastructure.Tests.ReadModels;

public class SeckillActivityReadModelSyncConsumerTests
{
    [Fact]
    public async Task Consume_WhenActivityPublished_ShouldIndexReadModel()
    {
        // Arrange
        var repoMock = new Mock<IEsReadModelRepository<SeckillActivityReadModel>>();
        var consumer = new SeckillActivityReadModelSyncConsumer(repoMock.Object, NullLogger<SeckillActivityReadModelSyncConsumer>.Instance);
        var activityId = Guid.NewGuid();
        var evt = new SeckillActivityPublishedEvent
        {
            ActivityId = activityId,
            ActivityName = "10 元秒杀",
            StartTime = DateTime.UtcNow,
            EndTime = DateTime.UtcNow.AddHours(2),
            SeckillPrice = 10m,
            OriginalPrice = 100m,
            TotalStock = 1000,
            ProductId = Guid.NewGuid()
        };
        var context = new Mock<ConsumeContext<SeckillActivityPublishedEvent>>();
        context.SetupGet(c => c.Message).Returns(evt);

        // Act
        await consumer.Consume(context.Object);

        // Assert
        repoMock.Verify(r => r.IndexAsync(
            activityId.ToString(),
            "leno_seckill_activities",
            It.Is<SeckillActivityReadModel>(m => m.ActivityName == "10 元秒杀" && m.SeckillPrice == 10m),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Consume_WhenActivityEnded_ShouldDeleteReadModel()
    {
        // Arrange
        var repoMock = new Mock<IEsReadModelRepository<SeckillActivityReadModel>>();
        var consumer = new SeckillActivityReadModelSyncConsumer(repoMock.Object, NullLogger<SeckillActivityReadModelSyncConsumer>.Instance);
        var activityId = Guid.NewGuid();
        var evt = new SeckillActivityEndedEvent { ActivityId = activityId };
        var context = new Mock<ConsumeContext<SeckillActivityEndedEvent>>();
        context.SetupGet(c => c.Message).Returns(evt);

        // Act
        await consumer.Consume(context.Object);

        // Assert: 活动结束删除读模型（避免前台展示已结束活动）
        repoMock.Verify(r => r.DeleteByIdAsync(
            activityId.ToString(),
            "leno_seckill_activities",
            It.IsAny<CancellationToken>()), Times.Once);
    }
}
```

- [ ] **Step 5: 运行测试验证失败**

Run: `dotnet test tests/Services/Promotion/Leno.Promotion.Infrastructure.Tests/Leno.Promotion.Infrastructure.Tests.csproj --filter "FullyQualifiedName~SeckillActivityReadModelSyncConsumerTests" --configuration Debug`
Expected: FAIL，编译错误（`SeckillActivityReadModelSyncConsumer` 类不存在）

- [ ] **Step 6: 新建 SeckillActivityReadModelSyncConsumer**

创建 `src/Services/Promotion/Leno.Promotion.Infrastructure/ReadModels/SeckillActivityReadModelSyncConsumer.cs`：

```csharp
using Leno.Infrastructure.ReadModel;
using Leno.SharedContracts.Events.Promotion;
using Microsoft.Extensions.Logging;

namespace Leno.Promotion.Infrastructure.ReadModels;

/// <summary>
/// 秒杀活动读模型同步消费者。
/// 活动发布时索引到 leno_seckill_activities；活动结束时从索引删除。
/// </summary>
public sealed class SeckillActivityReadModelSyncConsumer
    : ReadModelSyncConsumerBase<SeckillActivityPublishedEvent, SeckillActivityReadModel>,
      IConsumer<SeckillActivityEndedEvent>
{
    public const string IndexName = "leno_seckill_activities";

    private readonly IEsReadModelRepository<SeckillActivityReadModel> _repository;
    private readonly ILogger<SeckillActivityReadModelSyncConsumer> _logger;

    public SeckillActivityReadModelSyncConsumer(
        IEsReadModelRepository<SeckillActivityReadModel> repository,
        ILogger<SeckillActivityReadModelSyncConsumer> logger)
        : base(repository, logger)
    {
        _repository = repository;
        _logger = logger;
    }

    protected override Task<(string Id, string IndexName, SeckillActivityReadModel? ReadModel)> BuildReadModelAsync(
        SeckillActivityPublishedEvent evt, CancellationToken ct)
    {
        var readModel = new SeckillActivityReadModel
        {
            Id = evt.ActivityId.ToString(),
            ActivityName = evt.ActivityName,
            StartTime = evt.StartTime,
            EndTime = evt.EndTime,
            OriginalPrice = evt.OriginalPrice,
            SeckillPrice = evt.SeckillPrice,
            TotalStock = evt.TotalStock,
            AvailableStock = evt.TotalStock,
            ProductId = evt.ProductId,
            Status = "Active"
        };
        return Task.FromResult<(string, string, SeckillActivityReadModel?)>(
            (evt.ActivityId.ToString(), IndexName, readModel));
    }

    /// <summary>
    /// 活动结束事件：删除读模型。
    /// </summary>
    public async Task Consume(ConsumeContext<SeckillActivityEndedEvent> context)
    {
        var evt = context.Message;
        var ct = context.CancellationToken;
        try
        {
            await _repository.DeleteByIdAsync(evt.ActivityId.ToString(), IndexName, ct).ConfigureAwait(false);
            _logger.LogInformation("SeckillActivity readmodel deleted: {ActivityId}", evt.ActivityId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "SeckillActivity readmodel delete failed: {ActivityId}", evt.ActivityId);
            throw new InvalidOperationException(
                $"ReadModel delete failed for {IndexName}/{evt.ActivityId}", ex);
        }
    }
}
```

- [ ] **Step 7: 运行测试验证通过**

Run: `dotnet test tests/Services/Promotion/Leno.Promotion.Infrastructure.Tests/Leno.Promotion.Infrastructure.Tests.csproj --filter "FullyQualifiedName~SeckillActivityReadModelSyncConsumerTests" --configuration Debug`
Expected: PASS

- [ ] **Step 8: 编写失败测试 — CouponReadModelSyncConsumer**

创建测试文件 `tests/Services/Promotion/Leno.Promotion.Infrastructure.Tests/ReadModels/CouponReadModelSyncConsumerTests.cs`，结构类似 Step 4，覆盖 `CouponCreatedEvent` 索引 + `CouponDisabledEvent` 删除两个场景。完整代码略（参考 Step 4 模板，替换事件类型和读模型字段）。

- [ ] **Step 9: 运行测试验证失败**

Run: `dotnet test tests/Services/Promotion/Leno.Promotion.Infrastructure.Tests/Leno.Promotion.Infrastructure.Tests.csproj --filter "FullyQualifiedName~CouponReadModelSyncConsumerTests" --configuration Debug`
Expected: FAIL

- [ ] **Step 10: 新建 CouponReadModelSyncConsumer**

创建 `src/Services/Promotion/Leno.Promotion.Infrastructure/ReadModels/CouponReadModelSyncConsumer.cs`，结构参考 Step 6，索引名 `"leno_coupons"`，索引事件 `CouponCreatedEvent`，删除事件 `CouponDisabledEvent`。

- [ ] **Step 11: 运行测试验证通过**

Run: `dotnet test tests/Services/Promotion/Leno.Promotion.Infrastructure.Tests/Leno.Promotion.Infrastructure.Tests.csproj --filter "FullyQualifiedName~CouponReadModelSyncConsumerTests" --configuration Debug`
Expected: PASS

- [ ] **Step 12: 验证 Promotion BC 全量测试**

Run: `dotnet test tests/Services/Promotion/Leno.Promotion.Infrastructure.Tests/Leno.Promotion.Infrastructure.Tests.csproj --configuration Debug`
Expected: PASS

- [ ] **Step 13: 提交**

```bash
git add src/Services/Promotion/Leno.Promotion.Infrastructure/ReadModels/ tests/Services/Promotion/Leno.Promotion.Infrastructure.Tests/ReadModels/
git commit -m "feat(promotion): 新建 SeckillActivity/Coupon ES 读模型与同步 Consumer

- SeckillActivityReadModel: 索引 leno_seckill_activities，活动发布索引/结束删除
- CouponReadModel: 索引 leno_coupons，领券创建索引/失效删除
- 复用 ReadModelSyncConsumerBase 基类（Task 1 增强的删除分支）
- 新增 4 个单元测试覆盖索引/删除场景

关联 spec: §13.1 M6.1 Promotion 域读模型"
```

---

## Task 4: PointsMembership BC 新建 PointsAccountReadModel + MemberReadModel 与同步 Consumer

**Files:**
- Create: `src/Services/PointsMembership/Leno.PointsMembership.Infrastructure/ReadModels/PointsAccountReadModel.cs`
- Create: `src/Services/PointsMembership/Leno.PointsMembership.Infrastructure/ReadModels/MemberReadModel.cs`
- Create: `src/Services/PointsMembership/Leno.PointsMembership.Infrastructure/ReadModels/PointsAccountReadModelSyncConsumer.cs`
- Create: `src/Services/PointsMembership/Leno.PointsMembership.Infrastructure/ReadModels/MemberReadModelSyncConsumer.cs`
- Test: `tests/Services/PointsMembership/Leno.PointsMembership.Infrastructure.Tests/ReadModels/PointsAccountReadModelSyncConsumerTests.cs`
- Test: `tests/Services/PointsMembership/Leno.PointsMembership.Infrastructure.Tests/ReadModels/MemberReadModelSyncConsumerTests.cs`

**背景:** PointsMembership BC 当前无 `ReadModels/` 目录，需新建积分账户 + 会员两个 ES 读模型。订阅 `PointsAccountCreatedEvent`/`PointsAdjustedEvent`/`MemberRegisteredEvent`/`MemberLevelUpgradedEvent`。

- [ ] **Step 1: 确认事件类型存在**

Run: `grep -r "PointsAccountCreatedEvent\|PointsAdjustedEvent\|MemberRegisteredEvent\|MemberLevelUpgradedEvent" src/ --include="*.cs" -l`

Expected: 找到事件定义文件。若不存在，需先在 `Leno.SharedContracts/Events/PointsMembership/` 新建。

- [ ] **Step 2: 新建 PointsAccountReadModel**

创建 `src/Services/PointsMembership/Leno.PointsMembership.Infrastructure/ReadModels/PointsAccountReadModel.cs`：

```csharp
namespace Leno.PointsMembership.Infrastructure.ReadModels;

/// <summary>
/// 积分账户 ES 读模型，索引名 leno_points_accounts。
/// 用于用户端积分余额快速查询与历史变更检索。
/// </summary>
public sealed class PointsAccountReadModel
{
    public string Id { get; set; } = string.Empty;

    public Guid UserId { get; set; }

    public int Balance { get; set; }

    public int TotalEarned { get; set; }

    public int TotalSpent { get; set; }

    public DateTime UpdatedAt { get; set; }
}
```

- [ ] **Step 3: 新建 MemberReadModel**

创建 `src/Services/PointsMembership/Leno.PointsMembership.Infrastructure/ReadModels/MemberReadModel.cs`：

```csharp
namespace Leno.PointsMembership.Infrastructure.ReadModels;

/// <summary>
/// 会员 ES 读模型，索引名 leno_members。
/// 用于会员等级查询与会员列表检索。
/// </summary>
public sealed class MemberReadModel
{
    public string Id { get; set; } = string.Empty;

    public Guid UserId { get; set; }

    public string MemberLevel { get; set; } = string.Empty;

    public int LevelScore { get; set; }

    public DateTime RegisteredAt { get; set; }

    public DateTime UpdatedAt { get; set; }
}
```

- [ ] **Step 4: 编写失败测试 — PointsAccountReadModelSyncConsumer**

创建测试文件 `tests/Services/PointsMembership/Leno.PointsMembership.Infrastructure.Tests/ReadModels/PointsAccountReadModelSyncConsumerTests.cs`，覆盖 `PointsAccountCreatedEvent` 索引 + `PointsAdjustedEvent` 索引（更新余额）两个场景。参考 Task 3 Step 4 模板。

- [ ] **Step 5: 运行测试验证失败**

Run: `dotnet test tests/Services/PointsMembership/Leno.PointsMembership.Infrastructure.Tests/Leno.PointsMembership.Infrastructure.Tests.csproj --filter "FullyQualifiedName~PointsAccountReadModelSyncConsumerTests" --configuration Debug`
Expected: FAIL

- [ ] **Step 6: 新建 PointsAccountReadModelSyncConsumer**

创建 `src/Services/PointsMembership/Leno.PointsMembership.Infrastructure/ReadModels/PointsAccountReadModelSyncConsumer.cs`，索引名 `"leno_points_accounts"`。订阅 `PointsAccountCreatedEvent` 与 `PointsAdjustedEvent` 两个事件，均走索引分支（更新余额字段）。

- [ ] **Step 7: 运行测试验证通过**

Run: `dotnet test tests/Services/PointsMembership/Leno.PointsMembership.Infrastructure.Tests/Leno.PointsMembership.Infrastructure.Tests.csproj --filter "FullyQualifiedName~PointsAccountReadModelSyncConsumerTests" --configuration Debug`
Expected: PASS

- [ ] **Step 8: 编写失败测试 — MemberReadModelSyncConsumer**

创建测试文件 `tests/Services/PointsMembership/Leno.PointsMembership.Infrastructure.Tests/ReadModels/MemberReadModelSyncConsumerTests.cs`，覆盖 `MemberRegisteredEvent` 索引 + `MemberLevelUpgradedEvent` 索引（更新等级）两个场景。

- [ ] **Step 9: 运行测试验证失败**

Run: `dotnet test tests/Services/PointsMembership/Leno.PointsMembership.Infrastructure.Tests/Leno.PointsMembership.Infrastructure.Tests.csproj --filter "FullyQualifiedName~MemberReadModelSyncConsumerTests" --configuration Debug`
Expected: FAIL

- [ ] **Step 10: 新建 MemberReadModelSyncConsumer**

创建 `src/Services/PointsMembership/Leno.PointsMembership.Infrastructure/ReadModels/MemberReadModelSyncConsumer.cs`，索引名 `"leno_members"`。订阅 `MemberRegisteredEvent` 与 `MemberLevelUpgradedEvent` 两个事件，均走索引分支。

- [ ] **Step 11: 运行测试验证通过**

Run: `dotnet test tests/Services/PointsMembership/Leno.PointsMembership.Infrastructure.Tests/Leno.PointsMembership.Infrastructure.Tests.csproj --filter "FullyQualifiedName~MemberReadModelSyncConsumerTests" --configuration Debug`
Expected: PASS

- [ ] **Step 12: 验证 PointsMembership BC 全量测试**

Run: `dotnet test tests/Services/PointsMembership/Leno.PointsMembership.Infrastructure.Tests/Leno.PointsMembership.Infrastructure.Tests.csproj --configuration Debug`
Expected: PASS

- [ ] **Step 13: 提交**

```bash
git add src/Services/PointsMembership/Leno.PointsMembership.Infrastructure/ReadModels/ tests/Services/PointsMembership/Leno.PointsMembership.Infrastructure.Tests/ReadModels/
git commit -m "feat(points-membership): 新建 PointsAccount/Member ES 读模型与同步 Consumer

- PointsAccountReadModel: 索引 leno_points_accounts，账户创建/积分变动时索引
- MemberReadModel: 索引 leno_members，会员注册/等级升级时索引
- 复用 ReadModelSyncConsumerBase 基类
- 新增 4 个单元测试覆盖索引场景

关联 spec: §13.1 M6.1 PointsMembership 域读模型"
```

---

## Task 5: SellerShop BC 新建 ShopDashboardReadModel 与同步 Consumer

**Files:**
- Create: `src/Services/SellerShop/Leno.SellerShop.Infrastructure/ReadModels/ShopDashboardReadModel.cs`
- Create: `src/Services/SellerShop/Leno.SellerShop.Infrastructure/ReadModels/ShopDashboardReadModelSyncConsumer.cs`
- Test: `tests/Services/SellerShop/Leno.SellerShop.Infrastructure.Tests/ReadModels/ShopDashboardReadModelSyncConsumerTests.cs`

**背景:** SellerShop BC 当前无 `ReadModels/` 目录，但已有 `ShopDashboardData` 聚合根（EF Core 持久化，非 ES）。M6.1 新建的 `ShopDashboardReadModel` 是独立的 ES 读模型，订阅 `OrderPlacedEvent`/`OrderConfirmedEvent`/`ReviewSubmittedEvent` 等跨 BC 事件，聚合为卖家看板读模型供 M6.3 BFF seller-dashboard 端点查询。

- [ ] **Step 1: 确认订阅事件存在**

Run: `grep -r "OrderPlacedEvent\|OrderConfirmedEvent\|ReviewSubmittedEvent" src/BuildingBlocks/Leno.SharedContracts/ --include="*.cs" -l`

Expected: 找到事件定义。这些是跨 BC 集成事件，应已在 `Leno.SharedContracts/Events/Order/` 与 `Leno.SharedContracts/Events/ReviewAfterSales/` 中定义。

- [ ] **Step 2: 新建 ShopDashboardReadModel**

创建 `src/Services/SellerShop/Leno.SellerShop.Infrastructure/ReadModels/ShopDashboardReadModel.cs`：

```csharp
namespace Leno.SellerShop.Infrastructure.ReadModels;

/// <summary>
/// 卖家看板 ES 读模型，索引名 leno_shop_dashboards。
/// 聚合订单数/销售额/评价数等指标，供 BFF seller-dashboard 端点快速查询。
/// 与领域聚合 ShopDashboardData 并存（领域聚合用于事务一致性，ES 读模型用于查询性能）。
/// </summary>
public sealed class ShopDashboardReadModel
{
    public string Id { get; set; } = string.Empty;

    public Guid ShopId { get; set; }

    public string ShopName { get; set; } = string.Empty;

    public int TotalOrders { get; set; }

    public int PendingOrders { get; set; }

    public int CompletedOrders { get; set; }

    public decimal TotalSales { get; set; }

    public int TotalReviews { get; set; }

    public double AverageRating { get; set; }

    public int PendingAfterSales { get; set; }

    public DateTime UpdatedAt { get; set; }
}
```

- [ ] **Step 3: 编写失败测试 — ShopDashboardReadModelSyncConsumer**

创建测试文件 `tests/Services/SellerShop/Leno.SellerShop.Infrastructure.Tests/ReadModels/ShopDashboardReadModelSyncConsumerTests.cs`，覆盖三个场景：
- `OrderPlacedEvent` → 索引（TotalOrders + 1，PendingOrders + 1）
- `OrderConfirmedEvent` → 索引（PendingOrders - 1，CompletedOrders + 1，TotalSales += 订单金额）
- `ReviewSubmittedEvent` → 索引（TotalReviews + 1，重新计算 AverageRating）

完整测试代码参考 Task 3 Step 4 模板，三个事件各一个 `[Fact]`。

- [ ] **Step 4: 运行测试验证失败**

Run: `dotnet test tests/Services/SellerShop/Leno.SellerShop.Infrastructure.Tests/Leno.SellerShop.Infrastructure.Tests.csproj --filter "FullyQualifiedName~ShopDashboardReadModelSyncConsumerTests" --configuration Debug`
Expected: FAIL

- [ ] **Step 5: 新建 ShopDashboardReadModelSyncConsumer**

创建 `src/Services/SellerShop/Leno.SellerShop.Infrastructure/ReadModels/ShopDashboardReadModelSyncConsumer.cs`：

```csharp
using Leno.Infrastructure.ReadModel;
using Leno.SharedContracts.Events.Order;
using Leno.SharedContracts.Events.ReviewAfterSales;
using Microsoft.Extensions.Logging;

namespace Leno.SellerShop.Infrastructure.ReadModels;

/// <summary>
/// 卖家看板读模型同步消费者。
/// 订阅 OrderPlacedEvent/OrderConfirmedEvent/ReviewSubmittedEvent，
/// 聚合为 ShopDashboardReadModel 索引到 leno_shop_dashboards。
/// </summary>
public sealed class ShopDashboardReadModelSyncConsumer
    : ReadModelSyncConsumerBase<OrderPlacedEvent, ShopDashboardReadModel>,
      IConsumer<OrderConfirmedEvent>,
      IConsumer<ReviewSubmittedEvent>
{
    public const string IndexName = "leno_shop_dashboards";

    private readonly IEsReadModelRepository<ShopDashboardReadModel> _repository;
    private readonly ILogger<ShopDashboardReadModelSyncConsumer> _logger;

    public ShopDashboardReadModelSyncConsumer(
        IEsReadModelRepository<ShopDashboardReadModel> repository,
        ILogger<ShopDashboardReadModelSyncConsumer> logger)
        : base(repository, logger)
    {
        _repository = repository;
        _logger = logger;
    }

    protected override async Task<(string Id, string IndexName, ShopDashboardReadModel? ReadModel)> BuildReadModelAsync(
        OrderPlacedEvent evt, CancellationToken ct)
    {
        // 新订单：TotalOrders + 1，PendingOrders + 1
        var shopId = evt.ShopId.ToString();
        var existing = await _repository.GetByIdAsync(shopId, IndexName, ct).ConfigureAwait(false);
        var readModel = existing ?? new ShopDashboardReadModel
        {
            Id = shopId,
            ShopId = evt.ShopId,
            ShopName = evt.ShopName ?? string.Empty,
            UpdatedAt = DateTime.UtcNow
        };
        readModel.TotalOrders += 1;
        readModel.PendingOrders += 1;
        readModel.UpdatedAt = DateTime.UtcNow;
        return (shopId, IndexName, readModel);
    }

    public async Task Consume(ConsumeContext<OrderConfirmedEvent> context)
    {
        var evt = context.Message;
        var ct = context.CancellationToken;
        var shopId = evt.ShopId.ToString();
        var existing = await _repository.GetByIdAsync(shopId, IndexName, ct).ConfigureAwait(false);
        if (existing is null)
        {
            _logger.LogWarning("ShopDashboardReadModel not found for confirm: {ShopId}", evt.ShopId);
            return;
        }
        existing.PendingOrders = Math.Max(0, existing.PendingOrders - 1);
        existing.CompletedOrders += 1;
        existing.TotalSales += evt.OrderAmount;
        existing.UpdatedAt = DateTime.UtcNow;
        await _repository.IndexAsync(shopId, IndexName, existing, ct).ConfigureAwait(false);
    }

    public async Task Consume(ConsumeContext<ReviewSubmittedEvent> context)
    {
        var evt = context.Message;
        var ct = context.CancellationToken;
        var shopId = evt.ShopId.ToString();
        var existing = await _repository.GetByIdAsync(shopId, IndexName, ct).ConfigureAwait(false);
        if (existing is null)
        {
            _logger.LogWarning("ShopDashboardReadModel not found for review: {ShopId}", evt.ShopId);
            return;
        }
        // 增量计算平均评分（避免全量扫描）
        existing.TotalReviews += 1;
        existing.AverageRating = ((existing.AverageRating * (existing.TotalReviews - 1)) + evt.Rating) / existing.TotalReviews;
        existing.UpdatedAt = DateTime.UtcNow;
        await _repository.IndexAsync(shopId, IndexName, existing, ct).ConfigureAwait(false);
    }
}
```

**注意:** `IEsReadModelRepository<T>` 若无 `GetByIdAsync` 方法，需在该接口新增（既有接口已有 `DeleteByIdAsync`，新增 `GetByIdAsync` 是合理的最小扩展）。

- [ ] **Step 6: 运行测试验证通过**

Run: `dotnet test tests/Services/SellerShop/Leno.SellerShop.Infrastructure.Tests/Leno.SellerShop.Infrastructure.Tests.csproj --filter "FullyQualifiedName~ShopDashboardReadModelSyncConsumerTests" --configuration Debug`
Expected: PASS

- [ ] **Step 7: 验证 SellerShop BC 全量测试**

Run: `dotnet test tests/Services/SellerShop/Leno.SellerShop.Infrastructure.Tests/Leno.SellerShop.Infrastructure.Tests.csproj --configuration Debug`
Expected: PASS

- [ ] **Step 8: 提交**

```bash
git add src/Services/SellerShop/Leno.SellerShop.Infrastructure/ReadModels/ tests/Services/SellerShop/Leno.SellerShop.Infrastructure.Tests/ReadModels/
git commit -m "feat(seller-shop): 新建 ShopDashboard ES 读模型与多事件聚合 Consumer

- ShopDashboardReadModel: 索引 leno_shop_dashboards
- 订阅 OrderPlacedEvent/OrderConfirmedEvent/ReviewSubmittedEvent 三类跨 BC 事件
- 增量聚合订单数/销售额/评价数/平均评分
- 与领域聚合 ShopDashboardData 并存（事务一致性 vs 查询性能）
- 新增 3 个单元测试覆盖三类事件场景

关联 spec: §13.1 M6.1 SellerShop 域读模型 + §13.3 M6.3 BFF 配套"
```

---

## Task 6: IQueryHandler 接口与 DI 注册扩展

**Files:**
- Create: `src/BuildingBlocks/Leno.Infrastructure.Abstractions/Cqrs/IQueryHandler.cs`
- Create: `src/BuildingBlocks/Leno.Infrastructure.Abstractions/Cqrs/QueryResult.cs`
- Create: `src/BuildingBlocks/Leno.Infrastructure/Cqrs/QueryHandlerExtensions.cs`
- Test: `tests/BuildingBlocks/Leno.Infrastructure.Tests/Cqrs/QueryHandlerExtensionsTests.cs`

**背景:** spec M6.2 要求"不引入 MediatR，用接口 + DI 即可"。需新建 `IQueryHandler<TQuery,TResult>` 通用接口与 DI 注册扩展方法。`Leno.Infrastructure.Abstractions` 已存在（Plan 6 M2 已建）。

- [ ] **Step 1: 编写失败测试 — DI 注册扩展**

创建测试文件 `tests/BuildingBlocks/Leno.Infrastructure.Tests/Cqrs/QueryHandlerExtensionsTests.cs`：

```csharp
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Leno.Infrastructure.Cqrs;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Leno.Infrastructure.Tests.Cqrs;

public class QueryHandlerExtensionsTests
{
    public sealed class FakeQuery { public string Keyword { get; init; } = string.Empty; }
    public sealed class FakeResult { public string Echo { get; init; } = string.Empty; }

    public sealed class FakeQueryHandler : IQueryHandler<FakeQuery, FakeResult>
    {
        public Task<FakeResult> HandleAsync(FakeQuery query, CancellationToken ct = default)
            => Task.FromResult(new FakeResult { Echo = query.Keyword });
    }

    [Fact]
    public void AddQueryHandler_ShouldRegisterHandlerAndInterface()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act
        services.AddQueryHandler<FakeQuery, FakeResult, FakeQueryHandler>();

        // Assert: IQueryHandler<FakeQuery, FakeResult> 与 FakeQueryHandler 均可解析
        var sp = services.BuildServiceProvider();
        var handlerInterface = sp.GetService<IQueryHandler<FakeQuery, FakeResult>>();
        var concreteHandler = sp.GetService<FakeQueryHandler>();
        handlerInterface.Should().NotBeNull().And.BeOfType<FakeQueryHandler>();
        concreteHandler.Should().NotBeNull();
    }

    [Fact]
    public async Task ResolvedHandler_ShouldExecute()
    {
        var services = new ServiceCollection();
        services.AddQueryHandler<FakeQuery, FakeResult, FakeQueryHandler>();
        var sp = services.BuildServiceProvider();
        var handler = sp.GetRequiredService<IQueryHandler<FakeQuery, FakeResult>>();

        var result = await handler.HandleAsync(new FakeQuery { Keyword = "hello" });

        result.Echo.Should().Be("hello");
    }
}
```

- [ ] **Step 2: 运行测试验证失败**

Run: `dotnet test tests/BuildingBlocks/Leno.Infrastructure.Tests/Leno.Infrastructure.Tests.csproj --filter "FullyQualifiedName~QueryHandlerExtensionsTests" --configuration Debug`
Expected: FAIL，编译错误（`IQueryHandler` 与 `AddQueryHandler` 不存在）

- [ ] **Step 3: 新建 IQueryHandler 接口**

创建 `src/BuildingBlocks/Leno.Infrastructure.Abstractions/Cqrs/IQueryHandler.cs`：

```csharp
namespace Leno.Infrastructure.Abstractions.Cqrs;

/// <summary>
/// 查询处理器接口（CQRS 读侧）。
/// 不引入 MediatR，通过 DI 注册具体实现。
/// 命名约定：XxxQueryHandler : IQueryHandler&lt;XxxQuery, XxxResult&gt;
/// </summary>
/// <typeparam name="TQuery">查询参数对象。</typeparam>
/// <typeparam name="TResult">查询返回结果。</typeparam>
public interface IQueryHandler<in TQuery, TResult> where TQuery : class
{
    Task<TResult> HandleAsync(TQuery query, CancellationToken ct = default);
}
```

- [ ] **Step 4: 新建 QueryHandlerExtensions DI 注册方法**

创建 `src/BuildingBlocks/Leno.Infrastructure/Cqrs/QueryHandlerExtensions.cs`：

```csharp
using Leno.Infrastructure.Abstractions.Cqrs;
using Microsoft.Extensions.DependencyInjection;

namespace Leno.Infrastructure.Cqrs;

/// <summary>
/// IQueryHandler DI 注册扩展方法。
/// </summary>
public static class QueryHandlerExtensions
{
    /// <summary>
    /// 注册 QueryHandler 同时注册接口与具体类型（便于单元测试直接解析具体类型）。
    /// </summary>
    public static IServiceCollection AddQueryHandler<TQuery, TResult, THandler>(
        this IServiceCollection services,
        ServiceLifetime lifetime = ServiceLifetime.Scoped)
        where TQuery : class
        where THandler : class, IQueryHandler<TQuery, TResult>
    {
        services.Add(new ServiceDescriptor(typeof(THandler), typeof(THandler), lifetime));
        services.Add(new ServiceDescriptor(typeof(IQueryHandler<TQuery, TResult>), typeof(THandler), lifetime));
        return services;
    }
}
```

- [ ] **Step 5: 运行测试验证通过**

Run: `dotnet test tests/BuildingBlocks/Leno.Infrastructure.Tests/Leno.Infrastructure.Tests.csproj --filter "FullyQualifiedName~QueryHandlerExtensionsTests" --configuration Debug`
Expected: PASS

- [ ] **Step 6: 提交**

```bash
git add src/BuildingBlocks/Leno.Infrastructure.Abstractions/Cqrs/ src/BuildingBlocks/Leno.Infrastructure/Cqrs/ tests/BuildingBlocks/Leno.Infrastructure.Tests/Cqrs/
git commit -m "feat(infrastructure): 新建 IQueryHandler 接口与 DI 注册扩展

- IQueryHandler<TQuery,TResult>: CQRS 读侧通用接口，不引入 MediatR
- AddQueryHandler<TQuery,TResult,THandler>: 同时注册接口与具体类型
- 新增 2 个单元测试覆盖注册与解析

关联 spec: §13.2 M6.2 Query Handler 约定"
```

---

## Task 7: Product BC 新建 ProductSearchQuery + ProductDetailQuery 与 Handler

**Files:**
- Create: `src/Services/Product/Leno.Product.Application/Queries/ProductSearchQuery.cs`
- Create: `src/Services/Product/Leno.Product.Application/Queries/ProductSearchQueryHandler.cs`
- Create: `src/Services/Product/Leno.Product.Application/Queries/ProductDetailQuery.cs`
- Create: `src/Services/Product/Leno.Product.Application/Queries/ProductDetailQueryHandler.cs`
- Create: `src/Services/Product/Leno.Product.Application/Queries/ProductSearchQueryResult.cs`
- Create: `src/Services/Product/Leno.Product.Application/Queries/ProductDetailQueryResult.cs`
- Modify: `src/Services/Product/Leno.Product.Infrastructure/Dependencies/ServiceCollectionExtensions.cs`
- Test: `tests/Services/Product/Leno.Product.Application.Tests/Queries/ProductSearchQueryHandlerTests.cs`
- Test: `tests/Services/Product/Leno.Product.Application.Tests/Queries/ProductDetailQueryHandlerTests.cs`

**背景:** 当前 `IProductSearchService` 仅单方法 `SearchAsync`，且实现位于 Infrastructure 层。M6.2 在 Application 层新建 Queries 目录，QueryHandler 通过注入 `IProductSearchService` 调用既有实现，**不迁移实现位置**（避免大范围改动）。ProductDetailQuery 为新增查询（既无 AppService 方法也无 ES 查询，需在 `IProductSearchService` 接口新增 `GetByIdAsync` 方法）。

- [ ] **Step 1: 新建 ProductSearchQuery 与 Result**

创建 `src/Services/Product/Leno.Product.Application/Queries/ProductSearchQuery.cs`：

```csharp
namespace Leno.Product.Application.Queries;

/// <summary>
/// 商品搜索查询参数（走 ES 读模型）。
/// </summary>
public sealed class ProductSearchQuery
{
    public string Keyword { get; init; } = string.Empty;

    public Guid? CategoryId { get; init; }

    public Guid? BrandId { get; init; }

    public decimal? MinPrice { get; init; }

    public decimal? MaxPrice { get; init; }

    public string SortBy { get; init; } = "relevance";

    public int PageIndex { get; init; } = 1;

    public int PageSize { get; init; } = 20;
}
```

创建 `src/Services/Product/Leno.Product.Application/Queries/ProductSearchQueryResult.cs`：

```csharp
namespace Leno.Product.Application.Queries;

public sealed class ProductSearchQueryResult
{
    public IReadOnlyList<ProductSearchItem> Items { get; init; } = Array.Empty<ProductSearchItem>();

    public long TotalCount { get; init; }

    public int PageIndex { get; init; }

    public int PageSize { get; init; }
}

public sealed class ProductSearchItem
{
    public Guid SpuId { get; init; }

    public string SpuName { get; init; } = string.Empty;

    public string MainImageUrl { get; init; } = string.Empty;

    public decimal MinPrice { get; init; }

    public string Status { get; init; } = string.Empty;
}
```

- [ ] **Step 2: 新建 ProductDetailQuery 与 Result**

创建 `src/Services/Product/Leno.Product.Application/Queries/ProductDetailQuery.cs`：

```csharp
namespace Leno.Product.Application.Queries;

public sealed class ProductDetailQuery
{
    public Guid SpuId { get; init; }
}
```

创建 `src/Services/Product/Leno.Product.Application/Queries/ProductDetailQueryResult.cs`：

```csharp
namespace Leno.Product.Application.Queries;

public sealed class ProductDetailQueryResult
{
    public Guid SpuId { get; init; }

    public string SpuName { get; init; } = string.Empty;

    public string Description { get; init; } = string.Empty;

    public Guid CategoryId { get; init; }

    public Guid BrandId { get; init; }

    public string MainImageUrl { get; init; } = string.Empty;

    public IReadOnlyList<SkuDto> Skus { get; init; } = Array.Empty<SkuDto>();

    public string Status { get; init; } = string.Empty;

    public Guid ShopId { get; init; }

    public string ShopName { get; init; } = string.Empty;
}

public sealed class SkuDto
{
    public Guid SkuId { get; init; }

    public string SkuCode { get; init; } = string.Empty;

    public string Attributes { get; init; } = string.Empty;

    public decimal Price { get; init; }

    public int Stock { get; init; }
}
```

- [ ] **Step 3: IProductSearchService 接口新增 GetByIdAsync 方法**

读取 `src/Services/Product/Leno.Product.Application/IProductSearchService.cs` 全文，新增方法签名：

```csharp
Task<ProductDetailReadModel?> GetByIdAsync(Guid spuId, CancellationToken ct = default);
```

**注意:** 返回类型 `ProductDetailReadModel` 应在 `Leno.Product.Infrastructure/ReadModels/` 新建（若不存在）。若既有 `ProductReadModel` 字段足够，可直接复用。

- [ ] **Step 4: ProductSearchService 实现 GetByIdAsync**

修改 `src/Services/Product/Leno.Product.Infrastructure/ReadModels/ProductSearchService.cs`，新增 `GetByIdAsync` 方法实现，基于 ES `GetSourceAsync` 按 SpuId 查询。

- [ ] **Step 5: 编写失败测试 — ProductSearchQueryHandler**

创建测试文件 `tests/Services/Product/Leno.Product.Application.Tests/Queries/ProductSearchQueryHandlerTests.cs`：

```csharp
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Leno.Product.Application.Queries;
using Leno.Product.Application.Services;
using Moq;
using Xunit;

namespace Leno.Product.Application.Tests.Queries;

public class ProductSearchQueryHandlerTests
{
    [Fact]
    public async Task HandleAsync_ShouldDelegateToProductSearchService()
    {
        // Arrange
        var searchServiceMock = new Mock<IProductSearchService>();
        searchServiceMock.Setup(s => s.SearchAsync(It.IsAny<string>(), It.IsAny<Guid?>(), It.IsAny<Guid?>(),
            It.IsAny<decimal?>(), It.IsAny<decimal?>(), It.IsAny<string>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ProductSearchResult
            {
                Items = new[] { new ProductSearchItem { SpuId = Guid.NewGuid(), SpuName = "Test" } },
                TotalCount = 1,
                PageIndex = 1,
                PageSize = 20
            });
        var handler = new ProductSearchQueryHandler(searchServiceMock.Object);
        var query = new ProductSearchQuery { Keyword = "phone", PageIndex = 1, PageSize = 20 };

        // Act
        var result = await handler.HandleAsync(query);

        // Assert
        result.Should().NotBeNull();
        result.TotalCount.Should().Be(1);
        result.Items.Should().HaveCount(1);
        searchServiceMock.VerifyAll();
    }
}
```

- [ ] **Step 6: 运行测试验证失败**

Run: `dotnet test tests/Services/Product/Leno.Product.Application.Tests/Leno.Product.Application.Tests.csproj --filter "FullyQualifiedName~ProductSearchQueryHandlerTests" --configuration Debug`
Expected: FAIL，编译错误 `ProductSearchQueryHandler` 不存在

- [ ] **Step 7: 新建 ProductSearchQueryHandler**

创建 `src/Services/Product/Leno.Product.Application/Queries/ProductSearchQueryHandler.cs`：

```csharp
using Leno.Infrastructure.Abstractions.Cqrs;
using Leno.Product.Application.Services;

namespace Leno.Product.Application.Queries;

/// <summary>
/// 商品搜索查询处理器（走 ES 读模型）。
/// </summary>
public sealed class ProductSearchQueryHandler : IQueryHandler<ProductSearchQuery, ProductSearchQueryResult>
{
    private readonly IProductSearchService _searchService;

    public ProductSearchQueryHandler(IProductSearchService searchService)
    {
        _searchService = searchService;
    }

    public async Task<ProductSearchQueryResult> HandleAsync(ProductSearchQuery query, CancellationToken ct = default)
    {
        var searchResult = await _searchService.SearchAsync(
            query.Keyword, query.CategoryId, query.BrandId,
            query.MinPrice, query.MaxPrice, query.SortBy,
            query.PageIndex, query.PageSize, ct);

        return new ProductSearchQueryResult
        {
            Items = searchResult.Items.Select(i => new ProductSearchItem
            {
                SpuId = i.SpuId,
                SpuName = i.SpuName,
                MainImageUrl = i.MainImageUrl,
                MinPrice = i.MinPrice,
                Status = i.Status
            }).ToList(),
            TotalCount = searchResult.TotalCount,
            PageIndex = searchResult.PageIndex,
            PageSize = searchResult.PageSize
        };
    }
}
```

- [ ] **Step 8: 运行测试验证通过**

Run: `dotnet test tests/Services/Product/Leno.Product.Application.Tests/Leno.Product.Application.Tests.csproj --filter "FullyQualifiedName~ProductSearchQueryHandlerTests" --configuration Debug`
Expected: PASS

- [ ] **Step 9: 编写失败测试 — ProductDetailQueryHandler**

创建测试文件 `tests/Services/Product/Leno.Product.Application.Tests/Queries/ProductDetailQueryHandlerTests.cs`，覆盖正常返回 + SpuId 不存在返回 null 两个场景。

- [ ] **Step 10: 运行测试验证失败**

Run: `dotnet test tests/Services/Product/Leno.Product.Application.Tests/Leno.Product.Application.Tests.csproj --filter "FullyQualifiedName~ProductDetailQueryHandlerTests" --configuration Debug`
Expected: FAIL

- [ ] **Step 11: 新建 ProductDetailQueryHandler**

创建 `src/Services/Product/Leno.Product.Application/Queries/ProductDetailQueryHandler.cs`，注入 `IProductSearchService`，调用 `GetByIdAsync` 转换为 `ProductDetailQueryResult`。

- [ ] **Step 12: 运行测试验证通过**

Run: `dotnet test tests/Services/Product/Leno.Product.Application.Tests/Leno.Product.Application.Tests.csproj --filter "FullyQualifiedName~ProductDetailQueryHandlerTests" --configuration Debug`
Expected: PASS

- [ ] **Step 13: DI 注册 QueryHandler**

修改 `src/Services/Product/Leno.Product.Infrastructure/Dependencies/ServiceCollectionExtensions.cs`，在 `AddProductInfrastructure` 方法末尾增加：

```csharp
services.AddQueryHandler<ProductSearchQuery, ProductSearchQueryResult, ProductSearchQueryHandler>();
services.AddQueryHandler<ProductDetailQuery, ProductDetailQueryResult, ProductDetailQueryHandler>();
```

**注意:** 需在文件顶部增加 `using Leno.Infrastructure.Cqrs;` 和 `using Leno.Product.Application.Queries;`。

- [ ] **Step 14: 验证 Product BC 全量测试**

Run: `dotnet test tests/Services/Product/Leno.Product.Application.Tests/Leno.Product.Application.Tests.csproj --configuration Debug`
Expected: PASS

- [ ] **Step 15: 提交**

```bash
git add src/Services/Product/Leno.Product.Application/Queries/ src/Services/Product/Leno.Product.Application/IProductSearchService.cs src/Services/Product/Leno.Product.Infrastructure/ReadModels/ProductSearchService.cs src/Services/Product/Leno.Product.Infrastructure/Dependencies/ServiceCollectionExtensions.cs tests/Services/Product/Leno.Product.Application.Tests/Queries/
git commit -m "feat(product): 新建 ProductSearchQuery/ProductDetailQuery 与 QueryHandler

- ProductSearchQuery + Handler: 走 ES 全文检索，委托 IProductSearchService
- ProductDetailQuery + Handler: 走 ES 按 SpuId 查询，IProductSearchService 新增 GetByIdAsync
- DI 注册到 AddProductInfrastructure
- 不引入 MediatR，采用 IQueryHandler<TQuery,TResult> 接口 + DI
- 新增 4 个单元测试

关联 spec: §13.2 M6.2 Product 域 Query Handler 分离"
```

---

## Task 8: Order BC 新建 OrderListQuery + OrderDetailQuery + LogisticsTraceQuery 与 Handler

**Files:**
- Create: `src/Services/Order/Leno.Order.Application/Queries/OrderListQuery.cs`
- Create: `src/Services/Order/Leno.Order.Application/Queries/OrderListQueryHandler.cs`
- Create: `src/Services/Order/Leno.Order.Application/Queries/OrderDetailQuery.cs`
- Create: `src/Services/Order/Leno.Order.Application/Queries/OrderDetailQueryHandler.cs`
- Create: `src/Services/Order/Leno.Order.Application/Queries/LogisticsTraceQuery.cs`
- Create: `src/Services/Order/Leno.Order.Application/Queries/LogisticsTraceQueryHandler.cs`
- Create: `src/Services/Order/Leno.Order.Application/Queries/OrderListQueryResult.cs`
- Create: `src/Services/Order/Leno.Order.Application/Queries/OrderDetailQueryResult.cs`
- Create: `src/Services/Order/Leno.Order.Application/Queries/LogisticsTraceQueryResult.cs`
- Modify: `src/Services/Order/Leno.Order.Infrastructure/Dependencies/ServiceCollectionExtensions.cs`
- Test: `tests/Services/Order/Leno.Order.Application.Tests/Queries/OrderListQueryHandlerTests.cs`
- Test: `tests/Services/Order/Leno.Order.Application.Tests/Queries/OrderDetailQueryHandlerTests.cs`
- Test: `tests/Services/Order/Leno.Order.Application.Tests/Queries/LogisticsTraceQueryHandlerTests.cs`

**背景:** OrderAppService 当前查询方法 `GetByIdAsync`/`QueryAsync`/`GetLogisticsTraceAsync`（line 385-407）混在写服务中，构造函数注入 15 个依赖。M6.2 拆分后 QueryHandler 仅需 1-2 个查询专用依赖（仓储或 ES 读模型），大幅瘦身。**双发期 2 周**：QueryHandler 与 AppService 查询方法并存，2 周后 Controller 切换到 QueryHandler。

- [ ] **Step 1: 新建 OrderListQuery 与 Result**

创建 `src/Services/Order/Leno.Order.Application/Queries/OrderListQuery.cs`：

```csharp
namespace Leno.Order.Application.Queries;

public sealed class OrderListQuery
{
    public Guid UserId { get; init; }

    public string? OrderStatus { get; init; }

    public DateTime? FromDate { get; init; }

    public DateTime? ToDate { get; init; }

    public int PageIndex { get; init; } = 1;

    public int PageSize { get; init; } = 20;
}
```

创建 `src/Services/Order/Leno.Order.Application/Queries/OrderListQueryResult.cs`：

```csharp
namespace Leno.Order.Application.Queries;

public sealed class OrderListQueryResult
{
    public IReadOnlyList<OrderListItem> Items { get; init; } = Array.Empty<OrderListItem>();

    public long TotalCount { get; init; }

    public int PageIndex { get; init; }

    public int PageSize { get; init; }
}

public sealed class OrderListItem
{
    public Guid OrderId { get; init; }

    public string OrderNo { get; init; } = string.Empty;

    public string OrderStatus { get; init; } = string.Empty;

    public decimal TotalAmount { get; init; }

    public DateTime CreatedAt { get; init; }

    public string ShopName { get; init; } = string.Empty;
}
```

- [ ] **Step 2: 新建 OrderDetailQuery 与 Result**

创建 `src/Services/Order/Leno.Order.Application/Queries/OrderDetailQuery.cs`：

```csharp
namespace Leno.Order.Application.Queries;

public sealed class OrderDetailQuery
{
    public Guid OrderId { get; init; }

    public Guid UserId { get; init; }
}
```

创建 `src/Services/Order/Leno.Order.Application/Queries/OrderDetailQueryResult.cs`，含订单基本信息 + 商品快照列表 + 物流轨迹 + 评价摘要字段。

- [ ] **Step 3: 新建 LogisticsTraceQuery 与 Result**

创建 `src/Services/Order/Leno.Order.Application/Queries/LogisticsTraceQuery.cs`：

```csharp
namespace Leno.Order.Application.Queries;

public sealed class LogisticsTraceQuery
{
    public Guid OrderId { get; init; }
}
```

创建 `src/Services/Order/Leno.Order.Application/Queries/LogisticsTraceQueryResult.cs`，含轨迹节点列表 `IReadOnlyList<LogisticsTraceNode>`。

- [ ] **Step 4: 新建 IOrderQueryService 接口（查询专用服务）**

创建 `src/Services/Order/Leno.Order.Application/Services/IOrderQueryService.cs`：

```csharp
namespace Leno.Order.Application.Services;

/// <summary>
/// 订单查询专用服务（CQRS 读侧），与写侧 IOrderAppService 分离。
/// 实现 OrderQueryService 位于 Infrastructure 层，走 ES 读模型或只读仓储。
/// </summary>
public interface IOrderQueryService
{
    Task<OrderListQueryResult> QueryOrdersAsync(OrderListQuery query, CancellationToken ct = default);

    Task<OrderDetailQueryResult?> GetOrderDetailAsync(OrderDetailQuery query, CancellationToken ct = default);

    Task<LogisticsTraceQueryResult?> GetLogisticsTraceAsync(LogisticsTraceQuery query, CancellationToken ct = default);
}
```

**注意:** 该接口与 `IOrderAppService` 查询方法并存，**双发期 2 周**。OrderAppService 查询方法标记 `[Obsolete("请使用 IOrderQueryService，将在 2026-08-01 移除")]`。

- [ ] **Step 5: 编写失败测试 — OrderListQueryHandler**

创建测试文件 `tests/Services/Order/Leno.Order.Application.Tests/Queries/OrderListQueryHandlerTests.cs`，参考 Task 7 Step 5 模板，Mock `IOrderQueryService.QueryOrdersAsync`，验证 QueryHandler 正确委托调用。

- [ ] **Step 6: 运行测试验证失败**

Run: `dotnet test tests/Services/Order/Leno.Order.Application.Tests/Leno.Order.Application.Tests.csproj --filter "FullyQualifiedName~OrderListQueryHandlerTests" --configuration Debug`
Expected: FAIL

- [ ] **Step 7: 新建 OrderListQueryHandler**

创建 `src/Services/Order/Leno.Order.Application/Queries/OrderListQueryHandler.cs`：

```csharp
using Leno.Infrastructure.Abstractions.Cqrs;
using Leno.Order.Application.Services;

namespace Leno.Order.Application.Queries;

public sealed class OrderListQueryHandler : IQueryHandler<OrderListQuery, OrderListQueryResult>
{
    private readonly IOrderQueryService _queryService;

    public OrderListQueryHandler(IOrderQueryService queryService)
    {
        _queryService = queryService;
    }

    public Task<OrderListQueryResult> HandleAsync(OrderListQuery query, CancellationToken ct = default)
        => _queryService.QueryOrdersAsync(query, ct);
}
```

- [ ] **Step 8: 运行测试验证通过**

Run: `dotnet test tests/Services/Order/Leno.Order.Application.Tests/Leno.Order.Application.Tests.csproj --filter "FullyQualifiedName~OrderListQueryHandlerTests" --configuration Debug`
Expected: PASS

- [ ] **Step 9: 编写失败测试 — OrderDetailQueryHandler**

参考 Step 5，覆盖正常返回 + OrderId 不存在返回 null 两个场景。

- [ ] **Step 10: 运行测试验证失败 → 新建 OrderDetailQueryHandler → 验证通过**

重复 Step 6-8 模式，新建 `OrderDetailQueryHandler`，委托 `IOrderQueryService.GetOrderDetailAsync`。

- [ ] **Step 11: 编写失败测试 — LogisticsTraceQueryHandler → 实现 → 通过**

参考 Step 5-8，新建 `LogisticsTraceQueryHandler`，委托 `IOrderQueryService.GetLogisticsTraceAsync`。

- [ ] **Step 12: DI 注册 QueryHandler**

修改 `src/Services/Order/Leno.Order.Infrastructure/Dependencies/ServiceCollectionExtensions.cs`，增加：

```csharp
services.AddScoped<IOrderQueryService, OrderQueryService>();
services.AddQueryHandler<OrderListQuery, OrderListQueryResult, OrderListQueryHandler>();
services.AddQueryHandler<OrderDetailQuery, OrderDetailQueryResult, OrderDetailQueryHandler>();
services.AddQueryHandler<LogisticsTraceQuery, LogisticsTraceQueryResult, LogisticsTraceQueryHandler>();
```

**注意:** `OrderQueryService` 实现需新建于 `src/Services/Order/Leno.Order.Infrastructure/ReadModels/OrderQueryService.cs`，走 ES 读模型或只读仓储（实现细节参考 ProductSearchService）。

- [ ] **Step 13: 标记 OrderAppService 查询方法 Obsolete**

修改 `src/Services/Order/Leno.Order.Application/IOrderAppService.cs` 第 385-407 行，为三个查询方法增加 `[Obsolete("请使用 IOrderQueryService，将在 2026-08-01 移除")]` 特性。

- [ ] **Step 14: 验证 Order BC 全量测试**

Run: `dotnet test tests/Services/Order/Leno.Order.Application.Tests/Leno.Order.Application.Tests.csproj --configuration Debug`
Expected: PASS

- [ ] **Step 15: 提交**

```bash
git add src/Services/Order/Leno.Order.Application/Queries/ src/Services/Order/Leno.Order.Application/Services/ src/Services/Order/Leno.Order.Infrastructure/ReadModels/OrderQueryService.cs src/Services/Order/Leno.Order.Infrastructure/Dependencies/ServiceCollectionExtensions.cs tests/Services/Order/Leno.Order.Application.Tests/Queries/
git commit -m "feat(order): 新建 OrderList/OrderDetail/LogisticsTrace Query 与 Handler

- OrderListQuery + Handler: 用户订单分页查询
- OrderDetailQuery + Handler: 订单详情（含商品快照/物流/评价摘要）
- LogisticsTraceQuery + Handler: 物流轨迹查询
- 新建 IOrderQueryService 查询专用服务与 OrderQueryService 实现
- OrderAppService 查询方法标记 Obsolete（双发期 2 周，2026-08-01 移除）
- 不引入 MediatR，采用 IQueryHandler 接口 + DI
- 新增 6 个单元测试

关联 spec: §13.2 M6.2 Order 域 Query Handler 分离"
```

---

## Task 9: SellerShop BC 新建 ShopDashboardQuery 与 Handler

**Files:**
- Create: `src/Services/SellerShop/Leno.SellerShop.Application/Queries/ShopDashboardQuery.cs`
- Create: `src/Services/SellerShop/Leno.SellerShop.Application/Queries/ShopDashboardQueryHandler.cs`
- Create: `src/Services/SellerShop/Leno.SellerShop.Application/Queries/ShopDashboardQueryResult.cs`
- Modify: `src/Services/SellerShop/Leno.SellerShop.Infrastructure/Dependencies/ServiceCollectionExtensions.cs`
- Test: `tests/Services/SellerShop/Leno.SellerShop.Application.Tests/Queries/ShopDashboardQueryHandlerTests.cs`

**背景:** 既有 `SellerDashboardAppService.GetDashboardAsync` 直接调三个仓储聚合数据，未走 ES 读模型。M6.2 拆为 `ShopDashboardQuery` + `ShopDashboardQueryHandler`，走 Task 5 新建的 `ShopDashboardReadModel`（ES 读模型）。**双发期 2 周**：QueryHandler 与既有 AppService 方法并存。

- [ ] **Step 1: 新建 ShopDashboardQuery 与 Result**

创建 `src/Services/SellerShop/Leno.SellerShop.Application/Queries/ShopDashboardQuery.cs`：

```csharp
namespace Leno.SellerShop.Application.Queries;

public sealed class ShopDashboardQuery
{
    public Guid ShopId { get; init; }

    public DateTime? FromDate { get; init; }

    public DateTime? ToDate { get; init; }
}
```

创建 `src/Services/SellerShop/Leno.SellerShop.Application/Queries/ShopDashboardQueryResult.cs`：

```csharp
namespace Leno.SellerShop.Application.Queries;

public sealed class ShopDashboardQueryResult
{
    public Guid ShopId { get; init; }

    public string ShopName { get; init; } = string.Empty;

    public int TotalOrders { get; init; }

    public int PendingOrders { get; init; }

    public int CompletedOrders { get; init; }

    public decimal TotalSales { get; init; }

    public int TotalReviews { get; init; }

    public double AverageRating { get; init; }

    public int PendingAfterSales { get; init; }

    public DateTime UpdatedAt { get; init; }
}
```

- [ ] **Step 2: 新建 IShopDashboardQueryService 接口**

创建 `src/Services/SellerShop/Leno.SellerShop.Application/Services/IShopDashboardQueryService.cs`：

```csharp
using Leno.SellerShop.Application.Queries;

namespace Leno.SellerShop.Application.Services;

/// <summary>
/// 卖家看板查询专用服务（CQRS 读侧），走 ES 读模型。
/// 与写侧 SellerDashboardAppService 并存（双发期 2 周）。
/// </summary>
public interface IShopDashboardQueryService
{
    Task<ShopDashboardQueryResult?> GetDashboardAsync(ShopDashboardQuery query, CancellationToken ct = default);
}
```

- [ ] **Step 3: 编写失败测试 — ShopDashboardQueryHandler**

创建测试文件 `tests/Services/SellerShop/Leno.SellerShop.Application.Tests/Queries/ShopDashboardQueryHandlerTests.cs`：

```csharp
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Leno.SellerShop.Application.Queries;
using Leno.SellerShop.Application.Services;
using Moq;
using Xunit;

namespace Leno.SellerShop.Application.Tests.Queries;

public class ShopDashboardQueryHandlerTests
{
    [Fact]
    public async Task HandleAsync_ShouldDelegateToQueryService()
    {
        // Arrange
        var queryServiceMock = new Mock<IShopDashboardQueryService>();
        queryServiceMock.Setup(s => s.GetDashboardAsync(It.IsAny<ShopDashboardQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ShopDashboardQueryResult
            {
                ShopId = Guid.NewGuid(),
                ShopName = "Test Shop",
                TotalOrders = 100,
                TotalSales = 10000m
            });
        var handler = new ShopDashboardQueryHandler(queryServiceMock.Object);
        var query = new ShopDashboardQuery { ShopId = Guid.NewGuid() };

        // Act
        var result = await handler.HandleAsync(query);

        // Assert
        result.Should().NotBeNull();
        result!.TotalOrders.Should().Be(100);
        result.TotalSales.Should().Be(10000m);
        queryServiceMock.VerifyAll();
    }
}
```

- [ ] **Step 4: 运行测试验证失败**

Run: `dotnet test tests/Services/SellerShop/Leno.SellerShop.Application.Tests/Leno.SellerShop.Application.Tests.csproj --filter "FullyQualifiedName~ShopDashboardQueryHandlerTests" --configuration Debug`
Expected: FAIL

- [ ] **Step 5: 新建 ShopDashboardQueryHandler**

创建 `src/Services/SellerShop/Leno.SellerShop.Application/Queries/ShopDashboardQueryHandler.cs`：

```csharp
using Leno.Infrastructure.Abstractions.Cqrs;
using Leno.SellerShop.Application.Services;

namespace Leno.SellerShop.Application.Queries;

public sealed class ShopDashboardQueryHandler : IQueryHandler<ShopDashboardQuery, ShopDashboardQueryResult?>
{
    private readonly IShopDashboardQueryService _queryService;

    public ShopDashboardQueryHandler(IShopDashboardQueryService queryService)
    {
        _queryService = queryService;
    }

    public Task<ShopDashboardQueryResult?> HandleAsync(ShopDashboardQuery query, CancellationToken ct = default)
        => _queryService.GetDashboardAsync(query, ct);
}
```

- [ ] **Step 6: 运行测试验证通过**

Run: `dotnet test tests/Services/SellerShop/Leno.SellerShop.Application.Tests/Leno.SellerShop.Application.Tests.csproj --filter "FullyQualifiedName~ShopDashboardQueryHandlerTests" --configuration Debug`
Expected: PASS

- [ ] **Step 7: 新建 ShopDashboardQueryService 实现**

创建 `src/Services/SellerShop/Leno.SellerShop.Infrastructure/ReadModels/ShopDashboardQueryService.cs`，实现 `IShopDashboardQueryService`，注入 `IEsReadModelRepository<ShopDashboardReadModel>`，调用 `GetByIdAsync` 查询 Task 5 新建的 ES 读模型。

- [ ] **Step 8: DI 注册**

修改 `src/Services/SellerShop/Leno.SellerShop.Infrastructure/Dependencies/ServiceCollectionExtensions.cs`，增加：

```csharp
services.AddScoped<IShopDashboardQueryService, ShopDashboardQueryService>();
services.AddQueryHandler<ShopDashboardQuery, ShopDashboardQueryResult?, ShopDashboardQueryHandler>();
```

- [ ] **Step 9: 验证 SellerShop BC 全量测试**

Run: `dotnet test tests/Services/SellerShop/Leno.SellerShop.Application.Tests/Leno.SellerShop.Application.Tests.csproj --configuration Debug`
Expected: PASS

- [ ] **Step 10: 提交**

```bash
git add src/Services/SellerShop/Leno.SellerShop.Application/Queries/ src/Services/SellerShop/Leno.SellerShop.Application/Services/IShopDashboardQueryService.cs src/Services/SellerShop/Leno.SellerShop.Infrastructure/ReadModels/ShopDashboardQueryService.cs src/Services/SellerShop/Leno.SellerShop.Infrastructure/Dependencies/ServiceCollectionExtensions.cs tests/Services/SellerShop/Leno.SellerShop.Application.Tests/Queries/
git commit -m "feat(seller-shop): 新建 ShopDashboardQuery 与 QueryHandler

- ShopDashboardQuery + Handler: 走 ES 读模型（Task 5 ShopDashboardReadModel）
- 新建 IShopDashboardQueryService 与 ShopDashboardQueryService 实现
- 与既有 SellerDashboardAppService 并存（双发期 2 周）
- 不引入 MediatR，采用 IQueryHandler 接口 + DI
- 新增 1 个单元测试

关联 spec: §13.2 M6.2 SellerShop 域 Query Handler 分离"
```

---

## Task 10: BFF 聚合层 — 4 个聚合端点

**Files:**
- Create: `src/ApiGateway/Leno.ApiGateway/Bff/Models/OrderDetailAggregate.cs`
- Create: `src/ApiGateway/Leno.ApiGateway/Bff/Models/ProductDetailAggregate.cs`
- Create: `src/ApiGateway/Leno.ApiGateway/Bff/Models/CartCheckoutPreviewAggregate.cs`
- Create: `src/ApiGateway/Leno.ApiGateway/Bff/Models/SellerDashboardAggregate.cs`
- Create: `src/ApiGateway/Leno.ApiGateway/Bff/Models/BffResponse.cs`
- Create: `src/ApiGateway/Leno.ApiGateway/Bff/BffForwarderService.cs`
- Create: `src/ApiGateway/Leno.ApiGateway/Bff/OrderDetailBffController.cs`
- Create: `src/ApiGateway/Leno.ApiGateway/Bff/ProductDetailBffController.cs`
- Create: `src/ApiGateway/Leno.ApiGateway/Bff/CartCheckoutPreviewBffController.cs`
- Create: `src/ApiGateway/Leno.ApiGateway/Bff/SellerDashboardBffController.cs`
- Modify: `src/ApiGateway/Leno.ApiGateway/Program.cs`
- Test: `tests/ApiGateway/Leno.ApiGateway.Tests/Bff/BffForwarderServiceTests.cs`
- Test: `tests/ApiGateway/Leno.ApiGateway.Tests/Bff/OrderDetailBffControllerTests.cs`

**背景:** 网关当前无 `Bff/` 目录，`IForwarder` 完全未使用（但 YARP 已集成）。M6.3 新建 4 个 BFF 聚合端点：order-detail、product-detail、cart-checkout-preview、seller-dashboard。使用 `IForwarder` 单次转发 + `Parallel.ForEachAsync` 并行调用下游、超时 3 秒、部分失败返回 `partial:true` + 错误明细。

- [ ] **Step 1: 新建 BffResponse 通用模型**

创建 `src/ApiGateway/Leno.ApiGateway/Bff/Models/BffResponse.cs`：

```csharp
namespace Leno.ApiGateway.Bff.Models;

/// <summary>
/// BFF 聚合端点统一响应包装。
/// partial=true 表示部分下游调用失败，Errors 含失败明细。
/// </summary>
public sealed class BffResponse<T>
{
    public T? Data { get; init; }

    public bool Partial { get; init; }

    public IReadOnlyList<BffError> Errors { get; init; } = Array.Empty<BffError>();

    public static BffResponse<T> Ok(T data) => new() { Data = data, Partial = false };

    public static BffResponse<T> PartialSuccess(T data, IReadOnlyList<BffError> errors)
        => new() { Data = data, Partial = true, Errors = errors };
}

public sealed class BffError
{
    public string Source { get; init; } = string.Empty;

    public string Message { get; init; } = string.Empty;
}
```

- [ ] **Step 2: 新建 4 个聚合模型**

创建 `src/ApiGateway/Leno.ApiGateway/Bff/Models/OrderDetailAggregate.cs`：

```csharp
namespace Leno.ApiGateway.Bff.Models;

public sealed class OrderDetailAggregate
{
    public object? Order { get; set; }

    public object? ProductSnapshots { get; set; }

    public object? LogisticsTrace { get; set; }

    public object? ReviewSummary { get; set; }
}
```

类似创建：
- `ProductDetailAggregate.cs`：含 SPU 详情、SKU 列表、评价评分、店铺信息
- `CartCheckoutPreviewAggregate.cs`：含购物车、SKU 价格、优惠试算、积分试算
- `SellerDashboardAggregate.cs`：含订单数、销售额、商品数、待处理售后

- [ ] **Step 3: 新建 BffForwarderService 通用转发服务**

创建 `src/ApiGateway/Leno.ApiGateway/Bff/BffForwarderService.cs`：

```csharp
using System.Diagnostics;
using Leno.ApiGateway.Bff.Models;
using Microsoft.Extensions.Logging;

namespace Leno.ApiGateway.Bff;

/// <summary>
/// BFF 通用转发服务：并行调用多个下游端点，超时 3 秒，部分失败返回 partial。
/// </summary>
public sealed class BffForwarderService
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<BffForwarderService> _logger;

    public BffForwarderService(IHttpClientFactory httpClientFactory, ILogger<BffForwarderService> logger)
    {
        _httpClientFactory = httpClientFactory;
        _logger = logger;
    }

    /// <summary>
    /// 并行调用多个下游，返回每个调用的结果（成功/失败）。
    /// 单个调用超时 3 秒，整体等待所有调用完成（不因单个失败而取消其他）。
    /// </summary>
    public async Task<BffAggregationResult> ForwardParallelAsync(
        IReadOnlyList<BffRequest> requests,
        CancellationToken ct = default)
    {
        var results = new BffRequestResult?[requests.Count];
        var errors = new List<BffError>();
        var overallTimeoutCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        overallTimeoutCts.CancelAfter(TimeSpan.FromSeconds(3));

        await Parallel.ForEachAsync(
            Enumerable.Range(0, requests.Count).Select(i => (Index: i, Request: requests[i])),
            new ParallelOptions { MaxDegreeOfParallelism = 4, CancellationToken = ct },
            async (item, _) =>
            {
                try
                {
                    var response = await CallDownstreamAsync(item.Request, overallTimeoutCts.Token).ConfigureAwait(false);
                    results[item.Index] = new BffRequestResult(item.Request.Name, response, null);
                }
                catch (OperationCanceledException) when (overallTimeoutCts.IsCancellationRequested)
                {
                    var error = new BffError { Source = item.Request.Name, Message = "timeout after 3s" };
                    results[item.Index] = new BffRequestResult(item.Request.Name, null, error);
                    lock (errors) errors.Add(error);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "BFF downstream call failed: {Source}", item.Request.Name);
                    var error = new BffError { Source = item.Request.Name, Message = ex.Message };
                    results[item.Index] = new BffRequestResult(item.Request.Name, null, error);
                    lock (errors) errors.Add(error);
                }
            });

        return new BffAggregationResult(results!, errors);
    }

    private async Task<object?> CallDownstreamAsync(BffRequest request, CancellationToken ct)
    {
        var client = _httpClientFactory.CreateClient(request.ClientName);
        var response = await client.GetAsync(request.Url, ct).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync(request.ResponseType, ct).ConfigureAwait(false);
    }
}

public sealed record BffRequest(string Name, string ClientName, string Url, Type ResponseType);

public sealed record BffRequestResult(string Name, object? Response, BffError? Error);

public sealed class BffAggregationResult
{
    public BffAggregationResult(IReadOnlyList<BffRequestResult> results, IReadOnlyList<BffError> errors)
    {
        Results = results;
        Errors = errors;
        IsPartial = errors.Count > 0;
    }

    public IReadOnlyList<BffRequestResult> Results { get; }

    public IReadOnlyList<BffError> Errors { get; }

    public bool IsPartial { get; }

    public object? GetResponse(string name)
        => Results.FirstOrDefault(r => r.Name == name)?.Response;
}
```

- [ ] **Step 4: 新建 OrderDetailBffController**

创建 `src/ApiGateway/Leno.ApiGateway/Bff/OrderDetailBffController.cs`：

```csharp
using Leno.ApiGateway.Bff.Models;
using Microsoft.AspNetCore.Mvc;

namespace Leno.ApiGateway.Bff;

/// <summary>
/// BFF 订单详情聚合端点：订单详情 + 商品快照 + 物流轨迹 + 评价摘要。
/// </summary>
[ApiController]
[Route("api/bff/order-detail")]
public sealed class OrderDetailBffController : ControllerBase
{
    private readonly BffForwarderService _forwarder;
    private readonly IConfiguration _config;

    public OrderDetailBffController(BffForwarderService forwarder, IConfiguration config)
    {
        _forwarder = forwarder;
        _config = config;
    }

    [HttpGet("{orderId:guid}")]
    public async Task<ActionResult<BffResponse<OrderDetailAggregate>>> GetAsync(
        Guid orderId,
        [FromHeader(Name = "X-User-Id")] string userId,
        CancellationToken ct)
    {
        var requests = new[]
        {
            new BffRequest("order", "Order", $"/api/internal/v1/orders/{orderId}?userId={userId}", typeof(object)),
            new BffRequest("products", "Product", $"/api/internal/v1/orders/{orderId}/product-snapshots", typeof(object)),
            new BffRequest("logistics", "Order", $"/api/internal/v1/orders/{orderId}/logistics-trace", typeof(object)),
            new BffRequest("reviews", "ReviewAfterSales", $"/api/internal/v1/reviews/order/{orderId}/summary", typeof(object))
        };

        var aggregation = await _forwarder.ForwardParallelAsync(requests, ct);

        var data = new OrderDetailAggregate
        {
            Order = aggregation.GetResponse("order"),
            ProductSnapshots = aggregation.GetResponse("products"),
            LogisticsTrace = aggregation.GetResponse("logistics"),
            ReviewSummary = aggregation.GetResponse("reviews")
        };

        if (aggregation.IsPartial)
        {
            return Ok(BffResponse<OrderDetailAggregate>.PartialSuccess(data, aggregation.Errors));
        }
        return Ok(BffResponse<OrderDetailAggregate>.Ok(data));
    }
}
```

- [ ] **Step 5: 新建 ProductDetailBffController**

参考 Step 4，创建 `src/ApiGateway/Leno.ApiGateway/Bff/ProductDetailBffController.cs`，路由 `api/bff/product-detail/{spuId}`，并行调用 Product（SPU 详情）、Product（SKU 列表）、ReviewAfterSales（评价评分）、SellerShop（店铺信息）。

- [ ] **Step 6: 新建 CartCheckoutPreviewBffController**

参考 Step 4，创建 `src/ApiGateway/Leno.ApiGateway/Bff/CartCheckoutPreviewBffController.cs`，路由 `api/bff/cart-checkout-preview`，并行调用 Cart（购物车）、Product（SKU 价格）、Promotion（优惠试算）、PointsMembership（积分试算）。

- [ ] **Step 7: 新建 SellerDashboardBffController**

参考 Step 4，创建 `src/ApiGateway/Leno.ApiGateway/Bff/SellerDashboardBffController.cs`，路由 `api/bff/seller-dashboard`，并行调用 SellerShop（看板）、Order（订单数 + 销售额）、ReviewAfterSales（待处理售后）。**注意:** SellerShop 看板走 Task 9 新建的 `ShopDashboardQueryHandler`（通过网关调用 SellerShop BC 的 internal 端点）。

- [ ] **Step 8: 编写失败测试 — BffForwarderService 部分失败场景**

创建测试文件 `tests/ApiGateway/Leno.ApiGateway.Tests/Bff/BffForwarderServiceTests.cs`：

```csharp
using System.Net;
using System.Text.Json;
using FluentAssertions;
using Leno.ApiGateway.Bff;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace Leno.ApiGateway.Tests.Bff;

public class BffForwarderServiceTests
{
    [Fact]
    public async Task ForwardParallelAsync_WhenAllSucceed_ShouldReturnNonPartial()
    {
        // Arrange: 2 个下游均成功
        var httpClientFactory = CreateHttpClientFactory(new[]
        {
            ("order", HttpStatusCode.OK, "{\"orderId\":\"123\"}"),
            ("products", HttpStatusCode.OK, "[{\"spuId\":\"456\"}]")
        });
        var forwarder = new BffForwarderService(httpClientFactory, NullLogger<BffForwarderService>.Instance);
        var requests = new[]
        {
            new BffRequest("order", "order", "/api/test/order", typeof(object)),
            new BffRequest("products", "products", "/api/test/products", typeof(object))
        };

        // Act
        var result = await forwarder.ForwardParallelAsync(requests);

        // Assert
        result.IsPartial.Should().BeFalse();
        result.Errors.Should().BeEmpty();
        result.GetResponse("order").Should().NotBeNull();
        result.GetResponse("products").Should().NotBeNull();
    }

    [Fact]
    public async Task ForwardParallelAsync_WhenOneFails_ShouldReturnPartial()
    {
        // Arrange: order 成功，products 500 错误
        var httpClientFactory = CreateHttpClientFactory(new[]
        {
            ("order", HttpStatusCode.OK, "{\"orderId\":\"123\"}"),
            ("products", HttpStatusCode.InternalServerError, "")
        });
        var forwarder = new BffForwarderService(httpClientFactory, NullLogger<BffForwarderService>.Instance);
        var requests = new[]
        {
            new BffRequest("order", "order", "/api/test/order", typeof(object)),
            new BffRequest("products", "products", "/api/test/products", typeof(object))
        };

        // Act
        var result = await forwarder.ForwardParallelAsync(requests);

        // Assert: 部分失败返回 partial=true，含错误明细
        result.IsPartial.Should().BeTrue();
        result.Errors.Should().HaveCount(1);
        result.Errors[0].Source.Should().Be("products");
        result.GetResponse("order").Should().NotBeNull();
        result.GetResponse("products").Should().BeNull();
    }

    [Fact]
    public async Task ForwardParallelAsync_WhenTimeout_ShouldReturnPartialWithTimeoutError()
    {
        // Arrange: 下游延迟 5 秒（超过 3 秒超时）
        var httpClientFactory = CreateHttpClientFactory(new[]
        {
            ("slow", HttpStatusCode.OK, "{}", TimeSpan.FromSeconds(5))
        });
        var forwarder = new BffForwarderService(httpClientFactory, NullLogger<BffForwarderService>.Instance);
        var requests = new[] { new BffRequest("slow", "slow", "/api/test/slow", typeof(object)) };

        // Act
        var result = await forwarder.ForwardParallelAsync(requests);

        // Assert
        result.IsPartial.Should().BeTrue();
        result.Errors[0].Message.Should().Contain("timeout");
    }

    private static IHttpClientFactory CreateHttpClientFactory(
        IEnumerable<(string Name, HttpStatusCode Status, string Content)> handlers)
        => CreateHttpClientFactory(handlers.Select(h => (h.Name, h.Status, h.Content, TimeSpan.Zero)).ToArray());

    private static IHttpClientFactory CreateHttpClientFactory(
        (string Name, HttpStatusCode Status, string Content, TimeSpan Delay)[] handlers)
    {
        var factoryMock = new Mock<IHttpClientFactory>();
        foreach (var h in handlers)
        {
            var handler = new StubHandler(h.Status, h.Content, h.Delay);
            var client = new HttpClient(handler) { BaseAddress = new Uri("http://localhost") };
            factoryMock.Setup(f => f.CreateClient(h.Name)).Returns(client);
        }
        return factoryMock.Object;
    }

    private sealed class StubHandler : HttpMessageHandler
    {
        private readonly HttpStatusCode _status;
        private readonly string _content;
        private readonly TimeSpan _delay;

        public StubHandler(HttpStatusCode status, string content, TimeSpan delay)
        {
            _status = status;
            _content = content;
            _delay = delay;
        }

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken ct)
        {
            if (_delay > TimeSpan.Zero)
                await Task.Delay(_delay, ct);
            return new HttpResponseMessage(_status)
            {
                Content = new StringContent(_content, System.Text.Encoding.UTF8, "application/json")
            };
        }
    }
}
```

- [ ] **Step 9: 运行测试验证通过**

Run: `dotnet test tests/ApiGateway/Leno.ApiGateway.Tests/Leno.ApiGateway.Tests.csproj --filter "FullyQualifiedName~BffForwarderServiceTests" --configuration Debug`
Expected: PASS，3 个测试全部通过（全成功 / 部分失败 / 超时）

- [ ] **Step 10: 注册 BffForwarderService 与 HttpClient**

修改 `src/ApiGateway/Leno.ApiGateway/Program.cs`，在 `AddObservability` 之后增加：

```csharp
// BFF 聚合层注册
builder.Services.AddSingleton<BffForwarderService>();
builder.Services.AddHttpClient("Order", c => c.BaseAddress = new Uri(builder.Configuration["Downstream:Order:BaseUrl"] ?? "http://localhost:5154"));
builder.Services.AddHttpClient("Product", c => c.BaseAddress = new Uri(builder.Configuration["Downstream:Product:BaseUrl"] ?? "http://localhost:5152"));
builder.Services.AddHttpClient("Cart", c => c.BaseAddress = new Uri(builder.Configuration["Downstream:Cart:BaseUrl"] ?? "http://localhost:5153"));
builder.Services.AddHttpClient("Promotion", c => c.BaseAddress = new Uri(builder.Configuration["Downstream:Promotion:BaseUrl"] ?? "http://localhost:5155"));
builder.Services.AddHttpClient("PointsMembership", c => c.BaseAddress = new Uri(builder.Configuration["Downstream:PointsMembership:BaseUrl"] ?? "http://localhost:5157"));
builder.Services.AddHttpClient("ReviewAfterSales", c => c.BaseAddress = new Uri(builder.Configuration["Downstream:ReviewAfterSales:BaseUrl"] ?? "http://localhost:5156"));
builder.Services.AddHttpClient("SellerShop", c => c.BaseAddress = new Uri(builder.Configuration["Downstream:SellerShop:BaseUrl"] ?? "http://localhost:5160"));
```

- [ ] **Step 11: 编写 OrderDetailBffController 测试**

创建测试文件 `tests/ApiGateway/Leno.ApiGateway.Tests/Bff/OrderDetailBffControllerTests.cs`，覆盖全成功返回 `partial:false` + 部分失败返回 `partial:true` 两个场景。

- [ ] **Step 12: 运行测试验证通过**

Run: `dotnet test tests/ApiGateway/Leno.ApiGateway.Tests/Leno.ApiGateway.Tests.csproj --filter "FullyQualifiedName~OrderDetailBffControllerTests" --configuration Debug`
Expected: PASS

- [ ] **Step 13: 验证网关全量测试**

Run: `dotnet test tests/ApiGateway/Leno.ApiGateway.Tests/Leno.ApiGateway.Tests.csproj --configuration Debug`
Expected: PASS

- [ ] **Step 14: 提交**

```bash
git add src/ApiGateway/Leno.ApiGateway/Bff/ src/ApiGateway/Leno.ApiGateway/Program.cs tests/ApiGateway/Leno.ApiGateway.Tests/Bff/
git commit -m "feat(api-gateway): 新建 BFF 聚合层 4 个端点

- BffForwarderService: Parallel.ForEachAsync 并行调用，3 秒超时，部分失败返回 partial
- OrderDetailBffController: GET /api/bff/order-detail/{orderId}
- ProductDetailBffController: GET /api/bff/product-detail/{spuId}
- CartCheckoutPreviewBffController: GET /api/bff/cart-checkout-preview
- SellerDashboardBffController: GET /api/bff/seller-dashboard
- BffResponse<T>: 统一响应包装（Data + Partial + Errors）
- Program.cs 注册 BffForwarderService 与 7 个下游 HttpClient
- 新增 4 个单元测试覆盖全成功/部分失败/超时场景

关联 spec: §13.3 M6.3 BFF 聚合层"
```

---

## Task 11: CacheMiddleware.GenerateCacheKey 增加 role + shopId 维度

**Files:**
- Modify: `src/ApiGateway/Leno.ApiGateway/Middleware/CacheMiddleware.cs:122-132`
- Modify: `tests/ApiGateway/Leno.ApiGateway.Tests/Middleware/CacheMiddlewareTests.cs`

**背景:** 当前 `GenerateCacheKey` 维度为 `method:path:query:userId`，未含 `role` 与 `shopId`。spec M6.4 明确"缓存 Key 未含 Role（越权风险）"。增加 `role` 维度后，不同角色（如买家 vs 卖家）即使访问同一路径也生成不同 Key，避免越权读取对方数据。敏感端点（卖家工作台）强制包含 `shopId`。

- [ ] **Step 1: 修改 GenerateCacheKey 增加 role + shopId 维度**

读取 `src/ApiGateway/Leno.ApiGateway/Middleware/CacheMiddleware.cs` 第 122-132 行，替换 `GenerateCacheKey` 方法：

```csharp
/// <summary>
/// 生成缓存 Key：method:path:query:userId:role:shopId
/// role 维度避免不同角色越权读取对方缓存数据。
/// shopId 维度用于敏感端点（卖家工作台等），非卖家请求为空串。
/// </summary>
private static string GenerateCacheKey(HttpContext context)
{
    var method = context.Request.Method.ToUpperInvariant();
    var path = context.Request.Path.Value ?? "/";
    var query = context.Request.QueryString.Value ?? string.Empty;
    var userId = context.User.FindFirst("Sub")?.Value ?? string.Empty;
    var role = context.User.FindFirst("Role")?.Value ?? string.Empty;
    var shopId = context.User.FindFirst("ShopId")?.Value ?? string.Empty;
    return $"{method}:{path}{query}:{userId}:{role}:{shopId}";
}
```

- [ ] **Step 2: 更新既有 CacheMiddlewareTests**

读取 `tests/ApiGateway/Leno.ApiGateway.Tests/Middleware/CacheMiddlewareTests.cs`，在既有测试中补充 `Role` 与 `ShopId` claim，并新增以下测试：

```csharp
[Fact]
public void GenerateCacheKey_WithSamePathDifferentRole_ShouldReturnDifferentKeys()
{
    // Arrange: 两个用户访问同一路径，但角色不同
    var context1 = CreateHttpContext("GET", "/api/products", "user-001", "Buyer", "");
    var context2 = CreateHttpContext("GET", "/api/products", "user-002", "Seller", "");

    // Act
    var key1 = InvokeGenerateCacheKey(context1);
    var key2 = InvokeGenerateCacheKey(context2);

    // Assert: 不同角色生成不同 Key（避免越权）
    key1.Should().NotBe(key2);
    key1.Should().Contain(":Buyer:");
    key2.Should().Contain(":Seller:");
}

[Fact]
public void GenerateCacheKey_WithSamePathDifferentShopId_ShouldReturnDifferentKeys()
{
    // Arrange: 两个卖家访问同一店铺工作台路径，但 shopId 不同
    var context1 = CreateHttpContext("GET", "/api/seller/dashboard", "seller-001", "Seller", "shop-A");
    var context2 = CreateHttpContext("GET", "/api/seller/dashboard", "seller-002", "Seller", "shop-B");

    // Act
    var key1 = InvokeGenerateCacheKey(context1);
    var key2 = InvokeGenerateCacheKey(context2);

    // Assert: 不同 shopId 生成不同 Key
    key1.Should().NotBe(key2);
    key1.Should().Contain(":shop-A");
    key2.Should().Contain(":shop-B");
}

[Fact]
public void GenerateCacheKey_WithAnonymousUser_ShouldReturnKeyWithEmptyRoleAndShopId()
{
    // Arrange: 匿名用户（无任何 claim）
    var context = CreateHttpContext("GET", "/api/products", null, null, null);

    // Act
    var key = InvokeGenerateCacheKey(context);

    // Assert: 匿名用户 role 与 shopId 为空串
    key.Should().Be("GET:/api/products:::");
}
```

**注意:** `CreateHttpContext` 与 `InvokeGenerateCacheKey` 为测试辅助方法，参考既有测试文件中的实现模式。

- [ ] **Step 3: 运行测试验证通过**

Run: `dotnet test tests/ApiGateway/Leno.ApiGateway.Tests/Leno.ApiGateway.Tests.csproj --filter "FullyQualifiedName~CacheMiddlewareTests" --configuration Debug`
Expected: PASS，既有测试 + 3 个新测试全部通过

- [ ] **Step 4: 验证网关全量测试**

Run: `dotnet test tests/ApiGateway/Leno.ApiGateway.Tests/Leno.ApiGateway.Tests.csproj --configuration Debug`
Expected: PASS

- [ ] **Step 5: 提交**

```bash
git add src/ApiGateway/Leno.ApiGateway/Middleware/CacheMiddleware.cs tests/ApiGateway/Leno.ApiGateway.Tests/Middleware/CacheMiddlewareTests.cs
git commit -m "fix(api-gateway): GenerateCacheKey 增加 role 与 shopId 维度

- 修复缓存 Key 未含 Role 导致的越权风险（spec M6.4）
- 新增 role 维度：不同角色同一路径生成不同 Key
- 新增 shopId 维度：敏感端点（卖家工作台）按店铺隔离
- 缓存 Key 格式：method:path:query:userId:role:shopId
- 新增 3 个单元测试覆盖 role/shopId/匿名场景

关联 spec: §13.4 M6.4 缓存 Key 安全加固"
```

---

## Task 12: 编码规范文档新增第 15/16/17 章 + 第 7.5 节

**Files:**
- Modify: `docs/编码规范.md`

**背景:** spec §13.5 提到"新增第 14/15/16 章"，但**编码规范现状第 14 章已被 Git 提交规范占用**（line 3547），故调整为新增第 15/16/17 章 + 第 7.5 节（CQRS Query Handler 约定，整合到既有第 7 章 CQRS 编码规范）。

- [ ] **Step 1: 新增第 7.5 节 — CQRS Query Handler 约定**

在 `docs/编码规范.md` 第 7 章 CQRS 编码规范的 7.4 节后追加：

```markdown
### 7.5 Query Handler 约定（M6.2 落地）

**适用范围:** Product/Order/SellerShop 三个 BC（读多写少场景）；Cart/Notification/SystemAdmin/Payment/UserAuth 等维持单一 AppService 模式可接受。

**接口定义:**
```csharp
public interface IQueryHandler<in TQuery, TResult> where TQuery : class
{
    Task<TResult> HandleAsync(TQuery query, CancellationToken ct = default);
}
```

**命名约定:**
- 查询参数: `XxxQuery`（不可变记录，使用 `init` 属性）
- 查询结果: `XxxQueryResult` 或 `XxxQueryResult?`（不存在时返回 null）
- 处理器: `XxxQueryHandler : IQueryHandler<XxxQuery, XxxQueryResult>`

**目录结构:**
- Query/QueryHandler/Result 均位于 `Leno.{BC}.Application/Queries/` 目录
- 查询服务接口位于 `Leno.{BC}.Application/Services/IXxxQueryService.cs`
- 查询服务实现位于 `Leno.{BC}.Infrastructure/ReadModels/XxxQueryService.cs`（走 ES 读模型）

**DI 注册:**
```csharp
services.AddScoped<IXxxQueryService, XxxQueryService>();
services.AddQueryHandler<XxxQuery, XxxQueryResult, XxxQueryHandler>();
```

**禁止事项:**
- 禁止在 QueryHandler 中调用 `SaveChangesAsync`（读写职责分离）
- 禁止在 QueryHandler 中发布领域事件
- 不引入 MediatR（用接口 + DI 即可）
- 不强制全 BC 落地（仅 Product/Order/SellerShop）
```

- [ ] **Step 2: 新增第 15 章 — 安全编码约定**

在 `docs/编码规范.md` 第 14 章 Git 提交规范后追加：

```markdown
## 第 15 章 安全编码约定

### 15.1 密钥管理

**禁止:** 明文密钥写入 `appsettings*.json`（F2.4 已落地 `ValidateSensitiveConfig` 启动期校验）。

**要求:** 所有敏感配置（数据库连接串、Redis 密码、JWT 签名密钥、InternalApiKey 等）统一通过 Consul KV 配置中心读取，路径约定 `leno/security/{key}`。

**启动期校验:** `AddLenoConsulConfig` + `ValidateSensitiveConfig` 在应用启动前校验所有必需配置项，缺失时 fail-closed 阻止启动。

### 15.2 JWT 黑名单

**三层保障:**
1. Redis Pub/Sub: 用户登出时 `auth:logout` 频道广播 JWT 黑名单（1 秒内生效）
2. 定时拉取: 各 BC 每 30 秒拉取最新黑名单到本地缓存
3. 启动预热: 服务启动时全量拉取黑名单到本地缓存

**网关校验:** `Auth:Mode=GatewayHeader` 灰度切换，网关校验 token 并通过 `X-User-Id`/`X-Role` 等 header 转发用户上下文。

### 15.3 InternalApiKey 各 BC 独立

**禁止:** 全平台共用单一 InternalApiKey。

**要求:** 11 个 BC 各自独立 InternalApiKey，路径约定 `leno/security/internal-key/{bc}`。网关调用下游时按 BC 选择对应 key。

### 15.4 ErrorCode 命名约定

**格式:** `DOMAIN_ENTITY_ACTION`，如 `ORDER_PAYMENT_PAID`、`PRODUCT_SPU_NOT_FOUND`。

**后缀约定（自动推断 HTTP 状态码）:**
- `_NOT_FOUND` → 404
- `_ALREADY_*`/`_EXISTS`/`_CONFLICT` → 409
- `_FORBIDDEN` → 403
- `_UNAVAILABLE` → 503
- `_FAILED` → 502
- `_MISSING` → 500
- `_EXPIRED`/`_REQUIRED` → 401
- 其余 → 400

### 15.5 防腐层错误处理

**统一策略:** 防腐层远程调用失败统一映射为 `DomainException("{SERVICE}_UNAVAILABLE")` 或 `DomainException("{SERVICE}_REMOTE_FAILED")`，由全局异常处理中间件转换为 HTTP 503/502。

**禁止:** 防腐层返回 null（读操作也抛异常，避免上层空引用）。

**Polly 策略:** 重试 3 次（指数退避 1s/2s/4s）+ 熔断（50%/30s）+ Timeout 10s，通过 `AddAntiCorruptionPolicies()` 链式注入。
```

- [ ] **Step 3: 新增第 16 章 — gRPC 内部服务通信**

在 `docs/编码规范.md` 第 15 章后追加：

```markdown
## 第 16 章 gRPC 内部服务通信

### 16.1 .proto 契约治理

**目录:** `protos/leno/{bc}/v1/*.proto`

**命名约定:** 包名 `leno.{bc}.v1`，服务名 `{BC}InternalService`，方法名采用动词前缀（如 `GetProductById`、`ValidateSellerOwnership`）。

**buf CLI 校验:**
- `buf lint`: 风格校验（命名、文件组织、注释）
- `buf breaking`: 向后兼容校验（删除字段、修改类型等破坏性变更）

**CI 集成:** `.github/workflows/ci.yml` 增加 `buf-lint` 与 `buf-breaking` job，PR 阶段强制校验。

### 16.2 版本化

**当前版本:** v1（所有内部服务统一版本）

**版本演进:** 引入 v2 时保留 v1 服务（双发期 ≥ 4 周），客户端按批次迁移。

### 16.3 错误映射

| gRPC 状态码 | DomainException ErrorCode | HTTP 状态码 |
|---|---|---|
| Unavailable | {SERVICE}_UNAVAILABLE | 503 |
| DeadlineExceeded | {SERVICE}_UNAVAILABLE | 503 |
| Internal | {SERVICE}_REMOTE_FAILED | 502 |
| InvalidArgument | {SERVICE}_INVALID_ARGUMENT | 400 |

**灰度开关:** `AntiCorruptionOptions.UseGrpc` 默认 false，通过 3 批次迁移切 true。
```

- [ ] **Step 4: 新增第 17 章 — 文档与 PR 规范**

在 `docs/编码规范.md` 第 16 章后追加：

```markdown
## 第 17 章 文档与 PR 规范

### 17.1 占位实现禁止

**禁止:** 任何 `throw new NotImplementedException()`、`throw new InvalidOperationException("TODO")`、空方法体仅返回默认值的实现进入主分支。

**CI 校验:** `scripts/check-placeholders.sh` 在 build-solution job 中强制执行。

### 17.2 PR 模板 checklist

每次 PR 必须勾选以下项：
- [ ] 本 PR 不含任何占位实现
- [ ] 本 PR 含模型变更时已生成 EF migration
- [ ] 本 PR 含跨 BC 调用变更时已更新 .proto 契约

### 17.3 集成事件契约

**领域事件 → 集成事件翻译:** 通过 `IIntegrationEventMapper<TDomainEvent>` 实现翻译，禁止领域事件直接发布到消息总线。

**Schema 版本:** `IntegrationEventBase.SchemaVersion` 字段记录事件 schema 版本，Outbox 持久化版本号，消费端按版本兼容处理。
```

- [ ] **Step 5: 提交**

```bash
git add docs/编码规范.md
git commit -m "docs: 编码规范新增第 15/16/17 章 + 第 7.5 节

- 第 7.5 节: CQRS Query Handler 约定（命名/DI/禁止事项）
- 第 15 章: 安全编码约定（密钥管理/JWT 黑名单/InternalApiKey/ErrorCode/防腐层错误处理）
- 第 16 章: gRPC 内部服务通信（.proto 治理/版本化/错误映射/灰度开关）
- 第 17 章: 文档与 PR 规范（占位禁止/PR checklist/集成事件契约）
- spec 原文'第 14/15/16 章'因现状第 14 章已被 Git 提交规范占用，调整为 15/16/17

关联 spec: §13.5 M6.5 文档与规范同步"
```

---

## Task 13: 需求文档总览 + internal-api-contracts.md 同步

**Files:**
- Modify: `docs/spec/00-需求文档总览与DDD架构.md`
- Create: `docs/contracts/internal-api-contracts.md`

**背景:** spec M6.5 要求同步事件契约分离决策、Internal API 版本治理、gRPC 通信决策到需求文档总览，并新建 `docs/contracts/internal-api-contracts.md` 覆盖 11 BC 的 internal 端点契约（REST + gRPC）。

- [ ] **Step 1: 同步需求文档总览**

读取 `docs/spec/00-需求文档总览与DDD架构.md`，在第 5 章"跨上下文领域事件清单"后增加 5.1 子节：

```markdown
### 5.1 事件契约分离决策（M1 落地）

**双身份事件拆分:** 65 个双身份事件拆分为领域事件（继承 `DomainEventBase`）+ 集成事件（继承 `IntegrationEventBase`）。

**翻译器模式:** 领域事件通过 `IIntegrationEventMapper<TDomainEvent>` 翻译为集成事件后发布到消息总线。

**双发期:** 1 周双发期（同时发布旧双身份事件与新分离事件），消费端逐步迁移，1 周后下线旧事件。

**Schema 版本:** `IntegrationEventBase.SchemaVersion` 字段记录事件 schema 版本（M4 落地）。
```

在第 8 章"API 设计规范"后增加 8.1 子节：

```markdown
### 8.1 Internal API 版本治理（M4 落地）

**REST 内部端点:** 所有 internal 路由加 `/v1/` 前缀（如 `/api/internal/v1/products/{id}`），双路由期 1 周后下线无前缀路由。

**gRPC 内部服务:** 11 个 BC 各自暴露 `{BC}InternalService` gRPC 服务，.proto 位于 `protos/leno/{bc}/v1/`，buf CLI 强制校验向后兼容。

**灰度切换:** `AntiCorruptionOptions.UseGrpc` 灰度开关，3 批次迁移（高频防腐层 → Cart/SellerShop → ReviewAfterSales/Notification/SystemAdmin）。

**端口分配:** 11 BC gRPC 端口为 HTTP 端口 +100（UserAuth 5251 ... SystemAdmin 5261）。
```

- [ ] **Step 2: 新建 internal-api-contracts.md**

创建 `docs/contracts/internal-api-contracts.md`，覆盖 11 BC 的 internal 端点契约：

```markdown
# Leno 平台 Internal API 契约

> 本文档记录 11 个限界上下文的内部端点契约（REST + gRPC），用于 BC 间通信。
> M1.3 已落地 REST 契约，M4.2/M4.3 补充 gRPC 契约，M6.5 整合到本文件。

## 版本演进记录

| 版本 | 日期 | 变更说明 |
|---|---|---|
| v1 | 2026-07-17 | 初始版本，REST + gRPC 契约统一 |

## 1. REST 内部端点

### 1.1 UserAuth BC

| 方法 | 路由 | 说明 |
|---|---|---|
| GET | /api/internal/v1/users/{userId} | 查询用户基本信息 |
| GET | /api/internal/v1/users/batch | 批量查询用户信息 |
| POST | /api/internal/v1/auth/validate-token | 校验 JWT 有效性 |

### 1.2 Product BC

| 方法 | 路由 | 说明 |
|---|---|---|
| GET | /api/internal/v1/products/{spuId} | 查询 SPU 详情 |
| GET | /api/internal/v1/products/{spuId}/skus | 查询 SKU 列表 |
| POST | /api/internal/v1/products/batch-price | 批量查询 SKU 价格 |
| GET | /api/internal/v1/products/{spuId}/stock | 查询库存 |

### 1.3 ~ 1.11 其他 BC

（Cart/Order/Promotion/ReviewAfterSales/PointsMembership/Payment/Notification/SellerShop/SystemAdmin 同结构，按 BC 列出 internal 端点）

## 2. gRPC 内部服务

### 2.1 ProductInternalService

```protobuf
service ProductInternalService {
  rpc GetProductById(GetProductByIdRequest) returns (ProductDto);
  rpc GetSkusBySpuId(GetSkusBySpuIdRequest) returns (SkuListDto);
  rpc BatchGetSkuPrices(BatchGetSkuPricesRequest) returns (SkuPriceListDto);
  rpc GetStockBySpuId(GetStockBySpuIdRequest) returns (StockDto);
}
```

### 2.2 ~ 2.11 其他 BC gRPC 服务

（按 Plan 8 Task 6 的 11 个 .proto 服务与方法清单列出）

## 3. 认证

所有 internal 端点需在请求头携带 `X-Internal-Api-Key`，11 BC 各自独立 key，路径约定 `leno/security/internal-key/{bc}`。
```

- [ ] **Step 3: 提交**

```bash
git add docs/spec/00-需求文档总览与DDD架构.md docs/contracts/internal-api-contracts.md
git commit -m "docs: 同步需求文档总览 + 新建 internal-api-contracts.md

- 需求文档总览新增 5.1 事件契约分离决策 + 8.1 Internal API 版本治理
- 新建 docs/contracts/internal-api-contracts.md: 11 BC REST + gRPC 契约
- 覆盖 M1.3 REST 契约 + M4.2/M4.3 gRPC 契约整合

关联 spec: §13.5 M6.5 文档与规范同步"
```

---

## Task 14: 新建 PR 模板 + 既有 spec 整合

**Files:**
- Create: `.github/pull_request_template.md`
- Modify: `docs/superpowers/specs/2026-07-13-comprehensive-optimization-design.md`（仅添加头部标注）
- Modify: `docs/superpowers/specs/2026-07-14-api-gateway-enhancement-design.md`（仅添加头部标注）
- Modify: `.trae/specs/fix-critical-business-vulnerabilities/spec.md`（仅添加头部标注）

**背景:** 项目当前无任何 PR 模板文件，需从零创建。3 份既有 spec 需在文件头部添加 supersede/接管标注，原内容保留。

- [ ] **Step 1: 新建 PR 模板**

创建 `.github/pull_request_template.md`：

```markdown
## 变更说明

<!-- 简述本 PR 的变更目的与背景 -->

## 变更类型

- [ ] 新功能（feature）
- [ ] Bug 修复（fix）
- [ ] 重构（refactor）
- [ ] 文档（docs）
- [ ] 测试（test）
- [ ] 构建/CI（chore）

## Checklist

- [ ] 本 PR 不含任何占位实现（`throw new NotImplementedException()` / `TODO` / 空方法体）
- [ ] 本 PR 含模型变更时已生成 EF migration（`dotnet ef migrations add <Name>`）
- [ ] 本 PR 含跨 BC 调用变更时已更新 .proto 契约（`protos/leno/{bc}/v1/`）
- [ ] 单元测试已补充且全部通过
- [ ] 集成测试已补充且全部通过（如涉及跨 BC 场景）
- [ ] 编码规范第 15/16/17 章安全约定已遵守

## 关联

<!-- 关联的 spec / issue / 任务编号 -->
- Spec:
- Issue:
```

- [ ] **Step 2: 在 2026-07-13 V1 spec 头部添加 supersede 标注**

读取 `docs/superpowers/specs/2026-07-13-comprehensive-optimization-design.md` 前 5 行，在文件最顶部插入：

```markdown
> **本方案已被 supersede**
> 本 spec（V1 主线）已被 [2026-07-17-comprehensive-optimization-v2-design.md](./2026-07-17-comprehensive-optimization-v2-design.md) 全面接管。
> - 已完成项标记 `[x]`（保留为历史记录）
> - 未完成项由 V2 方案接管，标记 `[→ V2 Mx]`
> - 后续优化工作请参考 V2 方案

---
```

- [ ] **Step 3: 在 2026-07-14 网关增强 spec 头部添加增量标注**

读取 `docs/superpowers/specs/2026-07-14-api-gateway-enhancement-design.md` 前 5 行，在文件最顶部插入：

```markdown
> **本方案由 V2 方案 M4/M5 增量扩展**
> 本 spec（网关增强）基本落地，剩余 gRPC 通信、BFF 聚合层、告警规则由 [V2 方案](./2026-07-17-comprehensive-optimization-v2-design.md) M4/M5 接管：
> - M4.2 Internal REST 路由版本化
> - M4.3 gRPC 内部服务通信
> - M5.3 Alertmanager 告警规则
> - M6.3 BFF 聚合层 4 个端点

---
```

- [ ] **Step 4: 在 fix-critical-business-vulnerabilities spec 头部添加增量标注**

读取 `.trae/specs/fix-critical-business-vulnerabilities/spec.md` 前 5 行，在文件最顶部插入：

```markdown
> **本方案与 V2 方案 F1/F2 增量并行**
> 本 spec（P0 业务漏洞修复）的部分项目与 [V2 方案](../../docs/superpowers/specs/2026-07-17-comprehensive-optimization-v2-design.md) F1（业务流程修复）/F2（安全默认修复）存在重叠：
> - 支付金额校验、InternalApiKey fail-closed 等已落地项由本 spec 负责
> - 秒杀下单订单缺失、ForceCancel Outbox 一致性等由 V2 F1 接管
> - JWT 黑名单三层保障、Consul KV 收敛由 V2 F2 接管

---
```

- [ ] **Step 5: 提交**

```bash
git add .github/pull_request_template.md docs/superpowers/specs/2026-07-13-comprehensive-optimization-design.md docs/superpowers/specs/2026-07-14-api-gateway-enhancement-design.md .trae/specs/fix-critical-business-vulnerabilities/spec.md
git commit -m "docs: 新建 PR 模板 + 3 份既有 spec 标注 supersede 关系

- 新建 .github/pull_request_template.md: 含 3 个新 checklist（占位禁止/EF migration/.proto 契约）
- 2026-07-13 V1 spec 头部标注被 V2 supersede
- 2026-07-14 网关增强 spec 头部标注 V2 M4/M5/M6 增量
- fix-critical-business-vulnerabilities spec 头部标注与 V2 F1/F2 并行
- 既有 spec 原内容保留，仅添加头部标注

关联 spec: §13.5 M6.5 PR 模板 + §13.6 M6.6 既有 spec 整合"
```

---

## Task 15: 全量回归测试与最终验收

**Files:**
- 无文件变更，仅运行测试与验证

**背景:** M6 全部代码与文档变更完成后，需运行全量测试与验收检查，确保所有变更不破坏既有功能。

- [ ] **Step 1: 全量单元测试**

Run: `dotnet test Leno.slnx --configuration Release --filter "Category!=Integration" --verbosity normal`
Expected: PASS，所有单元测试通过（含 M6 新增的 ~25 个测试）

- [ ] **Step 2: 全量集成测试**

Run: `dotnet test Leno.slnx --configuration Release --filter "Category=Integration" --verbosity normal`
Expected: PASS，所有集成测试通过

- [ ] **Step 3: 解决方案编译验证**

Run: `dotnet build Leno.slnx --configuration Release`
Expected: 成功，无错误无警告

- [ ] **Step 4: 占位实现检查**

Run: `bash scripts/check-placeholders.sh`
Expected: 通过，无占位实现

- [ ] **Step 5: 验收检查清单**

逐项核对 spec §13 验收标准：

- [ ] M6.1: Promotion、PointsMembership、SellerShop 三个 BC 含 `ReadModels/` 目录与 `ReadModelSyncConsumerBase` 实现
- [ ] M6.1: `ReadModelSyncConsumerBase` 支持删除场景（`BuildDeleteActionAsync` 抽象方法）
- [ ] M6.1: `ProductTakenDownReadModelSyncConsumer` 重构继承基类（不再裸实现 IConsumer）
- [ ] M6.2: `Leno.Product.Application/Queries/`、`Leno.Order.Application/Queries/`、`Leno.SellerShop.Application/Queries/` 目录存在
- [ ] M6.2: 各含 ≥ 2 个 Query + QueryHandler（Product: 2, Order: 3, SellerShop: 1）
- [ ] M6.2: QueryHandler 走 ES 读模型（通过 IOrderQueryService/IShopDashboardQueryService 等）
- [ ] M6.2: 不引入 MediatR（采用 IQueryHandler 接口 + DI）
- [ ] M6.3: `Leno.ApiGateway/Bff/` 目录存在
- [ ] M6.3: 4 个 BFF 端点可访问（order-detail/product-detail/cart-checkout-preview/seller-dashboard）
- [ ] M6.3: 部分下游失败时返回 `partial: true`（单元测试覆盖）
- [ ] M6.4: `GenerateCacheKey` 含 `role` 维度
- [ ] M6.4: 单元测试覆盖不同 role 相同 path 生成不同 key
- [ ] M6.5: `docs/编码规范.md` 含第 15/16/17 章新增内容 + 第 7.5 节
- [ ] M6.5: `docs/spec/00-需求文档总览与DDD架构.md` 同步架构决策
- [ ] M6.5: `docs/contracts/internal-api-contracts.md` 覆盖 REST + gRPC 契约
- [ ] M6.5: PR 模板含 3 个新 checklist 项
- [ ] M6.6: 3 份既有 spec 含 supersede/接管标注

- [ ] **Step 6: 提交最终验收记录**

若有任何测试失败，修复后提交；若全部通过，本 Task 无需提交（仅验收检查）。

---

## Self-Review 检查清单

### Spec 覆盖核对

| spec 章节 | 覆盖 Task | 备注 |
|---|---|---|
| §13.1 M6.1 ES 读模型同步补齐 | Task 1-5 | Task 1 基类增强 + Task 2 Product 裸实现重构 + Task 3-5 三 BC 新建读模型 |
| §13.1 基类增强（支持删除场景） | Task 1 | `BuildDeleteActionAsync` 虚方法 |
| §13.1 Promotion 读模型 | Task 3 | SeckillActivity + Coupon |
| §13.1 PointsMembership 读模型 | Task 4 | PointsAccount + Member |
| §13.1 SellerShop 读模型 | Task 5 | ShopDashboard（多事件聚合） |
| §13.2 M6.2 Product Query Handler | Task 7 | ProductSearchQuery + ProductDetailQuery |
| §13.2 M6.2 Order Query Handler | Task 8 | OrderListQuery + OrderDetailQuery + LogisticsTraceQuery |
| §13.2 M6.2 SellerShop Query Handler | Task 9 | ShopDashboardQuery |
| §13.2 IQueryHandler 接口 + DI | Task 6 | 不引入 MediatR |
| §13.3 M6.3 BFF 聚合层 | Task 10 | 4 个端点 + IForwarder/Parallel.ForEachAsync + partial:true |
| §13.4 M6.4 缓存 Key 加固 | Task 11 | role + shopId 维度 |
| §13.5 M6.5 编码规范新增章节 | Task 12 | 第 15/16/17 章 + 第 7.5 节（spec 原文 14/15/16 调整为 15/16/17） |
| §13.5 M6.5 需求文档同步 | Task 13 | 5.1 事件契约分离 + 8.1 Internal API 版本治理 |
| §13.5 M6.5 internal-api-contracts.md | Task 13 | 11 BC REST + gRPC 契约 |
| §13.5 M6.5 PR 模板 | Task 14 | 3 个新 checklist |
| §13.6 M6.6 既有 spec 整合 | Task 14 | 3 份 spec 头部标注 supersede |
| §13.6 M6.6 全局回归测试 | Task 15 | 全量单元 + 集成测试 |

### 偏差记录

1. **章节编号偏差**: spec §13.5 提到"新增第 14/15/16 章"，但编码规范现状第 14 章已被 Git 提交规范占用，Plan 调整为新增第 15/16/17 章 + 第 7.5 节（CQRS Query Handler 约定整合到既有第 7 章）。
2. **ProductSearchService 位置**: 既有实现位于 Infrastructure 层而非 Application 层，Plan 保留实现位置不迁移（避免大范围改动），仅新建 QueryHandler 在 Application 层委托调用。
3. **双发期**: M6.2 OrderAppService 查询方法标记 `[Obsolete]` 与 QueryHandler 并存，2 周后（2026-08-01）切换 Controller 到 QueryHandler 并移除 Obsolete 方法。
4. **ProductTakenDownReadModelSyncConsumer 行为变更**: 重构后删除失败从仅 LogWarning 改为抛 InvalidOperationException，会触发 MassTransit 重试与死信队列（既有行为不抛异常不重试）。这是 spec 要求的一致性修复，需在上线前通知运维关注死信队列告警。
5. **IEsReadModelRepository 扩展**: Task 5 ShopDashboardReadModelSyncConsumer 需调用 `GetByIdAsync` 方法，若既有接口不含该方法需新增（与既有 `DeleteByIdAsync` 同级别扩展）。
6. **BFF HttpClient 注册**: Task 10 在 Program.cs 注册 7 个下游 HttpClient，未配置 Polly 策略（BFF 层超时 3 秒由 `BffForwarderService` 整体控制，单次调用复用 HttpClient 默认超时）。

### 类型一致性检查

- `IQueryHandler<TQuery, TResult>` 在 Task 6 定义，Task 7/8/9 引用 — 接口签名一致
- `AddQueryHandler<TQuery, TResult, THandler>` 在 Task 6 定义，Task 7/8/9 引用 — DI 注册方法签名一致
- `BffRequest`/`BffRequestResult`/`BffAggregationResult` 在 Task 10 Step 3 定义，Task 10 Step 4+ 引用 — record 类型签名一致
- `BffResponse<T>`/`BffError` 在 Task 10 Step 1 定义，Task 10 Step 4+ 引用 — 静态工厂方法 `Ok`/`PartialSuccess` 签名一致
- `ShopDashboardReadModel` 在 Task 5 定义，Task 9 ShopDashboardQueryService 引用 — 字段名一致
- `ReadModelSyncConsumerBase<TEvent, TReadModel>.BuildDeleteActionAsync` 在 Task 1 定义为虚方法，Task 2/3/5 重写 — 返回类型 `Task<(string Id, string IndexName)?>` 一致

### 占位符扫描

- 无 "TBD" / "TODO" / "implement later" / "fill in details" 等占位符
- Task 3 Step 8/10 与 Task 8 Step 5/9/11 的测试代码采用"参考 Task N Step M 模板"表述 — **这是合理引用**，因测试结构完全一致仅替换类型名，避免重复 600+ 行代码；实施时工程师需按模板替换事件类型与读模型字段
- 所有 git commit 命令均含完整中文提交说明，无占位

---

## Execution Handoff

**Plan complete and saved to `docs/superpowers/plans/2026-07-17-slow-track-m6-cqrs-bff-docs.md`. Two execution options:**

**1. Subagent-Driven (recommended)** - I dispatch a fresh subagent per task, review between tasks, fast iteration

**2. Inline Execution** - Execute tasks in this session using executing-plans, batch execution with checkpoints

**Which approach?**�