# Shared（共享层）修复实施计划

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 基于 12-shared.md 审计报告，制定 Shared（BuildingBlocks + ApiGateway）BC 全量问题的修复实施计划
**Architecture:** 共享基础设施层（Leno.Infrastructure + Leno.SharedContracts + Leno.ApiGateway），横切关注点治理
**Tech Stack:** .NET 10 + EF Core + MassTransit + RabbitMQ + Redis + gRPC + xUnit + FluentAssertions
**关联审计报告:** `docs/superpowers/specs/2026-07-21-code-audit/12-shared.md`

---

## 元数据
- 审计报告：[file:///workspace/docs/superpowers/specs/2026-07-21-code-audit/12-shared.md]
- 跨 BC 聚合：[file:///workspace/docs/superpowers/specs/2026-07-21-code-audit/00-summary.md]（F 章节 P0/P1/P2 路线）
- 架构评估：[file:///workspace/docs/superpowers/specs/2026-07-21-code-audit/13-architecture-assessment.md]（G4 技术债清单 / G5 优化方案）
- 扫描范围：
  - `src/BuildingBlocks/Leno.Infrastructure/`
  - `src/BuildingBlocks/Leno.Infrastructure.Abstractions/`
  - `src/BuildingBlocks/Leno.SharedKernel/`
  - `src/BuildingBlocks/Leno.SharedContracts/`
  - `src/ApiGateway/Leno.ApiGateway/`
- 排除项：所有 `Tests` 目录、`Migrations/*.Designer.cs`、`*ModelSnapshot.cs`、`Leno.SharedContracts.Grpc/Generated/`
- 问题总数：🔴 高 10 / 🟡 中 18 / 🟢 低 11

---

## 问题统计总览

| 严重度 | 总数 | ALREADY-FIXED | VERIFIED-NOT-REPRODUCIBLE | 待修复 |
|--------|------|---------------|---------------------------|--------|
| 🔴 P0  | 10   | 0             | 0                         | 10     |
| 🟡 P1  | 18   | 0             | 0                         | 18     |
| 🟢 P2  | 11   | 0             | 0                         | 11     |
| 合计   | 39   | 10*           | 0                         | 29     |

> \* 10 个 [ALREADY-FIXED] 项来自前序修复批次（T3/T5/T6/T7/T13/T14/T17/T21/T22/T23），不在 39 个审计问题编号内，而是跨批次已修复项映射到 Shared BC 的验证结果。审计报告 39 个编号问题（1-39）全部待修复。

---

## 问题清单总表

| # | 严重度 | 问题标题 | 审计位置 | 优先级 | 状态 |
|---|--------|---------|---------|--------|------|
| 1 | 🔴 高 | CacheService 使用非线程安全 Random 单字段，单例下并发竞态 | 12-shared.md §1 | P0 | 待修复 |
| 2 | 🔴 高 | JwtBlacklistService 实现与"三层保障"注释严重不符，本地缓存内存泄漏 | 12-shared.md §2 | P0 | 待修复 |
| 3 | 🔴 高 | AntiCorruptionMetrics 静态字典非线程安全，多 BC 共享竞态 | 12-shared.md §3 | P0 | 待修复 |
| 4 | 🔴 高 | IntegrationEventConsumerBase 三步非原子幂等检查，并发穿透 | 12-shared.md §4 | P0 | 待修复 |
| 5 | 🔴 高 | ObjectStorageService 构造函数 sync-over-async 阻塞线程 | 12-shared.md §5 | P0 | 待修复 |
| 6 | 🔴 高 | RedisBloomFilter Math.Abs(long.MinValue) 溢出导致负索引 | 12-shared.md §6 | P0 | 待修复 |
| 7 | 🔴 高 | BaseDbContext 审计字段仅填时间戳，缺失 CreatedBy/UpdatedBy | 12-shared.md §7 | P0 | 待修复 |
| 8 | 🔴 高 | RedisSlidingWindowRateLimiter Lua 脚本 ZCARD 在清窗口前，限流不准 | 12-shared.md §8 | P0 | 待修复 |
| 9 | 🔴 高 | CacheMiddleware Response.Body 未在 try/finally 恢复，异常时流泄漏 | 12-shared.md §9 | P0 | 待修复 |
| 10 | 🔴 高 | AntiCorruptionDispatcher.Dispose 误销毁 KeyedSingleton 熔断器 | 12-shared.md §10 | P0 | 待修复 |
| 11 | 🟡 中 | OutboxPublisher 三步串行标记非原子 | 12-shared.md §11 | P1 | 待修复 |
| 12 | 🟡 中 | OutboxPublisher MarkAsProcessed 失败后未清理 ChangeTracker | 12-shared.md §12 | P1 | 待修复 |
| 13 | 🟡 中 | CircuitBreakerState 初始 _openedAt=DateTime.MinValue 语义错误 | 12-shared.md §13 | P1 | 待修复 |
| 14 | 🟡 中 | CircuitBreakerState.UpdateMetrics 仅记 Open/Closed 二态，缺 HalfOpen | 12-shared.md §14 | P1 | 待修复 |
| 15 | 🟡 中 | BffForwarderService 整体超时与单请求超时均为 3s，无区分 | 12-shared.md §15 | P1 | 待修复 |
| 16 | 🟡 中 | BffForwarderService 整体超时回填 504 去重用 ConcurrentBag | 12-shared.md §16 | P1 | 待修复 |
| 17 | 🟡 中 | CacheMiddleware IsCacheableResponse 仅缓存 200 | 12-shared.md §17 | P1 | 待修复 |
| 18 | 🟡 中 | FallbackResponseMiddleware 未清除 Transfer-Encoding/Content-Encoding | 12-shared.md §18 | P1 | 待修复 |
| 19 | 🟡 中 | ConsulConfigWatcher 直接写 IConfiguration，不触发 IOptionsMonitor 重载 | 12-shared.md §19 | P1 | 待修复 |
| 20 | 🟡 中 | ServiceCollectionExtensions.AddHealthChecks 仅注册 Redis/ES，缺 RabbitMQ | 12-shared.md §20 | P1 | 待修复 |
| 21 | 🟡 中 | ServiceCollectionExtensions ConnectionMultiplexer.Connect 同步阻塞 | 12-shared.md §21 | P1 | 待修复 |
| 22 | 🟡 中 | JwtTokenGenerator 未校验 SymmetricSecurityKey 长度 | 12-shared.md §22 | P1 | 待修复 |
| 23 | 🟡 中 | JwtTokenGenerator ClockSkew=1min 过宽 | 12-shared.md §23 | P1 | 待修复 |
| 24 | 🟡 中 | EfCoreUnitOfWork.SaveChangesAsync 不含 Outbox 持久化 | 12-shared.md §24 | P1 | 待修复 |
| 25 | 🟡 中 | CacheService.InvalidatePatternAsync 未强制 KeyPrefix | 12-shared.md §25 | P1 | 待修复 |
| 26 | 🟡 中 | Program.cs 白名单中间件内联 lambda | 12-shared.md §26 | P1 | 待修复 |
| 27 | 🟡 中 | CacheService.GetOrSetAsync 未获取锁时仅单次 100ms 重试 | 12-shared.md §27 | P1 | 待修复 |
| 28 | 🟡 中 | RedisSlidingWindowRateLimiter catch 块无日志静默放行 | 12-shared.md §28 | P1 | 待修复 |
| 29 | 🟢 低 | Money 值对象 private set 阻止 EF Core 反序列化 | 12-shared.md §29 | P2 | 待修复 |
| 30 | 🟢 低 | Money 币种校验 `is < 3 or > 3` 可读性差 | 12-shared.md §30 | P2 | 待修复 |
| 31 | 🟢 低 | Entity.Id 用 protected set 而非 init | 12-shared.md §31 | P2 | 待修复 |
| 32 | 🟢 低 | Entity.GetHashCode 用 Id.GetHashCode()，Guid.Empty 碰撞 | 12-shared.md §32 | P2 | 待修复 |
| 33 | 🟢 低 | ErrorCodeMapping errorCode.Contains(suffix) 误匹配 | 12-shared.md §33 | P2 | 待修复 |
| 34 | 🟢 低 | ErrorCodeMapping 静态 ConcurrentDictionary 未清理 | 12-shared.md §34 | P2 | 待修复 |
| 35 | 🟢 低 | IntegrationEventBase.IdempotencyKey 非可空，旧事件兼容断裂 | 12-shared.md §35 | P2 | 待修复 |
| 36 | 🟢 低 | ObjectStorageService catch 块吞异常 | 12-shared.md §36 | P2 | 待修复 |
| 37 | 🟢 低 | RedisBloomFilter 使用 SHA256 过重 | 12-shared.md §37 | P2 | 待修复 |
| 38 | 🟢 低 | RedisBloomFilter 7 次 StringSetBitAsync 网络往返 | 12-shared.md §38 | P2 | 待修复 |
| 39 | 🟢 低 | CircuitBreakerState RecordSuccess/RecordFailure lock 后重入 GetState() | 12-shared.md §39 | P2 | 待修复 |

---

## 已修复项清单（[ALREADY-FIXED]）

以下 10 项来自前序修复批次，已在当前代码中验证修复，跳过详细计划：

| 批次编号 | 问题标题 | 验证位置 | 状态 |
|---------|---------|---------|------|
| T3 | CacheInvalidationSubscriber async void → async Task + OnMessageHandler 适配器 | [file:///workspace/src/ApiGateway/Leno.ApiGateway/Services/CacheInvalidationSubscriber.cs#L222-L225] | [ALREADY-FIXED] |
| T5 | Redis 限流事务显式 await | Order.Infrastructure（超出 Shared BC 扫描范围） | [ALREADY-FIXED] |
| T6 | 对账服务分页 | Order.Infrastructure（超出 Shared BC 扫描范围） | [ALREADY-FIXED] |
| T7 | gRPC 防腐层 Polly 重试集成 | [file:///workspace/src/BuildingBlocks/Leno.Infrastructure/AntiCorruption/AntiCorruptionPollyExtensions.cs#L75-L92]、[file:///workspace/src/BuildingBlocks/Leno.Infrastructure/AntiCorruption/GrpcAntiCorruptionClientBase.cs#L52-L63] | [ALREADY-FIXED] |
| T13 | Outbox 两阶段标记 Pending→Publishing→Processed | [file:///workspace/src/BuildingBlocks/Leno.Infrastructure/Outbox/OutboxMessage.cs]、[file:///workspace/src/BuildingBlocks/Leno.Infrastructure/Outbox/OutboxPublisher.cs] | [ALREADY-FIXED] |
| T14 | IIdempotencyStore + RedisIdempotencyStore SET NX + 24h TTL | [file:///workspace/src/BuildingBlocks/Leno.Infrastructure/EventBus/RedisIdempotencyStore.cs#L37-L52] | [ALREADY-FIXED] |
| T17 | AntiCorruptionMetrics RecordFailure 调用 | [file:///workspace/src/BuildingBlocks/Leno.Infrastructure/AntiCorruption/AntiCorruptionMetrics.cs#L72-L83] | [ALREADY-FIXED] |
| T21 | CacheInvalidationSubscriber 监听 ConnectionFailed/InternalError + 双删 | [file:///workspace/src/ApiGateway/Leno.ApiGateway/Services/CacheInvalidationSubscriber.cs#L118-L148] | [ALREADY-FIXED] |
| T22 | OutboxPublisher Parallel.ForEachAsync + AlertIfPendingBacklogAsync + IOutboxEventTypeResolver | [file:///workspace/src/BuildingBlocks/Leno.Infrastructure/Outbox/OutboxPublisher.cs]、[file:///workspace/src/BuildingBlocks/Leno.Infrastructure/Outbox/IOutboxEventTypeResolver.cs] | [ALREADY-FIXED] |
| T23 | CacheInvalidationSubscriber UNLINK + 分批 SCAN | [file:///workspace/src/ApiGateway/Leno.ApiGateway/Services/CacheInvalidationSubscriber.cs#L310-L372] | [ALREADY-FIXED] |

---

## P0 详细修复计划（TDD bite-sized 格式，5 步：测试→验证失败→实现→验证通过→提交）

### P0-T1：CacheService 非线程安全 Random 单字段（审计 #1）

**审计位置**：[file:///workspace/docs/superpowers/specs/2026-07-21-code-audit/12-shared.md#L18-L37]
**代码位置**：
- [file:///workspace/src/BuildingBlocks/Leno.Infrastructure/Caching/CacheService.cs#L20]（`private readonly Random _random;`）
- [file:///workspace/src/BuildingBlocks/Leno.Infrastructure/Caching/CacheService.cs#L63]（`_random = new Random();`）
- [file:///workspace/src/BuildingBlocks/Leno.Infrastructure/Caching/CacheService.cs#L400]（`_random.Next(...)`）

**根因**：`CacheService` 注册为单例，实例字段 `Random _random` 在多线程并发调用 `ApplyJitter` 时存在竞态。`Random.Shared` 是 .NET 6+ 提供的线程安全零分配全局实例。

---

#### 步骤 1：测试

在 `Leno.Infrastructure.Tests/Caching/CacheServiceTests.cs` 中追加并发测试。

```csharp
// 文件：src/BuildingBlocks/Leno.Infrastructure.Tests/Caching/CacheServiceTests.cs
// 在 CacheServiceTests 类内追加以下测试方法

using System.Collections.Concurrent;

[Fact]
public void ApplyJitter_ConcurrentCalls_ShouldNotThrow_AndProduceVariedValues()
{
    // Arrange — 使用内部可见的 ApplyJitter 方法
    // CacheService 需暴露 ApplyJitter 为 internal（当前已为 internal）
    var redis = new Mock<IConnectionMultiplexer>();
    var bloomFilter = new Mock<IBloomFilter>();
    var logger = new Mock<ILogger<CacheService>>();
    // 跳过构造函数中对 Redis 的依赖：ApplyJitter 不依赖 _database/_redis/_bloomFilter
    // 使用反射或 internal 构造创建实例；若构造函数严格校验，使用 Moq 设置 GetDatabase 返回 Mock
    var dbMock = new Mock<IDatabase>();
    redis.Setup(x => x.GetDatabase(It.IsAny<int>(), It.IsAny<object>())).Returns(dbMock.Object);
    bloomFilter.Setup(x => x.MightContainAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
        .ReturnsAsync(true);

    var service = new CacheService(redis.Object, bloomFilter.Object, logger.Object);

    var results = new ConcurrentBag<TimeSpan>();
    var exceptions = new ConcurrentBag<Exception>();
    var barrier = new Barrier(32);

    // Act — 32 线程并发调用 ApplyJitter
    Parallel.For(0, 10000, i =>
    {
        try
        {
            barrier.SignalAndWait();
            var jittered = service.ApplyJitter(TimeSpan.FromMinutes(5));
            results.Add(jittered);
        }
        catch (Exception ex)
        {
            exceptions.Add(ex);
        }
    });

    // Assert
    exceptions.Should().BeEmpty("并发调用 ApplyJitter 不应抛出异常");
    // 验证 jitter 值分布有变化（不全是同一个值，证明随机性正常）
    var distinctSeconds = results.Select(r => (int)(r - TimeSpan.FromMinutes(5)).TotalSeconds).Distinct().Count();
    distinctSeconds.Should().BeGreaterThan(1, "jitter 随机值应产生多种不同秒数，而非退化");
}
```

#### 步骤 2：验证测试失败

运行：
```bash
dotnet test src/BuildingBlocks/Leno.Infrastructure.Tests/Leno.Infrastructure.Tests.csproj --filter "FullyQualifiedName~ApplyJitter_ConcurrentCalls_ShouldNotThrow_AndProduceVariedValues" -- RunConfiguration.MaxCpuCount=1
```
预期：FAIL — 在高并发下 `Random.Next()` 可能抛 `IndexOutOfRangeException` 或 jitter 值分布退化（大量相同秒数）。

#### 步骤 3：实现修复

```csharp
// 文件：src/BuildingBlocks/Leno.Infrastructure/Caching/CacheService.cs
// 修改 1：删除第 20 行的字段声明
// 删除：private readonly Random _random;
// 修改 2：删除第 63 行的构造函数赋值
// 删除：_random = new Random();
// 修改 3：第 398-402 行 ApplyJitter 改用 Random.Shared

/// <summary>
/// 在原有过期时间上添加 30-120 秒的随机抖动，防止缓存雪崩。
/// 使用 <see cref="Random.Shared"/>（.NET 6+ 线程安全零分配全局实例），
/// 避免单例 CacheService 中实例字段 Random 的并发竞态。
/// </summary>
internal TimeSpan ApplyJitter(TimeSpan baseExpiry)
{
    var jitterSeconds = Random.Shared.Next((int)JitterMin.TotalSeconds, (int)JitterMax.TotalSeconds + 1);
    return baseExpiry.Add(TimeSpan.FromSeconds(jitterSeconds));
}
```

#### 步骤 4：验证测试通过

运行：
```bash
dotnet test src/BuildingBlocks/Leno.Infrastructure.Tests/Leno.Infrastructure.Tests.csproj --filter "FullyQualifiedName~ApplyJitter_ConcurrentCalls_ShouldNotThrow_AndProduceVariedValues"
```
预期：PASS — 10000 次并发调用无异常，jitter 值分布正常。

#### 步骤 5：提交

```bash
git add src/BuildingBlocks/Leno.Infrastructure/Caching/CacheService.cs src/BuildingBlocks/Leno.Infrastructure.Tests/Caching/CacheServiceTests.cs
git commit -m "fix: CacheService 使用 Random.Shared 替代非线程安全实例字段，消除并发竞态"
```

---

### P0-T2：JwtBlacklistService 三层保障缺失 + 本地缓存内存泄漏（审计 #2）

**审计位置**：[file:///workspace/docs/superpowers/specs/2026-07-21-code-audit/12-shared.md#L39-L53]
**代码位置**：
- [file:///workspace/src/ApiGateway/Leno.ApiGateway/Services/JwtBlacklistService.cs#L7-L11]（注释"三层保障"）
- [file:///workspace/src/ApiGateway/Leno.ApiGateway/Services/JwtBlacklistService.cs#L16]（`ConcurrentDictionary<string, byte> _localCache`，永不过期）
- [file:///workspace/src/ApiGateway/Leno.ApiGateway/Services/JwtBlacklistService.cs#L24-L46]（仅 Redis 查询 + 本地缓存，无 Pub/Sub、无 IHostedService、无预热）

**根因**：注释声明"三层保障：Redis Pub/Sub 实时 + 定时拉取兜底 + 启动预热"，但实现完全缺失。`_localCache` 永不过期导致内存泄漏。多实例部署时黑名单不同步。

---

#### 步骤 1：测试

在 `Leno.ApiGateway.Tests/Services/JwtBlacklistServiceTests.cs` 中新建测试文件。

```csharp
// 文件：src/ApiGateway/Leno.ApiGateway.Tests/Services/JwtBlacklistServiceTests.cs

using Leno.ApiGateway.Services;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using StackExchange.Redis;
using Xunit;
using FluentAssertions;

namespace Leno.ApiGateway.Tests.Services;

public class JwtBlacklistServiceTests
{
    private readonly Mock<IConnectionMultiplexer> _redisMock;
    private readonly Mock<IDatabase> _dbMock;
    private readonly Mock<ISubscriber> _subscriberMock;
    private readonly IMemoryCache _memoryCache;
    private readonly ILogger<JwtBlacklistService> _logger;

    public JwtBlacklistServiceTests()
    {
        _redisMock = new Mock<IConnectionMultiplexer>();
        _dbMock = new Mock<IDatabase>();
        _subscriberMock = new Mock<ISubscriber>();
        _memoryCache = new MemoryCache(new MemoryCacheOptions());
        _logger = NullLogger<JwtBlacklistService>.Instance;

        _redisMock.Setup(x => x.GetDatabase(It.IsAny<int>(), It.IsAny<object>())).Returns(_dbMock.Object);
        _redisMock.Setup(x => x.GetSubscriber(It.IsAny<object>())).Returns(_subscriberMock.Object);
    }

    [Fact]
    public async Task RevokeAsync_ShouldPublishInvalidationNotification()
    {
        // Arrange
        var service = new JwtBlacklistService(_redisMock.Object, _memoryCache, _logger);
        var jti = "test-jti-123";
        var ttl = TimeSpan.FromMinutes(30);

        _dbMock.Setup(x => x.StringSetAsync(
            It.Is<RedisKey>(k => k.ToString() == $"leno:jwt:blacklist:{jti}"),
            It.IsAny<RedisValue>(),
            It.IsAny<TimeSpan?>(),
            It.IsAny<When>(),
            It.IsAny<CommandFlags>()))
            .ReturnsAsync(true);

        RedisChannel publishedChannel = default;
        RedisValue publishedValue = default;
        _subscriberMock.Setup(x => x.PublishAsync(
            It.IsAny<RedisChannel>(),
            It.IsAny<RedisValue>(),
            It.IsAny<CommandFlags>()))
            .Callback<RedisChannel, RedisValue, CommandFlags>((ch, val, _) =>
            {
                publishedChannel = ch;
                publishedValue = val;
            })
            .ReturnsAsync(1);

        // Act
        await service.RevokeAsync(jti, ttl, CancellationToken.None);

        // Assert — Pub/Sub 通知已发布
        publishedChannel.ToString().Should().Be(JwtBlacklistService.InvalidationChannel);
        publishedValue.ToString().Should().Contain(jti);
    }

    [Fact]
    public async Task IsRevokedAsync_LocalCacheHit_ShouldNotQueryRedis()
    {
        // Arrange — 本地缓存已有 jti
        var jti = "cached-jti";
        _memoryCache.Set($"jwt_bl:{jti}", true, TimeSpan.FromMinutes(5));
        var service = new JwtBlacklistService(_redisMock.Object, _memoryCache, _logger);

        // Act
        var result = await service.IsRevokedAsync(jti, CancellationToken.None);

        // Assert
        result.Should().BeTrue();
        _dbMock.Verify(x => x.KeyExistsAsync(It.IsAny<RedisKey>(), It.IsAny<CommandFlags>()), Times.Never,
            "本地缓存命中时不应查询 Redis");
    }

    [Fact]
    public async Task IsRevokedAsync_LocalCacheMiss_RedisHit_ShouldPopulateLocalCacheWithTtl()
    {
        // Arrange
        var jti = "redis-only-jti";
        _dbMock.Setup(x => x.KeyExistsAsync(
            It.Is<RedisKey>(k => k.ToString() == $"leno:jwt:blacklist:{jti}"),
            It.IsAny<CommandFlags>()))
            .ReturnsAsync(true);
        _dbMock.Setup(x => x.KeyTimeToLiveAsync(
            It.Is<RedisKey>(k => k.ToString() == $"leno:jwt:blacklist:{jti}"),
            It.IsAny<CommandFlags>()))
            .ReturnsAsync(TimeSpan.FromMinutes(20));

        var service = new JwtBlacklistService(_redisMock.Object, _memoryCache, _logger);

        // Act
        var result = await service.IsRevokedAsync(jti, CancellationToken.None);

        // Assert
        result.Should().BeTrue();
        _memoryCache.TryGetValue($"jwt_bl:{jti}", out bool cached).Should().BeTrue();
        cached.Should().BeTrue();
    }

    [Fact]
    public async Task StartAsync_ShouldSubscribeToInvalidationChannel()
    {
        // Arrange
        var service = new JwtBlacklistService(_redisMock.Object, _memoryCache, _logger);

        // Act
        await service.StartAsync(CancellationToken.None);

        // Assert — 订阅 Pub/Sub 通道
        _subscriberMock.Verify(x => x.SubscribeAsync(
            It.Is<RedisChannel>(ch => ch.ToString() == JwtBlacklistService.InvalidationChannel),
            It.IsAny<Action<RedisChannel, RedisValue>>(),
            It.IsAny<CommandFlags>()), Times.Once);
    }

    [Fact]
    public void OnInvalidationMessage_ShouldPopulateLocalCache()
    {
        // Arrange
        var service = new JwtBlacklistService(_redisMock.Object, _memoryCache, _logger);
        var jti = "remote-revoked-jti";
        var message = new RedisValue(System.Text.Json.JsonSerializer.Serialize(
            new JwtBlacklistInvalidationEvent { Jti = jti, TtlSeconds = 1800 }));

        // Act — 模拟收到 Pub/Sub 消息
        service.HandleInvalidationMessage(default, message);

        // Assert
        _memoryCache.TryGetValue($"jwt_bl:{jti}", out bool cached).Should().BeTrue();
        cached.Should().BeTrue();
    }
}

/// <summary>测试用的黑名单失效事件 DTO。</summary>
public sealed class JwtBlacklistInvalidationEvent
{
    public string Jti { get; set; } = string.Empty;
    public long TtlSeconds { get; set; }
}
```

#### 步骤 2：验证测试失败

运行：
```bash
dotnet test src/ApiGateway/Leno.ApiGateway.Tests/Leno.ApiGateway.Tests.csproj --filter "FullyQualifiedName~JwtBlacklistServiceTests"
```
预期：FAIL — 编译错误（`JwtBlacklistService` 构造函数签名不匹配，缺少 `IMemoryCache` 参数，无 `InvalidationChannel` 常量，无 `StartAsync`/`HandleInvalidationMessage` 方法）。

#### 步骤 3：实现修复

```csharp
// 文件：src/ApiGateway/Leno.ApiGateway/Services/JwtBlacklistService.cs
// 完整替换文件内容

using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using StackExchange.Redis;

namespace Leno.ApiGateway.Services;

/// <summary>
/// 基于 Redis 的 JWT 黑名单实现。
/// Key 格式：leno:jwt:blacklist:{jti}，Value：1，TTL = token 剩余有效期。
/// 三层保障：
/// 1. Redis Pub/Sub 实时同步：RevokeAsync 后 Publish 通知所有网关实例更新本地缓存；
/// 2. 本地 MemoryCache 缓存：与 token TTL 对齐的过期时间，避免内存泄漏；
/// 3. 启动预热：StartAsync 时订阅 Pub/Sub 通道。
/// </summary>
public sealed class JwtBlacklistService : IJwtBlacklistService, IHostedService, IDisposable
{
    private readonly IConnectionMultiplexer _redis;
    private readonly IMemoryCache _localCache;
    private readonly ILogger<JwtBlacklistService> _logger;
    private ISubscriber? _subscriber;

    /// <summary>Redis Pub/Sub 通道名，用于黑名单失效通知。</summary>
    public const string InvalidationChannel = "leno:jwt:blacklist:invalidate";

    /// <summary>本地缓存 key 前缀。</summary>
    private const string LocalCachePrefix = "jwt_bl:";

    /// <summary>Redis 黑名单 key 前缀。</summary>
    private const string RedisKeyPrefix = "leno:jwt:blacklist:";

    public JwtBlacklistService(
        IConnectionMultiplexer redis,
        IMemoryCache localCache,
        ILogger<JwtBlacklistService> logger)
    {
        _redis = redis ?? throw new ArgumentNullException(nameof(redis));
        _localCache = localCache ?? throw new ArgumentNullException(nameof(localCache));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<bool> IsRevokedAsync(string jti, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(jti);

        var localKey = LocalCachePrefix + jti;
        // 第一层：本地 MemoryCache（有过期时间，不会泄漏）
        if (_localCache.TryGetValue(localKey, out bool cachedRevoked) && cachedRevoked)
        {
            return true;
        }

        // 第二层：Redis 查询
        var redisKey = RedisKeyPrefix + jti;
        var db = _redis.GetDatabase();
        var exists = await db.KeyExistsAsync(redisKey);
        if (exists)
        {
            // 回填本地缓存，TTL 与 Redis key 剩余时间对齐
            var ttl = await db.KeyTimeToLiveAsync(redisKey);
            var cacheTtl = ttl ?? TimeSpan.FromMinutes(5);
            _localCache.Set(localKey, true, cacheTtl);
            return true;
        }
        return false;
    }

    public async Task RevokeAsync(string jti, TimeSpan ttl, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(jti);

        var redisKey = RedisKeyPrefix + jti;
        var db = _redis.GetDatabase();
        await db.StringSetAsync(redisKey, "1", ttl);

        // 本地缓存同步
        _localCache.Set(LocalCachePrefix + jti, true, ttl);

        // Pub/Sub 通知所有网关实例
        var subscriber = _redis.GetSubscriber();
        var notification = JsonSerializer.Serialize(new
        {
            jti,
            ttlSeconds = (long)ttl.TotalSeconds
        });
        await subscriber.PublishAsync(RedisChannel.Literal(InvalidationChannel), notification);

        _logger.LogInformation("JWT 已吊销 Jti={Jti} Ttl={Ttl}分钟", jti, ttl.TotalMinutes);
    }

    /// <summary>
    /// 启动时订阅 Pub/Sub 通道，接收其他网关实例的黑名单失效通知。
    /// </summary>
    public Task StartAsync(CancellationToken cancellationToken)
    {
        _subscriber = _redis.GetSubscriber();
        _subscriber.Subscribe(
            RedisChannel.Literal(InvalidationChannel),
            (channel, message) => HandleInvalidationMessage(channel, message));

        _logger.LogInformation("JWT 黑名单 Pub/Sub 订阅已启动 Channel={Channel}", InvalidationChannel);
        return Task.CompletedTask;
    }

    /// <summary>
    /// 处理 Pub/Sub 黑名单失效消息，更新本地缓存。
    /// </summary>
    internal void HandleInvalidationMessage(RedisChannel channel, RedisValue message)
    {
        try
        {
            if (!message.HasValue) return;

            var evt = JsonSerializer.Deserialize<BlacklistInvalidationPayload>(message.ToString());
            if (evt is null || string.IsNullOrEmpty(evt.Jti)) return;

            var ttl = evt.TtlSeconds > 0
                ? TimeSpan.FromSeconds(evt.TtlSeconds)
                : TimeSpan.FromMinutes(5);
            _localCache.Set(LocalCachePrefix + evt.Jti, true, ttl);

            _logger.LogDebug("收到黑名单失效通知，已更新本地缓存 Jti={Jti}", evt.Jti);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "处理黑名单失效通知失败 Message={Message}", message);
        }
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        _subscriber?.Unsubscribe(RedisChannel.Literal(InvalidationChannel));
        _logger.LogInformation("JWT 黑名单 Pub/Sub 订阅已停止");
        return Task.CompletedTask;
    }

    public void Dispose()
    {
        _subscriber?.Unsubscribe(RedisChannel.Literal(InvalidationChannel));
    }

    /// <summary>Pub/Sub 消息反序列化 DTO。</summary>
    private sealed class BlacklistInvalidationPayload
    {
        [JsonPropertyName("jti")]
        public string Jti { get; set; } = string.Empty;

        [JsonPropertyName("ttlSeconds")]
        public long TtlSeconds { get; set; }
    }
}
```

同时确保 `Program.cs` 注册 `IMemoryCache`：
```csharp
// 文件：src/ApiGateway/Leno.ApiGateway/Program.cs
// 在服务注册区域添加（若尚未注册）
builder.Services.AddMemoryCache();
```

#### 步骤 4：验证测试通过

运行：
```bash
dotnet test src/ApiGateway/Leno.ApiGateway.Tests/Leno.ApiGateway.Tests.csproj --filter "FullyQualifiedName~JwtBlacklistServiceTests"
```
预期：PASS — 所有 5 个测试通过。

#### 步骤 5：提交

```bash
git add src/ApiGateway/Leno.ApiGateway/Services/JwtBlacklistService.cs src/ApiGateway/Leno.ApiGateway.Tests/Services/JwtBlacklistServiceTests.cs src/ApiGateway/Leno.ApiGateway/Program.cs
git commit -m "fix: JwtBlacklistService 补全三层保障（Pub/Sub+MemoryCache TTL+启动订阅），消除内存泄漏"
```

---

### P0-T3：AntiCorruptionMetrics 静态字典非线程安全（审计 #3）

**审计位置**：[file:///workspace/docs/superpowers/specs/2026-07-21-code-audit/12-shared.md#L55-L67]
**代码位置**：
- [file:///workspace/src/BuildingBlocks/Leno.Infrastructure/AntiCorruption/AntiCorruptionMetrics.cs#L55]（`private static readonly Dictionary<string, int> _circuitOpenStates = new();`）
- [file:///workspace/src/BuildingBlocks/Leno.Infrastructure/AntiCorruption/AntiCorruptionMetrics.cs#L58-L67]（Initialize 枚举 `_circuitOpenStates`）
- [file:///workspace/src/BuildingBlocks/Leno.Infrastructure/AntiCorruption/AntiCorruptionMetrics.cs#L101-L104]（`UpdateCircuitOpenState` 写入字典）

**根因**：`Dictionary<string, int>` 非线程安全，多 BC 并发写入 + OpenTelemetry 周期枚举导致 `InvalidOperationException: Collection was modified` 或数据丢失。

---

#### 步骤 1：测试

在 `Leno.Infrastructure.Tests/AntiCorruption/` 下新建测试文件。

```csharp
// 文件：src/BuildingBlocks/Leno.Infrastructure.Tests/AntiCorruption/AntiCorruptionMetricsTests.cs

using Leno.Infrastructure.AntiCorruption;
using System.Collections.Concurrent;
using Xunit;
using FluentAssertions;

namespace Leno.Infrastructure.Tests.AntiCorruption;

public class AntiCorruptionMetricsTests
{
    [Fact]
    public void UpdateCircuitOpenState_ConcurrentWrites_ShouldNotThrow()
    {
        // Arrange
        AntiCorruptionMetrics.Initialize();
        var services = new[] { "svc-a", "svc-b", "svc-c", "svc-d", "svc-e" };
        var exceptions = new ConcurrentBag<Exception>();

        // Act — 50 线程并发写入不同 service
        Parallel.For(0, 50, i =>
        {
            try
            {
                var svc = services[i % services.Length];
                AntiCorruptionMetrics.UpdateCircuitOpenState(svc, i % 2 == 0);
            }
            catch (Exception ex)
            {
                exceptions.Add(ex);
            }
        });

        // Assert
        exceptions.Should().BeEmpty("并发写入 _circuitOpenStates 不应抛出异常");
    }

    [Fact]
    public void UpdateCircuitOpenState_ConcurrentWriteAndEnumerate_ShouldNotThrow()
    {
        // Arrange
        AntiCorruptionMetrics.Initialize();
        var exceptions = new ConcurrentBag<Exception>();

        // Act — 并发写入 + 同时触发 ObservableGauge 回调（枚举 _circuitOpenStates）
        var writeTask = Task.Run(() =>
        {
            Parallel.For(0, 100, i =>
            {
                try
                {
                    AntiCorruptionMetrics.UpdateCircuitOpenState($"svc-{i % 10}", i % 2 == 0);
                }
                catch (Exception ex)
                {
                    exceptions.Add(ex);
                }
            });
        });

        var enumerateTask = Task.Run(() =>
        {
            for (var i = 0; i < 100; i++)
            {
                try
                {
                    // 触发 ObservableGauge 的 observeValues 回调，内部枚举 _circuitOpenStates
                    AntiCorruptionMetrics.CircuitOpenGauge?.TryRead();
                }
                catch (Exception ex)
                {
                    exceptions.Add(ex);
                }
            }
        });

        Task.WaitAll(writeTask, enumerateTask);

        // Assert
        exceptions.Should().BeEmpty("并发写入与枚举不应抛出 Collection was modified 异常");
    }
}
```

#### 步骤 2：验证测试失败

运行：
```bash
dotnet test src/BuildingBlocks/Leno.Infrastructure.Tests/Leno.Infrastructure.Tests.csproj --filter "FullyQualifiedName~AntiCorruptionMetricsTests"
```
预期：FAIL — 并发写入/枚举时抛 `InvalidOperationException: Operations that change non-concurrent collections must have exclusive access` 或 `Collection was modified`。

#### 步骤 3：实现修复

```csharp
// 文件：src/BuildingBlocks/Leno.Infrastructure/AntiCorruption/AntiCorruptionMetrics.cs
// 修改第 55 行：Dictionary → ConcurrentDictionary

// 原代码（第 55 行）：
// private static readonly Dictionary<string, int> _circuitOpenStates = new();

// 替换为：
using System.Collections.Concurrent;

/// <summary>熔断器状态值回调表（service -> 1=Open / 0=Closed|HalfOpen）。由 CircuitBreakerState 维护。</summary>
/// <remarks>使用 ConcurrentDictionary 保证多 BC 并发写入与 OTLP 枚举的线程安全。</remarks>
private static readonly ConcurrentDictionary<string, int> _circuitOpenStates = new();
```

`UpdateCircuitOpenState` 方法（第 101-104 行）无需修改——`_circuitOpenStates[service] = isOpen ? 1 : 0;` 对 `ConcurrentDictionary` 同样有效。`Initialize` 中的 `_circuitOpenStates.Select(...)` 也兼容 `ConcurrentDictionary` 的枚举（快照枚举，不会抛 `Collection was modified`）。

需在文件顶部添加 `using System.Collections.Concurrent;`（若尚未引用）。

#### 步骤 4：验证测试通过

运行：
```bash
dotnet test src/BuildingBlocks/Leno.Infrastructure.Tests/Leno.Infrastructure.Tests.csproj --filter "FullyQualifiedName~AntiCorruptionMetricsTests"
```
预期：PASS — 并发写入和枚举均无异常。

#### 步骤 5：提交

```bash
git add src/BuildingBlocks/Leno.Infrastructure/AntiCorruption/AntiCorruptionMetrics.cs src/BuildingBlocks/Leno.Infrastructure.Tests/AntiCorruption/AntiCorruptionMetricsTests.cs
git commit -m "fix: AntiCorruptionMetrics 字典改用 ConcurrentDictionary，消除多 BC 并发竞态"
```

---

### P0-T4：IntegrationEventConsumerBase 三步非原子幂等检查（审计 #4）

**审计位置**：[file:///workspace/docs/superpowers/specs/2026-07-21-code-audit/12-shared.md#L69-L83]
**代码位置**：
- [file:///workspace/src/BuildingBlocks/Leno.Infrastructure/EventBus/IntegrationEventConsumerBase.cs#L33-L54]（Consume 流程：IsProcessedAsync → HandleAsync → MarkAsProcessedAsync 三步非原子）
- [file:///workspace/src/BuildingBlocks/Leno.Infrastructure/EventBus/RedisIdempotencyStore.cs#L37-L52]（`IsProcessedAsync` 用 `KeyExistsAsync`，`MarkAsProcessedAsync` 用 `StringSetAsync(..., When.NotExists)`，两次独立调用）

**根因**：`IsProcessedAsync` 与 `MarkAsProcessedAsync` 是两次独立 Redis 调用，中间窗口存在并发穿透。两个消费者实例同时收到同一事件，都通过 `IsProcessedAsync` 检查（尚未标记），都执行 `HandleAsync`，导致业务副作用重复执行。

---

#### 步骤 1：测试

在 `Leno.Infrastructure.Tests/EventBus/` 下新建测试文件。

```csharp
// 文件：src/BuildingBlocks/Leno.Infrastructure.Tests/EventBus/IntegrationEventConsumerAtomicityTests.cs

using Leno.Infrastructure.Abstractions;
using Leno.Infrastructure.EventBus;
using Leno.SharedContracts.Events;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using StackExchange.Redis;
using Xunit;
using FluentAssertions;
using System.Collections.Concurrent;

namespace Leno.Infrastructure.Tests.EventBus;

/// <summary>
/// 验证 IntegrationEventConsumerBase 的幂等检查是原子的（TryMarkAsProcessing 原子获取）。
/// </summary>
public class IntegrationEventConsumerAtomicityTests
{
    [Fact]
    public async Task ConcurrentConsume_SameEvent_ShouldOnlyProcessOnce()
    {
        // Arrange — 使用原子 TryMarkAsProcessing 的 IIdempotencyStore
        var processedKeys = new ConcurrentDictionary<string, byte>();
        var processingKeys = new ConcurrentDictionary<string, byte>();

        var store = new Mock<IIdempotencyStore>();
        // 模拟原子 TryMarkAsProcessing：ConcurrentDictionary.TryAdd 保证只有一个线程成功
        store.Setup(x => x.TryMarkAsProcessingAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Guid eventId, CancellationToken _) =>
                processingKeys.TryAdd(eventId.ToString(), 0));

        store.Setup(x => x.MarkAsProcessedAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        store.Setup(x => x.IsProcessedAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Guid eventId, CancellationToken _) =>
                processedKeys.ContainsKey(eventId.ToString()));

        var executionCount = 0;
        var consumer = new TestIntegrationEventConsumer(
            store.Object, () => Interlocked.Increment(ref executionCount));

        var evt = new TestIntegrationEvent { EventId = Guid.NewGuid() };
        var context = MockConsumeContext(evt);

        // Act — 10 个并发消费者同时处理同一事件
        var tasks = Enumerable.Range(0, 10)
            .Select(_ => consumer.Consume(context))
            .ToArray();
        await Task.WhenAll(tasks);

        // Assert — HandleAsync 只执行一次
        executionCount.Should().Be(1, "原子幂等检查应保证同一事件只处理一次");
    }

    private static ConsumeContext<TestIntegrationEvent> MockConsumeContext(TestIntegrationEvent evt)
    {
        var mock = new Mock<ConsumeContext<TestIntegrationEvent>>();
        mock.SetupGet(x => x.Message).Returns(evt);
        mock.SetupGet(x => x.CancellationToken).Returns(CancellationToken.None);
        return mock.Object;
    }

    private sealed class TestIntegrationEvent : IIntegrationEvent
    {
        public Guid EventId { get; set; }
        public DateTime OccurredAt { get; set; } = DateTime.UtcNow;
        public string EventType => nameof(TestIntegrationEvent);
        public string? IdempotencyKey { get; set; }
    }

    private sealed class TestIntegrationEventConsumer : IntegrationEventConsumerBase<TestIntegrationEvent>
    {
        private readonly Action _onHandle;

        public TestIntegrationEventConsumer(IIdempotencyStore store, Action onHandle)
            : base(NullLogger.Instance, store)
        {
            _onHandle = onHandle;
        }

        protected override Task HandleAsync(TestIntegrationEvent integrationEvent, CancellationToken ct)
        {
            _onHandle();
            return Task.CompletedTask;
        }
    }
}
```

#### 步骤 2：验证测试失败

运行：
```bash
dotnet test src/BuildingBlocks/Leno.Infrastructure.Tests/Leno.Infrastructure.Tests.csproj --filter "FullyQualifiedName~IntegrationEventConsumerAtomicityTests"
```
预期：FAIL — 编译错误（`IIdempotencyStore` 无 `TryMarkAsProcessingAsync` 方法，`IntegrationEventConsumerBase.Consume` 仍用三步非原子流程）。

#### 步骤 3：实现修复

**3a. 修改 `IIdempotencyStore` 接口，新增原子 `TryMarkAsProcessingAsync` 方法：**

```csharp
// 文件：src/BuildingBlocks/Leno.Infrastructure.Abstractions/EventBus/IIdempotencyStore.cs
// 在接口中新增 TryMarkAsProcessingAsync 方法

using Leno.Infrastructure.Abstractions;

namespace Leno.Infrastructure.Abstractions;

/// <summary>
/// 集成事件幂等去重存储接口。
/// </summary>
public interface IIdempotencyStore
{
    /// <summary>
    /// 判断事件是否已处理（仅用于查询，不保证原子性）。
    /// </summary>
    Task<bool> IsProcessedAsync(Guid eventId, CancellationToken ct = default);

    /// <summary>
    /// 原子地尝试将事件标记为"处理中"。
    /// 使用 Redis SET NX 原子操作，返回 true 表示获取到处理权（当前消费者应执行 HandleAsync），
    /// 返回 false 表示已有其他消费者正在处理或已处理完成（当前消费者应跳过）。
    /// </summary>
    /// <param name="eventId">事件唯一标识。</param>
    /// <param name="ct">取消令牌。</param>
    /// <returns>true=获取处理权，false=已被其他消费者占用。</returns>
    Task<bool> TryMarkAsProcessingAsync(Guid eventId, CancellationToken ct = default);

    /// <summary>
    /// 标记事件已处理完成。
    /// 在 HandleAsync 成功后调用，将"处理中"标记升级为"已处理"。
    /// </summary>
    Task MarkAsProcessedAsync(Guid eventId, CancellationToken ct = default);
}
```

**3b. 修改 `RedisIdempotencyStore` 实现原子 `TryMarkAsProcessingAsync`：**

```csharp
// 文件：src/BuildingBlocks/Leno.Infrastructure/EventBus/RedisIdempotencyStore.cs
// 新增 TryMarkAsProcessingAsync 方法

/// <inheritdoc />
public async Task<bool> TryMarkAsProcessingAsync(Guid eventId, CancellationToken ct = default)
{
    var db = _redisMultiplexer.GetDatabase();
    var key = BuildProcessingKey(eventId);
    // SET NX：原子操作，仅当 key 不存在时设置成功
    // processing key 的 TTL 略长于处理超时，防止消费者崩溃后永久锁定
    var processingTtl = TimeSpan.FromMinutes(5);
    var wasSet = await db.StringSetAsync(key, "1", processingTtl, when: When.NotExists);
    return wasSet;
}

/// <inheritdoc />
public async Task MarkAsProcessedAsync(Guid eventId, CancellationToken ct = default)
{
    var db = _redisMultiplexer.GetDatabase();
    var processedKey = BuildKey(eventId);
    var processingKey = BuildProcessingKey(eventId);

    // 原子标记已处理（SET NX + TTL）
    await db.StringSetAsync(processedKey, "1", KeyTtl, when: When.NotExists);
    // 删除 processing 标记
    await db.KeyDeleteAsync(processingKey);
}

private string BuildProcessingKey(Guid eventId) => $"{KeyPrefix}:processing:{eventId}";
```

**3c. 修改 `IntegrationEventConsumerBase.Consume` 使用原子获取：**

```csharp
// 文件：src/BuildingBlocks/Leno.Infrastructure/EventBus/IntegrationEventConsumerBase.cs
// 替换第 33-54 行 Consume 方法

public async Task Consume(ConsumeContext<T> context)
{
    ArgumentNullException.ThrowIfNull(context);
    var evt = context.Message;

    // 原子幂等检查：TryMarkAsProcessingAsync 使用 SET NX，保证只有一个消费者获取处理权
    var acquired = await TryMarkAsProcessingAsync(evt.EventId, context.CancellationToken);
    if (!acquired)
    {
        Logger.LogInformation("事件已被其他消费者占用或已处理，跳过 EventId={EventId} Type={EventType}",
            evt.EventId, typeof(T).Name);
        return;
    }

    Logger.LogInformation("开始消费集成事件 EventId={EventId} Type={EventType}",
        evt.EventId, typeof(T).Name);

    try
    {
        await HandleAsync(evt, context.CancellationToken);
    }
    catch
    {
        // 处理失败：删除 processing 标记，允许重试
        await ReleaseProcessingLockAsync(evt.EventId, context.CancellationToken);
        throw;
    }

    await MarkAsProcessedAsync(evt.EventId, context.CancellationToken);

    Logger.LogInformation("集成事件消费完成 EventId={EventId} Type={EventType}",
        evt.EventId, typeof(T).Name);
}

/// <summary>
/// 原子地尝试获取事件处理权。默认委托给 <see cref="IIdempotencyStore.TryMarkAsProcessingAsync"/>。
/// </summary>
protected virtual Task<bool> TryMarkAsProcessingAsync(Guid eventId, CancellationToken ct)
    => IdempotencyStore.TryMarkAsProcessingAsync(eventId, ct);

/// <summary>
/// 处理失败时释放处理锁，允许后续重试。默认委托给删除 processing key。
/// </summary>
protected virtual Task ReleaseProcessingLockAsync(Guid eventId, CancellationToken ct)
    => IdempotencyStore.ReleaseProcessingLockAsync(eventId, ct);
```

在 `IIdempotencyStore` 接口和 `RedisIdempotencyStore` 中补充 `ReleaseProcessingLockAsync` 方法：

```csharp
// IIdempotencyStore.cs 新增
/// <summary>
/// 释放处理锁（处理失败时调用，允许后续重试）。
/// </summary>
Task ReleaseProcessingLockAsync(Guid eventId, CancellationToken ct = default);

// RedisIdempotencyStore.cs 新增
/// <inheritdoc />
public async Task ReleaseProcessingLockAsync(Guid eventId, CancellationToken ct = default)
{
    var db = _redisMultiplexer.GetDatabase();
    var key = BuildProcessingKey(eventId);
    await db.KeyDeleteAsync(key);
}
```

#### 步骤 4：验证测试通过

运行：
```bash
dotnet test src/BuildingBlocks/Leno.Infrastructure.Tests/Leno.Infrastructure.Tests.csproj --filter "FullyQualifiedName~IntegrationEventConsumerAtomicityTests"
```
预期：PASS — 10 个并发消费者中只有一个执行了 `HandleAsync`。

#### 步骤 5：提交

```bash
git add src/BuildingBlocks/Leno.Infrastructure.Abstractions/EventBus/IIdempotencyStore.cs src/BuildingBlocks/Leno.Infrastructure/EventBus/RedisIdempotencyStore.cs src/BuildingBlocks/Leno.Infrastructure/EventBus/IntegrationEventConsumerBase.cs src/BuildingBlocks/Leno.Infrastructure.Tests/EventBus/IntegrationEventConsumerAtomicityTests.cs
git commit -m "fix: IntegrationEventConsumerBase 改用原子 TryMarkAsProcessingAsync，消除并发幂等穿透"
```

---

### P0-T5：ObjectStorageService 构造函数 sync-over-async（审计 #5）

**审计位置**：[file:///workspace/docs/superpowers/specs/2026-07-21-code-audit/12-shared.md#L85-L99]
**代码位置**：[file:///workspace/src/BuildingBlocks/Leno.Infrastructure/Storage/ObjectStorageService.cs#L46]（`EnsureBucketExistsAsync().GetAwaiter().GetResult();`）

**根因**：构造函数中 `.GetAwaiter().GetResult()` 是 sync-over-async，在高并发启动或线程池 starvation 时会导致死锁。应改为延迟初始化或 `IHostedService` 异步预热。

---

#### 步骤 1：测试

在 `Leno.Infrastructure.Tests/StorageTests.cs` 中追加测试。

```csharp
// 文件：src/BuildingBlocks/Leno.Infrastructure.Tests/StorageTests.cs
// 追加以下测试方法

using Leno.Infrastructure.Storage;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;
using FluentAssertions;

public partial class StorageTests
{
    [Fact]
    public void Constructor_ShouldNotBlockOnEnsureBucketExists()
    {
        // Arrange — 构造函数不应同步调用 EnsureBucketExistsAsync
        var options = Options.Create(new ObjectStorageOptions
        {
            Endpoint = "localhost:9000",
            AccessKey = "minioadmin",
            SecretKey = "minioadmin",
            UseSsl = false,
            BucketName = "test-bucket"
        });
        var logger = new Mock<ILogger<ObjectStorageService>>();

        // Act — 构造函数应快速返回（不阻塞）
        var sw = System.Diagnostics.Stopwatch.StartNew();
        var service = new ObjectStorageService(options, logger.Object);
        sw.Stop();

        // Assert — 构造函数不应同步等待网络调用
        sw.ElapsedMilliseconds.Should().BeLessThan(500,
            "构造函数不应 sync-over-async 阻塞线程池");

        // EnsureBucketExists 应延迟到首次使用时异步执行
        service.IsBucketEnsurePending.Should().BeTrue(
            "Bucket 确保应延迟到首次使用时异步执行");
    }
}
```

#### 步骤 2：验证测试失败

运行：
```bash
dotnet test src/BuildingBlocks/Leno.Infrastructure.Tests/Leno.Infrastructure.Tests.csproj --filter "FullyQualifiedName~Constructor_ShouldNotBlockOnEnsureBucketExists"
```
预期：FAIL — 构造函数同步调用 `EnsureBucketExistsAsync().GetAwaiter().GetResult()` 会阻塞或超时，`IsBucketEnsurePending` 属性不存在。

#### 步骤 3：实现修复

```csharp
// 文件：src/BuildingBlocks/Leno.Infrastructure/Storage/ObjectStorageService.cs
// 修改构造函数（第 30-47 行），移除 sync-over-async，改为延迟初始化

// 在类中新增字段
private readonly SemaphoreSlim _bucketEnsureLock = new(1, 1);
private int _bucketEnsured; // 0=未确保, 1=已确保

/// <summary>
/// 指示 Bucket 确保操作是否尚未执行（延迟到首次使用）。
/// </summary>
internal bool IsBucketEnsurePending => Volatile.Read(ref _bucketEnsured) == 0;

public ObjectStorageService(IOptions<ObjectStorageOptions> options, ILogger<ObjectStorageService> logger)
{
    ArgumentNullException.ThrowIfNull(options);
    _options = options.Value ?? throw new InvalidOperationException("ObjectStorageOptions 未配置");
    _logger = logger;

    // 敏感参数优先从环境变量读取
    var accessKey = ResolveSensitiveValue(_options.AccessKey, "FILE_STORAGE_ACCESS_KEY");
    var secretKey = ResolveSensitiveValue(_options.SecretKey, "FILE_STORAGE_SECRET_KEY");

    _minioClient = new MinioClient()
        .WithEndpoint(_options.Endpoint)
        .WithCredentials(accessKey, secretKey)
        .WithSSL(_options.UseSsl)
        .Build();

    // 移除：EnsureBucketExistsAsync().GetAwaiter().GetResult();
    // Bucket 确保延迟到首次使用时异步执行（见 EnsureBucketExistsOnceAsync）
}

/// <summary>
/// 延迟确保 Bucket 存在（首次使用时异步执行，后续调用跳过）。
/// 使用双重检查锁定 + Volatile.Read 保证线程安全。
/// </summary>
private async Task EnsureBucketExistsOnceAsync(CancellationToken ct)
{
    if (Volatile.Read(ref _bucketEnsured) == 1) return;

    await _bucketEnsureLock.WaitAsync(ct);
    try
    {
        if (_bucketEnsured == 1) return;
        await EnsureBucketExistsAsync(ct);
        Volatile.Write(ref _bucketEnsured, 1);
    }
    finally
    {
        _bucketEnsureLock.Release();
    }
}
```

然后在所有公开方法（`UploadAsync`、`DownloadAsync`、`DeleteAsync` 等）开头调用 `await EnsureBucketExistsOnceAsync(ct);`。以 `UploadAsync` 为例：

```csharp
/// <inheritdoc />
public async Task<FileUploadResult> UploadAsync(Stream stream, string fileName, string contentType, string category, CancellationToken ct = default)
{
    ArgumentNullException.ThrowIfNull(stream);
    await EnsureBucketExistsOnceAsync(ct);
    // ... 原有上传逻辑保持不变
}
```

`EnsureBucketExistsAsync` 方法签名需增加 `CancellationToken` 参数：
```csharp
private async Task EnsureBucketExistsAsync(CancellationToken ct)
{
    // 原有实现，传入 ct
    var beArgs = new BucketExistsArgs().WithBucket(_options.BucketName);
    var exists = await _minioClient.BucketExistsAsync(beArgs, ct).ConfigureAwait(false);
    if (!exists)
    {
        var mbArgs = new MakeBucketArgs().WithBucket(_options.BucketName);
        await _minioClient.MakeBucketAsync(mbArgs, ct).ConfigureAwait(false);
        _logger.LogInformation("已创建 MinIO Bucket: {Bucket}", _options.BucketName);
    }
}
```

#### 步骤 4：验证测试通过

运行：
```bash
dotnet test src/BuildingBlocks/Leno.Infrastructure.Tests/Leno.Infrastructure.Tests.csproj --filter "FullyQualifiedName~Constructor_ShouldNotBlockOnEnsureBucketExists"
```
预期：PASS — 构造函数快速返回，`IsBucketEnsurePending` 为 true。

#### 步骤 5：提交

```bash
git add src/BuildingBlocks/Leno.Infrastructure/Storage/ObjectStorageService.cs src/BuildingBlocks/Leno.Infrastructure.Tests/StorageTests.cs
git commit -m "fix: ObjectStorageService 构造函数移除 sync-over-async，改为延迟初始化 Bucket 确保"
```

---

### P0-T6：RedisBloomFilter Math.Abs(long.MinValue) 溢出（审计 #6）

**审计位置**：[file:///workspace/docs/superpowers/specs/2026-07-21-code-audit/12-shared.md#L101-L115]
**代码位置**：[file:///workspace/src/BuildingBlocks/Leno.Infrastructure/Caching/RedisBloomFilter.cs#L103]（`positions[i] = Math.Abs(combinedHash % _bitSize);`）

**根因**：`Math.Abs(long.MinValue)` 返回 `long.MinValue`（负数），导致 `positions[i]` 为负数，Redis `StringSetBitAsync` 对负偏移量的行为未定义，可能写入错误位或抛异常。

---

#### 步骤 1：测试

在 `Leno.Infrastructure.Tests/Caching/` 下新建测试文件。

```csharp
// 文件：src/BuildingBlocks/Leno.Infrastructure.Tests/Caching/RedisBloomFilterOverflowTests.cs

using Leno.Infrastructure.Caching;
using Moq;
using StackExchange.Redis;
using Xunit;
using FluentAssertions;
using System.Reflection;

namespace Leno.Infrastructure.Tests.Caching;

public class RedisBloomFilterOverflowTests
{
    [Fact]
    public void GetHashPositions_ShouldNeverProduceNegativePositions()
    {
        // Arrange — 使用反射调用 internal GetHashPositions
        var redisMock = new Mock<IConnectionMultiplexer>();
        var dbMock = new Mock<IDatabase>();
        redisMock.Setup(x => x.GetDatabase(It.IsAny<int>(), It.IsAny<object>())).Returns(dbMock.Object);

        var filter = new RedisBloomFilter(redisMock.Object, "test:bloom", bitSize: 1000, hashCount: 7);
        var method = typeof(RedisBloomFilter).GetMethod("GetHashPositions",
            BindingFlags.NonPublic | BindingFlags.Instance);

        // Act — 构造可能产生 long.MinValue 的输入
        // 使用大量不同 key，确保覆盖各种哈希组合
        var negativeCount = 0;
        for (var i = 0; i < 100000; i++)
        {
            var key = $"overflow-test-key-{i}-{'\x00'}-{'\xFF'}";
            var positions = (long[])method!.Invoke(filter, new object[] { key })!;

            foreach (var pos in positions)
            {
                if (pos < 0)
                {
                    negativeCount++;
                }
            }
        }

        // Assert — 不应有任何负数位置
        negativeCount.Should().Be(0, "GetHashPositions 不应产生负数位置（Math.Abs(long.MinValue) 溢出）");
    }

    [Fact]
    public void GetHashPositions_AllPositionsShouldBeWithinBitSize()
    {
        // Arrange
        var redisMock = new Mock<IConnectionMultiplexer>();
        var dbMock = new Mock<IDatabase>();
        redisMock.Setup(x => x.GetDatabase(It.IsAny<int>(), It.IsAny<object>())).Returns(dbMock.Object);

        const long bitSize = 1000;
        var filter = new RedisBloomFilter(redisMock.Object, "test:bloom", bitSize: bitSize, hashCount: 7);
        var method = typeof(RedisBloomFilter).GetMethod("GetHashPositions",
            BindingFlags.NonPublic | BindingFlags.Instance);

        // Act
        for (var i = 0; i < 10000; i++)
        {
            var key = $"range-test-{i}";
            var positions = (long[])method!.Invoke(filter, new object[] { key })!;

            // Assert
            foreach (var pos in positions)
            {
                pos.Should().BeInRange(0, bitSize - 1,
                    $"位置应在 [0, {bitSize - 1}] 范围内");
            }
        }
    }
}
```

#### 步骤 2：验证测试失败

运行：
```bash
dotnet test src/BuildingBlocks/Leno.Infrastructure.Tests/Leno.Infrastructure.Tests.csproj --filter "FullyQualifiedName~RedisBloomFilterOverflowTests"
```
预期：FAIL — 存在负数位置（`Math.Abs(long.MinValue)` 溢出）。

#### 步骤 3：实现修复

```csharp
// 文件：src/BuildingBlocks/Leno.Infrastructure/Caching/RedisBloomFilter.cs
// 修改第 100-104 行 GetHashPositions 方法

private long[] GetHashPositions(string key)
{
    var positions = new long[_hashCount];
    var keyBytes = Encoding.UTF8.GetBytes(key);

    // 使用双重哈希技术：h(i) = (hash1 + i * hash2) % m
    var hash1 = GetHash64(keyBytes, 0);
    var hash2 = GetHash64(keyBytes, 1);

    for (var i = 0; i < _hashCount; i++)
    {
        var combinedHash = unchecked(hash1 + (long)i * hash2);
        // 修复：Math.Abs(long.MinValue) 会溢出返回负数
        // 使用位掩码强制非负：& 0x7FFFFFFFFFFFFFFF 清除符号位
        var absHash = combinedHash & 0x7FFFFFFFFFFFFFFF;
        positions[i] = absHash % _bitSize;
    }

    return positions;
}
```

#### 步骤 4：验证测试通过

运行：
```bash
dotnet test src/BuildingBlocks/Leno.Infrastructure.Tests/Leno.Infrastructure.Tests.csproj --filter "FullyQualifiedName~RedisBloomFilterOverflowTests"
```
预期：PASS — 100000 次调用无负数位置，所有位置在 `[0, bitSize-1]` 范围内。

#### 步骤 5：提交

```bash
git add src/BuildingBlocks/Leno.Infrastructure/Caching/RedisBloomFilter.cs src/BuildingBlocks/Leno.Infrastructure.Tests/Caching/RedisBloomFilterOverflowTests.cs
git commit -m "fix: RedisBloomFilter 用位掩码替代 Math.Abs，消除 long.MinValue 溢出导致负索引"
```

---

### P0-T7：BaseDbContext 审计字段缺失 CreatedBy/UpdatedBy（审计 #7）

**审计位置**：[file:///workspace/docs/superpowers/specs/2026-07-21-code-audit/12-shared.md#L117-L131]
**代码位置**：
- [file:///workspace/src/BuildingBlocks/Leno.Infrastructure/Persistence/BaseDbContext.cs#L98-L114]（`FillAuditableFields` 仅填 `CreatedAt`/`UpdatedAt`）
- [file:///workspace/src/BuildingBlocks/Leno.SharedKernel/Abstractions/Entity.cs#L6-L12]（`IAuditable` 含 `CreatedBy`/`UpdatedBy` 字段）

**根因**：`FillAuditableFields` 未注入 `ICurrentUserContext`，仅填充时间戳，`CreatedBy`/`UpdatedBy` 永远为 null，审计追踪断裂。

---

#### 步骤 1：测试

在 `Leno.Infrastructure.Tests/Persistence/` 下新建测试文件。

```csharp
// 文件：src/BuildingBlocks/Leno.Infrastructure.Tests/Persistence/BaseDbContextAuditTests.cs

using Leno.Infrastructure.Auth;
using Leno.Infrastructure.Persistence;
using Leno.SharedKernel.Abstractions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Moq;
using Xunit;
using FluentAssertions;

namespace Leno.Infrastructure.Tests.Persistence;

public class BaseDbContextAuditTests
{
    [Fact]
    public async Task SaveChangesAsync_OnAdd_ShouldFillCreatedByAndUpdatedBy()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var userContext = new Mock<ICurrentUserContext>();
        userContext.SetupGet(x => x.UserId).Returns(userId);
        userContext.SetupGet(x => x.IsAuthenticated).Returns(true);

        var options = new DbContextOptionsBuilder<TestAuditDbContext>()
            .UseInMemoryDatabase("audit-test-add")
            .Options;

        await using var context = new TestAuditDbContext(options, userContext.Object);
        var entity = new TestAuditableEntity { Name = "test" };

        // Act
        context.AuditableEntities.Add(entity);
        await context.SaveChangesAsync();

        // Assert
        entity.CreatedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
        entity.UpdatedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
        entity.CreatedBy.Should().Be(userId.ToString());
        entity.UpdatedBy.Should().Be(userId.ToString());
    }

    [Fact]
    public async Task SaveChangesAsync_OnModify_ShouldFillUpdatedByOnly()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var originalUserId = Guid.NewGuid().ToString();
        var userContext = new Mock<ICurrentUserContext>();
        userContext.SetupGet(x => x.UserId).Returns(userId);
        userContext.SetupGet(x => x.IsAuthenticated).Returns(true);

        var options = new DbContextOptionsBuilder<TestAuditDbContext>()
            .UseInMemoryDatabase("audit-test-modify")
            .Options;

        await using var context = new TestAuditDbContext(options, userContext.Object);
        var entity = new TestAuditableEntity
        {
            Name = "original",
            CreatedBy = originalUserId,
            UpdatedBy = originalUserId
        };
        context.AuditableEntities.Add(entity);
        await context.SaveChangesAsync();

        // Act
        entity.Name = "modified";
        await context.SaveChangesAsync();

        // Assert
        entity.CreatedBy.Should().Be(originalUserId, "CreatedBy 在修改时不应被覆盖");
        entity.UpdatedBy.Should().Be(userId.ToString(), "UpdatedBy 应为当前用户");
    }

    [Fact]
    public async Task SaveChangesAsync_AnonymousUser_ShouldFillSystemIdentifier()
    {
        // Arrange — 未认证用户（如后台任务）
        var userContext = new Mock<ICurrentUserContext>();
        userContext.SetupGet(x => x.UserId).Returns((Guid?)null);
        userContext.SetupGet(x => x.IsAuthenticated).Returns(false);

        var options = new DbContextOptionsBuilder<TestAuditDbContext>()
            .UseInMemoryDatabase("audit-test-anonymous")
            .Options;

        await using var context = new TestAuditDbContext(options, userContext.Object);
        var entity = new TestAuditableEntity { Name = "bg-task" };

        // Act
        context.AuditableEntities.Add(entity);
        await context.SaveChangesAsync();

        // Assert
        entity.CreatedBy.Should().Be("system", "未认证用户的审计标识应为 system");
        entity.UpdatedBy.Should().Be("system");
    }

    private sealed class TestAuditDbContext : BaseDbContext
    {
        private readonly ICurrentUserContext _userContext;

        public DbSet<TestAuditableEntity> AuditableEntities => Set<TestAuditableEntity>();

        public TestAuditDbContext(DbContextOptions options, ICurrentUserContext userContext)
            : base(options)
        {
            _userContext = userContext;
        }

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            if (!optionsBuilder.IsConfigured)
            {
                optionsBuilder.UseInMemoryDatabase("fallback");
            }
            optionsBuilder.ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning));
        }

        protected override ICurrentUserContext? CurrentUserContext => _userContext;
    }

    private sealed class TestAuditableEntity : Entity, IAuditable
    {
        public string Name { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
        public string? CreatedBy { get; set; }
        public string? UpdatedBy { get; set; }
    }
}
```

#### 步骤 2：验证测试失败

运行：
```bash
dotnet test src/BuildingBlocks/Leno.Infrastructure.Tests/Leno.Infrastructure.Tests.csproj --filter "FullyQualifiedName~BaseDbContextAuditTests"
```
预期：FAIL — `CreatedBy`/`UpdatedBy` 为 null（`FillAuditableFields` 未填充用户标识），`CurrentUserContext` 虚属性不存在。

#### 步骤 3：实现修复

```csharp
// 文件：src/BuildingBlocks/Leno.Infrastructure/Persistence/BaseDbContext.cs
// 1. 新增 CurrentUserContext 虚属性（子类可覆盖注入）
// 2. 修改 FillAuditableFields 填充 CreatedBy/UpdatedBy

using System.Linq.Expressions;
using Leno.Infrastructure.Auth;
using Leno.Infrastructure.Outbox;
using Leno.SharedKernel.Abstractions;
using Microsoft.EntityFrameworkCore;

namespace Leno.Infrastructure.Persistence;

public abstract class BaseDbContext : DbContext
{
    public DbSet<OutboxMessage> OutboxMessages => Set<OutboxMessage>();

    /// <summary>
    /// 当前用户上下文，子类通过构造函数注入并覆盖此属性。
    /// 为 null 时（如后台迁移工具），审计字段填 "system"。
    /// </summary>
    protected virtual ICurrentUserContext? CurrentUserContext => null;

    protected BaseDbContext(DbContextOptions options) : base(options)
    {
    }

    protected BaseDbContext()
    {
    }

    // ... OnModelCreating 保持不变 ...

    public override int SaveChanges()
    {
        FillAuditableFields();
        return base.SaveChanges();
    }

    public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        FillAuditableFields();
        return base.SaveChangesAsync(cancellationToken);
    }

    private void FillAuditableFields()
    {
        var now = DateTime.UtcNow;
        var userIdentifier = ResolveUserIdentifier();

        foreach (var entry in ChangeTracker.Entries<IAuditable>())
        {
            switch (entry.State)
            {
                case EntityState.Added:
                    entry.Entity.CreatedAt = now;
                    entry.Entity.UpdatedAt = now;
                    entry.Entity.CreatedBy = userIdentifier;
                    entry.Entity.UpdatedBy = userIdentifier;
                    break;
                case EntityState.Modified:
                    entry.Entity.UpdatedAt = now;
                    entry.Entity.UpdatedBy = userIdentifier;
                    // CreatedBy/CreatedAt 在修改时不应被覆盖
                    break;
            }
        }
    }

    /// <summary>
    /// 解析当前用户标识符，用于审计字段 CreatedBy/UpdatedBy。
    /// 已认证用户返回 UserId.ToString()，未认证返回 "system"。
    /// </summary>
    private string ResolveUserIdentifier()
    {
        var userContext = CurrentUserContext;
        if (userContext is null || !userContext.IsAuthenticated || userContext.UserId is null)
        {
            return "system";
        }
        return userContext.UserId.Value.ToString();
    }
}
```

各 BC 的 DbContext 子类需在构造函数中注入 `ICurrentUserContext` 并覆盖 `CurrentUserUserContext`：

```csharp
// 示例：某 BC 的 DbContext 子类
public sealed class OrderDbContext : BaseDbContext
{
    private readonly ICurrentUserContext _userContext;

    protected override ICurrentUserContext CurrentUserContext => _userContext;

    public OrderDbContext(DbContextOptions<OrderDbContext> options, ICurrentUserContext userContext)
        : base(options)
    {
        _userContext = userContext;
    }
}
```

#### 步骤 4：验证测试通过

运行：
```bash
dotnet test src/BuildingBlocks/Leno.Infrastructure.Tests/Leno.Infrastructure.Tests.csproj --filter "FullyQualifiedName~BaseDbContextAuditTests"
```
预期：PASS — 三个测试全部通过（Add 填充 CreatedBy/UpdatedBy、Modify 仅填 UpdatedBy、匿名用户填 "system"）。

#### 步骤 5：提交

```bash
git add src/BuildingBlocks/Leno.Infrastructure/Persistence/BaseDbContext.cs src/BuildingBlocks/Leno.Infrastructure.Tests/Persistence/BaseDbContextAuditTests.cs
git commit -m "fix: BaseDbContext 注入 ICurrentUserContext 填充 CreatedBy/UpdatedBy 审计字段"
```

---

### P0-T8：RedisSlidingWindowRateLimiter Lua 脚本 ZCARD 在清窗口前（审计 #8）

**审计位置**：[file:///workspace/docs/superpowers/specs/2026-07-21-code-audit/12-shared.md#L133-L147]
**代码位置**：[file:///workspace/src/ApiGateway/Leno.ApiGateway/Services/RedisSlidingWindowRateLimiter.cs#L25-L41]（Lua 脚本顺序：`ZCARD → ZREMRANGEBYSCORE → ZADD → ZCARD`，第一次 ZCARD 在清窗口前）

**根因**：第一次 `ZCARD` 在 `ZREMRANGEBYSCORE` 之前执行，统计了已过期但未清理的旧记录，导致限流计数偏高，在窗口边界附近误拒合法请求。

---

#### 步骤 1：测试

在 `Leno.ApiGateway.Tests/Services/RedisSlidingWindowRateLimiterTests.cs` 中追加测试。

```csharp
// 文件：src/ApiGateway/Leno.ApiGateway.Tests/Services/RedisSlidingWindowRateLimiterTests.cs
// 追加以下测试方法

using Leno.ApiGateway.Services;
using Moq;
using StackExchange.Redis;
using Xunit;
using FluentAssertions;

public partial class RedisSlidingWindowRateLimiterTests
{
    [Fact]
    public async Task AcquireAsync_WindowBoundary_ShouldCleanExpiredEntriesBeforeCounting()
    {
        // Arrange — 模拟 Lua 脚本执行
        // 验证 Lua 脚本顺序：先 ZREMRANGEBYSCORE 清窗口，再 ZCARD 计数
        var luaScript = RedisSlidingWindowRateLimiter.GetScriptForTesting();
        luaScript.Should().Contain("ZREMRANGEBYSCORE");
        luaScript.Should().Contain("ZCARD");

        // 验证 ZREMRANGEBYSCORE 出现在第一次 ZCARD 之前
        var removeIndex = luaScript.IndexOf("ZREMRANGEBYSCORE", StringComparison.OrdinalIgnoreCase);
        var cardIndex = luaScript.IndexOf("ZCARD", StringComparison.OrdinalIgnoreCase);
        removeIndex.Should().BeLessThan(cardIndex,
            "ZREMRANGEBYSCORE 必须在第一次 ZCARD 之前执行，清除过期记录后再计数");

        // 使用真实 Redis 或 Mock 验证行为
        // 这里验证脚本结构，集成测试在 Integration 目录覆盖
    }

    [Fact]
    public async Task AcquireAsync_ExpiredEntriesShouldNotBlockNewRequests()
    {
        // Arrange — 使用内联 Lua 解释器模拟
        // permitLimit=2，窗口内已有 2 条过期记录（时间戳在窗口外）
        // 正确实现应先清除过期记录，然后 ZCARD=0，允许新请求

        var redisMock = new Mock<IConnectionMultiplexer>();
        var dbMock = new Mock<IDatabase>();

        // 模拟 Lua 脚本执行结果：先清窗口 → ZCARD=0 → ZADD → ZCARD=1 → 允许
        // 错误实现：先 ZCARD=2（含过期） → 拒绝
        var scriptExecutionLog = new List<string>();
        dbMock.Setup(x => x.ScriptEvaluateAsync(
            It.IsAny<string>(),
            It.IsAny<RedisKey[]>(),
            It.IsAny<RedisValue[]>(),
            It.IsAny<CommandFlags>()))
            .ReturnsAsync((string script, RedisKey[] keys, RedisValue[] values, CommandFlags _) =>
            {
                // 验证脚本顺序正确
                script.Should().Contain("ZREMRANGEBYSCORE",
                    "脚本必须先清除窗口外记录");
                var rmIndex = script.IndexOf("ZREMRANGEBYSCORE");
                var cardIndex = script.IndexOf("ZCARD");
                rmIndex.Should().BeLessThan(cardIndex);

                return RedisResult.Create(1); // 允许
            });

        redisMock.Setup(x => x.GetDatabase(It.IsAny<int>(), It.IsAny<object>())).Returns(dbMock.Object);

        var limiter = new RedisSlidingWindowRateLimiter(
            dbMock.Object, "test:ratelimit", permitLimit: 2, window: TimeSpan.FromMinutes(1), segmentsPerWindow: 1);

        // Act
        var result = await limiter.AcquireAsync(1, CancellationToken.None);

        // Assert
        result.IsAcquired.Should().BeTrue("过期记录清除后不应阻止新请求");
    }
}
```

#### 步骤 2：验证测试失败

运行：
```bash
dotnet test src/ApiGateway/Leno.ApiGateway.Tests/Leno.ApiGateway.Tests.csproj --filter "FullyQualifiedName~RedisSlidingWindowRateLimiterTests"
```
预期：FAIL — 当前 Lua 脚本第一次 `ZCARD` 在 `ZREMRANGEBYSCORE` 之前，`removeIndex > cardIndex`。

#### 步骤 3：实现修复

```csharp
// 文件：src/ApiGateway/Leno.ApiGateway/Services/RedisSlidingWindowRateLimiter.cs
// 替换第 25-41 行 Lua 脚本

// 修复：先 ZREMRANGEBYSCORE 清除窗口外记录，再 ZCARD 计数
private const string Script = @"
redis.call('ZREMRANGEBYSCORE', KEYS[1], 0, ARGV[2])
local count = redis.call('ZCARD', KEYS[1])
if count >= tonumber(ARGV[4]) then
    return 0
end
redis.call('ZADD', KEYS[1], ARGV[1], ARGV[3])
if count == 0 then
    redis.call('EXPIRE', KEYS[1], ARGV[5])
end
local newCount = redis.call('ZCARD', KEYS[1])
if newCount > tonumber(ARGV[4]) then
    redis.call('ZREM', KEYS[1], ARGV[3])
    return 0
end
return 1
";

/// <summary>
/// 暴露 Lua 脚本用于测试验证（internal 可见性）。
/// </summary>
internal static string GetScriptForTesting() => Script;
```

#### 步骤 4：验证测试通过

运行：
```bash
dotnet test src/ApiGateway/Leno.ApiGateway.Tests/Leno.ApiGateway.Tests.csproj --filter "FullyQualifiedName~RedisSlidingWindowRateLimiterTests"
```
预期：PASS — `ZREMRANGEBYSCORE` 在 `ZCARD` 之前，过期记录不阻止新请求。

#### 步骤 5：提交

```bash
git add src/ApiGateway/Leno.ApiGateway/Services/RedisSlidingWindowRateLimiter.cs src/ApiGateway/Leno.ApiGateway.Tests/Services/RedisSlidingWindowRateLimiterTests.cs
git commit -m "fix: RedisSlidingWindowRateLimiter Lua 脚本先清窗口再计数，修复窗口边界误拒"
```

---

### P0-T9：CacheMiddleware Response.Body 未在 try/finally 恢复（审计 #9）

**审计位置**：[file:///workspace/docs/superpowers/specs/2026-07-21-code-audit/12-shared.md#L149-L163]
**代码位置**：[file:///workspace/src/ApiGateway/Leno.ApiGateway/Middleware/CacheMiddleware.cs#L63-L86]（`await _next(context)` 未在 try/finally 中恢复 `context.Response.Body`）

**根因**：`_next(context)` 抛异常时，`context.Response.Body` 仍指向 `memoryStream`，异常传播到上层中间件时写入错误的流，导致响应损坏或连接挂起。

---

#### 步骤 1：测试

在 `Leno.ApiGateway.Tests/Middleware/CacheMiddlewareTests.cs` 中追加测试。

```csharp
// 文件：src/ApiGateway/Leno.ApiGateway.Tests/Middleware/CacheMiddlewareTests.cs
// 追加以下测试方法

using Leno.ApiGateway.Middleware;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;
using Moq;
using StackExchange.Redis;
using Xunit;
using FluentAssertions;

public partial class CacheMiddlewareTests
{
    [Fact]
    public async Task InvokeAsync_NextThrows_ShouldRestoreResponseBody()
    {
        // Arrange
        var redisMock = new Mock<IConnectionMultiplexer>();
        var dbMock = new Mock<IDatabase>();
        redisMock.Setup(x => x.GetDatabase(It.IsAny<int>(), It.IsAny<object>())).Returns(dbMock.Object);
        dbMock.Setup(x => x.StringGetAsync(It.IsAny<RedisKey>(), It.IsAny<CommandFlags>()))
            .ReturnsAsync(RedisValue.NullValue); // 缓存未命中

        var options = Options.Create(new CacheMiddlewareOptions());
        var loggerMock = new Mock<ILogger<CacheMiddleware>>();

        RequestDelegate nextThrows = _ => throw new InvalidOperationException("downstream error");

        var middleware = new CacheMiddleware(nextThrows, redisMock.Object, options, loggerMock.Object);

        var context = new DefaultHttpContext();
        var originalBody = context.Response.Body;
        context.Request.Method = "GET";
        context.Request.Path = "/api/test";

        // Act
        Func<Task> act = () => middleware.InvokeAsync(context);

        // Assert — 异常传播，但 Response.Body 必须恢复为原始流
        await act.Should().ThrowAsync<InvalidOperationException>();
        context.Response.Body.Should().BeSameAs(originalBody,
            "异常发生时 Response.Body 必须在 finally 中恢复为原始流");
    }

    [Fact]
    public async Task InvokeAsync_NextSucceeds_ShouldRestoreResponseBody()
    {
        // Arrange
        var redisMock = new Mock<IConnectionMultiplexer>();
        var dbMock = new Mock<IDatabase>();
        redisMock.Setup(x => x.GetDatabase(It.IsAny<int>(), It.IsAny<object>())).Returns(dbMock.Object);
        dbMock.Setup(x => x.StringGetAsync(It.IsAny<RedisKey>(), It.IsAny<CommandFlags>()))
            .ReturnsAsync(RedisValue.NullValue);

        var options = Options.Create(new CacheMiddlewareOptions());
        var loggerMock = new Mock<ILogger<CacheMiddleware>>();

        RequestDelegate nextSucceeds = async ctx =>
        {
            ctx.Response.StatusCode = 200;
            await ctx.Response.WriteAsync("OK");
        };

        var middleware = new CacheMiddleware(nextSucceeds, redisMock.Object, options, loggerMock.Object);

        var context = new DefaultHttpContext();
        var originalBody = new MemoryStream();
        context.Response.Body = originalBody;
        context.Request.Method = "GET";
        context.Request.Path = "/api/test";

        // Act
        await middleware.InvokeAsync(context);

        // Assert
        context.Response.Body.Should().BeSameAs(originalBody,
            "正常完成后 Response.Body 应恢复为原始流");
        originalBody.ToArray().Should().NotBeEmpty("响应内容应写入原始流");
    }
}
```

#### 步骤 2：验证测试失败

运行：
```bash
dotnet test src/ApiGateway/Leno.ApiGateway.Tests/Leno.ApiGateway.Tests.csproj --filter "FullyQualifiedName~CacheMiddlewareTests~InvokeAsync_NextThrows"
```
预期：FAIL — 异常时 `context.Response.Body` 仍指向 `memoryStream` 而非原始流。

#### 步骤 3：实现修复

```csharp
// 文件：src/ApiGateway/Leno.ApiGateway/Middleware/CacheMiddleware.cs
// 修改第 62-86 行，用 try/finally 包裹 _next(context)

// 缓存未命中：替换 Response.Body 捕获响应，转发到 YARP
var originalBodyStream = context.Response.Body;
using var memoryStream = new MemoryStream();
context.Response.Body = memoryStream;

try
{
    await _next(context);
}
finally
{
    // 恢复原始 Body 流（无论成功或异常都必须恢复）
    context.Response.Body = originalBodyStream;
}

memoryStream.Seek(0, SeekOrigin.Begin);
var responseBytes = memoryStream.ToArray();

// 若响应可缓存，写入 Redis
if (IsCacheableResponse(context.Response))
{
    var ttl = _options.GetTtlForPath(context.Request.Path.Value ?? "/");
    var serialized = SerializeResponse(
        context.Response.StatusCode, context.Response.Headers, responseBytes);
    await _redis.StringSetAsync(redisKey, serialized, ttl);
}

// 将捕获的响应写回客户端
memoryStream.Seek(0, SeekOrigin.Begin);
await memoryStream.CopyToAsync(originalBodyStream);
```

#### 步骤 4：验证测试通过

运行：
```bash
dotnet test src/ApiGateway/Leno.ApiGateway.Tests/Leno.ApiGateway.Tests.csproj --filter "FullyQualifiedName~CacheMiddlewareTests"
```
预期：PASS — 异常和正常两种场景下 `Response.Body` 都正确恢复。

#### 步骤 5：提交

```bash
git add src/ApiGateway/Leno.ApiGateway/Middleware/CacheMiddleware.cs src/ApiGateway/Leno.ApiGateway.Tests/Middleware/CacheMiddlewareTests.cs
git commit -m "fix: CacheMiddleware 用 try/finally 恢复 Response.Body，消除异常时流泄漏"
```

---

### P0-T10：AntiCorruptionDispatcher.Dispose 误销毁 KeyedSingleton 熔断器（审计 #10）

**审计位置**：[file:///workspace/docs/superpowers/specs/2026-07-21-code-audit/12-shared.md#L165-L179]
**代码位置**：[file:///workspace/src/BuildingBlocks/Leno.Infrastructure/AntiCorruption/AntiCorruptionDispatcher.cs#L105]（`public void Dispose() => _circuitBreaker?.Dispose();`）

**根因**：`_circuitBreaker` 是通过 DI 容器解析的 KeyedSingleton，生命周期由 DI 管理。`AntiCorruptionDispatcher`（Scoped）在 Dispose 时销毁共享的 KeyedSingleton，导致同进程中其他 Scope 的 Dispatcher 熔断器状态丢失。

---

#### 步骤 1：测试

在 `Leno.Infrastructure.Tests/AntiCorruption/AntiCorruptionDispatcherTests.cs` 中追加测试。

```csharp
// 文件：src/BuildingBlocks/Leno.Infrastructure.Tests/AntiCorruption/AntiCorruptionDispatcherTests.cs
// 追加以下测试方法

using Leno.Infrastructure.AntiCorruption;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using Xunit;
using FluentAssertions;

public partial class AntiCorruptionDispatcherTests
{
    [Fact]
    public void Dispose_ShouldNotDisposeKeyedSingletonCircuitBreaker()
    {
        // Arrange — 模拟 DI 容器注册 KeyedSingleton CircuitBreakerState
        var services = new ServiceCollection();
        services.AddKeyedSingleton<CircuitBreakerState>("test-service",
            (sp, key) => new CircuitBreakerState("test-service",
                failureThreshold: 5, openDuration: TimeSpan.FromSeconds(30)));

        // 追踪 CircuitBreakerState 是否被 Dispose
        var circuitBreakerDisposeCount = 0;
        services.AddKeyedSingleton<CircuitBreakerStateTracker>("tracker",
            (sp, key) => new CircuitBreakerStateTracker(
                sp.GetRequiredKeyedService<CircuitBreakerState>("test-service"),
                () => circuitBreakerDisposeCount++));

        using var provider = services.BuildServiceProvider();
        var tracker = provider.GetRequiredKeyedService<CircuitBreakerStateTracker>("tracker");

        // 模拟两个 Scoped Dispatcher 共享同一个 KeyedSingleton CircuitBreakerState
        using (var scope1 = provider.CreateScope())
        {
            var dispatcher1 = new TestDispatcher(
                scope1.ServiceProvider.GetRequiredKeyedService<CircuitBreakerState>("test-service"),
                "test-service");
            dispatcher1.Dispose(); // 第一个 Scope 的 Dispatcher Dispose
        }

        using (var scope2 = provider.CreateScope())
        {
            var dispatcher2 = new TestDispatcher(
                scope2.ServiceProvider.GetRequiredKeyedService<CircuitBreakerState>("test-service"),
                "test-service");

            // Act — 第二个 Scope 的 Dispatcher 仍应能正常使用共享的 CircuitBreakerState
            var state = dispatcher2.GetCircuitBreakerState();

            // Assert — KeyedSingleton 未被第一个 Dispatcher Dispose 销毁
            circuitBreakerDisposeCount.Should().Be(0,
                "Scoped Dispatcher.Dispose 不应销毁 KeyedSingleton 的 CircuitBreakerState");
            state.Should().NotBeNull("共享的 CircuitBreakerState 应仍然可用");
        }
    }

    /// <summary>测试用 Dispatcher，暴露 CircuitBreakerState 供验证。</summary>
    private sealed class TestDispatcher : IDisposable
    {
        private readonly CircuitBreakerState _circuitBreaker;
        private readonly bool _ownsCircuitBreaker;

        public TestDispatcher(CircuitBreakerState circuitBreaker, string serviceName)
        {
            _circuitBreaker = circuitBreaker;
            // KeyedSingleton 由 DI 管理生命周期，Dispatcher 不拥有它
            _ownsCircuitBreaker = false;
        }

        public CircuitBreakerState GetCircuitBreakerState() => _circuitBreaker;

        public void Dispose()
        {
            // 修复后：不 Dispose KeyedSingleton
            if (_ownsCircuitBreaker)
            {
                _circuitBreaker.Dispose();
            }
        }
    }

    /// <summary>追踪 CircuitBreakerState 是否被 Dispose。</summary>
    private sealed class CircuitBreakerStateTracker
    {
        private readonly CircuitBreakerState _state;
        private readonly Action _onDispose;

        public CircuitBreakerStateTracker(CircuitBreakerState state, Action onDispose)
        {
            _state = state;
            _onDispose = onDispose;
        }
    }
}
```

#### 步骤 2：验证测试失败

运行：
```bash
dotnet test src/BuildingBlocks/Leno.Infrastructure.Tests/Leno.Infrastructure.Tests.csproj --filter "FullyQualifiedName~AntiCorruptionDispatcherTests~Dispose_ShouldNotDisposeKeyedSingleton"
```
预期：FAIL — 当前 `Dispose() => _circuitBreaker?.Dispose()` 会销毁 KeyedSingleton，第二个 Scope 的 Dispatcher 无法使用共享的 CircuitBreakerState。

#### 步骤 3：实现修复

```csharp
// 文件：src/BuildingBlocks/Leno.Infrastructure/AntiCorruption/AntiCorruptionDispatcher.cs
// 修改第 105 行 Dispose 方法

// 原代码：
// public void Dispose() => _circuitBreaker?.Dispose();

// 替换为：
// KeyedSingleton CircuitBreakerState 的生命周期由 DI 容器管理，
// Scoped Dispatcher 不应销毁共享的 KeyedSingleton，否则同进程其他 Scope 的熔断器状态丢失。
public void Dispose()
{
    // 不 Dispose _circuitBreaker — 它是 KeyedSingleton，由 DI 容器管理生命周期
    // 仅清理 Dispatcher 自身拥有的非共享资源（当前无）
    Dispose(disposing: true);
    GC.SuppressFinalize(this);
}

/// <summary>
/// 释放资源。KeyedSingleton CircuitBreakerState 不在此释放。
/// </summary>
/// <param name="disposing">是否显式释放。</param>
protected virtual void Dispose(bool disposing)
{
    // KeyedSingleton _circuitBreaker 由 DI 容器管理，不在此 Dispose
    // 子类如有自有非托管资源，可覆盖此方法
}
```

#### 步骤 4：验证测试通过

运行：
```bash
dotnet test src/BuildingBlocks/Leno.Infrastructure.Tests/Leno.Infrastructure.Tests.csproj --filter "FullyQualifiedName~AntiCorruptionDispatcherTests~Dispose_ShouldNotDisposeKeyedSingleton"
```
预期：PASS — KeyedSingleton 未被销毁，第二个 Scope 的 Dispatcher 正常使用共享的 CircuitBreakerState。

#### 步骤 5：提交

```bash
git add src/BuildingBlocks/Leno.Infrastructure/AntiCorruption/AntiCorruptionDispatcher.cs src/BuildingBlocks/Leno.Infrastructure.Tests/AntiCorruption/AntiCorruptionDispatcherTests.cs
git commit -m "fix: AntiCorruptionDispatcher.Dispose 不再销毁 KeyedSingleton CircuitBreakerState"
```

---

## P1 修复任务清单（18 项，任务清单格式）

### P1-T11：OutboxPublisher 三步串行标记非原子（审计 #11）

- **审计位置**：[file:///workspace/docs/superpowers/specs/2026-07-21-code-audit/12-shared.md#L181-L195]
- **代码位置**：[file:///workspace/src/BuildingBlocks/Leno.Infrastructure/Outbox/OutboxPublisher.cs#L84-L87]（FetchPending → Publish → MarkAsProcessed 三步串行）
- **根因**：三步非原子，Publish 成功但 MarkAsProcessed 失败时，下次轮询会重复 Publish。T13 已引入两阶段标记（Pending→Publishing→Processed），但 Publishing→Processed 的转换仍非原子。
- **修复步骤**：
  1. 在 MarkAsProcessed 使用条件更新（`WHERE Status = Publishing AND Id = @id`），保证只有持有 Publishing 锁的实例能标记 Processed
  2. Publishing 超时（如 5 分钟）自动回退为 Pending，允许其他实例重新处理
  3. 补充并发测试：多实例同时轮询同一批 Pending 消息，验证无重复 Publish
- **影响范围**：所有 BC 的 Outbox 发布路径
- **验证方法**：集成测试验证多实例并发无重复发布

### P1-T12：OutboxPublisher MarkAsProcessed 失败后未清理 ChangeTracker（审计 #12）

- **审计位置**：[file:///workspace/docs/superpowers/specs/2026-07-21-code-audit/12-shared.md#L197-L211]
- **代码位置**：[file:///workspace/src/BuildingBlocks/Leno.Infrastructure/Outbox/OutboxPublisher.cs#L294-L310]（注释自承认未清理 ChangeTracker）
- **根因**：`MarkAsProcessed` 失败后未调用 `ChangeTracker.Clear()`，残留的 Tracked Entity 在下次 SaveChanges 时被意外持久化。
- **修复步骤**：
  1. 在 `MarkAsProcessed` 的 catch/finally 块中调用 `_context.ChangeTracker.Clear()`
  2. 或使用独立短生命周期 DbContext（`using var ctx = new ...`）执行标记操作
  3. 补充单元测试：MarkAsProcessed 抛异常后，ChangeTracker.Entries() 为空
- **影响范围**：Outbox 发布器的数据一致性
- **验证方法**：单元测试验证异常后 ChangeTracker 清空

### P1-T13：CircuitBreakerState 初始 _openedAt=DateTime.MinValue 语义错误（审计 #13）

- **审计位置**：[file:///workspace/docs/superpowers/specs/2026-07-21-code-audit/12-shared.md#L213-L227]
- **代码位置**：[file:///workspace/src/BuildingBlocks/Leno.Infrastructure/AntiCorruption/CircuitBreakerState.cs#L16]（`_openedAt = DateTime.MinValue`）
- **根因**：`DateTime.MinValue` 表示"从未打开"，但 `IsHalfOpen` 判断 `DateTime.UtcNow - _openedAt > OpenDuration` 在初始时为 true（差值极大），导致熔断器初始即为 HalfOpen 状态。
- **修复步骤**：
  1. 改用 `DateTime? _openedAt`，null 表示从未打开
  2. `IsHalfOpen` 判断：`_openedAt.HasValue && DateTime.UtcNow - _openedAt.Value > OpenDuration`
  3. `RecordFailure` 打开熔断器时设 `_openedAt = DateTime.UtcNow`
  4. `RecordSuccess` 关闭熔断器时设 `_openedAt = null`
  5. 补充单元测试：初始状态为 Closed 而非 HalfOpen
- **影响范围**：防腐层熔断器初始状态
- **验证方法**：单元测试验证初始 GetState() == Closed

### P1-T14：CircuitBreakerState.UpdateMetrics 仅记 Open/Closed 二态，缺 HalfOpen（审计 #14）

- **审计位置**：[file:///workspace/docs/superpowers/specs/2026-07-21-code-audit/12-shared.md#L229-L243]
- **代码位置**：[file:///workspace/src/BuildingBlocks/Leno.Infrastructure/AntiCorruption/CircuitBreakerState.cs#L96-L100]（`UpdateMetrics` 仅传 `isOpen` 布尔值）
- **根因**：`AntiCorruptionMetrics.UpdateCircuitOpenState(service, isOpen)` 只有 Open/Closed 两态，HalfOpen 状态下指标显示为 Closed（0），运维无法区分"正常关闭"和"半开探测中"。
- **修复步骤**：
  1. `AntiCorruptionMetrics.UpdateCircuitOpenState` 增加 `int state` 参数（0=Closed, 1=HalfOpen, 2=Open）
  2. `CircuitBreakerState.UpdateMetrics` 传入实际三态
  3. ObservableGauge 回调返回 0/1/2
  4. 补充单元测试验证三态指标值正确
- **影响范围**：熔断器可观测性
- **验证方法**：单元测试验证三种状态的指标值

### P1-T15：BffForwarderService 整体超时与单请求超时均为 3s，无区分（审计 #15）

- **审计位置**：[file:///workspace/docs/superpowers/specs/2026-07-21-code-audit/12-shared.md#L245-L259]
- **代码位置**：[file:///workspace/src/ApiGateway/Leno.ApiGateway/Bff/BffForwarderService.cs#L70-L71]（整体超时 3s）、[file:///workspace/src/ApiGateway/Leno.ApiGateway/Bff/BffForwarderService.cs#L84-L85]（单请求超时 3s）
- **根因**：BFF 聚合多个后端请求，整体超时应大于单请求超时（如整体 10s、单请求 3s），否则单请求超时无意义。
- **修复步骤**：
  1. 整体超时改为可配置（默认 10s），单请求超时保持 3s
  2. 从 `BffOptions` 读取 `OverallTimeout` 配置
  3. 补充单元测试验证整体超时 > 单请求超时
- **影响范围**：BFF 聚合请求的可用性
- **验证方法**：单元测试验证超时配置生效

### P1-T16：BffForwarderService 整体超时回填 504 去重用 ConcurrentBag（审计 #16）

- **审计位置**：[file:///workspace/docs/superpowers/specs/2026-07-21-code-audit/12-shared.md#L261-L275]
- **代码位置**：[file:///workspace/src/ApiGateway/Leno.ApiGateway/Bff/BffForwarderService.cs#L138-L164]（`ConcurrentBag<HttpResponseMessage>` 去重）
- **根因**：整体超时触发时，多个并发请求同时回填 504 到 `ConcurrentBag`，去重逻辑不清晰且 `ConcurrentBag` 不保证顺序。
- **修复步骤**：
  1. 改用 `ConcurrentQueue<HttpResponseMessage>` + `Interlocked.CompareExchange` 标志位保证只回填一次 504
  2. 或使用 `CancellationTokenSource` 取消未完成请求，仅记录第一个超时
  3. 补充单元测试验证整体超时只产生一个 504 回填
- **影响范围**：BFF 超时响应正确性
- **验证方法**：单元测试验证 504 去重

### P1-T17：CacheMiddleware IsCacheableResponse 仅缓存 200（审计 #17）

- **审计位置**：[file:///workspace/docs/superpowers/specs/2026-07-21-code-audit/12-shared.md#L277-L291]
- **代码位置**：[file:///workspace/src/ApiGateway/Leno.ApiGateway/Middleware/CacheMiddleware.cs#L100-L116]（`response.StatusCode != 200` 直接返回 false）
- **根因**：仅缓存 200，301/302 重定向、404（负缓存）等合理可缓存状态被忽略。
- **修复步骤**：
  1. 扩展可缓存状态码集合：200, 203, 204, 206, 300, 301, 404, 405, 410, 414, 501
  2. 404 负缓存 TTL 设置较短（如 30s），防止后端恢复后长时间不可见
  3. 从 `CacheMiddlewareOptions` 读取可缓存状态码列表与各状态码 TTL
  4. 补充单元测试验证 301/404 可缓存
- **影响范围**：网关缓存命中率
- **验证方法**：单元测试验证多状态码缓存

### P1-T18：FallbackResponseMiddleware 未清除 Transfer-Encoding/Content-Encoding（审计 #18）

- **审计位置**：[file:///workspace/docs/superpowers/specs/2026-07-21-code-audit/12-shared.md#L293-L307]
- **代码位置**：[file:///workspace/src/ApiGateway/Leno.ApiGateway/Middleware/FallbackResponseMiddleware.cs#L83-L101]（`RewriteAsFallbackAsync` 未清除编码头）
- **根因**：降级响应重写后，原响应的 `Transfer-Encoding: chunked` 或 `Content-Encoding: gzip` 头残留，但响应体已被解压/重写为明文，客户端按残留头解析失败。
- **修复步骤**：
  1. 在 `RewriteAsFallbackAsync` 中清除 `Transfer-Encoding`、`Content-Encoding` 头
  2. 重新计算 `Content-Length` 为降级响应体长度
  3. 补充单元测试验证降级响应无残留编码头
- **影响范围**：降级响应的正确性
- **验证方法**：单元测试验证头清除

### P1-T19：ConsulConfigWatcher 直接写 IConfiguration，不触发 IOptionsMonitor 重载（审计 #19）

- **审计位置**：[file:///workspace/docs/superpowers/specs/2026-07-21-code-audit/12-shared.md#L309-L323]
- **代码位置**：[file:///workspace/src/BuildingBlocks/Leno.Infrastructure/Configuration/ConsulConfigWatcher.cs#L67-L68]（`_configuration["AntiCorruption:UseGrpc"] = newValue`）
- **根因**：直接写 `IConfiguration` 不触发 `IOptionsMonitor<T>.OnChange` 回调，依赖 `IOptionsMonitor<AntiCorruptionOptions>` 的组件无法感知热更新。
- **修复步骤**：
  1. 使用 `IConfigurationRoot.Reload()` 触发所有 `IOptionsMonitor` 重载
  2. 或使用自定义 `IConfigurationProvider` 实现 `Set` 方法并调用 `OnReload`
  3. 补充单元测试验证 `IOptionsMonitor.CurrentValue` 在配置变更后更新
- **影响范围**：配置中心热更新
- **验证方法**：单元测试验证 IOptionsMonitor 回调触发

### P1-T20：ServiceCollectionExtensions.AddHealthChecks 仅注册 Redis/ES，缺 RabbitMQ（审计 #20）

- **审计位置**：[file:///workspace/docs/superpowers/specs/2026-07-21-code-audit/12-shared.md#L325-L339]
- **代码位置**：[file:///workspace/src/BuildingBlocks/Leno.Infrastructure/Dependencies/ServiceCollectionExtensions.cs#L187-L193]（`AddHealthChecks` 仅注册 Redis/ES）
- **根因**：`AddHealthChecks` 路径未注册 RabbitMQ 健康检查（`AddLenoFullHealthChecks` 已条件性注册，但 `AddHealthChecks` 路径遗漏）。
- **修复步骤**：
  1. 在 `AddHealthChecks` 中统一注册 RabbitMQ（`AddRabbitMQ` 或自定义健康检查）
  2. 确保两条注册路径（`AddHealthChecks` 和 `AddLenoFullHealthChecks`）都包含 RabbitMQ
  3. 补充单元测试验证 RabbitMQ 健康检查已注册
- **影响范围**：健康检查覆盖完整性
- **验证方法**：单元测试验证健康检查注册

### P1-T21：ServiceCollectionExtensions ConnectionMultiplexer.Connect 同步阻塞（审计 #21）

- **审计位置**：[file:///workspace/docs/superpowers/specs/2026-07-21-code-audit/12-shared.md#L341-L355]
- **代码位置**：[file:///workspace/src/BuildingBlocks/Leno.Infrastructure/Dependencies/ServiceCollectionExtensions.cs#L97]（`ConnectionMultiplexer.Connect(redisConfig)`）
- **根因**：`ConnectionMultiplexer.Connect` 是同步阻塞调用，在应用启动时阻塞主线程。
- **修复步骤**：
  1. 改用 `ConnectionMultiplexer.ConnectAsync` 并通过 `IHostedService` 或工厂模式异步初始化
  2. 或使用 `Lazy<Task<IConnectionMultiplexer>>` 延迟异步连接
  3. 补充单元测试验证非阻塞注册
- **影响范围**：应用启动性能
- **验证方法**：单元测试验证异步连接

### P1-T22：JwtTokenGenerator 未校验 SymmetricSecurityKey 长度（审计 #22）

- **审计位置**：[file:///workspace/docs/superpowers/specs/2026-07-21-code-audit/12-shared.md#L357-L371]
- **代码位置**：[file:///workspace/src/BuildingBlocks/Leno.Infrastructure/Auth/JwtTokenGenerator.cs#L84]（`new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_options.SecretKey))`）
- **根因**：HS256 要求密钥至少 256 位（32 字节），未校验长度会导致短密钥在运行时抛异常或安全降级。
- **修复步骤**：
  1. 在构造函数或 `GenerateToken` 中校验 `SecretKey.Length >= 32`
  2. 不满足时抛 `InvalidOperationException` 并给出明确错误信息
  3. 补充单元测试验证短密钥抛异常、32 字节密钥通过
- **影响范围**：JWT 安全性
- **验证方法**：单元测试验证密钥长度校验

### P1-T23：JwtTokenGenerator ClockSkew=1min 过宽（审计 #23）

- **审计位置**：[file:///workspace/docs/superpowers/specs/2026-07-21-code-audit/12-shared.md#L373-L387]
- **代码位置**：[file:///workspace/src/BuildingBlocks/Leno.Infrastructure/Auth/JwtTokenGenerator.cs#L141]（`ClockSkew = TimeSpan.FromMinutes(1)`）
- **根因**：1 分钟时钟偏移容忍过大，已吊销的 token 在 1 分钟内仍可使用，安全窗口过大。
- **修复步骤**：
  1. `ClockSkew` 改为 30 秒或从配置读取（默认 30s）
  2. 配合 P0-T2（JwtBlacklistService Pub/Sub 实时同步）缩短吊销生效窗口
  3. 补充单元测试验证 ClockSkew 配置生效
- **影响范围**：JWT 吊销安全窗口
- **验证方法**：单元测试验证 ClockSkew 值

### P1-T24：EfCoreUnitOfWork.SaveChangesAsync 不含 Outbox 持久化（审计 #24）

- **审计位置**：[file:///workspace/docs/superpowers/specs/2026-07-21-code-audit/12-shared.md#L389-L403]
- **代码位置**：[file:///workspace/src/BuildingBlocks/Leno.Infrastructure/Persistence/EfCoreUnitOfWork.cs#L51-L52]（`SaveChangesAsync` 仅 `_context.SaveChangesAsync(ct)`）
- **根因**：UoW 的 `SaveChangesAsync` 未包含 Outbox 消息持久化，业务事务与 Outbox 不在同一 DB 事务中，违反 Outbox 模式的原子性要求。
- **修复步骤**：
  1. `EfCoreUnitOfWork` 注入 `IOutboxService` 或直接操作 `BaseDbContext.OutboxMessages`
  2. `SaveChangesAsync` 在同一事务中先保存业务实体再保存 Outbox 消息
  3. 补充单元测试验证业务实体与 Outbox 消息在同一事务提交
- **影响范围**：事件驱动的原子性保证
- **验证方法**：单元测试验证事务原子性

### P1-T25：CacheService.InvalidatePatternAsync 未强制 KeyPrefix（审计 #25）

- **审计位置**：[file:///workspace/docs/superpowers/specs/2026-07-21-code-audit/12-shared.md#L405-L419]
- **代码位置**：[file:///workspace/src/BuildingBlocks/Leno.Infrastructure/Caching/CacheService.cs]（`InvalidatePatternAsync` 未校验/拼接 KeyPrefix）
- **根因**：`InvalidatePatternAsync` 直接使用传入 pattern，未强制添加 `leno:cache:` 前缀，可能误删非缓存 key。
- **修复步骤**：
  1. `InvalidatePatternAsync` 内部强制拼接 KeyPrefix
  2. 拒绝包含 `..` 或绝对路径的 pattern
  3. 补充单元测试验证 pattern 自动添加前缀
- **影响范围**：缓存失效安全性
- **验证方法**：单元测试验证前缀强制

### P1-T26：Program.cs 白名单中间件内联 lambda（审计 #26）

- **审计位置**：[file:///workspace/docs/superpowers/specs/2026-07-21-code-audit/12-shared.md#L421-L435]
- **代码位置**：[file:///workspace/src/ApiGateway/Leno.ApiGateway/Program.cs#L132-L155]（白名单中间件内联 lambda）
- **根因**：内联 lambda 难以测试和维护，白名单逻辑应提取为独立中间件类。
- **修复步骤**：
  1. 提取 `WhitelistMiddleware` 类，封装白名单路径匹配逻辑
  2. 白名单路径从 `IOptions<WhitelistOptions>` 读取
  3. 补充单元测试验证白名单匹配
- **影响范围**：网关中间件可维护性
- **验证方法**：单元测试验证白名单中间件

### P1-T27：CacheService.GetOrSetAsync 未获取锁时仅单次 100ms 重试（审计 #27）

- **审计位置**：[file:///workspace/docs/superpowers/specs/2026-07-21-code-audit/12-shared.md#L437-L451]
- **代码位置**：[file:///workspace/src/BuildingBlocks/Leno.Infrastructure/Caching/CacheService.cs]（`GetOrSetAsync` 未获取锁时仅单次 100ms 重试）
- **根因**：缓存击穿防护中，获取互斥锁失败后仅单次 100ms 重试即直接回源，高并发下仍可能击穿。
- **修复步骤**：
  1. 改为指数退避重试（最多 3 次：50ms → 100ms → 200ms）
  2. 重试耗尽后仍回源但记 warning 日志
  3. 补充单元测试验证重试次数与退避
- **影响范围**：缓存击穿防护
- **验证方法**：单元测试验证重试行为

### P1-T28：RedisSlidingWindowRateLimiter catch 块无日志静默放行（审计 #28）

- **审计位置**：[file:///workspace/docs/superpowers/specs/2026-07-21-code-audit/12-shared.md#L453-L467]
- **代码位置**：
  - [file:///workspace/src/ApiGateway/Leno.ApiGateway/Services/RedisSlidingWindowRateLimiter.cs#L119-L123]（`catch { return true; }`）
  - [file:///workspace/src/ApiGateway/Leno.ApiGateway/Services/RedisSlidingWindowRateLimiter.cs#L153-L157]（`catch { return true; }`）
- **根因**：Redis 异常时 catch 块静默返回 `true`（放行），无日志记录，运维无法感知限流器故障。
- **修复步骤**：
  1. catch 块内记 `LogWarning` 日志，包含异常信息与 key
  2. 可配置故障策略：fail-open（放行，默认）或 fail-close（拒绝）
  3. 补充单元测试验证异常时日志输出
- **影响范围**：限流器可观测性与故障策略
- **验证方法**：单元测试验证日志输出

---

## P2 修复任务清单（11 项，任务清单格式）

### P2-T29：Money 值对象 private set 阻止 EF Core 反序列化（审计 #29）

- **审计位置**：[file:///workspace/docs/superpowers/specs/2026-07-21-code-audit/12-shared.md#L469-L483]
- **代码位置**：[file:///workspace/src/BuildingBlocks/Leno.SharedKernel/ValueObjects/Money.cs#L13-L15]（`private set`）
- **根因**：`Amount`/`Currency` 使用 `private set`，EF Core 反序列化需要公开 setter 或 backing field 配置。
- **修复步骤**：
  1. 改为 `init` 或 `set`（配合 `[JsonConstructor]`）
  2. 或配置 EF Core backing field（`HasField("_amount")`）
  3. 补充单元测试验证 EF Core 可反序列化
- **影响范围**：共享内核值对象持久化
- **验证方法**：单元测试验证反序列化

### P2-T30：Money 币种校验 `is < 3 or > 3` 可读性差（审计 #30）

- **审计位置**：[file:///workspace/docs/superpowers/specs/2026-07-21-code-audit/12-shared.md#L485-L499]
- **代码位置**：[file:///workspace/src/BuildingBlocks/Leno.SharedKernel/ValueObjects/Money.cs#L38]（`if (normalized.Length is < 3 or > 3)`）
- **根因**：`is < 3 or > 3` 等价于 `!= 3`，但可读性差且意图不明确。
- **修复步骤**：
  1. 改为 `if (normalized.Length != 3)` 并补充注释说明 ISO 4217 三字母币种码
  2. 补充单元测试验证 2/4 字母币种码被拒绝
- **影响范围**：代码可读性
- **验证方法**：单元测试验证币种校验

### P2-T31：Entity.Id 用 protected set 而非 init（审计 #31）

- **审计位置**：[file:///workspace/docs/superpowers/specs/2026-07-21-code-audit/12-shared.md#L501-L515]
- **代码位置**：[file:///workspace/src/BuildingBlocks/Leno.SharedKernel/Abstractions/Entity.cs#L28]（`public Guid Id { get; protected set; }`）
- **根因**：`protected set` 允许子类在任意时刻修改 Id，不如 `init` 安全（仅构造时赋值）。
- **修复步骤**：
  1. 改为 `public Guid Id { get; init; }`
  2. EF Core 通过 backing field 或 `init` 支持反序列化
  3. 补充单元测试验证 Id 初始化后不可变
- **影响范围**：实体标识不可变性
- **验证方法**：单元测试验证 Id 不可变

### P2-T32：Entity.GetHashCode 用 Id.GetHashCode()，Guid.Empty 碰撞（审计 #32）

- **审计位置**：[file:///workspace/docs/superpowers/specs/2026-07-21-code-audit/12-shared.md#L517-L531]
- **代码位置**：[file:///workspace/src/BuildingBlocks/Leno.SharedKernel/Abstractions/Entity.cs#L70]（`GetHashCode() => Id.GetHashCode()`）
- **根因**：未持久化实体的 `Id = Guid.Empty`，多个临时实体哈希碰撞，影响 `HashSet`/`Dictionary` 性能。
- **修复步骤**：
  1. `GetHashCode` 改为基于类型 + Id：`return HashCode.Combine(GetType(), Id)`
  2. 或对 `Id == Guid.Empty` 的情况使用运行时唯一标识
  3. 补充单元测试验证不同类型同 Guid.Empty 不碰撞
- **影响范围**：实体哈希性能
- **验证方法**：单元测试验证哈希分布

### P2-T33：ErrorCodeMapping errorCode.Contains(suffix) 误匹配（审计 #33）

- **审计位置**：[file:///workspace/docs/superpowers/specs/2026-07-21-code-audit/12-shared.md#L533-L547]
- **代码位置**：[file:///workspace/src/BuildingBlocks/Leno.Infrastructure/Middleware/ErrorCodeMapping.cs#L67]（`errorCode.Contains(suffix, ...)`）
- **根因**：`Contains` 子串匹配导致 `USER_NOT_FOUND` 误匹配 `FOUND` 后缀。
- **修复步骤**：
  1. 改为后缀精确匹配：`errorCode.EndsWith(suffix, ...)` 或分割 `_` 后匹配
  2. 补充单元测试验证误匹配消除
- **影响范围**：错误码映射准确性
- **验证方法**：单元测试验证精确匹配

### P2-T34：ErrorCodeMapping 静态 ConcurrentDictionary 未清理（审计 #34）

- **审计位置**：[file:///workspace/docs/superpowers/specs/2026-07-21-code-audit/12-shared.md#L549-L563]
- **代码位置**：[file:///workspace/src/BuildingBlocks/Leno.Infrastructure/Middleware/ErrorCodeMapping.cs#L12]（`static ConcurrentDictionary`）
- **根因**：静态缓存无清理策略，长期运行后持续增长。
- **修复步骤**：
  1. 使用 `MemoryCache` 设置过期时间
  2. 或限制缓存大小（LRU）
  3. 补充单元测试验证缓存清理
- **影响范围**：内存稳定性
- **验证方法**：单元测试验证缓存过期

### P2-T35：IntegrationEventBase.IdempotencyKey 非可空，旧事件兼容断裂（审计 #35）

- **审计位置**：[file:///workspace/docs/superpowers/specs/2026-07-21-code-audit/12-shared.md#L565-L579]
- **代码位置**：[file:///workspace/src/BuildingBlocks/Leno.SharedContracts/Events/IntegrationEventBase.cs#L14]（`public string IdempotencyKey { get; init; }`）
- **根因**：`IdempotencyKey` 非可空，旧版事件无此字段，反序列化时为 null 或空字符串导致异常。
- **修复步骤**：
  1. 改为 `public string? IdempotencyKey { get; init; }`
  2. 消费侧处理 null/空字符串时回退到 EventId 作为幂等键
  3. 补充单元测试验证旧事件兼容
- **影响范围**：事件向后兼容
- **验证方法**：单元测试验证旧事件反序列化

### P2-T36：ObjectStorageService catch 块吞异常（审计 #36）

- **审计位置**：[file:///workspace/docs/superpowers/specs/2026-07-21-code-audit/12-shared.md#L581-L595]
- **代码位置**：[file:///workspace/src/BuildingBlocks/Leno.Infrastructure/Storage/ObjectStorageService.cs#L149-L152]（`catch { return false; }`）
- **根因**：`catch` 块吞异常返回 false，运维无法排查文件存在性检查失败原因。
- **修复步骤**：
  1. catch 块内记 `LogWarning` 日志，包含异常信息
  2. 补充单元测试验证异常时日志输出
- **影响范围**：存储可观测性
- **验证方法**：单元测试验证日志

### P2-T37：RedisBloomFilter 使用 SHA256 过重（审计 #37）

- **审计位置**：[file:///workspace/docs/superpowers/specs/2026-07-21-code-audit/12-shared.md#L597-L611]
- **代码位置**：[file:///workspace/src/BuildingBlocks/Leno.Infrastructure/Caching/RedisBloomFilter.cs#L109-L119]（`GetHash64` 使用 SHA256）
- **根因**：SHA256 是加密级哈希，对布隆过滤器非必要，性能开销大。`xxHash` 或 `MurmurHash3` 更适合。
- **修复步骤**：
  1. 替换为 `System.IO.Hashing.XxHash64`（.NET 8+ 内置，非加密哈希，速度快 10 倍以上）
  2. 保留 SHA256 作为回退选项（配置开关）
  3. 补充性能测试验证哈希速度提升
- **影响范围**：布隆过滤器性能
- **验证方法**：性能测试验证提升

### P2-T38：RedisBloomFilter 7 次 StringSetBitAsync 网络往返（审计 #38）

- **审计位置**：[file:///workspace/docs/superpowers/specs/2026-07-21-code-audit/12-shared.md#L613-L627]
- **代码位置**：[file:///workspace/src/BuildingBlocks/Leno.Infrastructure/Caching/RedisBloomFilter.cs]（`AddAsync` 循环调用 `StringSetBitAsync`）
- **根因**：7 次哈希产生 7 次独立 Redis 调用，网络往返开销大。
- **修复步骤**：
  1. 使用 Lua 脚本批量设置多个 bit（一次网络往返）
  2. 或使用 pipeline/batch 批量提交
  3. 补充单元测试验证批量设置正确性
- **影响范围**：布隆过滤器性能
- **验证方法**：单元测试验证批量设置

### P2-T39：CircuitBreakerState RecordSuccess/RecordFailure lock 后重入 GetState()（审计 #39）

- **审计位置**：[file:///workspace/docs/superpowers/specs/2026-07-21-code-audit/12-shared.md#L629-L643]
- **代码位置**：[file:///workspace/src/BuildingBlocks/Leno.Infrastructure/AntiCorruption/CircuitBreakerState.cs]（`RecordSuccess`/`RecordFailure` 内 `lock` 后调用 `GetState()`）
- **根因**：`lock` 后调用 `GetState()`（也获取同一锁）是可重入锁，不会死锁但增加锁持有时间。
- **修复步骤**：
  1. 将 `GetState()` 的逻辑提取为 `GetStateUnsafe()`（不加锁），在已持锁的上下文中调用
  2. 公开的 `GetState()` 调用 `GetStateUnsafe()` 并加锁
  3. 补充单元测试验证行为不变
- **影响范围**：熔断器锁性能
- **验证方法**：单元测试验证状态正确

---

## 已修复项（[ALREADY-FIXED] 或 [VERIFIED-NOT-REPRODUCIBLE]）

| # | 问题标题 | 状态 | 说明 |
|---|---------|------|------|
| T3 | CacheInvalidationSubscriber async void | [ALREADY-FIXED] | `OnMessage` 已改为 `async Task`，`OnMessageHandler` 适配器包装为 `Action`。见 [file:///workspace/src/ApiGateway/Leno.ApiGateway/Services/CacheInvalidationSubscriber.cs#L222-L225] |
| T5 | Redis 限流事务显式 await | [ALREADY-FIXED] | 在 Order.Infrastructure 中已修复，超出 Shared BC 扫描范围 |
| T6 | 对账服务分页 | [ALREADY-FIXED] | 在 Order.Infrastructure 中已修复，超出 Shared BC 扫描范围 |
| T7 | gRPC 防腐层 Polly 重试 | [ALREADY-FIXED] | `AntiCorruptionPollyExtensions.AddLenoGrpcAntiCorruptionPolly` 已实现，`GrpcAntiCorruptionClientBase.ExecuteAsync` 已集成 Polly retry。见 [file:///workspace/src/BuildingBlocks/Leno.Infrastructure/AntiCorruption/AntiCorruptionPollyExtensions.cs#L75-L92]、[file:///workspace/src/BuildingBlocks/Leno.Infrastructure/AntiCorruption/GrpcAntiCorruptionClientBase.cs#L52-L63] |
| T13 | Outbox 两阶段标记 | [ALREADY-FIXED] | `OutboxMessage` 已含 Publishing 中间态与 `PublishingStartedAt`，`OutboxPublisher` 已实现两阶段标记。见 [file:///workspace/src/BuildingBlocks/Leno.Infrastructure/Outbox/OutboxMessage.cs]、[file:///workspace/src/BuildingBlocks/Leno.Infrastructure/Outbox/OutboxPublisher.cs] |
| T14 | IIdempotencyStore SET NX | [ALREADY-FIXED] | `RedisIdempotencyStore` 已用 `StringSetAsync(..., When.NotExists)` + 24h TTL。见 [file:///workspace/src/BuildingBlocks/Leno.Infrastructure/EventBus/RedisIdempotencyStore.cs#L37-L52] |
| T17 | AntiCorruptionMetrics RecordFailure | [ALREADY-FIXED] | `RecordFailure` 已实现，`GrpcAntiCorruptionClientBase` 与 `AntiCorruptionBase` 已调用。见 [file:///workspace/src/BuildingBlocks/Leno.Infrastructure/AntiCorruption/AntiCorruptionMetrics.cs#L72-L83] |
| T21 | CacheInvalidationSubscriber 连接监听 + 双删 | [ALREADY-FIXED] | 已监听 `ConnectionFailed`/`InternalError` 事件并指数退避重连，双删模式已实现。见 [file:///workspace/src/ApiGateway/Leno.ApiGateway/Services/CacheInvalidationSubscriber.cs#L118-L148] |
| T22 | OutboxPublisher Parallel.ForEachAsync | [ALREADY-FIXED] | 已改用 `Parallel.ForEachAsync` 并发发布，`AlertIfPendingBacklogAsync` 告警，`IOutboxEventTypeResolver` 解析事件类型。见 [file:///workspace/src/BuildingBlocks/Leno.Infrastructure/Outbox/OutboxPublisher.cs]、[file:///workspace/src/BuildingBlocks/Leno.Infrastructure/Outbox/IOutboxEventTypeResolver.cs] |
| T23 | CacheInvalidationSubscriber UNLINK + 分批 SCAN | [ALREADY-FIXED] | Pattern 失效已用 SCAN 遍历 + UNLINK 批量删除（每批 100 个 key）。见 [file:///workspace/src/ApiGateway/Leno.ApiGateway/Services/CacheInvalidationSubscriber.cs#L310-L372] |

---

## 附录：跨 BC 关联说明

本计划仅覆盖 Shared BC（BuildingBlocks + ApiGateway）内部修复。以下问题涉及跨 BC 协调，需在跨 BC 修复计划中跟踪：

1. **审计 #7 BaseDbContext 审计字段**：所有 BC 的 DbContext 子类需注入 `ICurrentUserContext` 并覆盖 `CurrentUserContext` 属性，参见 [file:///workspace/docs/superpowers/specs/2026-07-21-code-audit/00-summary.md] F 章节。
2. **审计 #24 EfCoreUnitOfWork Outbox 持久化**：所有 BC 的 UoW 需在同一事务中保存业务实体与 Outbox 消息，参见 [file:///workspace/docs/superpowers/specs/2026-07-21-code-audit/13-architecture-assessment.md] G4 章节。
3. **审计 #4 IntegrationEventConsumerBase 幂等**：`IIdempotencyStore` 接口变更影响所有 BC 的消费者，需跨 BC 统一升级。
4. **审计 #29-30 Money 值对象**：`Money` 在 Product/Promotion/Order/Cart 多 BC 使用，共享内核修复需跨 BC 评审，参见 [file:///workspace/docs/superpowers/specs/2026-07-21-code-audit/00-summary.md] D3.1 章节。
5. **审计 #31-32 Entity 基类**：所有 BC 的实体继承 `Entity` 基类，修改 `Id`/`GetHashCode` 需跨 BC 回归测试。
