using System.Net;
using System.Net.Http.Json;
using Leno.SharedContracts.Responses;
using Leno.SystemAdmin.Application;
using Leno.SystemAdmin.Application.DTOs;
using Leno.SystemAdmin.Domain.Aggregates;
using Leno.SystemAdmin.Domain.ValueObjects;
using Moq;

namespace Leno.SystemAdmin.Api.Tests.Controllers;

/// <summary>
/// LoginLogsController 集成测试（Task 7.15，8 用例）。
/// 覆盖登录日志分页查询、详情、CSV 导出 3 个端点，
/// 验证 200/401/403/404 状态码、Admin 与 Operator 双角色鉴权与 ApiResponse 包装。
/// </summary>
public class LoginLogsControllerTests : IClassFixture<SystemAdminApiFactory>
{
    private readonly SystemAdminApiFactory _factory;
    private readonly HttpClient _client;

    public LoginLogsControllerTests(SystemAdminApiFactory factory)
    {
        _factory = factory;
        _client = factory.CreateAdminClient();
    }

    #region GET /api/admin/login-logs

    [Fact]
    public async Task List_WithAdminRole_ShouldReturn200()
    {
        var listResult = new LoginLogListResultDto
        {
            Items = new List<LoginLogDto>
            {
                new()
                {
                    Id = Guid.NewGuid(),
                    Username = "adminuser",
                    UserId = Guid.NewGuid(),
                    IpAddress = "192.168.1.100",
                    Browser = "Chrome",
                    Os = "Windows",
                    Result = LoginResult.Success,
                    DurationMs = 150,
                    UserAgent = "Mozilla/5.0",
                    TraceId = "trace-001",
                    LoginAt = DateTime.UtcNow.AddHours(-1)
                }
            },
            Total = 1,
            Page = 1,
            PageSize = 20
        };
        _factory.LoginLogAppServiceMock
            .Setup(s => s.QueryAsync(It.IsAny<LoginLogQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(listResult);

        var response = await _client.GetAsync("/api/admin/login-logs");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<ApiResponse<LoginLogListResultDto>>();
        body!.Code.Should().Be(200);
        body.Data!.Total.Should().Be(1);
        body.Data.Items.Should().HaveCount(1);
    }

    [Fact]
    public async Task List_WithOperatorRole_ShouldReturn200()
    {
        var listResult = new LoginLogListResultDto
        {
            Items = new List<LoginLogDto>(),
            Total = 0,
            Page = 1,
            PageSize = 20
        };
        _factory.LoginLogAppServiceMock
            .Setup(s => s.QueryAsync(It.IsAny<LoginLogQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(listResult);

        var operatorClient = _factory.CreateClientWithRole("Operator");
        var response = await operatorClient.GetAsync("/api/admin/login-logs");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<ApiResponse<LoginLogListResultDto>>();
        body!.Code.Should().Be(200);
    }

    [Fact]
    public async Task List_WithoutAuth_ShouldReturn401()
    {
        var anonymousClient = _factory.CreateAnonymousClient();
        var response = await anonymousClient.GetAsync("/api/admin/login-logs");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task List_WithBuyerRole_ShouldReturn403()
    {
        var buyerClient = _factory.CreateClientWithRole("Buyer");
        var response = await buyerClient.GetAsync("/api/admin/login-logs");

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    #endregion

    #region GET /api/admin/login-logs/{id}

    [Fact]
    public async Task GetById_WithValidId_ShouldReturn200()
    {
        var logId = Guid.NewGuid();
        var log = new LoginLogDto
        {
            Id = logId,
            Username = "testuser",
            UserId = Guid.NewGuid(),
            IpAddress = "10.0.0.1",
            Browser = "Firefox",
            Os = "Linux",
            Result = LoginResult.Failed,
            FailureReason = "密码错误",
            DurationMs = 89,
            UserAgent = "Mozilla/5.0",
            TraceId = "trace-002",
            LoginAt = DateTime.UtcNow.AddMinutes(-15)
        };
        _factory.LoginLogAppServiceMock
            .Setup(s => s.GetByIdAsync(logId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(log);

        var response = await _client.GetAsync($"/api/admin/login-logs/{logId}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<ApiResponse<LoginLogDto>>();
        body!.Code.Should().Be(200);
        body.Data!.Id.Should().Be(logId);
        body.Data.Result.Should().Be(LoginResult.Failed);
    }

    [Fact]
    public async Task GetById_WithNonExistentId_ShouldReturn404()
    {
        var logId = Guid.NewGuid();
        _factory.LoginLogAppServiceMock
            .Setup(s => s.GetByIdAsync(logId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((LoginLogDto?)null);

        var response = await _client.GetAsync($"/api/admin/login-logs/{logId}");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        var body = await response.Content.ReadFromJsonAsync<ApiResponse>();
        body!.Code.Should().Be(404);
    }

    #endregion

    #region GET /api/admin/login-logs/export

    [Fact]
    public async Task Export_WithAdminRole_ShouldReturnCsv()
    {
        var csvContent = "id,loginAt,username,ipAddress,geoLocation,browser,os,result,failureReason,durationMs,traceId\n"
            + $"{Guid.NewGuid()},2026-07-28T10:00:00Z,adminuser,192.168.1.100,,Chrome,Windows,Success,,150,trace-001\n";
        _factory.LoginLogAppServiceMock
            .Setup(s => s.ExportAsync(It.IsAny<LoginLogQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(csvContent);

        var response = await _client.GetAsync("/api/admin/login-logs/export");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Content.Headers.ContentType!.MediaType.Should().Be("text/csv");
        var content = await response.Content.ReadAsStringAsync();
        content.Should().Contain("id,loginAt,username");
        content.Should().Contain("adminuser");
    }

    [Fact]
    public async Task Export_WithOperatorRole_ShouldReturnCsv()
    {
        var csvContent = "id,loginAt,username,ipAddress,geoLocation,browser,os,result,failureReason,durationMs,traceId\n";
        _factory.LoginLogAppServiceMock
            .Setup(s => s.ExportAsync(It.IsAny<LoginLogQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(csvContent);

        var operatorClient = _factory.CreateClientWithRole("Operator");
        var response = await operatorClient.GetAsync("/api/admin/login-logs/export");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Content.Headers.ContentType!.MediaType.Should().Be("text/csv");
    }

    #endregion
}
