using Leno.SystemAdmin.Domain.Services;
using Leno.SystemAdmin.Infrastructure.Services;
using StackExchange.Redis;
using Testcontainers.Redis;

namespace Leno.SystemAdmin.Infrastructure.Tests.Services;

/// <summary>
/// RedisCacheMonitorService 基础设施集成测试（Testcontainers Redis）。
/// 验证 INFO 解析、Keyspace 枚举、SCAN 模式/类型过滤、Key 详情读取、Key 删除。
/// 需要 Docker 环境运行 Testcontainers Redis 容器。
/// </summary>
public sealed class RedisCacheMonitorServiceTests : IAsyncLifetime
{
    private RedisContainer _container = null!;
    private IConnectionMultiplexer _multiplexer = null!;
    private RedisCacheMonitorService _service = null!;

    public async Task InitializeAsync()
    {
        _container = new RedisBuilder().WithImage("redis:7.2-alpine").Build();
        await _container.StartAsync();
        _multiplexer = ConnectionMultiplexer.Connect(_container.GetConnectionString());
        _service = new RedisCacheMonitorService(_multiplexer);
    }

    public async Task DisposeAsync()
    {
        if (_multiplexer is not null) await _multiplexer.DisposeAsync();
        if (_container is not null) await _container.DisposeAsync();
    }

    [Fact]
    public async Task GetInfoAsync_ReturnsAllFields()
    {
        var info = await _service.GetInfoAsync(default);

        info.RedisVersion.Should().NotBeNullOrEmpty();
        info.UptimeInDays.Should().BeGreaterThanOrEqualTo(0);
        info.ConnectedClients.Should().BeGreaterThanOrEqualTo(0);
    }

    [Fact]
    public async Task GetKeyspacesAsync_ReturnsDb0ToDb15()
    {
        var keyspaces = await _service.GetKeyspacesAsync(default);

        keyspaces.Should().HaveCount(16);
        keyspaces.Select(k => k.Db).Should().BeEquivalentTo(Enumerable.Range(0, 16));
    }

    [Fact]
    public async Task ScanKeysAsync_PatternStar_ReturnsAllKeys()
    {
        var db = _multiplexer.GetDatabase();
        await db.StringSetAsync("key1", "v1");
        await db.StringSetAsync("key2", "v2");
        await db.StringSetAsync("key3", "v3");

        var result = await _service.ScanKeysAsync(0, "*", null, 1, 100, default);

        result.Total.Should().BeGreaterThanOrEqualTo(3);
    }

    [Fact]
    public async Task ScanKeysAsync_PatternUserPrefix_FiltersCorrectly()
    {
        var db = _multiplexer.GetDatabase();
        await db.StringSetAsync("user:1", "v1");
        await db.StringSetAsync("user:2", "v2");
        await db.StringSetAsync("order:1", "v1");

        var result = await _service.ScanKeysAsync(0, "user:*", null, 1, 100, default);

        result.Items.Should().OnlyContain(k => k.Key.StartsWith("user:"));
    }

    [Fact]
    public async Task ScanKeysAsync_TypeFilter_HashOnly()
    {
        var db = _multiplexer.GetDatabase();
        await db.StringSetAsync("str1", "v");
        await db.HashSetAsync("hash1", new HashEntry[] { new("f", "v") });

        var result = await _service.ScanKeysAsync(0, "*", "hash", 1, 100, default);

        result.Items.Should().OnlyContain(k => k.Type == "hash");
    }

    [Fact]
    public async Task GetKeyDetailAsync_StringType_ReturnsValue()
    {
        var db = _multiplexer.GetDatabase();
        await db.StringSetAsync("mystr", "hello");

        var detail = await _service.GetKeyDetailAsync("mystr", 0, default);

        detail.Should().NotBeNull();
        detail!.Type.Should().Be("string");
        detail.Value.Should().Be("hello");
    }

    [Fact]
    public async Task GetKeyDetailAsync_HashType_ReturnsDictionary()
    {
        var db = _multiplexer.GetDatabase();
        await db.HashSetAsync("myhash", new HashEntry[] { new("f1", "v1"), new("f2", "v2") });

        var detail = await _service.GetKeyDetailAsync("myhash", 0, default);

        detail.Should().NotBeNull();
        detail!.Type.Should().Be("hash");
        detail.Value.Should().Contain("f1").And.Contain("v1");
        detail.Value.Should().Contain("f2").And.Contain("v2");
    }

    [Fact]
    public async Task DeleteKeyAsync_ExistingKey_ReturnsTrue()
    {
        var db = _multiplexer.GetDatabase();
        await db.StringSetAsync("todelete", "v");

        var result = await _service.DeleteKeyAsync("todelete", 0, default);

        result.Should().BeTrue();
        (await db.KeyExistsAsync("todelete")).Should().BeFalse();
    }
}
