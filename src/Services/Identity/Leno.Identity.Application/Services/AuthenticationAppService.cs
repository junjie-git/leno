using System.Diagnostics;
using System.Net;
using System.Security.Claims;
using Leno.Identity.Application.DTOs;
using Leno.Identity.Domain.Aggregates;
using Leno.Identity.Domain.Events;
using Leno.Identity.Domain.Exceptions;
using Leno.Identity.Domain.Repositories;
using Leno.Identity.Domain.Services;
using Leno.Identity.Domain.ValueObjects;
using Leno.Infrastructure.Abstractions.Sessions;
using Leno.Infrastructure.Abstractions.UserAgent;
using Leno.SharedContracts.Events;
using Leno.SharedKernel.Abstractions;
using MassTransit;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace Leno.Identity.Application.Services;

/// <summary>
/// 认证应用服务实现，编排登录、刷新、登出与 OAuth 回调用例（Identity BC，3.6 AuthN/AuthZ 拆分）。
/// <para>
/// 编排流：
/// <list type="bullet">
/// <item><b>LoginAsync</b>：查找用户 → 校验账户状态 → 验证密码 → 重置失败计数 →
/// 签发刷新令牌聚合 → 发布 <see cref="UserAuthenticatedEvent"/> → 提交工作单元 → 生成访问令牌 →
/// 写入在线会话至 Redis（失败仅记日志，不阻塞登录）→ 发布 <see cref="UserLoggedInEvent"/>（成功/失败均发布）。</item>
/// <item><b>RefreshAsync</b>：校验刷新令牌有效 → 轮换（旧令牌 Rotate，新令牌签发）→ 提交 → 生成新访问令牌。</item>
/// <item><b>LogoutAsync</b>：吊销用户所有活跃刷新令牌 → 提交。</item>
/// <item><b>HandleOAuthCallbackAsync</b>（3.7 OAuth/SSO 通用化）：按 provider slug 查找 OAuthClient 配置 →
/// 通过 <see cref="IOAuth2ProviderFactory"/> 按 ProviderType 解析适配器 → 交换授权码 → 拉取 IdP userinfo →
/// 映射 claim 为 ClaimsPrincipal → 按 (Provider, ProviderUserId) 查找已绑定用户，未找到则自动创建 →
/// 签发刷新令牌 → 生成访问令牌。</item>
/// </list>
/// </para>
/// <para>
/// 角色填充不在本类直接处理，由 <see cref="JwtTokenService.GenerateAccessToken"/> 调用
/// AccessControl BC <c>GetUserRoles</c> RPC 完成。
/// </para>
/// <para>
/// P0 系统管理改动（spec §5.10）：登录成功后同步写 Redis 会话 + 异步发布 UserLoggedInEvent；
/// 登录失败（密码错误/用户不存在）也发布 UserLoggedInEvent（Success=false）供安全审计。
/// Redis 写入失败仅记日志不抛异常，登录仍成功（spec §8 风险矩阵）。
/// </para>
/// </summary>
public sealed class AuthenticationAppService : IAuthenticationAppService
{
    private const string AuthMethodPassword = "Password";
    private const string AuthMethodRefreshToken = "RefreshToken";
    private const string AuthMethodOAuth = "OAuth";
    private const string RevokeReasonLogout = "Logout";
    private const string RevokeReasonRotated = "Rotated";

    /// <summary>登录失败原因常量（与 UserLoggedInEvent.FailureReason 对齐）。</summary>
    private const string FailureReasonInvalidCredentials = "用户名或密码错误";
    private const string FailureReasonUserNotFound = "用户不存在";

    /// <summary>标准 OIDC claim 名称（用于从 ClaimsPrincipal 提取 OAuth 用户信息）。</summary>
    private const string ClaimSub = "sub";
    private const string ClaimEmail = "email";
    private const string ClaimName = "name";
    private const string ClaimPicture = "picture";
    private const string ClaimAvatarUrl = "avatar_url";

