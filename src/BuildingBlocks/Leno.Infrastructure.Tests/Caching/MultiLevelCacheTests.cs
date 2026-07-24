using System.Text.Json;
using Leno.Infrastructure.Abstractions;
using Leno.Infrastructure.Caching;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using StackExchange.Redis;

namespace Leno.Infrastructure.Tests.Caching;

/// <summary>
/// 多级缓存（L1 IMemoryCache + L2 Redis ICacheService + Pub/Sub 跨实例失效）单元测试。
/// <para>
/// 覆盖场景：
/// <list type="bullet">
/// <item>L1 命中直接返回，不查 L2</item>
/// <item>L1 未命中 L2 命中，回填 L1 后返回</item>
/// <item>L1+L2 均未命中，回源 factory 并回填 L1+L2</item>
/// <item>双 miss 回源返回 null，不回填缓存</item>
/// <item>SetAsync 同时写入 L1+L2</item>
/// <item>RemoveAsync 删 L1+L2 并发布 Pub/Sub 失效通知</item>
/// <item>CacheInvalidationSubscriber 收到 Pub/Sub 消息清本地 L1</item>
/// <item>L1EnabledPrefixes 按 Key 前缀切流：不匹配前缀的 Key 仅走 L2</item>
/// <item>订阅者处理坏消息不抛异常（保证订阅通道不中断）</item>
/// </list>
/// </para>
/// </summary>
public class MultiLevelCacheTests : IDisposable
{
    private readonly Mock<ICacheService> _l2Mock;
    private readonly Mock<ICacheInvalidationPublisher> _publisherMock;
    private readonly Mock<ILogger<MultiLevelCache>> _loggerMock;
    private readonly MemoryCache _l1;
    private readonly MultiLevelCacheOptions _options;

    public MultiLevelCacheTests()
    {
        _l2Mock = new Mock<ICacheService>();
        _publisherMock = new Mock<ICacheInvalidationPublisher>();
        _loggerMock = new Mock<ILogger<MultiLevelCache>>();
        _l1 = new MemoryCache(new MemoryCacheOptions());
        _options = new MultiLevelCacheOptions
        {
            L1Ttl = TimeSpan.FromSeconds(5),
            L2Ttl = TimeSpan.FromMinutes(30),
            InvalidationChannel = "leno:cache:invalidation:test"
        };
    }

    private MultiLevelCache CreateSut(MultiLevelCacheOptions? options = null)
    {
        var opts = Options.Create(options ?? _options);
        return new MultiLevelCache(_l1, _l2Mock.Object, _publisherMock.Object, opts, _loggerMock.Object);
    }

