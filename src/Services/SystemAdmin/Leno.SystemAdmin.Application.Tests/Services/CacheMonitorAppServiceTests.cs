using Leno.SystemAdmin.Application.Services;
using Leno.SystemAdmin.Domain.Exceptions;
using Leno.SystemAdmin.Domain.Services;
using Leno.SystemAdmin.Domain.ValueObjects;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using StackExchange.Redis;

namespace Leno.SystemAdmin.Application.Tests.Services;

public sealed class CacheMonitorAppServiceTests
{
    private readonly Mock<IRedisCacheMonitor> _monitor = new();
    private readonly CacheMonitorAppService _service;

    public CacheMonitorAppServiceTests()
    {
        _service = new CacheMonitorAppService(_monitor.Object, NullLogger<CacheMonitorAppService>.Instance);
    }

    [Fact]
    public async Task GetRedisInfoAsync_MapsAllFields()
    {
        _monitor.Setup(m => m.GetInfoAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(BuildSampleRedisInfo());

        var info = await _service.GetRedisInfoAsync(default);

        info.RedisVersion.Should().Be("7.2.0");
        info.UptimeInDays.Should().BeGreaterThan(0);
        info.ConnectedClients.Should().BeGreaterThan(0);
        info.UsedMemoryHuman.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task GetKeyspacesAsync_Returns16Dbs()
    {
        _monitor.Setup(m => m.GetKeyspacesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(BuildSampleKeyspaces());

        var keyspaces = await _service.GetKeyspacesAsync(default);

        keyspaces.Should().HaveCount(16);
        keyspaces[0].Db.Should().Be(0);
    }

    [Fact]
    public async Task QueryKeysAsync_PatternMatch_FiltersByPattern()
    {
        var keys = new List<RedisKeyDto>
        {
            new() { Key = "user:1", Type = "string" },
            new() { Key = "user:2", Type = "string" }
        };
        _monitor.Setup(m => m.ScanKeysAsync(0, "user:*", It.IsAny<string?>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PagedResult<RedisKeyDto> { Items = keys, Total = 2, Page = 1, PageSize = 20 });

        var result = await _service.QueryKeysAsync(0, "user:*", null, 1, 20, default);

        result.Items.Should().OnlyContain(k => k.Key.StartsWith("user:"));
        result.Total.Should().Be(2);
    }

    [Fact]
    public async Task QueryKeysAsync_TypeFilter_FiltersByType()
    {
        var keys = new List<RedisKeyDto>
        {
            new() { Key = "h1", Type = "hash" }
        };
        _monitor.Setup(m => m.ScanKeysAsync(0, "*", "hash", It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PagedResult<RedisKeyDto> { Items = keys, Total = 1, Page = 1, PageSize = 20 });

        var result = await _service.QueryKeysAsync(0, "*", "hash", 1, 20, default);

        result.Items.Should().OnlyContain(k => k.Type == "hash");
    }

    [Fact]
    public async Task GetKeyDetailAsync_StringType_ReturnsValue()
    {
        _monitor.Setup(m => m.GetKeyDetailAsync("mykey", 0, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new RedisKeyDetailDto { Key = "mykey", Type = "string", Value = "hello", Ttl = -1 });

        var detail = await _service.GetKeyDetailAsync("mykey", 0, default);

        detail.Should().NotBeNull();
        detail!.Type.Should().Be("string");
        detail.Value.Should().Be("hello");
    }

    [Fact]
    public async Task GetKeyDetailAsync_HashType_ReturnsDetail()
    {
        _monitor.Setup(m => m.GetKeyDetailAsync("h", 0, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new RedisKeyDetailDto { Key = "h", Type = "hash", Value = "{\"f1\":\"v1\"}", Ttl = -1 });

        var detail = await _service.GetKeyDetailAsync("h", 0, default);

        detail.Should().NotBeNull();
        detail!.Type.Should().Be("hash");
        detail.Value.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task GetKeyDetailAsync_KeyNotFound_ReturnsNull()
    {
        _monitor.Setup(m => m.GetKeyDetailAsync("missing", 0, It.IsAny<CancellationToken>()))
            .ReturnsAsync((RedisKeyDetailDto?)null);

        var detail = await _service.GetKeyDetailAsync("missing", 0, default);

        detail.Should().BeNull();
    }

    [Fact]
    public async Task DeleteKeyAsync_ExistingKey_ReturnsTrue()
    {
        _monitor.Setup(m => m.DeleteKeyAsync("mykey", 0, It.IsAny<CancellationToken>())).ReturnsAsync(true);

        var result = await _service.DeleteKeyAsync("mykey", 0, default);

        result.Deleted.Should().BeTrue();
    }

    [Fact]
    public async Task GetRedisInfoAsync_RedisUnavailable_ThrowsServiceUnavailableException()
    {
        _monitor.Setup(m => m.GetInfoAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(new RedisConnectionException(ConnectionFailureType.UnableToConnect, "Redis down"));

        var act = () => _service.GetRedisInfoAsync(default);

        await act.Should().ThrowAsync<SystemAdminDomainException>()
            .Where(e => e.ErrorCode == "CACHE_REDIS_UNAVAILABLE");
    }

    private static RedisInfoDto BuildSampleRedisInfo() => new()
    {
        RedisVersion = "7.2.0",
        RedisMode = "standalone",
        Os = "Linux",
        ArchBits = "64",
        UptimeInDays = 5,
        ConnectedClients = 10,
        UsedMemoryHuman = "1.5M",
        MaxmemoryHuman = "0",
        UsedMemoryPeakHuman = "2.0M",
        TotalConnectionsReceived = 1000,
        TotalCommandsProcessed = 50000,
        KeyspaceHits = 8000,
        KeyspaceMisses = 200
    };

    private static List<KeyspaceDto> BuildSampleKeyspaces() =>
        Enumerable.Range(0, 16).Select(i => new KeyspaceDto { Db = i, Keys = 0, Expires = 0, AvgTtl = 0 }).ToList();
}