    private readonly IUserRepository _userRepository;
    private readonly IRefreshTokenRepository _refreshTokenRepository;
    private readonly IOAuthClientRepository _oauthClientRepository;
    private readonly IPasswordHasher _passwordHasher;
    private readonly IUnitOfWork _unitOfWork;
    private readonly JwtTokenService _jwtTokenService;
    private readonly IOAuth2ProviderFactory _oauthProviderFactory;
    private readonly IBcryptToArgon2Migrator _passwordMigrator;
    private readonly IUserSessionStore _userSessionStore;
    private readonly IUserAgentParser _uaParser;
    private readonly IPublishEndpoint _publishEndpoint;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly ILogger<AuthenticationAppService> _logger;

    public AuthenticationAppService(
        IUserRepository userRepository,
        IRefreshTokenRepository refreshTokenRepository,
        IOAuthClientRepository oauthClientRepository,
        IPasswordHasher passwordHasher,
        IUnitOfWork unitOfWork,
        JwtTokenService jwtTokenService,
        IOAuth2ProviderFactory oauthProviderFactory,
        IBcryptToArgon2Migrator passwordMigrator,
        IUserSessionStore userSessionStore,
        IUserAgentParser uaParser,
        IPublishEndpoint publishEndpoint,
        IHttpContextAccessor httpContextAccessor,
        ILogger<AuthenticationAppService> logger)
    {
        _userRepository = userRepository ?? throw new ArgumentNullException(nameof(userRepository));
        _refreshTokenRepository = refreshTokenRepository ?? throw new ArgumentNullException(nameof(refreshTokenRepository));
        _oauthClientRepository = oauthClientRepository ?? throw new ArgumentNullException(nameof(oauthClientRepository));
        _passwordHasher = passwordHasher ?? throw new ArgumentNullException(nameof(passwordHasher));
        _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
        _jwtTokenService = jwtTokenService ?? throw new ArgumentNullException(nameof(jwtTokenService));
        _oauthProviderFactory = oauthProviderFactory ?? throw new ArgumentNullException(nameof(oauthProviderFactory));
        _passwordMigrator = passwordMigrator ?? throw new ArgumentNullException(nameof(passwordMigrator));
        _userSessionStore = userSessionStore ?? throw new ArgumentNullException(nameof(userSessionStore));
        _uaParser = uaParser ?? throw new ArgumentNullException(nameof(uaParser));
        _publishEndpoint = publishEndpoint ?? throw new ArgumentNullException(nameof(publishEndpoint));
        _httpContextAccessor = httpContextAccessor ?? throw new ArgumentNullException(nameof(httpContextAccessor));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <inheritdoc />
    public async Task<TokenDto> LoginAsync(LoginDto dto, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(dto);

        if (string.IsNullOrWhiteSpace(dto.UsernameOrEmail))
        {
            throw new IdentityDomainException("用户名或邮箱不可为空", "AUTH_IDENTIFIER_EMPTY");
        }

        if (string.IsNullOrWhiteSpace(dto.Password))
        {
            throw new IdentityDomainException("密码不可为空", "AUTH_PASSWORD_EMPTY");
        }

        var stopwatch = Stopwatch.StartNew();
        var identifier = dto.UsernameOrEmail.Trim();
        var httpContext = _httpContextAccessor.HttpContext;
        var ipAddress = ExtractClientIpAddress(httpContext);
        var userAgent = ExtractUserAgent(httpContext);

        var user = await FindUserByIdentifierAsync(identifier, ct).ConfigureAwait(false);
        if (user is null)
        {
            // 不暴露"用户不存在"以防枚举攻击，统一返回凭证无效
            _logger.LogWarning("登录失败：用户标识未找到，Identifier={Identifier}", identifier);
            await PublishLoginEventAsync(
                eventId: Guid.NewGuid(),
                username: identifier,
                userId: null,
                ipAddress: ipAddress,
                userAgent: userAgent,
                success: false,
                failureReason: FailureReasonUserNotFound,
                durationMs: (int)stopwatch.ElapsedMilliseconds,
                ct: ct).ConfigureAwait(false);
            throw new IdentityDomainException("用户名或密码错误", "AUTH_INVALID_CREDENTIALS");
        }

        if (!user.CanLogin())
        {
            _logger.LogWarning("登录被拒：账户不可登录，UserId={UserId}, Status={Status}",
                user.Id, user.Status);
            await PublishLoginEventAsync(
                eventId: Guid.NewGuid(),
                username: identifier,
                userId: user.Id,
                ipAddress: ipAddress,
                userAgent: userAgent,
                success: false,
                failureReason: "账户已锁定或禁用",
                durationMs: (int)stopwatch.ElapsedMilliseconds,
                ct: ct).ConfigureAwait(false);
            throw new IdentityDomainException("账户已锁定或禁用，无法登录", "USER_LOCKED_OR_DISABLED");
        }

        // 密码校验失败时也要持久化 FailedLoginCount 累加结果（可能触发账户锁定）
        if (!user.VerifyPassword(dto.Password, _passwordHasher))
        {
            try
            {
                await _userRepository.UpdateAsync(user, ct).ConfigureAwait(false);
                await _unitOfWork.SaveEntitiesAsync(ct).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "持久化登录失败计数时异常，UserId={UserId}", user.Id);
            }

            await PublishLoginEventAsync(
                eventId: Guid.NewGuid(),
                username: identifier,
                userId: user.Id,
                ipAddress: ipAddress,
                userAgent: userAgent,
                success: false,
                failureReason: FailureReasonInvalidCredentials,
                durationMs: (int)stopwatch.ElapsedMilliseconds,
                ct: ct).ConfigureAwait(false);
            throw new IdentityDomainException("用户名或密码错误", "AUTH_INVALID_CREDENTIALS");
        }

        // 登录成功：重置失败计数，发布领域事件
        user.RecordLogin(AuthMethodPassword);

        // 3.10 安全技术栈升级：bcrypt → Argon2id 懒迁移（登录成功后无感知升级）
        await _passwordMigrator.TryMigrateAsync(user, dto.Password, ct).ConfigureAwait(false);

        await _userRepository.UpdateAsync(user, ct).ConfigureAwait(false);

        // 签发刷新令牌
        var refreshToken = _jwtTokenService.GenerateRefreshToken(user.Id);
        await _refreshTokenRepository.AddAsync(refreshToken, ct).ConfigureAwait(false);

        // 同一事务提交聚合变更与领域事件（经 Outbox 持久化）
        await _unitOfWork.SaveEntitiesAsync(ct).ConfigureAwait(false);

        // 生成访问令牌（此时聚合变更已提交，调用 gRPC 获取角色）
        var accessToken = await _jwtTokenService.GenerateAccessToken(user, ct).ConfigureAwait(false);

        // P0 系统管理：登录成功后写入 Redis 在线会话（失败仅记日志，不阻塞登录）
        await RecordOnlineSessionSafeAsync(user, accessToken, ipAddress, userAgent, ct).ConfigureAwait(false);

        // P0 系统管理：发布 UserLoggedInEvent 供 SystemAdmin 消费写入 LoginLog
        await PublishLoginEventAsync(
            eventId: Guid.NewGuid(),
            username: user.Username,
            userId: user.Id,
            ipAddress: ipAddress,
            userAgent: userAgent,
            success: true,
            failureReason: null,
            durationMs: (int)stopwatch.ElapsedMilliseconds,
            ct: ct).ConfigureAwait(false);

        _logger.LogInformation("用户登录成功，UserId={UserId}, AuthMethod={AuthMethod}",
            user.Id, AuthMethodPassword);

        return new TokenDto
        {
            AccessToken = accessToken,
            RefreshToken = refreshToken.Token,
            ExpiresAt = _jwtTokenService.AccessTokenExpiresAt
        };
    }

