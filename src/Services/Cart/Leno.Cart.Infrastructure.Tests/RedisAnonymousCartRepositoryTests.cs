using System.Text.Json;
using Leno.Cart.Domain.Aggregates;
using Leno.Cart.Domain.Exceptions;
using Leno.Cart.Infrastructure.Repositories;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using StackExchange.Redis;
using CartAggregate = global::Leno.Cart.Domain.Aggregates.Cart;

namespace Leno.Cart.Infrastructure.Tests;

/// <summary>
/// RedisAnonymousCartRepository 单元测试。
/// <para>
/// P1-1 修复后覆盖：CAS Lua 脚本原子更新、Hash 存储格式、并发冲突检测、
/// 旧 String 格式向后兼容读取、领域事件清理、基础设施异常传播。
/// </para>
/// </summary>
public class RedisAnonymousCartRepositoryTests
{
    private readonly Mock<IConnectionMultiplexer> _redisMock = new();
    private readonly Mock<IDatabase> _dbMock = new();

    /// <summary>
    /// 测试用 JSON 序列化选项（与 RedisAnonymousCartRepository.JsonOptions 配置一致，Web/camelCase）。
    /// 缓存重用避免 CA1869：每次序列化创建新实例会触发源生成器重复初始化。
    /// </summary>
    private static readonly JsonSerializerOptions TestJsonOptions = new(JsonSerializerDefaults.Web);

    public RedisAnonymousCartRepositoryTests()
    {
        _redisMock.Setup(r => r.GetDatabase(It.IsAny<int>(), It.IsAny<object>())).Returns(_dbMock.Object);
    }

    // ============== GetAsync 异常传播测试 ==============

    [Fact]
    public async Task GetAsync_RedisConnectionException_ShouldThrowCartInfrastructureException()
    {
        _dbMock
            .Setup(d => d.KeyTypeAsync(It.IsAny<RedisKey>(), It.IsAny<CommandFlags>()))
            .ThrowsAsync(new RedisConnectionException(ConnectionFailureType.SocketFailure, "redis down"));
        var sut = new RedisAnonymousCartRepository(_redisMock.Object, NullLogger<RedisAnonymousCartRepository>.Instance);

        var act = () => sut.GetAsync("session-1");

        await act.Should().ThrowAsync<CartInfrastructureException>()
            .WithMessage("*匿名购物车暂不可用*")
            .WithInnerException<CartInfrastructureException, RedisConnectionException>();
    }

    [Fact]
    public async Task GetAsync_KeyNotExists_ShouldReturnNullWithoutThrowing()
    {
        _dbMock
            .Setup(d => d.KeyTypeAsync(It.IsAny<RedisKey>(), It.IsAny<CommandFlags>()))
            .ReturnsAsync(RedisType.None);
        var sut = new RedisAnonymousCartRepository(_redisMock.Object, NullLogger<RedisAnonymousCartRepository>.Instance);

        var result = await sut.GetAsync("session-1");

        result.Should().BeNull();
    }

    [Fact]
    public async Task GetAsync_GeneralException_ShouldThrowCartInfrastructureException()
    {
        _dbMock
            .Setup(d => d.KeyTypeAsync(It.IsAny<RedisKey>(), It.IsAny<CommandFlags>()))
            .ThrowsAsync(new InvalidOperationException("unexpected"));
        var sut = new RedisAnonymousCartRepository(_redisMock.Object, NullLogger<RedisAnonymousCartRepository>.Instance);

        var act = () => sut.GetAsync("session-1");

        await act.Should().ThrowAsync<CartInfrastructureException>();
    }

    // ============== GetAsync Hash 格式读取测试 ==============

