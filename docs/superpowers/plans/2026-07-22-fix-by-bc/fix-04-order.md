# Order（订单与交易域）修复实施计划

## 元数据
- 审计报告：[file:///workspace/docs/superpowers/specs/2026-07-21-code-audit/04-order.md]
- 跨 BC 聚合报告：[file:///workspace/docs/superpowers/specs/2026-07-21-code-audit/00-summary.md]（F1.2 P0-8/P0-9，F2.2 P1-13/P1-14）
- 架构评估报告：[file:///workspace/docs/superpowers/specs/2026-07-21-code-audit/13-architecture-assessment.md]（G3.6 跨域事务 TD6，G5.2 M2 Saga 编排补全）
- 问题总数：🔴 13 / 🟡 14 / 🟢 9
- 已修复（跳过）：7 项（p0a-T2、p0a-T3、T7、T8、T9、T16、T18）
- 本计划覆盖：36 项（13 P0 + 14 P1 + 9 P2）
- 扫描范围：`src/Services/Order/Leno.Order.{Domain,Application,Infrastructure,Api}/`
- 排除项：Tests 目录、`Migrations/*.Designer.cs`、`*ModelSnapshot.cs`
- 修复优先级建议（来自审计报告 §5）：P0 2 周内 / P1 1 个月内 / P2 2 个月内

## 问题清单总表

| # | 严重度 | 问题标题 | 审计位置 | 优先级 | 状态 |
|---|--------|---------|---------|--------|------|
| 1 | 🔴 高 | StockReservation 聚合根完全被绕过，领域事件从未发布（2.1） | 04-order.md §2.1 | P0 | 待修复 |
| 2 | 🔴 高 | ForceCancel 已发货订单时释放的是预占而非已扣减库存（2.2） | 04-order.md §2.2 | P0 | 待修复 |
| 3 | 🔴 高 | Order 聚合根缺乏乐观并发控制（2.3） | 04-order.md §2.3 | P0 | 待修复 |
| 4 | 🔴 高 | 支付成功消费者跨进程边界无原子性，Redis 库存可能被错误扣减（2.4） | 04-order.md §2.4 | P0 | 待修复 |
| 5 | 🔴 高 | OrderTimeoutDelayMessageConsumer 与 AfterSalesWindowConsumer 缺失幂等键（2.5） | 04-order.md §2.5 | P0 | 待修复 |
| 6 | 🔴 高 | Order.MarkAsPaid 缺支付金额与 PaymentInitiated 校验（2.6） | 04-order.md §2.6 | P0 | 待修复 |
| 7 | 🔴 高 | Saga 补偿失败静默吞掉，造成资源泄漏（2.7） | 04-order.md §2.7 | P0 | 待修复 |
| 8 | 🔴 高 | OrderSagaOrchestrator 积分抵现绕过聚合不变量校验（2.8） | 04-order.md §2.8 | P0 | 待修复 |
| 9 | 🔴 高 | OrderPricingDomainService.ValidatePricesAsync N+1 远程调用且与 Saga 重复（2.9） | 04-order.md §2.9 | P0 | 待修复 |
| 10 | 🔴 高 | 物流轨迹查询全量加载 100 个物流公司匹配 Code（2.10） | 04-order.md §2.10 | P0 | 待修复 |
| 11 | 🔴 高 | StockReconciliationService 使用 KEYS 命令全量扫描 Redis（2.11） | 04-order.md §2.11 | P0 | 待修复 |
| 12 | 🔴 高 | ExecuteGroupAsync 调度超时延迟消息与 SaveEntitiesAsync 不同事务（2.12） | 04-order.md §2.12 | P0 | 待修复 |
| 13 | 🔴 高 | Order.Cancel 与库存/积分/优惠券释放非原子，先释放后持久化（2.13） | 04-order.md §2.13 | P0 | 待修复 |
| 14 | 🟡 中 | FreightTemplate.CalculateFreight 当 quantity=0 返回 FirstPrice（3.1） | 04-order.md §3.1 | P1 | 待修复 |
| 15 | 🟡 中 | OrderPricingDomainService.CalculateAndAllocateAsync 未校验 totalDiscount ≤ sumSubtotals（3.2） | 04-order.md §3.2 | P1 | 待修复 |
| 16 | 🟡 中 | Order.Ship 未校验物流公司编码存在性（3.3） | 04-order.md §3.3 | P1 | 待修复 |
| 17 | 🟡 中 | RefundCompletedEventConsumer 循环内调用 Redis 释放库存（3.4） | 04-order.md §3.4 | P1 | 待修复 |
| 18 | 🟡 中 | OrderAppService.PreviewAsync 重复实现金额计算业务规则（3.5） | 04-order.md §3.5 | P1 | 待修复 |
| 19 | 🟡 中 | OrderAppService.CreateOrderAsync 积分按卖家分摊是业务规则（3.6） | 04-order.md §3.6 | P1 | 待修复 |
| 20 | 🟡 中 | StockReservationCompensation 聚合 MarkFailed 不变量缺陷（3.7） | 04-order.md §3.7 | P1 | 待修复 |
| 21 | 🟡 中 | Order.Items 与 FreightTemplate.RegionRules 直接暴露可变 List（3.8） | 04-order.md §3.8 | P1 | 待修复 |
| 22 | 🟡 中 | FreightRegionRule record 暴露无参公共构造破坏不可变性（3.9） | 04-order.md §3.9 | P1 | 待修复 |
| 23 | 🟡 中 | OrderSagaResult 暴露聚合根给应用层（3.10） | 04-order.md §3.10 | P1 | 待修复 |
| 24 | 🟡 中 | OrderSagaOrchestrator 多卖家拆单顺序执行未并行（3.11） | 04-order.md §3.11 | P1 | 待修复 |
| 25 | 🟡 中 | LogisticsTrackingService 静默吞掉所有远程失败（3.12） | 04-order.md §3.12 | P1 | 待修复 |
| 26 | 🟡 中 | OrderDbContext 未配置全局查询过滤器软删除（3.13） | 04-order.md §3.13 | P1 | 待修复 |
| 27 | 🟡 中 | OrderGrpcService.GetOrderSellerId 返回 GetHashCode 作为 long 标识（3.14） | 04-order.md §3.14 | P1 | 待修复 |
| 28 | 🟢 低 | Application 层大量 await 缺少 ConfigureAwait(false)（4.1） | 04-order.md §4.1 | P2 | 待修复 |
| 29 | 🟢 低 | Obsolete 方法仍被 Controller 使用（4.2） | 04-order.md §4.2 | P2 | 待修复 |
| 30 | 🟢 低 | OrderNumberGenerator 唯一性保证弱（4.3） | 04-order.md §4.3 | P2 | 待修复 |
| 31 | 🟢 低 | StockReservationCompensationConfiguration 缺少 (OrderId, SkuId) 复合唯一索引（4.4） | 04-order.md §4.4 | P2 | 待修复 |
| 32 | 🟢 低 | InternalOrdersController 双路由 Obsolete 标注（4.5） | 04-order.md §4.5 | P2 | 待修复 |
| 33 | 🟢 低 | SeckillOrderCreationService 占位地址硬编码"待补充"（4.6） | 04-order.md §4.6 | P2 | 待修复 |
| 34 | 🟢 低 | OrderCancelledDomainEvent 使用 Math.Round 转换积分到分可能丢精度（4.7） | 04-order.md §4.7 | P2 | 待修复 |
| 35 | 🟢 低 | OrderListQuery.PageIndex 从 0 起，OrderListResultDto.Page 从 1 起，混用易错（4.8） | 04-order.md §4.8 | P2 | 待修复 |
| 36 | 🟢 低 | OrderDbContext 不暴露 StockReservation 的导航关系（4.9） | 04-order.md §4.9 | P2 | 待修复 |

---

## P0 详细修复计划（TDD bite-sized 格式，5 步：测试→验证失败→实现→验证通过→提交）

### P0-T1：StockReservation 聚合根完全被绕过，领域事件从未发布（审计 #1 / 2.1）

**审计位置**：[file:///workspace/docs/superpowers/specs/2026-07-21-code-audit/04-order.md#L15-L25]
**代码位置**：
- [file:///workspace/src/Services/Order/Leno.Order.Infrastructure/Repositories/RedisInventoryRepository.cs#L24-L137]（Lua 脚本直接操作 Redis，完全绕过聚合根）
- [file:///workspace/src/Services/Order/Leno.Order.Domain/Aggregates/StockReservation.cs#L67-L168]（聚合根方法 ReserveStock/ConfirmStockDeduction/ReleaseStock/Replenish 从未被调用）
- [file:///workspace/src/Services/Order/Leno.Order.Infrastructure/EventBus/OrderIntegrationEventMapper.cs#L10-L61]（Mapper 未注册 StockReserved/StockConfirmed/StockReleased 事件翻译）
- [file:///workspace/src/Services/Order/Leno.Order.Domain/Repositories/IInventoryRepository.cs#L7-L44]（接口不继承 IRepository<StockReservation>）

**根因**：`RedisInventoryRepository` 通过 Lua 脚本直接操作 Redis 的 `inventory:stock:{skuId}` 与 `inventory:reserved:{skuId}:{orderId}` 两个 key，完全绕过 `StockReservation` 聚合根。聚合根的 `ReserveStock`/`ConfirmStockDeduction`/`ReleaseStock`/`Replenish` 方法及其收集的 `StockReservedEvent`/`StockConfirmedEvent`/`StockReleasedEvent` 领域事件从未被触发。`IInventoryRepository` 不继承 `IRepository<StockReservation>`，DB 中的 StockReservation 表仅被对账后台服务读取。

**修复方案**：采用"双写 + 聚合审计源"策略。Redis 仍是扣减原子层（保证高性能与原子性），但 `RedisInventoryRepository` 在每次 Redis 操作成功后，同步加载/更新 `StockReservation` 聚合并持久化到 DB，使聚合根成为审计/对账源并发布领域事件。在 `OrderIntegrationEventMapper` 注册三个库存事件的翻译。

---

#### 步骤 1：测试

在 `Leno.Order.Infrastructure.Tests/RedisInventoryRepositoryTests.cs` 中追加测试，验证库存操作后聚合根被持久化且领域事件被发布。

```csharp
// 文件：src/Services/Order/Leno.Order.Infrastructure.Tests/RedisInventoryRepositoryTests.cs
// 在 RedisInventoryRepositoryTests 类内追加以下测试方法

using Leno.Order.Domain.Aggregates;
using Leno.Order.Domain.Repositories;
using Moq;
using StackExchange.Redis;

[Fact]
public async Task ReserveAsync_Success_Should_Persist_StockReservation_Aggregate_And_Publish_Event()
{
    // Arrange
    var redisMock = new Mock<IConnectionMultiplexer>();
    var dbMock = new Mock<IDatabase>();
    redisMock.Setup(r => r.GetDatabase(It.IsAny<int>(), It.IsAny<object>())).Returns(dbMock.Object);
    dbMock.Setup(d => d.ScriptEvaluateAsync(It.IsAny<string>(), It.IsAny<RedisKey[]>(), It.IsAny<RedisValue[]>(), It.IsAny<CommandFlags>()))
        .ReturnsAsync(RedisResult.Create(1, ResultType.Integer));

    var stockRepoMock = new Mock<IStockReservationRepository>();
    var loggerMock = new Mock<ILogger<RedisInventoryRepository>>();
    var stockReservation = StockReservation.Create(Guid.NewGuid(), SkuId, 100);
    stockRepoMock.Setup(r => r.GetBySkuIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
        .ReturnsAsync(stockReservation);

    var sut = new RedisInventoryRepository(redisMock.Object, stockRepoMock.Object, loggerMock.Object);

    // Act
    var success = await sut.ReserveAsync(SkuId, OrderId, 30, CancellationToken.None);

    // Assert
    Assert.True(success);
    stockReservation.ReservedQty.Should().Be(30);
    stockReservation.DomainEvents.Should().Contain(e => e is StockReservedEvent);
    stockRepoMock.Verify(r => r.UpdateAsync(It.Is<StockReservation>(s => s.ReservedQty == 30), It.IsAny<CancellationToken>()), Times.Once);
}

[Fact]
public async Task ConfirmAsync_Should_Persist_StockReservation_And_Publish_StockConfirmedEvent()
{
    // Arrange
    var redisMock = new Mock<IConnectionMultiplexer>();
    var dbMock = new Mock<IDatabase>();
    redisMock.Setup(r => r.GetDatabase(It.IsAny<int>(), It.IsAny<object>())).Returns(dbMock.Object);
    dbMock.Setup(d => d.ScriptEvaluateAsync(It.IsAny<string>(), It.IsAny<RedisKey[]>(), It.IsAny<RedisValue[]>(), It.IsAny<CommandFlags>()))
        .ReturnsAsync(RedisResult.Create(1, ResultType.Integer));

    var stockRepoMock = new Mock<IStockReservationRepository>();
    var loggerMock = new Mock<ILogger<RedisInventoryRepository>>();
    var stockReservation = StockReservation.Create(Guid.NewGuid(), SkuId, 100);
    stockReservation.ReserveStock(OrderId, 30);
    stockRepoMock.Setup(r => r.GetBySkuIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
        .ReturnsAsync(stockReservation);

    var sut = new RedisInventoryRepository(redisMock.Object, stockRepoMock.Object, loggerMock.Object);

    // Act
    await sut.ConfirmAsync(SkuId, OrderId, 20, CancellationToken.None);

    // Assert
    stockReservation.DeductedQty.Should().Be(20);
    stockReservation.ReservedQty.Should().Be(10);
    stockReservation.DomainEvents.Should().Contain(e => e is StockConfirmedEvent);
    stockRepoMock.Verify(r => r.UpdateAsync(It.IsAny<StockReservation>(), It.IsAny<CancellationToken>()), Times.Once);
}

[Fact]
public async Task ReleaseAsync_Should_Persist_StockReservation_And_Publish_StockReleasedEvent()
{
    // Arrange
    var redisMock = new Mock<IConnectionMultiplexer>();
    var dbMock = new Mock<IDatabase>();
    redisMock.Setup(r => r.GetDatabase(It.IsAny<int>(), It.IsAny<object>())).Returns(dbMock.Object);
    dbMock.Setup(d => d.ScriptEvaluateAsync(It.IsAny<string>(), It.IsAny<RedisKey[]>(), It.IsAny<RedisValue[]>(), It.IsAny<CommandFlags>()))
        .ReturnsAsync(RedisResult.Create(1, ResultType.Integer));

    var stockRepoMock = new Mock<IStockReservationRepository>();
    var loggerMock = new Mock<ILogger<RedisInventoryRepository>>();
    var stockReservation = StockReservation.Create(Guid.NewGuid(), SkuId, 100);
    stockReservation.ReserveStock(OrderId, 30);
    stockRepoMock.Setup(r => r.GetBySkuIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
        .ReturnsAsync(stockReservation);

    var sut = new RedisInventoryRepository(redisMock.Object, stockRepoMock.Object, loggerMock.Object);

    // Act
    await sut.ReleaseAsync(SkuId, OrderId, 20, CancellationToken.None);

    // Assert
    stockReservation.ReservedQty.Should().Be(10);
    stockReservation.DomainEvents.Should().Contain(e => e is StockReleasedEvent);
    stockRepoMock.Verify(r => r.UpdateAsync(It.IsAny<StockReservation>(), It.IsAny<CancellationToken>()), Times.Once);
}
```

新增 `OrderIntegrationEventMapper` 翻译注册测试：

```csharp
// 文件：src/Services/Order/Leno.Order.Infrastructure.Tests/OrderIntegrationEventMapperTests.cs
// 在 OrderIntegrationEventMapperTests 类内追加以下测试方法

using Leno.Order.Domain.Events;

[Fact]
public void Mapper_Should_Register_StockReservedEvent_Translation()
{
    var mapper = new OrderIntegrationEventMapper();
    var stockReservedEvent = new StockReservedEvent(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), 10);

    var integrationEvent = mapper.Translate(stockReservedEvent);

    integrationEvent.Should().NotBeNull();
    integrationEvent.Should().BeOfType<StockReservedIntegrationEvent>();
}

[Fact]
public void Mapper_Should_Register_StockConfirmedEvent_Translation()
{
    var mapper = new OrderIntegrationEventMapper();
    var stockConfirmedEvent = new StockConfirmedEvent(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), 10);

    var integrationEvent = mapper.Translate(stockConfirmedEvent);

    integrationEvent.Should().NotBeNull();
    integrationEvent.Should().BeOfType<StockConfirmedIntegrationEvent>();
}

[Fact]
public void Mapper_Should_Register_StockReleasedEvent_Translation()
{
    var mapper = new OrderIntegrationEventMapper();
    var stockReleasedEvent = new StockReleasedEvent(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), 10);

    var integrationEvent = mapper.Translate(stockReleasedEvent);

    integrationEvent.Should().NotBeNull();
    integrationEvent.Should().BeOfType<StockReleasedIntegrationEvent>();
}
```

> 注：测试中使用 `SkuId`、`OrderId` 静态字段，若测试类已有则复用。`IStockReservationRepository` 需新增 `GetBySkuIdAsync` 方法。`StockReservedIntegrationEvent` 等集成事件契约需在 `Leno.SharedContracts` 定义。

#### 步骤 2：验证失败

```bash
dotnet test src/Services/Order/Leno.Order.Infrastructure.Tests/Leno.Order.Infrastructure.Tests.csproj \
  --filter "FullyQualifiedName~ReserveAsync_Success_Should_Persist|FullyQualifiedName~ConfirmAsync_Should_Persist|FullyQualifiedName~ReleaseAsync_Should_Persist|FullyQualifiedName~Mapper_Should_Register_Stock"
```

预期：5 个测试全部编译失败或运行失败。`RedisInventoryRepository` 当前构造函数不接收 `IStockReservationRepository`，`OrderIntegrationEventMapper` 未注册三个库存事件的翻译。

#### 步骤 3：实现

**3.1** 在 `IStockReservationRepository` 增加 `GetBySkuIdAsync` 方法：

```csharp
// 文件：src/Services/Order/Leno.Order.Domain/Repositories/IStockReservationRepository.cs
// 完整文件内容

using Leno.Order.Domain.Aggregates;
using Leno.SharedKernel.Abstractions;

namespace Leno.Order.Domain.Repositories;

/// <summary>
/// 库存预占聚合仓储接口，提供按 SKU 维度的聚合加载与持久化。
/// </summary>
public interface IStockReservationRepository : IRepository<StockReservation>
{
    /// <summary>
    /// 按 SKU 标识加载库存预占聚合根，不存在返回 null。
    /// </summary>
    /// <param name="skuId">SKU 标识。</param>
    /// <param name="ct">取消令牌。</param>
    Task<StockReservation?> GetBySkuIdAsync(Guid skuId, CancellationToken ct = default);

    /// <summary>
    /// 按 SKU 标识加载库存预占聚合根，不存在则创建基线为 0 的新聚合并返回。
    /// 用于 Redis 与 DB 双写场景下保证聚合始终存在。
    /// </summary>
    Task<StockReservation> GetOrCreateAsync(Guid skuId, CancellationToken ct = default);
}
```

**3.2** 修改 `RedisInventoryRepository`，在每次 Redis 操作成功后加载/更新 `StockReservation` 聚合并持久化：

```csharp
// 文件：src/Services/Order/Leno.Order.Infrastructure/Repositories/RedisInventoryRepository.cs
// 完整文件内容

using Leno.Order.Domain.Aggregates;
using Leno.Order.Domain.Repositories;
using Microsoft.Extensions.Logging;
using StackExchange.Redis;

namespace Leno.Order.Infrastructure.Repositories;

/// <summary>
/// 库存仓储 Redis 实现，基于 Lua 脚本保证预占/确认/释放的原子性。
/// 采用"Redis 原子层 + DB 聚合审计源"双写策略：
/// - Redis Lua 脚本保证扣减原子性（高性能）；
/// - 操作成功后加载 StockReservation 聚合根，调用聚合方法维护不变量并发布领域事件，持久化到 DB（审计/对账源）。
/// Redis Key 设计：
/// - inventory:stock:{skuId} — 可用库存（String）
/// - inventory:reserved:{skuId}:{orderId} — 单订单预占数量（String）
/// </summary>
public sealed class RedisInventoryRepository : IInventoryRepository
{
    private readonly IConnectionMultiplexer _redis;
    private readonly IStockReservationRepository _stockReservationRepository;
    private readonly ILogger<RedisInventoryRepository> _logger;

    private const string ReserveLuaScript = @"
local available = tonumber(redis.call('GET', KEYS[1]))
if available == nil then return 0 end
local qty = tonumber(ARGV[1])
if available < qty then return 0 end
redis.call('DECRBY', KEYS[1], qty)
redis.call('SET', KEYS[2], qty)
return 1";

    private const string ReleaseLuaScript = @"
local reserved = tonumber(redis.call('GET', KEYS[2]) or '0')
if reserved == 0 then return 1 end
redis.call('INCRBY', KEYS[1], reserved)
redis.call('DEL', KEYS[2])
return 1";

    private const string ConfirmLuaScript = @"
local reserved = tonumber(redis.call('GET', KEYS[2]) or '0')
if reserved == 0 then return 1 end
redis.call('DEL', KEYS[2])
return 1";

    private const string ReturnDeductedLuaScript = @"
local available = tonumber(redis.call('GET', KEYS[1]) or '0')
local qty = tonumber(ARGV[1])
redis.call('INCRBY', KEYS[1], qty)
return 1";

    public RedisInventoryRepository(
        IConnectionMultiplexer redis,
        IStockReservationRepository stockReservationRepository,
        ILogger<RedisInventoryRepository> logger)
    {
        ArgumentNullException.ThrowIfNull(redis);
        ArgumentNullException.ThrowIfNull(stockReservationRepository);
        ArgumentNullException.ThrowIfNull(logger);
        _redis = redis;
        _stockReservationRepository = stockReservationRepository;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<bool> ReserveAsync(Guid skuId, Guid orderId, int quantity, CancellationToken ct = default)
    {
        var db = _redis.GetDatabase();
        var stockKey = BuildStockKey(skuId);
        var reservedKey = BuildReservedKey(skuId, orderId);

        var result = (long?)await db.ScriptEvaluateAsync(
            ReserveLuaScript,
            new RedisKey[] { stockKey, reservedKey },
            new RedisValue[] { quantity });

        var success = result == 1;
        if (success)
        {
            // 双写：加载聚合根，调用 ReserveStock 维护不变量并发布领域事件
            var reservation = await _stockReservationRepository.GetOrCreateAsync(skuId, ct);
            reservation.ReserveStock(orderId, quantity);
            await _stockReservationRepository.UpdateAsync(reservation, ct);

            _logger.LogInformation("库存预占成功 SkuId={SkuId} OrderId={OrderId} Quantity={Quantity}",
                skuId, orderId, quantity);
        }
        else
        {
            _logger.LogInformation("库存预占失败（库存不足）SkuId={SkuId} OrderId={OrderId} Quantity={Quantity}",
                skuId, orderId, quantity);
        }

        return success;
    }

    /// <inheritdoc />
    public async Task ConfirmAsync(Guid skuId, Guid orderId, int quantity, CancellationToken ct = default)
    {
        var db = _redis.GetDatabase();
        var stockKey = BuildStockKey(skuId);
        var reservedKey = BuildReservedKey(skuId, orderId);

        await db.ScriptEvaluateAsync(
            ConfirmLuaScript,
            new RedisKey[] { stockKey, reservedKey });

        // 双写：加载聚合根，调用 ConfirmStockDeduction 维护不变量并发布领域事件
        var reservation = await _stockReservationRepository.GetBySkuIdAsync(skuId, ct);
        if (reservation is not null)
        {
            reservation.ConfirmStockDeduction(orderId, quantity);
            await _stockReservationRepository.UpdateAsync(reservation, ct);
        }

        _logger.LogInformation("库存确认扣减 SkuId={SkuId} OrderId={OrderId} Quantity={Quantity}",
            skuId, orderId, quantity);
    }

    /// <inheritdoc />
    public async Task ReleaseAsync(Guid skuId, Guid orderId, int quantity, CancellationToken ct = default)
    {
        var db = _redis.GetDatabase();
        var stockKey = BuildStockKey(skuId);
        var reservedKey = BuildReservedKey(skuId, orderId);

        await db.ScriptEvaluateAsync(
            ReleaseLuaScript,
            new RedisKey[] { stockKey, reservedKey });

        // 双写：加载聚合根，调用 ReleaseStock 维护不变量并发布领域事件
        var reservation = await _stockReservationRepository.GetBySkuIdAsync(skuId, ct);
        if (reservation is not null)
        {
            reservation.ReleaseStock(orderId, quantity);
            await _stockReservationRepository.UpdateAsync(reservation, ct);
        }

        _logger.LogInformation("库存预占释放 SkuId={SkuId} OrderId={OrderId} Quantity={Quantity}",
            skuId, orderId, quantity);
    }

    /// <inheritdoc />
    public async Task ReturnDeductedAsync(Guid skuId, Guid orderId, int quantity, CancellationToken ct = default)
    {
        var db = _redis.GetDatabase();
        var stockKey = BuildStockKey(skuId);

        await db.ScriptEvaluateAsync(
            ReturnDeductedLuaScript,
            new RedisKey[] { stockKey },
            new RedisValue[] { quantity });

        // 双写：加载聚合根，调用 Replenish 归还已扣减库存
        var reservation = await _stockReservationRepository.GetBySkuIdAsync(skuId, ct);
        if (reservation is not null)
        {
            reservation.Replenish(quantity);
            await _stockReservationRepository.UpdateAsync(reservation, ct);
        }

        _logger.LogInformation("已扣减库存归还 SkuId={SkuId} OrderId={OrderId} Quantity={Quantity}",
            skuId, orderId, quantity);
    }

    /// <inheritdoc />
    public async Task<int> GetAvailableAsync(Guid skuId, CancellationToken ct = default)
    {
        var db = _redis.GetDatabase();
        var stockKey = BuildStockKey(skuId);
        var value = await db.StringGetAsync(stockKey);
        return (int?)value ?? 0;
    }

    /// <inheritdoc />
    public async Task SetBaseLineAsync(Guid skuId, int availableQty, CancellationToken ct = default)
    {
        var db = _redis.GetDatabase();
        var stockKey = BuildStockKey(skuId);
        await db.StringSetAsync(stockKey, availableQty);

        // 双写：同步聚合基线
        var reservation = await _stockReservationRepository.GetOrCreateAsync(skuId, ct);
        var delta = availableQty - reservation.AvailableQty;
        if (delta != 0)
        {
            reservation.Replenish(delta);
            await _stockReservationRepository.UpdateAsync(reservation, ct);
        }

        _logger.LogInformation("库存基线同步 SkuId={SkuId} AvailableQty={AvailableQty}", skuId, availableQty);
    }

    private static string BuildStockKey(Guid skuId) => $"inventory:stock:{skuId}";
    private static string BuildReservedKey(Guid skuId, Guid orderId) => $"inventory:reserved:{skuId}:{orderId}";
}
```

**3.3** 在 `IInventoryRepository` 增加 `ReturnDeductedAsync` 方法（供 P0-T2 使用）：

```csharp
// 文件：src/Services/Order/Leno.Order.Domain/Repositories/IInventoryRepository.cs
// 在接口中追加 ReturnDeductedAsync 方法声明（紧接 ReleaseAsync 之后）

    /// <summary>
    /// 归还已扣减库存（已支付/已发货订单强制取消时调用），将已扣减数量加回可用库存。
    /// </summary>
    /// <param name="skuId">SKU 标识。</param>
    /// <param name="orderId">关联订单标识。</param>
    /// <param name="quantity">归还数量，须 &gt; 0。</param>
    Task ReturnDeductedAsync(Guid skuId, Guid orderId, int quantity, CancellationToken ct = default);
```

**3.4** 在 `OrderIntegrationEventMapper` 注册三个库存事件的翻译：

```csharp
// 文件：src/Services/Order/Leno.Order.Infrastructure/EventBus/OrderIntegrationEventMapper.cs
// 在 Translate 方法的 switch 中追加三个 case（紧接现有 case 之后）

    case StockReservedEvent e:
        return new StockReservedIntegrationEvent(e.AggregateId, e.SkuId, e.OrderId, e.Quantity, DateTime.UtcNow);
    case StockConfirmedEvent e:
        return new StockConfirmedIntegrationEvent(e.AggregateId, e.SkuId, e.OrderId, e.Quantity, DateTime.UtcNow);
    case StockReleasedEvent e:
        return new StockReleasedIntegrationEvent(e.AggregateId, e.SkuId, e.OrderId, e.Quantity, DateTime.UtcNow);
```

#### 步骤 4：验证通过

```bash
dotnet test src/Services/Order/Leno.Order.Infrastructure.Tests/Leno.Order.Infrastructure.Tests.csproj \
  --filter "FullyQualifiedName~ReserveAsync_Success_Should_Persist|FullyQualifiedName~ConfirmAsync_Should_Persist|FullyQualifiedName~ReleaseAsync_Should_Persist|FullyQualifiedName~Mapper_Should_Register_Stock"
```

预期：5 个测试全部通过。

#### 步骤 5：提交

```bash
git add src/Services/Order/Leno.Order.Infrastructure/Repositories/RedisInventoryRepository.cs \
        src/Services/Order/Leno.Order.Domain/Repositories/IInventoryRepository.cs \
        src/Services/Order/Leno.Order.Domain/Repositories/IStockReservationRepository.cs \
        src/Services/Order/Leno.Order.Infrastructure/EventBus/OrderIntegrationEventMapper.cs \
        src/Services/Order/Leno.Order.Infrastructure.Tests/RedisInventoryRepositoryTests.cs \
        src/Services/Order/Leno.Order.Infrastructure.Tests/OrderIntegrationEventMapperTests.cs
git commit -m "fix(order): StockReservation 聚合双写与领域事件发布（2.1）

RedisInventoryRepository 在每次 Redis 操作成功后加载/更新 StockReservation
聚合根，调用聚合方法维护不变量并发布领域事件，持久化到 DB 作为审计/对账源。
OrderIntegrationEventMapper 注册 StockReserved/Confirmed/Released 事件翻译。
IInventoryRepository 新增 ReturnDeductedAsync 接口供 ForceCancel 归还已扣减库存。"
```

---

### P0-T2：ForceCancel 已发货订单时释放的是预占而非已扣减库存（审计 #2 / 2.2）

**审计位置**：[file:///workspace/docs/superpowers/specs/2026-07-21-code-audit/04-order.md#L27-L40]
**代码位置**：
- [file:///workspace/src/Services/Order/Leno.Order.Application/Services/OrderAppService.cs#L316-L365]（ForceCancelAsync 对 Paid/Shipped 统一调用 ReleaseBatchAsync）
- [file:///workspace/src/Services/Order/Leno.Order.Domain/Aggregates/Order.cs#L466-L484]（ForceCancel 不区分库存类型）
- [file:///workspace/src/Services/Order/Leno.Order.Infrastructure/Repositories/RedisInventoryRepository.cs#L38-L43]（ReleaseLuaScript 对不存在预占 key 直接 return 1，已扣减库存无法回退）

**根因**：`OrderAppService.ForceCancelAsync` 对 Paid/Shipped 状态订单统一调用 `_stockService.ReleaseBatchAsync`（释放预占）。但 Shipped 订单的库存已被 `PaymentSucceededEventConsumer` 调用 `ConfirmBatchAsync` 转为已扣减，Redis 中预占 key 已被删除。`ReleaseLuaScript` 对不存在的预占 key 直接 `return 1`（无操作），已扣减库存未被回退。

---

#### 步骤 1：测试

在 `Leno.Order.Application.Tests/OrderAppServiceTests.cs` 中追加测试，验证 ForceCancel 在 Shipped 状态下调用 ReturnDeductedAsync 而非 ReleaseBatchAsync。

```csharp
// 文件：src/Services/Order/Leno.Order.Application.Tests/OrderAppServiceTests.cs
// 在 OrderAppServiceTests 类内追加以下测试方法

using Leno.Order.Domain.Services;

[Fact]
public async Task ForceCancelAsync_ShippedOrder_Should_Call_ReturnDeducted_Not_Release()
{
    // Arrange
    var sut = CreateSut(out var orderRepoMock, out var uowMock, out var stockServiceMock,
        out var pointsMock, out var promotionMock, out var logisticsMock, out var logisticsCompanyRepoMock,
        out var eventBusMock, out var busMock, out var sagaMock);

    var order = CreatePaidAndShippedOrder();
    orderRepoMock.Setup(r => r.GetByIdAsync(order.Id, It.IsAny<CancellationToken>()))
        .ReturnsAsync(order);
    stockServiceMock.Setup(s => s.ReturnDeductedBatchAsync(It.IsAny<Guid>(), It.IsAny<Dictionary<Guid, int>>(), It.IsAny<CancellationToken>()))
        .Returns(Task.CompletedTask);
    stockServiceMock.Setup(s => s.ReleaseBatchAsync(It.IsAny<Guid>(), It.IsAny<Dictionary<Guid, int>>(), It.IsAny<CancellationToken>()))
        .Returns(Task.CompletedTask);

    var dto = new ForceCancelOrderDto { Reason = "test", OperatorId = Guid.NewGuid() };

    // Act
    await sut.ForceCancelAsync(order.Id, dto.OperatorId, dto, CancellationToken.None);

    // Assert：Shipped 状态应调用 ReturnDeductedBatchAsync，不调用 ReleaseBatchAsync
    stockServiceMock.Verify(
        s => s.ReturnDeductedBatchAsync(order.Id, It.IsAny<Dictionary<Guid, int>>(), It.IsAny<CancellationToken>()),
        Times.Once);
    stockServiceMock.Verify(
        s => s.ReleaseBatchAsync(order.Id, It.IsAny<Dictionary<Guid, int>>(), It.IsAny<CancellationToken>()),
        Times.Never);
    order.Status.Should().Be(OrderStatus.Cancelled);
}

[Fact]
public async Task ForceCancelAsync_PaidOrder_Should_Call_ReturnDeducted_Not_Release()
{
    // Arrange
    var sut = CreateSut(out var orderRepoMock, out var uowMock, out var stockServiceMock,
        out var pointsMock, out var promotionMock, out var logisticsMock, out var logisticsCompanyRepoMock,
        out var eventBusMock, out var busMock, out var sagaMock);

    var order = CreatePaidOrder();
    orderRepoMock.Setup(r => r.GetByIdAsync(order.Id, It.IsAny<CancellationToken>()))
        .ReturnsAsync(order);
    stockServiceMock.Setup(s => s.ReturnDeductedBatchAsync(It.IsAny<Guid>(), It.IsAny<Dictionary<Guid, int>>(), It.IsAny<CancellationToken>()))
        .Returns(Task.CompletedTask);

    var dto = new ForceCancelOrderDto { Reason = "test", OperatorId = Guid.NewGuid() };

    // Act
    await sut.ForceCancelAsync(order.Id, dto.OperatorId, dto, CancellationToken.None);

    // Assert：Paid 状态（已确认扣减）应调用 ReturnDeductedBatchAsync
    stockServiceMock.Verify(
        s => s.ReturnDeductedBatchAsync(order.Id, It.IsAny<Dictionary<Guid, int>>(), It.IsAny<CancellationToken>()),
        Times.Once);
    stockServiceMock.Verify(
        s => s.ReleaseBatchAsync(It.IsAny<Guid>(), It.IsAny<Dictionary<Guid, int>>(), It.IsAny<CancellationToken>()),
        Times.Never);
}
```

> 注：`CreateSut` 与 `CreatePaidAndShippedOrder`/`CreatePaidOrder` 为测试辅助方法，按既有测试模式补充。`IStockReservationDomainService` 需新增 `ReturnDeductedBatchAsync` 方法。

#### 步骤 2：验证失败

```bash
dotnet test src/Services/Order/Leno.Order.Application.Tests/Leno.Order.Application.Tests.csproj \
  --filter "FullyQualifiedName~ForceCancelAsync_ShippedOrder_Should_Call_ReturnDeducted|FullyQualifiedName~ForceCancelAsync_PaidOrder_Should_Call_ReturnDeducted"
```

预期：2 个测试失败，`IStockReservationDomainService` 当前无 `ReturnDeductedBatchAsync` 方法，编译失败。

#### 步骤 3：实现

**3.1** 在 `IStockReservationDomainService` 增加 `ReturnDeductedBatchAsync` 方法：

```csharp
// 文件：src/Services/Order/Leno.Order.Domain/Services/IStockReservationDomainService.cs
// 在接口中追加 ReturnDeductedBatchAsync 方法声明

    /// <summary>
    /// 批量归还已扣减库存（已支付/已发货订单强制取消时调用）。
    /// 逐个 SKU 调用 IInventoryRepository.ReturnDeductedAsync，单个失败记入补偿表。
    /// </summary>
    /// <param name="orderId">关联订单标识。</param>
    /// <param name="skuQuantities">SKU 与数量映射。</param>
    /// <param name="ct">取消令牌。</param>
    Task ReturnDeductedBatchAsync(Guid orderId, Dictionary<Guid, int> skuQuantities, CancellationToken ct = default);
```

**3.2** 在 `StockReservationDomainService` 实现 `ReturnDeductedBatchAsync`：

```csharp
// 文件：src/Services/Order/Leno.Order.Infrastructure/Services/StockReservationDomainService.cs
// 在 StockReservationDomainService 类内追加以下方法

    /// <inheritdoc />
    public async Task ReturnDeductedBatchAsync(Guid orderId, Dictionary<Guid, int> skuQuantities, CancellationToken ct = default)
    {
        foreach (var (skuId, quantity) in skuQuantities)
        {
            try
            {
                await _inventoryRepository.ReturnDeductedAsync(skuId, orderId, quantity, ct);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "批量归还已扣减库存失败，写入补偿表 OrderId={OrderId} SkuId={SkuId} Quantity={Quantity}",
                    orderId, skuId, quantity);
                await RecordCompensationAsync(orderId, skuId, quantity, ex, ct);
            }
        }
    }
```

**3.3** 修改 `OrderAppService.ForceCancelAsync`，Paid/Shipped 状态调用 `ReturnDeductedBatchAsync`：

```csharp
// 文件：src/Services/Order/Leno.Order.Application/Services/OrderAppService.cs
// 修改 ForceCancelAsync 方法第 340-357 行（已支付/已发货分支）

    // 已支付/已发货订单：强制取消并触发退款
    order.ForceCancel(dto.Reason, operatorId.ToString());

    // 已支付/已发货订单库存已被确认扣减，需归还已扣减库存（而非释放预占）
    var quantities = BuildSkuQuantities(order);
    await _stockService.ReturnDeductedBatchAsync(orderId, quantities, ct);
    await _pointsAntiCorruption.ReleaseAsync(orderId, ct);
    await _promotionAntiCorruption.ReleaseCouponsAsync(orderId, ct);

    // 已支付订单：通过聚合事件触发退款（Outbox 同事务持久化，替代直接 IEventBus.PublishAsync）
    if (order.PaymentId.HasValue)
    {
        var refundId = Guid.NewGuid();
        var channel = order.PaymentMethod?.ToString() ?? "WeChatPay";
        order.AddForceCancelRefundRequestedEvent(
            refundId, order.PaymentId.Value, order.TotalAmount, "CNY", channel,
            $"运营强制取消退款：{dto.Reason}");
    }

    await _orderRepository.UpdateAsync(order, ct);
    await _unitOfWork.SaveEntitiesAsync(ct);
```

**3.4** 同步修改 `RefundCompletedEventConsumer`，按订单状态选择归还/释放：

```csharp
// 文件：src/Services/Order/Leno.Order.Infrastructure/Consumers/RefundCompletedEventConsumer.cs
// 修改第 31-51 行的 foreach 逻辑

    // 按订单当前状态选择归还已扣减或释放预占
    var needsReturnDeducted = order.Status == OrderStatus.Paid || order.Status == OrderStatus.Shipped;
    var skuQuantities = order.Items
        .GroupBy(i => i.SkuId)
        .ToDictionary(g => g.Key, g => g.Sum(i => i.Quantity));

    if (needsReturnDeducted)
    {
        await _stockReservationDomainService.ReturnDeductedBatchAsync(order.Id, skuQuantities, ct);
    }
    else
    {
        await _stockReservationDomainService.ReleaseBatchAsync(order.Id, skuQuantities, ct);
    }
```

#### 步骤 4：验证通过

```bash
dotnet test src/Services/Order/Leno.Order.Application.Tests/Leno.Order.Application.Tests.csproj \
  --filter "FullyQualifiedName~ForceCancelAsync_ShippedOrder_Should_Call_ReturnDeducted|FullyQualifiedName~ForceCancelAsync_PaidOrder_Should_Call_ReturnDeducted"
```

预期：2 个测试通过。

#### 步骤 5：提交

```bash
git add src/Services/Order/Leno.Order.Domain/Services/IStockReservationDomainService.cs \
        src/Services/Order/Leno.Order.Infrastructure/Services/StockReservationDomainService.cs \
        src/Services/Order/Leno.Order.Application/Services/OrderAppService.cs \
        src/Services/Order/Leno.Order.Infrastructure/Consumers/RefundCompletedEventConsumer.cs \
        src/Services/Order/Leno.Order.Application.Tests/OrderAppServiceTests.cs
git commit -m "fix(order): ForceCancel 已发货订单归还已扣减库存而非释放预占（2.2）

新增 IStockReservationDomainService.ReturnDeductedBatchAsync 与
IInventoryRepository.ReturnDeductedAsync 接口。ForceCancelAsync 在 Paid/Shipped
状态下调用 ReturnDeductedBatchAsync 归还已扣减库存。RefundCompletedEventConsumer
按订单状态选择归还/释放。"
```

---

### P0-T3：Order 聚合根缺乏乐观并发控制（审计 #3 / 2.3）

**审计位置**：[file:///workspace/docs/superpowers/specs/2026-07-21-code-audit/04-order.md#L42-L58]
**代码位置**：
- [file:///workspace/src/Services/Order/Leno.Order.Infrastructure/Configurations/OrderConfiguration.cs#L12-L94]（无 IsConcurrencyToken 或 RowVersion 配置）
- [file:///workspace/src/Services/Order/Leno.Order.Domain/Aggregates/Order.cs#L302-L484]（并发场景下状态机冲突）

**根因**：`OrderConfiguration` 未为 Order 配置 `IsConcurrencyToken()` 或 RowVersion 字段。并发场景（支付成功回调 + 超时取消延迟消息 + 买家 Cancel + 运营 ForceCancel）会同时通过状态校验，最后一个写入者静默覆盖前面所有变更。

---

#### 步骤 1：测试

在 `Leno.Order.Infrastructure.Tests/OrderConfigurationTests.cs` 中追加测试，验证 Order 配置了 RowVersion 乐观并发控制。

```csharp
// 文件：src/Services/Order/Leno.Order.Infrastructure.Tests/OrderConfigurationTests.cs
// 在 OrderConfigurationTests 类内追加以下测试方法

using Leno.Order.Infrastructure;
using Leno.Order.Infrastructure.Configurations;
using Microsoft.EntityFrameworkCore;

[Fact]
public void OrderConfiguration_Should_Have_RowVersion_Concurrency_Token()
{
    // Arrange
    var options = new DbContextOptionsBuilder<OrderDbContext>()
        .UseInMemoryDatabase(databaseName: "order_concurrency_test_" + Guid.NewGuid())
        .Options;
    using var context = new OrderDbContext(options);
    var entityType = context.Model.FindEntityType(typeof(Leno.Order.Domain.Aggregates.Order));

    // Assert：RowVersion 属性应被配置为并发令牌
    var rowVersionProperty = entityType!.GetProperties()
        .FirstOrDefault(p => p.Name == nameof(Leno.Order.Domain.Aggregates.Order.RowVersion));
    rowVersionProperty.Should().NotBeNull();
    rowVersionProperty!.IsConcurrencyToken.Should().BeTrue();
    rowVersionProperty.ValueGenerated.Should().Be(Microsoft.EntityFrameworkCore.Metadata.ValueGenerated.OnAddOrUpdate);
}
```

在 `Leno.Order.Domain.Tests/OrderTests.cs` 中追加测试，验证 Order 聚合根含 RowVersion 属性：

```csharp
// 文件：src/Services/Order/Leno.Order.Domain.Tests/OrderTests.cs
// 在 OrderTests 类内追加以下测试方法

[Fact]
public void Order_Should_Have_RowVersion_Property_Initialized_Empty()
{
    var order = CreateOrder();

    order.RowVersion.Should().NotBeNull();
    order.RowVersion.Should().HaveCount(0);
}
```

#### 步骤 2：验证失败

```bash
dotnet test src/Services/Order/Leno.Order.Infrastructure.Tests/Leno.Order.Infrastructure.Tests.csproj \
  --filter "FullyQualifiedName~OrderConfiguration_Should_Have_RowVersion_Concurrency_Token"
dotnet test src/Services/Order/Leno.Order.Domain.Tests/Leno.Order.Domain.Tests.csproj \
  --filter "FullyQualifiedName~Order_Should_Have_RowVersion_Property_Initialized_Empty"
```

预期：2 个测试失败。`Order` 类当前无 `RowVersion` 属性，`OrderConfiguration` 未配置并发令牌。

#### 步骤 3：实现

**3.1** 在 `Order` 聚合根增加 `RowVersion` 属性：

```csharp
// 文件：src/Services/Order/Leno.Order.Domain/Aggregates/Order.cs
// 在 Order 类的属性区域（紧接 CancelReason 属性之后，约第 97 行后）追加：

    /// <summary>乐观并发控制版本号，由 EF Core 自动生成与校验。</summary>
    public byte[] RowVersion { get; private set; } = Array.Empty<byte>();
```

**3.2** 在 `OrderConfiguration` 配置 RowVersion 为并发令牌：

```csharp
// 文件：src/Services/Order/Leno.Order.Infrastructure/Configurations/OrderConfiguration.cs
// 在 Configure 方法中（紧接 CancelReason 属性配置之后，约第 42 行后）追加：

        // 乐观并发控制：RowVersion 由数据库自动生成与校验，并发写入时抛 DbUpdateConcurrencyException
        builder.Property(o => o.RowVersion).HasColumnName("row_version").IsRowVersion();
```

**3.3** 新增 EF Core 迁移：

```bash
dotnet ef migrations add AddOrderRowVersion \
  --project src/Services/Order/Leno.Order.Infrastructure \
  --startup-project src/Services/Order/Leno.Order.Api
```

#### 步骤 4：验证通过

```bash
dotnet test src/Services/Order/Leno.Order.Infrastructure.Tests/Leno.Order.Infrastructure.Tests.csproj \
  --filter "FullyQualifiedName~OrderConfiguration_Should_Have_RowVersion_Concurrency_Token"
dotnet test src/Services/Order/Leno.Order.Domain.Tests/Leno.Order.Domain.Tests.csproj \
  --filter "FullyQualifiedName~Order_Should_Have_RowVersion_Property_Initialized_Empty"
```

预期：2 个测试通过。全量回归测试确保现有功能不受影响：

```bash
dotnet test src/Services/Order/Leno.Order.Domain.Tests/Leno.Order.Domain.Tests.csproj
```

#### 步骤 5：提交

```bash
git add src/Services/Order/Leno.Order.Domain/Aggregates/Order.cs \
        src/Services/Order/Leno.Order.Infrastructure/Configurations/OrderConfiguration.cs \
        src/Services/Order/Leno.Order.Infrastructure/Migrations/*_AddOrderRowVersion*.cs \
        src/Services/Order/Leno.Order.Infrastructure.Tests/OrderConfigurationTests.cs \
        src/Services/Order/Leno.Order.Domain.Tests/OrderTests.cs
git commit -m "fix(order): Order 聚合根增加 RowVersion 乐观并发控制（2.3）

OrderConfiguration 配置 row_version 为 IsRowVersion 并发令牌，并发写入时抛
DbUpdateConcurrencyException 而非静默覆盖。消除支付回调/超时取消/Cancel/ForceCancel
并发场景下的状态机冲突资损风险。"
```

---

### P0-T4：支付成功消费者跨进程边界无原子性，Redis 库存可能被错误扣减（审计 #4 / 2.4）

**审计位置**：[file:///workspace/docs/superpowers/specs/2026-07-21-code-audit/04-order.md#L60-L66]
**代码位置**：[file:///workspace/src/Services/Order/Leno.Order.Infrastructure/Consumers/PaymentSucceededEventConsumer.cs#L44-L90]
**代码位置**：[file:///workspace/src/Services/Order/Leno.Order.Infrastructure/Services/StockReservationDomainService.cs#L71-L78]

**根因**：`PaymentSucceededEventConsumer` 执行顺序：`order.MarkAsPaid` → `_stockService.ConfirmBatchAsync`（Redis） → `_pointsAntiCorruption.ConfirmDeductionAsync`（HTTP） → `SaveEntitiesAsync`（DB）。前两个调用在 Redis/远程完成，最后一个才落 DB。如果积分确认抛异常，DB 事务回滚（订单仍为 PendingPayment），但 Redis 库存已被部分扣减。MassTransit 重试时 EventId 幂等去重直接跳过，Redis 库存状态永久错误。

**修复方案**：将 `order.MarkAsPaid` 与 Outbox 事件同事务持久化（仅更新订单状态 + 发布 `OrderPaidDomainEvent`），库存确认改为消费 `OrderPaidEvent` 的独立消费者 `StockConfirmConsumer`，使其可独立重试且通过事件幂等键去重。

---

#### 步骤 1：测试

在 `Leno.Order.Infrastructure.Tests/PaymentSucceededEventConsumerTests.cs` 中追加测试，验证消费者仅更新订单状态，不直接调用库存确认。

```csharp
// 文件：src/Services/Order/Leno.Order.Infrastructure.Tests/PaymentSucceededEventConsumerTests.cs
// 在 PaymentSucceededEventConsumerTests 类内追加以下测试方法

[Fact]
public async Task HandleAsync_Should_Only_MarkAsPaid_And_Not_Call_StockConfirm_Directly()
{
    // Arrange
    var sut = CreateSut(out var orderRepoMock, out var uowMock, out var stockServiceMock,
        out var pointsMock, out var loggerMock, out var idempotencyMock);

    var order = CreatePendingPaymentOrder();
    order.MarkPaymentInitiated(PaymentMethod.WeChatPay);
    // 重置领域事件以隔离测试
    order.ClearDomainEvents();

    orderRepoMock.Setup(r => r.GetByIdAsync(order.Id, It.IsAny<CancellationToken>()))
        .ReturnsAsync(order);

    var integrationEvent = new PaymentSucceededEvent(
        eventId: Guid.NewGuid(),
        idempotencyKey: "test-key",
        orderId: order.Id,
        paymentId: Guid.NewGuid(),
        channel: "WeChatPay",
        paidAt: DateTime.UtcNow,
        tradeNo: "T001",
        amount: order.TotalAmount,
        currency: "CNY");

    idempotencyMock.Setup(i => i.IsProcessedAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
        .ReturnsAsync(false);

    // Act
    await sut.HandleAsync(integrationEvent, CancellationToken.None);

    // Assert：消费者仅调用 MarkAsPaid + SaveEntitiesAsync，不直接调用 ConfirmBatchAsync
    order.Status.Should().Be(OrderStatus.Paid);
    stockServiceMock.Verify(
        s => s.ConfirmBatchAsync(It.IsAny<Guid>(), It.IsAny<Dictionary<Guid, int>>(), It.IsAny<CancellationToken>()),
        Times.Never);
    pointsMock.Verify(
        p => p.ConfirmDeductionAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()),
        Times.Never);
    uowMock.Verify(u => u.SaveEntitiesAsync(It.IsAny<CancellationToken>()), Times.Once);
    // 领域事件 OrderPaidDomainEvent 应通过 Outbox 持久化
    order.DomainEvents.Should().Contain(e => e is OrderPaidDomainEvent);
}
```

新增 `StockConfirmConsumer` 测试：

```csharp
// 文件：src/Services/Order/Leno.Order.Infrastructure.Tests/StockConfirmConsumerTests.cs（新建）

using Leno.Order.Domain.Aggregates;
using Leno.Order.Domain.Repositories;
using Leno.Order.Domain.Services;
using Leno.Order.Infrastructure.Consumers;
using Leno.Order.Infrastructure.Services;
using Leno.SharedContracts.Events;
using Microsoft.Extensions.Logging;
using Moq;

namespace Leno.Order.Infrastructure.Tests;

public sealed class StockConfirmConsumerTests
{
    private static readonly Guid OrderId = Guid.NewGuid();
    private static readonly Guid SkuId = Guid.NewGuid();

    [Fact]
    public async Task HandleAsync_Should_Confirm_Stock_For_Paid_Order()
    {
        // Arrange
        var orderRepoMock = new Mock<IOrderRepository>();
        var stockServiceMock = new Mock<IStockReservationDomainService>();
        var uowMock = new Mock<IUnitOfWork>();
        var loggerMock = new Mock<ILogger<StockConfirmConsumer>>();
        var idempotencyMock = new Mock<IIdempotencyStore>();

        var order = CreatePaidOrderWithItems();
        orderRepoMock.Setup(r => r.GetByIdAsync(OrderId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(order);
        stockServiceMock.Setup(s => s.ConfirmBatchAsync(It.IsAny<Guid>(), It.IsAny<Dictionary<Guid, int>>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var sut = new StockConfirmConsumer(
            orderRepoMock.Object, stockServiceMock.Object, uowMock.Object,
            loggerMock.Object, idempotencyMock.Object);

        var evt = new OrderPaidEvent(
            eventId: Guid.NewGuid(),
            idempotencyKey: "stock-confirm-key",
            orderId: OrderId,
            userId: Guid.NewGuid(),
            sellerId: Guid.NewGuid(),
            paymentId: Guid.NewGuid(),
            channel: "WeChatPay",
            paidAt: DateTime.UtcNow,
            tradeNo: "T001",
            amount: 100m,
            currency: "CNY");

        idempotencyMock.Setup(i => i.IsProcessedAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        // Act
        await sut.HandleAsync(evt, CancellationToken.None);

        // Assert
        stockServiceMock.Verify(
            s => s.ConfirmBatchAsync(OrderId, It.IsAny<Dictionary<Guid, int>>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    private static Order CreatePaidOrderWithItems()
    {
        var snapshot = ProductSnapshot.Create(SkuId, Guid.NewGuid(), "商品", "规格", null, Guid.NewGuid());
        var item = OrderItem.Create(Guid.NewGuid(), SkuId, snapshot, 100m, 1, null);
        var order = Order.Create(
            OrderId, "ORD-001", OrderType.Normal, Guid.NewGuid(), Guid.NewGuid(),
            new List<OrderItem> { item }, AddressSnapshot.Create("张三", "13800000000", "北京", "北京", "海淀区", "xx路"),
            10m, 0m, DateTime.UtcNow.AddHours(1));
        order.MarkPaymentInitiated(PaymentMethod.WeChatPay);
        order.MarkAsPaid(Guid.NewGuid(), "WeChatPay", DateTime.UtcNow, "T001");
        return order;
    }
}
```

#### 步骤 2：验证失败

```bash
dotnet test src/Services/Order/Leno.Order.Infrastructure.Tests/Leno.Order.Infrastructure.Tests.csproj \
  --filter "FullyQualifiedName~HandleAsync_Should_Only_MarkAsPaid|FullyQualifiedName~StockConfirmConsumerTests"
```

预期：测试失败。`PaymentSucceededEventConsumer` 当前直接调用 `ConfirmBatchAsync`，`StockConfirmConsumer` 类不存在导致编译失败。

#### 步骤 3：实现

**3.1** 修改 `PaymentSucceededEventConsumer.HandleAsync`，移除直接库存/积分确认，仅更新订单状态：

```csharp
// 文件：src/Services/Order/Leno.Order.Infrastructure/Consumers/PaymentSucceededEventConsumer.cs
// 替换 HandleAsync 方法第 44-90 行为：

    /// <inheritdoc />
    protected override async Task HandleAsync(PaymentSucceededEvent integrationEvent, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(integrationEvent);

        var order = await _orderRepository.GetByIdAsync(integrationEvent.OrderId, ct);
        if (order is null)
        {
            Logger.LogInformation("支付成功事件：订单不存在 OrderId={OrderId}，跳过", integrationEvent.OrderId);
            return;
        }

        if (order.Status != OrderStatus.PendingPayment)
        {
            Logger.LogInformation("支付成功事件：订单 {OrderId} 当前状态 {Status} 非待支付，跳过",
                integrationEvent.OrderId, order.Status);
            return;
        }

        // 校验支付金额与订单金额一致（P0-T6 联动修复）
        if (integrationEvent.Amount != order.TotalAmount)
        {
            Logger.LogError("支付成功事件：支付金额 {PaidAmount} 与订单金额 {OrderAmount} 不匹配 OrderId={OrderId}",
                integrationEvent.Amount, order.TotalAmount, integrationEvent.OrderId);
            throw new OrderDomainException(
                $"支付金额不匹配：应付 {order.TotalAmount}，实付 {integrationEvent.Amount}",
                "ORDER_PAID_AMOUNT_MISMATCH");
        }

        // 仅更新订单状态并发布 OrderPaidDomainEvent（经 Outbox 同事务持久化）
        // 库存确认与积分确认由独立消费者 StockConfirmConsumer / PointsConfirmConsumer 消费 OrderPaidEvent 执行
        order.MarkAsPaid(integrationEvent.PaymentId, integrationEvent.Channel, integrationEvent.PaidAt, integrationEvent.TradeNo, integrationEvent.Amount);

        // 会员订阅订单支付后自动完成（无发货流程）
        if (order.OrderType == OrderType.Membership)
        {
            order.CompleteMembershipOrder();
        }

        await _orderRepository.UpdateAsync(order, ct);
        await _unitOfWork.SaveEntitiesAsync(ct);

        Logger.LogInformation("订单 {OrderId} 已标记支付成功 PaymentId={PaymentId} TradeNo={TradeNo}",
            integrationEvent.OrderId, integrationEvent.PaymentId, integrationEvent.TradeNo);
    }
```

**3.2** 新增 `StockConfirmConsumer`，消费 `OrderPaidEvent` 执行库存确认（可独立重试）：

```csharp
// 文件：src/Services/Order/Leno.Order.Infrastructure/Consumers/StockConfirmConsumer.cs（新建）

using Leno.Infrastructure.Abstractions;
using Leno.Infrastructure.EventBus;
using Leno.Order.Domain.Aggregates;
using Leno.Order.Domain.Exceptions;
using Leno.Order.Domain.Repositories;
using Leno.Order.Domain.Services;
using Leno.SharedContracts.Events;
using Leno.SharedKernel.Abstractions;
using Microsoft.Extensions.Logging;

namespace Leno.Order.Infrastructure.Consumers;

/// <summary>
/// 库存确认消费者，消费 OrderPaidEvent 执行预占→真实扣减的库存确认。
/// 独立于 PaymentSucceededEventConsumer，使其可独立重试且通过 EventId 幂等去重。
/// 仅对非会员订单执行（会员订单无发货流程，无需确认库存）。
/// </summary>
public sealed class StockConfirmConsumer : IntegrationEventConsumerBase<OrderPaidEvent>
{
    private readonly IOrderRepository _orderRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IStockReservationDomainService _stockService;

    public StockConfirmConsumer(
        IOrderRepository orderRepository,
        IStockReservationDomainService stockService,
        IUnitOfWork unitOfWork,
        ILogger<StockConfirmConsumer> logger,
        IIdempotencyStore idempotencyStore)
        : base(logger, idempotencyStore)
    {
        ArgumentNullException.ThrowIfNull(orderRepository);
        ArgumentNullException.ThrowIfNull(stockService);
        ArgumentNullException.ThrowIfNull(unitOfWork);
        _orderRepository = orderRepository;
        _stockService = stockService;
        _unitOfWork = unitOfWork;
    }

    /// <inheritdoc />
    protected override async Task HandleAsync(OrderPaidEvent integrationEvent, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(integrationEvent);

        var order = await _orderRepository.GetByIdAsync(integrationEvent.OrderId, ct);
        if (order is null)
        {
            Logger.LogInformation("库存确认：订单不存在 OrderId={OrderId}，跳过", integrationEvent.OrderId);
            return;
        }

        // 仅对已支付的非会员订单执行库存确认（会员订单支付后直接完成，无库存扣减）
        if (order.Status != OrderStatus.Paid || order.OrderType == OrderType.Membership)
        {
            Logger.LogInformation("库存确认：订单 {OrderId} 状态 {Status} 或类型 {Type} 跳过",
                integrationEvent.OrderId, order.Status, order.OrderType);
            return;
        }

        var skuQuantities = order.Items
            .GroupBy(i => i.SkuId)
            .ToDictionary(g => g.Key, g => g.Sum(i => i.Quantity));

        await _stockService.ConfirmBatchAsync(order.Id, skuQuantities, ct);

        Logger.LogInformation("库存确认扣减完成 OrderId={OrderId}", integrationEvent.OrderId);
    }
}
```

#### 步骤 4：验证通过

```bash
dotnet test src/Services/Order/Leno.Order.Infrastructure.Tests/Leno.Order.Infrastructure.Tests.csproj \
  --filter "FullyQualifiedName~HandleAsync_Should_Only_MarkAsPaid|FullyQualifiedName~StockConfirmConsumerTests"
```

预期：测试通过。

#### 步骤 5：提交

```bash
git add src/Services/Order/Leno.Order.Infrastructure/Consumers/PaymentSucceededEventConsumer.cs \
        src/Services/Order/Leno.Order.Infrastructure/Consumers/StockConfirmConsumer.cs \
        src/Services/Order/Leno.Order.Infrastructure.Tests/PaymentSucceededEventConsumerTests.cs \
        src/Services/Order/Leno.Order.Infrastructure.Tests/StockConfirmConsumerTests.cs
git commit -m "fix(order): 支付消费者原子性拆分，库存确认独立消费者（2.4）

PaymentSucceededEventConsumer 仅更新订单状态+发布 OrderPaidDomainEvent（经 Outbox
同事务），库存确认拆分到独立 StockConfirmConsumer 消费 OrderPaidEvent，使其可独立
重试且通过 EventId 幂等去重，避免 DB 回滚后 Redis 库存状态永久错误。"
```

---

### P0-T5：OrderTimeoutDelayMessageConsumer 与 AfterSalesWindowConsumer 缺失幂等键（审计 #5 / 2.5）

**审计位置**：[file:///workspace/docs/superpowers/specs/2026-07-21-code-audit/04-order.md#L68-L74]
**代码位置**：
- [file:///workspace/src/Services/Order/Leno.Order.Infrastructure/Consumers/OrderTimeoutDelayMessageConsumer.cs#L16-L92]（直接实现 IConsumer，无 IIdempotencyStore）
- [file:///workspace/src/Services/Order/Leno.Order.Infrastructure/Consumers/AfterSalesWindowConsumer.cs#L14-L59]（同上）

**根因**：两个延迟消息消费者直接实现 `IConsumer<T>`，未注册 `IIdempotencyStore` 幂等去重。`order.Cancel` → `_stockService.ReleaseBatchAsync` → `_pointsAntiCorruption.ReleaseAsync` → `_promotionAntiCorruption.ReleaseCouponsAsync` → `SaveEntitiesAsync` 之间任何一步抛异常，重试时会重复调用积分/优惠券释放远程接口。

---

#### 步骤 1：测试

在 `Leno.Order.Infrastructure.Tests/OrderTimeoutDelayMessageConsumerTests.cs` 中追加幂等性测试。

```csharp
// 文件：src/Services/Order/Leno.Order.Infrastructure.Tests/OrderTimeoutDelayMessageConsumerTests.cs
// 在 OrderTimeoutDelayMessageConsumerTests 类内追加以下测试方法

[Fact]
public async Task Consume_Should_Check_Idempotency_Before_Processing()
{
    // Arrange
    var orderRepoMock = new Mock<IOrderRepository>();
    var uowMock = new Mock<IUnitOfWork>();
    var stockServiceMock = new Mock<IStockReservationDomainService>();
    var pointsMock = new Mock<IPointsAntiCorruptionService>();
    var promotionMock = new Mock<IPromotionAntiCorruptionService>();
    var loggerMock = new Mock<ILogger<OrderTimeoutDelayMessageConsumer>>();
    var idempotencyMock = new Mock<IIdempotencyStore>();

    var order = CreateExpiredPendingOrder();
    orderRepoMock.Setup(r => r.GetByIdAsync(order.Id, It.IsAny<CancellationToken>()))
        .ReturnsAsync(order);

    // 幂等键已标记为已处理
    idempotencyMock.Setup(i => i.IsProcessedAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
        .ReturnsAsync(true);

    var sut = new OrderTimeoutDelayMessageConsumer(
        orderRepoMock.Object, uowMock.Object, stockServiceMock.Object,
        pointsMock.Object, promotionMock.Object, loggerMock.Object, idempotencyMock.Object);

    var context = new TestConsumeContext<OrderTimeoutMessage>(new OrderTimeoutMessage(order.Id));

    // Act
    await sut.Consume(context);

    // Assert：已处理的事件不应重复执行库存/积分/优惠券释放
    stockServiceMock.Verify(
        s => s.ReleaseBatchAsync(It.IsAny<Guid>(), It.IsAny<Dictionary<Guid, int>>(), It.IsAny<CancellationToken>()),
        Times.Never);
    pointsMock.Verify(p => p.ReleaseAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
    uowMock.Verify(u => u.SaveEntitiesAsync(It.IsAny<CancellationToken>()), Times.Never);
}

[Fact]
public async Task Consume_Should_Mark_Idempotency_After_Success()
{
    // Arrange
    var orderRepoMock = new Mock<IOrderRepository>();
    var uowMock = new Mock<IUnitOfWork>();
    var stockServiceMock = new Mock<IStockReservationDomainService>();
    var pointsMock = new Mock<IPointsAntiCorruptionService>();
    var promotionMock = new Mock<IPromotionAntiCorruptionService>();
    var loggerMock = new Mock<ILogger<OrderTimeoutDelayMessageConsumer>>();
    var idempotencyMock = new Mock<IIdempotencyStore>();

    var order = CreateExpiredPendingOrder();
    orderRepoMock.Setup(r => r.GetByIdAsync(order.Id, It.IsAny<CancellationToken>()))
        .ReturnsAsync(order);
    idempotencyMock.Setup(i => i.IsProcessedAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
        .ReturnsAsync(false);

    var sut = new OrderTimeoutDelayMessageConsumer(
        orderRepoMock.Object, uowMock.Object, stockServiceMock.Object,
        pointsMock.Object, promotionMock.Object, loggerMock.Object, idempotencyMock.Object);

    var context = new TestConsumeContext<OrderTimeoutMessage>(new OrderTimeoutMessage(order.Id));

    // Act
    await sut.Consume(context);

    // Assert：成功后应标记幂等键
    idempotencyMock.Verify(
        i => i.MarkAsProcessedAsync(It.IsAny<string>(), It.IsAny<TimeSpan>(), It.IsAny<CancellationToken>()),
        Times.Once);
    stockServiceMock.Verify(
        s => s.ReleaseBatchAsync(It.IsAny<Guid>(), It.IsAny<Dictionary<Guid, int>>(), It.IsAny<CancellationToken>()),
        Times.Once);
}
```

#### 步骤 2：验证失败

```bash
dotnet test src/Services/Order/Leno.Order.Infrastructure.Tests/Leno.Order.Infrastructure.Tests.csproj \
  --filter "FullyQualifiedName~Consume_Should_Check_Idempotency|FullyQualifiedName~Consume_Should_Mark_Idempotency"
```

预期：测试失败。`OrderTimeoutDelayMessageConsumer` 当前构造函数不接收 `IIdempotencyStore`。

#### 步骤 3：实现

**3.1** 修改 `OrderTimeoutDelayMessageConsumer`，注入 `IIdempotencyStore` 并在处理前检查幂等：

```csharp
// 文件：src/Services/Order/Leno.Order.Infrastructure/Consumers/OrderTimeoutDelayMessageConsumer.cs
// 完整文件内容

using Leno.Infrastructure.Abstractions;
using Leno.Order.Application.Messages;
using Leno.Order.Application.Services;
using Leno.Order.Domain.Repositories;
using Leno.Order.Domain.Services;
using Leno.Order.Domain.ValueObjects;
using Leno.SharedKernel.Abstractions;
using MassTransit;
using Microsoft.Extensions.Logging;

namespace Leno.Order.Infrastructure.Consumers;

/// <summary>
/// 订单超时延迟消息消费者，检查待支付订单是否已超时，超时则自动取消。
/// 通过 IIdempotencyStore 幂等去重（幂等键 = OrderId + 消息类型），避免重试时重复释放库存/积分/优惠券。
/// </summary>
public sealed class OrderTimeoutDelayMessageConsumer : IConsumer<OrderTimeoutMessage>
{
    private readonly IOrderRepository _orderRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IStockReservationDomainService _stockService;
    private readonly IPointsAntiCorruptionService _pointsAntiCorruption;
    private readonly IPromotionAntiCorruptionService _promotionAntiCorruption;
    private readonly ILogger<OrderTimeoutDelayMessageConsumer> _logger;
    private readonly IIdempotencyStore _idempotencyStore;
    private static readonly TimeSpan IdempotencyTtl = TimeSpan.FromHours(2);

    public OrderTimeoutDelayMessageConsumer(
        IOrderRepository orderRepository,
        IUnitOfWork unitOfWork,
        IStockReservationDomainService stockService,
        IPointsAntiCorruptionService pointsAntiCorruption,
        IPromotionAntiCorruptionService promotionAntiCorruption,
        ILogger<OrderTimeoutDelayMessageConsumer> logger,
        IIdempotencyStore idempotencyStore)
    {
        ArgumentNullException.ThrowIfNull(orderRepository);
        ArgumentNullException.ThrowIfNull(unitOfWork);
        ArgumentNullException.ThrowIfNull(stockService);
        ArgumentNullException.ThrowIfNull(pointsAntiCorruption);
        ArgumentNullException.ThrowIfNull(promotionAntiCorruption);
        ArgumentNullException.ThrowIfNull(logger);
        ArgumentNullException.ThrowIfNull(idempotencyStore);
        _orderRepository = orderRepository;
        _unitOfWork = unitOfWork;
        _stockService = stockService;
        _pointsAntiCorruption = pointsAntiCorruption;
        _promotionAntiCorruption = promotionAntiCorruption;
        _logger = logger;
        _idempotencyStore = idempotencyStore;
    }

    /// <inheritdoc />
    public async Task Consume(ConsumeContext<OrderTimeoutMessage> context)
    {
        ArgumentNullException.ThrowIfNull(context);
        var msg = context.Message;

        // 幂等去重：用 OrderId + 消息类型作为幂等键
        var idempotencyKey = $"order-timeout:{msg.OrderId}";
        if (await _idempotencyStore.IsProcessedAsync(idempotencyKey, context.CancellationToken))
        {
            _logger.LogInformation("超时取消：订单 {OrderId} 已处理过，跳过", msg.OrderId);
            return;
        }

        var order = await _orderRepository.GetByIdAsync(msg.OrderId, context.CancellationToken);
        if (order is null)
        {
            _logger.LogInformation("超时取消：订单不存在 OrderId={OrderId}，跳过", msg.OrderId);
            return;
        }

        if (order.Status != OrderStatus.PendingPayment)
        {
            _logger.LogInformation("超时取消：订单 {OrderId} 当前状态 {Status} 非待支付，跳过",
                msg.OrderId, order.Status);
            await _idempotencyStore.MarkAsProcessedAsync(idempotencyKey, IdempotencyTtl, context.CancellationToken);
            return;
        }

        if (DateTime.UtcNow < order.ExpireAt)
        {
            _logger.LogInformation("超时取消：订单 {OrderId} 尚未到达支付截止时间 {ExpireAt}，跳过",
                msg.OrderId, order.ExpireAt);
            return;
        }

        order.Cancel("支付超时自动取消", "System");

        var skuQuantities = order.Items
            .GroupBy(i => i.SkuId)
            .ToDictionary(g => g.Key, g => g.Sum(i => i.Quantity));
        await _stockService.ReleaseBatchAsync(order.Id, skuQuantities, context.CancellationToken);
        await _pointsAntiCorruption.ReleaseAsync(order.Id, context.CancellationToken);
        await _promotionAntiCorruption.ReleaseCouponsAsync(order.Id, context.CancellationToken);

        await _orderRepository.UpdateAsync(order, context.CancellationToken);
        await _unitOfWork.SaveEntitiesAsync(context.CancellationToken);

        // 成功后标记幂等键
        await _idempotencyStore.MarkAsProcessedAsync(idempotencyKey, IdempotencyTtl, context.CancellationToken);

        _logger.LogInformation("订单 {OrderId} 因支付超时已自动取消", msg.OrderId);
    }
}
```

**3.2** 同样修改 `AfterSalesWindowConsumer`，注入 `IIdempotencyStore`：

```csharp
// 文件：src/Services/Order/Leno.Order.Infrastructure/Consumers/AfterSalesWindowConsumer.cs
// 完整文件内容

using Leno.Infrastructure.Abstractions;
using Leno.Order.Application.Messages;
using Leno.Order.Domain.Repositories;
using Leno.Order.Domain.ValueObjects;
using Leno.SharedKernel.Abstractions;
using MassTransit;
using Microsoft.Extensions.Logging;

namespace Leno.Order.Infrastructure.Consumers;

/// <summary>
/// 售后窗口结束延迟消息消费者，在售后窗口到期后关闭订单的售后窗口。
/// 通过 IIdempotencyStore 幂等去重（幂等键 = OrderId + 消息类型）。
/// </summary>
public sealed class AfterSalesWindowConsumer : IConsumer<AfterSalesWindowMessage>
{
    private readonly IOrderRepository _orderRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<AfterSalesWindowConsumer> _logger;
    private readonly IIdempotencyStore _idempotencyStore;
    private static readonly TimeSpan IdempotencyTtl = TimeSpan.FromHours(2);

    public AfterSalesWindowConsumer(
        IOrderRepository orderRepository,
        IUnitOfWork unitOfWork,
        ILogger<AfterSalesWindowConsumer> logger,
        IIdempotencyStore idempotencyStore)
    {
        ArgumentNullException.ThrowIfNull(orderRepository);
        ArgumentNullException.ThrowIfNull(unitOfWork);
        ArgumentNullException.ThrowIfNull(logger);
        ArgumentNullException.ThrowIfNull(idempotencyStore);
        _orderRepository = orderRepository;
        _unitOfWork = unitOfWork;
        _logger = logger;
        _idempotencyStore = idempotencyStore;
    }

    /// <inheritdoc />
    public async Task Consume(ConsumeContext<AfterSalesWindowMessage> context)
    {
        ArgumentNullException.ThrowIfNull(context);
        var msg = context.Message;

        var idempotencyKey = $"after-sales-window:{msg.OrderId}";
        if (await _idempotencyStore.IsProcessedAsync(idempotencyKey, context.CancellationToken))
        {
            _logger.LogInformation("售后窗口关闭：订单 {OrderId} 已处理过，跳过", msg.OrderId);
            return;
        }

        var order = await _orderRepository.GetByIdAsync(msg.OrderId, context.CancellationToken);
        if (order is null)
        {
            _logger.LogInformation("售后窗口关闭：订单不存在 OrderId={OrderId}，跳过", msg.OrderId);
            return;
        }

        if (order.Status != OrderStatus.Completed)
        {
            _logger.LogInformation("售后窗口关闭：订单 {OrderId} 当前状态 {Status} 非已完成，跳过",
                msg.OrderId, order.Status);
            await _idempotencyStore.MarkAsProcessedAsync(idempotencyKey, IdempotencyTtl, context.CancellationToken);
            return;
        }

        order.CloseAfterSalesWindow();

        await _orderRepository.UpdateAsync(order, context.CancellationToken);
        await _unitOfWork.SaveEntitiesAsync(context.CancellationToken);

        await _idempotencyStore.MarkAsProcessedAsync(idempotencyKey, IdempotencyTtl, context.CancellationToken);

        _logger.LogInformation("订单 {OrderId} 售后窗口已关闭", msg.OrderId);
    }
}
```

#### 步骤 4：验证通过

```bash
dotnet test src/Services/Order/Leno.Order.Infrastructure.Tests/Leno.Order.Infrastructure.Tests.csproj \
  --filter "FullyQualifiedName~Consume_Should_Check_Idempotency|FullyQualifiedName~Consume_Should_Mark_Idempotency"
```

预期：测试通过。

#### 步骤 5：提交

```bash
git add src/Services/Order/Leno.Order.Infrastructure/Consumers/OrderTimeoutDelayMessageConsumer.cs \
        src/Services/Order/Leno.Order.Infrastructure/Consumers/AfterSalesWindowConsumer.cs \
        src/Services/Order/Leno.Order.Infrastructure.Tests/OrderTimeoutDelayMessageConsumerTests.cs
git commit -m "fix(order): 延迟消息消费者增加幂等键去重（2.5）

OrderTimeoutDelayMessageConsumer 与 AfterSalesWindowConsumer 注入 IIdempotencyStore，
用 OrderId+消息类型作为幂等键，处理前检查已处理状态，成功后标记幂等键，避免重试时
重复释放库存/积分/优惠券。"
```

---

### P0-T6：Order.MarkAsPaid 缺支付金额与 PaymentInitiated 校验（审计 #6 / 2.6）

**审计位置**：[file:///workspace/docs/superpowers/specs/2026-07-21-code-audit/04-order.md#L76-L94]
**代码位置**：[file:///workspace/src/Services/Order/Leno.Order.Domain/Aggregates/Order.cs#L326-L347]
**代码位置**：[file:///workspace/src/Services/Order/Leno.Order.Infrastructure/Consumers/PaymentSucceededEventConsumer.cs#L44-L62]

**根因**：`MarkAsPaid` 仅校验 `Status == PendingPayment`，未校验：1) `PaymentInitiated == true`；2) 实付金额是否等于 `TotalAmount`；3) `paymentId` 是否非空。`PaymentSucceededEventConsumer` 接到事件后直接 `MarkAsPaid`，未校验 `integrationEvent.Amount == order.TotalAmount`。

---

#### 步骤 1：测试

在 `Leno.Order.Domain.Tests/OrderTests.cs` 中追加测试。

```csharp
// 文件：src/Services/Order/Leno.Order.Domain.Tests/OrderTests.cs
// 在 OrderTests 类内追加以下测试方法

[Fact]
public void MarkAsPaid_NotInitiated_ShouldThrowException()
{
    var order = CreateOrder();

    var act = () => order.MarkAsPaid(Guid.NewGuid(), "WeChatPay", DateTime.UtcNow, "T001", 99.99m + 10m);

    act.Should().Throw<OrderDomainException>().Which.ErrorCode.Should().Be("ORDER_PAY_NOT_INITIATED");
}

[Fact]
public void MarkAsPaid_AmountMismatch_ShouldThrowException()
{
    var order = CreateOrder();
    order.MarkPaymentInitiated(PaymentMethod.WeChatPay);

    var act = () => order.MarkAsPaid(Guid.NewGuid(), "WeChatPay", DateTime.UtcNow, "T001", 50m);

    act.Should().Throw<OrderDomainException>().Which.ErrorCode.Should().Be("ORDER_PAID_AMOUNT_MISMATCH");
}

[Fact]
public void MarkAsPaid_EmptyPaymentId_ShouldThrowException()
{
    var order = CreateOrder();
    order.MarkPaymentInitiated(PaymentMethod.WeChatPay);

    var act = () => order.MarkAsPaid(Guid.Empty, "WeChatPay", DateTime.UtcNow, "T001", 99.99m + 10m);

    act.Should().Throw<OrderDomainException>().Which.ErrorCode.Should().Be("ORDER_PAYMENT_ID_EMPTY");
}

[Fact]
public void MarkAsPaid_ValidWithAmount_ShouldSucceed()
{
    var order = CreateOrder();
    order.MarkPaymentInitiated(PaymentMethod.WeChatPay);

    order.MarkAsPaid(Guid.NewGuid(), "WeChatPay", DateTime.UtcNow, "T001", 99.99m + 10m);

    order.Status.Should().Be(OrderStatus.Paid);
}
```

#### 步骤 2：验证失败

```bash
dotnet test src/Services/Order/Leno.Order.Domain.Tests/Leno.Order.Domain.Tests.csproj \
  --filter "FullyQualifiedName~MarkAsPaid_NotInitiated|FullyQualifiedName~MarkAsPaid_AmountMismatch|FullyQualifiedName~MarkAsPaid_EmptyPaymentId|FullyQualifiedName~MarkAsPaid_ValidWithAmount"
```

预期：4 个测试失败。`MarkAsPaid` 当前签名不含 `paidAmount` 参数，编译失败。

#### 步骤 3：实现

修改 `Order.MarkAsPaid` 方法，增加 `paidAmount` 参数与校验逻辑：

```csharp
// 文件：src/Services/Order/Leno.Order.Domain/Aggregates/Order.cs
// 替换第 326-347 行的 MarkAsPaid 方法为：

    /// <summary>
    /// 标记支付成功，校验待支付态、支付已发起、支付金额匹配、支付单标识非空，置已支付态并发布 <see cref="OrderPaidEvent"/>。
    /// </summary>
    /// <param name="paymentId">支付单标识。</param>
    /// <param name="channel">支付渠道。</param>
    /// <param name="paidAt">支付时间（UTC）。</param>
    /// <param name="tradeNo">第三方交易号。</param>
    /// <param name="paidAmount">实付金额，须等于 <see cref="TotalAmount"/>。</param>
    public void MarkAsPaid(Guid paymentId, string channel, DateTime paidAt, string tradeNo, decimal paidAmount)
    {
        if (Status != OrderStatus.PendingPayment)
        {
            throw new OrderDomainException(
                $"当前状态 {Status} 不可标记支付，仅 PendingPayment 可支付",
                "ORDER_PAID_STATUS_INVALID");
        }

        if (!PaymentInitiated)
        {
            throw new OrderDomainException(
                "支付未发起，不可标记支付成功",
                "ORDER_PAY_NOT_INITIATED");
        }

        if (paymentId == Guid.Empty)
        {
            throw new OrderDomainException("支付单标识不可为空", "ORDER_PAYMENT_ID_EMPTY");
        }

        if (paidAmount != TotalAmount)
        {
            throw new OrderDomainException(
                $"支付金额不匹配：应付 {TotalAmount}，实付 {paidAmount}",
                "ORDER_PAID_AMOUNT_MISMATCH");
        }

        Status = OrderStatus.Paid;
        PaymentId = paymentId;
        PaidAt = paidAt;
        TradeNo = tradeNo;
        AddDomainEvent(new OrderPaidDomainEvent(Id, UserId, SellerId ?? Guid.Empty, paymentId, channel, paidAt, tradeNo, TotalAmount, "CNY"));
    }
```

同步更新所有调用 `MarkAsPaid` 的位置（`PaymentSucceededEventConsumer` 在 P0-T4 已更新；`OrderTests.cs` 中已有的 `FullStateMachine_ShouldFlowCorrectly`、`MarkAsPaid_NotPendingPayment_ShouldThrowException` 等测试需补充 `paidAmount` 参数）。

#### 步骤 4：验证通过

```bash
dotnet test src/Services/Order/Leno.Order.Domain.Tests/Leno.Order.Domain.Tests.csproj
```

预期：全量测试通过，包括新增的 4 个测试与已有的状态机测试（已补充 `paidAmount` 参数）。

#### 步骤 5：提交

```bash
git add src/Services/Order/Leno.Order.Domain/Aggregates/Order.cs \
        src/Services/Order/Leno.Order.Domain.Tests/OrderTests.cs
git commit -m "fix(order): MarkAsPaid 增加 PaymentInitiated 与支付金额校验（2.6）

MarkAsPaid 新增 paidAmount 参数，校验支付已发起（PaymentInitiated）、支付单标识
非空、实付金额等于应付金额。消除支付回调金额不匹配时仍标记已支付的资损风险。"
```

---

### P0-T7：Saga 补偿失败静默吞掉，造成资源泄漏（审计 #7 / 2.7）

**审计位置**：[file:///workspace/docs/superpowers/specs/2026-07-21-code-audit/04-order.md#L96-L102]
**代码位置**：[file:///workspace/src/Services/Order/Leno.Order.Application/Services/OrderSagaOrchestrator.cs#L204-L256]
**代码位置**：[file:///workspace/src/Services/Order/Leno.Order.Application/Services/OrderSagaOrchestrator.cs#L58-L85]

**根因**：`CompensateAsync` 中释放优惠券/积分/库存的失败均被 try-catch 后仅 `_logger.LogError`，不抛出。如果 Saga 第二组失败，第一组已预占的库存/已冻结的积分/已锁定的优惠券补偿失败时，Saga 直接抛原始异常给客户端，已成功的预占库存/冻结积分永久占用。

**修复方案**：将补偿失败记录到统一的补偿表 `SagaCompensationRecord`（复用 `StockReservationCompensation` 模式），后台任务重试；补偿失败时抛出 `SagaCompensationFailedException` 触发告警。

---

#### 步骤 1：测试

在 `Leno.Order.Application.Tests/OrderSagaOrchestratorTests.cs` 中追加测试。

```csharp
// 文件：src/Services/Order/Leno.Order.Application.Tests/OrderSagaOrchestratorTests.cs
// 在 OrderSagaOrchestratorTests 类内追加以下测试方法

[Fact]
public async Task CompensateAsync_WhenStockReleaseFails_Should_Throw_SagaCompensationFailedException()
{
    // Arrange
    var sut = CreateSut(out var orderRepoMock, out var uowMock, out var orderNoGenMock,
        out var stockServiceMock, out var pricingMock, out var freightMock,
        out var promotionMock, out var pointsMock, out var busMock, out var loggerMock);

    // 第一组成功，第二组失败触发补偿
    stockServiceMock.Setup(s => s.ReserveBatchAsync(It.IsAny<Guid>(), It.IsAny<Dictionary<Guid, int>>(), It.IsAny<CancellationToken>()))
        .ReturnsAsync(true);
    pointsMock.Setup(p => p.FreezeAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
        .Returns(Task.CompletedTask);
    orderNoGenMock.Setup(g => g.GenerateAsync(It.IsAny<CancellationToken>()))
        .ReturnsAsync("ORD-001");

    // 第二组预占失败
    stockServiceMock.SetupSequence(s => s.ReserveBatchAsync(It.IsAny<Guid>(), It.IsAny<Dictionary<Guid, int>>(), It.IsAny<CancellationToken>()))
        .ReturnsAsync(true)   // 第一组成功
        .ReturnsAsync(false); // 第二组失败

    // 补偿时释放库存失败
    stockServiceMock.Setup(s => s.ReleaseBatchAsync(It.IsAny<Guid>(), It.IsAny<Dictionary<Guid, int>>(), It.IsAny<CancellationToken>()))
        .ThrowsAsync(new InvalidOperationException("redis down"));

    var context = CreateSagaContextWithTwoGroups();

    // Act
    var act = async () => await sut.ExecuteAsync(context, CancellationToken.None);

    // Assert：应抛出 SagaCompensationFailedException 而非静默吞掉
    await act.Should().ThrowAsync<SagaCompensationFailedException>();
}

[Fact]
public async Task CompensateAsync_WhenPointsReleaseFails_Should_Throw_SagaCompensationFailedException()
{
    // Arrange
    var sut = CreateSut(out var orderRepoMock, out var uowMock, out var orderNoGenMock,
        out var stockServiceMock, out var pricingMock, out var freightMock,
        out var promotionMock, out var pointsMock, out var busMock, out var loggerMock);

    stockServiceMock.SetupSequence(s => s.ReserveBatchAsync(It.IsAny<Guid>(), It.IsAny<Dictionary<Guid, int>>(), It.IsAny<CancellationToken>()))
        .ReturnsAsync(true)
        .ReturnsAsync(false);
    pointsMock.Setup(p => p.FreezeAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
        .Returns(Task.CompletedTask);
    pointsMock.Setup(p => p.ReleaseAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
        .ThrowsAsync(new InvalidOperationException("points service down"));
    orderNoGenMock.Setup(g => g.GenerateAsync(It.IsAny<CancellationToken>()))
        .ReturnsAsync("ORD-001");

    var context = CreateSagaContextWithTwoGroups();

    // Act & Assert
    await FluentActions.Invoking(() => sut.ExecuteAsync(context, CancellationToken.None))
        .Should().ThrowAsync<SagaCompensationFailedException>();
}
```

#### 步骤 2：验证失败

```bash
dotnet test src/Services/Order/Leno.Order.Application.Tests/Leno.Order.Application.Tests.csproj \
  --filter "FullyQualifiedName~CompensateAsync_WhenStockReleaseFails|FullyQualifiedName~CompensateAsync_WhenPointsReleaseFails"
```

预期：测试失败。`SagaCompensationFailedException` 类不存在，`CompensateAsync` 当前仅 `LogError` 不抛出。

#### 步骤 3：实现

**3.1** 新增 `SagaCompensationFailedException`：

```csharp
// 文件：src/Services/Order/Leno.Order.Domain/Exceptions/SagaCompensationFailedException.cs（新建）

namespace Leno.Order.Domain.Exceptions;

/// <summary>
/// Saga 补偿失败异常，表示至少一个补偿动作（释放库存/积分/优惠券）失败。
/// 触发该异常时应记录告警并人工介入，避免资源永久泄漏。
/// </summary>
public sealed class SagaCompensationFailedException : Exception
{
    /// <summary>补偿失败的分组信息列表。</summary>
    public IReadOnlyList<CompensationFailure> Failures { get; }

    public SagaCompensationFailedException(IReadOnlyList<CompensationFailure> failures)
        : base($"Saga 补偿失败，{failures.Count} 个补偿动作失败：{string.Join("; ", failures.Select(f => $"{f.ActionType} OrderId={f.OrderId}: {f.ErrorMessage}"))}")
    {
        Failures = failures;
    }
}

/// <summary>
/// 补偿动作失败记录。
/// </summary>
public sealed record CompensationFailure(Guid OrderId, string ActionType, string ErrorMessage);
```

**3.2** 修改 `OrderSagaOrchestrator.CompensateAsync`，收集失败并在全部补偿后抛出异常：

```csharp
// 文件：src/Services/Order/Leno.Order.Application/Services/OrderSagaOrchestrator.cs
// 替换第 200-256 行的 CompensateAsync 方法为：

    /// <summary>
    /// 对已成功组逆序执行补偿：释放优惠券 → 释放积分 → 释放库存 → 移除未提交的订单聚合。
    /// 每个补偿动作独立 try/catch 收集失败，全部补偿后若有失败则抛 SagaCompensationFailedException 触发告警。
    /// </summary>
    private async Task CompensateAsync(List<CompletedGroup> completed, CancellationToken ct)
    {
        var failures = new List<CompensationFailure>();

        for (var i = completed.Count - 1; i >= 0; i--)
        {
            var g = completed[i];

            // 释放优惠券（若该组涉及优惠）
            if (g.HasDiscount)
            {
                try
                {
                    await _promotionAntiCorruption.ReleaseCouponsAsync(g.OrderId, ct);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Saga 补偿：释放优惠券失败 OrderId={OrderId}", g.OrderId);
                    failures.Add(new CompensationFailure(g.OrderId, "ReleaseCoupons", ex.Message));
                }
            }

            // 释放积分（若该组已冻结积分）
            if (g.PointsFrozen)
            {
                try
                {
                    await _pointsAntiCorruption.ReleaseAsync(g.OrderId, ct);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Saga 补偿：释放积分失败 OrderId={OrderId}", g.OrderId);
                    failures.Add(new CompensationFailure(g.OrderId, "ReleasePoints", ex.Message));
                }
            }

            // 释放预占库存
            try
            {
                await _stockService.ReleaseBatchAsync(g.OrderId, g.SkuQuantities, ct);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Saga 补偿：释放库存失败 OrderId={OrderId}", g.OrderId);
                failures.Add(new CompensationFailure(g.OrderId, "ReleaseStock", ex.Message));
            }

            // 移除未提交的订单聚合（Saga 失败未统一提交，聚合仅在变更跟踪器中）
            try
            {
                await _orderRepository.RemoveAsync(g.Order, ct);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Saga 补偿：移除订单聚合失败 OrderId={OrderId}", g.OrderId);
                failures.Add(new CompensationFailure(g.OrderId, "RemoveOrder", ex.Message));
            }
        }

        // 有补偿失败时抛异常触发告警（库存有 T18 补偿表兜底，但积分/优惠券无补偿表）
        if (failures.Count > 0)
        {
            throw new SagaCompensationFailedException(failures);
        }
    }
```

#### 步骤 4：验证通过

```bash
dotnet test src/Services/Order/Leno.Order.Application.Tests/Leno.Order.Application.Tests.csproj \
  --filter "FullyQualifiedName~CompensateAsync_WhenStockReleaseFails|FullyQualifiedName~CompensateAsync_WhenPointsReleaseFails"
```

预期：测试通过。

#### 步骤 5：提交

```bash
git add src/Services/Order/Leno.Order.Domain/Exceptions/SagaCompensationFailedException.cs \
        src/Services/Order/Leno.Order.Application/Services/OrderSagaOrchestrator.cs \
        src/Services/Order/Leno.Order.Application.Tests/OrderSagaOrchestratorTests.cs
git commit -m "fix(order): Saga 补偿失败抛异常触发告警而非静默吞掉（2.7）

新增 SagaCompensationFailedException，CompensateAsync 收集所有补偿失败后抛异常，
触发告警人工介入，避免预占库存/冻结积分/锁定优惠券永久泄漏。库存有 T18 补偿表
兜底，积分/优惠券无补偿表需人工介入。"
```

---

### P0-T8：OrderSagaOrchestrator 积分抵现绕过聚合不变量校验（审计 #8 / 2.8）

**审计位置**：[file:///workspace/docs/superpowers/specs/2026-07-21-code-audit/04-order.md#L104-L111]
**代码位置**：
- [file:///workspace/src/Services/Order/Leno.Order.Application/Services/OrderSagaOrchestrator.cs#L128-L177]（Saga 裁剪积分后直接传给 Order.Create）
- [file:///workspace/src/Services/Order/Leno.Order.Domain/Aggregates/Order.cs#L117-L210]（Order.Create 仅校验 pointsOffsetAmount ≤ ItemsAmount，未减优惠）
- [file:///workspace/src/Services/Order/Leno.Order.Domain/Aggregates/Order.cs#L264-L294]（ApplyPointsOffset 校验 maxOffset = ItemsAmount - DiscountAmount，但未被 Saga 调用）

**根因**：Saga 在 `ExecuteGroupAsync` 中按 `maxOffset = groupItemsAmount - discount` 裁剪积分抵现，然后直接传给 `Order.Create(pointsOffsetAmount: groupPointsOffset)`。但 `Order.Create` 仅校验 `pointsOffsetAmount ≤ ItemsAmount`（未减优惠）。之后 Saga 调用 `order.ApplyDiscount(discount, allocations)` 时未重新校验 PointsOffsetAmount。聚合不变量 `0 ≤ PointsOffsetAmount ≤ ItemsAmount - DiscountAmount` 仅在未被调用的 `ApplyPointsOffset` 中保证。

**修复方案**：Saga 调用 `Order.Create(pointsOffsetAmount: 0)`，然后依次调用 `ApplyDiscount` 和 `ApplyPointsOffset` 让聚合根自身维护不变量。

---

#### 步骤 1：测试

在 `Leno.Order.Application.Tests/OrderSagaOrchestratorTests.cs` 中追加测试，验证积分抵现经过聚合不变量校验。

```csharp
// 文件：src/Services/Order/Leno.Order.Application.Tests/OrderSagaOrchestratorTests.cs
// 在 OrderSagaOrchestratorTests 类内追加以下测试方法

[Fact]
public async Task ExecuteAsync_WithPointsAndDiscount_Should_Use_Aggregate_Invariants()
{
    // Arrange：积分抵现 + 优惠，验证 TotalAmount 不为负
    var sut = CreateSut(out var orderRepoMock, out var uowMock, out var orderNoGenMock,
        out var stockServiceMock, out var pricingMock, out var freightMock,
        out var promotionMock, out var pointsMock, out var busMock, out var loggerMock);

    var skuInfo = CreateSkuInfo(unitPrice: 100m);
    var checkoutItem = new CheckoutItemDto { SkuId = skuInfo.SkuId, Quantity = 1 };

    stockServiceMock.Setup(s => s.ReserveBatchAsync(It.IsAny<Guid>(), It.IsAny<Dictionary<Guid, int>>(), It.IsAny<CancellationToken>()))
        .ReturnsAsync(true);
    pointsMock.Setup(p => p.FreezeAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
        .Returns(Task.CompletedTask);
    promotionMock.Setup(p => p.CalculateDiscountAsync(It.IsAny<Guid>(), It.IsAny<List<(Guid, decimal)>>(), It.IsAny<CancellationToken>()))
        .ReturnsAsync(80m); // 优惠 80 元
    pricingMock.Setup(p => p.ValidatePricesAsync(It.IsAny<List<(Guid, decimal)>>(), It.IsAny<CancellationToken>()))
        .Returns(Task.CompletedTask);
    pricingMock.Setup(p => p.CalculateAndAllocateAsync(It.IsAny<decimal>(), It.IsAny<List<(Guid, decimal)>>(), It.IsAny<CancellationToken>()))
        .ReturnsAsync(new List<(Guid, decimal)> { (skuInfo.SkuId, 80m) });
    freightMock.Setup(f => f.CalculateAsync(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<int>(), It.IsAny<decimal>(), It.IsAny<CancellationToken>()))
        .ReturnsAsync(10m);
    orderNoGenMock.Setup(g => g.GenerateAsync(It.IsAny<CancellationToken>()))
        .ReturnsAsync("ORD-001");

    // 积分抵现原始金额 50 元（超过 ItemsAmount - Discount = 100 - 80 = 20，应被聚合裁剪到 20）
    var context = new OrderSagaContext
    {
        UserId = Guid.NewGuid(),
        Address = CreateTestAddress(),
        Groups = new List<OrderSagaGroupInput>
        {
            new()
            {
                SellerId = Guid.NewGuid(),
                Items = new List<CheckoutItemDto> { checkoutItem },
                SkuInfos = new Dictionary<Guid, SkuInfo> { { skuInfo.SkuId, skuInfo } },
                GroupPointsOffsetRaw = 50m,
                UsePoints = true
            }
        }
    };

    OrderAggregate capturedOrder = null!;
    orderRepoMock.Setup(r => r.AddAsync(It.IsAny<OrderAggregate>(), It.IsAny<CancellationToken>()))
        .Callback<OrderAggregate, CancellationToken>((o, _) => capturedOrder = o)
        .Returns(Task.CompletedTask);

    // Act
    await sut.ExecuteAsync(context, CancellationToken.None);

    // Assert：积分抵现应被聚合根裁剪到 ItemsAmount - DiscountAmount = 20
    capturedOrder.PointsOffsetAmount.Should().Be(20m);
    capturedOrder.DiscountAmount.Should().Be(80m);
    // TotalAmount = 100 - 80 - 20 + 10 = 10，不为负
    capturedOrder.TotalAmount.Should().Be(10m);
}
```

#### 步骤 2：验证失败

```bash
dotnet test src/Services/Order/Leno.Order.Application.Tests/Leno.Order.Application.Tests.csproj \
  --filter "FullyQualifiedName~ExecuteAsync_WithPointsAndDiscount_Should_Use_Aggregate_Invariants"
```

预期：测试失败。当前 Saga 直接传 `groupPointsOffset` 给 `Order.Create`，未调用 `ApplyPointsOffset`，积分抵现可能超过 `ItemsAmount - DiscountAmount`。

#### 步骤 3：实现

修改 `OrderSagaOrchestrator.ExecuteGroupAsync`，`Order.Create` 传入 `pointsOffsetAmount: 0`，然后依次调用 `ApplyDiscount` 和 `ApplyPointsOffset`：

```csharp
// 文件：src/Services/Order/Leno.Order.Application/Services/OrderSagaOrchestrator.cs
// 替换第 167-177 行（生成订单编号到应用优惠分摊）为：

        // 生成订单编号并创建订单聚合（积分抵现初始为 0，由 ApplyPointsOffset 校验不变量）
        var orderNo = await _orderNumberGenerator.GenerateAsync(ct);
        var order = OrderAggregate.Create(
            orderId, orderNo, OrderType.Normal, userId, group.SellerId,
            orderItems, address, freight, pointsOffsetAmount: 0m, DateTime.UtcNow.AddMinutes(30));

        // 应用优惠分摊（聚合根校验分摊总和与单项上限）
        if (discount > 0 && allocations.Count > 0)
        {
            order.ApplyDiscount(discount, allocations);
        }

        // 应用积分抵现（聚合根校验 0 ≤ pointsOffset ≤ ItemsAmount - DiscountAmount）
        // Saga 已按 maxOffset = groupItemsAmount - discount 裁剪，ApplyPointsOffset 会再次校验
        if (groupPointsOffset > 0)
        {
            order.ApplyPointsOffset(groupPointsOffset);
        }
```

#### 步骤 4：验证通过

```bash
dotnet test src/Services/Order/Leno.Order.Application.Tests/Leno.Order.Application.Tests.csproj \
  --filter "FullyQualifiedName~ExecuteAsync_WithPointsAndDiscount_Should_Use_Aggregate_Invariants"
```

预期：测试通过。

#### 步骤 5：提交

```bash
git add src/Services/Order/Leno.Order.Application/Services/OrderSagaOrchestrator.cs \
        src/Services/Order/Leno.Order.Application.Tests/OrderSagaOrchestratorTests.cs
git commit -m "fix(order): 积分抵现经聚合根 ApplyPointsOffset 校验不变量（2.8）

Saga 调用 Order.Create(pointsOffsetAmount: 0)，然后依次 ApplyDiscount 与
ApplyPointsOffset，让聚合根自身维护 0 ≤ PointsOffsetAmount ≤ ItemsAmount -
DiscountAmount 不变量，消除积分抵现+优惠导致 TotalAmount 为负的边界 Bug。"
```

---

### P0-T9：OrderPricingDomainService.ValidatePricesAsync N+1 远程调用且与 Saga 重复（审计 #9 / 2.9）

**审计位置**：[file:///workspace/docs/superpowers/specs/2026-07-21-code-audit/04-order.md#L113-L120]
**代码位置**：
- [file:///workspace/src/Services/Order/Leno.Order.Infrastructure/Services/OrderPricingDomainService.cs#L21-L33]（内部循环再次调用 GetSkuInfoAsync）
- [file:///workspace/src/Services/Order/Leno.Order.Application/Services/OrderSagaOrchestrator.cs#L114-L116]（Saga 调用 ValidatePricesAsync）
- [file:///workspace/src/Services/Order/Leno.Order.Application/Services/OrderAppService.cs#L78-L87]（AppService 已循环查询 SkuInfo）

**根因**：`OrderAppService.CreateOrderAsync` 已经循环调用 `GetSkuInfoAsync` 获取所有 SKU 信息并放入字典；Saga 内又调用 `ValidatePricesAsync`，该方法内部循环再次调用 `GetSkuInfoAsync`。N 个 SKU 在一次下单中触发 2N 次 HTTP 调用。

---

#### 步骤 1：测试

在 `Leno.Order.Infrastructure.Tests/OrderPricingDomainServiceTests.cs` 中追加测试。

```csharp
// 文件：src/Services/Order/Leno.Order.Infrastructure.Tests/OrderPricingDomainServiceTests.cs
// 在 OrderPricingDomainServiceTests 类内追加以下测试方法

[Fact]
public async Task ValidatePricesAsync_With_PreQueried_SkuInfos_Should_Not_Call_ProductAntiCorruption()
{
    // Arrange
    var productAntiCorruptionMock = new Mock<IProductAntiCorruptionService>();
    var sut = new OrderPricingDomainService(productAntiCorruptionMock.Object);

    var skuId1 = Guid.NewGuid();
    var skuId2 = Guid.NewGuid();
    var skuInfos = new Dictionary<Guid, SkuInfo>
    {
        { skuId1, new SkuInfo { SkuId = skuId1, UnitPrice = 100m, IsOnSale = true } },
        { skuId2, new SkuInfo { SkuId = skuId2, UnitPrice = 50m, IsOnSale = true } }
    };
    var skuPrices = new List<(Guid SkuId, decimal ExpectedPrice)>
    {
        { (skuId1, 100m) },
        { (skuId2, 50m) }
    };

    // Act
    await sut.ValidatePricesAsync(skuPrices, skuInfos, CancellationToken.None);

    // Assert：不应再次调用 ProductAntiCorruption（使用预查的字典）
    productAntiCorruptionMock.Verify(
        p => p.GetSkuInfoAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()),
        Times.Never);
}

[Fact]
public async Task ValidatePricesAsync_PriceChanged_Should_Throw()
{
    // Arrange
    var productAntiCorruptionMock = new Mock<IProductAntiCorruptionService>();
    var sut = new OrderPricingDomainService(productAntiCorruptionMock.Object);

    var skuId = Guid.NewGuid();
    var skuInfos = new Dictionary<Guid, SkuInfo>
    {
        { skuId, new SkuInfo { SkuId = skuId, UnitPrice = 100m, IsOnSale = true } }
    };
    var skuPrices = new List<(Guid SkuId, decimal ExpectedPrice)> { (skuId, 99m) };

    // Act & Assert
    var act = () => sut.ValidatePricesAsync(skuPrices, skuInfos, CancellationToken.None);
    await act.Should().ThrowAsync<OrderDomainException>().Which.ErrorCode.Should().Be("ORDER_PRICE_CHANGED");
}
```

#### 步骤 2：验证失败

```bash
dotnet test src/Services/Order/Leno.Order.Infrastructure.Tests/Leno.Order.Infrastructure.Tests.csproj \
  --filter "FullyQualifiedName~ValidatePricesAsync_With_PreQueried|FullyQualifiedName~ValidatePricesAsync_PriceChanged"
```

预期：测试失败。`ValidatePricesAsync` 当前签名不含 `IReadOnlyDictionary<Guid, SkuInfo>` 参数。

#### 步骤 3：实现

**3.1** 修改 `IOrderPricingDomainService.ValidatePricesAsync` 签名，增加预查字典参数：

```csharp
// 文件：src/Services/Order/Leno.Order.Domain/Services/IOrderPricingDomainService.cs
// 修改 ValidatePricesAsync 方法签名为：

    /// <summary>
    /// 价格防篡改校验：下单单价须与商品域当前售价一致。
    /// 接收预查的 SKU 信息字典，避免 N+1 远程调用。
    /// </summary>
    /// <param name="skuPrices">SKU 与期望单价列表。</param>
    /// <param name="skuInfos">预查的 SKU 信息字典（由应用层批量查询后传入）。</param>
    /// <param name="ct">取消令牌。</param>
    Task ValidatePricesAsync(List<(Guid SkuId, decimal ExpectedPrice)> skuPrices, IReadOnlyDictionary<Guid, SkuInfo> skuInfos, CancellationToken ct = default);
```

**3.2** 修改 `OrderPricingDomainService.ValidatePricesAsync` 实现，使用预查字典：

```csharp
// 文件：src/Services/Order/Leno.Order.Infrastructure/Services/OrderPricingDomainService.cs
// 替换第 21-33 行的 ValidatePricesAsync 方法为：

    /// <inheritdoc />
    public Task ValidatePricesAsync(List<(Guid SkuId, decimal ExpectedPrice)> skuPrices, IReadOnlyDictionary<Guid, SkuInfo> skuInfos, CancellationToken ct = default)
    {
        foreach (var (skuId, expectedPrice) in skuPrices)
        {
            if (!skuInfos.TryGetValue(skuId, out var skuInfo) || skuInfo is null)
            {
                throw new OrderDomainException($"SKU {skuId} 不存在或已下架", "ORDER_SKU_NOT_FOUND");
            }

            if (skuInfo.UnitPrice != expectedPrice)
            {
                throw new OrderDomainException("商品价格已变更，请重新下单", "ORDER_PRICE_CHANGED");
            }
        }

        return Task.CompletedTask;
    }
```

**3.3** 更新 `OrderSagaOrchestrator.ExecuteGroupAsync` 调用，传入预查的 `group.SkuInfos`：

```csharp
// 文件：src/Services/Order/Leno.Order.Application/Services/OrderSagaOrchestrator.cs
// 修改第 114-116 行为：

        // 价格防篡改校验（使用预查的 SkuInfos，避免 N+1）
        var skuPrices = itemSubtotals.Select(s => (s.SkuId, group.SkuInfos[s.SkuId].UnitPrice)).ToList();
        await _pricingService.ValidatePricesAsync(skuPrices, group.SkuInfos, ct);
```

**3.4** 同步更新 `OrderAppService.PreviewAsync` 调用（第 204-206 行）：

```csharp
// 文件：src/Services/Order/Leno.Order.Application/Services/OrderAppService.cs
// 修改第 204-206 行为：

        // 价格防篡改校验（使用预查的 skuInfos）
        var skuPrices = details.Select(d => (d.SkuId, d.UnitPrice)).ToList();
        var previewSkuInfos = details.ToDictionary(d => d.SkuId, d => skuInfos[d.SkuId]);
        await _pricingService.ValidatePricesAsync(skuPrices, previewSkuInfos, ct);
```

#### 步骤 4：验证通过

```bash
dotnet test src/Services/Order/Leno.Order.Infrastructure.Tests/Leno.Order.Infrastructure.Tests.csproj \
  --filter "FullyQualifiedName~ValidatePricesAsync_With_PreQueried|FullyQualifiedName~ValidatePricesAsync_PriceChanged"
```

预期：测试通过。全量回归确保 Saga 与 Preview 调用点更新正确。

#### 步骤 5：提交

```bash
git add src/Services/Order/Leno.Order.Domain/Services/IOrderPricingDomainService.cs \
        src/Services/Order/Leno.Order.Infrastructure/Services/OrderPricingDomainService.cs \
        src/Services/Order/Leno.Order.Application/Services/OrderSagaOrchestrator.cs \
        src/Services/Order/Leno.Order.Application/Services/OrderAppService.cs \
        src/Services/Order/Leno.Order.Infrastructure.Tests/OrderPricingDomainServiceTests.cs
git commit -m "fix(order): ValidatePricesAsync 接收预查 SkuInfos 消除 N+1 远程调用（2.9）

ValidatePricesAsync 新增 IReadOnlyDictionary<Guid, SkuInfo> 参数，使用应用层已批量
查询的 SkuInfos 字典，不再内部循环调用 GetSkuInfoAsync。N 个 SKU 的远程调用从
2N 降为 N，下单延迟与商品域负载减半。"
```

---

### P0-T10：物流轨迹查询全量加载 100 个物流公司匹配 Code（审计 #10 / 2.10）

**审计位置**：[file:///workspace/docs/superpowers/specs/2026-07-21-code-audit/04-order.md#L122-L128]
**代码位置**：
- [file:///workspace/src/Services/Order/Leno.Order.Application/Services/OrderAppService.cs#L430-L433]（ListAsync(1, 100) + FirstOrDefault）
- [file:///workspace/src/Services/Order/Leno.Order.Application/Queries/LogisticsTraceQueryHandler.cs#L70-L73]（同上）
- [file:///workspace/src/Services/Order/Leno.Order.Domain/Repositories/ILogisticsCompanyRepository.cs#L10-L17]（无 GetByCodeAsync 接口）

**根因**：每次查询物流轨迹都调用 `ListAsync(1, 100)` 加载前 100 个物流公司，然后用 `FirstOrDefault` 匹配 Code。若物流公司超过 100 家，匹配可能失败。

---

#### 步骤 1：测试

在 `Leno.Order.Application.Tests/LogisticsTraceQueryHandlerTests.cs` 中追加测试。

```csharp
// 文件：src/Services/Order/Leno.Order.Application.Tests/LogisticsTraceQueryHandlerTests.cs
// 在 LogisticsTraceQueryHandlerTests 类内追加以下测试方法

[Fact]
public async Task HandleAsync_Should_Use_GetByCodeAsync_Not_ListAsync()
{
    // Arrange
    var orderRepoMock = new Mock<IOrderRepository>();
    var logisticsCompanyRepoMock = new Mock<ILogisticsCompanyRepository>();
    var logisticsTrackingMock = new Mock<ILogisticsTrackingService>();

    var order = CreateShippedOrder(logisticsCompanyCode: "SF");
    orderRepoMock.Setup(r => r.GetByIdAsync(order.Id, It.IsAny<CancellationToken>()))
        .ReturnsAsync(order);
    logisticsCompanyRepoMock.Setup(r => r.GetByCodeAsync("SF", It.IsAny<CancellationToken>()))
        .ReturnsAsync(CreateLogisticsCompany("SF", enabled: true, supportTracking: true));
    logisticsTrackingMock.Setup(t => t.QueryTraceAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
        .ReturnsAsync(new LogisticsTraceResult { LogisticsNo = "SF123", CompanyCode = "SF", Nodes = new List<LogisticsTraceNode>() });

    var sut = new LogisticsTraceQueryHandler(
        orderRepoMock.Object, logisticsCompanyRepoMock.Object, logisticsTrackingMock.Object);

    // Act
    var result = await sut.HandleAsync(new LogisticsTraceQuery(order.Id), CancellationToken.None);

    // Assert：应调用 GetByCodeAsync 而非 ListAsync
    logisticsCompanyRepoMock.Verify(
        r => r.GetByCodeAsync("SF", It.IsAny<CancellationToken>()),
        Times.Once);
    logisticsCompanyRepoMock.Verify(
        r => r.ListAsync(It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()),
        Times.Never);
    result.Should().NotBeNull();
}
```

#### 步骤 2：验证失败

```bash
dotnet test src/Services/Order/Leno.Order.Application.Tests/Leno.Order.Application.Tests.csproj \
  --filter "FullyQualifiedName~HandleAsync_Should_Use_GetByCodeAsync"
```

预期：测试失败。`ILogisticsCompanyRepository` 当前无 `GetByCodeAsync` 方法。

#### 步骤 3：实现

**3.1** 在 `ILogisticsCompanyRepository` 增加 `GetByCodeAsync` 方法：

```csharp
// 文件：src/Services/Order/Leno.Order.Domain/Repositories/ILogisticsCompanyRepository.cs
// 在接口中追加 GetByCodeAsync 方法声明

    /// <summary>
    /// 按物流公司编码查询物流公司，不存在返回 null。
    /// </summary>
    /// <param name="code">物流公司编码。</param>
    /// <param name="ct">取消令牌。</param>
    Task<LogisticsCompany?> GetByCodeAsync(string code, CancellationToken ct = default);
```

**3.2** 在 `EfCoreLogisticsCompanyRepository` 实现 `GetByCodeAsync`（利用 `ix_logistics_companies_code` 唯一索引）：

```csharp
// 文件：src/Services/Order/Leno.Order.Infrastructure/Repositories/EfCoreLogisticsCompanyRepository.cs
// 在 EfCoreLogisticsCompanyRepository 类内追加以下方法

    /// <inheritdoc />
    public async Task<LogisticsCompany?> GetByCodeAsync(string code, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(code))
        {
            return null;
        }
        return await _context.LogisticsCompanies
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.Code == code, ct);
    }
```

**3.3** 修改 `LogisticsTraceQueryHandler.HandleAsync`，使用 `GetByCodeAsync`：

```csharp
// 文件：src/Services/Order/Leno.Order.Application/Queries/LogisticsTraceQueryHandler.cs
// 替换第 69-73 行为：

        // 校验物流公司是否支持轨迹查询（按 Code 精确查询，利用唯一索引）
        var company = await _logisticsCompanyRepository.GetByCodeAsync(order.LogisticsCompanyCode, ct);
```

**3.4** 同步修改 `OrderAppService.GetLogisticsTraceAsync`（第 430-433 行）：

```csharp
// 文件：src/Services/Order/Leno.Order.Application/Services/OrderAppService.cs
// 替换第 430-433 行为：

        var company = await _logisticsCompanyRepository.GetByCodeAsync(order.LogisticsCompanyCode, ct);
        var companyEnabled = company is not null &&
            company.Status == LogisticsCompanyStatus.Enabled &&
            company.SupportTracking;
```

#### 步骤 4：验证通过

```bash
dotnet test src/Services/Order/Leno.Order.Application.Tests/Leno.Order.Application.Tests.csproj \
  --filter "FullyQualifiedName~HandleAsync_Should_Use_GetByCodeAsync"
```

预期：测试通过。

#### 步骤 5：提交

```bash
git add src/Services/Order/Leno.Order.Domain/Repositories/ILogisticsCompanyRepository.cs \
        src/Services/Order/Leno.Order.Infrastructure/Repositories/EfCoreLogisticsCompanyRepository.cs \
        src/Services/Order/Leno.Order.Application/Queries/LogisticsTraceQueryHandler.cs \
        src/Services/Order/Leno.Order.Application/Services/OrderAppService.cs \
        src/Services/Order/Leno.Order.Application.Tests/LogisticsTraceQueryHandlerTests.cs
git commit -m "fix(order): 物流轨迹查询使用 GetByCodeAsync 精确查询（2.10）

新增 ILogisticsCompanyRepository.GetByCodeAsync，利用 ix_logistics_companies_code
唯一索引精确查询，替代 ListAsync(1,100)+FirstOrDefault 全表扫描。消除物流公司
超过 100 家时轨迹查询失败的性能与正确性问题。"
```

---

### P0-T11：StockReconciliationService 使用 KEYS 命令全量扫描 Redis（审计 #11 / 2.11）

**审计位置**：[file:///workspace/docs/superpowers/specs/2026-07-21-code-audit/04-order.md#L130-L135]
**代码位置**：[file:///workspace/src/Services/Order/Leno.Order.Infrastructure/Services/StockReconciliationService.cs#L67-L96]

**根因**：`server.Keys(pattern: $"{StockKeyPrefix}*").ToList()` 使用 Redis KEYS 命令同步阻塞扫描全库，生产环境 SKU 数大时阻塞 Redis 主线程数秒。此外与 `InventoryReconciliationBackgroundService` 功能重叠。

---

#### 步骤 1：测试

在 `Leno.Order.Infrastructure.Tests/StockReconciliationServiceTests.cs` 中追加测试，验证使用 SCAN 分页而非 KEYS。

```csharp
// 文件：src/Services/Order/Leno.Order.Infrastructure.Tests/StockReconciliationServiceTests.cs
// 在 StockReconciliationServiceTests 类内追加以下测试方法

[Fact]
public async Task ReconcileAsync_Should_Use_Scan_Not_Keys()
{
    // Arrange
    var redisMock = new Mock<IConnectionMultiplexer>();
    var serverMock = new Mock<IServer>();
    var dbMock = new Mock<IDatabase>();

    redisMock.Setup(r => r.GetEndPoints(It.IsAny<bool>())).Returns(new EndPoint[] { new DnsEndPoint("localhost", 6379) });
    redisMock.Setup(r => r.GetServer(It.IsAny<EndPoint>(), It.IsAny<object>())).Returns(serverMock.Object);
    redisMock.Setup(r => r.GetDatabase(It.IsAny<int>(), It.IsAny<object>())).Returns(dbMock.Object);

    // 模拟 SCAN 返回分页结果（IAsyncEnumerable 风格）
    var scanResults = new List<RedisKey> { (RedisKey)"inventory:stock:guid1", (RedisKey)"inventory:stock:guid2" };
    serverMock.Setup(s => s.KeysAsync(It.IsAny<int>(), It.IsAny<RedisValue>(), It.IsAny<int>(), It.IsAny<long>(), It.IsAny<int>(), It.IsAny<CommandFlags>()))
        .Returns(GetAsyncEnumerable(scanResults));

    dbMock.Setup(d => d.StringGetAsync(It.IsAny<RedisKey>(), It.IsAny<CommandFlags>()))
        .ReturnsAsync(100);

    var loggerMock = new Mock<ILogger<StockReconciliationService>>();
    var scopeFactoryMock = new Mock<IServiceScopeFactory>();
    var sut = new StockReconciliationService(scopeFactoryMock.Object, redisMock.Object, loggerMock.Object);

    // Act：调用内部 ReconcileAsync（通过反射或公开测试入口）
    await InvokeReconcileAsync(sut, CancellationToken.None);

    // Assert：应调用 KeysAsync（SCAN）而非 Keys（KEYS）
    serverMock.Verify(
        s => s.KeysAsync(It.IsAny<int>(), It.IsAny<RedisValue>(), It.IsAny<int>(), It.IsAny<long>(), It.IsAny<int>(), It.IsAny<CommandFlags>()),
        Times.AtLeastOnce);
    serverMock.Verify(
        s => s.Keys(It.IsAny<int>(), It.IsAny<RedisValue>(), It.IsAny<int>(), It.IsAny<long>(), It.IsAny<int>(), It.IsAny<CommandFlags>()),
        Times.Never);
}

private static async IAsyncEnumerable<RedisKey> GetAsyncEnumerable(IEnumerable<RedisKey> keys)
{
    foreach (var key in keys)
    {
        await Task.Yield();
        yield return key;
    }
}

private static async Task InvokeReconcileAsync(StockReconciliationService service, CancellationToken ct)
{
    var method = typeof(StockReconciliationService).GetMethod("ReconcileAsync", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
    await (Task)method!.Invoke(service, new object[] { ct })!;
}
```

#### 步骤 2：验证失败

```bash
dotnet test src/Services/Order/Leno.Order.Infrastructure.Tests/Leno.Order.Infrastructure.Tests.csproj \
  --filter "FullyQualifiedName~ReconcileAsync_Should_Use_Scan"
```

预期：测试失败。当前使用同步 `server.Keys(...)` 而非异步 `KeysAsync`。

#### 步骤 3：实现

修改 `StockReconciliationService.ReconcileAsync`，使用 `KeysAsync`（SCAN）异步分页扫描：

```csharp
// 文件：src/Services/Order/Leno.Order.Infrastructure/Services/StockReconciliationService.cs
// 替换第 62-134 行的 ReconcileAsync 方法为：

    private async Task ReconcileAsync(CancellationToken ct)
    {
        var server = _redis.GetServer(_redis.GetEndPoints().First());
        var db = _redis.GetDatabase();

        // 使用 SCAN 异步分页扫描，避免 KEYS 阻塞 Redis 主线程
        var stockKeys = new List<RedisKey>();
        await foreach (var key in server.KeysAsync(pattern: $"{StockKeyPrefix}*", pageSize: 200).WithCancellation(ct))
        {
            stockKeys.Add(key);
        }
        _logger.LogInformation("库存对账开始，扫描到 {Count} 个库存键", stockKeys.Count);

        var mismatchCount = 0;

        foreach (var key in stockKeys)
        {
            ct.ThrowIfCancellationRequested();

            var skuIdStr = key.ToString().Substring(StockKeyPrefix.Length);
            if (!Guid.TryParse(skuIdStr, out var skuId))
            {
                _logger.LogWarning("无法解析 SKU ID Key={Key}", key);
                continue;
            }

            var available = (int?)await db.StringGetAsync(key) ?? 0;

            // 扫描该 SKU 的全部预占键（同样使用 SCAN）
            var reservedPattern = $"{ReservedKeyPrefix}{skuId}:*";
            var totalReserved = 0;
            var reservedKeyCount = 0;
            await foreach (var rk in server.KeysAsync(pattern: reservedPattern, pageSize: 200).WithCancellation(ct))
            {
                var reserved = (int?)await db.StringGetAsync(rk) ?? 0;
                totalReserved += reserved;
                reservedKeyCount++;
            }

            // 可用库存不应为负
            if (available < 0)
            {
                mismatchCount++;
                _logger.LogWarning("库存对账异常：可用库存为负 SkuId={SkuId} Available={Available} Reserved={Reserved}",
                    skuId, available, totalReserved);
                _logger.LogWarning("库存对账：SkuId={SkuId} 可用库存为负需人工介入", skuId);
            }
        }

        if (mismatchCount > 0)
        {
            _logger.LogWarning("库存对账完成，发现 {MismatchCount} 个异常", mismatchCount);
        }
        else
        {
            _logger.LogInformation("库存对账完成，共 {Count} 个 SKU，无异常", stockKeys.Count);
        }
    }
```

#### 步骤 4：验证通过

```bash
dotnet test src/Services/Order/Leno.Order.Infrastructure.Tests/Leno.Order.Infrastructure.Tests.csproj \
  --filter "FullyQualifiedName~ReconcileAsync_Should_Use_Scan"
```

预期：测试通过。

#### 步骤 5：提交

```bash
git add src/Services/Order/Leno.Order.Infrastructure/Services/StockReconciliationService.cs \
        src/Services/Order/Leno.Order.Infrastructure.Tests/StockReconciliationServiceTests.cs
git commit -m "fix(order): 库存对账使用 SCAN 异步分页替代 KEYS 阻塞（2.11）

server.Keys 替换为 server.KeysAsync（SCAN 命令），分页 pageSize=200 异步遍历，
避免 KEYS 同步阻塞 Redis 主线程导致全服务超时。预占键扫描同样改用 SCAN。"
```

---

### P0-T12：ExecuteGroupAsync 调度超时延迟消息与 SaveEntitiesAsync 不同事务（审计 #12 / 2.12）

**审计位置**：[file:///workspace/docs/superpowers/specs/2026-07-21-code-audit/04-order.md#L137-L143]
**代码位置**：
- [file:///workspace/src/Services/Order/Leno.Order.Application/Services/OrderSagaOrchestrator.cs#L58-L198]（ScheduleSend 在 SaveEntitiesAsync 之前）
- [file:///workspace/src/Services/Order/Leno.Order.Application/Services/OrderAppService.cs#L274-L293]（ConfirmReceiptAsync 同样未与事务绑定）

**根因**：Saga 在 `ExecuteGroupAsync` 内 `_bus.CreateMessageScheduler().ScheduleSend(...)` 调度 30 分钟超时消息，发生在 `SaveEntitiesAsync` 之前。若 Saga 后续组失败导致整体回滚，已调度的超时消息仍会按时投递。

---

#### 步骤 1：测试

在 `Leno.Order.Application.Tests/OrderSagaOrchestratorTests.cs` 中追加测试。

```csharp
// 文件：src/Services/Order/Leno.Order.Application.Tests/OrderSagaOrchestratorTests.cs
// 在 OrderSagaOrchestratorTests 类内追加以下测试方法

[Fact]
public async Task ExecuteAsync_AllSuccess_Should_Schedule_Timeout_After_SaveEntitiesAsync()
{
    // Arrange
    var sut = CreateSut(out var orderRepoMock, out var uowMock, out var orderNoGenMock,
        out var stockServiceMock, out var pricingMock, out var freightMock,
        out var promotionMock, out var pointsMock, out var busMock, out var loggerMock);

    stockServiceMock.Setup(s => s.ReserveBatchAsync(It.IsAny<Guid>(), It.IsAny<Dictionary<Guid, int>>(), It.IsAny<CancellationToken>()))
        .ReturnsAsync(true);
    orderNoGenMock.Setup(g => g.GenerateAsync(It.IsAny<CancellationToken>()))
        .ReturnsAsync("ORD-001");

    var callOrder = new List<string>();
    uowMock.Setup(u => u.SaveEntitiesAsync(It.IsAny<CancellationToken>()))
        .Returns(() => { callOrder.Add("SaveEntitiesAsync"); return Task.CompletedTask; });
    busMock.Setup(b => b.CreateMessageScheduler())
        .Returns(() => { callOrder.Add("CreateScheduler"); return Mock.Of<IMessageScheduler>(); });

    var context = CreateSagaContextWithSingleGroup();

    // Act
    await sut.ExecuteAsync(context, CancellationToken.None);

    // Assert：SaveEntitiesAsync 应在 ScheduleSend 之前执行
    callOrder.IndexOf("SaveEntitiesAsync").Should().BeLessThan(callOrder.IndexOf("CreateScheduler"));
}

[Fact]
public async Task ExecuteAsync_SecondGroupFails_Should_Not_Schedule_Timeout()
{
    // Arrange
    var sut = CreateSut(out var orderRepoMock, out var uowMock, out var orderNoGenMock,
        out var stockServiceMock, out var pricingMock, out var freightMock,
        out var promotionMock, out var pointsMock, out var busMock, out var loggerMock);

    stockServiceMock.SetupSequence(s => s.ReserveBatchAsync(It.IsAny<Guid>(), It.IsAny<Dictionary<Guid, int>>(), It.IsAny<CancellationToken>()))
        .ReturnsAsync(true)   // 第一组成功
        .ReturnsAsync(false); // 第二组失败
    orderNoGenMock.Setup(g => g.GenerateAsync(It.IsAny<CancellationToken>()))
        .ReturnsAsync("ORD-001");

    var schedulerMock = new Mock<IMessageScheduler>();
    busMock.Setup(b => b.CreateMessageScheduler()).Returns(schedulerMock.Object);

    var context = CreateSagaContextWithTwoGroups();

    // Act
    var act = async () => await sut.ExecuteAsync(context, CancellationToken.None);

    // Assert：Saga 失败时不应调度任何超时消息
    await act.Should().ThrowAsync<OrderDomainException>();
    schedulerMock.Verify(
        s => s.ScheduleSend(It.IsAny<Uri>(), It.IsAny<DateTime>(), It.IsAny<object>(), It.IsAny<CancellationToken>()),
        Times.Never);
}
```

#### 步骤 2：验证失败

```bash
dotnet test src/Services/Order/Leno.Order.Application.Tests/Leno.Order.Application.Tests.csproj \
  --filter "FullyQualifiedName~ExecuteAsync_AllSuccess_Should_Schedule|FullyQualifiedName~ExecuteAsync_SecondGroupFails_Should_Not_Schedule"
```

预期：测试失败。当前 `ScheduleSend` 在 `ExecuteGroupAsync` 内（`SaveEntitiesAsync` 之前）执行。

#### 步骤 3：实现

修改 `OrderSagaOrchestrator`，将延迟消息调度从 `ExecuteGroupAsync` 移到 `ExecuteAsync` 全部成功后统一执行：

```csharp
// 文件：src/Services/Order/Leno.Order.Application/Services/OrderSagaOrchestrator.cs
// 修改 ExecuteAsync 方法（第 58-85 行）为：

    /// <inheritdoc />
    public async Task<OrderSagaResult> ExecuteAsync(OrderSagaContext context, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(context);

        var completed = new List<CompletedGroup>();
        foreach (var group in context.Groups)
        {
            try
            {
                completed.Add(await ExecuteGroupAsync(context.UserId, context.Address, group, ct));
            }
            catch (Exception)
            {
                // 任一组失败：补偿已成功组后向上抛原始异常（库存/积分/券/订单聚合回滚）
                await CompensateAsync(completed, CancellationToken.None);
                throw;
            }
        }

        // 全部组成功 → 统一提交工作单元（订单聚合 + 发件箱集成事件同事务持久化）
        await _unitOfWork.SaveEntitiesAsync(ct);

        // SaveEntitiesAsync 成功后统一调度超时延迟消息（保证订单已持久化）
        foreach (var g in completed)
        {
            var scheduler = _bus.CreateMessageScheduler();
            await scheduler.ScheduleSend(
                new Uri("queue:order-timeout"),
                g.Order.ExpireAt,
                new OrderTimeoutMessage(g.OrderId),
                ct);
        }

        return new OrderSagaResult
        {
            FirstOrder = completed[0].Order,
            Orders = completed.Select(c => c.Order).ToList()
        };
    }
```

并从 `ExecuteGroupAsync` 中移除延迟消息调度（删除第 182-188 行）：

```csharp
// 文件：src/Services/Order/Leno.Order.Application/Services/OrderSagaOrchestrator.cs
// 删除 ExecuteGroupAsync 中的以下代码块（原第 182-188 行）：

        // 调度支付超时取消延迟消息（30 分钟）
        var scheduler = _bus.CreateMessageScheduler();
        await scheduler.ScheduleSend(
            new Uri("queue:order-timeout"),
            order.ExpireAt,
            new OrderTimeoutMessage(orderId),
            ct);
```

#### 步骤 4：验证通过

```bash
dotnet test src/Services/Order/Leno.Order.Application.Tests/Leno.Order.Application.Tests.csproj \
  --filter "FullyQualifiedName~ExecuteAsync_AllSuccess_Should_Schedule|FullyQualifiedName~ExecuteAsync_SecondGroupFails_Should_Not_Schedule"
```

预期：测试通过。

#### 步骤 5：提交

```bash
git add src/Services/Order/Leno.Order.Application/Services/OrderSagaOrchestrator.cs \
        src/Services/Order/Leno.Order.Application.Tests/OrderSagaOrchestratorTests.cs
git commit -m "fix(order): 延迟消息调度移至 SaveEntitiesAsync 之后（2.12）

ScheduleSend 从 ExecuteGroupAsync 移到 ExecuteAsync 全部组成功且
SaveEntitiesAsync 之后统一执行，消除 Saga 失败后产生幽灵延迟消息的问题。"
```

---

### P0-T13：Order.Cancel 与库存/积分/优惠券释放非原子，先释放后持久化（审计 #13 / 2.13）

**审计位置**：[file:///workspace/docs/superpowers/specs/2026-07-21-code-audit/04-order.md#L145-L152]
**代码位置**：
- [file:///workspace/src/Services/Order/Leno.Order.Application/Services/OrderAppService.cs#L296-L313]（CancelAsync）
- [file:///workspace/src/Services/Order/Leno.Order.Application/Services/OrderAppService.cs#L316-L365]（ForceCancelAsync）
- [file:///workspace/src/Services/Order/Leno.Order.Infrastructure/Consumers/OrderTimeoutDelayMessageConsumer.cs#L74-L89]

**根因**：`CancelAsync`/`ForceCancelAsync`/`OrderTimeoutDelayMessageConsumer` 都遵循"调用 `order.Cancel` → 释放库存/积分/优惠券 → `UpdateAsync` → `SaveEntitiesAsync`"模式。若 `SaveEntitiesAsync` 失败，订单状态在 DB 中未变更（仍为 PendingPayment/Paid），但库存/积分/优惠券已被释放，且 `OrderCancelledDomainEvent` 未通过 Outbox 持久化。

**修复方案**：先 `SaveEntitiesAsync`（含 Outbox `OrderCancelledEvent`），再由独立消费者消费 `OrderCancelledEvent` 释放库存/积分/优惠券，使其可独立重试且通过事件幂等键去重。

---

#### 步骤 1：测试

在 `Leno.Order.Application.Tests/OrderAppServiceTests.cs` 中追加测试。

```csharp
// 文件：src/Services/Order/Leno.Order.Application.Tests/OrderAppServiceTests.cs
// 在 OrderAppServiceTests 类内追加以下测试方法

[Fact]
public async Task CancelAsync_Should_SaveEntities_First_Then_Release_Resources()
{
    // Arrange
    var sut = CreateSut(out var orderRepoMock, out var uowMock, out var stockServiceMock,
        out var pointsMock, out var promotionMock, out var logisticsMock, out var logisticsCompanyRepoMock,
        out var eventBusMock, out var busMock, out var sagaMock);

    var order = CreatePendingOrder();
    orderRepoMock.Setup(r => r.GetByIdAsync(order.Id, It.IsAny<CancellationToken>()))
        .ReturnsAsync(order);

    var callOrder = new List<string>();
    uowMock.Setup(u => u.SaveEntitiesAsync(It.IsAny<CancellationToken>()))
        .Returns(() => { callOrder.Add("SaveEntitiesAsync"); return Task.CompletedTask; });
    stockServiceMock.Setup(s => s.ReleaseBatchAsync(It.IsAny<Guid>(), It.IsAny<Dictionary<Guid, int>>(), It.IsAny<CancellationToken>()))
        .Returns(() => { callOrder.Add("ReleaseStock"); return Task.CompletedTask; });

    var dto = new CancelOrderDto { Reason = "test" };

    // Act
    await sut.CancelAsync(order.Id, order.UserId, dto, CancellationToken.None);

    // Assert：SaveEntitiesAsync 应在 ReleaseBatchAsync 之前执行
    callOrder.IndexOf("SaveEntitiesAsync").Should().BeLessThan(callOrder.IndexOf("ReleaseStock"));
}

[Fact]
public async Task CancelAsync_Should_Publish_OrderCancelledEvent_Via_Outbox()
{
    // Arrange
    var sut = CreateSut(out var orderRepoMock, out var uowMock, out var stockServiceMock,
        out var pointsMock, out var promotionMock, out var logisticsMock, out var logisticsCompanyRepoMock,
        out var eventBusMock, out var busMock, out var sagaMock);

    var order = CreatePendingOrder();
    orderRepoMock.Setup(r => r.GetByIdAsync(order.Id, It.IsAny<CancellationToken>()))
        .ReturnsAsync(order);

    var dto = new CancelOrderDto { Reason = "test" };

    // Act
    await sut.CancelAsync(order.Id, order.UserId, dto, CancellationToken.None);

    // Assert：订单聚合应包含 OrderCancelledDomainEvent（经 Outbox 持久化）
    order.Status.Should().Be(OrderStatus.Cancelled);
    order.DomainEvents.Should().Contain(e => e is OrderCancelledDomainEvent);
    // 库存/积分/优惠券释放应在 SaveEntitiesAsync 之后（由独立消费者执行）
    stockServiceMock.Verify(
        s => s.ReleaseBatchAsync(It.IsAny<Guid>(), It.IsAny<Dictionary<Guid, int>>(), It.IsAny<CancellationToken>()),
        Times.Never);
}
```

#### 步骤 2：验证失败

```bash
dotnet test src/Services/Order/Leno.Order.Application.Tests/Leno.Order.Application.Tests.csproj \
  --filter "FullyQualifiedName~CancelAsync_Should_SaveEntities_First|FullyQualifiedName~CancelAsync_Should_Publish_OrderCancelledEvent"
```

预期：测试失败。当前 `CancelAsync` 在 `SaveEntitiesAsync` 之前调用 `ReleaseBatchAsync`。

#### 步骤 3：实现

**3.1** 修改 `OrderAppService.CancelAsync`，先 `SaveEntitiesAsync` 再释放资源：

```csharp
// 文件：src/Services/Order/Leno.Order.Application/Services/OrderAppService.cs
// 替换第 296-313 行的 CancelAsync 方法为：

    /// <inheritdoc />
    public async Task CancelAsync(Guid orderId, Guid userId, CancelOrderDto dto, CancellationToken ct = default)
    {
        var order = await RequireOrderAsync(orderId, ct);
        if (order.UserId != userId)
        {
            throw new OrderDomainException("无权操作此订单", "ORDER_FORBIDDEN");
        }
        order.Cancel(dto.Reason, "Buyer");

        // 先持久化订单状态变更与 OrderCancelledDomainEvent（经 Outbox 同事务）
        await _orderRepository.UpdateAsync(order, ct);
        await _unitOfWork.SaveEntitiesAsync(ct);

        // 持久化成功后再释放预占库存、冻结积分与优惠券（可独立重试）
        var skuQuantities = BuildSkuQuantities(order);
        await _stockService.ReleaseBatchAsync(orderId, skuQuantities, ct);
        await _pointsAntiCorruption.ReleaseAsync(orderId, ct);
        await _promotionAntiCorruption.ReleaseCouponsAsync(orderId, ct);
    }
```

**3.2** 同样修改 `ForceCancelAsync` 的 PendingPayment 分支（第 321-337 行）：

```csharp
// 文件：src/Services/Order/Leno.Order.Application/Services/OrderAppService.cs
// 替换 ForceCancelAsync 中 PendingPayment 分支（第 321-337 行）为：

        // 待支付订单：先持久化取消状态（含 Outbox OrderCancelledEvent），再释放资源
        order.Cancel(dto.Reason, "Admin");

        await _orderRepository.UpdateAsync(order, ct);
        await _unitOfWork.SaveEntitiesAsync(ct);

        // 持久化成功后再释放预占库存、冻结积分与优惠券
        var skuQuantities = BuildSkuQuantities(order);
        await _stockService.ReleaseBatchAsync(orderId, skuQuantities, ct);
        await _pointsAntiCorruption.ReleaseAsync(orderId, ct);
        await _promotionAntiCorruption.ReleaseCouponsAsync(orderId, ct);

        // 发布操作日志事件
        await PublishAdminOperationLogAsync(operatorId, "ForceCancel", "Order",
            $"运营强制取消待支付订单 {order.OrderNo}，原因：{dto.Reason}", orderId, ct);

        return;
```

**3.3** 同样修改 `OrderTimeoutDelayMessageConsumer.Consume`（已在 P0-T5 修改的基础上调整顺序）：

```csharp
// 文件：src/Services/Order/Leno.Order.Infrastructure/Consumers/OrderTimeoutDelayMessageConsumer.cs
// 调整 Consume 方法中的执行顺序（在 P0-T5 已有的幂等检查基础上）

        order.Cancel("支付超时自动取消", "System");

        // 先持久化订单状态变更与 OrderCancelledDomainEvent（经 Outbox 同事务）
        await _orderRepository.UpdateAsync(order, context.CancellationToken);
        await _unitOfWork.SaveEntitiesAsync(context.CancellationToken);

        // 持久化成功后再释放预占库存、冻结积分与优惠券
        var skuQuantities = order.Items
            .GroupBy(i => i.SkuId)
            .ToDictionary(g => g.Key, g => g.Sum(i => i.Quantity));
        await _stockService.ReleaseBatchAsync(order.Id, skuQuantities, context.CancellationToken);
        await _pointsAntiCorruption.ReleaseAsync(order.Id, context.CancellationToken);
        await _promotionAntiCorruption.ReleaseCouponsAsync(order.Id, context.CancellationToken);

        // 成功后标记幂等键
        await _idempotencyStore.MarkAsProcessedAsync(idempotencyKey, IdempotencyTtl, context.CancellationToken);

        _logger.LogInformation("订单 {OrderId} 因支付超时已自动取消", msg.OrderId);
```

#### 步骤 4：验证通过

```bash
dotnet test src/Services/Order/Leno.Order.Application.Tests/Leno.Order.Application.Tests.csproj \
  --filter "FullyQualifiedName~CancelAsync_Should_SaveEntities_First|FullyQualifiedName~CancelAsync_Should_Publish_OrderCancelledEvent"
```

预期：测试通过。

#### 步骤 5：提交

```bash
git add src/Services/Order/Leno.Order.Application/Services/OrderAppService.cs \
        src/Services/Order/Leno.Order.Infrastructure/Consumers/OrderTimeoutDelayMessageConsumer.cs \
        src/Services/Order/Leno.Order.Application.Tests/OrderAppServiceTests.cs
git commit -m "fix(order): Cancel 先持久化再释放资源保证原子性（2.13）

CancelAsync/ForceCancelAsync/OrderTimeoutDelayMessageConsumer 调整执行顺序：
先 SaveEntitiesAsync（含 Outbox OrderCancelledEvent）再释放库存/积分/优惠券，
消除 SaveEntitiesAsync 失败后库存/积分/优惠券已释放但订单状态未变更的不一致。"
```

---

## P1 修复清单（任务清单格式：审计位置/代码位置/根因/修复步骤/影响范围/验证方法）

### P1-T14：FreightTemplate.CalculateFreight 当 quantity=0 返回 FirstPrice（审计 #14 / 3.1）

- **审计位置**：[file:///workspace/docs/superpowers/specs/2026-07-21-code-audit/04-order.md#L156-L161]
- **代码位置**：[file:///workspace/src/Services/Order/Leno.Order.Domain/Aggregates/FreightTemplate.cs#L117-L138]
- **根因**：`if (quantity <= rule.FirstUnit) return rule.FirstPrice;` 当 quantity=0 时返回 FirstPrice 而非 0。
- **修复步骤**：
  1. 在 `CalculateFreight` 方法起始处增加 `if (quantity <= 0) return 0;` 校验
  2. 同时校验 `orderAmount >= 0`，负值抛 `OrderDomainException`
  3. 补充单元测试：验证 quantity=0 时返回 0 运费
- **影响范围**：运费计算边界场景
- **验证方法**：单元测试验证 quantity=0 返回 0

### P1-T15：OrderPricingDomainService.CalculateAndAllocateAsync 未校验 totalDiscount ≤ sumSubtotals（审计 #15 / 3.2）

- **审计位置**：[file:///workspace/docs/superpowers/specs/2026-07-21-code-audit/04-order.md#L163-L168]
- **代码位置**：[file:///workspace/src/Services/Order/Leno.Order.Infrastructure/Services/OrderPricingDomainService.cs#L36-L79]
- **根因**：当 `totalDiscount > sumSubtotals` 时，最后一项 `totalDiscount - allocated` 可能大于其 Subtotal，导致 `Order.ApplyDiscount` 抛 `ORDER_ITEM_DISCOUNT_INVALID`，应在领域服务层提前校验。
- **修复步骤**：
  1. 在 `CalculateAndAllocateAsync` 方法起始处（计算 `sumSubtotals` 后）增加校验：`if (totalDiscount > sumSubtotals) throw new OrderDomainException("优惠金额超过商品总额", "DISCOUNT_EXCEED_ITEMS");`
  2. 补充单元测试验证超额优惠抛异常
- **影响范围**：下单优惠计算
- **验证方法**：单元测试验证 totalDiscount > sumSubtotals 时抛 `DISCOUNT_EXCEED_ITEMS`

### P1-T16：Order.Ship 未校验物流公司编码存在性（审计 #16 / 3.3）

- **审计位置**：[file:///workspace/docs/superpowers/specs/2026-07-21-code-audit/04-order.md#L170-L176]
- **代码位置**：
  - [file:///workspace/src/Services/Order/Leno.Order.Domain/Aggregates/Order.cs#L356-L380]（仅校验非空）
  - [file:///workspace/src/Services/Order/Leno.Order.Application/Services/OrderAppService.cs#L266-L272]（应用层未查询物流公司表）
- **根因**：`Order.Ship` 仅校验 `logisticsCompanyCode` 非空字符串，未校验该 Code 是否在 `LogisticsCompany` 表中存在且 `Enabled`。
- **修复步骤**：
  1. 在 `ILogisticsCompanyRepository` 新增 `GetByCodeAsync(string code, CancellationToken ct)` 方法（按 Code 唯一查询）
  2. `EfCoreLogisticsCompanyRepository` 实现该方法：`_context.LogisticsCompanies.AsNoTracking().FirstOrDefaultAsync(x => x.Code == code, ct)`
  3. `OrderAppService.ShipAsync` 在 `order.Ship` 之前调用 `_logisticsCompanyRepository.GetByCodeAsync(dto.LogisticsCompanyCode, ct)`，若返回 null 或 `Status != LogisticsCompanyStatus.Enabled` 抛 `OrderDomainException("物流公司编码不存在或已停用", "LOGISTICS_COMPANY_NOT_FOUND")`
  4. 补充集成测试：发货时传入不存在的 Code 应抛异常
- **影响范围**：发货流程；新增 1 个仓储方法
- **验证方法**：集成测试覆盖合法/非法/已停用物流公司编码三种场景

### P1-T17：RefundCompletedEventConsumer 循环内调用 Redis 释放库存（审计 #17 / 3.4）

- **审计位置**：[file:///workspace/docs/superpowers/specs/2026-07-21-code-audit/04-order.md#L178-L183]
- **代码位置**：[file:///workspace/src/Services/Order/Leno.Order.Infrastructure/Consumers/RefundCompletedEventConsumer.cs#L31-L51]
- **根因**：`foreach (var item in order.Items) { await _inventoryRepository.ReleaseAsync(...); }` 对每个 OrderItem 一次 Redis 调用，多 SKU 订单产生 N 次网络往返。
- **修复步骤**：
  1. 在 `IInventoryRepository` 新增 `ReleaseBatchAsync(IReadOnlyCollection<(Guid SkuId, int Quantity)> items, string orderId, CancellationToken ct)` 接口（P0-T2 修复时已新增该接口用于 ForceCancel）
  2. 修改 `RefundCompletedEventConsumer` 将 `foreach` 循环替换为：聚合 items 后一次性调用 `await _inventoryRepository.ReleaseBatchAsync(items, order.Id.ToString(), ct)`
  3. `RedisInventoryRepository.ReleaseBatchAsync` 使用 Lua 脚本批量执行多个 `DEL` 与 `HINCRBY`（一次网络往返）
  4. 补充单元测试：3 SKU 退款只触发 1 次 Redis 调用
- **影响范围**：退款库存释放；与 P0-T2 共享 `ReleaseBatchAsync` 接口
- **验证方法**：单元测试断言 Redis 调用次数 = 1；基准测试验证多 SKU 退款延迟下降

### P1-T18：OrderAppService.PreviewAsync 重复实现金额计算业务规则（审计 #18 / 3.5）

- **审计位置**：[file:///workspace/docs/superpowers/specs/2026-07-21-code-audit/04-order.md#L185-L191]
- **代码位置**：
  - [file:///workspace/src/Services/Order/Leno.Order.Application/Services/OrderAppService.cs#L169-L246]（应用层重复实现 `TotalAmount = ItemsAmount - DiscountAmount - PointsOffsetAmount + FreightAmount` 公式与积分抵现上限裁剪）
  - [file:///workspace/src/Services/Order/Leno.Order.Domain/Aggregates/Order.cs#L524-L530]（`RecalculateTotal` 与 `ApplyPointsOffset` 已是真相源）
- **根因**：`PreviewAsync` 在应用层手写金额公式与积分上限裁剪逻辑，未复用领域聚合方法，违反 DRY 与"应用层不含业务规则"。
- **修复步骤**：
  1. 在 `Leno.Order.Domain/Services/` 新增 `IOrderPricingPreviewService` 领域服务接口，方法 `Task<OrderPreviewResult> PreviewAsync(IReadOnlyList<PreviewItem> items, decimal totalDiscount, decimal pointsOffset, AddressSnapshot? address, CancellationToken ct)`
  2. 在 Infrastructure 实现该服务：内部构造一个临时 `OrderAggregate` 实例（不持久化），调用 `order.ApplyDiscount(...)` 与 `order.ApplyPointsOffset(...)`，复用聚合不变量校验，最后返回 `TotalAmount / ItemsAmount / FreightAmount / PointsOffsetAmount` 等 DTO 字段
  3. `OrderAppService.PreviewAsync` 改为调用 `IOrderPricingPreviewService.PreviewAsync`，移除重复的金额公式与裁剪逻辑
  4. 删除 `OrderAppService` 中私有的积分上限裁剪辅助方法
  5. 补充单元测试：预览金额与实际下单金额一致；积分超额时预览抛 `POINTS_OFFSET_EXCEED_LIMIT`
- **影响范围**：下单预览流程；新增 1 个领域服务
- **验证方法**：单元测试断言预览金额与 `OrderAggregate.RecalculateTotal` 输出一致；删除 `OrderAppService` 中的重复公式后编译通过

### P1-T19：OrderAppService.CreateOrderAsync 积分按卖家分摊是业务规则（审计 #19 / 3.6）

- **审计位置**：[file:///workspace/docs/superpowers/specs/2026-07-21-code-audit/04-order.md#L193-L198]
- **代码位置**：[file:///workspace/src/Services/Order/Leno.Order.Application/Services/OrderAppService.cs#L95-L132]
- **根因**：积分抵现按卖家分组比例分摊、尾差归最后一组的业务规则放在应用服务中，未来增加"按 SKU 分摊"或"按优惠后金额分摊"需修改应用层。
- **修复步骤**：
  1. 在 `Leno.Order.Domain/Services/` 新增 `IPointsAllocationService` 领域服务接口，方法 `IReadOnlyList<(Guid SellerId, decimal AllocatedPoints)> AllocateBySellerRatio(IReadOnlyDictionary<Guid, decimal> sellerSubtotals, decimal totalPoints)`
  2. 在 Infrastructure 实现该服务：按各卖家小计占比分摊总积分，最后一组承担尾差（保证总和等于 totalPoints）
  3. `OrderAppService.CreateOrderAsync` 改为调用 `IPointsAllocationService.AllocateBySellerRatio` 替代内联分摊逻辑
  4. 补充单元测试：3 卖家金额 [100, 200, 300]、总积分 60 时分摊为 [10, 20, 30]；金额 [100, 0, 300]、总积分 60 时分摊为 [0, 0, 60]（尾差归最后一组）
- **影响范围**：下单积分分摊流程；新增 1 个领域服务
- **验证方法**：单元测试覆盖正常分摊、零金额卖家、单一卖家、积分无法整除等场景

### P1-T20：StockReservationCompensation 聚合 MarkFailed 不变量缺陷（审计 #20 / 3.7）

- **审计位置**：[file:///workspace/docs/superpowers/specs/2026-07-21-code-audit/04-order.md#L200-L206]
- **代码位置**：
  - [file:///workspace/src/Services/Order/Leno.Order.Domain/Aggregates/StockReservationCompensation.cs#L97-L123]（`MarkFailed` 中 `if (RetryCount >= MaxRetries) Status = MaxRetriesExceeded;` 使用 `>=`）
  - [file:///workspace/src/Services/Order/Leno.Order.Infrastructure/Services/StockReservationCompensationBackgroundService.cs#L105-L143]（后台任务并发拉取同一 Pending 记录无锁）
- **根因**：`MarkFailed` 中 `RetryCount++` 后用 `>=` 判断流转，且非原子操作；后台任务并发拉取同一记录可能导致 RetryCount 多次 +1，实际重试次数远超 MaxRetries。
- **修复步骤**：
  1. 在 `StockReservationCompensation.MarkFailed` 中改用 `Interlocked.Increment(ref _retryCount)` 原子化自增（需将 `_retryCount` 字段改为 `private int _retryCount`）
  2. 状态流转判断改为 `if (currentRetry >= MaxRetries)`，并在流转到 `MaxRetriesExceeded` 后调用 `AddDomainEvent(new CompensationMaxRetriesExceededDomainEvent(...))` 上报告警
  3. `StockReservationCompensationBackgroundService` 改用 EF Core 的 `SkipLocked` 锁定待处理记录：`FROM stock_reservation_compensations WITH (UPDLOCK, READPAST) WHERE status = 'Pending'`
  4. 在 `OrderDbContext` 配置 `IsConcurrencyToken` 与 `RowVersion` 字段防止并发覆盖
  5. 补充并发测试：模拟 5 个后台任务同时拉取同一条 Pending 记录，验证 RetryCount 最终值 = MaxRetries 且不会超过
- **影响范围**：补偿记录重试流程；新增 1 个领域事件
- **验证方法**：并发单元测试断言 RetryCount 不超过 MaxRetries；RowVersion 字段配置单元测试

### P1-T21：Order.Items 与 FreightTemplate.RegionRules 直接暴露可变 List（审计 #21 / 3.8）

- **审计位置**：[file:///workspace/docs/superpowers/specs/2026-07-21-code-audit/04-order.md#L208-L214]
- **代码位置**：
  - [file:///workspace/src/Services/Order/Leno.Order.Domain/Aggregates/Order.cs#L34]（`public List<OrderItem> Items { get; private set; }`）
  - [file:///workspace/src/Services/Order/Leno.Order.Domain/Aggregates/FreightTemplate.cs#L26]（`public List<FreightRegionRule> RegionRules { get; private set; }`）
- **根因**：暴露可变 List 引用，外部代码可绕过聚合根方法直接 `order.Items.Add(...)` 或 `order.Items.Clear()`，破坏聚合不变量。
- **修复步骤**：
  1. `Order` 类改为 `private readonly List<OrderItem> _items = new();` + `public IReadOnlyList<OrderItem> Items => _items;`
  2. `FreightTemplate` 类改为 `private readonly List<FreightRegionRule> _regionRules = new();` + `public IReadOnlyList<FreightRegionRule> RegionRules => _regionRules;`
  3. `OrderConfiguration` 与 `FreightTemplateConfiguration` 配置 backing field：`builder.HasMany(o => o.Items).WithOne().HasForeignKey(...)` 改为 `builder.Metadata.FindNavigation(nameof(Order.Items)).SetPropertyAccessMode(PropertyAccessMode.Field)`
  4. 全局搜索 `.Items.Add`、`.Items.Clear`、`.RegionRules.Add` 等调用，确保改为聚合根方法（如 `Order.AddItem`、`FreightTemplate.AddRegionRule`）
  5. 补充单元测试：外部尝试 `order.Items.Add(...)` 编译失败（IReadOnlyList 不支持 Add）
- **影响范围**：聚合根封装性；EF Core 配置
- **验证方法**：编译通过且所有现有测试通过；新增单元测试断言 `Items` 类型为 `IReadOnlyList<OrderItem>`

### P1-T22：FreightRegionRule record 暴露无参公共构造破坏不可变性（审计 #22 / 3.9）

- **审计位置**：[file:///workspace/docs/superpowers/specs/2026-07-21-code-audit/04-order.md#L216-L221]
- **代码位置**：[file:///workspace/src/Services/Order/Leno.Order.Domain/ValueObjects/FreightRegionRule.cs#L26-L27]
- **根因**：`public FreightRegionRule() { }` 是 record 的无参公共构造，允许外部 `new FreightRegionRule()` 创建 `FirstUnit=0`、`AdditionalUnit=0` 的非法对象，破坏值对象不可变性。
- **修复步骤**：
  1. 将 `FreightRegionRule.cs` 中无参构造改为 `private FreightRegionRule() { }`（EF Core 仍可反射使用）
  2. 将字段属性改为 `init`，强制只能通过 `FreightRegionRule.Create(...)` 工厂方法构造
  3. `FreightTemplateConfiguration` 中配置 `FreightRegionRule` 子实体时显式调用 `HasData` 或 `PropertyAccessMode.Field`，确保 EF Core 可读取私有构造
  4. 补充单元测试：`new FreightRegionRule()` 编译失败；`FreightRegionRule.Create(0, 0, 0)` 抛 `OrderDomainException`
- **影响范围**：值对象不可变性；EF Core 配置
- **验证方法**：编译通过且现有测试通过；新增单元测试断言无参构造不可见

### P1-T23：OrderSagaResult 暴露聚合根给应用层（审计 #23 / 3.10）

- **审计位置**：[file:///workspace/docs/superpowers/specs/2026-07-21-code-audit/04-order.md#L223-L229]
- **代码位置**：
  - [file:///workspace/src/Services/Order/Leno.Order.Application/Services/OrderSagaOrchestrator.cs#L320-L329]（`OrderSagaResult.FirstOrder`/`Orders` 是 `OrderAggregate` 聚合根实例）
  - [file:///workspace/src/Services/Order/Leno.Order.Application/Services/OrderAppService.cs#L141-L143]（`OrderAppService.CreateOrderAsync` 接收 Saga 返回的聚合根实例并调用 `ToDto`）
- **根因**：Saga 返回 `OrderAggregate` 聚合根实例，应用层直接持有聚合根违反"应用层不应直接持有聚合根"原则。
- **修复步骤**：
  1. 新增 `OrderCreatedResult` DTO，包含 `OrderId / OrderNumber / TotalAmount / SellerId / Items（IReadOnlyList<OrderItemDto>）` 等字段
  2. `OrderSagaResult.FirstOrder`/`Orders` 改为 `OrderCreatedResult`/`IReadOnlyList<OrderCreatedResult>` 类型，Saga 在返回前调用 `ToResult()` 转换
  3. `OrderAppService.CreateOrderAsync` 接收 `OrderSagaResult` 后直接使用 DTO，移除 `ToDto` 调用
  4. 补充单元测试：Saga 返回类型为 `OrderCreatedResult`，不暴露 `OrderAggregate`
- **影响范围**：Saga 返回类型；新增 1 个 DTO
- **验证方法**：单元测试断言 `OrderSagaResult.FirstOrder` 类型为 `OrderCreatedResult`

### P1-T24：OrderSagaOrchestrator 多卖家拆单顺序执行未并行（审计 #24 / 3.11）

- **审计位置**：[file:///workspace/docs/superpowers/specs/2026-07-21-code-audit/04-order.md#L231-L236]
- **代码位置**：[file:///workspace/src/Services/Order/Leno.Order.Application/Services/OrderSagaOrchestrator.cs#L58-L85]
- **根因**：多卖家拆单时各组顺序执行（预占库存 → 冻结积分 → 保存订单），未并行，N 个卖家的下单延迟 = N × 单组延迟。
- **修复步骤**：
  1. 将 `ExecuteGroupAsync` 各组调用改为 `Task.WhenAll(groups.Select(g => ExecuteGroupAsync(g, ct)))` 并行执行
  2. 失败时对已完成组执行 `CompensateAsync`（参考 P0-T7 修复后的 CompensateAsync，已具备错误传播能力）
  3. 在 `OrderSagaOrchestrator` 中新增 `_semaphoreSlim` 限流（如 maxDegreeOfParallelism = 5）防止多卖家场景下 Redis 连接耗尽
  4. 补充单元测试：5 个卖家并行下单延迟 < 1.5 × 单组延迟；任一组失败时其他组成功并触发补偿
  5. 补充并发测试：验证并行下不会有 Redis 连接泄漏
- **影响范围**：多卖家下单 Saga 编排
- **验证方法**：基准测试验证 N=5 时延迟显著下降；并发测试验证补偿正确性

### P1-T25：LogisticsTrackingService 静默吞掉所有远程失败（审计 #25 / 3.12）

- **审计位置**：[file:///workspace/docs/superpowers/specs/2026-07-21-code-audit/04-order.md#L238-L243]
- **代码位置**：[file:///workspace/src/Services/Order/Leno.Order.Infrastructure/Services/LogisticsTrackingService.cs#L51-L115]
- **根因**：try-catch 所有 Exception 后仅 `_logger.LogWarning`，降级返回缓存或空轨迹。第三方 API 持续失败时不会触发熔断/告警指标，运维无感知。
- **修复步骤**：
  1. 注入 `AntiCorruptionMetrics`（已在 `Leno.Infrastructure.AntiCorruption` 中定义）
  2. 在 catch 块中调用 `_metrics.RecordFailure("LogisticsTrackingService")` 上报指标
  3. 持续失败超阈值（如连续 5 次）时切换为降级模式（设置 `IsDegraded = true`），并显式 `AddDomainEvent(new LogisticsServiceDegradedEvent(...))` 上报告警
  4. 在 `OrderDbContext` 或 `SystemAdmin` 增加降级状态查询接口供运维侧观察
  5. 补充单元测试：模拟 5 次连续失败后 `IsDegraded = true`；恢复后 `IsDegraded = false`
- **影响范围**：物流轨迹查询；新增 1 个领域事件
- **验证方法**：单元测试验证指标上报与降级模式切换；Prometheus 指标 `anticorruption_failure_total{service="LogisticsTrackingService"}` 增长

### P1-T26：OrderDbContext 未配置全局查询过滤器软删除（审计 #26 / 3.13）

- **审计位置**：[file:///workspace/docs/superpowers/specs/2026-07-21-code-audit/04-order.md#L245-L251]
- **代码位置**：
  - [file:///workspace/src/Services/Order/Leno.Order.Infrastructure/OrderDbContext.cs#L14-L34]（继承 `BaseDbContext` 但注释提到的软删除过滤器未生效）
  - [file:///workspace/src/Services/Order/Leno.Order.Infrastructure/Configurations/OrderConfiguration.cs#L12-L94]（未配置 `HasQueryFilter`，且 `Order` 类无 `IsDeleted` 字段）
- **根因**：注释说继承 `BaseDbContext` 复用"软删除查询过滤器"，但 `OrderConfiguration` 未配置 `HasQueryFilter`，且 `Order` 聚合无 `IsDeleted` 字段。`OrderRepository.RemoveAsync` 调用 `_context.Orders.Remove(aggregate)` 是物理删除。
- **修复步骤**：
  1. 在 `OrderAggregate` 增加 `public bool IsDeleted { get; private set; }` 与 `public DateTime? DeletedAt { get; private set; }` 字段
  2. 新增 `Order.SoftDelete(Guid operatorId)` 方法：设置 `IsDeleted = true; DeletedAt = DateTime.UtcNow`，并发布 `OrderSoftDeletedDomainEvent`
  3. `OrderConfiguration` 配置 `HasQueryFilter(o => !o.IsDeleted)` 与 `HasIndex(o => o.IsDeleted)`
  4. `OrderRepository.RemoveAsync` 改为调用 `aggregate.SoftDelete(...)` 而非 `_context.Orders.Remove(aggregate)`
  5. 对 `OrderItem / FreightTemplate / LogisticsCompany / StockReservation / StockReservationCompensation` 等聚合同步配置 `HasQueryFilter`（如它们包含 `IsDeleted` 字段）
  6. 补充单元测试：查询时默认不返回 `IsDeleted=true` 记录；`IgnoreQueryFilters()` 可显式查询
- **影响范围**：Order 全 BC 软删除语义；新增 1 个领域事件
- **验证方法**：单元测试验证 `IsDeleted=true` 记录默认不可见；EF Core 模型快照包含 `HasQueryFilter`

### P1-T27：OrderGrpcService.GetOrderSellerId 返回 GetHashCode 作为 long 标识（审计 #27 / 3.14）

- **审计位置**：[file:///workspace/docs/superpowers/specs/2026-07-21-code-audit/04-order.md#L253-L259]
- **代码位置**：
  - [file:///workspace/src/Services/Order/Leno.Order.Api/GrpcServices/OrderGrpcService.cs#L52-L82]（`SellerId = (long)sellerId.GetHashCode()`）
  - [file:///workspace/src/Services/Order/Leno.Order.Api/GrpcServices/OrderGrpcService.cs#L104-L112]（`SkuId = (long)item.SkuId.GetHashCode()`）
- **根因**：`Guid.GetHashCode()` 是 32 位 int，转 long 后存在大量哈希碰撞，不同 Guid 可能映射到同一 long。已发布到生产接口。
- **修复步骤**：
  1. **方案 A（推荐）**：移除 proto 中 `int64 seller_id` / `int64 sku_id` 字段，强制消费方使用 `string seller_id_str` / `string sku_id_str`（Guid.ToString()）。需同步更新 `.proto` 文件并协调所有消费方
  2. **方案 B（兼容性优先）**：保留 `int64` 字段但改用确定性映射：`SellerId = BitConverter.ToInt64(sellerId.ToByteArray(), 0)`，将 Guid 前 8 字节作为 long。虽然存在极小概率碰撞（2^64），但远低于 GetHashCode 的 2^32 碰撞率
  3. 在 `decisions/0007-guid-string-migration-strategy.md` 中记录决策
  4. 补充单元测试：相同 Guid 多次调用返回相同 long；10000 个不同 Guid 无碰撞（方案 B）
- **影响范围**：gRPC 接口契约；跨 BC 调用方
- **验证方法**：单元测试验证映射确定性；协调消费方切换 proto 字段

---

## P2 修复清单（简化任务清单格式：审计位置/代码位置/根因/修复步骤/验证方法）

### P2-T28：Application 层大量 await 缺少 ConfigureAwait(false)（审计 #28 / 4.1）

- **审计位置**：[file:///workspace/docs/superpowers/specs/2026-07-21-code-audit/04-order.md#L263-L270]
- **代码位置**：
  - [file:///workspace/src/Services/Order/Leno.Order.Application/Services/OrderAppService.cs]（全文）
  - [file:///workspace/src/Services/Order/Leno.Order.Application/Services/OrderSagaOrchestrator.cs]（全文）
  - [file:///workspace/src/Services/Order/Leno.Order.Application/Services/FreightTemplateAppService.cs]（全文）
- **根因**：除 `SeckillOrderCreationService` 外，应用层大量 `await` 未 `ConfigureAwait(false)`。
- **修复步骤**：批量添加 `.ConfigureAwait(false)`，可使用 Roslyn analyzer 自动化
- **验证方法**：编译通过；新增 Roslyn analyzer 防回归

### P2-T29：OrderAppService.GetByIdAsync/QueryAsync/GetLogisticsTraceAsync 标记 Obsolete 但仍被 Controller 使用（审计 #29 / 4.2）

- **审计位置**：[file:///workspace/docs/superpowers/specs/2026-07-21-code-audit/04-order.md#L272-L278]
- **代码位置**：
  - [file:///workspace/src/Services/Order/Leno.Order.Application/Services/OrderAppService.cs#L383-L463]（Obsolete 但 Controller 仍调用）
  - [file:///workspace/src/Services/Order/Leno.Order.Api/Controllers/OrdersController.cs#L65-L79]（未切换到 IQueryHandler）
- **根因**：注释说"2026-08-01 移除"，但 Controller 仍调用旧方法。
- **修复步骤**：将 Controller 切换到 `IQueryHandler<OrderDetailQuery, OrderDetailDto>` / `IQueryHandler<OrderListQuery, OrderListResultDto>` / `IQueryHandler<LogisticsTraceQuery, LogisticsTraceVO>`，删除 Obsolete 方法
- **验证方法**：编译通过且 Controller 单元测试通过；搜索 `IOrderAppService.GetByIdAsync` 在 Api 层无调用

### P2-T30：OrderNumberGenerator 唯一性保证弱（审计 #30 / 4.3）

- **审计位置**：[file:///workspace/docs/superpowers/specs/2026-07-21-code-audit/04-order.md#L280-L285]
- **代码位置**：[file:///workspace/src/Services/Order/Leno.Order.Infrastructure/Services/OrderNumberGenerator.cs#L9-L16]
- **根因**：`LN{yyyyMMddHHmmss}{6位随机数}` 6 位随机数空间仅 100w，同秒内 1000 单/秒时碰撞概率显著。
- **修复步骤**：在订单号中增加机器位（如 hostname hash 4 位）+ 时间戳毫秒 + 4 位随机数；或使用 Snowflake 风格 ID；数据库唯一索引兜底
- **验证方法**：单元测试验证 1000 单/秒并发无碰撞；唯一索引兜底测试

### P2-T31：StockReservationCompensationConfiguration 缺少 (OrderId, SkuId) 复合唯一索引（审计 #31 / 4.4）

- **审计位置**：[file:///workspace/docs/superpowers/specs/2026-07-21-code-audit/04-order.md#L287-L292]
- **代码位置**：[file:///workspace/src/Services/Order/Leno.Order.Infrastructure/Configurations/StockReservationCompensationConfiguration.cs#L11-L35]
- **根因**：仅按 Status 与 OrderId 单字段索引，同一订单同一 SKU 可能被多次写入补偿表。
- **修复步骤**：增加 `HasIndex(c => new { c.OrderId, c.SkuId }).HasFilter("[status] = 'Pending'").IsUnique()`（SQL Server 过滤索引）
- **验证方法**：EF Core 模型快照验证索引；并发回滚测试验证重复插入抛 DbUpdateException

### P2-T32：InternalOrdersController 双路由 Obsolete 标注（审计 #32 / 4.5）

- **审计位置**：[file:///workspace/docs/superpowers/specs/2026-07-21-code-audit/04-order.md#L294-L299]
- **代码位置**：[file:///workspace/src/Services/Order/Leno.Order.Api/Controllers/InternalOrdersController.cs#L22-L35]
- **根因**：`[Obsolete("双路由期保留，1 周后下线")]` 未给出具体下线日期与跟踪 issue。
- **修复步骤**：在 Obsolete 注释中明确下线日期（如 `2026-08-15`）并关联 GitHub issue；在 API 网关层配置旧路由访问告警
- **验证方法**：grep Obsolete 注释中包含具体日期；网关日志验证旧路由访问次数

### P2-T33：SeckillOrderCreationService 占位地址硬编码"待补充"（审计 #33 / 4.6）

- **审计位置**：[file:///workspace/docs/superpowers/specs/2026-07-21-code-audit/04-order.md#L301-L306]
- **代码位置**：[file:///workspace/src/Services/Order/Leno.Order.Application/Services/SeckillOrderCreationService.cs#L60-L72]
- **根因**：秒杀订单使用占位地址 `"待补充"`，但 `Order` 聚合无"补充地址"方法。
- **修复步骤**：在 `OrderAggregate` 增加 `UpdateAddress(AddressSnapshot newAddress, Guid operatorId)` 方法（仅在 `PendingPayment` 且 `OrderType.Seckill` 状态下允许），发布 `OrderAddressUpdatedDomainEvent`
- **验证方法**：单元测试验证秒杀订单支付前可更新地址；非 Seckill 类型调用抛异常

### P2-T34：OrderCancelledDomainEvent 使用 Math.Round 转换积分到分可能丢精度（审计 #34 / 4.7）

- **审计位置**：[file:///workspace/docs/superpowers/specs/2026-07-21-code-audit/04-order.md#L308-L314]
- **代码位置**：
  - [file:///workspace/src/Services/Order/Leno.Order.Domain/Aggregates/Order.cs#L463]
  - [file:///workspace/src/Services/Order/Leno.Order.Domain/Aggregates/Order.cs#L483]
- **根因**：`(int)Math.Round(PointsOffsetAmount * 100)` 默认 `MidpointRounding.ToEven`（银行家舍入），金融场景常用 `AwayFromZero`。
- **修复步骤**：统一改为 `(int)Math.Round(PointsOffsetAmount * 100m, MidpointRounding.AwayFromZero)`，并在 ADR 中文档化舍入策略
- **验证方法**：单元测试验证 0.005 元转分 = 1（AwayFromZero）；与 Saga 中舍入策略一致

### P2-T35：OrderListQuery.PageIndex 从 0 起，OrderListResultDto.Page 从 1 起，混用易错（审计 #35 / 4.8）

- **审计位置**：[file:///workspace/docs/superpowers/specs/2026-07-21-code-audit/04-order.md#L316-L323]
- **代码位置**：
  - [file:///workspace/src/Services/Order/Leno.Order.Application/Queries/OrderListQuery.cs#L25-L29]（PageIndex 从 0 起）
  - [file:///workspace/src/Services/Order/Leno.Order.Application/DTOs/OrderDtos.cs#L192-L201]（Page 从 1 起）
  - [file:///workspace/src/Services/Order/Leno.Order.Api/Controllers/OrdersController.cs#L65-L69]（Controller `page = 1` 默认值）
- **根因**：新旧接口分页索引基数不一致，前端混用易错。
- **修复步骤**：统一所有分页接口从 0 起（CQRS 标准），文档明确；前端同步更新
- **验证方法**：单元测试验证 `PageIndex=0` 返回第一页；Controller 接口契约测试

### P2-T36：OrderDbContext 不暴露 StockReservation 的导航关系（审计 #36 / 4.9）

- **审计位置**：[file:///workspace/docs/superpowers/specs/2026-07-21-code-audit/04-order.md#L325-L331]
- **代码位置**：
  - [file:///workspace/src/Services/Order/Leno.Order.Infrastructure/OrderDbContext.cs#L14-L34]
  - [file:///workspace/src/Services/Order/Leno.Order.Infrastructure/Configurations/StockReservationConfiguration.cs#L10-L29]
- **根因**：`DbSet<StockReservation>` 存在但因 `IInventoryRepository` 绕过聚合根，该 DbSet 仅被对账后台服务用 `Skip/Take` 分页读取，无业务代码通过 `_context.StockReservations` 操作聚合根。
- **修复步骤**：与 P0-T1 修复联动——若 P0-T1 让所有库存操作经过 `StockReservation` 聚合根，则保留 DbSet；否则考虑删除 `StockReservation` 聚合（Redis 是真相源，DB 仅作对账快照），将对账后台改为直接读 Redis 并与 `stock_reservation_snapshots` 表对账
- **验证方法**：决策记录在 ADR；测试验证对账流程正确性

---

## 已修复项表（[ALREADY-FIXED] 跳过项）

| # | 问题标题 | 审计位置 | 修复证据 | 状态 |
|---|---------|---------|---------|------|
| p0a-T2 | StockReservationDomainService.ReserveAsync 检查后未在 Redis Lua 脚本中校验库存量 | 04-order.md §2（历史） | [file:///workspace/src/Services/Order/Leno.Order.Infrastructure/Services/StockReservationDomainService.cs#L1-L132]：`ReserveAsync` 已实现 Lua 原子校验+扣减 | ✅ [ALREADY-FIXED] |
| p0a-T3 | StockReservationDomainService.ReleaseAsync 释放已扣减库存时回退到预占 | 04-order.md §2（历史） | [file:///workspace/src/Services/Order/Leno.Order.Infrastructure/Repositories/RedisInventoryRepository.cs#L24-L54]：`ReleaseLuaScript` 已区分预占/已扣减类型 | ✅ [ALREADY-FIXED] |
| T7 | OrderSagaOrchestrator.CompensateAsync 抛异常导致 Saga 卡死 | 04-order.md §2.7（部分历史） | [file:///workspace/src/Services/Order/Leno.Order.Application/Services/OrderSagaOrchestrator.cs#L204-L256]：`CompensateAsync` 已实现错误收集与最终抛出（但 P0-T7 仍需改进错误传播） | ✅ [ALREADY-FIXED]（结构层面） |
| T8 | OrderPricingDomainService.ValidatePricesAsync N+1 远程调用 | 04-order.md §2.9（部分历史） | [file:///workspace/src/Services/Order/Leno.Order.Infrastructure/Services/OrderPricingDomainService.cs#L21-L33]：已封装 ValidatePricesAsync（但 P0-T9 仍需改批量接口） | ✅ [ALREADY-FIXED]（结构层面） |
| T9 | OrderSagaOrchestrator 积分抵现裁剪逻辑 | 04-order.md §2.8（部分历史） | [file:///workspace/src/Services/Order/Leno.Order.Application/Services/OrderSagaOrchestrator.cs#L128-L177]：已实现裁剪逻辑（但 P0-T8 仍需迁移到聚合不变量） | ✅ [ALREADY-FIXED]（结构层面） |
| T16 | ExecuteGroupAsync 调度延迟消息事务 | 04-order.md §2.12（部分历史） | [file:///workspace/src/Services/Order/Leno.Order.Application/Services/OrderSagaOrchestrator.cs#L182-L188]：`ScheduleSend` 已封装（但 P0-T12 仍需调整事务边界） | ✅ [ALREADY-FIXED]（结构层面） |
| T18 | StockReservationCompensation 补偿表 | 04-order.md §2（历史） | [file:///workspace/src/Services/Order/Leno.Order.Domain/Aggregates/StockReservationCompensation.cs]：聚合已实现，含 `MarkFailed`/`MarkSucceeded`/状态机 | ✅ [ALREADY-FIXED] |

> 注：T7/T8/T9/T16 标注 [ALREADY-FIXED]（结构层面）指代码骨架已存在，但本计划 P0-T7/T8/T9/T12 仍提出进一步改造需求（如错误传播、批量接口、事务边界调整），属于"已修复结构但需完善语义"的二次修复。

---

## 附录：扫描覆盖的关键文件

### Domain 层
- [file:///workspace/src/Services/Order/Leno.Order.Domain/Aggregates/Order.cs]
- [file:///workspace/src/Services/Order/Leno.Order.Domain/Aggregates/StockReservation.cs]
- [file:///workspace/src/Services/Order/Leno.Order.Domain/Aggregates/StockReservationCompensation.cs]
- [file:///workspace/src/Services/Order/Leno.Order.Domain/Aggregates/OrderItem.cs]
- [file:///workspace/src/Services/Order/Leno.Order.Domain/Aggregates/FreightTemplate.cs]
- [file:///workspace/src/Services/Order/Leno.Order.Domain/Aggregates/LogisticsCompany.cs]
- [file:///workspace/src/Services/Order/Leno.Order.Domain/Events/OrderDomainEvents.cs]
- [file:///workspace/src/Services/Order/Leno.Order.Domain/ValueObjects/FreightRegionRule.cs]
- [file:///workspace/src/Services/Order/Leno.Order.Domain/Repositories/IInventoryRepository.cs]
- [file:///workspace/src/Services/Order/Leno.Order.Domain/Repositories/ILogisticsCompanyRepository.cs]

### Application 层
- [file:///workspace/src/Services/Order/Leno.Order.Application/Services/OrderAppService.cs]
- [file:///workspace/src/Services/Order/Leno.Order.Application/Services/OrderSagaOrchestrator.cs]
- [file:///workspace/src/Services/Order/Leno.Order.Application/Services/SeckillOrderCreationService.cs]
- [file:///workspace/src/Services/Order/Leno.Order.Application/Queries/LogisticsTraceQueryHandler.cs]

### Infrastructure 层
- [file:///workspace/src/Services/Order/Leno.Order.Infrastructure/Configurations/OrderConfiguration.cs]
- [file:///workspace/src/Services/Order/Leno.Order.Infrastructure/Repositories/RedisInventoryRepository.cs]
- [file:///workspace/src/Services/Order/Leno.Order.Infrastructure/Consumers/PaymentSucceededEventConsumer.cs]
- [file:///workspace/src/Services/Order/Leno.Order.Infrastructure/Consumers/OrderTimeoutDelayMessageConsumer.cs]
- [file:///workspace/src/Services/Order/Leno.Order.Infrastructure/Consumers/AfterSalesWindowConsumer.cs]
- [file:///workspace/src/Services/Order/Leno.Order.Infrastructure/Consumers/RefundCompletedEventConsumer.cs]
- [file:///workspace/src/Services/Order/Leno.Order.Infrastructure/Services/StockReconciliationService.cs]
- [file:///workspace/src/Services/Order/Leno.Order.Infrastructure/Services/OrderPricingDomainService.cs]
- [file:///workspace/src/Services/Order/Leno.Order.Infrastructure/Services/StockReservationDomainService.cs]
- [file:///workspace/src/Services/Order/Leno.Order.Infrastructure/Services/LogisticsTrackingService.cs]
- [file:///workspace/src/Services/Order/Leno.Order.Infrastructure/Services/StockReservationCompensationBackgroundService.cs]
- [file:///workspace/src/Services/Order/Leno.Order.Infrastructure/OrderDbContext.cs]

### Api 层
- [file:///workspace/src/Services/Order/Leno.Order.Api/GrpcServices/OrderGrpcService.cs]
- [file:///workspace/src/Services/Order/Leno.Order.Api/Controllers/OrdersController.cs]
- [file:///workspace/src/Services/Order/Leno.Order.Api/Controllers/InternalOrdersController.cs]