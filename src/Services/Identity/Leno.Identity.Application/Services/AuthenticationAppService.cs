using Leno.Identity.Application.DTOs;
using Leno.Identity.Domain.Aggregates;
using Leno.Identity.Domain.Events;
using Leno.Identity.Domain.Exceptions;
using Leno.Identity.Domain.Repositories;
using Leno.Identity.Domain.Services;
using Leno.SharedKernel.Abstractions;
using Microsoft.Extensions.Logging;

namespace Leno.Identity.Application.Services;

/// <summary>
/// 认证应用服务实现，编排登录、刷新与登出用例（Identity BC，3.6 AuthN/AuthZ 拆分）。
/// <para>
/// 编排流：
/// <list type="bullet">
/// <item><b>LoginAsync</b>：查找用户 → 校验账户状态 → 验证密码 → 重置失败计数 →
/// 签发刷新令牌聚合 → 发布 <see cref="UserAuthenticatedEvent"/> → 提交工作单元 → 生成访问令牌。</item>
/// <item><b>RefreshAsync</b>：校验刷新令牌有效 → 轮换（旧令牌 Rotate，新令牌签发）→ 提交 → 生成新访问令牌。</item>
/// <item><b>LogoutAsync</b>：吊销用户所有活跃刷新令牌 → 提交。</item>
/// </list>
/// </para>
/// <para>
/// 角色填充不在本类直接处理，由 <see cref="JwtTokenService.GenerateAccessToken"/> 调用
/// AccessControl BC <c>GetUserRoles</c> RPC 完成。
/// </para>
/// </summary>
public sealed class AuthenticationAppService : IAuthenticationAppService
{
    private const string AuthMethodPassword = "Password";
    private const string AuthMethodRefreshToken = "RefreshToken";
    private const string RevokeReasonLogout = "Logout";
    private const string RevokeReasonRotated = "Rotated";

    private readonly IUserRepository _userRepository;
    private readonly IRefreshTokenRepository _refreshTokenRepository;
    private readonly IPasswordHasher _passwordHasher;
    private readonly IUnitOfWork _unitOfWork;
    private readonly JwtTokenService _jwtTokenService;
    private readonly ILogger<AuthenticationAppService> _logger;

    public AuthenticationAppService(
        IUserRepository userRepository,
        IRefreshTokenRepository refreshTokenRepository,
        IPasswordHasher passwordHasher,
        IUnitOfWork unitOfWork,
        JwtTokenService jwtTokenService,
        ILogger<AuthenticationAppService> logger)
    {
        _userRepository = userRepository ?? throw new ArgumentNullException(nameof(userRepository));
        _refreshTokenRepository = refreshTokenRepository ?? throw new ArgumentNullException(nameof(refreshTokenRepository));
        _passwordHasher = passwordHasher ?? throw new ArgumentNullException(nameof(passwordHasher));
        _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
        _jwtTokenService = jwtTokenService ?? throw new ArgumentNullException(nameof(jwtTokenService));
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

        var identifier = dto.UsernameOrEmail.Trim();
        var user = await FindUserByIdentifierAsync(identifier, ct).ConfigureAwait(false);
        if (user is null)
        {
            // 不暴露"用户不存在"以防枚举攻击，统一返回凭证无效
            _logger.LogWarning("登录失败：用户标识未找到，Identifier={Identifier}", identifier);
            throw new IdentityDomainException("用户名或密码错误", "AUTH_INVALID_CREDENTIALS");
        }

        if (!user.CanLogin())
        {
            _logger.LogWarning("登录被拒：账户不可登录，UserId={UserId}, Status={Status}",
                user.Id, user.Status);
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

            throw new IdentityDomainException("用户名或密码错误", "AUTH_INVALID_CREDENTIALS");
        }

        // 登录成功：重置失败计数，发布领域事件
        user.RecordLogin(AuthMethodPassword);

        await _userRepository.UpdateAsync(user, ct).ConfigureAwait(false);

        // 签发刷新令牌
        var refreshToken = _jwtTokenService.GenerateRefreshToken(user.Id);
        await _refreshTokenRepository.AddAsync(refreshToken, ct).ConfigureAwait(false);

        // 同一事务提交聚合变更与领域事件（经 Outbox 持久化）
        await _unitOfWork.SaveEntitiesAsync(ct).ConfigureAwait(false);

        // 生成访问令牌（此时聚合变更已提交，调用 gRPC 获取角色）
        var accessToken = await _jwtTokenService.GenerateAccessToken(user, ct).ConfigureAwait(false);

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