    [Fact]
    public async Task GetAsync_HashFormat_ShouldLoadCartWithVersion()
    {
        // P1-1：Hash 格式读取应加载 payload 并同步聚合 Revision 为 Hash version 字段值
        var cart = CartAggregate.CreateAnonymous(Guid.NewGuid());
        cart.AddItem(Guid.NewGuid(), 2, Guid.NewGuid());
        var payloadJson = System.Text.Json.JsonSerializer.Serialize(cart, TestJsonOptions);
        const int storedVersion = 5;

        _dbMock
            .Setup(d => d.KeyTypeAsync(It.IsAny<RedisKey>(), It.IsAny<CommandFlags>()))
            .ReturnsAsync(RedisType.Hash);
        _dbMock
            .Setup(d => d.HashGetAsync(It.IsAny<RedisKey>(), It.IsAny<RedisValue[]>(), It.IsAny<CommandFlags>()))
            .ReturnsAsync(new RedisValue[] { payloadJson, (RedisValue)storedVersion.ToString() });

        var sut = new RedisAnonymousCartRepository(_redisMock.Object, NullLogger<RedisAnonymousCartRepository>.Instance);

        var result = await sut.GetAsync("session-1");

        result.Should().NotBeNull();
        result!.Revision.Should().Be(storedVersion, "Hash version 字段应同步到聚合 Revision");
    }

    [Fact]
    public async Task GetAsync_LegacyStringFormat_ShouldLoadCartWithVersionZero()
    {
        // P1-1 兼容：迁移前 String 格式无 version 字段，按 0 处理，首次 CAS 保存会迁移到 Hash 格式
        var cart = CartAggregate.CreateAnonymous(Guid.NewGuid());
        cart.AddItem(Guid.NewGuid(), 1, Guid.NewGuid());
        var payloadJson = System.Text.Json.JsonSerializer.Serialize(cart, TestJsonOptions);

        _dbMock
            .Setup(d => d.KeyTypeAsync(It.IsAny<RedisKey>(), It.IsAny<CommandFlags>()))
            .ReturnsAsync(RedisType.String);
        _dbMock
            .Setup(d => d.StringGetAsync(It.IsAny<RedisKey>(), It.IsAny<CommandFlags>()))
            .ReturnsAsync((RedisValue)payloadJson);

        var sut = new RedisAnonymousCartRepository(_redisMock.Object, NullLogger<RedisAnonymousCartRepository>.Instance);

        var result = await sut.GetAsync("session-1");

        result.Should().NotBeNull();
        result!.Revision.Should().Be(0, "旧 String 格式无 version，默认 0");
    }

    [Fact]
    public async Task GetAsync_UnexpectedKeyType_ShouldReturnNullAndLogWarning()
    {
        _dbMock
            .Setup(d => d.KeyTypeAsync(It.IsAny<RedisKey>(), It.IsAny<CommandFlags>()))
            .ReturnsAsync(RedisType.List);
        var sut = new RedisAnonymousCartRepository(_redisMock.Object, NullLogger<RedisAnonymousCartRepository>.Instance);

        var result = await sut.GetAsync("session-1");

        result.Should().BeNull();
    }

    // ============== SaveAsync CAS 原子更新测试 ==============

    [Fact]
    public async Task SaveAsync_RedisConnectionException_ShouldThrowCartInfrastructureException()
    {
        _dbMock
            .Setup(d => d.ScriptEvaluateAsync(
                It.IsAny<string>(),
                It.IsAny<RedisKey[]>(),
                It.IsAny<RedisValue[]>(),
                It.IsAny<CommandFlags>()))
            .ThrowsAsync(new RedisConnectionException(ConnectionFailureType.SocketFailure, "redis down"));
        var sut = new RedisAnonymousCartRepository(_redisMock.Object, NullLogger<RedisAnonymousCartRepository>.Instance);
        var cart = CartAggregate.CreateAnonymous(Guid.NewGuid());

        var act = () => sut.SaveAsync("session-1", cart);

        await act.Should().ThrowAsync<CartInfrastructureException>()
            .WithMessage("*匿名购物车暂不可用*");
    }

    [Fact]
    public async Task SaveAsync_WithExpectedVersion_FirstSave_ShouldReturnTrueAndIncrementRevision()
    {
        // P1-1：新购物车 Revision=0，首次 CAS 保存（expectedVersion=0）应成功并递增 Revision 到 1
        var cart = CartAggregate.CreateAnonymous(Guid.NewGuid());
        cart.Revision.Should().Be(0, "新购物车 Revision 默认为 0");

        SetupScriptEvaluateReturning(1);
        var sut = new RedisAnonymousCartRepository(_redisMock.Object, NullLogger<RedisAnonymousCartRepository>.Instance);

        var result = await sut.SaveAsync("session-1", cart, expectedVersion: 0);

        result.Should().BeTrue("首次保存版本匹配应成功");
        cart.Revision.Should().Be(1, "CAS 成功后 Revision 应递增为 expectedVersion + 1");
    }

