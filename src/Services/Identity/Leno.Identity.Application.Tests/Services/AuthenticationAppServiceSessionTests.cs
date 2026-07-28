using System.Net;
using Grpc.Core;
using Leno.Identity.Application.DTOs;
using Leno.Identity.Application.Services;
using Leno.Identity.Domain.Aggregates;
using Leno.Identity.Domain.Repositories;
using Leno.Identity.Domain.Services;
using Leno.Infrastructure.Abstractions.Sessions;
using Leno.Infrastructure.Abstractions.UserAgent;
using Leno.Infrastructure.Security;
using Leno.SharedContracts.Events;
using Leno.SharedContracts.Grpc.AccessControl.V1;
using Leno.SharedKernel.Abstractions;
using MassTransit;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using System.IdentityModel.Tokens.Jwt;

namespace Leno.Identity.Application.Tests.Services;

/// <summary>
/// AuthenticationAppService 登录会话写入与事件发布单元测试（P0 系统管理 spec §5.10）。
/// 覆盖：登录成功写 Redis 会话 + 发布 UserLoggedInEvent（Success=true）；
/// 登录失败（密码错误/用户不存在）发布 UserLoggedInEvent（Success=false）；
/// Redis 写入异常仅记日志不阻塞登录；User-Agent 缺失仍发布事件。
/// </summary>
public sealed class AuthenticationAppServiceSessionTests
{
    private readonly Mock<IUserRepository> _userRepository = new();
    private readonly Mock<IRefreshTokenRepository> _refreshTokenRepository = new();
    private readonly Mock<IOAuthClientRepository> _oauthClientRepository = new();
    private readonly Mock<Leno.Identity.Domain.Services.IPasswordHasher> _passwordHasher = new();
    private readonly Mock<IUnitOfWork> _unitOfWork = new();
    private readonly Mock<IOAuth2ProviderFactory> _oauthFactory = new();
    private readonly Mock<IBcryptToArgon2Migrator> _migrator = new();
    private readonly Mock<IUserSessionStore> _sessionStore = new();
    private readonly Mock<IUserAgentParser> _uaParser = new();
    private readonly Mock<IPublishEndpoint> _publishEndpoint = new();
    private readonly Mock<IHttpContextAccessor> _httpContextAccessor = new();
    private readonly ILogger<AuthenticationAppService> _logger = NullLogger<AuthenticationAppService>.Instance;

