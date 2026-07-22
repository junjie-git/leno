using Leno.UserAuth.Application.Abstractions;
using Leno.UserAuth.Infrastructure.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using StackExchange.Redis;
using Xunit;

namespace Leno.UserAuth.Infrastructure.Tests.Services;

public sealed class RedisRefreshTokenStoreTests
{
    private readonly Mock<IConnectionMultiplexer> _multiplexerMock = new();
    private readonly Mock<IDatabase> _databaseMock = new();
    private static readonly TimeSpan DefaultTtl = TimeSpan.FromHours(2);

    public RedisRefreshTokenStoreTests()
    {
        _multiplexerMock
            .Setup(m => m.GetDatabase(It.IsAny<int>(), It.IsAny<object?>()))
            .Returns(_databaseMock.Object);
    }

    [Fact]
    public async Task IssueAsync_Should_Store_Token_With_Ttl_In_Redis()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var store = new RedisRefreshTokenStore(_multiplexerMock.Object, DefaultTtl, NullLogger<RedisRefreshTokenStore>.Instance);

        string? capturedKey = null;
        TimeSpan? capturedTtl = null;
        _databaseMock
            .Setup(d => d.StringSetAsync(
                It.IsAny<RedisKey>(),
                It.IsAny<RedisValue>(),
                It.IsAny<TimeSpan?>(),
                It.IsAny<When>(),
                It.IsAny<CommandFlags>()))
            .Callback<RedisKey, RedisValue, TimeSpan?, When, CommandFlags>((k, v, ttl, _, _) =>
            {
                capturedKey = k.ToString();
                capturedTtl = ttl;
            })
            .ReturnsAsync(true);

        // Act
        var token = await store.IssueAsync(userId, CancellationToken.None);