    /// <inheritdoc />
    public async Task<TokenDto> RefreshAsync(RefreshTokenDto dto, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(dto);

        if (string.IsNullOrWhiteSpace(dto.RefreshToken))
        {
            throw new IdentityDomainException("刷新令牌不可为空", "AUTH_REFRESH_TOKEN_EMPTY");
        }

        var existingToken = await _refreshTokenRepository.GetByTokenAsync(dto.RefreshToken, ct)
            .ConfigureAwait(false);
        if (existingToken is null || !existingToken.IsActive)
        {
            _logger.LogWarning("刷新令牌无效或已过期");
            throw new IdentityDomainException("刷新令牌无效或已过期", "AUTH_REFRESH_TOKEN_INVALID");
        }

        var user = await _userRepository.GetByIdAsync(existingToken.UserId, ct).ConfigureAwait(false);
        if (user is null)
        {
            _logger.LogError("刷新令牌关联的用户不存在，UserId={UserId}", existingToken.UserId);
            throw new IdentityDomainException("用户不存在", "USER_NOT_FOUND");
        }

        if (!user.CanLogin())
        {
            _logger.LogWarning("账户不可登录，拒绝刷新令牌，UserId={UserId}, Status={Status}",
                user.Id, user.Status);
            throw new IdentityDomainException("账户已锁定或禁用，无法刷新令牌", "USER_LOCKED_OR_DISABLED");
        }

        // 轮换：旧令牌标记为 Rotated 并记录新令牌标识；新令牌签发
        var newRefreshToken = _jwtTokenService.GenerateRefreshToken(user.Id);
        existingToken.Rotate(newRefreshToken.Id);

        await _refreshTokenRepository.UpdateAsync(existingToken, ct).ConfigureAwait(false);
        await _refreshTokenRepository.AddAsync(newRefreshToken, ct).ConfigureAwait(false);

        // 发布刷新令牌轮换事件，供审计与风控消费
        user.RecordLogin(AuthMethodRefreshToken);

        await _userRepository.UpdateAsync(user, ct).ConfigureAwait(false);
        await _unitOfWork.SaveEntitiesAsync(ct).ConfigureAwait(false);

        var accessToken = await _jwtTokenService.GenerateAccessToken(user, ct).ConfigureAwait(false);

        _logger.LogInformation("刷新令牌轮换成功，UserId={UserId}, OldTokenId={OldTokenId}, NewTokenId={NewTokenId}",
            user.Id, existingToken.Id, newRefreshToken.Id);

        return new TokenDto
        {
            AccessToken = accessToken,
            RefreshToken = newRefreshToken.Token,
            ExpiresAt = _jwtTokenService.AccessTokenExpiresAt
        };
    }

