using System.Security.Claims;
using Grpc.Core;
using Leno.Identity.Application;
using Leno.Identity.Application.Services;
using Leno.Identity.Domain.Aggregates;
using Leno.Identity.Domain.Exceptions;
using Leno.Identity.Domain.Repositories;
using Leno.Identity.Domain.Services;
using Leno.Identity.Domain.ValueObjects;
using Leno.Infrastructure.Security;
using Leno.SharedContracts.Grpc.AccessControl.V1;
using Leno.SharedKernel.Abstractions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Leno.Identity.Application.Tests.OAuth;

/// <summary>
/// AuthenticationAppService.HandleOAuthCallbackAsync 单元测试（Identity BC，3.7 OAuth/SSO 通用化）。
/// 覆盖 OAuth 回调编排流：provider 配置查找 → 适配器路由 → 授权码交换 → userinfo 拉取 →
/// claim 映射 → 用户绑定/自动创建 → 刷新令牌签发。
/// </summary>
public class AuthenticationAppServiceOAuthTests
{
    private static readonly string ValidSigningKey = new('x', 48);

    [Fact]
    public async Task HandleOAuthCallbackAsync_With_Existing_User_Should_Issue_Tokens()
    {
        var oauthClient = CreateOidcClient(enabled: true);
        var existingUser = CreateUser();
        var adapter = CreateMockAdapter(
            tokenResponse: new TokenResponse { AccessToken = "at-123" },
            userInfo: new UserInfoResponse
            {
                RawClaims = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                {
                    ["sub"] = "ext-123",
                    ["email"] = "user@example.com",
                    ["name"] = "Test User"
                }
            },
            principal: BuildPrincipal(("sub", "ext-123"), ("email", "user@example.com"), ("name", "Test User")));

        var (service, mocks) = BuildService(
            oauthClient: oauthClient,
            existingUser: existingUser,
            adapter: adapter);

        var result = await service.HandleOAuthCallbackAsync("google", "auth-code", "https://leno.local/callback", CancellationToken.None);

        result.AccessToken.Should().NotBeNullOrEmpty();
        result.RefreshToken.Should().NotBeNullOrEmpty();
        result.ExpiresAt.Should().BeAfter(DateTime.UtcNow);

        mocks.UserRepo.Verify(r => r.UpdateAsync(existingUser, It.IsAny<CancellationToken>()), Times.Once);
        mocks.UserRepo.Verify(r => r.AddAsync(It.IsAny<User>(), It.IsAny<CancellationToken>()), Times.Never);
        mocks.RefreshTokenRepo.Verify(r => r.AddAsync(It.IsAny<RefreshToken>(), It.IsAny<CancellationToken>()), Times.Once);
        mocks.UnitOfWork.Verify(u => u.SaveEntitiesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task HandleOAuthCallbackAsync_With_New_User_Should_Auto_Create()
    {
        var oauthClient = CreateOidcClient(enabled: true);
        var adapter = CreateMockAdapter(
            tokenResponse: new TokenResponse { AccessToken = "at-123" },
            userInfo: new UserInfoResponse
            {
                RawClaims = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                {
                    ["sub"] = "ext-new",
                    ["email"] = "newuser@example.com",
                    ["name"] = "New User"
                }
            },
            principal: BuildPrincipal(("sub", "ext-new"), ("email", "newuser@example.com"), ("name", "New User")));

        var (service, mocks) = BuildService(
            oauthClient: oauthClient,
            existingUser: null,
            adapter: adapter);

        var result = await service.HandleOAuthCallbackAsync("google", "auth-code", "https://leno.local/callback", CancellationToken.None);

        result.AccessToken.Should().NotBeNullOrEmpty();
        result.RefreshToken.Should().NotBeNullOrEmpty();

        mocks.UserRepo.Verify(r => r.AddAsync(It.IsAny<User>(), It.IsAny<CancellationToken>()), Times.Once);
        mocks.UserRepo.Verify(r => r.UpdateAsync(It.IsAny<User>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task HandleOAuthCallbackAsync_With_Empty_Provider_Should_Throw()
    {
        var (service, _) = BuildService();

        var act = async () => await service.HandleOAuthCallbackAsync("", "code", "https://leno.local/callback", CancellationToken.None);

        (await act.Should().ThrowAsync<IdentityDomainException>())
            .Which.ErrorCode.Should().Be("OAUTH_PROVIDER_EMPTY");
    }

    [Fact]
    public async Task HandleOAuthCallbackAsync_With_Empty_Code_Should_Throw()
    {
        var (service, _) = BuildService();

        var act = async () => await service.HandleOAuthCallbackAsync("google", "", "https://leno.local/callback", CancellationToken.None);

        (await act.Should().ThrowAsync<IdentityDomainException>())
            .Which.ErrorCode.Should().Be("OAUTH_CODE_EMPTY");
    }

    [Fact]
    public async Task HandleOAuthCallbackAsync_With_Empty_RedirectUri_Should_Throw()
    {
        var (service, _) = BuildService();

        var act = async () => await service.HandleOAuthCallbackAsync("google", "code", "", CancellationToken.None);

        (await act.Should().ThrowAsync<IdentityDomainException>())
            .Which.ErrorCode.Should().Be("OAUTH_REDIRECT_URI_EMPTY");
    }

    [Fact]
    public async Task HandleOAuthCallbackAsync_With_Client_Not_Found_Should_Throw()
    {
        var (service, mocks) = BuildService(oauthClient: null);
        mocks.OAuthClientRepo
            .Setup(r => r.GetByProviderAsync("unknown", It.IsAny<CancellationToken>()))
            .ReturnsAsync((OAuthClient?)null);

        var act = async () => await service.HandleOAuthCallbackAsync("unknown", "code", "https://leno.local/callback", CancellationToken.None);

        (await act.Should().ThrowAsync<IdentityDomainException>())
            .Which.ErrorCode.Should().Be("OAUTH_CLIENT_NOT_FOUND");
    }

    [Fact]
    public async Task HandleOAuthCallbackAsync_With_Disabled_Client_Should_Throw()
    {
        var oauthClient = CreateOidcClient(enabled: false);
        var (service, _) = BuildService(oauthClient: oauthClient);

        var act = async () => await service.HandleOAuthCallbackAsync("google", "code", "https://leno.local/callback", CancellationToken.None);

        (await act.Should().ThrowAsync<IdentityDomainException>())
            .Which.ErrorCode.Should().Be("OAUTH_CLIENT_DISABLED");
    }

    [Fact]
    public async Task HandleOAuthCallbackAsync_With_Empty_AccessToken_Should_Throw()
    {
        var oauthClient = CreateOidcClient(enabled: true);
        var adapter = CreateMockAdapter(
            tokenResponse: new TokenResponse { AccessToken = "" });

        var (service, _) = BuildService(oauthClient: oauthClient, adapter: adapter);

        var act = async () => await service.HandleOAuthCallbackAsync("google", "code", "https://leno.local/callback", CancellationToken.None);

        (await act.Should().ThrowAsync<IdentityDomainException>())
            .Which.ErrorCode.Should().Be("OAUTH_TOKEN_EMPTY");
    }

    [Fact]
    public async Task HandleOAuthCallbackAsync_With_Empty_Sub_Should_Throw()
    {
        var oauthClient = CreateOidcClient(enabled: true);
        var adapter = CreateMockAdapter(
            tokenResponse: new TokenResponse { AccessToken = "at-123" },
            userInfo: new UserInfoResponse
            {
                RawClaims = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                {
                    ["email"] = "user@example.com"
                }
            });

        var (service, _) = BuildService(oauthClient: oauthClient, adapter: adapter);

        var act = async () => await service.HandleOAuthCallbackAsync("google", "code", "https://leno.local/callback", CancellationToken.None);

        (await act.Should().ThrowAsync<IdentityDomainException>())
            .Which.ErrorCode.Should().Be("OAUTH_USER_ID_EMPTY");
    }

    [Fact]
    public async Task HandleOAuthCallbackAsync_With_Locked_User_Should_Throw()
    {
        var oauthClient = CreateOidcClient(enabled: true);
        var lockedUser = CreateUser(status: AccountStatus.Disabled);
        var adapter = CreateMockAdapter(
            tokenResponse: new TokenResponse { AccessToken = "at-123" },
            userInfo: new UserInfoResponse
            {
                RawClaims = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                {
                    ["sub"] = "ext-locked"
                }
            },
            principal: BuildPrincipal(("sub", "ext-locked")));

        var (service, _) = BuildService(oauthClient: oauthClient, existingUser: lockedUser, adapter: adapter);

        var act = async () => await service.HandleOAuthCallbackAsync("google", "code", "https://leno.local/callback", CancellationToken.None);

        (await act.Should().ThrowAsync<IdentityDomainException>())
            .Which.ErrorCode.Should().Be("USER_LOCKED_OR_DISABLED");
    }

    /// <summary>构建被测服务与依赖 Mock 集合。</summary>
    private static (AuthenticationAppService Service, ServiceMocks Mocks) BuildService(
        OAuthClient? oauthClient = null,
        User? existingUser = null,
        Mock<IOAuth2ProviderAdapter>? adapter = null)
    {
        var userRepo = new Mock<IUserRepository>();
        var refreshTokenRepo = new Mock<IRefreshTokenRepository>();
        var oauthClientRepo = new Mock<IOAuthClientRepository>();
        var passwordHasher = new Mock<Leno.Identity.Domain.Services.IPasswordHasher>();
        var unitOfWork = new Mock<IUnitOfWork>();
        var providerFactory = new Mock<IOAuth2ProviderFactory>();
        var passwordMigrator = new Mock<IBcryptToArgon2Migrator>();
        passwordMigrator
            .Setup(m => m.TryMigrateAsync(It.IsAny<User>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        var logger = new Mock<ILogger<AuthenticationAppService>>();

        oauthClientRepo
            .Setup(r => r.GetByProviderAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(oauthClient);

        userRepo
            .Setup(r => r.FindByExternalLoginAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(existingUser);

        var jwtService = CreateJwtTokenService();

        if (adapter is not null)
        {
            var adapterObj = adapter.Object;
            providerFactory
                .Setup(f => f.GetAdapter(It.IsAny<string>()))
                .Returns(adapterObj);
        }

        var service = new AuthenticationAppService(
            userRepo.Object,
            refreshTokenRepo.Object,
            oauthClientRepo.Object,
            passwordHasher.Object,
            unitOfWork.Object,
            jwtService,
            providerFactory.Object,
            passwordMigrator.Object,
            logger.Object);

        return (service, new ServiceMocks(userRepo, refreshTokenRepo, oauthClientRepo, unitOfWork));
    }

    /// <summary>创建真实 JwtTokenService（gRPC 客户端返回空角色列表，签名服务返回占位令牌）。</summary>
    private static JwtTokenService CreateJwtTokenService()
    {
        var options = Options.Create(new JwtOptions
        {
            SigningKey = ValidSigningKey,
            Issuer = "leno-identity-test",
            Audience = "leno-clients-test",
            AccessTokenExpirationMinutes = 30,
            RefreshTokenExpirationDays = 7
        });

        var signingService = new Mock<IJwtSigningService>();
        signingService
            .Setup(s => s.SignAsync(It.IsAny<System.IdentityModel.Tokens.Jwt.JwtPayload>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("dummy-signed-token");

        var mockInvoker = new Mock<CallInvoker>();
        mockInvoker
            .Setup(i => i.AsyncUnaryCall(
                It.IsAny<Method<GetUserRolesRequest, GetUserRolesResponse>>(),
                It.IsAny<string?>(),
                It.IsAny<CallOptions>(),
                It.IsAny<GetUserRolesRequest>()))
            .Returns(new AsyncUnaryCall<GetUserRolesResponse>(
                Task.FromResult(new GetUserRolesResponse()),
                Task.FromResult(new Metadata()),
                () => Status.DefaultSuccess,
                () => new Metadata(),
                () => { }));

        var client = new AccessControlService.AccessControlServiceClient(mockInvoker.Object);
        var logger = Mock.Of<ILogger<JwtTokenService>>();

        return new JwtTokenService(options, signingService.Object, client, logger);
    }

    private static OAuthClient CreateOidcClient(bool enabled)
    {
        return OAuthClient.Create(
            Guid.NewGuid(),
            "google",
            "Oidc",
            "client-id",
            "encrypted-secret",
            "https://leno.local/callback",
            new[] { "openid", "email", "profile" },
            "https://accounts.google.com/.well-known/openid-configuration",
            null,
            enabled);
    }

    private static User CreateUser(AccountStatus status = AccountStatus.Active)
    {
        var info = new ExternalLoginInfo("google", "ext-123", "user@example.com", "Test User", null);
        var user = User.CreateFromExternal(Guid.NewGuid(), info);
        // CreateFromExternal 固定 Active，通过聚合方法切换到 Disabled 状态以测试 CanLogin 拦截
        if (status == AccountStatus.Disabled)
        {
            user.Disable("test-suspension");
        }

        return user;
    }

    private static ClaimsPrincipal BuildPrincipal(params (string Type, string Value)[] claims)
    {
        var identity = new ClaimsIdentity(claims.Select(c => new Claim(c.Type, c.Value)), "Oidc", "name", "role");
        return new ClaimsPrincipal(identity);
    }

    private static Mock<IOAuth2ProviderAdapter> CreateMockAdapter(
        TokenResponse? tokenResponse = null,
        UserInfoResponse? userInfo = null,
        ClaimsPrincipal? principal = null)
    {
        var adapter = new Mock<IOAuth2ProviderAdapter>();
        adapter.SetupGet(a => a.ProviderType).Returns("Oidc");

        adapter
            .Setup(a => a.ExchangeCodeForTokenAsync(It.IsAny<OAuthClient>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(tokenResponse ?? new TokenResponse { AccessToken = "at-default" });

        adapter
            .Setup(a => a.GetUserInfoAsync(It.IsAny<OAuthClient>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(userInfo ?? new UserInfoResponse());

        adapter
            .Setup(a => a.MapClaimsAsync(It.IsAny<UserInfoResponse>(), It.IsAny<OidcClaimMapping>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(principal ?? new ClaimsPrincipal(new ClaimsIdentity("Oidc")));

        return adapter;
    }

    /// <summary>聚合测试用 Mock 引用，便于断言验证。</summary>
    private sealed record ServiceMocks(
        Mock<IUserRepository> UserRepo,
        Mock<IRefreshTokenRepository> RefreshTokenRepo,
        Mock<IOAuthClientRepository> OAuthClientRepo,
        Mock<IUnitOfWork> UnitOfWork);
}