    private const string ValidUsername = "admin";
    private const string ValidPassword = "Password123!";
    private const string RawUserAgent = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) Chrome/120.0";
    private const string ClientIp = "203.0.113.10";
    // FakeAccessToken 的 payload 部分为 Base64URL 编码的 {"jti":"test-session-id-001"}，
    // 确保 ExtractSessionIdFromToken 能解析出 sessionId，从而触发 RecordAsync 调用。
    private const string FakeAccessToken = "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9.eyJqdGkiOiJ0ZXN0LXNlc3Npb24taWQtMDAxIn0.signature";
    private const string FakeRefreshToken = "fake-refresh-token-string";
    private static readonly string ValidSigningKey = new('x', 48);

    private AuthenticationAppService CreateService()
    {
        var jwtTokenService = CreateJwtTokenService();
        return new AuthenticationAppService(
            _userRepository.Object,
            _refreshTokenRepository.Object,
            _oauthClientRepository.Object,
            _passwordHasher.Object,
            _unitOfWork.Object,
            jwtTokenService,
            _oauthFactory.Object,
            _migrator.Object,
            _sessionStore.Object,
            _uaParser.Object,
            _publishEndpoint.Object,
            _httpContextAccessor.Object,
            _logger);
    }

    /// <summary>
    /// 创建真实 JwtTokenService 实例（sealed 类不可 Mock）。
    /// 通过 Mock IJwtSigningService.SignAsync 返回 FakeAccessToken；
    /// 通过 Mock CallInvoker 让 GetUserRolesAsync gRPC 调用返回空角色列表。
    /// </summary>
    private JwtTokenService CreateJwtTokenService()
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
            .Setup(s => s.SignAsync(It.IsAny<JwtPayload>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(FakeAccessToken);

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

    private User BuildUser()
    {
        return User.Create(
            Guid.NewGuid(),
            ValidUsername,
            "admin@leno.com",
            "+8613800013800",
            "hashed-password",
            "Admin",
            null);
    }

    private void SetupHttpContext(string userAgent, string ip)
    {
        var httpContext = new DefaultHttpContext();
        httpContext.Request.Headers.UserAgent = userAgent;
        httpContext.Connection.RemoteIpAddress = IPAddress.Parse(ip);
        _httpContextAccessor.SetupGet(x => x.HttpContext).Returns(httpContext);
    }

    private void SetupSuccessfulLoginFlow(User user)
    {
        _userRepository.Setup(r => r.GetByUsernameAsync(ValidUsername, It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);
        _passwordHasher.Setup(h => h.Verify(ValidPassword, user.PasswordHash!)).Returns(true);
        _migrator.Setup(m => m.TryMigrateAsync(user, ValidPassword, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        _uaParser.Setup(p => p.ParseBrowser(RawUserAgent)).Returns("Chrome 120");
        _uaParser.Setup(p => p.ParseOs(RawUserAgent)).Returns("Windows 11");
        _uaParser.Setup(p => p.ParseDeviceFingerprint(RawUserAgent)).Returns("fp1a2b3c4");
    }

    [Fact]
    public async Task LoginAsync_Success_CallsUserSessionStoreRecordAsync()
    {
        var user = BuildUser();
        SetupSuccessfulLoginFlow(user);
        SetupHttpContext(RawUserAgent, ClientIp);
        var service = CreateService();
        var dto = new LoginDto { UsernameOrEmail = ValidUsername, Password = ValidPassword };

        await service.LoginAsync(dto);

        _sessionStore.Verify(
            s => s.RecordAsync(It.Is<OnlineUserSession>(session =>
                session.UserId == user.Id
                && session.Username == ValidUsername
                && session.IpAddress == ClientIp
                && session.Browser == "Chrome 120"
                && session.Os == "Windows 11"),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task LoginAsync_Success_PublishesUserLoggedInEventWithSuccessTrue()
    {
        var user = BuildUser();
        SetupSuccessfulLoginFlow(user);
        SetupHttpContext(RawUserAgent, ClientIp);
        var service = CreateService();
        var dto = new LoginDto { UsernameOrEmail = ValidUsername, Password = ValidPassword };

        await service.LoginAsync(dto);

        _publishEndpoint.Verify(
            p => p.Publish(It.Is<UserLoggedInEvent>(e =>
                e.Success == true
                && e.UserId == user.Id
                && e.Username == ValidUsername
                && e.IpAddress == ClientIp
                && e.UserAgent == RawUserAgent
                && e.FailureReason == null),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task LoginAsync_InvalidPassword_PublishesUserLoggedInEventWithSuccessFalse()
    {
        var user = BuildUser();
        _userRepository.Setup(r => r.GetByUsernameAsync(ValidUsername, It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);
        _passwordHasher.Setup(h => h.Verify(ValidPassword, user.PasswordHash!)).Returns(false);
        SetupHttpContext(RawUserAgent, ClientIp);
        var service = CreateService();
        var dto = new LoginDto { UsernameOrEmail = ValidUsername, Password = ValidPassword };

        Func<Task> act = () => service.LoginAsync(dto);
        await act.Should().ThrowAsync<Leno.Identity.Domain.Exceptions.IdentityDomainException>();

        _publishEndpoint.Verify(
            p => p.Publish(It.Is<UserLoggedInEvent>(e =>
                e.Success == false
                && e.UserId == user.Id
                && e.Username == ValidUsername
                && e.FailureReason != null),
                It.IsAny<CancellationToken>()),
            Times.Once);
        _sessionStore.Verify(
            s => s.RecordAsync(It.IsAny<OnlineUserSession>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task LoginAsync_UserNotFound_PublishesUserLoggedInEventWithNullUserId()
    {
        _userRepository.Setup(r => r.GetByUsernameAsync("nobody", It.IsAny<CancellationToken>()))
            .ReturnsAsync((User?)null);
        SetupHttpContext(RawUserAgent, ClientIp);
        var service = CreateService();
        var dto = new LoginDto { UsernameOrEmail = "nobody", Password = ValidPassword };

        Func<Task> act = () => service.LoginAsync(dto);
        await act.Should().ThrowAsync<Leno.Identity.Domain.Exceptions.IdentityDomainException>();

        _publishEndpoint.Verify(
            p => p.Publish(It.Is<UserLoggedInEvent>(e =>
                e.Success == false
                && e.UserId == null
                && e.Username == "nobody"
                && e.FailureReason != null),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task LoginAsync_SessionStoreThrows_LogsWarningAndDoesNotRethrow()
    {
        var user = BuildUser();
        SetupSuccessfulLoginFlow(user);
        SetupHttpContext(RawUserAgent, ClientIp);
        _sessionStore.Setup(s => s.RecordAsync(It.IsAny<OnlineUserSession>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Redis connection refused"));
        var service = CreateService();
        var dto = new LoginDto { UsernameOrEmail = ValidUsername, Password = ValidPassword };

        var result = await service.LoginAsync(dto);

        result.AccessToken.Should().Be(FakeAccessToken);
        _publishEndpoint.Verify(
            p => p.Publish(It.Is<UserLoggedInEvent>(e => e.Success == true),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task LoginAsync_Success_PublishesEventEvenWhenUserAgentMissing()
    {
        var user = BuildUser();
        SetupSuccessfulLoginFlow(user);
        var httpContext = new DefaultHttpContext();
        httpContext.Connection.RemoteIpAddress = IPAddress.Parse(ClientIp);
        _httpContextAccessor.SetupGet(x => x.HttpContext).Returns(httpContext);
        var service = CreateService();
        var dto = new LoginDto { UsernameOrEmail = ValidUsername, Password = ValidPassword };

        await service.LoginAsync(dto);

        _publishEndpoint.Verify(
            p => p.Publish(It.Is<UserLoggedInEvent>(e =>
                e.UserAgent == string.Empty
                && e.IpAddress == ClientIp),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }
}