    /// <inheritdoc />
    public async Task LogoutAsync(Guid userId, CancellationToken ct = default)
    {
        if (userId == Guid.Empty)
        {
            throw new ArgumentException("UserId 不可为空", nameof(userId));
        }

        await _refreshTokenRepository.RevokeAllByUserAsync(userId, RevokeReasonLogout, ct)
            .ConfigureAwait(false);
        await _unitOfWork.SaveEntitiesAsync(ct).ConfigureAwait(false);

        _logger.LogInformation("用户登出成功，已吊销所有刷新令牌，UserId={UserId}", userId);
    }

    /// <inheritdoc />
    public async Task<TokenDto> HandleOAuthCallbackAsync(
        string provider,
        string code,
        string redirectUri,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(provider))
        {
            throw new IdentityDomainException("OAuth 提供方不可为空", "OAUTH_PROVIDER_EMPTY");
        }
        if (string.IsNullOrWhiteSpace(code))
        {
            throw new IdentityDomainException("OAuth 授权码不可为空", "OAUTH_CODE_EMPTY");
        }
        if (string.IsNullOrWhiteSpace(redirectUri))
        {
            throw new IdentityDomainException("OAuth 回调地址不可为空", "OAUTH_REDIRECT_URI_EMPTY");
        }

        // 1. 按 provider slug 查找 OAuthClient 配置
        var oauthClient = await _oauthClientRepository.GetByProviderAsync(provider, ct).ConfigureAwait(false);
        if (oauthClient is null)
        {
            _logger.LogWarning("OAuth 回调失败：未找到 provider 配置，Provider={Provider}", provider);
            throw new IdentityDomainException($"未配置的 OAuth 提供方：{provider}", "OAUTH_CLIENT_NOT_FOUND");
        }

        if (!oauthClient.Enabled)
        {
            _logger.LogWarning("OAuth 回调失败：provider 已禁用，Provider={Provider}", provider);
            throw new IdentityDomainException($"OAuth 提供方已禁用：{provider}", "OAUTH_CLIENT_DISABLED");
        }

        // 2. 通过 ProviderType 解析适配器
        var adapter = _oauthProviderFactory.GetAdapter(oauthClient.ProviderType);
        _logger.LogInformation("OAuth 回调处理，Provider={Provider}, ProviderType={ProviderType}, Adapter={Adapter}",
            provider, oauthClient.ProviderType, adapter.GetType().Name);

