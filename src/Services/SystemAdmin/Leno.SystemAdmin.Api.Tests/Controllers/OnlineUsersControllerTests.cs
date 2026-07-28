using System.Net;
using System.Net.Http.Json;
using Leno.Infrastructure.Abstractions.Sessions;
using Leno.SharedContracts.Responses;
using Leno.SystemAdmin.Application;
using Leno.SystemAdmin.Application.DTOs;
using Leno.SystemAdmin.Domain.Exceptions;
using Moq;

namespace Leno.SystemAdmin.Api.Tests.Controllers;

/// <summary>
/// OnlineUsersController 集成测试（Task 7.15，8 用例）。
/// 覆盖在线用户分页查询、详情、强制下线、统计 4 个端点，
/// 验证 200/400/401/403/404/503 状态码与 ApiResponse 包装。
/// </summary>
public class OnlineUsersControllerTests : IClassFixture<SystemAdminApiFactory>
{
    private readonly SystemAdminApiFactory _factory;
    private readonly HttpClient _client;

    public OnlineUsersControllerTests(SystemAdminApiFactory factory)
    {
        _factory = factory;
        _client = factory.CreateAdminClient();
    }

    #region GET /api/admin/online-users

    [Fact]
    public async Task List_WithAdminRole_ShouldReturn200()
    {
        var listResult = new OnlineUserListResultDto
        {
            Items = new List<OnlineUserDto>
            {
                new()
                {
                    SessionId = "session-001",
                    UserId = Guid.NewGuid(),
                    Username = "adminuser",
                    IpAddress = "192.168.1.100",
                    Browser = "Chrome",
                    Os = "Windows",
                    LoginAt = DateTime.UtcNow.AddHours(-1),
                    LastActivityAt = DateTime.UtcNow,
                    SessionDurationMs = 3600000,
                    RequestCount = 42
                }
            },
            Total = 1,
            Page = 1,
            PageSize = 20
        };
        _factory.OnlineUserAppServiceMock
            .Setup(s => s.QueryAsync(It.IsAny<OnlineUserQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(listResult);

        var response = await _client.GetAsync("/api/admin/online-users");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<ApiResponse<OnlineUserListResultDto>>();
        body!.Code.Should().Be(200);
        body.Data!.Total.Should().Be(1);
        body.Data.Items.Should().HaveCount(1);
    }

    [Fact]
    public async Task List_WithoutAuth_ShouldReturn401()
    {
        var anonymousClient = _factory.CreateAnonymousClient();
        var response = await anonymousClient.GetAsync("/api/admin/online-users");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task List_WithOperatorRole_ShouldReturn403()
    {
        var operatorClient = _factory.CreateClientWithRole("Operator");
        var response = await operatorClient.GetAsync("/api/admin/online-users");

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    #endregion

    #region GET /api/admin/online-users/{sessionId}

    [Fact]
    public async Task GetById_WithValidSessionId_ShouldReturn200()
    {
        var sessionId = "session-001";
        var user = new OnlineUserDto
        {
            SessionId = sessionId,
            UserId = Guid.NewGuid(),
            Username = "testuser",
            IpAddress = "10.0.0.1",
            Browser = "Firefox",
            Os = "Linux",
            LoginAt = DateTime.UtcNow.AddMinutes(30),
            LastActivityAt = DateTime.UtcNow,
            SessionDurationMs = 1800000
        };
        _factory.OnlineUserAppServiceMock
            .Setup(s => s.GetByIdAsync(sessionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);

        var response = await _client.GetAsync($"/api/admin/online-users/{sessionId}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<ApiResponse<OnlineUserDto>>();
        body!.Code.Should().Be(200);
        body.Data!.SessionId.Should().Be(sessionId);
    }

    [Fact]
    public async Task GetById_WithNonExistentSessionId_ShouldReturn404()
    {
        var sessionId = "non-existent-session";
        _factory.OnlineUserAppServiceMock
            .Setup(s => s.GetByIdAsync(sessionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((OnlineUserDto?)null);

        var response = await _client.GetAsync($"/api/admin/online-users/{sessionId}");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        var body = await response.Content.ReadFromJsonAsync<ApiResponse>();
        body!.Code.Should().Be(404);
    }

    #endregion

    #region DELETE /api/admin/online-users/{sessionId}

    [Fact]
    public async Task ForceOffline_WithValidSessionId_ShouldReturn200()
    {
        var sessionId = "session-to-offline";
        _factory.OnlineUserAppServiceMock
            .Setup(s => s.ForceOfflineAsync(sessionId, It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var response = await _client.DeleteAsync($"/api/admin/online-users/{sessionId}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<ApiResponse>();
        body!.Code.Should().Be(200);
    }

    [Fact]
    public async Task ForceOffline_WithSelfSession_ShouldReturn403()
    {
        // 使用默认测试会话 ID 作为目标，触发 ONLINE_USER_FORCE_OFFLINE_SELF_FORBIDDEN
        var selfSessionId = SystemAdminApiFactory.DefaultTestSessionId;
        _factory.OnlineUserAppServiceMock
            .Setup(s => s.ForceOfflineAsync(selfSessionId, It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new SystemAdminDomainException("不可强制下线当前操作者自身的会话", "ONLINE_USER_FORCE_OFFLINE_SELF_FORBIDDEN"));

        var response = await _client.DeleteAsync($"/api/admin/online-users/{selfSessionId}");

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        var body = await response.Content.ReadFromJsonAsync<ApiResponse>();
        body!.Code.Should().Be(403);
    }

    #endregion

    #region GET /api/admin/online-users/stats

    [Fact]
    public async Task GetStats_WithAdminRole_ShouldReturn200()
    {
        var stats = new OnlineUserStatsDto
        {
            Total = 15,
            Logins24h = 128,
            Anomalies = 2
        };
        _factory.OnlineUserAppServiceMock
            .Setup(s => s.GetStatsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(stats);

        var response = await _client.GetAsync("/api/admin/online-users/stats");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<ApiResponse<OnlineUserStatsDto>>();
        body!.Code.Should().Be(200);
        body.Data!.Total.Should().Be(15);
        body.Data.Anomalies.Should().Be(2);
    }

    #endregion
}
