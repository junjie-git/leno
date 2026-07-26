using Leno.Identity.Application.DTOs;
using Leno.Identity.Domain.Aggregates;
using Leno.Identity.Domain.Exceptions;
using Leno.Identity.Domain.Repositories;
using Leno.Identity.Domain.Services;
using Leno.SharedKernel.Abstractions;
using Microsoft.Extensions.Logging;

namespace Leno.Identity.Application.Services;

/// <summary>
/// 认证应用服务实现（Identity BC，Task A2 补齐）。
/// <para>
/// RegisterAsync 为本服务核心实现：注册账户 → 唯一性校验 → 哈希密码 → 签发刷新令牌 → 提交工作单元 → 生成访问令牌。
/// LoginAsync / RefreshTokenAsync / LogoutAsync 委托 <see cref="AuthenticationAppService"/> 既有实现，
/// 复用已验证的登录、刷新与登出编排逻辑。
/// </para>
/// </summary>
public sealed class AuthAppService : IAuthAppService
{
    private readonly IUserRepository _userRepository;
    private readonly IPasswordHasher _passwordHasher;
    private readonly IUserUniquenessChecker _uniquenessChecker;
    private readonly IRefreshTokenRepository _refreshTokenRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly JwtTokenService _jwtTokenService;
    private readonly AuthenticationAppService _authenticationAppService;
    private readonly ILogger<AuthAppService> _logger;

    public AuthAppService(
        IUserRepository userRepository,
        IPasswordHasher passwordHasher,
        IUserUniquenessChecker uniquenessChecker,
        IRefreshTokenRepository refreshTokenRepository,
        IUnitOfWork unitOfWork,
        JwtTokenService jwtTokenService,
        AuthenticationAppService authenticationAppService,
        ILogger<AuthAppService> logger)
    {
        _userRepository = userRepository ?? throw new ArgumentNullException(nameof(userRepository));
        _passwordHasher = passwordHasher ?? throw new ArgumentNullException(nameof(passwordHasher));
        _uniquenessChecker = uniquenessChecker ?? throw new ArgumentNullException(nameof(uniquenessChecker));
        _refreshTokenRepository = refreshTokenRepository ?? throw new ArgumentNullException(nameof(refreshTokenRepository));
        _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
        _jwtTokenService = jwtTokenService ?? throw new ArgumentNullException(nameof(jwtTokenService));
        _authenticationAppService = authenticationAppService ?? throw new ArgumentNullException(nameof(authenticationAppService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <inheritdoc />
    public async Task<TokenDto> RegisterAsync(RegisterDto request, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (string.IsNullOrWhiteSpace(request.Username))
        {
            throw new IdentityDomainException("用户名不可为空", "USER_USERNAME_EMPTY");
        }

        if (string.IsNullOrWhiteSpace(request.Password))
        {
            throw new IdentityDomainException("密码不可为空", "USER_PASSWORD_EMPTY");
        }

        if (string.IsNullOrWhiteSpace(request.Nickname))
        {
            throw new IdentityDomainException("昵称不可为空", "USER_NICKNAME_EMPTY");
        }

        if (!await _uniquenessChecker.IsUsernameUniqueAsync(request.Username, null, ct).ConfigureAwait(false))
        {
            throw new IdentityDomainException("用户名已被注册", "USER_USERNAME_EXISTS");
        }

        if (!string.IsNullOrWhiteSpace(request.Email)
            && !await _uniquenessChecker.IsEmailUniqueAsync(request.Email, null, ct).ConfigureAwait(false))
        {
            throw new IdentityDomainException("邮箱已被注册", "USER_EMAIL_EXISTS");
        }

        if (!string.IsNullOrWhiteSpace(request.PhoneNumber)
            && !await _uniquenessChecker.IsPhoneUniqueAsync(request.PhoneNumber, null, ct).ConfigureAwait(false))
        {
            throw new IdentityDomainException("手机号已被注册", "USER_PHONE_EXISTS");
        }

        var passwordHash = _passwordHasher.Hash(request.Password);
        var user = User.Create(
            Guid.NewGuid(),
            request.Username,
            request.Email,
            request.PhoneNumber,
            passwordHash,
            request.Nickname,
            request.AvatarUrl);

        await _userRepository.AddAsync(user, ct).ConfigureAwait(false);

        var refreshToken = _jwtTokenService.GenerateRefreshToken(user.Id);
        await _refreshTokenRepository.AddAsync(refreshToken, ct).ConfigureAwait(false);

        await _unitOfWork.SaveEntitiesAsync(ct).ConfigureAwait(false);

        var accessToken = await _jwtTokenService.GenerateAccessToken(user, ct).ConfigureAwait(false);

        _logger.LogInformation("用户注册成功，UserId={UserId}, Username={Username}",
            user.Id, user.Username);

        return new TokenDto
        {
            AccessToken = accessToken,
            RefreshToken = refreshToken.Token,
            ExpiresAt = _jwtTokenService.AccessTokenExpiresAt
        };
    }

    /// <inheritdoc />
    public Task<TokenDto> LoginAsync(LoginDto request, CancellationToken ct = default)
    {
        return _authenticationAppService.LoginAsync(request, ct);
    }

    /// <inheritdoc />
    public Task<TokenDto> RefreshTokenAsync(RefreshTokenDto request, CancellationToken ct = default)
    {
        return _authenticationAppService.RefreshAsync(request, ct);
    }

    /// <inheritdoc />
    public Task LogoutAsync(Guid userId, CancellationToken ct = default)
    {
        return _authenticationAppService.LogoutAsync(userId, ct);
    }
}