        // Assert
        Assert.False(string.IsNullOrEmpty(token));
        Assert.Contains($"leno:userauth:refresh:{userId}:date:", capturedKey);
        Assert.Equal(DefaultTtl, capturedTtl);
        _databaseMock.Verify(d => d.StringSetAsync(
            It.IsAny<RedisKey>(),
            It.IsAny<RedisValue>(),
            DefaultTtl,
            It.IsAny<When>(),
            It.IsAny<CommandFlags>()), Times.Once);
    }

    [Fact]
    public async Task ValidateAndRotateAsync_Should_Return_UserId_When_Token_Valid_And_Delete_Atomic()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var store = new RedisRefreshTokenStore(_multiplexerMock.Object, DefaultTtl, NullLogger<RedisRefreshTokenStore>.Instance);
        var storedValue = userId.ToString();

        // 先签发，拿到 token
        _databaseMock
            .Setup(d => d.StringSetAsync(
                It.IsAny<RedisKey>(),
                It.IsAny<RedisValue>(),
                It.IsAny<TimeSpan?>(),
                It.IsAny<When>(),
                It.IsAny<CommandFlags>()))
            .ReturnsAsync(true);

        _databaseMock
            .Setup(d => d.ScriptEvaluateAsync(
                It.IsAny<string>(),
                It.IsAny<RedisKey[]?>(),
                It.IsAny<RedisValue[]?>(),
                It.IsAny<CommandFlags>()))
            .ReturnsAsync(RedisResult.Create((RedisValue)storedValue, ResultType.BulkString));

        var token = await store.IssueAsync(userId, CancellationToken.None);

        // Act
        var result = await store.ValidateAndRotateAsync(token, CancellationToken.None);

        // Assert
        Assert.Equal(userId, result);
        _databaseMock.Verify(d => d.ScriptEvaluateAsync(
            It.Is<string>(s => s.Contains("GETDEL", StringComparison.Ordinal)),
            It.IsAny<RedisKey[]?>(),
            It.IsAny<RedisValue[]?>(),
            It.IsAny<CommandFlags>()), Times.Once);
    }

    [Fact]
    public async Task ValidateAndRotateAsync_Should_Return_Null_When_Token_Not_Found()
    {
        // Arrange
        var store = new RedisRefreshTokenStore(_multiplexerMock.Object, DefaultTtl, NullLogger<RedisRefreshTokenStore>.Instance);

        _databaseMock
            .Setup(d => d.ScriptEvaluateAsync(
                It.IsAny<string>(),
                It.IsAny<RedisKey[]?>(),
                It.IsAny<RedisValue[]?>(),
                It.IsAny<CommandFlags>()))
            .ReturnsAsync(RedisResult.Create(RedisValue.Null, ResultType.BulkString));

        // 用一个合法格式的 token 触发脚本调用
        var fakeToken = GenerateTokenForTest(Guid.NewGuid());

        // Act
        var result = await store.ValidateAndRotateAsync(fakeToken, CancellationToken.None);

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public async Task ValidateAndRotateAsync_Should_Return_Null_When_Token_Format_Invalid()
    {
        // Arrange
        var store = new RedisRefreshTokenStore(_multiplexerMock.Object, DefaultTtl, NullLogger<RedisRefreshTokenStore>.Instance);

        // Act
        var result = await store.ValidateAndRotateAsync("not-a-valid-token", CancellationToken.None);

        // Assert
        Assert.Null(result);
        _databaseMock.Verify(d => d.ScriptEvaluateAsync(
            It.IsAny<string>(),
            It.IsAny<RedisKey[]?>(),
            It.IsAny<RedisValue[]?>(),
            It.IsAny<CommandFlags>()), Times.Never);
    }

    [Fact]
    public async Task ValidateAndRotateAsync_Should_Return_Null_When_Token_Empty()
    {
        var store = new RedisRefreshTokenStore(_multiplexerMock.Object, DefaultTtl, NullLogger<RedisRefreshTokenStore>.Instance);

        Assert.Null(await store.ValidateAndRotateAsync(string.Empty, CancellationToken.None));
        Assert.Null(await store.ValidateAndRotateAsync(null!, CancellationToken.None));
    }

    [Fact]
    public async Task RevokeAllAsync_Should_Delete_All_Matched_Keys_For_User()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var store = new RedisRefreshTokenStore(_multiplexerMock.Object, DefaultTtl, NullLogger<RedisRefreshTokenStore>.Instance);

        var endpoint = new System.Net.DnsEndPoint("localhost", 6379);
        var serverMock = new Mock<IServer>();
        serverMock.SetupGet(s => s.IsReplica).Returns(false);

        var keys = new RedisKey[]
        {
            $"leno:userauth:refresh:{userId}:date:1",
            $"leno:userauth:refresh:{userId}:date:2"
        };

        serverMock
            .Setup(s => s.KeysAsync(
                It.IsAny<int>(),
                It.IsAny<RedisValue>(),
                It.IsAny<int>(),
                It.IsAny<long>(),
                It.IsAny<int>(),
                It.IsAny<CommandFlags>()))
            .Returns(CreateKeyAsyncEnumerable(keys));

        _multiplexerMock.Setup(m => m.GetEndPoints()).Returns(new System.Net.EndPoint[] { endpoint });
        _multiplexerMock.Setup(m => m.GetServer(endpoint, It.IsAny<object?>())).Returns(serverMock.Object);

        _databaseMock
            .Setup(d => d.KeyDeleteAsync(It.IsAny<RedisKey[]>(), It.IsAny<CommandFlags>()))
            .ReturnsAsync(2);

        // Act
        await store.RevokeAllAsync(userId, CancellationToken.None);

        // Assert
        _databaseMock.Verify(d => d.KeyDeleteAsync(
            It.Is<RedisKey[]>(arr => arr.Length == 2 && arr.All(k => k.ToString().Contains(userId.ToString()))),
            It.IsAny<CommandFlags>()), Times.Once);
    }

    [Fact]
    public async Task RevokeAllAsync_Should_Skip_Replica_Servers()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var store = new RedisRefreshTokenStore(_multiplexerMock.Object, DefaultTtl, NullLogger<RedisRefreshTokenStore>.Instance);

        var endpoint = new System.Net.DnsEndPoint("replica", 6379);
        var serverMock = new Mock<IServer>();
        serverMock.SetupGet(s => s.IsReplica).Returns(true);

        _multiplexerMock.Setup(m => m.GetEndPoints()).Returns(new System.Net.EndPoint[] { endpoint });
        _multiplexerMock.Setup(m => m.GetServer(endpoint, It.IsAny<object?>())).Returns(serverMock.Object);

        // Act
        await store.RevokeAllAsync(userId, CancellationToken.None);

        // Assert：不应在副本上调用 KeysAsync
        serverMock.Verify(s => s.KeysAsync(
            It.IsAny<int>(),
            It.IsAny<RedisValue>(),
            It.IsAny<int>(),
            It.IsAny<long>(),
            It.IsAny<int>(),
            It.IsAny<CommandFlags>()), Times.Never);
        _databaseMock.Verify(d => d.KeyDeleteAsync(It.IsAny<RedisKey[]>(), It.IsAny<CommandFlags>()), Times.Never);
    }

    [Fact]
    public async Task RevokeAllAsync_Should_Do_Nothing_When_No_Keys_Match()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var store = new RedisRefreshTokenStore(_multiplexerMock.Object, DefaultTtl, NullLogger<RedisRefreshTokenStore>.Instance);

        var endpoint = new System.Net.DnsEndPoint("localhost", 6379);
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
            .Returns(CreateKeyAsyncEnumerable(Array.Empty<RedisKey>()));

        _multiplexerMock.Setup(m => m.GetEndPoints()).Returns(new System.Net.EndPoint[] { endpoint });
        _multiplexerMock.Setup(m => m.GetServer(endpoint, It.IsAny<object?>())).Returns(serverMock.Object);

        // Act
        await store.RevokeAllAsync(userId, CancellationToken.None);

        // Assert
        _databaseMock.Verify(d => d.KeyDeleteAsync(It.IsAny<RedisKey[]>(), It.IsAny<CommandFlags>()), Times.Never);
    }

    [Fact]
    public void Constructor_Should_Throw_When_Redis_Null()
    {
        Assert.Throws<ArgumentNullException>(() =>
            new RedisRefreshTokenStore(null!, DefaultTtl, NullLogger<RedisRefreshTokenStore>.Instance));
    }

    [Fact]
    public void Constructor_Should_Throw_When_Logger_Null()
    {
        Assert.Throws<ArgumentNullException>(() =>
            new RedisRefreshTokenStore(_multiplexerMock.Object, DefaultTtl, null!));
    }

    [Fact]
    public void Constructor_Should_Throw_When_Expiry_NonPositive()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new RedisRefreshTokenStore(_multiplexerMock.Object, TimeSpan.Zero, NullLogger<RedisRefreshTokenStore>.Instance));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new RedisRefreshTokenStore(_multiplexerMock.Object, TimeSpan.FromSeconds(-1), NullLogger<RedisRefreshTokenStore>.Instance));
    }

    /// <summary>
    /// 生成与生产实现相同格式的测试 token：Base64Url(userIdBytes|randomBytes)。
    /// </summary>
    private static string GenerateTokenForTest(Guid userId)
    {
        Span<byte> buffer = stackalloc byte[48];
        userId.TryWriteBytes(buffer);
        System.Security.Cryptography.RandomNumberGenerator.Fill(buffer.Slice(16));
        return Convert.ToBase64String(buffer)
            .Replace('+', '-')
            .Replace('/', '_')
            .TrimEnd('=');
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
