using System.Net;
using System.Net.Http.Json;
using Leno.SharedContracts.Responses;
using Leno.SystemAdmin.Application;
using Leno.SystemAdmin.Application.DTOs;
using Leno.SystemAdmin.Domain.Exceptions;
using Leno.SystemAdmin.Domain.ValueObjects;
using Moq;

namespace Leno.SystemAdmin.Api.Tests.Controllers;

/// <summary>
/// CacheController 集成测试（Task 7.16，8 用例）。
/// 覆盖 Redis INFO、keyspace、key 列表、key 详情、删除 key 5 个端点，
/// 验证 200/400/401/403/404/503 状态码与 ApiResponse 包装。
/// </summary>
public class CacheControllerTests : IClassFixture<SystemAdminApiFactory>
{
    private readonly SystemAdminApiFactory _factory;
    private readonly HttpClient _client;

    public CacheControllerTests(SystemAdminApiFactory factory)
    {
        _factory = factory;
        _client = factory.CreateAdminClient();
    }

    #region GET /api/admin/cache/info

    [Fact]
    public async Task GetInfo_WithAdminRole_ShouldReturn200()
    {
        var info = new RedisInfoDto
        {
            RedisVersion = "7.0.5",
            RedisMode = "standalone",
            Os = "Linux",
            ArchBits = "64",
            TcpPort = 6379,
            UptimeInDays = 30,
            ConnectedClients = 15,
            UsedMemoryHuman = "2.5M",
            UsedMemoryPeakHuman = "5.0M",
            MaxmemoryHuman = "256M",
            MemFragmentationRatio = 1.2,
            TotalConnectionsReceived = 10000,
            TotalCommandsProcessed = 50000,
            KeyspaceHits = 8000,
            KeyspaceMisses = 2000,
            EvictedKeys = 0
        };
        _factory.CacheMonitorAppServiceMock
            .Setup(s => s.GetRedisInfoAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(info);

        var response = await _client.GetAsync("/api/admin/cache/info");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<ApiResponse<RedisInfoDto>>();
        body!.Code.Should().Be(200);
        body.Data!.RedisVersion.Should().Be("7.0.5");
        body.Data.ConnectedClients.Should().Be(15);
    }

    [Fact]
    public async Task GetInfo_WithoutAuth_ShouldReturn401()
    {
        var anonymousClient = _factory.CreateAnonymousClient();
        var response = await anonymousClient.GetAsync("/api/admin/cache/info");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task GetInfo_WhenRedisUnavailable_ShouldReturn503()
    {
        _factory.CacheMonitorAppServiceMock
            .Setup(s => s.GetRedisInfoAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(new SystemAdminDomainException("Redis 暂时不可用", "CACHE_REDIS_UNAVAILABLE"));

        var response = await _client.GetAsync("/api/admin/cache/info");

        response.StatusCode.Should().Be(HttpStatusCode.ServiceUnavailable);
        var body = await response.Content.ReadFromJsonAsync<ApiResponse>();
        body!.Code.Should().Be(503);
    }

    #endregion

    #region GET /api/admin/cache/keyspaces

    [Fact]
    public async Task GetKeyspaces_WithAdminRole_ShouldReturn200()
    {
        var keyspaces = new List<KeyspaceDto>
        {
            new() { Db = 0, Keys = 150, Expires = 30, AvgTtl = 3600000 },
            new() { Db = 1, Keys = 50, Expires = 10, AvgTtl = 1800000 }
        };
        _factory.CacheMonitorAppServiceMock
            .Setup(s => s.GetKeyspacesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(keyspaces);

        var response = await _client.GetAsync("/api/admin/cache/keyspaces");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<ApiResponse<List<KeyspaceDto>>>();
        body!.Code.Should().Be(200);
        body.Data.Should().HaveCount(2);
        body.Data![0].Keys.Should().Be(150);
    }

    #endregion

    #region GET /api/admin/cache/keys

    [Fact]
    public async Task QueryKeys_WithValidParams_ShouldReturn200()
    {
        var result = new CacheKeyQueryResultDto
        {
            Items = new List<RedisKeyDto>
            {
                new() { Key = "user:session:001", Type = "string", Size = 1, Ttl = 3600 },
                new() { Key = "user:session:002", Type = "string", Size = 1, Ttl = -1 }
            },
            Total = 2,
            Page = 1,
            PageSize = 20
        };
        _factory.CacheMonitorAppServiceMock
            .Setup(s => s.QueryKeysAsync(0, "*", null, 1, 20, It.IsAny<CancellationToken>()))
            .ReturnsAsync(result);

        var response = await _client.GetAsync("/api/admin/cache/keys?db=0&pattern=*&page=1&pageSize=20");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<ApiResponse<CacheKeyQueryResultDto>>();
        body!.Code.Should().Be(200);
        body.Data!.Total.Should().Be(2);
        body.Data.Items.Should().HaveCount(2);
    }

    [Fact]
    public async Task QueryKeys_WithOperatorRole_ShouldReturn403()
    {
        var operatorClient = _factory.CreateClientWithRole("Operator");
        var response = await operatorClient.GetAsync("/api/admin/cache/keys");

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    #endregion

    #region GET /api/admin/cache/keys/{key}

    [Fact]
    public async Task GetKeyDetail_WithValidKey_ShouldReturn200()
    {
        var key = "user:session:001";
        var detail = new RedisKeyDetailDto
        {
            Key = key,
            Type = "string",
            Size = 1,
            Ttl = 3600,
            Value = "{\"userId\":\"00000000-0000-0000-0000-000000000001\"}",
            Truncated = false
        };
        _factory.CacheMonitorAppServiceMock
            .Setup(s => s.GetKeyDetailAsync(key, 0, It.IsAny<CancellationToken>()))
            .ReturnsAsync(detail);

        var response = await _client.GetAsync($"/api/admin/cache/keys/{key}?db=0");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<ApiResponse<RedisKeyDetailDto>>();
        body!.Code.Should().Be(200);
        body.Data!.Key.Should().Be(key);
        body.Data.Type.Should().Be("string");
    }

    [Fact]
    public async Task GetKeyDetail_WithNonExistentKey_ShouldReturn404()
    {
        var key = "non:existent:key";
        _factory.CacheMonitorAppServiceMock
            .Setup(s => s.GetKeyDetailAsync(key, 0, It.IsAny<CancellationToken>()))
            .ReturnsAsync((RedisKeyDetailDto?)null);

        var response = await _client.GetAsync($"/api/admin/cache/keys/{key}?db=0");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        var body = await response.Content.ReadFromJsonAsync<ApiResponse>();
        body!.Code.Should().Be(404);
    }

    #endregion

    #region DELETE /api/admin/cache/keys/{key}

    [Fact]
    public async Task DeleteKey_WithValidKey_ShouldReturn200()
    {
        var key = "user:session:001";
        var deleteResult = new CacheKeyDeleteResultDto { Deleted = true, Key = key };
        _factory.CacheMonitorAppServiceMock
            .Setup(s => s.DeleteKeyAsync(key, 0, It.IsAny<CancellationToken>()))
            .ReturnsAsync(deleteResult);

        var response = await _client.DeleteAsync($"/api/admin/cache/keys/{key}?db=0");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<ApiResponse<CacheKeyDeleteResultDto>>();
        body!.Code.Should().Be(200);
        body.Data!.Deleted.Should().BeTrue();
        body.Data.Key.Should().Be(key);
    }

    #endregion
}
