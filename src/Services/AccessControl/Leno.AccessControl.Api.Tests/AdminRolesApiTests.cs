using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text.Encodings.Web;
using Leno.AccessControl.Application;
using Leno.AccessControl.Application.DTOs;
using Leno.SharedContracts.Responses;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;

namespace Leno.AccessControl.Api.Tests;

/// <summary>
/// AccessControl 域 AdminRolesController API 集成测试。
/// 覆盖 7 个角色管理端点：列表、详情、创建、更新、删除、查权限、改权限。
/// 使用 WebApplicationFactory + Mock 应用服务，验证路由、鉴权（Operator,Admin RBAC）、ApiResponse 包装。
/// </summary>
public class AdminRolesApiTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly HttpClient _client;
    private readonly Mock<IRoleAppService> _roleAppServiceMock = new();
    private readonly Mock<IRolePermissionAppService> _rolePermissionAppServiceMock = new();

    private static readonly Guid OperatorRoleId = Guid.NewGuid();
    private static readonly Guid NonExistentRoleId = Guid.NewGuid();

    public AdminRolesApiTests(WebApplicationFactory<Program> factory)
    {
        _client = factory.WithWebHostBuilder(builder =>
        {
            // 使用 Development 环境，跳过程序启动期的敏感配置校验
            builder.UseSetting("Environment", "Development");

            builder.ConfigureServices(services =>
            {
                RemoveMassTransitServices(services);
                RemoveElasticsearchServices(services);
                RemoveApplicationServiceRegistrations(services);
                RemoveEventBusServices(services);
                ReplaceDistributedLockProvider(services);

                services.AddSingleton(_roleAppServiceMock.Object);
                services.AddSingleton(_rolePermissionAppServiceMock.Object);

                services.AddAuthentication(defaultScheme: "Test")
                    .AddScheme<AuthenticationSchemeOptions, TestAuthHandler>("Test", _ => { });
            });
        }).CreateClient();

        _client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Test");
    }

    private static void RemoveMassTransitServices(IServiceCollection services)
    {
        var descriptors = services
            .Where(s => s.ServiceType.FullName?.Contains("MassTransit") == true
                     || s.ImplementationType?.FullName?.Contains("MassTransit") == true
                     || s.ServiceType == typeof(MassTransit.IBus)
                     || s.ServiceType == typeof(MassTransit.IBusControl)
                     || s.ServiceType.FullName?.StartsWith("MassTransit.", StringComparison.Ordinal) == true)
            .ToList();
        foreach (var d in descriptors) services.Remove(d);
    }

    private static void RemoveElasticsearchServices(IServiceCollection services)
    {
        var descriptors = services
            .Where(s => s.ServiceType.FullName?.Contains("Elasticsearch") == true
                     || s.ServiceType.FullName?.Contains("Elastic") == true
                     || s.ServiceType.FullName?.Contains("Nest") == true
                     || s.ImplementationType?.FullName?.Contains("Elastic") == true)
            .ToList();
        foreach (var d in descriptors) services.Remove(d);
    }

    private static void RemoveApplicationServiceRegistrations(IServiceCollection services)
    {
        var appServiceInterfaces = new[]
        {
            typeof(IRoleAppService),
            typeof(IRolePermissionAppService)
        };

        var descriptors = services
            .Where(s => appServiceInterfaces.Contains(s.ServiceType))
            .ToList();
        foreach (var d in descriptors) services.Remove(d);
    }

    private static void RemoveEventBusServices(IServiceCollection services)
    {
        var descriptors = services
            .Where(s => s.ServiceType == typeof(Leno.Infrastructure.Abstractions.IEventBus)
                     || s.ImplementationType?.FullName?.Contains("RabbitMqEventBus") == true)
            .ToList();
        foreach (var d in descriptors) services.Remove(d);
    }

    private static void ReplaceDistributedLockProvider(IServiceCollection services)
    {
        var descriptors = services
            .Where(s => s.ServiceType == typeof(Medallion.Threading.IDistributedLockProvider))
            .ToList();
        foreach (var d in descriptors) services.Remove(d);

        var lockMock = new Mock<Medallion.Threading.IDistributedLock>();
        lockMock
            .Setup(l => l.TryAcquireAsync(It.IsAny<TimeSpan>(), It.IsAny<CancellationToken>()))
            .Returns(() => default);

        var lockProviderMock = new Mock<Medallion.Threading.IDistributedLockProvider>();
        lockProviderMock
            .Setup(p => p.CreateLock(It.IsAny<string>()))
            .Returns(lockMock.Object);

        services.AddSingleton(lockProviderMock.Object);
    }

    #region ListRoles - GET /api/admin/roles

    [Fact]
    public async Task ListRoles_WithOperatorAuth_ReturnsOk()
    {
        SetupOperatorAuth();
        var paged = new PagedResult<RoleDto>
        {
            Items = new List<RoleDto>
            {
                new() { Id = OperatorRoleId, Name = "Operator", IsBuiltIn = true }
            },
            Total = 1,
            Page = 1,
            PageSize = 20
        };
        _roleAppServiceMock.Setup(s => s.QueryRolesAsync(null, 1, 20, It.IsAny<CancellationToken>()))
            .ReturnsAsync(paged);

        var response = await _client.GetAsync("/api/admin/roles");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await response.Content.ReadFromJsonAsync<ApiResponse<PagedResult<RoleDto>>>();
        result!.Code.Should().Be(200);
        result.Data!.Total.Should().Be(1);
        result.Data.Items.Should().HaveCount(1);
        result.Data.Items[0].Name.Should().Be("Operator");
    }

    [Fact]
    public async Task ListRoles_WithoutAuth_Returns401()
    {
        // 移除 Authorization 头，模拟未认证
        var originalAuth = _client.DefaultRequestHeaders.Authorization;
        _client.DefaultRequestHeaders.Authorization = null;

        try
        {
            var response = await _client.GetAsync("/api/admin/roles");
            response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        }
        finally
        {
            _client.DefaultRequestHeaders.Authorization = originalAuth;
        }
    }

    [Fact]
    public async Task ListRoles_WithBuyerAuth_Returns403()
    {
        // 仅 Buyer 角色，不符合 [Authorize(Roles = "Operator,Admin")]
        _client.DefaultRequestHeaders.Remove("X-Test-Role");
        _client.DefaultRequestHeaders.Add("X-Test-Role", "Buyer");

        try
        {
            var response = await _client.GetAsync("/api/admin/roles");
            response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        }
        finally
        {
            _client.DefaultRequestHeaders.Remove("X-Test-Role");
        }
    }

    #endregion

    #region GetRole - GET /api/admin/roles/{roleId}

    [Fact]
    public async Task GetRole_WithOperatorAuth_ReturnsOk()
    {
        SetupOperatorAuth();
        var dto = new RoleDto
        {
            Id = OperatorRoleId,
            Name = "Operator",
            Description = "运营人员",
            IsBuiltIn = true,
            Permissions = new List<string> { "api:/admin/users" }
        };
        _roleAppServiceMock.Setup(s => s.GetRoleAsync(OperatorRoleId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(dto);

        var response = await _client.GetAsync($"/api/admin/roles/{OperatorRoleId}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await response.Content.ReadFromJsonAsync<ApiResponse<RoleDto>>();
        result!.Code.Should().Be(200);
        result.Data!.Id.Should().Be(OperatorRoleId);
        result.Data.Name.Should().Be("Operator");
        result.Data.IsBuiltIn.Should().BeTrue();
        result.Data.Permissions.Should().ContainSingle(p => p == "api:/admin/users");
    }

    [Fact]
    public async Task GetRole_WithNonExistentId_Returns404()
    {
        SetupOperatorAuth();
        _roleAppServiceMock.Setup(s => s.GetRoleAsync(NonExistentRoleId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((RoleDto?)null);

        var response = await _client.GetAsync($"/api/admin/roles/{NonExistentRoleId}");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        var result = await response.Content.ReadFromJsonAsync<ApiResponse>();
        result!.Code.Should().Be(StatusCodes.Status404NotFound);
        result.Message.Should().Be("角色不存在");
    }

    #endregion

    #region CreateRole - POST /api/admin/roles

    [Fact]
    public async Task CreateRole_WithOperatorAuth_ReturnsOk()
    {
        SetupOperatorAuth();
        var created = new RoleDto
        {
            Id = Guid.NewGuid(),
            Name = "ContentReviewer",
            Description = "内容审核员",
            IsBuiltIn = false,
            Permissions = Array.Empty<string>()
        };
        _roleAppServiceMock.Setup(s => s.CreateRoleAsync(
                It.Is<CreateRoleDto>(d => d.Name == "ContentReviewer" && d.Description == "内容审核员"),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(created);

        var body = new { name = "ContentReviewer", description = "内容审核员" };
        var response = await _client.PostAsJsonAsync("/api/admin/roles", body);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await response.Content.ReadFromJsonAsync<ApiResponse<RoleDto>>();
        result!.Code.Should().Be(200);
        result.Data!.Name.Should().Be("ContentReviewer");
        result.Data.IsBuiltIn.Should().BeFalse();

        _roleAppServiceMock.Verify(
            s => s.CreateRoleAsync(
                It.Is<CreateRoleDto>(d => d.Name == "ContentReviewer"),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    #endregion

    #region UpdateRole - PUT /api/admin/roles/{roleId}

    [Fact]
    public async Task UpdateRole_WithOperatorAuth_ReturnsOk()
    {
        SetupOperatorAuth();
        _roleAppServiceMock.Setup(s => s.UpdateRoleAsync(
                OperatorRoleId,
                It.Is<UpdateRoleDto>(d => d.Name == "Operator2"),
                It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var body = new { name = "Operator2", description = "运营 V2" };
        var response = await _client.PutAsJsonAsync($"/api/admin/roles/{OperatorRoleId}", body);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await response.Content.ReadFromJsonAsync<ApiResponse>();
        result!.Code.Should().Be(200);

        _roleAppServiceMock.Verify(
            s => s.UpdateRoleAsync(
                OperatorRoleId,
                It.Is<UpdateRoleDto>(d => d.Name == "Operator2" && d.Description == "运营 V2"),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    #endregion

    #region DeleteRole - DELETE /api/admin/roles/{roleId}

    [Fact]
    public async Task DeleteRole_WithOperatorAuth_ReturnsOk()
    {
        SetupOperatorAuth();
        _roleAppServiceMock.Setup(s => s.DeleteRoleAsync(OperatorRoleId, It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var response = await _client.DeleteAsync($"/api/admin/roles/{OperatorRoleId}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await response.Content.ReadFromJsonAsync<ApiResponse>();
        result!.Code.Should().Be(200);

        _roleAppServiceMock.Verify(
            s => s.DeleteRoleAsync(OperatorRoleId, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    #endregion

    #region GetPermissions - GET /api/admin/roles/{roleId}/permissions

    [Fact]
    public async Task GetPermissions_WithOperatorAuth_ReturnsOk()
    {
        SetupOperatorAuth();
        var permissions = new List<string> { "api:/admin/users", "api:/admin/roles" };
        _rolePermissionAppServiceMock.Setup(s => s.GetRolePermissionsAsync(OperatorRoleId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(permissions);

        var response = await _client.GetAsync($"/api/admin/roles/{OperatorRoleId}/permissions");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await response.Content.ReadFromJsonAsync<ApiResponse<IReadOnlyList<string>>>();
        result!.Code.Should().Be(200);
        result.Data.Should().HaveCount(2);
        result.Data.Should().Contain("api:/admin/users");
        result.Data.Should().Contain("api:/admin/roles");
    }

    #endregion

    #region UpdatePermissions - PUT /api/admin/roles/{roleId}/permissions

    [Fact]
    public async Task UpdatePermissions_WithOperatorAuth_ReturnsOk()
    {
        SetupOperatorAuth();
        _rolePermissionAppServiceMock.Setup(s => s.UpdateRolePermissionsAsync(
                OperatorRoleId,
                It.Is<IReadOnlyList<string>>(list => list.Count == 2 && list.Contains("api:/admin/users")),
                It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var body = new { permissions = new List<string> { "api:/admin/users", "api:/admin/roles" } };
        var response = await _client.PutAsJsonAsync($"/api/admin/roles/{OperatorRoleId}/permissions", body);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await response.Content.ReadFromJsonAsync<ApiResponse>();
        result!.Code.Should().Be(200);

        _rolePermissionAppServiceMock.Verify(
            s => s.UpdateRolePermissionsAsync(
                OperatorRoleId,
                It.Is<IReadOnlyList<string>>(list => list.Count == 2),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    #endregion

    #region Auth Helpers

    /// <summary>
    /// 设置 Operator 角色鉴权（清空 X-Test-Role 让 TestAuthHandler 走默认全角色路径，
    /// 包含 Operator 与 Admin，可通过 [Authorize(Roles = "Operator,Admin")] 校验）。
    /// </summary>
    private void SetupOperatorAuth()
    {
        _client.DefaultRequestHeaders.Remove("X-Test-Role");
    }

    #endregion
}

/// <summary>
/// 测试鉴权处理器，模拟 JWT 鉴权并支持按 <c>X-Test-Role</c> 请求头动态注入角色。
/// 默认（无 X-Test-Role 头）注入全部 4 个角色（Buyer/Seller/Admin/Operator），
/// 便于通用的"Operator,Admin"RBAC 端点测试；指定 X-Test-Role 时仅注入该角色，
/// 用于 403 Forbidden 场景验证。
/// </summary>
public class TestAuthHandler : AuthenticationHandler<AuthenticationSchemeOptions>
{
    public TestAuthHandler(
        IOptionsMonitor<AuthenticationSchemeOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder)
        : base(options, logger, encoder) { }

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        if (!Request.Headers.ContainsKey("Authorization"))
        {
            return Task.FromResult(AuthenticateResult.NoResult());
        }

        var claims = new List<Claim>
        {
            new(ClaimTypes.Name, "test"),
            new(ClaimTypes.NameIdentifier, Guid.NewGuid().ToString())
        };

        if (Request.Headers.TryGetValue("X-Test-Role", out var roleHeader)
            && !string.IsNullOrWhiteSpace(roleHeader.ToString()))
        {
            // 仅注入指定角色，用于测试 RBAC 拒绝场景
            claims.Add(new Claim(ClaimTypes.Role, roleHeader.ToString()!));
        }
        else
        {
            // 默认注入全部角色，便于通过 [Authorize(Roles = "Operator,Admin")]
            claims.Add(new Claim(ClaimTypes.Role, "Buyer"));
            claims.Add(new Claim(ClaimTypes.Role, "Seller"));
            claims.Add(new Claim(ClaimTypes.Role, "Admin"));
            claims.Add(new Claim(ClaimTypes.Role, "Operator"));
        }

        var identity = new ClaimsIdentity(claims, "Test");
        var principal = new ClaimsPrincipal(identity);
        var ticket = new AuthenticationTicket(principal, "Test");

        return Task.FromResult(AuthenticateResult.Success(ticket));
    }
}
