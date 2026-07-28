using Leno.Infrastructure.Abstractions.Sessions;
using Leno.SystemAdmin.Application.Services;
using Leno.SystemAdmin.Domain.Exceptions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using StackExchange.Redis;

namespace Leno.SystemAdmin.Application.Tests.Services;

public sealed class OnlineUserAppServiceTests
{
    private readonly Mock<IUserSessionStore> _store = new();
    private readonly OnlineUserAppService _service;

    public OnlineUserAppServiceTests()
    {
        _service = new OnlineUserAppService(_store.Object, NullLogger<OnlineUserAppService>.Instance);
    }

    [Fact]
    public async Task QueryAsync_DerivesSessionDurationMs()
    {
        var session = new OnlineUserSession
        {
            SessionId = "s1", UserId = Guid.NewGuid(), Username = "u1",
            LoginAt = DateTime.UtcNow.AddHours(-1), LastActivityAt = DateTime.UtcNow
        };
        _store.Setup(s => s.QueryAsync(It.IsAny<OnlineUserQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<OnlineUserSession> { session });

        var result = await _service.QueryAsync(new OnlineUserQuery(), default);

        result.Total.Should().Be(1);
        result.Items[0].SessionDurationMs.Should().BeGreaterThan(3_500_000);
    }

    [Fact]
    public async Task QueryAsync_FiltersByUsername()
    {
        var sessions = new List<OnlineUserSession>
        {
            new() { SessionId = "s1", Username = "admin" },
            new() { SessionId = "s2", Username = "user1" },
            new() { SessionId = "s3", Username = "admin2" }
        };
        _store.Setup(s => s.QueryAsync(It.IsAny<OnlineUserQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(sessions);

        var result = await _service.QueryAsync(new OnlineUserQuery { Username = "admin" }, default);

        result.Items.Should().OnlyContain(s => s.Username.Contains("admin"));
    }

    [Fact]
    public async Task GetStatsAsync_ReturnsThreeMetrics()
    {
        _store.Setup(s => s.GetStatsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new OnlineUserStats { Total = 5, Logins24h = 3, Anomalies = 1 });

        var stats = await _service.GetStatsAsync(default);

        stats.Total.Should().Be(5);
        stats.Logins24h.Should().Be(3);
        stats.Anomalies.Should().Be(1);
    }

    [Fact]
    public async Task ForceOfflineAsync_SelfSession_ThrowsForbiddenException()
    {
        var act = () => _service.ForceOfflineAsync("my-session", "my-session", default);

        await act.Should().ThrowAsync<SystemAdminDomainException>()
            .Where(e => e.ErrorCode == "ONLINE_USER_FORCE_OFFLINE_SELF_FORBIDDEN");
    }

    [Fact]
    public async Task ForceOfflineAsync_OtherSession_CallsStoreRemoveAsync()
    {
        await _service.ForceOfflineAsync("other-session", "my-session", default);

        _store.Verify(s => s.RemoveAsync("other-session", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task QueryAsync_RedisUnavailable_ReturnsEmptyList()
    {
        _store.Setup(s => s.QueryAsync(It.IsAny<OnlineUserQuery>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new RedisConnectionException(ConnectionFailureType.UnableToConnect, "Redis down"));

        var result = await _service.QueryAsync(new OnlineUserQuery(), default);

        result.Items.Should().BeEmpty();
        result.Total.Should().Be(0);
    }
}