    [Fact]
    public async Task GetAsync_L1Hit_ShouldReturnDirectly_WithoutQueryingL2()
    {
        // Arrange：L1 已有值
        var key = "product:spu:1";
        var cached = new TestDto { Id = 1, Name = "L1Value" };
        _l1.Set(key, cached, _options.L1Ttl);
        var factoryCallCount = 0;
        var sut = CreateSut();

        // Act
        var result = await sut.GetAsync<TestDto>(key, ct =>
        {
            factoryCallCount++;
            return Task.FromResult<TestDto?>(new TestDto { Id = 999, Name = "factory" });
        });

        // Assert：直接返回 L1 值，不查 L2、不调 factory
        result.Should().BeSameAs(cached);
        factoryCallCount.Should().Be(0);
        _l2Mock.Verify(l2 => l2.GetAsync<TestDto>(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
        _l2Mock.Verify(l2 => l2.SetAsync(It.IsAny<string>(), It.IsAny<TestDto>(), It.IsAny<TimeSpan?>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task GetAsync_L1MissL2Hit_ShouldBackfillL1_AndReturnL2Value()
    {
        // Arrange：L1 未命中，L2 命中
        var key = "product:spu:2";
        var l2Value = new TestDto { Id = 2, Name = "L2Value" };
        _l2Mock.Setup(l2 => l2.GetAsync<TestDto>(key, It.IsAny<CancellationToken>()))
            .ReturnsAsync(l2Value);

        var factoryCallCount = 0;
        var sut = CreateSut();

        // Act
        var result = await sut.GetAsync<TestDto>(key, ct =>
        {
            factoryCallCount++;
            return Task.FromResult<TestDto?>(null);
        });

        // Assert：返回 L2 值，回填 L1，不调 factory
        result.Should().BeSameAs(l2Value);
        factoryCallCount.Should().Be(0);
        _l1.TryGetValue(key, out TestDto? l1Backfilled).Should().BeTrue();
        l1Backfilled.Should().BeSameAs(l2Value);
        _l2Mock.Verify(l2 => l2.SetAsync(It.IsAny<string>(), It.IsAny<TestDto>(), It.IsAny<TimeSpan?>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task GetAsync_BothMiss_ShouldInvokeFactory_AndBackfillL1AndL2()
    {
        // Arrange：L1+L2 均未命中
        var key = "product:spu:3";
        _l2Mock.Setup(l2 => l2.GetAsync<TestDto>(key, It.IsAny<CancellationToken>()))
            .ReturnsAsync((TestDto?)null);

        var factoryValue = new TestDto { Id = 3, Name = "FactoryValue" };
        var factoryCallCount = 0;
        var sut = CreateSut();

        // Act
        var result = await sut.GetAsync<TestDto>(key, ct =>
        {
            factoryCallCount++;
            return Task.FromResult<TestDto?>(factoryValue);
        });

        // Assert：返回 factory 值，调用 factory 1 次，回填 L1+L2
        result.Should().BeSameAs(factoryValue);
        factoryCallCount.Should().Be(1);
        _l1.TryGetValue(key, out TestDto? l1Backfilled).Should().BeTrue();
        l1Backfilled.Should().BeSameAs(factoryValue);
        _l2Mock.Verify(l2 => l2.SetAsync(key, factoryValue, _options.L2Ttl, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GetAsync_BothMiss_FactoryReturnsNull_ShouldNotBackfill()
    {
        // Arrange：L1+L2 均未命中
        var key = "product:spu:4";
        _l2Mock.Setup(l2 => l2.GetAsync<TestDto>(key, It.IsAny<CancellationToken>()))
            .ReturnsAsync((TestDto?)null);

        var sut = CreateSut();

        // Act
        var result = await sut.GetAsync<TestDto>(key, _ => Task.FromResult<TestDto?>(null));

        // Assert：返回 null，不回填 L1+L2
        result.Should().BeNull();
        _l1.TryGetValue(key, out TestDto? _).Should().BeFalse();
        _l2Mock.Verify(l2 => l2.SetAsync(It.IsAny<string>(), It.IsAny<TestDto>(), It.IsAny<TimeSpan?>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task GetAsync_BothMiss_ShouldUseCustomL2Ttl_WhenProvided()
    {
        // Arrange
        var key = "product:spu:5";
        var customTtl = TimeSpan.FromMinutes(10);
        _l2Mock.Setup(l2 => l2.GetAsync<TestDto>(key, It.IsAny<CancellationToken>()))
            .ReturnsAsync((TestDto?)null);

        var factoryValue = new TestDto { Id = 5, Name = "FactoryValue" };
        var sut = CreateSut();

        // Act
        await sut.GetAsync(key, _ => Task.FromResult<TestDto?>(factoryValue), customTtl);

        // Assert：使用自定义 L2 TTL
        _l2Mock.Verify(l2 => l2.SetAsync(key, factoryValue, customTtl, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task SetAsync_ShouldWriteBothL1AndL2()
    {
        // Arrange
        var key = "product:spu:6";
        var value = new TestDto { Id = 6, Name = "SetValue" };
        var sut = CreateSut();

        // Act
        await sut.SetAsync(key, value);

        // Assert：L1+L2 均写入
        _l1.TryGetValue(key, out TestDto? l1Value).Should().BeTrue();
        l1Value.Should().BeSameAs(value);
        _l2Mock.Verify(l2 => l2.SetAsync(key, value, _options.L2Ttl, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task SetAsync_ShouldUseCustomL2Ttl_WhenProvided()
    {
        // Arrange
        var key = "product:spu:7";
        var value = new TestDto { Id = 7, Name = "SetValue" };
        var customTtl = TimeSpan.FromMinutes(15);
        var sut = CreateSut();

        // Act
        await sut.SetAsync(key, value, customTtl);

        // Assert
        _l2Mock.Verify(l2 => l2.SetAsync(key, value, customTtl, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task RemoveAsync_ShouldRemoveL1AndL2_AndPublishInvalidation()
    {
        // Arrange：L1 已有值
        var key = "product:spu:8";
        var cached = new TestDto { Id = 8, Name = "ToRemove" };
        _l1.Set(key, cached, _options.L1Ttl);
        var sut = CreateSut();

        // Act
        await sut.RemoveAsync(key);

        // Assert：L1 已删、L2 已删、Pub/Sub 已发布
        _l1.TryGetValue(key, out TestDto? _).Should().BeFalse();
        _l2Mock.Verify(l2 => l2.RemoveAsync(key, It.IsAny<CancellationToken>()), Times.Once);
        _publisherMock.Verify(p => p.PublishInvalidationAsync(key, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GetAsync_L1DisabledByKeyPrefix_ShouldOnlyQueryL2()
    {
        // Arrange：L1EnabledPrefixes 配置为 ["product:"]，"user:" 前缀的 Key 不启用 L1
        var options = new MultiLevelCacheOptions
        {
            L1Ttl = TimeSpan.FromSeconds(5),
            L2Ttl = TimeSpan.FromMinutes(30),
            L1EnabledPrefixes = new List<string> { "product:" }
        };
        var key = "user:profile:1";
        var l2Value = new TestDto { Id = 1, Name = "L2Only" };
        _l2Mock.Setup(l2 => l2.GetAsync<TestDto>(key, It.IsAny<CancellationToken>()))
            .ReturnsAsync(l2Value);

        // 即便手动往 L1 写入，由于前缀不匹配，GetAsync 也不应读 L1
        _l1.Set(key, new TestDto { Id = 999, Name = "StaleL1Value" }, _options.L1Ttl);

        var sut = CreateSut(options);

        // Act
        var result = await sut.GetAsync<TestDto>(key, _ => Task.FromResult<TestDto?>(null));

        // Assert：跳过 L1 直接查 L2，且不回填 L1
        result.Should().BeSameAs(l2Value);
        // L1 中仍是手动写入的旧值，未被回填
        _l1.TryGetValue(key, out TestDto? l1Current).Should().BeTrue();
        l1Current!.Id.Should().Be(999);
    }

    [Fact]
    public async Task GetAsync_L1EnabledByKeyPrefix_ShouldQueryL1First()
    {
        // Arrange：L1EnabledPrefixes 配置为 ["product:"]，"product:" 前缀的 Key 启用 L1
        var options = new MultiLevelCacheOptions
        {
            L1Ttl = TimeSpan.FromSeconds(5),
            L2Ttl = TimeSpan.FromMinutes(30),
            L1EnabledPrefixes = new List<string> { "product:" }
        };
        var key = "product:spu:9";
        var l1Value = new TestDto { Id = 9, Name = "L1Value" };
        _l1.Set(key, l1Value, options.L1Ttl);

        var sut = CreateSut(options);

        // Act
        var result = await sut.GetAsync<TestDto>(key, _ => Task.FromResult<TestDto?>(null));

        // Assert：L1 命中，不查 L2
        result.Should().BeSameAs(l1Value);
        _l2Mock.Verify(l2 => l2.GetAsync<TestDto>(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task GetAsync_NullKey_ShouldThrow()
    {
        var sut = CreateSut();
        var act = () => sut.GetAsync<TestDto>(null!, _ => Task.FromResult<TestDto?>(null));
        await act.Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public async Task GetAsync_NullFactory_ShouldThrow()
    {
        var sut = CreateSut();
        var act = () => sut.GetAsync<TestDto>("key", null!);
        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    [Fact]
    public async Task SetAsync_NullKey_ShouldThrow()
    {
        var sut = CreateSut();
        var act = () => sut.SetAsync<TestDto>(null!, new TestDto());
        await act.Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public async Task SetAsync_NullValue_ShouldThrow()
    {
        var sut = CreateSut();
        var act = () => sut.SetAsync<TestDto>("key", null!);
        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    [Fact]
    public async Task RemoveAsync_NullKey_ShouldThrow()
    {
        var sut = CreateSut();
        var act = () => sut.RemoveAsync(null!);
        await act.Should().ThrowAsync<ArgumentException>();
    }

    /// <summary>
    /// 验证 CacheInvalidationSubscriber 收到 Pub/Sub 失效消息后清除本地 L1。
    /// </summary>
    [Fact]
    public void Subscriber_ReceivingInvalidationMessage_ShouldRemoveKeyFromL1()
    {
        // Arrange
        var key = "product:spu:10";
        var cached = new TestDto { Id = 10, Name = "CachedValue" };
        _l1.Set(key, cached, _options.L1Ttl);

        var redisMock = new Mock<IConnectionMultiplexer>();
        var subMock = new Mock<ISubscriber>();
        redisMock.Setup(r => r.GetSubscriber(It.IsAny<object>())).Returns(subMock.Object);

        var subscriberLoggerMock = new Mock<ILogger<CacheInvalidationSubscriber>>();
        var subscriber = new CacheInvalidationSubscriber(
            redisMock.Object, _l1, Options.Create(_options), subscriberLoggerMock.Object);

        // 构造一条失效消息
        var payload = new CacheInvalidationPublisher.CacheInvalidationPayload(key, "test-origin");
        var message = JsonSerializer.Serialize(payload);

        // Act：直接调用 internal 处理方法（模拟收到 Pub/Sub 消息）
        subscriber.HandleInvalidationMessage(
            RedisChannel.Literal(_options.InvalidationChannel),
            message);

        // Assert：L1 中对应 Key 已被清除
        _l1.TryGetValue(key, out TestDto? _).Should().BeFalse();
    }

    /// <summary>
    /// 验证订阅者处理坏消息（无法反序列化）时不抛异常，保证订阅通道不中断。
    /// </summary>
    [Fact]
    public void Subscriber_ReceivingMalformedMessage_ShouldNotThrow()
    {
        // Arrange
        var redisMock = new Mock<IConnectionMultiplexer>();
        var subMock = new Mock<ISubscriber>();
        redisMock.Setup(r => r.GetSubscriber(It.IsAny<object>())).Returns(subMock.Object);

        var subscriberLoggerMock = new Mock<ILogger<CacheInvalidationSubscriber>>();
        var subscriber = new CacheInvalidationSubscriber(
            redisMock.Object, _l1, Options.Create(_options), subscriberLoggerMock.Object);

        // Act & Assert：坏消息不抛异常
        var act = () => subscriber.HandleInvalidationMessage(
            RedisChannel.Literal(_options.InvalidationChannel),
            "this is not valid json");
        act.Should().NotThrow();
    }

    /// <summary>
    /// 验证订阅者收到空消息时不做任何处理。
    /// </summary>
    [Fact]
    public void Subscriber_ReceivingEmptyMessage_ShouldDoNothing()
    {
        // Arrange
        var key = "product:spu:11";
        var cached = new TestDto { Id = 11, Name = "Cached" };
        _l1.Set(key, cached, _options.L1Ttl);

        var redisMock = new Mock<IConnectionMultiplexer>();
        var subMock = new Mock<ISubscriber>();
        redisMock.Setup(r => r.GetSubscriber(It.IsAny<object>())).Returns(subMock.Object);

        var subscriberLoggerMock = new Mock<ILogger<CacheInvalidationSubscriber>>();
        var subscriber = new CacheInvalidationSubscriber(
            redisMock.Object, _l1, Options.Create(_options), subscriberLoggerMock.Object);

        // Act：空消息
        subscriber.HandleInvalidationMessage(
            RedisChannel.Literal(_options.InvalidationChannel),
            RedisValue.Null);

        // Assert：L1 中的值未被清除
        _l1.TryGetValue(key, out TestDto? current).Should().BeTrue();
        current.Should().BeSameAs(cached);
    }

    /// <summary>
    /// 验证 CacheInvalidationPublisher 发布失效消息到 Pub/Sub 通道。
    /// </summary>
    [Fact]
    public async Task Publisher_PublishInvalidationAsync_ShouldPublishToChannel()
    {
        // Arrange
        var redisMock = new Mock<IConnectionMultiplexer>();
        var subMock = new Mock<ISubscriber>();
        redisMock.Setup(r => r.GetSubscriber(It.IsAny<object>())).Returns(subMock.Object);

        var publisherLoggerMock = new Mock<ILogger<CacheInvalidationPublisher>>();
        var publisher = new CacheInvalidationPublisher(
            redisMock.Object, Options.Create(_options), publisherLoggerMock.Object);

        // Act
        await publisher.PublishInvalidationAsync("product:spu:12");

        // Assert：发布到配置的 Pub/Sub 通道
        subMock.Verify(
            s => s.PublishAsync(
                RedisChannel.Literal(_options.InvalidationChannel),
                It.IsAny<RedisValue>(),
                It.IsAny<CommandFlags>()),
            Times.Once);
    }

    /// <summary>
    /// 验证 MultiLevelCacheOptions.IsL1EnabledForKey：空前缀列表时所有 Key 启用 L1。
    /// </summary>
    [Fact]
    public void Options_EmptyPrefixes_ShouldEnableL1ForAllKeys()
    {
        var opts = new MultiLevelCacheOptions
        {
            L1EnabledPrefixes = Array.Empty<string>()
        };

        opts.IsL1EnabledForKey("any:key").Should().BeTrue();
        opts.IsL1EnabledForKey("product:1").Should().BeTrue();
        opts.IsL1EnabledForKey("").Should().BeTrue(); // 空列表时连空 key 也启用
    }

    /// <summary>
    /// 验证 MultiLevelCacheOptions.IsL1EnabledForKey：非空前缀列表时仅匹配前缀的 Key 启用 L1。
    /// </summary>
    [Fact]
    public void Options_NonEmptyPrefixes_ShouldEnableL1OnlyForMatchingKeys()
    {
        var opts = new MultiLevelCacheOptions
        {
            L1EnabledPrefixes = new List<string> { "product:", "promotion:seckill:" }
        };

        opts.IsL1EnabledForKey("product:spu:1").Should().BeTrue();
        opts.IsL1EnabledForKey("promotion:seckill:abc").Should().BeTrue();
        opts.IsL1EnabledForKey("user:profile:1").Should().BeFalse();
        opts.IsL1EnabledForKey("").Should().BeFalse();
    }

    /// <summary>
    /// 验证 DI 扩展方法 AddMultiLevelCache 注册所有必需服务。
    /// <para>
    /// 通过检查 <see cref="ServiceDescriptor"/> 而非解析实例，避免触发真实 Redis 连接
    /// （<c>AddRedisCache</c> 内 <c>Lazy&lt;IConnectionMultiplexer&gt;</c> 在解析 <c>ICacheService</c> 时会连接 Redis）。
    /// </para>
    /// </summary>
    [Fact]
    public void AddMultiLevelCache_ShouldRegisterAllRequiredServices()
    {
        // Arrange
        var services = new ServiceCollection();
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Redis:Configuration"] = "localhost:6379",
                ["Cache:MultiLevel:L1Ttl"] = "00:00:05",
                ["Cache:MultiLevel:L2Ttl"] = "00:30:00"
            })
            .Build();
        services.AddLogging();
        services.AddMultiLevelCache(config);

        // Assert：通过服务描述符验证注册（避免解析触发 Redis 连接）
        services.Should().Contain(d => d.ServiceType == typeof(IMemoryCache));
        services.Should().Contain(d => d.ServiceType == typeof(ICacheService));
        services.Should().Contain(d => d.ServiceType == typeof(IBloomFilter));
        services.Should().Contain(d => d.ServiceType == typeof(IConnectionMultiplexer));
        services.Should().Contain(d => d.ServiceType == typeof(ICacheInvalidationPublisher));
        services.Should().Contain(d => d.ServiceType == typeof(IMultiLevelCache));
        services.Should().Contain(d => d.ServiceType == typeof(CacheInvalidationSubscriber));
        // CacheInvalidationSubscriber 作为 IHostedService 注册
        services.Should().Contain(d =>
            d.ServiceType == typeof(Microsoft.Extensions.Hosting.IHostedService)
            && d.ImplementationType == typeof(CacheInvalidationSubscriber));

        // 验证 IMultiLevelCache 实例可解析（不触发 Redis 连接，
        // 因为 MultiLevelCache 的依赖 IMemoryCache/ICacheService/ICacheInvalidationPublisher 中，
        // 仅 ICacheService 解析会触发 Redis。这里仅验证描述符完整性。）
    }

    private sealed class TestDto
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
    }

    public void Dispose()
    {
        _l1.Dispose();
        GC.SuppressFinalize(this);
    }
}
