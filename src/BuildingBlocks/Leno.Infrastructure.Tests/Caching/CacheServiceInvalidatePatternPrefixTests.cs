using Leno.Infrastructure.Caching;
using Microsoft.Extensions.Logging;
using Moq;
using StackExchange.Redis;

namespace Leno.Infrastructure.Tests.Caching;

/// <summary>
/// T25 单元测试：验证 CacheService.InvalidatePatternAsync 强制拼接 KeyPrefix（leno:cache:）。
/// 不修改既有 CacheServiceTests 中的断言，仅新增针对 KeyPrefix 强制行为的验证。
/// </summary>
public class CacheServiceInvalidatePatternPrefixTests
{
    private const string KeyPrefix = "leno:cache:";

    private readonly Mock<IConnectionMultiplexer> _redisMock;
    private readonly Mock<IDatabase> _databaseMock;
    private readonly Mock<IBloomFilter> _bloomFilterMock;
    private readonly Mock<ILogger<CacheService>> _loggerMock;
    private readonly CacheService _sut;

    public CacheServiceInvalidatePatternPrefixTests()
    {
        _redisMock = new Mock<IConnectionMultiplexer>();
        _databaseMock = new Mock<IDatabase>();
        _bloomFilterMock = new Mock<IBloomFilter>();
        _loggerMock = new Mock<ILogger<CacheService>>();

        _redisMock.Setup(r => r.GetDatabase(It.IsAny<int>(), It.IsAny<object?>())).Returns(_databaseMock.Object);
        _sut = new CacheService(_redisMock.Object, _bloomFilterMock.Object, _loggerMock.Object);
    }

    /// <summary>
    /// T25：传入裸 pattern（不带 leno:cache: 前缀）时，传给 SCAN 的实际 pattern 应自动添加前缀。
    /// </summary>
    [Fact]
    public async Task InvalidatePatternAsync_BarePattern_ShouldPrependKeyPrefix()
    {
        // Arrange
        var serverMock = CreateServerMock(Array.Empty<RedisKey>());
        _redisMock.Setup(r => r.GetServers()).Returns(new[] { serverMock.Object });

        RedisValue capturedPattern = RedisValue.Null;
        serverMock
            .Setup(s => s.KeysAsync(
                It.IsAny<int>(),
                It.IsAny<RedisValue>(),
                It.IsAny<int>(),
                It.IsAny<long>(),
                It.IsAny<int>(),
                It.IsAny<CommandFlags>()))
            .Callback<int, RedisValue, int, long, int, CommandFlags>((_, pattern, _, _, _, _) => capturedPattern = pattern)
            .Returns(CreateKeyAsyncEnumerable(Array.Empty<RedisKey>()));

        // Act
        await _sut.InvalidatePatternAsync("user:*");

        // Assert：实际传给 SCAN 的 pattern 应为 "leno:cache:user:*"
        capturedPattern.ToString().Should().Be(KeyPrefix + "user:*");
    }

    /// <summary>
    /// T25：传入已带前缀的 pattern 时，不应重复拼接 KeyPrefix。
    /// </summary>
    [Fact]
    public async Task InvalidatePatternAsync_AlreadyPrefixed_ShouldNotDoublePrepend()
    {
        // Arrange
        var serverMock = CreateServerMock(Array.Empty<RedisKey>());
        _redisMock.Setup(r => r.GetServers()).Returns(new[] { serverMock.Object });

        RedisValue capturedPattern = RedisValue.Null;
        serverMock
            .Setup(s => s.KeysAsync(
                It.IsAny<int>(),
                It.IsAny<RedisValue>(),
                It.IsAny<int>(),
                It.IsAny<long>(),
                It.IsAny<int>(),
                It.IsAny<CommandFlags>()))
            .Callback<int, RedisValue, int, long, int, CommandFlags>((_, pattern, _, _, _, _) => capturedPattern = pattern)
            .Returns(CreateKeyAsyncEnumerable(Array.Empty<RedisKey>()));

        var inputPattern = KeyPrefix + "user:*";

        // Act
        await _sut.InvalidatePatternAsync(inputPattern);

        // Assert：实际传给 SCAN 的 pattern 应保持原值，不重复拼接
        capturedPattern.ToString().Should().Be(inputPattern);
    }

    /// <summary>
    /// T25：pattern 包含 ".." 路径穿越片段时应抛 ArgumentException。
    /// </summary>
    [Theory]
    [InlineData("../etc/passwd")]
    [InlineData("user/../secret:*")]
    [InlineData("foo..bar:*")] // 即使是相邻字段名也拒绝，防止绕过
    public async Task InvalidatePatternAsync_ContainsDoubleDot_ShouldThrow(string pattern)
    {
        // Arrange：即使有可用主节点也不应到达 SCAN
        var serverMock = CreateServerMock(Array.Empty<RedisKey>());
        _redisMock.Setup(r => r.GetServers()).Returns(new[] { serverMock.Object });

        // Act
        var act = () => _sut.InvalidatePatternAsync(pattern);

        // Assert
        await act.Should().ThrowAsync<ArgumentException>()
            .WithParameterName("pattern");

        // 验证未调用 SCAN
        serverMock.Verify(
            s => s.KeysAsync(
                It.IsAny<int>(),
                It.IsAny<RedisValue>(),
                It.IsAny<int>(),
                It.IsAny<long>(),
                It.IsAny<int>(),
                It.IsAny<CommandFlags>()),
            Times.Never);
    }

