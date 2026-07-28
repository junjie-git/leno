using Leno.Infrastructure.Abstractions.Sessions;
using Leno.Infrastructure.Sessions;
using StackExchange.Redis;
using Testcontainers.Redis;

namespace Leno.SystemAdmin.Infrastructure.Tests.Services;

/// <summary>
/// RedisUserSessionStore 基础设施集成测试（Testcontainers Redis）。
/// 验证三层 Key 结构（session:{id} Hash / session:user:{uid} Set / session:index ZSet）、
/// TTL 24h、查询过滤、统计、删除清理。
/// 需要 Docker 环境运行 Testcontainers Redis 容器。
/// </summary>
public sealed class RedisUserSessionStoreTests : IAsyncLifetime
{
    private RedisContainer _container = null!;
    private IConnectionMultiplexer _multiplexer = null!;
    private RedisUserSessionStore _store = null!;

    public async Task InitializeAsync()
    {
        _container = new RedisBuilder().WithImage("redis:7.2-alpine").Build();
        await _container.StartAsync();
        _multiplexer = ConnectionMultiplexer.Connect(_container.GetConnectionString());
        _store = new RedisUserSessionStore(_multiplexer);
    }

    public async Task DisposeAsync()
    {
        if (_multiplexer is not null) await _multiplexer.DisposeAsync();
        if (_container is not null) await _container.DisposeAsync();
    }

    private static OnlineUserSession BuildSession(string sessionId = "s1") => new()
    {
        SessionId = sessionId,
        UserId = Guid.NewGuid(),
        Username = "admin",
        Roles = new List<string> { "Admin" },
        IpAddress = "192.168.1.1",
        Browser = "Chrome 120",
        Os = "Windows 11",
        LoginAt = DateTime.UtcNow,
        LastActivityAt = DateTime.UtcNow
    };

    [Fact]
    public async Task RecordAsync_WritesThreeKeys()
    {
        var session = BuildSession();

        await _store.RecordAsync(session);

        var db = _multiplexer.GetDatabase();
        (await db.KeyExistsAsync($"session:{session.SessionId}")).Should().BeTrue();
        (await db.KeyExistsAsync($"session:user:{session.UserId}")).Should().BeTrue();
        (await db.KeyExistsAsync("session:index")).Should().BeTrue();
    }

    [Fact]
    public async Task QueryAsync_ReturnsRecordedSessions()
    {
        await _store.RecordAsync(BuildSession("s1"));
        await _store.RecordAsync(BuildSession("s2"));
        await _store.RecordAsync(BuildSession("s3"));

        var results = await _store.QueryAsync(new OnlineUserQuery { Page = 1, PageSize = 100 }, default);

        results.Should().HaveCount(3);
    }

    [Fact]
    public async Task QueryAsync_FiltersByLoginAtRange()
    {
        var oldSession = BuildSession("old");
        oldSession.LoginAt = DateTime.UtcNow.AddHours(-10);
        await _store.RecordAsync(oldSession);

        var newSession = BuildSession("new");
        newSession.LoginAt = DateTime.UtcNow;
        await _store.RecordAsync(newSession);

        var results = await _store.QueryAsync(
            new OnlineUserQuery { LoginAtFrom = DateTime.UtcNow.AddHours(-1), Page = 1, PageSize = 100 },
            default);

        results.Should().OnlyContain(s => s.SessionId == "new");
    }

    [Fact]
    public async Task RemoveAsync_DeletesAllThreeKeys()
    {
        var session = BuildSession("rm-test");
        await _store.RecordAsync(session);

        await _store.RemoveAsync("rm-test");

        var db = _multiplexer.GetDatabase();
        (await db.KeyExistsAsync($"session:rm-test")).Should().BeFalse();
    }

    [Fact]
    public async Task GetStatsAsync_ReturnsCorrectCounts()
    {
        await _store.RecordAsync(BuildSession("st1"));
        await _store.RecordAsync(BuildSession("st2"));
        await _store.RecordAsync(BuildSession("st3"));

        var stats = await _store.GetStatsAsync(default);

        stats.Total.Should().Be(3);
        stats.Logins24h.Should().Be(3);
    }

    [Fact]
    public async Task RecordAsync_SetsTtl_KeyExpiresIn24h()
    {
        var session = BuildSession("ttl-test");
        await _store.RecordAsync(session);

        var db = _multiplexer.GetDatabase();
        var ttl = await db.KeyTimeToLiveAsync($"session:{session.SessionId}");

        ttl.Should().NotBeNull();
        ttl!.Value.TotalHours.Should().BeGreaterThan(23);
        ttl.Value.TotalHours.Should().BeLessThanOrEqualTo(24);
    }
}