    [Fact]
    public async Task SaveAsync_WithExpectedVersion_SequentialSave_ShouldReturnTrueAndIncrementRevision()
    {
        // P1-1：连续保存场景，每次成功后 Revision 递增
        var cart = CartAggregate.CreateAnonymous(Guid.NewGuid());
        cart.MarkLoaded(5); // 模拟从 Redis 加载的购物车，当前版本 5

        SetupScriptEvaluateReturning(1);
        var sut = new RedisAnonymousCartRepository(_redisMock.Object, NullLogger<RedisAnonymousCartRepository>.Instance);

        var result = await sut.SaveAsync("session-1", cart, expectedVersion: 5);

        result.Should().BeTrue();
        cart.Revision.Should().Be(6, "CAS 成功后 Revision 应递增为 5 + 1 = 6");
    }

    [Fact]
    public async Task SaveAsync_WithExpectedVersion_VersionMismatch_ShouldReturnFalseWithoutIncrementingRevision()
    {
        // P1-1：并发冲突场景，expectedVersion 与 Redis version 不一致应返回 false
        var cart = CartAggregate.CreateAnonymous(Guid.NewGuid());
        cart.MarkLoaded(5); // 客户端加载时的版本 5，但 Redis 中已是 6（另一请求已修改）

        SetupScriptEvaluateReturning(0); // Lua 返回 0 = 版本不匹配
        var sut = new RedisAnonymousCartRepository(_redisMock.Object, NullLogger<RedisAnonymousCartRepository>.Instance);

        var result = await sut.SaveAsync("session-1", cart, expectedVersion: 5);

        result.Should().BeFalse("版本不匹配应返回 false");
        cart.Revision.Should().Be(5, "CAS 失败时 Revision 不应递增");
    }

    [Fact]
    public async Task SaveAsync_NoVersion_ConcurrentConflict_ShouldThrowCartConcurrencyException()
    {
        // P1-1：无版本重载（向后兼容）在并发冲突时抛出 CartConcurrencyException
        var cart = CartAggregate.CreateAnonymous(Guid.NewGuid());
        cart.MarkLoaded(3); // 客户端加载版本 3

        // CAS 返回 0（冲突），随后 TryGetVersionAsync 读取实际版本 4
        SetupScriptEvaluateReturning(0);
        _dbMock
            .Setup(d => d.KeyTypeAsync(It.IsAny<RedisKey>(), It.IsAny<CommandFlags>()))
            .ReturnsAsync(RedisType.Hash);
        _dbMock
            .Setup(d => d.HashGetAsync(It.IsAny<RedisKey>(), It.IsAny<RedisValue>(), It.IsAny<CommandFlags>()))
            .ReturnsAsync((RedisValue)"4");

        var sut = new RedisAnonymousCartRepository(_redisMock.Object, NullLogger<RedisAnonymousCartRepository>.Instance);

        var act = () => sut.SaveAsync("session-1", cart);

        var ex = await act.Should().ThrowAsync<CartConcurrencyException>();
        ex.Which.ExpectedVersion.Should().Be(3);
        ex.Which.ActualVersion.Should().Be(4);
    }

    [Fact]
    public async Task SaveAsync_NoVersion_Success_ShouldNotThrow()
    {
        // P1-1：无版本重载在 CAS 成功时正常返回（不抛异常）
        var cart = CartAggregate.CreateAnonymous(Guid.NewGuid());

        SetupScriptEvaluateReturning(1);
        var sut = new RedisAnonymousCartRepository(_redisMock.Object, NullLogger<RedisAnonymousCartRepository>.Instance);

        var act = () => sut.SaveAsync("session-1", cart);

        await act.Should().NotThrowAsync();
        cart.Revision.Should().Be(1, "CAS 成功后 Revision 应递增");
    }