        // 3. 授权码交换 → 拉取 userinfo → 映射 claim
        var tokenResponse = await adapter.ExchangeCodeForTokenAsync(oauthClient, code, redirectUri, ct)
            .ConfigureAwait(false);
        if (string.IsNullOrWhiteSpace(tokenResponse.AccessToken))
        {
            throw new IdentityDomainException("OAuth 适配器未返回 access_token", "OAUTH_TOKEN_EMPTY");
        }

        var userInfo = await adapter.GetUserInfoAsync(oauthClient, tokenResponse.AccessToken, ct)
            .ConfigureAwait(false);
        if (string.IsNullOrWhiteSpace(userInfo.Subject))
        {
            throw new IdentityDomainException("IdP userinfo 未返回 sub claim", "OAUTH_USER_ID_EMPTY");
        }

        // 应用 OAuthClient 自定义 claim 映射，未配置时使用默认 OIDC 映射
        var mapping = oauthClient.ClaimMappings.Count > 0
            ? new OidcClaimMapping { Mappings = oauthClient.ClaimMappings.ToList() }
            : OidcClaimMapping.Default;
        // 与默认映射合并：自定义规则优先于默认规则（相同 SourceClaim 时）
        mapping = OidcClaimMapping.Merge(OidcClaimMapping.Default, mapping);

        var principal = await adapter.MapClaimsAsync(userInfo, mapping, ct).ConfigureAwait(false);

        // 4. 从 ClaimsPrincipal 提取 ExternalLogin 所需信息
        var providerUserId = principal.FindFirst(ClaimSub)?.Value
            ?? userInfo.Subject;
        var email = principal.FindFirst(ClaimEmail)?.Value;
        var name = principal.FindFirst(ClaimName)?.Value;
        var avatarUrl = principal.FindFirst(ClaimPicture)?.Value
            ?? principal.FindFirst(ClaimAvatarUrl)?.Value;

        if (string.IsNullOrWhiteSpace(providerUserId))
        {
            throw new IdentityDomainException("OAuth 回调未返回第三方用户标识", "OAUTH_USER_ID_EMPTY");
        }

        // 5. 按 (Provider, ProviderUserId) 查找已绑定用户
        var user = await _userRepository.FindByExternalLoginAsync(provider, providerUserId, ct)
            .ConfigureAwait(false);

        if (user is null)
        {
            // 未找到则自动创建 OAuth 用户（无密码、无手机号）
            // 用户名生成由 User.CreateFromExternal 内部完成（从邮箱前缀或 GUID 兜底）
            var info = new ExternalLoginInfo(provider, providerUserId, email, name ?? string.Empty, avatarUrl);
            user = User.CreateFromExternal(Guid.NewGuid(), info);

            await _userRepository.AddAsync(user, ct).ConfigureAwait(false);
            _logger.LogInformation("OAuth 用户自动创建，Provider={Provider}, ProviderUserId={ProviderUserId}, UserId={UserId}",
                provider, providerUserId, user.Id);
        }
        else
        {
            if (!user.CanLogin())
            {
                _logger.LogWarning("OAuth 登录被拒：账户不可登录，UserId={UserId}, Status={Status}",
                    user.Id, user.Status);
                throw new IdentityDomainException("账户已锁定或禁用，无法登录", "USER_LOCKED_OR_DISABLED");
            }

            user.RecordLogin(AuthMethodOAuth);
            await _userRepository.UpdateAsync(user, ct).ConfigureAwait(false);
            _logger.LogInformation("OAuth 用户登录成功，Provider={Provider}, UserId={UserId}",
                provider, user.Id);
        }

        // 6. 签发刷新令牌
        var refreshToken = _jwtTokenService.GenerateRefreshToken(user.Id);
        await _refreshTokenRepository.AddAsync(refreshToken, ct).ConfigureAwait(false);

        // 7. 提交聚合变更与领域事件（经 Outbox 持久化）
        await _unitOfWork.SaveEntitiesAsync(ct).ConfigureAwait(false);

        // 8. 生成访问令牌（调用 gRPC 获取角色）
        var accessToken = await _jwtTokenService.GenerateAccessToken(user, ct).ConfigureAwait(false);

