using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text.Encodings.Web;
using Leno.Identity.Application;
using Leno.Identity.Application.DTOs;
using Leno.Identity.Domain.Exceptions;
using Leno.Identity.Domain.ValueObjects;
using Leno.Infrastructure.Auth;
using Leno.SharedContracts.Responses;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;

namespace Leno.Identity.Api.Tests;

/// <summary>
/// Identity 域 API 集成测试（Task A3）。
/// 覆盖 AuthController（9 端点）、UsersController（6 端点）、AccountController（2 端点）共 17 端点。
/// 使用 WebApplicationFactory + Mock 应用服务，验证路由、RBAC 鉴权、ApiResponse 包装、UserId 从 JWT 注入。
/// </summary>
public class IdentityApiTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly HttpClient _client;
    private readonly Mock<IAuthAppService> _authAppServiceMock = new();
    private readonly Mock<IUserProfileAppService> _userProfileAppServiceMock = new();
    private readonly Mock<ITwoFactorService> _twoFactorServiceMock = new();
    private readonly Mock<IPasswordService> _passwordServiceMock = new();
    private readonly Mock<IExternalLoginService> _externalLoginServiceMock = new();
    private readonly Mock<IOAuthService> _oauthServiceMock = new();
    private readonly Mock<ICurrentUserContext> _currentUserMock = new();

    private static readonly Guid UserId = Guid.NewGuid();
    private static readonly Guid AnotherUserId = Guid.NewGuid();

    public IdentityApiTests(WebApplicationFactory<Program> factory)
    {
        _client = factory.WithWebHostBuilder(builder =>
        {
            // 使用 Development 环境，跳过程序启动期的敏感配置校验（Program.cs 仅在非 Development 抛异常）
            builder.UseSetting("Environment", "Development");
            // 提供 OAuth2:AesKey 配置，避免 AddIdentityInfrastructure 中 fail-fast 检查抛异常
            // 32 字节全零 Base64 编码（仅测试用，非生产密钥）
            builder.UseSetting("OAuth2:AesKey", "AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA=");

            builder.ConfigureServices(services =>
            {
                // 先移除真实服务注册（Scoped），再添加 Mock 单例，避免 Remove 方法误删 Mock 注册
                RemoveMassTransitServices(services);
                RemoveElasticsearchServices(services);
                RemoveApplicationServiceRegistrations(services);
                RemoveEventBusServices(services);
                ReplaceDistributedLockProvider(services);

                services.AddSingleton(_authAppServiceMock.Object);
                services.AddSingleton(_userProfileAppServiceMock.Object);
                services.AddSingleton(_twoFactorServiceMock.Object);
                services.AddSingleton(_passwordServiceMock.Object);
                services.AddSingleton(_externalLoginServiceMock.Object);
                services.AddSingleton(_oauthServiceMock.Object);
                services.AddSingleton(_currentUserMock.Object);

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

    /// <summary>
    /// 移除 Program.cs 注册的真实应用服务（Scoped），避免 Development 环境的 ValidateOnBuild
    /// 校验因 Redis/SQL 等依赖未就绪而失败。移除后由测试注入 Mock 单例替代。
    /// </summary>
    private static void RemoveApplicationServiceRegistrations(IServiceCollection services)
    {
        var appServiceInterfaces = new[]
        {
            typeof(IAuthAppService),
            typeof(IUserProfileAppService),
            typeof(ITwoFactorService),
            typeof(IPasswordService),
            typeof(IExternalLoginService),
            typeof(IOAuthService)
        };

        var descriptors = services
            .Where(s => appServiceInterfaces.Contains(s.ServiceType))
            .ToList();
        foreach (var d in descriptors) services.Remove(d);
    }

    /// <summary>
    /// 移除 IEventBus 注册（RabbitMqEventBus 依赖 MassTransit.IPublishEndpoint，移除 MassTransit 后无法构造）。
    /// 注意：仅移除 Leno.Infrastructure.Abstractions.IEventBus，保留 IIntegrationEventMapper（UnitOfWork 依赖）。
    /// </summary>
    private static void RemoveEventBusServices(IServiceCollection services)
    {
        var descriptors = services
            .Where(s => s.ServiceType == typeof(Leno.Infrastructure.Abstractions.IEventBus)
                     || s.ImplementationType?.FullName?.Contains("RabbitMqEventBus") == true)
            .ToList();
        foreach (var d in descriptors) services.Remove(d);
    }

    /// <summary>
    /// 替换 IDistributedLockProvider 为 Mock，使 MigrateWithLockAsync 跳过迁移（TryAcquireLockAsync 返回 null）。
    /// 测试环境无 Redis，避免 RedisConnectionException 阻止宿主启动。
    /// </summary>
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

    #region AuthController - Register (POST /api/auth/register)

    [Fact]
    public async Task Register_WithoutAuth_ReturnsOk()
    {
        var token = new TokenDto
        {
            AccessToken = "access-token-123",
            RefreshToken = "refresh-token-456",
            ExpiresAt = DateTime.UtcNow.AddMinutes(30)
        };
        _authAppServiceMock.Setup(s => s.RegisterAsync(It.IsAny<RegisterDto>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(token);

        var body = new
        {
            username = "newuser",
            email = "newuser@example.com",
            password = "Password123",
            nickname = "New User"
        };
        var response = await _client.PostAsJsonAsync("/api/auth/register", body);

        response.StatusCode.Should().Be(HttpStatusCode.OK, "register 是匿名端点");
        var result = await response.Content.ReadFromJsonAsync<ApiResponse<TokenDto>>();
        result!.Code.Should().Be(200);
        result.Data!.AccessToken.Should().Be("access-token-123");
        result.Data.RefreshToken.Should().Be("refresh-token-456");
    }

    #endregion

    #region AuthController - Login (POST /api/auth/login)

    [Fact]
    public async Task Login_WithoutAuth_ReturnsOk()
    {
        var token = new TokenDto
        {
            AccessToken = "access-token-login",
            RefreshToken = "refresh-token-login",
            ExpiresAt = DateTime.UtcNow.AddMinutes(30)
        };
        _authAppServiceMock.Setup(s => s.LoginAsync(It.IsAny<LoginDto>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(token);

        var body = new { usernameOrEmail = "testuser", password = "Password123" };
        var response = await _client.PostAsJsonAsync("/api/auth/login", body);

        response.StatusCode.Should().Be(HttpStatusCode.OK, "login 是匿名端点");
        var result = await response.Content.ReadFromJsonAsync<ApiResponse<TokenDto>>();
        result!.Code.Should().Be(200);
        result.Data!.AccessToken.Should().Be("access-token-login");
    }

    [Fact]
    public async Task Login_ReturnsApiWrappedResponse()
    {
        var token = new TokenDto
        {
            AccessToken = "wrapped-token",
            RefreshToken = "wrapped-refresh",
            ExpiresAt = DateTime.UtcNow.AddMinutes(30)
        };
        _authAppServiceMock.Setup(s => s.LoginAsync(It.IsAny<LoginDto>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(token);

        var body = new { usernameOrEmail = "testuser", password = "Password123" };
        var response = await _client.PostAsJsonAsync("/api/auth/login", body);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await response.Content.ReadFromJsonAsync<ApiResponse<TokenDto>>();
        result!.Code.Should().Be(200, "返工后必须使用 ApiResponse 包装");
        result.Message.Should().Be("success");
        result.Data.Should().NotBeNull();
    }

    #endregion

    #region AuthController - RefreshToken (POST /api/auth/refresh-token)

    [Fact]
    public async Task RefreshToken_PathIsRefreshToken_NotRefresh()
    {
        var token = new TokenDto
        {
            AccessToken = "new-access",
            RefreshToken = "new-refresh",
            ExpiresAt = DateTime.UtcNow.AddMinutes(30)
        };
        _authAppServiceMock.Setup(s => s.RefreshTokenAsync(It.IsAny<RefreshTokenDto>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(token);

        var body = new { refreshToken = "old-refresh-token" };
        // 验证返工后路径是 /api/auth/refresh-token（不是 /api/auth/refresh）
        var response = await _client.PostAsJsonAsync("/api/auth/refresh-token", body);

        response.StatusCode.Should().Be(HttpStatusCode.OK, "返工后路径应为 /api/auth/refresh-token");
        var result = await response.Content.ReadFromJsonAsync<ApiResponse<TokenDto>>();
        result!.Code.Should().Be(200);
        result.Data!.AccessToken.Should().Be("new-access");
    }

    [Fact]
    public async Task RefreshToken_OldPathRefresh_Returns404()
    {
        var body = new { refreshToken = "old-refresh-token" };
        // 验证旧路径 /api/auth/refresh 已不存在（返工后改为 refresh-token）
        var response = await _client.PostAsJsonAsync("/api/auth/refresh", body);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound, "旧路径 /api/auth/refresh 应已废弃");
    }

    [Fact]
    public async Task RefreshToken_ReturnsApiWrappedResponse()
    {
        var token = new TokenDto
        {
            AccessToken = "wrapped-refresh-access",
            RefreshToken = "wrapped-refresh-token",
            ExpiresAt = DateTime.UtcNow.AddMinutes(30)
        };
        _authAppServiceMock.Setup(s => s.RefreshTokenAsync(It.IsAny<RefreshTokenDto>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(token);

        var body = new { refreshToken = "old-refresh-token" };
        var response = await _client.PostAsJsonAsync("/api/auth/refresh-token", body);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await response.Content.ReadFromJsonAsync<ApiResponse<TokenDto>>();
        result!.Code.Should().Be(200, "返工后必须使用 ApiResponse 包装");
        result.Message.Should().Be("success");
        result.Data.Should().NotBeNull();
    }

    #endregion

    #region AuthController - Logout (POST /api/auth/logout)

    [Fact]
    public async Task Logout_WithoutAuth_Returns401()
    {
        _client.DefaultRequestHeaders.Authorization = null;
        var response = await _client.PostAsync("/api/auth/logout", null);
        _client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Test");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Logout_WithAuth_ReturnsOk()
    {
        SetupAuth();
        _authAppServiceMock.Setup(s => s.LogoutAsync(UserId, It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var response = await _client.PostAsync("/api/auth/logout", null);

        response.StatusCode.Should().Be(HttpStatusCode.OK, "返工后不用 204 NoContent");
        var result = await response.Content.ReadFromJsonAsync<ApiResponse>();
        result!.Code.Should().Be(200);
    }

    [Fact]
    public async Task Logout_WithAuth_ShouldCallServiceWithJwtUserId()
    {
        SetupAuth();
        _authAppServiceMock.Setup(s => s.LogoutAsync(UserId, It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        await _client.PostAsync("/api/auth/logout", null);

        _authAppServiceMock.Verify(
            s => s.LogoutAsync(UserId, It.IsAny<CancellationToken>()),
            Times.Once,
            "UserId 必须从 ICurrentUserContext 取，不传 userId 参数");
    }

    #endregion

    #region AuthController - OAuthLogin (GET /api/auth/oauth/{provider}/login)

    [Fact]
    public async Task OAuthLogin_WithoutAuth_ReturnsOk()
    {
        var authUrl = "https://accounts.google.com/o/oauth2/v2/auth?client_id=xxx";
        _oauthServiceMock.Setup(s => s.GetLoginUrlAsync("google", It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(authUrl);

        var response = await _client.GetAsync("/api/auth/oauth/google/login?redirectUri=https://example.com/callback");

        response.StatusCode.Should().Be(HttpStatusCode.OK, "oauth login 是匿名端点");
        var result = await response.Content.ReadFromJsonAsync<ApiResponse<OAuthLoginResponse>>();
        result!.Code.Should().Be(200);
        result.Data!.AuthorizationUrl.Should().Be(authUrl);
    }

    #endregion

    #region AuthController - OAuthCallback (GET /api/auth/oauth/{provider}/callback)

    [Fact]
    public async Task OAuthCallback_WithoutAuth_ReturnsOk()
    {
        var token = new TokenDto
        {
            AccessToken = "oauth-access",
            RefreshToken = "oauth-refresh",
            ExpiresAt = DateTime.UtcNow.AddMinutes(30)
        };
        _oauthServiceMock.Setup(s => s.HandleCallbackAsync("google", "auth-code-123", It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(token);

        var response = await _client.GetAsync("/api/auth/oauth/google/callback?code=auth-code-123&state=state-xyz");

        response.StatusCode.Should().Be(HttpStatusCode.OK, "oauth callback 是匿名端点");
        var result = await response.Content.ReadFromJsonAsync<ApiResponse<TokenDto>>();
        result!.Code.Should().Be(200);
        result.Data!.AccessToken.Should().Be("oauth-access");
    }

    #endregion

    #region AuthController - TwoFactorVerify (POST /api/auth/two-factor/verify)

    [Fact]
    public async Task TwoFactorVerify_WithoutAuth_Returns401()
    {
        _client.DefaultRequestHeaders.Authorization = null;
        var body = new { code = "123456" };
        var response = await _client.PostAsJsonAsync("/api/auth/two-factor/verify", body);
        _client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Test");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task TwoFactorVerify_WithAuth_ReturnsOk()
    {
        SetupAuth();
        _twoFactorServiceMock.Setup(s => s.VerifyAsync(UserId, "123456", It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var body = new { code = "123456" };
        var response = await _client.PostAsJsonAsync("/api/auth/two-factor/verify", body);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await response.Content.ReadFromJsonAsync<ApiResponse<bool>>();
        result!.Code.Should().Be(200);
        result.Data.Should().BeTrue();
    }

    #endregion

    #region AuthController - ForgotPassword (POST /api/auth/forgot-password)

    [Fact]
    public async Task ForgotPassword_WithoutAuth_ReturnsOk()
    {
        _passwordServiceMock.Setup(s => s.ForgotPasswordAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var body = new { account = "user@example.com" };
        var response = await _client.PostAsJsonAsync("/api/auth/forgot-password", body);

        response.StatusCode.Should().Be(HttpStatusCode.OK, "forgot-password 是匿名端点");
        var result = await response.Content.ReadFromJsonAsync<ApiResponse>();
        result!.Code.Should().Be(200);
        result.Message.Should().Be("若账号存在，重置链接已发送");
    }

    #endregion

    #region AuthController - ResetPassword (POST /api/auth/reset-password)

    [Fact]
    public async Task ResetPassword_WithoutAuth_ReturnsOk()
    {
        _passwordServiceMock.Setup(s => s.ResetPasswordAsync(It.IsAny<ResetPasswordDto>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var body = new { token = "reset-token-abc", newPassword = "NewPassword123" };
        var response = await _client.PostAsJsonAsync("/api/auth/reset-password", body);

        response.StatusCode.Should().Be(HttpStatusCode.OK, "reset-password 是匿名端点");
        var result = await response.Content.ReadFromJsonAsync<ApiResponse>();
        result!.Code.Should().Be(200);
        result.Message.Should().Be("密码重置成功");
    }

    #endregion

    #region UsersController - GetProfile (GET /api/users/me)

    [Fact]
    public async Task GetProfile_WithAuth_ReturnsOk()
    {
        SetupAuth();
        var dto = new UserDto
        {
            Id = UserId,
            Username = "testuser",
            Email = "test@example.com",
            Nickname = "Test User",
            Status = AccountStatus.Active,
            CreatedAt = DateTime.UtcNow.AddDays(-30),
            UpdatedAt = DateTime.UtcNow
        };
        _userProfileAppServiceMock.Setup(s => s.GetProfileAsync(UserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(dto);

        var response = await _client.GetAsync("/api/users/me");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await response.Content.ReadFromJsonAsync<ApiResponse<UserDto>>();
        result!.Code.Should().Be(200);
        result.Data!.Id.Should().Be(UserId);
        result.Data.Username.Should().Be("testuser");
    }

    [Fact]
    public async Task GetProfile_WithoutAuth_Returns401()
    {
        _client.DefaultRequestHeaders.Authorization = null;
        var response = await _client.GetAsync("/api/users/me");
        _client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Test");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task GetProfile_ShouldCallServiceWithJwtUserId()
    {
        // 模拟另一用户登录：ICurrentUserContext.UserId 返回 AnotherUserId
        // 验证 /me 端点不传 userId 参数，服务端从 JWT 取 userId，不会越权查到其他用户
        _currentUserMock.SetupGet(c => c.IsAuthenticated).Returns(true);
        _currentUserMock.SetupGet(c => c.UserId).Returns(AnotherUserId);
        _currentUserMock.SetupGet(c => c.Role).Returns("Buyer");
        SetTestRole("Buyer");

        var dto = new UserDto
        {
            Id = AnotherUserId,
            Username = "another",
            Email = "another@example.com",
            Nickname = "Another",
            Status = AccountStatus.Active,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        _userProfileAppServiceMock.Setup(s => s.GetProfileAsync(AnotherUserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(dto);

        var response = await _client.GetAsync("/api/users/me");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await response.Content.ReadFromJsonAsync<ApiResponse<UserDto>>();
        result!.Data!.Id.Should().Be(AnotherUserId, "应返回当前登录用户自己的信息");

        _userProfileAppServiceMock.Verify(
            s => s.GetProfileAsync(AnotherUserId, It.IsAny<CancellationToken>()),
            Times.Once,
            "/me 端点从 ICurrentUserContext.UserId 取，禁止客户端传 userId");
    }

    #endregion

    #region UsersController - UpdateProfile (PUT /api/users/me)

    [Fact]
    public async Task UpdateProfile_WithAuth_ReturnsOk()
    {
        SetupAuth();
        var dto = new UserDto
        {
            Id = UserId,
            Username = "testuser",
            Email = "test@example.com",
            Nickname = "Updated Nickname",
            Status = AccountStatus.Active,
            CreatedAt = DateTime.UtcNow.AddDays(-30),
            UpdatedAt = DateTime.UtcNow
        };
        _userProfileAppServiceMock.Setup(s => s.UpdateProfileAsync(UserId, It.IsAny<UpdateProfileDto>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(dto);

        var body = new { nickname = "Updated Nickname", avatarUrl = "https://example.com/avatar.png" };
        var response = await _client.PutAsJsonAsync("/api/users/me", body);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await response.Content.ReadFromJsonAsync<ApiResponse<UserDto>>();
        result!.Code.Should().Be(200);
        result.Data!.Nickname.Should().Be("Updated Nickname");
    }

    #endregion

    #region UsersController - ChangePassword (PUT /api/users/me/password)

    [Fact]
    public async Task ChangePassword_WithAuth_ReturnsOk()
    {
        SetupAuth();
        _userProfileAppServiceMock.Setup(s => s.ChangePasswordAsync(UserId, It.IsAny<ChangePasswordDto>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var body = new { oldPassword = "OldPassword123", newPassword = "NewPassword456" };
        var response = await _client.PutAsJsonAsync("/api/users/me/password", body);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await response.Content.ReadFromJsonAsync<ApiResponse>();
        result!.Code.Should().Be(200);
        result.Message.Should().Be("密码修改成功");
    }

    #endregion

    #region UsersController - EnableTwoFactor (POST /api/users/me/two-factor/enable)

    [Fact]
    public async Task EnableTwoFactor_WithAuth_ReturnsOk()
    {
        SetupAuth();
        var responseDto = new TwoFactorEnableResponseDto
        {
            Secret = "JBSWY3DPEHPK3PXP",
            QrCodeUri = "otpauth://totp/Leno:testuser?secret=JBSWY3DPEHPK3PXP&issuer=Leno"
        };
        _twoFactorServiceMock.Setup(s => s.EnableTwoFactorAsync(UserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(responseDto);

        var response = await _client.PostAsync("/api/users/me/two-factor/enable", null);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await response.Content.ReadFromJsonAsync<ApiResponse<TwoFactorEnableResponseDto>>();
        result!.Code.Should().Be(200);
        result.Data!.Secret.Should().Be("JBSWY3DPEHPK3PXP");
        result.Data.QrCodeUri.Should().StartWith("otpauth://totp/");
    }

    #endregion

    #region UsersController - ConfirmTwoFactor (POST /api/users/me/two-factor/confirm)

    [Fact]
    public async Task ConfirmTwoFactor_WithAuth_ReturnsOk()
    {
        SetupAuth();
        _twoFactorServiceMock.Setup(s => s.ConfirmTwoFactorAsync(UserId, "123456", It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var body = new { code = "123456" };
        var response = await _client.PostAsJsonAsync("/api/users/me/two-factor/confirm", body);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await response.Content.ReadFromJsonAsync<ApiResponse>();
        result!.Code.Should().Be(200);
        result.Message.Should().Be("双因子认证已启用");
    }

    #endregion

    #region UsersController - DisableTwoFactor (POST /api/users/me/two-factor/disable)

    [Fact]
    public async Task DisableTwoFactor_WithAuth_ReturnsOk()
    {
        SetupAuth();
        _twoFactorServiceMock.Setup(s => s.DisableTwoFactorAsync(UserId, It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var response = await _client.PostAsync("/api/users/me/two-factor/disable", null);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await response.Content.ReadFromJsonAsync<ApiResponse>();
        result!.Code.Should().Be(200);
        result.Message.Should().Be("双因子认证已禁用");
    }

    #endregion

    #region AccountController - BindExternalLogin (POST /api/account/external-logins)

    [Fact]
    public async Task BindExternalLogin_WithAuth_ReturnsOk()
    {
        SetupAuth();
        _externalLoginServiceMock.Setup(s => s.BindAsync(UserId, "google", It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var body = new
        {
            provider = "google",
            code = "auth-code-xyz",
            redirectUri = "https://example.com/callback"
        };
        var response = await _client.PostAsJsonAsync("/api/account/external-logins", body);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await response.Content.ReadFromJsonAsync<ApiResponse>();
        result!.Code.Should().Be(200);
        result.Message.Should().Be("外部登录绑定成功");
    }

    [Fact]
    public async Task BindExternalLogin_WithoutAuth_Returns401()
    {
        _client.DefaultRequestHeaders.Authorization = null;
        var body = new
        {
            provider = "google",
            code = "auth-code-xyz",
            redirectUri = "https://example.com/callback"
        };
        var response = await _client.PostAsJsonAsync("/api/account/external-logins", body);
        _client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Test");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    #endregion

    #region AccountController - UnbindExternalLogin (DELETE /api/account/external-logins/{provider})

    [Fact]
    public async Task UnbindExternalLogin_WithAuth_ReturnsOk()
    {
        SetupAuth();
        _externalLoginServiceMock.Setup(s => s.UnbindAsync(UserId, "google", It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var response = await _client.DeleteAsync("/api/account/external-logins/google");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await response.Content.ReadFromJsonAsync<ApiResponse>();
        result!.Code.Should().Be(200);
        result.Message.Should().Be("外部登录解绑成功");
    }

    #endregion

    #region Auth Helpers

    private void SetupAuth()
    {
        _currentUserMock.SetupGet(c => c.IsAuthenticated).Returns(true);
        _currentUserMock.SetupGet(c => c.UserId).Returns(UserId);
        _currentUserMock.SetupGet(c => c.Role).Returns("Buyer");
        // Identity 控制器无 RBAC 角色限制，[Authorize] 通过即可
        SetTestRole("Buyer");
    }

    private void SetTestRole(string role)
    {
        // 移除旧的 X-Test-Role 头（若存在），再添加当前角色
        _client.DefaultRequestHeaders.Remove("X-Test-Role");
        _client.DefaultRequestHeaders.Add("X-Test-Role", role);
    }

    #endregion
}

/// <summary>
/// OAuth 登录响应反序列化载体（与 AuthController.OAuthLoginAsync 返回的匿名对象结构对齐）。
/// </summary>
public sealed class OAuthLoginResponse
{
    public string AuthorizationUrl { get; set; } = string.Empty;
}

/// <summary>
/// 测试鉴权处理器，模拟 JWT 鉴权。
/// 通过 X-Test-Role 请求头控制注入的角色，便于 RBAC 403 测试：
/// - 头存在时：仅注入指定角色（如 Buyer），访问运营端 [Authorize(Roles="Operator,Admin")] 返回 403
/// - 头不存在时：注入全部角色（Buyer/Seller/Admin/Operator），[Authorize] 始终通过
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

        var testRoleHeader = Request.Headers["X-Test-Role"].FirstOrDefault();
        if (!string.IsNullOrEmpty(testRoleHeader))
        {
            // 头存在：仅注入指定角色，用于 RBAC 403 测试
            foreach (var role in testRoleHeader.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            {
                claims.Add(new Claim(ClaimTypes.Role, role));
            }
        }
        else
        {
            // 头不存在：注入全部角色，[Authorize] 始终通过
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