    /// <summary>
    /// T25：null pattern 应抛 ArgumentNullException（保持原有契约）。
    /// </summary>
    [Fact]
    public async Task InvalidatePatternAsync_NullPattern_ShouldThrowArgumentNullException()
    {
        var act = () => _sut.InvalidatePatternAsync(null!);

        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    /// <summary>
    /// T25：空字符串或仅空白字符的 pattern 应抛 ArgumentException。
    /// </summary>
    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("\t")]
    public async Task InvalidatePatternAsync_EmptyOrWhitespacePattern_ShouldThrow(string pattern)
    {
        var act = () => _sut.InvalidatePatternAsync(pattern);

        await act.Should().ThrowAsync<ArgumentException>()
            .WithParameterName("pattern");
    }

    /// <summary>
    /// T25：带前缀的 pattern 与裸 pattern 应产生相同的 SCAN 调用，
    /// 即调用方无论是否手动加前缀，最终行为一致。
    /// </summary>
    [Fact]
    public async Task InvalidatePatternAsync_BareAndPrefixed_ShouldProduceSameEffectivePattern()
    {
        // Arrange：两次调用使用不同 server mock，分别捕获 pattern
        var serverMock1 = CreateServerMock(Array.Empty<RedisKey>());
        var serverMock2 = CreateServerMock(Array.Empty<RedisKey>());

        RedisValue capturedPattern1 = RedisValue.Null;
        RedisValue capturedPattern2 = RedisValue.Null;

        serverMock1
            .Setup(s => s.KeysAsync(
                It.IsAny<int>(), It.IsAny<RedisValue>(), It.IsAny<int>(),
                It.IsAny<long>(), It.IsAny<int>(), It.IsAny<CommandFlags>()))
            .Callback<int, RedisValue, int, long, int, CommandFlags>((_, pattern, _, _, _, _) => capturedPattern1 = pattern)
            .Returns(CreateKeyAsyncEnumerable(Array.Empty<RedisKey>()));

        serverMock2
            .Setup(s => s.KeysAsync(
                It.IsAny<int>(), It.IsAny<RedisValue>(), It.IsAny<int>(),
                It.IsAny<long>(), It.IsAny<int>(), It.IsAny<CommandFlags>()))
            .Callback<int, RedisValue, int, long, int, CommandFlags>((_, pattern, _, _, _, _) => capturedPattern2 = pattern)
            .Returns(CreateKeyAsyncEnumerable(Array.Empty<RedisKey>()));

        // Act
        _redisMock.Setup(r => r.GetServers()).Returns(new[] { serverMock1.Object });
        await _sut.InvalidatePatternAsync("product:*");

        _redisMock.Setup(r => r.GetServers()).Returns(new[] { serverMock2.Object });
        await _sut.InvalidatePatternAsync(KeyPrefix + "product:*");

        // Assert：两次调用最终传给 SCAN 的 pattern 相同
        capturedPattern1.ToString().Should().Be(capturedPattern2.ToString());
        capturedPattern1.ToString().Should().Be(KeyPrefix + "product:*");
    }

    /// <summary>
    /// T25：删除日志应输出带前缀的 effectivePattern，便于运维排查实际删除范围。
    /// </summary>
    [Fact]
    public async Task InvalidatePatternAsync_LogShouldContainPrefixedPattern()
    {
        // Arrange
        var serverMock = CreateServerMock(new[] { (RedisKey)(KeyPrefix + "user:1") });
        _redisMock.Setup(r => r.GetServers()).Returns(new[] { serverMock.Object });
        _databaseMock
            .Setup(d => d.ExecuteAsync("UNLINK", It.IsAny<ICollection<object>>(), It.IsAny<CommandFlags>()))
            .ReturnsAsync(RedisResult.Create(1));

        string? loggedPattern = null;
        _loggerMock
            .Setup(x => x.Log(
                It.IsAny<LogLevel>(),
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => true),
                It.IsAny<Exception?>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()))
            .Callback<LogLevel, EventId, object, Exception?, Delegate>((level, _, state, _, _) =>
            {
                if (level == LogLevel.Information)
                {
                    var msg = state.ToString();
                    if (msg != null && msg.Contains("Pattern="))
                    {
                        loggedPattern = msg;
                    }
                }
            });

        // Act
        await _sut.InvalidatePatternAsync("user:*");

        // Assert：日志中应包含带前缀的 pattern
        loggedPattern.Should().NotBeNull();
        loggedPattern!.Should().Contain(KeyPrefix + "user:*");
        // 不应包含未带前缀的"裸" Pattern= 值
        loggedPattern.Should().NotContain("Pattern=user:*");
    }

    // ===== 辅助方法（与 CacheServiceTests 中保持一致，独立副本以避免耦合） =====

    private static Mock<IServer> CreateServerMock(RedisKey[] keys)
    {
        var serverMock = new Mock<IServer>();
        serverMock.SetupGet(s => s.IsReplica).Returns(false);
        serverMock
            .Setup(s => s.KeysAsync(
                It.IsAny<int>(),
                It.IsAny<RedisValue>(),
                It.IsAny<int>(),
                It.IsAny<long>(),
                It.IsAny<int>(),
                It.IsAny<CommandFlags>()))
            .Returns(CreateKeyAsyncEnumerable(keys));
        return serverMock;
    }

    private static async IAsyncEnumerable<RedisKey> CreateKeyAsyncEnumerable(IEnumerable<RedisKey> keys)
    {
        foreach (var key in keys)
        {
            await Task.Yield();
            yield return key;
        }
    }
}