        return new TokenDto
        {
            AccessToken = accessToken,
            RefreshToken = refreshToken.Token,
            ExpiresAt = _jwtTokenService.AccessTokenExpiresAt
        };
    }

    /// <summary>
    /// 写入在线会话至 Redis（容错：失败仅记日志，不阻塞登录流程）。
    /// spec §8 风险矩阵：Redis 不可用时登录仍成功，仅在线用户列表缺失该会话。
    /// </summary>
    private async Task RecordOnlineSessionSafeAsync(
        User user,
        string accessToken,
        string ipAddress,
        string userAgent,
        CancellationToken ct)
    {
        try
        {
            var sessionId = ExtractSessionIdFromToken(accessToken);
            if (string.IsNullOrEmpty(sessionId))
            {
                _logger.LogWarning("无法从访问令牌解析 sessionId，跳过在线会话写入，UserId={UserId}", user.Id);
                return;
            }

            var browser = SafeParseBrowser(userAgent);
            var os = SafeParseOs(userAgent);
            var deviceFingerprint = SafeParseDeviceFingerprint(userAgent);

            var session = new OnlineUserSession
            {
                SessionId = sessionId,
                UserId = user.Id,
                Username = user.Username,
                Roles = ExtractRolesFromClaims(),
                IpAddress = ipAddress,
                GeoLocation = null,
                Browser = browser,
                Os = os,
                TokenPreview = accessToken.Length >= 8 ? accessToken[..8] : accessToken,
                DeviceFingerprint = deviceFingerprint,
                RequestCount = 0,
                LoginAt = DateTime.UtcNow,
                LastActivityAt = DateTime.UtcNow,
                IsAnomaly = false
            };

            await _userSessionStore.RecordAsync(session, ct).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex,
                "写入在线会话至 Redis 失败，登录仍成功，UserId={UserId}, IpAddress={IpAddress}",
                user.Id, ipAddress);
        }
    }

    /// <summary>
    /// 发布 UserLoggedInEvent（成功/失败均发布），供 SystemAdmin.LoginLogConsumer 消费写入 LoginLog。
    /// </summary>
    private async Task PublishLoginEventAsync(
        Guid eventId,
        string username,
        Guid? userId,
        string ipAddress,
        string userAgent,
        bool success,
        string? failureReason,
        int durationMs,
        CancellationToken ct)
    {
        try
        {
            var evt = new UserLoggedInEvent
            {
                EventId = eventId,
                OccurredAt = DateTime.UtcNow,
                Username = username,
                UserId = userId,
                IpAddress = ipAddress,
                UserAgent = userAgent,
                RefererUrl = ExtractRefererUrl(),
                TraceId = ExtractTraceId(),
                DurationMs = durationMs,
                Success = success,
                FailureReason = failureReason
            };
            await _publishEndpoint.Publish(evt, ct).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "发布 UserLoggedInEvent 失败，UserId={UserId}, Username={Username}, Success={Success}",
                userId, username, success);
        }
    }

    /// <summary>
    /// 从 HttpContext 提取客户端真实 IP（优先 X-Forwarded-For 首段，回退 RemoteIpAddress）。
    /// </summary>
    private static string ExtractClientIpAddress(HttpContext? httpContext)
    {
        if (httpContext is null)
        {
            return string.Empty;
        }

        var forwarded = httpContext.Request.Headers["X-Forwarded-For"].FirstOrDefault();
        if (!string.IsNullOrWhiteSpace(forwarded))
        {
            var firstIp = forwarded.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
                .FirstOrDefault();
            if (!string.IsNullOrWhiteSpace(firstIp))
            {
                return firstIp;
            }
        }

        return httpContext.Connection.RemoteIpAddress?.ToString() ?? string.Empty;
    }

    /// <summary>
    /// 从 HttpContext 提取 User-Agent 字符串。
    /// </summary>
    private static string ExtractUserAgent(HttpContext? httpContext)
    {
        if (httpContext is null)
        {
            return string.Empty;
        }

        var ua = httpContext.Request.Headers.UserAgent.ToString();
        return string.IsNullOrWhiteSpace(ua) ? string.Empty : ua;
    }

    /// <summary>
    /// 从当前 HttpContext 提取 Referer URL（可空）。
    /// </summary>
    private string? ExtractRefererUrl()
    {
        var httpContext = _httpContextAccessor.HttpContext;
        if (httpContext is null)
        {
            return null;
        }

        var referer = httpContext.Request.Headers.Referer.ToString();
        return string.IsNullOrWhiteSpace(referer) ? null : referer;
    }

    /// <summary>
    /// 从当前活动链路提取 TraceId（可空）。
    /// </summary>
    private static string ExtractTraceId()
    {
        var activity = Activity.Current;
        return activity?.TraceId.ToString() ?? string.Empty;
    }

    /// <summary>
    /// 从 JWT access token 解析 jti claim 作为 sessionId。
    /// access token 格式为 header.payload.signature，payload 为 Base64URL 编码的 JSON。
    /// </summary>
    private static string ExtractSessionIdFromToken(string accessToken)
    {
        if (string.IsNullOrWhiteSpace(accessToken))
        {
            return string.Empty;
        }

        var parts = accessToken.Split('.', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length < 2)
        {
            return string.Empty;
        }

        try
        {
            var payload = parts[1];
            // Base64URL → Base64
            payload = payload.Replace('-', '+').Replace('_', '/');
            switch (payload.Length % 4)
            {
                case 2: payload += "=="; break;
                case 3: payload += "="; break;
            }

            var bytes = Convert.FromBase64String(payload);
            using var doc = System.Text.Json.JsonDocument.Parse(bytes);
            if (doc.RootElement.TryGetProperty("jti", out var jtiElement)
                && jtiElement.ValueKind == System.Text.Json.JsonValueKind.String)
            {
                return jtiElement.GetString() ?? string.Empty;
            }
        }
        catch
        {
            // 解析失败返回空字符串，调用方处理
        }

        return string.Empty;
    }

    /// <summary>
    /// 从当前 HttpContext 的 User claims 提取角色列表。
    /// </summary>
    private List<string> ExtractRolesFromClaims()
    {
        var httpContext = _httpContextAccessor.HttpContext;
        if (httpContext is null)
        {
            return [];
        }

        return httpContext.User.FindAll(ClaimTypes.Role)
            .Select(c => c.Value)
            .Where(v => !string.IsNullOrWhiteSpace(v))
            .Distinct(StringComparer.Ordinal)
            .ToList();
    }

    private string SafeParseBrowser(string userAgent)
    {
        if (string.IsNullOrWhiteSpace(userAgent))
        {
            return string.Empty;
        }

        try
        {
            return _uaParser.ParseBrowser(userAgent);
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "解析浏览器失败，UserAgent={UserAgent}", userAgent);
            return string.Empty;
        }
    }

    private string SafeParseOs(string userAgent)
    {
        if (string.IsNullOrWhiteSpace(userAgent))
        {
            return string.Empty;
        }

        try
        {
            return _uaParser.ParseOs(userAgent);
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "解析操作系统失败，UserAgent={UserAgent}", userAgent);
            return string.Empty;
        }
    }

    private string? SafeParseDeviceFingerprint(string userAgent)
    {
        if (string.IsNullOrWhiteSpace(userAgent))
        {
            return null;
        }

        try
        {
            return _uaParser.ParseDeviceFingerprint(userAgent);
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "解析设备指纹失败，UserAgent={UserAgent}", userAgent);
            return null;
        }
    }

    /// <summary>
    /// 按用户名或邮箱查找用户。
    /// 若标识包含 <c>@</c> 视为邮箱，直接按邮箱查询；否则先按用户名查询，未命中再按邮箱兜底，
    /// 兼容用户用邮箱登录但客户端未指定登录方式的场景。
    /// </summary>
    private async Task<User?> FindUserByIdentifierAsync(string identifier, CancellationToken ct)
    {
        if (identifier.Contains('@'))
        {
            return await _userRepository.GetByEmailAsync(identifier, ct).ConfigureAwait(false);
        }

        var user = await _userRepository.GetByUsernameAsync(identifier, ct).ConfigureAwait(false);
        if (user is not null)
        {
            return user;
        }

        return await _userRepository.GetByEmailAsync(identifier, ct).ConfigureAwait(false);
    }
}