    [Fact]
    public async Task SaveAsync_WithExpectedVersion_NegativeVersion_ShouldThrowArgumentOutOfRangeException()
    {
        var cart = CartAggregate.CreateAnonymous(Guid.NewGuid());
        var sut = new RedisAnonymousCartRepository(_redisMock.Object, NullLogger<RedisAnonymousCartRepository>.Instance);

        var act = () => sut.SaveAsync("session-1", cart, expectedVersion: -1);

        await act.Should().ThrowAsync<ArgumentOutOfRangeException>();
    }

    // ============== SaveAsync 领域事件清理测试 ==============

    [Fact]
    public async Task SaveAsync_ShouldClearDomainEventsBeforeSerializationToAvoidRedisJsonMonotonicGrowth()
    {
        // P1-4：匿名购物车 _domainEvents 在 EF Core 落库路径由 SaveChangesWithOutboxAsync 清理，
        // 走 Redis 持久化路径时若不清理，每次 SaveAsync 都把累积事件序列化进 JSON，单调增长。
        var capturedPayload = string.Empty;
        _dbMock
            .Setup(d => d.ScriptEvaluateAsync(
                It.IsAny<string>(),
                It.IsAny<RedisKey[]>(),
                It.IsAny<RedisValue[]>(),
                It.IsAny<CommandFlags>()))
            .Callback<string, RedisKey[], RedisValue[], CommandFlags>((_, _, values, _) =>
            {
                // ARGV[0]=expectedVersion, ARGV[1]=payload, ARGV[2]=newVersion, ARGV[3]=ttl
                if (values != null && values.Length > 1)
                {
                    capturedPayload = (string)values[1]!;
                }
            })
            .ReturnsAsync(RedisResult.Create(1, ResultType.Integer));

        var sut = new RedisAnonymousCartRepository(_redisMock.Object, NullLogger<RedisAnonymousCartRepository>.Instance);
        var cart = CartAggregate.CreateAnonymous(Guid.NewGuid());
        // AddItem 会发布 SkuAddedToCartEvent，构造有领域事件的状态
        cart.AddItem(Guid.NewGuid(), 1, Guid.NewGuid());
        cart.DomainEvents.Should().NotBeEmpty("预置：发布 SkuAddedToCartEvent 后应有领域事件");

        await sut.SaveAsync("session-1", cart);

        // 1) 调用方视角：SaveAsync 后聚合的领域事件已被清理
        cart.DomainEvents.Should().BeEmpty("SaveAsync 应清理领域事件");
        // 2) 序列化内容视角：JSON 中不应包含 domainEvents 字段
        capturedPayload.Should().NotContain("domainEvents", "序列化前应已清理领域事件，避免 Redis JSON 单调增长");
        capturedPayload.Should().NotContain("SkuAddedToCartEvent");
    }

    // ============== TrySaveAsync 原子创建测试 ==============

    [Fact]
    public async Task TrySaveAsync_FirstCreate_ShouldReturnTrue()
    {
        // P2-10：原子创建，Lua 脚本返回 1（key 不存在，已创建）
        var cart = CartAggregate.CreateAnonymous(Guid.NewGuid());
        SetupScriptEvaluateReturning(1);
        var sut = new RedisAnonymousCartRepository(_redisMock.Object, NullLogger<RedisAnonymousCartRepository>.Instance);

        var result = await sut.TrySaveAsync("session-1", cart);

        result.Should().BeTrue();
    }

    [Fact]
    public async Task TrySaveAsync_KeyExists_ShouldReturnFalse()
    {
        // P2-10：并发场景，key 已存在，Lua 脚本返回 0
        var cart = CartAggregate.CreateAnonymous(Guid.NewGuid());
        SetupScriptEvaluateReturning(0);
        var sut = new RedisAnonymousCartRepository(_redisMock.Object, NullLogger<RedisAnonymousCartRepository>.Instance);

        var result = await sut.TrySaveAsync("session-1", cart);

        result.Should().BeFalse();
    }

