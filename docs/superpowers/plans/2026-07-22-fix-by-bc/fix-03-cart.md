# Cart（购物车域）修复实施计划

## 元数据
- 审计报告：[file:///workspace/docs/superpowers/specs/2026-07-21-code-audit/03-cart.md]
- 问题总数：🔴 5 / 🟡 15 / 🟢 10
- 已修复（跳过）：1 项（T12 CartPriceService 失败处理"价格加载失败掩盖"子问题已修复）
- 本计划覆盖：29 项（5 P0 + 15 P1 + 9 P2；另有 1 P0 子项 [ALREADY-FIXED]）

## 问题清单总表

| # | 严重度 | 问题标题 | 审计位置 | 优先级 | 状态 |
|---|--------|---------|---------|--------|------|
| 2.1 | 🔴 | SkuAddedToCartEvent/SkuRemovedFromCartEvent 无处理器，反向索引永不维护 | [file:///workspace/docs/superpowers/specs/2026-07-21-code-audit/03-cart.md#L15-L46] | P0 | 待修复 |
| 2.2 | 🔴 | 匿名购物车 TOCTOU 竞态 + Redis 异常静默吞掉导致数据丢失 | [file:///workspace/docs/superpowers/specs/2026-07-21-code-audit/03-cart.md#L48-L75] | P0 | 待修复 |
| 2.3 | 🔴 | CartAppService.BuildCartDtoAsync catch 错误异常类型，价格降级永不触发 | [file:///workspace/docs/superpowers/specs/2026-07-21-code-audit/03-cart.md#L77-L106] | P0 | 待修复 |
| 2.4 | 🔴 | 匿名购物车结算预览存在 0 元结算漏洞 | [file:///workspace/docs/superpowers/specs/2026-07-21-code-audit/03-cart.md#L108-L133] | P0 | 待修复 |
| 2.5 | 🔴 | 聚合不变量违反：AddItem 不校验品类上限，maxVariety=50 仅在 MergeFrom 中生效 | [file:///workspace/docs/superpowers/specs/2026-07-21-code-audit/03-cart.md#L135-L159] | P0 | 待修复 |
| 3.1 | 🟡 | MergeAnonymousCartAsync 跨存储非原子操作，Redis 删除失败导致重复合并数量翻倍 | [file:///workspace/docs/superpowers/specs/2026-07-21-code-audit/03-cart.md#L163-L169] | P1 | 待修复 |
| 3.2 | 🟡 | ProductEventConsumer 三个消费者 N+1 查询 + UpdateAsync 滥用 | [file:///workspace/docs/superpowers/specs/2026-07-21-code-audit/03-cart.md#L171-L177] | P1 | 待修复 |
| 3.3 | 🟡 | ProductUpdatedEventConsumer 每 SKU 一次 HTTP 快照查询 | [file:///workspace/docs/superpowers/specs/2026-07-21-code-audit/03-cart.md#L179-L185] | P1 | 待修复 |
| 3.4 | 🟡 | 匿名购物车聚合 _domainEvents 永不清理，Redis JSON 单调增长 | [file:///workspace/docs/superpowers/specs/2026-07-21-code-audit/03-cart.md#L187-L193] | P1 | 待修复 |
| 3.5 | 🟡 | CartSkuIndexService Redis Set 无 TTL，stale 索引永久驻留 | [file:///workspace/docs/superpowers/specs/2026-07-21-code-audit/03-cart.md#L195-L201] | P1 | 待修复 |
| 3.6 | 🟡 | CartSkuIndexService 异常处理与 RedisAnonymousCartRepository 不一致 | [file:///workspace/docs/superpowers/specs/2026-07-21-code-audit/03-cart.md#L203-L209] | P1 | 待修复 |
| 3.7 | 🟡 | AnonymousCartsController 无鉴权 + 无限流，DoS 与 sessionId 泄露风险 | [file:///workspace/docs/superpowers/specs/2026-07-21-code-audit/03-cart.md#L211-L220] | P1 | 待修复 |
| 3.8 | 🟡 | EfCoreCartRepository 读写未分离 AsNoTracking，读路径无谓跟踪 | [file:///workspace/docs/superpowers/specs/2026-07-21-code-audit/03-cart.md#L222-L228] | P1 | 待修复 |
| 3.9 | 🟡 | Cart.AddItem 六参数重载 unitPrice 死参数 + 快照回退风险 | [file:///workspace/docs/superpowers/specs/2026-07-21-code-audit/03-cart.md#L230-L246] | P1 | 待修复 |
| 3.10 | 🟡 | CartInternalQueryService 金额转分截断而非四舍五入 | [file:///workspace/docs/superpowers/specs/2026-07-21-code-audit/03-cart.md#L248-L254] | P1 | 待修复 |
| 3.11 | 🟡 | CartInternalQueryService.GetCartSnapshotAsync 永不返回 null，死代码 | [file:///workspace/docs/superpowers/specs/2026-07-21-code-audit/03-cart.md#L256-L262] | P1 | 待修复 |
| 3.12 | 🟡 | ClearSelectedItems 死代码 + 未发布 SkuRemovedFromCartEvent | [file:///workspace/docs/superpowers/specs/2026-07-21-code-audit/03-cart.md#L264-L270] | P1 | 待修复 |
| 3.13 | 🟡 | CircuitBreakerState 单例工厂读取 IOptionsMonitor 时机错误 | [file:///workspace/docs/superpowers/specs/2026-07-21-code-audit/03-cart.md#L272-L278] | P1 | 待修复 |
| 3.14 | 🟡 | CartAppService 多币种聚合错误 | [file:///workspace/docs/superpowers/specs/2026-07-21-code-audit/03-cart.md#L280-L286] | P1 | 待修复 |
| 3.15 | 🟡 | 匿名购物车 BuildCartDtoAsync 不处理 AntiCorruptionException | [file:///workspace/docs/superpowers/specs/2026-07-21-code-audit/03-cart.md#L288-L294] | P1 | 待修复 |
| 4.1 | 🟢 | RedisCartCache 注册但全局未被使用 | [file:///workspace/docs/superpowers/specs/2026-07-21-code-audit/03-cart.md#L298-L304] | P2 | 待修复 |
| 4.2 | 🟢 | Cart.AddItem 与 MergeFrom 重复 FindItem | [file:///workspace/docs/superpowers/specs/2026-07-21-code-audit/03-cart.md#L306-L312] | P2 | 待修复 |
| 4.3 | 🟢 | ConfigureAwait 使用不一致 | [file:///workspace/docs/superpowers/specs/2026-07-21-code-audit/03-cart.md#L314-L320] | P2 | 待修复 |
| 4.4 | 🟢 | CartItem.IsValid 字段初始化器与构造函数重复赋值 | [file:///workspace/docs/superpowers/specs/2026-07-21-code-audit/03-cart.md#L322-L328] | P2 | 待修复 |
| 4.5 | 🟢 | CartDbContextDesignTimeFactory 硬编码连接字符串含密码 | [file:///workspace/docs/superpowers/specs/2026-07-21-code-audit/03-cart.md#L330-L336] | P2 | 待修复 |
| 4.6 | 🟢 | 匿名购物车 sessionId 暴露在 URL 路径 | [file:///workspace/docs/superpowers/specs/2026-07-21-code-audit/03-cart.md#L338-L344] | P2 | 待修复 |
| 4.7 | 🟢 | MergeFrom 不跳过匿名购物车中的无效项 | [file:///workspace/docs/superpowers/specs/2026-07-21-code-audit/03-cart.md#L346-L352] | P2 | 待修复 |
| 4.8 | 🟢 | AnonymousCartAppService.GetCartAsync 刷新 TTL 被攻击者利用 | [file:///workspace/docs/superpowers/specs/2026-07-21-code-audit/03-cart.md#L354-L360] | P2 | 待修复 |
| 4.9 | 🟢 | ProductEventConsumer 三个消费者共享 DbContext 跨批次累积跟踪 | [file:///workspace/docs/superpowers/specs/2026-07-21-code-audit/03-cart.md#L362-L368] | P2 | 待修复 |
| 4.10 | 🟢 | AnonymousCartAppService.GetOrCreateCartAsync 在不存在时立即 SaveAsync 覆盖 | [file:///workspace/docs/superpowers/specs/2026-07-21-code-audit/03-cart.md#L370-L376] | P2 | 待修复 |

---

## P0 详细修复计划（TDD bite-sized 格式，5 步：测试→验证失败→实现→验证通过→提交）

### P0-1：修复 2.1 SkuAddedToCartEvent/SkuRemovedFromCartEvent 无处理器

**问题证据**：
- 领域事件发布处：[file:///workspace/src/Services/Cart/Leno.Cart.Domain/Aggregates/Cart.cs#L90]（AddItem 发布 SkuAddedToCartEvent）、[file:///workspace/src/Services/Cart/Leno.Cart.Domain/Aggregates/Cart.cs#L131]（RemoveItem 发布 SkuRemovedFromCartEvent）
- 翻译器显式声明不映射：[file:///workspace/src/Services/Cart/Leno.Cart.Infrastructure/EventBus/CartIntegrationEventMapper.cs#L19-L21]
- 反向索引服务未被任何事件处理器调用：[file:///workspace/src/Services/Cart/Leno.Cart.Infrastructure/Services/CartSkuIndexService.cs#L27-L49]
- SaveChangesWithOutboxAsync 仅翻译集成事件，丢弃非 IIntegrationEvent：[file:///workspace/src/BuildingBlocks/Leno.Infrastructure/Outbox/OutboxDbContextExtensions.cs#L30-L60]

**修复策略**：在 `EfCoreUnitOfWork<CartDbContext>.SaveEntitiesAsync` 落库前，遍历聚合根的 `SkuAddedToCartEvent/SkuRemovedFromCartEvent` 域事件，调用 `ICartSkuIndexService.AddAsync/RemoveAsync` 维护反向索引。该方案与持久化同事务顺序执行，保证索引维护与聚合状态变更一致；处理后再由 `SaveChangesWithOutboxAsync` 清理域事件。

#### 步骤 1：编写测试（红）

新建测试文件 `/workspace/src/Services/Cart/Leno.Cart.Infrastructure.Tests/CartSkuIndexDomainEventDispatcherTests.cs`：

```csharp
using Leno.Cart.Domain.Aggregates;
using Leno.Cart.Domain.Events;
using Leno.Cart.Domain.Services;
using Leno.Cart.Infrastructure.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using StackExchange.Redis;

namespace Leno.Cart.Infrastructure.Tests;

public class CartSkuIndexDomainEventDispatcherTests
{
    private readonly Mock<ICartSkuIndexService> _indexServiceMock = new();

    [Fact]
    public async Task DispatchAsync_SkuAddedToCartEvent_ShouldCallIndexServiceAddAsync()
    {
        var cartId = Guid.NewGuid();
        var skuId = Guid.NewGuid();
        var dispatcher = new CartSkuIndexDomainEventDispatcher(_indexServiceMock.Object);
        var domainEvents = new List<object>
        {
            new SkuAddedToCartEvent(cartId, skuId)
        };

        await dispatcher.DispatchAsync(domainEvents, CancellationToken.None);

        _indexServiceMock.Verify(
            s => s.AddAsync(skuId, cartId, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task DispatchAsync_SkuRemovedFromCartEvent_ShouldCallIndexServiceRemoveAsync()
    {
        var cartId = Guid.NewGuid();
        var skuId = Guid.NewGuid();
        var dispatcher = new CartSkuIndexDomainEventDispatcher(_indexServiceMock.Object);
        var domainEvents = new List<object>
        {
            new SkuRemovedFromCartEvent(cartId, skuId)
        };

        await dispatcher.DispatchAsync(domainEvents, CancellationToken.None);

        _indexServiceMock.Verify(
            s => s.RemoveAsync(skuId, cartId, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task DispatchAsync_MixedEvents_ShouldDispatchEachToCorrectHandler()
    {
        var cartId = Guid.NewGuid();
        var skuAdd = Guid.NewGuid();
        var skuRemove = Guid.NewGuid();
        var dispatcher = new CartSkuIndexDomainEventDispatcher(_indexServiceMock.Object);
        var domainEvents = new List<object>
        {
            new SkuAddedToCartEvent(cartId, skuAdd),
            new SkuRemovedFromCartEvent(cartId, skuRemove),
            new SkuAddedToCartEvent(cartId, skuAdd) // 重复也应再次调用
        };

        await dispatcher.DispatchAsync(domainEvents, CancellationToken.None);

        _indexServiceMock.Verify(s => s.AddAsync(skuAdd, cartId, It.IsAny<CancellationToken>()), Times.Exactly(2));
        _indexServiceMock.Verify(s => s.RemoveAsync(skuRemove, cartId, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task DispatchAsync_UnrelatedEvent_ShouldSkipSilently()
    {
        var dispatcher = new CartSkuIndexDomainEventDispatcher(_indexServiceMock.Object);
        var domainEvents = new List<object>
        {
            Guid.NewGuid(),
            "not-an-event"
        };

        await dispatcher.DispatchAsync(domainEvents, CancellationToken.None);

        _indexServiceMock.Verify(
            s => s.AddAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()),
            Times.Never);
        _indexServiceMock.Verify(
            s => s.RemoveAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task DispatchAsync_IndexServiceThrows_ShouldPropagateToCaller()
    {
        var cartId = Guid.NewGuid();
        var skuId = Guid.NewGuid();
        _indexServiceMock
            .Setup(s => s.AddAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("redis down"));
        var dispatcher = new CartSkuIndexDomainEventDispatcher(_indexServiceMock.Object);
        var domainEvents = new List<object>
        {
            new SkuAddedToCartEvent(cartId, skuId)
        };

        var act = () => dispatcher.DispatchAsync(domainEvents, CancellationToken.None);

        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("*redis down*");
    }
}
```

新建集成测试 `/workspace/src/Services/Cart/Leno.Cart.Infrastructure.Tests/CartSkuIndexIntegrationTests.cs` 验证 `SaveEntitiesAsync` 时索引被维护：

```csharp
using Leno.Cart.Application.Services;
using Leno.Cart.Domain.Aggregates;
using Leno.Cart.Domain.Repositories;
using Leno.Cart.Domain.Services;
using Leno.Cart.Infrastructure.Repositories;
using Leno.Infrastructure.EventBus;
using Leno.Infrastructure.Persistence;
using Leno.SharedKernel.Abstractions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using StackExchange.Redis;

namespace Leno.Cart.Infrastructure.Tests.Integration;

public class CartSkuIndexIntegrationTests
{
    [Fact]
    public async Task SaveEntitiesAsync_WhenAddItemRaised_ShouldUpdateReverseIndexBeforeCommit()
    {
        // Arrange
        var indexServiceMock = new Mock<ICartSkuIndexService>();
        var capturedAddCalls = new List<(Guid SkuId, Guid CartId)>();
        indexServiceMock
            .Setup(s => s.AddAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .Callback<Guid, Guid, CancellationToken>((sku, cart, _) => capturedAddCalls.Add((sku, cart)))
            .Returns(Task.CompletedTask);

        await using var context = TestDbContextFactory.Create();
        var mapper = new EmptyIntegrationEventMapper();
        var uow = new CartUnitOfWorkWithIndexDispatch(context, mapper, indexServiceMock.Object);
        var cartRepo = new EfCoreCartRepository(context);

        var userId = Guid.NewGuid();
        var cart = Cart.Create(Guid.NewGuid(), userId);
        cartRepo.AddAsync(cart, default).Wait();
        await context.SaveChangesAsync(default);

        // Act
        var skuId = Guid.NewGuid();
        cart.AddItem(skuId, 1, Guid.NewGuid());
        await uow.SaveEntitiesAsync(default);

        // Assert：领域事件被分发到反向索引
        capturedAddCalls.Should().ContainSingle(c => c.SkuId == skuId && c.CartId == cart.Id);
    }

    [Fact]
    public async Task SaveEntitiesAsync_WhenRemoveItemRaised_ShouldUpdateReverseIndex()
    {
        var indexServiceMock = new Mock<ICartSkuIndexService>();
        var capturedRemoveCalls = new List<(Guid SkuId, Guid CartId)>();
        indexServiceMock
            .Setup(s => s.RemoveAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .Callback<Guid, Guid, CancellationToken>((sku, cart, _) => capturedRemoveCalls.Add((sku, cart)))
            .Returns(Task.CompletedTask);

        await using var context = TestDbContextFactory.Create();
        var mapper = new EmptyIntegrationEventMapper();
        var uow = new CartUnitOfWorkWithIndexDispatch(context, mapper, indexServiceMock.Object);
        var cartRepo = new EfCoreCartRepository(context);

        var userId = Guid.NewGuid();
        var skuId = Guid.NewGuid();
        var cart = Cart.Create(Guid.NewGuid(), userId);
        cart.AddItem(skuId, 1, Guid.NewGuid());
        cartRepo.AddAsync(cart, default).Wait();
        await context.SaveChangesAsync(default);
        cart.ClearDomainEvents();

        // Act
        cart.RemoveItem(skuId);
        await uow.SaveEntitiesAsync(default);

        // Assert
        capturedRemoveCalls.Should().ContainSingle(c => c.SkuId == skuId && c.CartId == cart.Id);
    }
}

internal sealed class EmptyIntegrationEventMapper : IIntegrationEventMapper
{
    public IIntegrationEvent? Map(IDomainEvent domainEvent) => null;
}
```

#### 步骤 2：运行测试，验证失败

```bash
dotnet test src/Services/Cart/Leno.Cart.Infrastructure.Tests/Leno.Cart.Infrastructure.Tests.csproj --filter "FullyQualifiedName~CartSkuIndexDomainEventDispatcherTests|FullyQualifiedName~CartSkuIndexIntegrationTests"
```

预期失败原因：`CartSkuIndexDomainEventDispatcher` 与 `CartUnitOfWorkWithIndexDispatch` 类型不存在，编译失败。

#### 步骤 3：实现修复代码（绿）

新建文件 `/workspace/src/Services/Cart/Leno.Cart.Infrastructure/Services/CartSkuIndexDomainEventDispatcher.cs`：

```csharp
using Leno.Cart.Domain.Events;
using Leno.Cart.Domain.Services;

namespace Leno.Cart.Infrastructure.Services;

/// <summary>
/// 购物车-SKU 反向索引领域事件分发器。
/// 在 <c>IUnitOfWork.SaveEntitiesAsync</c> 落库前由 CartUnitOfWork 调用，
/// 遍历聚合收集的 <see cref="SkuAddedToCartEvent"/> / <see cref="SkuRemovedFromCartEvent"/>
/// 并调用 <see cref="ICartSkuIndexService"/> 维护 Redis Set 反向索引，
/// 与聚合状态变更保持顺序一致（索引先于 DB 事务提交）。
/// </summary>
public sealed class CartSkuIndexDomainEventDispatcher
{
    private readonly ICartSkuIndexService _indexService;

    public CartSkuIndexDomainEventDispatcher(ICartSkuIndexService indexService)
    {
        ArgumentNullException.ThrowIfNull(indexService);
        _indexService = indexService;
    }

    /// <summary>
    /// 按顺序分发购物车域事件到反向索引服务。
    /// 索引服务异常上抛，由调用方决定是否中断事务（默认应中断以避免索引与聚合状态不一致）。
    /// </summary>
    /// <param name="domainEvents">本次保存变更中收集到的所有领域事件（含非 SKU 索引事件，会被忽略）。</param>
    /// <param name="ct">取消令牌。</param>
    public async Task DispatchAsync(IReadOnlyList<object> domainEvents, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(domainEvents);

        foreach (var domainEvent in domainEvents)
        {
            switch (domainEvent)
            {
                case SkuAddedToCartEvent added:
                    await _indexService.AddAsync(added.SkuId, added.CartId, ct);
                    break;
                case SkuRemovedFromCartEvent removed:
                    await _indexService.RemoveAsync(removed.SkuId, removed.CartId, ct);
                    break;
            }
        }
    }
}
```

新建 Cart 专用工作单元 `/workspace/src/Services/Cart/Leno.Cart.Infrastructure/CartUnitOfWork.cs`，覆盖通用 `EfCoreUnitOfWork<CartDbContext>` 以在保存前分发 SKU 索引事件：

```csharp
using Leno.Cart.Infrastructure.Services;
using Leno.Infrastructure.EventBus;
using Leno.Infrastructure.Outbox;
using Leno.Infrastructure.Persistence;
using Leno.SharedKernel.Abstractions;
using Microsoft.EntityFrameworkCore;

namespace Leno.Cart.Infrastructure;

/// <summary>
/// Cart BC 专用工作单元，在落库前分发 SkuAddedToCartEvent/SkuRemovedFromCartEvent 到反向索引服务。
/// 替代 <see cref="EfCoreUnitOfWork{TDbContext}"/> 的默认行为以维护购物车-SKU 反向索引。
/// </summary>
public sealed class CartUnitOfWork : IUnitOfWork
{
    private readonly CartDbContext _context;
    private readonly IIntegrationEventMapper _mapper;
    private readonly CartSkuIndexDomainEventDispatcher _indexDispatcher;

    public CartUnitOfWork(
        CartDbContext context,
        IIntegrationEventMapper mapper,
        CartSkuIndexDomainEventDispatcher indexDispatcher)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(mapper);
        ArgumentNullException.ThrowIfNull(indexDispatcher);
        _context = context;
        _mapper = mapper;
        _indexDispatcher = indexDispatcher;
    }

    /// <inheritdoc />
    public Task<int> SaveChangesAsync(CancellationToken ct = default)
        => _context.SaveChangesAsync(ct);

    /// <inheritdoc />
    public async Task<bool> SaveEntitiesAsync(CancellationToken ct = default)
    {
        // 步骤 1：在事务开启前，从 ChangeTracker 收集 Cart 聚合的领域事件并分发到反向索引。
        // 索引维护失败时直接抛出，由全局异常处理，避免聚合状态已落库但索引缺失。
        var aggregates = _context.ChangeTracker.Entries<AggregateRoot>()
            .Select(e => e.Entity)
            .ToList();

        var skuIndexEvents = aggregates
            .SelectMany(a => a.DomainEvents)
            .OfType<object>()
            .ToList();

        if (skuIndexEvents.Count > 0)
        {
            await _indexDispatcher.DispatchAsync(skuIndexEvents, ct);
        }

        // 步骤 2：在事务内保存聚合变更 + 集成事件落 Outbox（同时清空领域事件）
        await _context.SaveChangesWithOutboxAsync(_mapper, ct);
        return true;
    }

    /// <inheritdoc />
    public async Task<IUnitOfWorkTransaction> BeginTransactionAsync(CancellationToken ct = default)
    {
        var transaction = await _context.Database.BeginTransactionAsync(ct);
        return new EfCoreUnitOfWork<EfCoreUnitOfWork<CartDbContext>>.UnitOfWorkTransaction(transaction);
    }

    public void Dispose()
    {
        _context.Dispose();
    }
}
```

> 注：`EfCoreUnitOfWork<TDbContext>.UnitOfWorkTransaction` 为内部类型，应将其抽取为 `Leno.Infrastructure.Persistence.UnitOfWorkTransaction` 公共类（如已存在则直接引用）。若抽取工作量过大，可暂时在 `CartUnitOfWork` 内重复定义如下：

```csharp
private sealed class UnitOfWorkTransaction : IUnitOfWorkTransaction
{
    private readonly IDbContextTransaction _transaction;
    public UnitOfWorkTransaction(IDbContextTransaction transaction) => _transaction = transaction;
    public Task CommitAsync(CancellationToken ct = default) => _transaction.CommitAsync(ct);
    public Task RollbackAsync(CancellationToken ct = default) => _transaction.RollbackAsync(ct);
    public void Dispose() => _transaction.Dispose();
    public ValueTask DisposeAsync() => _transaction.DisposeAsync();
}
```

修改 `/workspace/src/Services/Cart/Leno.Cart.Infrastructure/Dependencies/ServiceCollectionExtensions.cs` L50：

```csharp
// 旧：
// services.AddScoped<IUnitOfWork, EfCoreUnitOfWork<CartDbContext>>();

// 新：注册 CartSkuIndexDomainEventDispatcher 与 CartUnitOfWork
services.AddScoped<CartSkuIndexDomainEventDispatcher>();
services.AddScoped<IUnitOfWork, CartUnitOfWork>();
```

#### 步骤 4：运行测试，验证通过

```bash
dotnet test src/Services/Cart/Leno.Cart.Infrastructure.Tests/Leno.Cart.Infrastructure.Tests.csproj \
    --filter "FullyQualifiedName~CartSkuIndexDomainEventDispatcherTests|FullyQualifiedName~CartSkuIndexIntegrationTests"
```

预期：5 + 2 = 7 个测试全部通过。

#### 步骤 5：提交

```bash
git add src/Services/Cart/Leno.Cart.Infrastructure/Services/CartSkuIndexDomainEventDispatcher.cs \
        src/Services/Cart/Leno.Cart.Infrastructure/CartUnitOfWork.cs \
        src/Services/Cart/Leno.Cart.Infrastructure/Dependencies/ServiceCollectionExtensions.cs \
        src/Services/Cart/Leno.Cart.Infrastructure.Tests/CartSkuIndexDomainEventDispatcherTests.cs \
        src/Services/Cart/Leno.Cart.Infrastructure.Tests/Integration/CartSkuIndexIntegrationTests.cs
git commit -m "fix(cart): 新增 SkuAddedToCartEvent/SkuRemovedFromCartEvent 处理器维护反向索引"
```

---

### P0-2：修复 2.2 匿名购物车 TOCTOU 竞态 + Redis 异常静默吞掉

**问题证据**：
- 全部方法 catch 后静默：[file:///workspace/src/Services/Cart/Leno.Cart.Infrastructure/Repositories/RedisAnonymousCartRepository.cs#L34-L51]、[file:///workspace/src/Services/Cart/Leno.Cart.Infrastructure/Repositories/RedisAnonymousCartRepository.cs#L54-L69]、[file:///workspace/src/Services/Cart/Leno.Cart.Infrastructure/Repositories/RedisAnonymousCartRepository.cs#L72-L85]、[file:///workspace/src/Services/Cart/Leno.Cart.Infrastructure/Repositories/RedisAnonymousCartRepository.cs#L88-L101]
- GetOrCreateCartAsync 在 GetAsync 返回 null 后立即 SaveAsync 覆盖：[file:///workspace/src/Services/Cart/Leno.Cart.Application/Services/AnonymousCartAppService.cs#L137-L147]

**修复策略**：
1. 异常不再静默：`catch (RedisConnectionException ex)` 包装为 `CartInfrastructureException`（新增）向上抛；`catch (Exception ex)` 仍记录但同样抛出。Redis Key 不存在（`RedisKeyNotFoundException`/`StringGetAsync` 返回 `HasValue=false`）保持返回 null，区分"购物车不存在"与"基础设施故障"。
2. 引入版本号 CAS：在 `CartAggregate` 上增加 `ConcurrencyVersion` 字段，`SaveAsync` 时序列化到 JSON 中；写入前用 Lua 脚本原子校验版本号，避免后写覆盖先写。

> 鉴于引入版本号涉及聚合结构变更与 EF Core 迁移，本计划聚焦"异常上抛"子项（解决静默清空与故障可观测），CAS 子项作为 P1 跟进（见 3.1 修复清单）。该拆分已可在 P0 阶段消除"Redis 抖动期间用户购物车被静默清空"风险——异常上抛后 `GetOrCreateCartAsync` 不再误判"购物车不存在"。

#### 步骤 1：编写测试（红）

新建测试 `/workspace/src/Services/Cart/Leno.Cart.Infrastructure.Tests/RedisAnonymousCartRepositoryTests.cs`：

```csharp
using Leno.Cart.Domain.Aggregates;
using Leno.Cart.Domain.Exceptions;
using Leno.Cart.Infrastructure.Repositories;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using StackExchange.Redis;

namespace Leno.Cart.Infrastructure.Tests;

public class RedisAnonymousCartRepositoryTests
{
    private readonly Mock<IConnectionMultiplexer> _redisMock = new();
    private readonly Mock<IDatabase> _dbMock = new();

    public RedisAnonymousCartRepositoryTests()
    {
        _redisMock.Setup(r => r.GetDatabase(It.IsAny<int>(), It.IsAny<object>())).Returns(_dbMock.Object);
    }

    [Fact]
    public async Task GetAsync_RedisConnectionException_ShouldThrowCartInfrastructureException()
    {
        _dbMock
            .Setup(d => d.StringGetAsync(It.IsAny<RedisKey>(), It.IsAny<CommandFlags>()))
            .ThrowsAsync(new RedisConnectionException(ConnectionFailureType.SocketFailure, "redis down"));
        var sut = new RedisAnonymousCartRepository(_redisMock.Object, NullLogger<RedisAnonymousCartRepository>.Instance);

        var act = () => sut.GetAsync("session-1");

        await act.Should().ThrowAsync<CartInfrastructureException>()
            .WithMessage("*匿名购物车暂不可用*")
            .WithInnerException<RedisConnectionException>();
    }

    [Fact]
    public async Task GetAsync_KeyNotExists_ShouldReturnNullWithoutThrowing()
    {
        _dbMock
            .Setup(d => d.StringGetAsync(It.IsAny<RedisKey>(), It.IsAny<CommandFlags>()))
            .ReturnsAsync(RedisValue.Null);
        var sut = new RedisAnonymousCartRepository(_redisMock.Object, NullLogger<RedisAnonymousCartRepository>.Instance);

        var result = await sut.GetAsync("session-1");

        result.Should().BeNull();
    }

    [Fact]
    public async Task SaveAsync_RedisConnectionException_ShouldThrowCartInfrastructureException()
    {
        _dbMock
            .Setup(d => d.StringSetAsync(It.IsAny<RedisKey>(), It.IsAny<RedisValue>(), It.IsAny<TimeSpan?>(), It.IsAny<bool>(), It.IsAny<When>(), It.IsAny<CommandFlags>()))
            .ThrowsAsync(new RedisConnectionException(ConnectionFailureType.SocketFailure, "redis down"));
        var sut = new RedisAnonymousCartRepository(_redisMock.Object, NullLogger<RedisAnonymousCartRepository>.Instance);
        var cart = Cart.CreateAnonymous(Guid.NewGuid());

        var act = () => sut.SaveAsync("session-1", cart);

        await act.Should().ThrowAsync<CartInfrastructureException>()
            .WithMessage("*匿名购物车暂不可用*");
    }

    [Fact]
    public async Task RemoveAsync_RedisConnectionException_ShouldThrowCartInfrastructureException()
    {
        _dbMock
            .Setup(d => d.KeyDeleteAsync(It.IsAny<RedisKey>(), It.IsAny<CommandFlags>()))
            .ThrowsAsync(new RedisConnectionException(ConnectionFailureType.SocketFailure, "redis down"));
        var sut = new RedisAnonymousCartRepository(_redisMock.Object, NullLogger<RedisAnonymousCartRepository>.Instance);

        var act = () => sut.RemoveAsync("session-1");

        await act.Should().ThrowAsync<CartInfrastructureException>();
    }

    [Fact]
    public async Task RefreshTtlAsync_RedisConnectionException_ShouldThrowCartInfrastructureException()
    {
        _dbMock
            .Setup(d => d.KeyExpireAsync(It.IsAny<RedisKey>(), It.IsAny<TimeSpan?>(), It.IsAny<CommandFlags>()))
            .ThrowsAsync(new RedisConnectionException(ConnectionFailureType.SocketFailure, "redis down"));
        var sut = new RedisAnonymousCartRepository(_redisMock.Object, NullLogger<RedisAnonymousCartRepository>.Instance);

        var act = () => sut.RefreshTtlAsync("session-1");

        await act.Should().ThrowAsync<CartInfrastructureException>();
    }

    [Fact]
    public async Task GetAsync_GeneralException_ShouldThrowCartInfrastructureException()
    {
        _dbMock
            .Setup(d => d.StringGetAsync(It.IsAny<RedisKey>(), It.IsAny<CommandFlags>()))
            .ThrowsAsync(new InvalidOperationException("unexpected"));
        var sut = new RedisAnonymousCartRepository(_redisMock.Object, NullLogger<RedisAnonymousCartRepository>.Instance);

        var act = () => sut.GetAsync("session-1");

        await act.Should().ThrowAsync<CartInfrastructureException>();
    }
}
```

新增集成测试 `/workspace/src/Services/Cart/Leno.Cart.Application.Tests/AnonymousCartAppServiceFailurePropagationTests.cs` 验证调用方不再掩盖故障：

```csharp
using Leno.Cart.Application.DTOs;
using Leno.Cart.Application.Services;
using Leno.Cart.Domain.Exceptions;
using Leno.Cart.Domain.Repositories;
using Leno.Cart.Domain.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace Leno.Cart.Application.Tests;

public class AnonymousCartAppServiceFailurePropagationTests
{
    private readonly Mock<IAnonymousCartRepository> _repoMock = new();
    private readonly Mock<ICartPriceService> _priceMock = new();

    [Fact]
    public async Task GetCartAsync_WhenRepoThrowsCartInfrastructureException_ShouldPropagateNotSilentlyCreateNew()
    {
        // Arrange：Redis 故障应向上抛，而非被掩盖为"购物车不存在 → 创建新空购物车覆盖"
        _repoMock
            .Setup(r => r.GetAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new CartInfrastructureException("匿名购物车暂不可用", "CART_REDIS_UNAVAILABLE"));
        _priceMock.Setup(p => p.GetSkuPricesAsync(It.IsAny<IEnumerable<Guid>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<SkuPriceSnapshot>());
        var sut = new AnonymousCartAppService(_repoMock.Object, _priceMock.Object);

        // Act
        var act = () => sut.GetCartAsync("session-1");

        // Assert
        await act.Should().ThrowAsync<CartInfrastructureException>();
        _repoMock.Verify(r => r.SaveAsync(It.IsAny<string>(), It.IsAny<Domain.Aggregates.Cart>(), It.IsAny<CancellationToken>()), Times.Never);
        _repoMock.Verify(r => r.RefreshTtlAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }
}
```

#### 步骤 2：运行测试，验证失败

```bash
dotnet test src/Services/Cart/Leno.Cart.Infrastructure.Tests/Leno.Cart.Infrastructure.Tests.csproj --filter "FullyQualifiedName~RedisAnonymousCartRepositoryTests"
dotnet test src/Services/Cart/Leno.Cart.Application.Tests/Leno.Cart.Application.Tests.csproj --filter "FullyQualifiedName~AnonymousCartAppServiceFailurePropagationTests"
```

预期失败：`CartInfrastructureException` 类型不存在；`RedisAnonymousCartRepository` 仍静默吞异常返回 null。

#### 步骤 3：实现修复代码（绿）

新增异常 `/workspace/src/Services/Cart/Leno.Cart.Domain/Exceptions/CartInfrastructureException.cs`：

```csharp
using Leno.SharedKernel.Exceptions;

namespace Leno.Cart.Domain.Exceptions;

/// <summary>
/// 购物车域基础设施故障异常（Redis / 数据库等不可用）。
/// 与 <see cref="CartDomainException"/> 区分业务异常不同，本异常表达基础设施层故障，
/// 携带错误码（如 <c>CART_REDIS_UNAVAILABLE</c>）由全局异常中间件映射为 HTTP 503。
/// </summary>
public sealed class CartInfrastructureException : DomainException
{
    public CartInfrastructureException(string message, string errorCode = "CART_INFRA_UNAVAILABLE")
        : base(message, errorCode)
    {
    }

    public CartInfrastructureException(string message, Exception innerException, string errorCode = "CART_INFRA_UNAVAILABLE")
        : base(message, innerException, errorCode)
    {
    }
}
```

修改 `/workspace/src/Services/Cart/Leno.Cart.Infrastructure/Repositories/RedisAnonymousCartRepository.cs` 完整替换 4 个方法实现（保留类签名、字段、构造函数与 `BuildKey`）：

```csharp
using System.Text.Json;
using Leno.Cart.Domain.Aggregates;
using Leno.Cart.Domain.Exceptions;
using Leno.Cart.Domain.Repositories;
using Microsoft.Extensions.Logging;
using StackExchange.Redis;
using CartAggregate = Leno.Cart.Domain.Aggregates.Cart;

namespace Leno.Cart.Infrastructure.Repositories;

/// <summary>
/// 匿名购物车 Redis 仓储实现，以会话标识为键存储匿名购物车聚合。
/// TTL 7 天，每次操作刷新过期时间。
/// 基础设施故障（Redis 不可达、超时等）包装为 <see cref="CartInfrastructureException"/> 向上抛出，
/// 避免调用方误判"购物车不存在"并覆盖写入。
/// </summary>
public sealed class RedisAnonymousCartRepository : IAnonymousCartRepository
{
    private static readonly TimeSpan Ttl = TimeSpan.FromDays(7);
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly IConnectionMultiplexer _redis;
    private readonly ILogger<RedisAnonymousCartRepository> _logger;

    public RedisAnonymousCartRepository(IConnectionMultiplexer redis, ILogger<RedisAnonymousCartRepository> logger)
    {
        ArgumentNullException.ThrowIfNull(redis);
        ArgumentNullException.ThrowIfNull(logger);
        _redis = redis;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<CartAggregate?> GetAsync(string sessionId, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sessionId);
        try
        {
            var db = _redis.GetDatabase();
            var key = BuildKey(sessionId);
            var value = await db.StringGetAsync(key);
            if (!value.HasValue)
            {
                return null;
            }

            return JsonSerializer.Deserialize<CartAggregate>((string)value!, JsonOptions);
        }
        catch (Exception ex) when (ex is not CartInfrastructureException)
        {
            _logger.LogError(ex, "读取匿名购物车缓存失败 SessionId={SessionId}", sessionId);
            throw new CartInfrastructureException("匿名购物车暂不可用", ex, "CART_REDIS_UNAVAILABLE");
        }
    }

    /// <inheritdoc />
    public async Task SaveAsync(string sessionId, CartAggregate cart, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sessionId);
        ArgumentNullException.ThrowIfNull(cart);
        try
        {
            var db = _redis.GetDatabase();
            var key = BuildKey(sessionId);
            var value = JsonSerializer.Serialize(cart, JsonOptions);
            await db.StringSetAsync(key, value, Ttl);
        }
        catch (Exception ex) when (ex is not CartInfrastructureException)
        {
            _logger.LogError(ex, "写入匿名购物车缓存失败 SessionId={SessionId}", sessionId);
            throw new CartInfrastructureException("匿名购物车暂不可用", ex, "CART_REDIS_UNAVAILABLE");
        }
    }

    /// <inheritdoc />
    public async Task RemoveAsync(string sessionId, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sessionId);
        try
        {
            var db = _redis.GetDatabase();
            var key = BuildKey(sessionId);
            await db.KeyDeleteAsync(key);
        }
        catch (Exception ex) when (ex is not CartInfrastructureException)
        {
            _logger.LogError(ex, "删除匿名购物车缓存失败 SessionId={SessionId}", sessionId);
            throw new CartInfrastructureException("匿名购物车暂不可用", ex, "CART_REDIS_UNAVAILABLE");
        }
    }

    /// <inheritdoc />
    public async Task RefreshTtlAsync(string sessionId, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sessionId);
        try
        {
            var db = _redis.GetDatabase();
            var key = BuildKey(sessionId);
            await db.KeyExpireAsync(key, Ttl);
        }
        catch (Exception ex) when (ex is not CartInfrastructureException)
        {
            _logger.LogError(ex, "刷新匿名购物车 TTL 失败 SessionId={SessionId}", sessionId);
            throw new CartInfrastructureException("匿名购物车暂不可用", ex, "CART_REDIS_UNAVAILABLE");
        }
    }

    private static string BuildKey(string sessionId) => $"cart:anon:{sessionId}";
}
```

#### 步骤 4：运行测试，验证通过

```bash
dotnet build src/Services/Cart/Leno.Cart.Infrastructure/Leno.Cart.Infrastructure.csproj
dotnet test src/Services/Cart/Leno.Cart.Infrastructure.Tests/Leno.Cart.Infrastructure.Tests.csproj --filter "FullyQualifiedName~RedisAnonymousCartRepositoryTests"
dotnet test src/Services/Cart/Leno.Cart.Application.Tests/Leno.Cart.Application.Tests.csproj --filter "FullyQualifiedName~AnonymousCartAppServiceFailurePropagationTests"
```

预期：6 + 1 = 7 个测试全部通过。

#### 步骤 5：提交

```bash
git add src/Services/Cart/Leno.Cart.Domain/Exceptions/CartInfrastructureException.cs \
        src/Services/Cart/Leno.Cart.Infrastructure/Repositories/RedisAnonymousCartRepository.cs \
        src/Services/Cart/Leno.Cart.Infrastructure.Tests/RedisAnonymousCartRepositoryTests.cs \
        src/Services/Cart/Leno.Cart.Application.Tests/AnonymousCartAppServiceFailurePropagationTests.cs
git commit -m "fix(cart): 匿名购物车仓储异常不再静默吞掉，包装为 CartInfrastructureException 上抛"
```

---

### P0-3：修复 2.3 CartAppService.BuildCartDtoAsync catch 错误异常类型

**问题证据**：
- catch 错误异常类型：[file:///workspace/src/Services/Cart/Leno.Cart.Application/Services/CartAppService.cs#L224]（catch `CartDomainException`）
- 防腐层实际抛 AntiCorruptionException：[file:///workspace/src/BuildingBlocks/Leno.Infrastructure/AntiCorruption/AntiCorruptionBase.cs#L34]、[file:///workspace/src/BuildingBlocks/Leno.Infrastructure/AntiCorruption/AntiCorruptionBase.cs#L41]、[file:///workspace/src/BuildingBlocks/Leno.Infrastructure/AntiCorruption/AntiCorruptionBase.cs#L53]、[file:///workspace/src/BuildingBlocks/Leno.Infrastructure/AntiCorruption/AntiCorruptionBase.cs#L76]

**修复策略**：将 `catch (CartDomainException ex)` 改为 `catch (AntiCorruptionException ex)`，使价格服务故障时进入降级分支。

#### 步骤 1：编写测试（红）

在 `/workspace/src/Services/Cart/Leno.Cart.Application.Tests/CartAppServiceTests.cs` 中新增测试用例。注意需引入 `Leno.Infrastructure.AntiCorruption` 命名空间：

```csharp
using Leno.Infrastructure.AntiCorruption;

// ... 在 CartAppServiceTests 类中追加：

[Fact]
public async Task GetCartAsync_WhenPriceServiceThrowsAntiCorruptionException_ShouldDegradeAndMarkPriceUnavailable()
{
    // Arrange：AntiCorruptionBase 实际抛 AntiCorruptionException，原 catch(CartDomainException) 不会命中
    var cart = CreateCart();
    cart.AddItem(SkuId, 2, SellerId);
    _cartRepoMock.Setup(r => r.GetByUserIdAsync(UserId, It.IsAny<CancellationToken>()))
        .ReturnsAsync(cart);
    _priceServiceMock.Setup(p => p.GetSkuPricesAsync(It.IsAny<IEnumerable<Guid>>(), It.IsAny<CancellationToken>()))
        .ThrowsAsync(new AntiCorruptionException("product 网络故障", "PRODUCT_UNAVAILABLE"));

    // Act
    var result = await _sut.GetCartAsync(UserId);

    // Assert：进入降级分支，标记 PriceUnavailable=true，不向控制器冒泡
    result.Should().NotBeNull();
    result.Items.Should().HaveCount(1);
    result.Items[0].PriceUnavailable.Should().BeTrue();
    result.Items[0].Available.Should().BeFalse();
    result.Items[0].Title.Should().Be("[价格加载失败]");
    result.SelectedTotalAmount.Should().Be(0m);
}
```

#### 步骤 2：运行测试，验证失败

```bash
dotnet test src/Services/Cart/Leno.Cart.Application.Tests/Leno.Cart.Application.Tests.csproj \
    --filter "FullyQualifiedName~GetCartAsync_WhenPriceServiceThrowsAntiCorruptionException_ShouldDegradeAndMarkPriceUnavailable"
```

预期失败：实际抛 `AntiCorruptionException` 未被 catch，测试用例抛出异常而非降级返回 DTO。

#### 步骤 3：实现修复代码（绿）

修改 `/workspace/src/Services/Cart/Leno.Cart.Application/Services/CartAppService.cs` L219-L231：

```csharp
// 旧：
// try
// {
//     var priceSnapshots = await _priceService.GetSkuPricesAsync(skuIds, ct);
//     priceMap = priceSnapshots.ToDictionary(p => p.SkuId);
// }
// catch (CartDomainException ex)
// {
//     _logger.LogWarning(ex, "购物车价格服务不可用，降级展示 UserId={UserId} ItemCount={ItemCount}",
//         cart.UserId, skuIds.Count);
//     priceServiceUnavailable = true;
// }

// 新：
try
{
    var priceSnapshots = await _priceService.GetSkuPricesAsync(skuIds, ct);
    priceMap = priceSnapshots.ToDictionary(p => p.SkuId);
}
catch (AntiCorruptionException ex)
{
    // 防腐层（HTTP/gRPC、超时、非 2xx）异常统一抛 AntiCorruptionException，进入降级展示分支
    _logger.LogWarning(ex, "购物车价格服务不可用，降级展示 UserId={UserId} ItemCount={ItemCount} ErrorCode={ErrorCode}",
        cart.UserId, skuIds.Count, ex.ErrorCode);
    priceServiceUnavailable = true;
}
```

并在文件顶部 `using` 区追加：

```csharp
using Leno.Infrastructure.AntiCorruption;
```

#### 步骤 4：运行测试，验证通过

```bash
dotnet test src/Services/Cart/Leno.Cart.Application.Tests/Leno.Cart.Application.Tests.csproj \
    --filter "FullyQualifiedName~PriceFailure|FullyQualifiedName~GetCartAsync_WhenPriceServiceThrowsAntiCorruptionException"
```

预期：原 `GetCartAsync_WhenPriceServiceThrows_ShouldDegradeAndMarkPriceUnavailable`（throw CartDomainException 版本）改为抛 AntiCorruptionException 后仍能命中降级分支；新测试通过。

#### 步骤 5：提交

```bash
git add src/Services/Cart/Leno.Cart.Application/Services/CartAppService.cs \
        src/Services/Cart/Leno.Cart.Application.Tests/CartAppServiceTests.cs
git commit -m "fix(cart): BuildCartDtoAsync 改 catch AntiCorruptionException 触发价格降级"
```

---

### P0-4：修复 2.4 匿名购物车结算预览 0 元结算漏洞

**问题证据**：
- BuildItemDto 不设置 PriceUnavailable：[file:///workspace/src/Services/Cart/Leno.Cart.Application/Services/AnonymousCartAppService.cs#L179-L196]
- PreviewCheckoutAsync 不校验 PriceUnavailable，缺价项按 0 元累加：[file:///workspace/src/Services/Cart/Leno.Cart.Application/Services/AnonymousCartAppService.cs#L113-L135]
- BuildCartDtoAsync 同样按 0 元累加选中项：[file:///workspace/src/Services/Cart/Leno.Cart.Application/Services/AnonymousCartAppService.cs#L173]
- 对比用户购物车版本的硬拦截：[file:///workspace/src/Services/Cart/Leno.Cart.Application/Services/CartAppService.cs#L133-L136]

**修复策略**：与 `CartAppService` 完全对齐：
1. `BuildItemDto` 缺价时显式 `PriceUnavailable = true`。
2. `BuildCartDtoAsync.SelectedTotalAmount` 仅累计 `!PriceUnavailable` 的选中项。
3. `PreviewCheckoutAsync` 在 groups 构建后校验 `Any(i => i.PriceUnavailable)`，命中则抛 `CartDomainException("CART_PRICE_UNAVAILABLE")`。

#### 步骤 1：编写测试（红）

新建 `/workspace/src/Services/Cart/Leno.Cart.Application.Tests/AnonymousCartAppServiceTests.cs`：

```csharp
using Leno.Cart.Application.DTOs;
using Leno.Cart.Application.Services;
using Leno.Cart.Domain.Aggregates;
using Leno.Cart.Domain.Exceptions;
using Leno.Cart.Domain.Repositories;
using Leno.Cart.Domain.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using CartAggregate = Leno.Cart.Domain.Aggregates.Cart;

namespace Leno.Cart.Application.Tests;

public class AnonymousCartAppServiceTests
{
    private readonly Mock<IAnonymousCartRepository> _repoMock = new();
    private readonly Mock<ICartPriceService> _priceMock = new();
    private readonly AnonymousCartAppService _sut;

    private static readonly Guid SkuId = Guid.NewGuid();
    private static readonly Guid SellerId = Guid.NewGuid();
    private const string SessionId = "session-1";

    public AnonymousCartAppServiceTests()
    {
        _sut = new AnonymousCartAppService(_repoMock.Object, _priceMock.Object);
    }

    private CartAggregate CreateAnonymousCartWithItem()
    {
        var cart = CartAggregate.CreateAnonymous(Guid.NewGuid());
        cart.AddItem(SkuId, 2, SellerId);
        return cart;
    }

    [Fact]
    public async Task GetCartAsync_PriceMapMissesSku_ShouldMarkPriceUnavailableTrue()
    {
        var cart = CreateAnonymousCartWithItem();
        _repoMock.Setup(r => r.GetAsync(SessionId, It.IsAny<CancellationToken>())).ReturnsAsync(cart);
        _repoMock.Setup(r => r.RefreshTtlAsync(SessionId, It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        _priceMock.Setup(p => p.GetSkuPricesAsync(It.IsAny<IEnumerable<Guid>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<SkuPriceSnapshot>());

        var result = await _sut.GetCartAsync(SessionId);

        result.Items[0].PriceUnavailable.Should().BeTrue();
        result.Items[0].Available.Should().BeFalse();
        result.Items[0].UnitPrice.Should().Be(0m);
        result.Items[0].Title.Should().Be("[价格加载失败]");
        // 选中项缺价不应计入可结算金额
        result.SelectedTotalAmount.Should().Be(0m);
    }

    [Fact]
    public async Task PreviewCheckoutAsync_PriceMapMissesSku_ShouldThrowCartDomainExceptionBlockingZeroCheckout()
    {
        var cart = CreateAnonymousCartWithItem();
        _repoMock.Setup(r => r.GetAsync(SessionId, It.IsAny<CancellationToken>())).ReturnsAsync(cart);
        _repoMock.Setup(r => r.RefreshTtlAsync(SessionId, It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        _priceMock.Setup(p => p.GetSkuPricesAsync(It.IsAny<IEnumerable<Guid>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<SkuPriceSnapshot>());

        var act = () => _sut.PreviewCheckoutAsync(SessionId);

        await act.Should().ThrowAsync<CartDomainException>()
            .WithMessage("*部分商品价格加载失败，暂不可结算*");
    }

    [Fact]
    public async Task PreviewCheckoutAsync_AllPricesAvailable_ShouldReturnNormalPreview()
    {
        var cart = CreateAnonymousCartWithItem();
        _repoMock.Setup(r => r.GetAsync(SessionId, It.IsAny<CancellationToken>())).ReturnsAsync(cart);
        _repoMock.Setup(r => r.RefreshTtlAsync(SessionId, It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        _priceMock.Setup(p => p.GetSkuPricesAsync(It.IsAny<IEnumerable<Guid>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[]
            {
                new SkuPriceSnapshot
                {
                    SkuId = SkuId,
                    Price = 19.9m,
                    Currency = "CNY",
                    Available = true,
                    Title = "在售商品",
                    MainImageUrl = "https://img.example.com/a.jpg",
                    SellerId = SellerId
                }
            });

        var result = await _sut.PreviewCheckoutAsync(SessionId);

        result.Groups.Should().HaveCount(1);
        result.Groups[0].Items[0].PriceUnavailable.Should().BeFalse();
        result.Groups[0].SubtotalAmount.Should().Be(19.9m * 2);
        result.TotalAmount.Should().Be(19.9m * 2);
    }

    [Fact]
    public async Task PreviewCheckoutAsync_PartialPriceMissing_ShouldThrowBlockingCheckout()
    {
        var cart = CartAggregate.CreateAnonymous(Guid.NewGuid());
        cart.AddItem(SkuId, 2, SellerId);
        var sku2 = Guid.NewGuid();
        cart.AddItem(sku2, 1, SellerId);
        _repoMock.Setup(r => r.GetAsync(SessionId, It.IsAny<CancellationToken>())).ReturnsAsync(cart);
        _repoMock.Setup(r => r.RefreshTtlAsync(SessionId, It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        _priceMock.Setup(p => p.GetSkuPricesAsync(It.IsAny<IEnumerable<Guid>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[]
            {
                new SkuPriceSnapshot
                {
                    SkuId = SkuId,
                    Price = 19.9m,
                    Currency = "CNY",
                    Available = true,
                    Title = "在售商品",
                    MainImageUrl = string.Empty,
                    SellerId = SellerId
                }
                // sku2 未返回
            });

        var act = () => _sut.PreviewCheckoutAsync(SessionId);

        await act.Should().ThrowAsync<CartDomainException>()
            .WithMessage("*部分商品价格加载失败，暂不可结算*");
    }
}
```

#### 步骤 2：运行测试，验证失败

```bash
dotnet test src/Services/Cart/Leno.Cart.Application.Tests/Leno.Cart.Application.Tests.csproj \
    --filter "FullyQualifiedName~AnonymousCartAppServiceTests"
```

预期失败：
- `PreviewCheckoutAsync_PriceMapMissesSku_ShouldThrowCartDomainExceptionBlockingZeroCheckout` 实际不抛异常，返回 0 元结算单
- `GetCartAsync_PriceMapMissesSku_ShouldMarkPriceUnavailableTrue` 实际 `PriceUnavailable=false`

#### 步骤 3：实现修复代码（绿）

修改 `/workspace/src/Services/Cart/Leno.Cart.Application/Services/AnonymousCartAppService.cs` 三个方法：

替换 `PreviewCheckoutAsync`（L99-L135）：

```csharp
public async Task<CheckoutPreviewDto> PreviewCheckoutAsync(string sessionId, CancellationToken ct = default)
{
    var cart = await RequireCartAsync(sessionId, ct);
    await _cartRepository.RefreshTtlAsync(sessionId, ct);
    var selectedItems = cart.Items.Where(i => i.IsSelected).ToList();
    if (selectedItems.Count == 0)
    {
        return new CheckoutPreviewDto();
    }

    var priceSnapshots = await _priceService.GetSkuPricesAsync(selectedItems.Select(i => i.SkuId), ct);
    var priceMap = priceSnapshots.ToDictionary(p => p.SkuId);

    var groups = selectedItems
        .GroupBy(i => i.SellerId)
        .Select(g =>
        {
            var items = g.Select(i => BuildItemDto(i, priceMap)).ToList();
            return new CheckoutGroupDto
            {
                SellerId = g.Key,
                Items = items,
                // 与 CartAppService 对齐：仅累计价格可用项
                SubtotalAmount = items.Where(i => !i.PriceUnavailable).Sum(i => i.Subtotal),
                Currency = items.FirstOrDefault()?.Currency ?? "CNY"
            };
        })
        .ToList();

    // 与 CartAppService 对齐：缺价项硬拦截，避免 0 元结算单
    if (groups.SelectMany(g => g.Items).Any(i => i.PriceUnavailable))
    {
        throw new CartDomainException("部分商品价格加载失败，暂不可结算", "CART_PRICE_UNAVAILABLE");
    }

    return new CheckoutPreviewDto
    {
        Groups = groups,
        TotalAmount = groups.Sum(g => g.SubtotalAmount),
        Currency = groups.FirstOrDefault()?.Currency ?? "CNY",
        TotalCount = selectedItems.Sum(i => i.Quantity)
    };
}
```

替换 `BuildCartDtoAsync`（L156-L177）：

```csharp
private async Task<CartDto> BuildCartDtoAsync(CartAggregate cart, CancellationToken ct)
{
    var skuIds = cart.Items.Select(i => i.SkuId).Distinct().ToList();
    Dictionary<Guid, SkuPriceSnapshot> priceMap = new();
    var priceServiceUnavailable = false;

    if (skuIds.Count > 0)
    {
        try
        {
            var priceSnapshots = await _priceService.GetSkuPricesAsync(skuIds, ct);
            priceMap = priceSnapshots.ToDictionary(p => p.SkuId);
        }
        catch (AntiCorruptionException ex)
        {
            // 与 CartAppService 对齐：降级展示，标记 PriceUnavailable=true
            priceServiceUnavailable = true;
        }
    }

    var itemDtos = cart.Items
        .Select(i => BuildItemDto(i, priceMap, priceServiceUnavailable))
        .ToList();

    // 与 CartAppService 对齐：选中项总金额仅累计价格可用项
    var selectedTotalAmount = itemDtos
        .Where(i => i.IsSelected && !i.PriceUnavailable)
        .Sum(i => i.Subtotal);

    return new CartDto
    {
        Id = cart.Id,
        UserId = cart.UserId,
        Items = itemDtos,
        SelectedTotalAmount = selectedTotalAmount,
        Currency = itemDtos.FirstOrDefault()?.Currency ?? "CNY",
        TotalCount = itemDtos.Sum(i => i.Quantity)
    };
}
```

替换 `BuildItemDto` 单参数版本（L179-L196）：

```csharp
private static CartItemDto BuildItemDto(CartItem item, Dictionary<Guid, SkuPriceSnapshot> priceMap, bool priceServiceUnavailable = false)
{
    // 与 CartAppService 对齐：价格服务整体不可用或单 SKU 未命中，标记 PriceUnavailable=true
    if (priceServiceUnavailable || !priceMap.TryGetValue(item.SkuId, out var snapshot))
    {
        return new CartItemDto
        {
            Id = item.Id,
            SkuId = item.SkuId,
            SellerId = item.SellerId,
            Quantity = item.Quantity,
            IsSelected = item.IsSelected,
            SourceCartItemId = item.SourceCartItemId,
            UnitPrice = 0,
            Currency = "CNY",
            Title = "[价格加载失败]",
            MainImageUrl = string.Empty,
            Available = false,
            PriceUnavailable = true
        };
    }

    return new CartItemDto
    {
        Id = item.Id,
        SkuId = item.SkuId,
        SellerId = item.SellerId,
        Quantity = item.Quantity,
        IsSelected = item.IsSelected,
        SourceCartItemId = item.SourceCartItemId,
        UnitPrice = snapshot!.Price,
        Currency = snapshot!.Currency,
        Title = snapshot!.Title,
        MainImageUrl = snapshot!.MainImageUrl,
        Available = snapshot!.Available,
        PriceUnavailable = false
    };
}
```

并在文件顶部 `using` 区追加：

```csharp
using Leno.Infrastructure.AntiCorruption;
```

#### 步骤 4：运行测试，验证通过

```bash
dotnet test src/Services/Cart/Leno.Cart.Application.Tests/Leno.Cart.Application.Tests.csproj \
    --filter "FullyQualifiedName~AnonymousCartAppServiceTests"
```

预期：4 个新测试全部通过。

#### 步骤 5：提交

```bash
git add src/Services/Cart/Leno.Cart.Application/Services/AnonymousCartAppService.cs \
        src/Services/Cart/Leno.Cart.Application.Tests/AnonymousCartAppServiceTests.cs
git commit -m "fix(cart): 匿名购物车结算预览补齐 PriceUnavailable 标记与硬拦截，避免 0 元结算"
```

---

### P0-5：修复 2.5 聚合不变量违反：AddItem 不校验品类上限

**问题证据**：
- AddItem 三参数重载无品类上限校验：[file:///workspace/src/Services/Cart/Leno.Cart.Domain/Aggregates/Cart.cs#L67-L91]
- maxVariety=50 仅在 MergeFrom 内部生效：[file:///workspace/src/Services/Cart/Leno.Cart.Domain/Aggregates/Cart.cs#L232]

**修复策略**：将 `maxVariety` 提升为聚合根常量 `MaxVariety = 50`，在 `AddItem` 新增分支前校验；`MergeFrom` 复用同一常量。

#### 步骤 1：编写测试（红）

在 `/workspace/src/Services/Cart/Leno.Cart.Domain.Tests/CartTests.cs` 末尾追加：

```csharp
[Fact]
public void AddItem_ExceedsMaxVariety_ShouldThrowCartDomainException()
{
    // Arrange：填满 50 个 SKU（已达上限）
    var cart = CreateCart();
    for (int i = 0; i < 50; i++)
    {
        cart.AddItem(Guid.NewGuid(), 1, Guid.NewGuid());
    }

    // Act：第 51 个不同 SKU 应被拒绝
    var act = () => cart.AddItem(Guid.NewGuid(), 1, Guid.NewGuid());

    // Assert
    act.Should().Throw<CartDomainException>()
        .WithMessage("*品类*")
        .WithMessage("*50*");
}

[Fact]
public void AddItem_AtMaxVarietyButExistingSku_ShouldMergeWithoutThrowing()
{
    // Arrange：达上限后追加已有 SKU 不应触发上限
    var cart = CreateCart();
    var firstSku = Guid.NewGuid();
    cart.AddItem(firstSku, 1, Guid.NewGuid());
    for (int i = 0; i < 49; i++)
    {
        cart.AddItem(Guid.NewGuid(), 1, Guid.NewGuid());
    }
    cart.Items.Should().HaveCount(50);

    // Act
    cart.AddItem(firstSku, 2, Guid.NewGuid());

    // Assert：合并数量，不新增项
    cart.Items.Should().HaveCount(50);
    cart.Items.First(i => i.SkuId == firstSku).Quantity.Should().Be(3);
}

[Fact]
public void AddItem_ExactlyAtMaxVariety_ShouldAllowFillingToLimit()
{
    var cart = CreateCart();

    for (int i = 0; i < 50; i++)
    {
        cart.AddItem(Guid.NewGuid(), 1, Guid.NewGuid());
    }

    cart.Items.Should().HaveCount(50);
}
```

#### 步骤 2：运行测试，验证失败

```bash
dotnet test src/Services/Cart/Leno.Cart.Domain.Tests/Leno.Cart.Domain.Tests.csproj \
    --filter "FullyQualifiedName~AddItem_ExceedsMaxVariety_ShouldThrowCartDomainException|FullyQualifiedName~AddItem_AtMaxVarietyButExistingSku_ShouldMergeWithoutThrowing"
```

预期失败：`AddItem_ExceedsMaxVariety_ShouldThrowCartDomainException` 不抛异常（当前无上限校验），可加到 100+ 项。

#### 步骤 3：实现修复代码（绿）

修改 `/workspace/src/Services/Cart/Leno.Cart.Domain/Aggregates/Cart.cs`：

在类内增加常量：

```csharp
/// <summary>购物车品类数量上限（不同 SKU 数）。</summary>
private const int MaxVariety = 50;
```

替换 `AddItem(Guid, int, Guid)` 方法（L67-L91）：

```csharp
public void AddItem(Guid skuId, int quantity, Guid sellerId)
{
    if (skuId == Guid.Empty)
    {
        throw new ArgumentException("SkuId 不可为空", nameof(skuId));
    }

    var existing = _items.FirstOrDefault(i => i.SkuId == skuId);
    if (existing is not null)
    {
        // 合并数量，校验上限
        var merged = existing.Quantity + quantity;
        if (merged > 99)
        {
            throw new CartDomainException($"SKU {skuId} 合并后数量 {merged} 超过上限 99", "CART_QTY_OVERFLOW");
        }

        existing.SetQuantity(merged);
        return;
    }

    // 新增 SKU 前校验品类上限（聚合不变量统一由聚合根保证）
    if (_items.Count >= MaxVariety)
    {
        throw new CartDomainException($"购物车品类数量已达上限 {MaxVariety}", "CART_VARIETY_LIMIT");
    }

    var item = new CartItem(Guid.NewGuid(), Id, skuId, sellerId, quantity);
    _items.Add(item);
    AddDomainEvent(new SkuAddedToCartEvent(Id, skuId));
}
```

替换 `MergeFrom` 方法（L229-L257），复用常量并删除局部 `const int maxVariety = 50;`：

```csharp
public int MergeFrom(Cart anonymousCart)
{
    ArgumentNullException.ThrowIfNull(anonymousCart);

    var mergedCount = 0;
    foreach (var item in anonymousCart.Items)
    {
        // 检查品类上限（新增项时）
        var existing = FindItem(item.SkuId);
        if (existing is null && _items.Count >= MaxVariety)
        {
            throw new CartDomainException($"购物车品类数量已达上限 {MaxVariety}", "CART_VARIETY_LIMIT");
        }

        AddItem(item.SkuId, item.Quantity, item.SellerId);

        // 选中状态：任一来源选中则选中
        if (item.IsSelected)
        {
            var merged = FindItem(item.SkuId);
            merged?.Select();
        }

        mergedCount++;
    }

    return mergedCount;
}
```

#### 步骤 4：运行测试，验证通过

```bash
dotnet test src/Services/Cart/Leno.Cart.Domain.Tests/Leno.Cart.Domain.Tests.csproj
```

预期：原有 `MergeFrom_VarietyExceedsLimit_ShouldThrowException` 与新增 3 个测试全部通过。

#### 步骤 5：提交

```bash
git add src/Services/Cart/Leno.Cart.Domain/Aggregates/Cart.cs \
        src/Services/Cart/Leno.Cart.Domain.Tests/CartTests.cs
git commit -m "fix(cart): AddItem 校验品类上限 MaxVariety=50，聚合不变量统一保证"
```

---

## P1 修复清单（任务清单格式）

### P1-1：3.1 MergeAnonymousCartAsync 跨存储非原子操作

- **审计位置**：[file:///workspace/docs/superpowers/specs/2026-07-21-code-audit/03-cart.md#L163-L169]
- **代码位置**：[file:///workspace/src/Services/Cart/Leno.Cart.Application/Services/CartAppService.cs#L172-L184]
- **根因**：DB 保存成功后 `RemoveAsync(Redis)` 失败被静默吞掉（P0-2 修复后改为抛异常，但仍非原子），下次同 anonymousId 触发合并时匿名购物车仍存在，`MergeFrom` 再次累加数量翻倍。
- **修复步骤**：
  1. 新增 `cart_merge_records` 表（anonymousId PK + merged_at + merged_count），由 `CartAppService.MergeAnonymousCartAsync` 在 `SaveEntitiesAsync` 前查询；若已合并则跳过 `MergeFrom` 仅返回当前用户购物车。
  2. `MergeFrom` 调用前在事务内 `INSERT cart_merge_records(anonymousId, ...)`，依赖主键唯一约束防止重复合并。
  3. 调整 `RemoveAsync` 调用：合并记录入库后即视为完成，Redis 删除失败时记录日志但不回滚事务（最终一致：下次合并因记录存在被跳过，匿名购物车 Redis Key 7 天 TTL 自动过期）。
- **影响范围**：Cart BC `CartAppService.MergeAnonymousCartAsync`、新增 `CartMergeRecord` 实体与 EF 配置、CartDbContext。
- **验证方法**：单元测试构造 DB 成功 + Redis RemoveAsync 抛异常场景，验证第二次调用同一 anonymousId 不触发 MergeFrom，购物车数量不翻倍。

### P1-2：3.2 ProductEventConsumer 三个消费者 N+1 查询 + UpdateAsync 滥用

- **审计位置**：[file:///workspace/docs/superpowers/specs/2026-07-21-code-audit/03-cart.md#L171-L177]
- **代码位置**：[file:///workspace/src/Services/Cart/Leno.Cart.Infrastructure/Consumers/ProductEventConsumer.cs#L49-L67]、[file:///workspace/src/Services/Cart/Leno.Cart.Infrastructure/Consumers/ProductEventConsumer.cs#L105-L124]、[file:///workspace/src/Services/Cart/Leno.Cart.Infrastructure/Consumers/ProductEventConsumer.cs#L161-L198]；[file:///workspace/src/Services/Cart/Leno.Cart.Infrastructure/Repositories/EfCoreCartRepository.cs#L38-L42]
- **根因**：`foreach (cartId in batch) { var cart = await GetByIdAsync(cartId); ... await UpdateAsync(cart); }` 100 个购物车 = 100 次 SELECT + 100 次 Update；`UpdateAsync` 对已跟踪实体调 `_context.Carts.Update()` 强制全字段 Modified。
- **修复步骤**：
  1. `ICartRepository` 新增 `GetByIdsAsync(IReadOnlyCollection<Guid> cartIds, ct)` 批量加载方法（`Where(c => cartIds.Contains(c.Id))` + `Include(c => c.Items)`）。
  2. 三个消费者重构：`var carts = await _cartRepository.GetByIdsAsync(batch, ct); foreach (var cart in carts) { cart.MarkInvalid/MarkValid/RefreshDisplaySnapshot(...); } await _unitOfWork.SaveEntitiesAsync(ct);`。
  3. `EfCoreCartRepository.UpdateAsync` 移除 `_context.Carts.Update(aggregate)` 调用，依赖 ChangeTracker 自动检测变更；该方法保留供显式附加场景使用，但消费者不再调用。
- **影响范围**：Cart.Infrastructure `ProductEventConsumer`、`ICartRepository`/`EfCoreCartRepository`。
- **验证方法**：单元测试 mock `ICartRepository.GetByIdsAsync`，验证消费者仅调用 1 次；EF Core 集成测试验证生成的 SQL 不含 100 次 SELECT。

### P1-3：3.3 ProductUpdatedEventConsumer 每 SKU 一次 HTTP 快照查询

- **审计位置**：[file:///workspace/docs/superpowers/specs/2026-07-21-code-audit/03-cart.md#L179-L185]
- **代码位置**：[file:///workspace/src/Services/Cart/Leno.Cart.Infrastructure/Consumers/ProductEventConsumer.cs#L166-L198]
- **根因**：`foreach (skuId in SkuIds) { snapshot = await _snapshotAntiCorruption.GetSkuSnapshotAsync(skuId, ct); ... }` 单事件 N SKU = N 次 HTTP。
- **修复步骤**：
  1. `IProductSnapshotAntiCorruption` 新增 `GetSkuSnapshotsAsync(IReadOnlyCollection<Guid> skuIds, ct)` 批量接口，与 `ICartPriceService.GetSkuPricesAsync` 对齐。
  2. `ProductSnapshotAntiCorruptionService`、`GrpcProductSnapshotAntiCorruptionClient`、`ProductSnapshotDispatcherAdapter` 同步实现批量方法。
  3. `ProductUpdatedEventConsumer` 改为先 `var snapshots = await _snapshotAntiCorruption.GetSkuSnapshotsAsync(integrationEvent.SkuIds, ct); var snapshotMap = snapshots.ToDictionary(s => s.SkuId);`，循环内查字典。
- **影响范围**：Cart.Application `IProductSnapshotAntiCorruption`、Cart.Infrastructure 三个 ACL 实现 + Consumer。
- **验证方法**：单元测试 mock `_snapshotAntiCorruption.GetSkuSnapshotsAsync`，验证单事件仅 1 次 ACL 调用。

### P1-4：3.4 匿名购物车 _domainEvents 永不清理，Redis JSON 单调增长

- **审计位置**：[file:///workspace/docs/superpowers/specs/2026-07-21-code-audit/03-cart.md#L187-L193]
- **代码位置**：[file:///workspace/src/Services/Cart/Leno.Cart.Infrastructure/Repositories/RedisAnonymousCartRepository.cs#L54-L69]；[file:///workspace/src/BuildingBlocks/Leno.SharedKernel/Abstractions/AggregateRoot.cs#L8-L17]
- **根因**：`SaveAsync` 直接 `JsonSerializer.Serialize(cart)`，包含 `_domainEvents`；`ClearDomainEvents()` 仅在 `SaveChangesWithOutboxAsync` 中调用，匿名购物车不走此路径。
- **修复步骤**：
  1. `RedisAnonymousCartRepository.SaveAsync` 序列化前显式 `cart.ClearDomainEvents()`。
  2. 同样适用于 `RedisCartCache.SetAsync`（P2-1 中决定是否保留该类）。
- **影响范围**：Cart.Infrastructure `RedisAnonymousCartRepository`。
- **验证方法**：单元测试调用 `SaveAsync` 后 mock 验证 `JsonSerializer.Serialize` 入参的 `cart.DomainEvents` 为空集合；或序列化后反序列化 JSON 不含 `domainEvents` 字段。

### P1-5：3.5 CartSkuIndexService Redis Set 无 TTL

- **审计位置**：[file:///workspace/docs/superpowers/specs/2026-07-21-code-audit/03-cart.md#L195-L201]
- **代码位置**：[file:///workspace/src/Services/Cart/Leno.Cart.Infrastructure/Services/CartSkuIndexService.cs#L27-L49]
- **根因**：`SetAddAsync` 不设置 Key 过期；购物车删除后索引残留。
- **修复步骤**：
  1. `AddAsync` 在 `SetAddAsync` 后调用 `KeyExpireAsync(key, TimeSpan.FromDays(30))`，每次 Add 刷新 TTL。
  2. `ClearItemsBySourceIds` 完全清空购物车时同步调用 `RemoveAsync(skuId, cartId)` 维护索引（聚合根事件未覆盖此场景，可在应用层补一次显式调用）。
- **影响范围**：Cart.Infrastructure `CartSkuIndexService`、`CartAppService` 清空购物车路径。
- **验证方法**：单元测试 mock `IDatabase`，验证 `KeyExpireAsync` 被调用且 TTL 30 天。

### P1-6：3.6 CartSkuIndexService 异常处理与 RedisAnonymousCartRepository 不一致

- **审计位置**：[file:///workspace/docs/superpowers/specs/2026-07-21-code-audit/03-cart.md#L203-L209]
- **代码位置**：[file:///workspace/src/Services/Cart/Leno.Cart.Infrastructure/Services/CartSkuIndexService.cs#L27-L49]
- **根因**：`AddAsync`/`RemoveAsync`/`GetCartIdsBySkuAsync` 不 catch，Redis 故障直接抛 `RedisConnectionException`；P0-2 修复后 `RedisAnonymousCartRepository` 改抛 `CartInfrastructureException`，策略仍不一致。
- **修复步骤**：与 P0-2 对齐策略：`CartSkuIndexService` 各方法 catch `RedisConnectionException` 后包装为 `CartInfrastructureException("CART_REDIS_UNAVAILABLE")` 上抛。`GetCartIdsBySkuAsync` 在 ProductEventConsumer 中被调用，异常上抛触发 MassTransit 重试与死信，符合"故障上抛 + 全局兜底"策略。
- **影响范围**：Cart.Infrastructure `CartSkuIndexService`。
- **验证方法**：单元测试 mock Redis 抛 `RedisConnectionException`，验证 `AddAsync`/`RemoveAsync`/`GetCartIdsBySkuAsync` 抛 `CartInfrastructureException`。

### P1-7：3.7 AnonymousCartsController 无鉴权 + 无限流

- **审计位置**：[file:///workspace/docs/superpowers/specs/2026-07-21-code-audit/03-cart.md#L211-L220]
- **代码位置**：[file:///workspace/src/Services/Cart/Leno.Cart.Api/Controllers/AnonymousCartsController.cs#L12-L31]、[file:///workspace/src/Services/Cart/Leno.Cart.Api/Controllers/AnonymousCartsController.cs#L34-L85]
- **根因**：无 `[Authorize]`、无 `[EnableRateLimiting]`；sessionId 出现在 URL 路径。
- **修复步骤**：
  1. `Program.cs` 配置 `AddRateLimiter`，定义 `"anonymous-cart"` 策略（IP 维度，10 次/分钟）。
  2. `AnonymousCartsController` 类上添加 `[EnableRateLimiting("anonymous-cart")]`。
  3. 与 P2-6 协同：sessionId 改为 `X-Cart-Session` 请求头传递。
- **影响范围**：Cart.Api `AnonymousCartsController`、`Program.cs`。
- **验证方法**：集成测试连续 11 次 `POST /api/cart/anonymous` 同 IP，第 11 次返回 429。

### P1-8：3.8 EfCoreCartRepository 读写未分离 AsNoTracking

- **审计位置**：[file:///workspace/docs/superpowers/specs/2026-07-21-code-audit/03-cart.md#L222-L228]
- **代码位置**：[file:///workspace/src/Services/Cart/Leno.Cart.Infrastructure/Repositories/EfCoreCartRepository.cs#L22-L31]
- **根因**：`GetByIdAsync`/`GetByUserIdAsync` 无 `AsNoTracking()`，只读路径强制 ChangeTracker 跟踪。
- **修复步骤**：
  1. `ICartRepository` 新增 `GetByIdReadOnlyAsync`/`GetByUserIdReadOnlyAsync`（AsNoTracking），原方法保留供写路径使用。
  2. `CartAppService.GetCartAsync`/`PreviewCheckoutAsync`/`BuildCartDtoAsync` 调用只读版本。
  3. `ProductEventConsumer` 仍用跟踪版本（需 MarkInvalid/MarkValid 后保存）。
- **影响范围**：Cart.Domain `ICartRepository`、Cart.Infrastructure `EfCoreCartRepository`、Cart.Application `CartAppService`。
- **验证方法**：EF Core 集成测试验证只读路径 `ChangeTracker.Entries().Count() == 0`。

### P1-9：3.9 Cart.AddItem 六参数重载 unitPrice 死参数 + 快照回退风险

- **审计位置**：[file:///workspace/docs/superpowers/specs/2026-07-21-code-audit/03-cart.md#L230-L246]
- **代码位置**：[file:///workspace/src/Services/Cart/Leno.Cart.Domain/Aggregates/Cart.cs#L104-L108]
- **根因**：`unitPrice` 接受但不持久化（违反用户规则 §2 死参数禁令）；已存在 SKU 合并时 `RefreshDisplaySnapshot` 用调用方传入的旧 title/ imageUrl 覆盖更新过的快照。
- **修复步骤**：
  1. 删除六参数 `AddItem` 重载。
  2. 测试代码改用 `AddItem(skuId, quantity, sellerId)` + `RefreshDisplaySnapshot(title, mainImageUrl)`。
  3. grep 全代码库（含 Tests）确认无调用方残留。
- **影响范围**：Cart.Domain `Cart`、Cart 域 Tests。
- **验证方法**：编译通过；grep `AddItem.*unitPrice` 零命中。

### P1-10：3.10 CartInternalQueryService 金额转分截断而非四舍五入

- **审计位置**：[file:///workspace/docs/superpowers/specs/2026-07-21-code-audit/03-cart.md#L248-L254]
- **代码位置**：[file:///workspace/src/Services/Cart/Leno.Cart.Application/InternalQueryServices/CartInternalQueryService.cs#L31]、[file:///workspace/src/Services/Cart/Leno.Cart.Application/InternalQueryServices/CartInternalQueryService.cs#L33]、[file:///workspace/src/Services/Cart/Leno.Cart.Application/InternalQueryServices/CartInternalQueryService.cs#L47]、[file:///workspace/src/Services/Cart/Leno.Cart.Application/InternalQueryServices/CartInternalQueryService.cs#L50]
- **根因**：`(long)(value * 100)` 向零截断，丢失分位。
- **修复步骤**：4 处 `(long)(x * 100)` 改为 `(long)Math.Round(x * 100m, MidpointRounding.AwayFromZero)`。
- **影响范围**：Cart.Application `CartInternalQueryService`。
- **验证方法**：单元测试用例 `19.999m → 2000`（原为 1999）、`-19.999m → -2000`、`0.005m → 1`。

### P1-11：3.11 CartInternalQueryService.GetCartSnapshotAsync 永不返回 null

- **审计位置**：[file:///workspace/docs/superpowers/specs/2026-07-21-code-audit/03-cart.md#L256-L262]
- **代码位置**：[file:///workspace/src/Services/Cart/Leno.Cart.Application/InternalQueryServices/CartInternalQueryService.cs#L19-L35]；[file:///workspace/src/Services/Cart/Leno.Cart.Api/GrpcServices/CartGrpcService.cs#L38-L41]
- **根因**：`_cartAppService.GetCartAsync` → `GetOrCreateCartAsync` 在购物车不存在时**创建**新空购物车，因此 `cart is null` 永远为 false；gRPC `NotFound` 分支不可达。
- **修复步骤**：
  1. `ICartAppService` 新增 `FindCartAsync(userId, ct)` 返回 `CartDto?`（不存在返回 null 不创建），与 `GetCartAsync` 区分语义。
  2. `CartInternalQueryService.GetCartSnapshotAsync` 改用 `FindCartAsync`；空购物车（items 为空）也应返回 null，明确"无有效购物车"语义。
  3. `CartGrpcService` 保留 `NotFound` 分支，现可正确触发。
- **影响范围**：Cart.Application `ICartAppService`/`CartAppService`/`CartInternalQueryService`、Cart.Api `CartGrpcService`。
- **验证方法**：单元测试用户无购物车时 `GetCartSnapshotAsync` 返回 null；gRPC 端返回 `StatusCode.NotFound`。

### P1-12：3.12 ClearSelectedItems 死代码 + 未发布 SkuRemovedFromCartEvent

- **审计位置**：[file:///workspace/docs/superpowers/specs/2026-07-21-code-audit/03-cart.md#L264-L270]
- **代码位置**：[file:///workspace/src/Services/Cart/Leno.Cart.Domain/Aggregates/Cart.cs#L193-L204]
- **根因**：`ClearSelectedItems` 不发布 `SkuRemovedFromCartEvent`；当前无生产调用方（订单创建走 `ClearItemsBySourceIds`）。
- **修复步骤**：
  1. 在 `ClearSelectedItems` 的 `foreach` 中 `AddDomainEvent(new SkuRemovedFromCartEvent(Id, item.SkuId))`，与 `RemoveItem` 行为一致。
  2. P0-1 的 `CartSkuIndexDomainEventDispatcher` 会自动维护反向索引。
- **影响范围**：Cart.Domain `Cart`。
- **验证方法**：单元测试 `ClearSelectedItems` 后 `DomainEvents` 包含对应 `SkuRemovedFromCartEvent`。

### P1-13：3.13 CircuitBreakerState 单例工厂读取 IOptionsMonitor 时机错误

- **审计位置**：[file:///workspace/docs/superpowers/specs/2026-07-21-code-audit/03-cart.md#L272-L278]
- **代码位置**：[file:///workspace/src/Services/Cart/Leno.Cart.Infrastructure/Dependencies/ServiceCollectionExtensions.cs#L78-L87]
- **根因**：KeyedSingleton 工厂构造时读取 `IOptionsMonitor.CurrentValue` 一次，熔断阈值被冻结，Consul KV 热更新不生效。
- **修复步骤**：
  1. `CircuitBreakerState` 构造函数改为接收 `IOptionsMonitor<AntiCorruptionOptions>` 引用（而非 `FailureThreshold/SuccessThreshold/OpenDuration` 值）。
  2. 每次状态变更前读取 `options.CurrentValue.CircuitBreaker` 最新值；公共方法 `OnFailure`/`OnSuccess` 内重读阈值。
  3. `ServiceCollectionExtensions` 工厂改为注入 `IOptionsMonitor<AntiCorruptionOptions>`：`new CircuitBreakerState("product", optionsMonitor)`。
- **影响范围**：Cart.Infrastructure `ServiceCollectionExtensions`、`Leno.Infrastructure.AntiCorruption.CircuitBreakerState`（共享层）。
- **验证方法**：单元测试构造后修改 `IOptionsMonitor.CurrentValue.CircuitBreaker.FailureThreshold`，验证 `OnFailure` 行为按新阈值。

### P1-14：3.14 CartAppService 多币种聚合错误

- **审计位置**：[file:///workspace/docs/superpowers/specs/2026-07-21-code-audit/03-cart.md#L280-L286]
- **代码位置**：[file:///workspace/src/Services/Cart/Leno.Cart.Application/Services/CartAppService.cs#L127]、[file:///workspace/src/Services/Cart/Leno.Cart.Application/Services/CartAppService.cs#L142]、[file:///workspace/src/Services/Cart/Leno.Cart.Application/Services/CartAppService.cs#L249]
- **根因**：`Currency = itemDtos.FirstOrDefault()?.Currency ?? "CNY"` 取第一项币种作为整单币种；混币种场景金额与币种不匹配。
- **修复步骤**：
  1. `CheckoutPreviewDto`/`CartDto` 增加按币种分组的 `Dictionary<string, decimal> SubtotalsByCurrency` 字段。
  2. `TotalAmount`/`SelectedTotalAmount` 仅在单币种时填充；混币种时抛 `CartDomainException("CART_MIXED_CURRENCY")` 阻止结算，前端按 `SubtotalsByCurrency` 展示。
  3. `AnonymousCartAppService` 同步对齐。
- **影响范围**：Cart.Application `CartDto`/`CheckoutPreviewDto`/`CartAppService`/`AnonymousCartAppService`。
- **验证方法**：单元测试混币种购物车抛 `CART_MIXED_CURRENCY`；单币种场景正常返回。

### P1-15：3.15 匿名购物车 BuildCartDtoAsync 不处理 AntiCorruptionException

- **审计位置**：[file:///workspace/docs/superpowers/specs/2026-07-21-code-audit/03-cart.md#L288-L294]
- **代码位置**：[file:///workspace/src/Services/Cart/Leno.Cart.Application/Services/AnonymousCartAppService.cs#L156-L177]
- **根因**：`_priceService.GetSkuPricesAsync(skuIds, ct)` 直接 await，无 try/catch，价格服务故障时 `AntiCorruptionException` 直接冒泡到控制器。
- **修复步骤**：在 P0-4 修复中已对齐（新增 `try/catch (AntiCorruptionException ex)` 降级分支，标记 `priceServiceUnavailable = true`）。本项作为 P0-4 的协同子项，无需额外改动，仅在 P0-4 完成后回归验证。
- **影响范围**：Cart.Application `AnonymousCartAppService`。
- **验证方法**：P0-4 测试用例已覆盖；新增 `GetCartAsync_WhenPriceServiceThrowsAntiCorruptionException_ShouldDegrade` 单元测试验证。

---

## P2 修复清单（任务清单格式，可简化）

### P2-1：4.1 RedisCartCache 注册但全局未使用

- **审计位置**：[file:///workspace/docs/superpowers/specs/2026-07-21-code-audit/03-cart.md#L298-L304]
- **代码位置**：[file:///workspace/src/Services/Cart/Leno.Cart.Infrastructure/Dependencies/ServiceCollectionExtensions.cs#L150]；[file:///workspace/src/Services/Cart/Leno.Cart.Infrastructure/Caching/RedisCartCache.cs]
- **修复步骤**：删除 `RedisCartCache` 类与 `services.AddSingleton<RedisCartCache>();` 注册；若后续需要读路径缓存，按 P1-8 的 CQRS 读写分离设计独立接入。
- **影响范围**：Cart.Infrastructure `Caching/RedisCartCache.cs`、`ServiceCollectionExtensions`。
- **验证方法**：grep `RedisCartCache` 全代码库零命中；编译通过。

### P2-2：4.2 Cart.AddItem 与 MergeFrom 重复 FindItem

- **审计位置**：[file:///workspace/docs/superpowers/specs/2026-07-21-code-audit/03-cart.md#L306-L312]
- **代码位置**：[file:///workspace/src/Services/Cart/Leno.Cart.Domain/Aggregates/Cart.cs#L106-L107]、[file:///workspace/src/Services/Cart/Leno.Cart.Domain/Aggregates/Cart.cs#L238-L244]
- **修复步骤**：将 `FindItem` 改为 `TryGetItem(Guid skuId, out CartItem? item)`，`MergeFrom`/六参数 `AddItem` 复用引用避免二次扫描。注：六参数 `AddItem` 在 P1-9 中删除后此项影响范围缩小至 `MergeFrom`。
- **影响范围**：Cart.Domain `Cart`。
- **验证方法**：单元测试不变；性能基准对比 `MergeFrom(50 项)` 减少 50 次扫描。

### P2-3：4.3 ConfigureAwait 使用不一致

- **审计位置**：[file:///workspace/docs/superpowers/specs/2026-07-21-code-audit/03-cart.md#L314-L320]
- **代码位置**：[file:///workspace/src/Services/Cart/Leno.Cart.Infrastructure/Services/Grpc/GrpcCartPriceService.cs#L59]、[file:///workspace/src/Services/Cart/Leno.Cart.Infrastructure/Services/Grpc/GrpcProductSnapshotAntiCorruptionClient.cs#L52]、[file:///workspace/src/Services/Cart/Leno.Cart.Api/GrpcServices/CartGrpcService.cs#L36]
- **修复步骤**：统一移除 gRPC 客户端类与 gRPC 服务端的 `ConfigureAwait(false)`（ASP.NET Core 无 SynchronizationContext，.NET 10 已无需该调用），保持代码风格一致。
- **影响范围**：Cart.Infrastructure `Services/Grpc/*`、Cart.Api `GrpcServices/CartGrpcService`。
- **验证方法**：grep `ConfigureAwait(false)` 在 Cart BC 零命中。

### P2-4：4.4 CartItem.IsValid 字段初始化器与构造函数重复赋值

- **审计位置**：[file:///workspace/docs/superpowers/specs/2026-07-21-code-audit/03-cart.md#L322-L328]
- **代码位置**：[file:///workspace/src/Services/Cart/Leno.Cart.Domain/Aggregates/CartItem.cs#L36]、[file:///workspace/src/Services/Cart/Leno.Cart.Domain/Aggregates/CartItem.cs#L68]
- **修复步骤**：删除 `CartItem` 构造函数中 `IsValid = true;`（L68），保留字段初始化器 `= true`（L36）。
- **影响范围**：Cart.Domain `CartItem`。
- **验证方法**：单元测试 `CartItem` 默认 `IsValid == true` 不变；编译通过。

### P2-5：4.5 CartDbContextDesignTimeFactory 硬编码连接字符串含密码

- **审计位置**：[file:///workspace/docs/superpowers/specs/2026-07-21-code-audit/03-cart.md#L330-L336]
- **代码位置**：[file:///workspace/src/Services/Cart/Leno.Cart.Infrastructure/CartDbContextDesignTimeFactory.cs#L15]
- **修复步骤**：改为从环境变量 `LENO_DESIGNTIME_CONNECTION_STRING` 读取；未配置时抛 `InvalidOperationException` 引导开发文档设置。与跨 BC 治理（13-architecture-assessment.md G5.1 S3）协同，使用统一约定。
- **影响范围**：Cart.Infrastructure `CartDbContextDesignTimeFactory`。
- **验证方法**：grep `Leno@SqlServer2019` 在 Cart BC 零命中；环境变量注入后 `dotnet ef migrations add` 成功。

### P2-6：4.6 匿名购物车 sessionId 暴露在 URL 路径

- **审计位置**：[file:///workspace/docs/superpowers/specs/2026-07-21-code-audit/03-cart.md#L338-L344]
- **代码位置**：[file:///workspace/src/Services/Cart/Leno.Cart.Api/Controllers/AnonymousCartsController.cs#L34]、[file:///workspace/src/Services/Cart/Leno.Cart.Api/Controllers/AnonymousCartsController.cs#L43]、[file:///workspace/src/Services/Cart/Leno.Cart.Api/Controllers/AnonymousCartsController.cs#L52]、[file:///workspace/src/Services/Cart/Leno.Cart.Api/Controllers/AnonymousCartsController.cs#L61]、[file:///workspace/src/Services/Cart/Leno.Cart.Api/Controllers/AnonymousCartsController.cs#L70]、[file:///workspace/src/Services/Cart/Leno.Cart.Api/Controllers/AnonymousCartsController.cs#L79]
- **修复步骤**：
  1. 控制器路由改为 `api/cart/anonymous/items`、`api/cart/anonymous/preview` 等不带 sessionId 的路径。
  2. 所有写操作从 `[FromHeader(Name = "X-Cart-Session")]` 读取 sessionId。
  3. 与 P1-7 协同：限流策略与 sessionId 头传递一并上线。
  4. 前端配套改造：访问日志脱敏 `X-Cart-Session` 头。
- **影响范围**：Cart.Api `AnonymousCartsController`、前端。
- **验证方法**：集成测试 `POST /api/cart/anonymous/items` 携带 `X-Cart-Session` 头成功；access log 不含 sessionId 路径段。

### P2-7：4.7 MergeFrom 不跳过匿名购物车中的无效项

- **审计位置**：[file:///workspace/docs/superpowers/specs/2026-07-21-code-audit/03-cart.md#L346-L352]
- **代码位置**：[file:///workspace/src/Services/Cart/Leno.Cart.Domain/Aggregates/Cart.cs#L235]
- **修复步骤**：`foreach (var item in anonymousCart.Items)` 改为 `foreach (var item in anonymousCart.Items.Where(i => i.IsValid))`，跳过 `IsValid == false` 的项。
- **影响范围**：Cart.Domain `Cart.MergeFrom`。
- **验证方法**：单元测试构造匿名购物车含 1 个无效项 + 1 个有效项，验证合并后用户购物车仅含有效项。

### P2-8：4.8 AnonymousCartAppService.GetCartAsync 刷新 TTL 被攻击者利用

- **审计位置**：[file:///workspace/docs/superpowers/specs/2026-07-21-code-audit/03-cart.md#L354-L360]
- **代码位置**：[file:///workspace/src/Services/Cart/Leno.Cart.Application/Services/AnonymousCartAppService.cs#L44-L49]
- **根因**：每次 `GetCartAsync` 都 `RefreshTtlAsync`，攻击者定时访问可让匿名购物车永久驻留。
- **修复步骤**：
  1. 移除 `GetCartAsync` 中的 `RefreshTtlAsync` 调用。
  2. 仅在写操作（`AddItemAsync`/`UpdateQuantityAsync`/`RemoveItemAsync`/`SelectItemsAsync`/`PreviewCheckoutAsync`）刷新 TTL，鼓励用户活跃操作。
  3. 与 P1-7 限流协同，限制单 IP 高频访问。
- **影响范围**：Cart.Application `AnonymousCartAppService`。
- **验证方法**：单元测试 `GetCartAsync` 不调用 `RefreshTtlAsync`；写操作仍调用。

### P2-9：4.9 ProductEventConsumer 三个消费者共享 DbContext 跨批次累积跟踪

- **审计位置**：[file:///workspace/docs/superpowers/specs/2026-07-21-code-audit/03-cart.md#L362-L368]
- **代码位置**：[file:///workspace/src/Services/Cart/Leno.Cart.Infrastructure/Consumers/ProductEventConsumer.cs#L54-L66]、[file:///workspace/src/Services/Cart/Leno.Cart.Infrastructure/Consumers/ProductEventConsumer.cs#L110-L123]、[file:///workspace/src/Services/Cart/Leno.Cart.Infrastructure/Consumers/ProductEventConsumer.cs#L184-L197]
- **根因**：内层 foreach 加载 100 个购物车全部由同一 Scoped DbContext 跟踪，`SaveEntitiesAsync` 后未清理。
- **修复步骤**：在每批 `SaveEntitiesAsync` 后调用 `_context.ChangeTracker.Clear()`（与 P1-2 协同，若已改用 `AsNoTracking` 加载则无需此步）。
- **影响范围**：Cart.Infrastructure `ProductEventConsumer` 三个消费者。
- **验证方法**：单元测试 mock DbContext 验证每批后 `ChangeTracker.Clear()` 被调用。

### P2-10：4.10 AnonymousCartAppService.GetOrCreateCartAsync 在不存在时立即 SaveAsync 覆盖

- **审计位置**：[file:///workspace/docs/superpowers/specs/2026-07-21-code-audit/03-cart.md#L370-L376]
- **代码位置**：[file:///workspace/src/Services/Cart/Leno.Cart.Application/Services/AnonymousCartAppService.cs#L137-L147]
- **根因**：两个并发请求同时遇 null 都创建并 SaveAsync，后者覆盖前者（虽空购物车无业务损失，浪费一次 Redis 写）。
- **修复步骤**：使用 Redis `SET NX`（`StringSetAsync(key, value, ttl, when: When.NotExists)`）原子创建，已存在时跳过 SaveAsync 改为再次 GetAsync。
- **影响范围**：Cart.Infrastructure `RedisAnonymousCartRepository` 新增 `TrySaveAsync` 方法；Cart.Application `AnonymousCartAppService.GetOrCreateCartAsync` 调用新方法。
- **验证方法**：单元测试并发 2 个 GetOrCreateCartAsync，验证仅 1 次 `StringSetAsync` 调用。

---

## 已修复项（标注 [ALREADY-FIXED] 或 [VERIFIED-NOT-REPRODUCIBLE]）

| # | 状态 | 标题 | 说明 |
|---|------|------|------|
| T12 | [ALREADY-FIXED] | CartPriceService 价格加载失败掩盖 | 对应审计 2.3 的"价格降级未实现"子问题：`CartAppService.BuildCartDtoAsync` 已添加 `try/catch` + `PriceUnavailable=true` 标记 + `SelectedTotalAmount` 排除价格失败项（[file:///workspace/src/Services/Cart/Leno.Cart.Application/Services/CartAppService.cs#L219-L252]、[file:///workspace/src/Services/Cart/Leno.Cart.Application/Services/CartAppService.cs#L254-L294]、[file:///workspace/src/Services/Cart/Leno.Cart.Application/Services/CartAppService.cs#L133-L136]）。本计划 P0-3 修复的是同问题中"catch 错误异常类型"的剩余子项（catch `CartDomainException` 应改 `AntiCorruptionException`），与 T12 互补。 |

---

## 附录：P0 修复顺序与依赖关系

```
P0-1 (反向索引处理器)
  ├─→ P0-5 (聚合不变量) 独立可并行
  ├─→ P0-2 (Redis 异常上抛) 独立可并行
  └─→ P0-3 (catch 类型修正) 独立可并行
                ↓
        P0-4 (匿名 0 元漏洞) 依赖 P0-3 的 AntiCorruptionException using 引入
```

**建议执行顺序**：P0-1 → P0-3 → P0-4 → P0-5 → P0-2（P0-2 工作量最大，可并行启动）

## 附录：跨 BC 协同修复项（来自 00-summary.md F 章节、13-architecture-assessment.md G4/G5）

- **D2 Outbox 旁路**（00-summary.md F1.1）：Cart BC 当前所有保存路径走 `SaveEntitiesAsync`（[file:///workspace/src/Services/Cart/Leno.Cart.Application/Services/CartAppService.cs#L48]、[file:///workspace/src/Services/Cart/Leno.Cart.Application/Services/CartAppService.cs#L57]、[file:///workspace/src/Services/Cart/Leno.Cart.Application/Services/CartAppService.cs#L66]、[file:///workspace/src/Services/Cart/Leno.Cart.Application/Services/CartAppService.cs#L83]、[file:///workspace/src/Services/Cart/Leno.Cart.Application/Services/CartAppService.cs#L92]、[file:///workspace/src/Services/Cart/Leno.Cart.Application/Services/CartAppService.cs#L179]），无 Outbox 旁路问题，本计划无需额外修复。
- **D5 Guid→int64 POC 残留**（00-summary.md F1.2、13-architecture-assessment.md G3.2）：Cart BC 的 `CartGrpcService.MapToProto` 仍使用 `(long)item.SkuId.GetHashCode()`（[file:///workspace/src/Services/Cart/Leno.Cart.Api/GrpcServices/CartGrpcService.cs#L77]），但已新增 `SkuIdStr = item.SkuId.ToString()` 双写字段（[file:///workspace/src/Services/Cart/Leno.Cart.Api/GrpcServices/CartGrpcService.cs#L78]）。按 ADR-0007 迁移计划，由跨 BC 统一治理（中期 M1），本 BC 计划不单列修复项，仅跟踪 deprecated 字段下线进度。
- **D6.1 设计期工厂硬编码密码**（00-summary.md F1.1 P0-7、13-architecture-assessment.md G3.8）：Cart BC 对应 P2-5，与 SellerShop/Notification BC 协同由共享层抽取 `DesignTimeDbContextFactoryBase<T>` 统一治理。
- **D3.1 Money 值对象不可变性**（00-summary.md F3.1 P2-4、13-architecture-assessment.md G3.4）：Cart BC 金额处理分散在 `CartInternalQueryService`（P1-10）与 `CartAppService`（P1-14），共享层 `Money` 标准化后受益，本 BC 计划聚焦 BC 内部修复，共享层治理由 12-shared.md 修复计划承接。