    [Fact]
    public async Task TrySaveAsync_RedisConnectionException_ShouldThrowCartInfrastructureException()
    {
        _dbMock
            .Setup(d => d.ScriptEvaluateAsync(
                It.IsAny<string>(),
                It.IsAny<RedisKey[]>(),
                It.IsAny<RedisValue[]>(),
                It.IsAny<CommandFlags>()))
            .ThrowsAsync(new RedisConnectionException(ConnectionFailureType.SocketFailure, "redis down"));
        var sut = new RedisAnonymousCartRepository(_redisMock.Object, NullLogger<RedisAnonymousCartRepository>.Instance);
        var cart = CartAggregate.CreateAnonymous(Guid.NewGuid());

        var act = () => sut.TrySaveAsync("session-1", cart);

        await act.Should().ThrowAsync<CartInfrastructureException>();
    }

    // ============== RemoveAsync / RefreshTtlAsync 异常传播测试 ==============

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
        // StackExchange.Redis 2.8+ 新增 ExpireWhen 参数重载，db.KeyExpireAsync(key, Ttl) 可能绑定到
        // 3 参数 (RedisKey, TimeSpan?, CommandFlags) 或 4 参数 (RedisKey, TimeSpan?, ExpireWhen, CommandFlags)，
        // 此处同时 mock 两个重载确保异常传播覆盖任一绑定路径。
        var redisEx = new RedisConnectionException(ConnectionFailureType.SocketFailure, "redis down");
        _dbMock
            .Setup(d => d.KeyExpireAsync(It.IsAny<RedisKey>(), It.IsAny<TimeSpan?>(), It.IsAny<CommandFlags>()))
            .ThrowsAsync(redisEx);
        _dbMock
            .Setup(d => d.KeyExpireAsync(It.IsAny<RedisKey>(), It.IsAny<TimeSpan?>(), It.IsAny<ExpireWhen>(), It.IsAny<CommandFlags>()))
            .ThrowsAsync(redisEx);
        var sut = new RedisAnonymousCartRepository(_redisMock.Object, NullLogger<RedisAnonymousCartRepository>.Instance);

        var act = () => sut.RefreshTtlAsync("session-1");

        await act.Should().ThrowAsync<CartInfrastructureException>();
    }

    // ============== SaveAsyncLegacy 标记 Obsolete 测试 ==============

    [Fact]
    public async Task SaveAsyncLegacy_RedisConnectionException_ShouldThrowCartInfrastructureException()
    {
        // 保留旧非原子实现作为 fallback，异常传播行为应与原 SaveAsync 一致
        #pragma warning disable CS0618 // SaveAsyncLegacy 已标记 Obsolete，测试中显式调用
        _dbMock
            .Setup(d => d.StringSetAsync(It.IsAny<RedisKey>(), It.IsAny<RedisValue>(), It.IsAny<TimeSpan?>(), It.IsAny<bool>(), It.IsAny<When>(), It.IsAny<CommandFlags>()))
            .ThrowsAsync(new RedisConnectionException(ConnectionFailureType.SocketFailure, "redis down"));
        var sut = new RedisAnonymousCartRepository(_redisMock.Object, NullLogger<RedisAnonymousCartRepository>.Instance);
        var cart = CartAggregate.CreateAnonymous(Guid.NewGuid());

        var act = () => sut.SaveAsyncLegacy("session-1", cart);

        await act.Should().ThrowAsync<CartInfrastructureException>()
            .WithMessage("*匿名购物车暂不可用*");
        #pragma warning restore CS0618
    }

    // ============== 辅助方法 ==============

    /// <summary>
    /// 配置 ScriptEvaluateAsync mock 返回指定的整数值（1=成功，0=冲突）。
    /// </summary>
    private void SetupScriptEvaluateReturning(int returnValue)
    {
        _dbMock
            .Setup(d => d.ScriptEvaluateAsync(
                It.IsAny<string>(),
                It.IsAny<RedisKey[]>(),
                It.IsAny<RedisValue[]>(),
                It.IsAny<CommandFlags>()))
            .ReturnsAsync(RedisResult.Create(returnValue, ResultType.Integer));
    }
}
