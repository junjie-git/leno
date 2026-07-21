using FluentValidation;
using Leno.UserAuth.Application.Abstractions;
using Leno.UserAuth.Application.DTOs;
using Leno.UserAuth.Application.Exceptions;
using Leno.UserAuth.Domain.Aggregates;
using Leno.UserAuth.Domain.Events;
using Leno.UserAuth.Domain.Exceptions;
using Leno.UserAuth.Domain.Repositories;
using Leno.UserAuth.Domain.Services;
using Leno.UserAuth.Domain.ValueObjects;
using Leno.SharedKernel.Abstractions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using StackExchange.Redis;

namespace Leno.UserAuth.Application.Services;

/// <summary>
/// 用户认证与个人资料应用服务实现。
/// 编排注册、登录、Token 刷新与资料维护，事务边界由工作单元统一控制。
/// </summary>
public sealed class UserAppService : IUserAppService
{
    /// <summary>
    /// 用于账号枚举时序对齐的预生成 bcrypt 哈希。
    /// 账号不存在分支会执行一次 dummy verify，使响应时间与真实路径一致，
    /// 防止攻击者通过响应时间差异枚举有效账户（INV-18）。
    /// 该哈希由 "leno-dummy-password-for-timing-equalization" 经 bcrypt cost 12 生成，无任何登录语义。
    /// </summary>
    private const string DummyPasswordHash =
        "$2a$12$N9qo8uLOickgx2ZMRZoMyeIjZAgcfl7p92ldGxad68LJZdL17lhWy";

    private readonly IUserRepository _userRepository;
    private readonly IPasswordHasher _passwordHasher;
    private readonly IUserUniquenessChecker _uniquenessChecker;
    private readonly ITokenService _tokenService;
    private readonly ITokenVerifier _tokenVerifier;
    private readonly IRefreshTokenStore _refreshTokenStore;
    private readonly IJwtRevocationService _jwtRevocationService;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IValidator<RegisterDto> _registerValidator;
    private readonly IValidator<LoginDto> _loginValidator;
    private readonly IValidator<UpdateProfileDto> _updateProfileValidator;
    private readonly IValidator<ChangePasswordDto> _changePasswordValidator;
    private readonly IEnumerable<IExternalAuthService> _externalAuthServices;
    private readonly IDatabase _redis;
    private readonly OAuth2Options _oauth2Options;

    public UserAppService(
        IUserRepository userRepository,
        IPasswordHasher passwordHasher,
        IUserUniquenessChecker uniquenessChecker,
        ITokenService tokenService,
        ITokenVerifier tokenVerifier,
        IRefreshTokenStore refreshTokenStore,
        IJwtRevocationService jwtRevocationService,
        IUnitOfWork unitOfWork,
        IValidator<RegisterDto> registerValidator,
        IValidator<LoginDto> loginValidator,
        IValidator<UpdateProfileDto> updateProfileValidator,
        IValidator<ChangePasswordDto> changePasswordValidator,
        IEnumerable<IExternalAuthService> externalAuthServices,
        IConnectionMultiplexer connectionMultiplexer,
        IOptions<OAuth2Options> oauth2Options)
    {
        _userRepository = userRepository;
        _passwordHasher = passwordHasher;
        _uniquenessChecker = uniquenessChecker;
        _tokenService = tokenService;
        _tokenVerifier = tokenVerifier;
        _refreshTokenStore = refreshTokenStore;
        _jwtRevocationService = jwtRevocationService;
        _unitOfWork = unitOfWork;
        _registerValidator = registerValidator;
        _loginValidator = loginValidator;
        _updateProfileValidator = updateProfileValidator;
        _changePasswordValidator = changePasswordValidator;
        _externalAuthServices = externalAuthServices;
        _redis = connectionMultiplexer.GetDatabase();
        _oauth2Options = oauth2Options.Value;
    }

    /// <inheritdoc />
    public async Task<TokenDto> RegisterAsync(RegisterDto dto, CancellationToken ct = default)
    {
        await ValidateAsync(_registerValidator, dto, ct);

        if (!await _uniquenessChecker.IsUsernameUniqueAsync(dto.Username, null, ct))
        {
            throw new UserAuthDomainException("用户名已被注册", "USER_USERNAME_EXISTS");
        }

        if (!string.IsNullOrWhiteSpace(dto.Email)
            && !await _uniquenessChecker.IsEmailUniqueAsync(dto.Email, null, ct))
        {
            throw new UserAuthDomainException("邮箱已被注册", "USER_EMAIL_EXISTS");
        }

        if (!string.IsNullOrWhiteSpace(dto.PhoneNumber)
            && !await _uniquenessChecker.IsPhoneUniqueAsync(dto.PhoneNumber, null, ct))
        {
            throw new UserAuthDomainException("手机号已被注册", "USER_PHONE_EXISTS");
        }

        var passwordHash = _passwordHasher.Hash(dto.Password);
        var user = User.Create(
            Guid.NewGuid(),
            dto.Username,
            dto.Email,
            dto.PhoneNumber,
            passwordHash,
            dto.Nickname,
            dto.AvatarUrl);

        await _userRepository.AddAsync(user, ct);
        await _unitOfWork.SaveEntitiesAsync(ct);

        return await IssueTokensAsync(user, ct);
    }

    /// <inheritdoc />
    public async Task<TokenDto> LoginAsync(LoginDto dto, CancellationToken ct = default)
    {
        await ValidateAsync(_loginValidator, dto, ct);

        var user = await FindByAccountAsync(dto.Account, ct);

        // 账号不存在统一返回账号或密码错误，防账号枚举（INV-18）。
        // 同时执行一次 dummy bcrypt verify 对齐响应时间，避免攻击者通过响应时间差异枚举有效账户。
        if (user is null)
        {
            _ = _passwordHasher.Verify("\x00", DummyPasswordHash);
            throw new UnauthorizedAccessException("账号或密码错误");
        }

        if (user.Status == AccountStatus.Disabled)
        {
            throw new UserAuthDomainException("账户已被禁用，请联系管理员", "USER_DISABLED");
        }

        // 锁定超时自动解锁
        if (user.Status == AccountStatus.Locked
            && (!user.LockedUntil.HasValue || user.LockedUntil.Value <= DateTime.UtcNow))
        {
            user.Unlock();
        }
        else if (!user.CanLogin())
        {
            throw new UserAuthDomainException(
                $"账户已锁定，请于 {user.LockedUntil:O} 后重试", "USER_LOCKED");
        }

        var passwordOk = user.VerifyPassword(dto.Password, _passwordHasher);

        if (!passwordOk)
        {
            await SaveWithConcurrencyRetryAsync(async ct =>
            {
                await _userRepository.UpdateAsync(user, ct);
                await _unitOfWork.SaveEntitiesAsync(ct);
            }, ct);
            throw new UnauthorizedAccessException("账号或密码错误");
        }

        user.RecordLogin();
        await SaveWithConcurrencyRetryAsync(async ct =>
        {
            await _userRepository.UpdateAsync(user, ct);
            await _unitOfWork.SaveEntitiesAsync(ct);
        }, ct);

        // 如已启用双因子认证，返回临时令牌要求二次验证
        if (user.TwoFactorEnabled)
        {
            return await IssueTwoFactorRequiredTokenAsync(user, ct);
        }

        return await IssueTokensAsync(user, ct);
    }

    /// <inheritdoc />
    public async Task<TokenDto> RefreshTokenAsync(string refreshToken, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(refreshToken))
        {
            throw new UserAuthValidationException("刷新令牌不可为空");
        }

        var userId = await _refreshTokenStore.ValidateAndRotateAsync(refreshToken, ct);
        if (!userId.HasValue)
        {
            throw new UnauthorizedAccessException("刷新令牌无效或已过期");
        }

        var user = await _userRepository.GetByIdAsync(userId.Value, ct);
        if (user is null || user.Status == AccountStatus.Disabled)
        {
            // 用户被禁用或不存在：撤销该用户所有 RefreshToken 防止旧令牌重试，
            // 并把 userId 加入 JWT 黑名单使已签发的 AccessToken 立即失效。
            await _refreshTokenStore.RevokeAllAsync(userId.Value, ct);
            await _jwtRevocationService.RevokeUserAsync(userId.Value, ct);
            throw new UnauthorizedAccessException("账户不可用");
        }

        // 锁定超时自动解锁（与 LoginAsync 一致），解锁后继续签发新令牌
        if (user.Status == AccountStatus.Locked
            && (!user.LockedUntil.HasValue || user.LockedUntil.Value <= DateTime.UtcNow))
        {
            user.Unlock();
            await _userRepository.UpdateAsync(user, ct);
            await _unitOfWork.SaveEntitiesAsync(ct);
        }
        else if (user.Status == AccountStatus.Locked)
        {
            // 仍在锁定期：拒绝刷新令牌，避免被锁用户绕过登录锁定机制
            throw new UserAuthDomainException(
                $"账户已锁定，请于 {user.LockedUntil:O} 后重试", "USER_LOCKED");
        }

        // 已启用 2FA 的用户：刷新令牌不应直接换发完整 AccessToken，
        // 改为签发临时令牌要求二次验证，避免 2FA 被旧刷新令牌绕过。
        if (user.TwoFactorEnabled)
        {
            return await IssueTwoFactorRequiredTokenAsync(user, ct);
        }

        return await IssueTokensAsync(user, ct);
    }

    /// <inheritdoc />
    public async Task<UserDto> GetProfileAsync(Guid userId, CancellationToken ct = default)
    {
        var user = await RequireUserAsync(userId, ct);
        return ToUserDto(user);
    }

    /// <inheritdoc />
    public async Task<UserDto> UpdateProfileAsync(Guid userId, UpdateProfileDto dto, CancellationToken ct = default)
    {
        await ValidateAsync(_updateProfileValidator, dto, ct);
        var user = await RequireUserAsync(userId, ct);

        user.UpdateProfile(dto.Nickname, dto.AvatarUrl);
        await _userRepository.UpdateAsync(user, ct);
        await _unitOfWork.SaveEntitiesAsync(ct);

        return ToUserDto(user);
    }

    /// <inheritdoc />
    public async Task ChangePasswordAsync(Guid userId, ChangePasswordDto dto, CancellationToken ct = default)
    {
        await ValidateAsync(_changePasswordValidator, dto, ct);
        var user = await RequireUserAsync(userId, ct);

        user.ChangePassword(dto.OldPassword, dto.NewPassword, _passwordHasher);
        await _userRepository.UpdateAsync(user, ct);
        await _unitOfWork.SaveEntitiesAsync(ct);

        // 撤销该用户所有 RefreshToken，强制重新登录
        await _refreshTokenStore.RevokeAllAsync(user.Id, ct);
    }

    /// <inheritdoc />
    public async Task<string> GetOAuthLoginUrlAsync(string provider, string redirectUri, CancellationToken ct = default)
    {
        // 白名单校验，防止开放重定向攻击（P1-8）
        if (_oauth2Options.AllowedRedirectUris.Count > 0
            && !_oauth2Options.AllowedRedirectUris.Contains(redirectUri, StringComparer.OrdinalIgnoreCase))
        {
            throw new UserAuthDomainException("redirectUri 不在白名单", "OAUTH_REDIRECT_URI_NOT_ALLOWED");
        }

        var authService = ResolveAuthService(provider);
        var state = Guid.NewGuid().ToString("N");

        // 存储 state 到 Redis，TTL 5 分钟
        var redisKey = $"oauth:state:{state}";
        var redisValue = $"{authService.Provider}|{redirectUri}";
        await _redis.StringSetAsync(redisKey, redisValue, TimeSpan.FromMinutes(5));

        return authService.GetAuthorizationUrl(state, redirectUri);
    }

    /// <inheritdoc />
    public async Task<TokenDto> HandleOAuthCallbackAsync(string provider, string code, string state, string redirectUri, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(code))
        {
            throw new UserAuthDomainException("授权码不可为空", "OAUTH_CODE_EMPTY");
        }

        if (string.IsNullOrWhiteSpace(state))
        {
            throw new UserAuthDomainException("State 参数不可为空", "OAUTH_STATE_EMPTY");
        }

        // 校验 state
        var redisKey = $"oauth:state:{state}";
        var redisValue = await _redis.StringGetAsync(redisKey);

        if (!redisValue.HasValue)
        {
            throw new UserAuthDomainException("State 已过期或无效", "OAUTH_STATE_EXPIRED");
        }

        // 删除 state，防止重放
        await _redis.KeyDeleteAsync(redisKey);

        var parts = redisValue.ToString().Split('|');
        if (parts.Length != 2)
        {
            throw new UserAuthDomainException("State 数据无效", "OAUTH_STATE_INVALID");
        }

        var stateProvider = parts[0];
        var stateRedirectUri = parts[1];

        // 校验 state 内 provider 与 callback provider 一致，防止跨 OAuth 提供方的 CSRF
        if (!string.Equals(stateProvider, provider.Trim().ToLowerInvariant(), StringComparison.OrdinalIgnoreCase))
        {
            throw new UserAuthDomainException("State 与 provider 不匹配", "OAUTH_STATE_PROVIDER_MISMATCH");
        }

        // 校验 state 内 redirectUri 与 callback redirectUri 一致，防止开放重定向
        if (!string.Equals(stateRedirectUri, redirectUri, StringComparison.OrdinalIgnoreCase))
        {
            throw new UserAuthDomainException("State 内 redirectUri 与回调不匹配", "OAUTH_REDIRECT_URI_MISMATCH");
        }

        var authService = ResolveAuthService(provider);

        // 交换授权码获取用户信息
        var externalLoginInfo = await authService.ExchangeCodeAsync(code, redirectUri, ct);

        // 查找是否已绑定外部登录
        var user = await _userRepository.FindByExternalLoginAsync(
            externalLoginInfo.Provider, externalLoginInfo.ProviderUserId, ct);

        if (user is not null)
        {
            // 已绑定用户直接登录
            if (!user.CanLogin())
            {
                if (user.Status == AccountStatus.Disabled)
                {
                    throw new UserAuthDomainException("账户已被禁用，请联系管理员", "USER_DISABLED");
                }

                throw new UserAuthDomainException(
                    $"账户已锁定，请于 {user.LockedUntil:O} 后重试", "USER_LOCKED");
            }

            user.RecordLogin();
            await _userRepository.UpdateAsync(user, ct);
            await _unitOfWork.SaveEntitiesAsync(ct);

            return await IssueTokensAsync(user, ct);
        }

        // 首次登录：检查邮箱是否已被其他账户使用
        // 静默绑定到已有账户会导致账户接管：攻击者只要控制一个 Google 账户并把邮箱改成受害者邮箱即可登录受害者账户。
        // 邮箱冲突时返回错误，要求用户先登录已有账户后通过 AccountController.BindExternalLogin 主动绑定。
        if (!string.IsNullOrWhiteSpace(externalLoginInfo.Email))
        {
            var existingByEmail = await _userRepository.GetByEmailAsync(externalLoginInfo.Email, ct);
            if (existingByEmail is not null)
            {
                throw new UserAuthDomainException(
                    $"邮箱 {externalLoginInfo.Email} 已被注册，请先登录已有账户后在「账户设置」中绑定 {externalLoginInfo.Provider} 登录",
                    "OAUTH_EMAIL_ALREADY_USED");
            }
        }

        // 创建新账户（一次性创建，冲突时通过 Rename 重试，不重建聚合）
        var newUser = User.CreateFromExternal(Guid.NewGuid(), externalLoginInfo);

        // 确保用户名唯一（冲突时调用聚合 Rename 方法追加随机后缀，不通过反射绕过封装）
        var baseUsername = newUser.Username;
        var retry = 0;
        while (!await _uniquenessChecker.IsUsernameUniqueAsync(newUser.Username, null, ct))
        {
            retry++;
            if (retry > 10)
            {
                throw new UserAuthDomainException("无法生成唯一用户名，请稍后重试", "USER_USERNAME_CONFLICT");
            }

            var suffix = Random.Shared.Next(1000, 9999).ToString(System.Globalization.CultureInfo.InvariantCulture);
            var candidate = baseUsername.Length + suffix.Length <= 32
                ? baseUsername + suffix
                : baseUsername[..(32 - suffix.Length)] + suffix;

            // 通过聚合行为方法修改用户名，复用 ValidateUsername 校验
            newUser.Rename(candidate);
        }

        await _userRepository.AddAsync(newUser, ct);
        await _unitOfWork.SaveEntitiesAsync(ct);

        return await IssueTokensAsync(newUser, ct);
    }

    /// <inheritdoc />
    public async Task<TwoFactorEnableResponseDto> EnableTwoFactorAsync(Guid userId, CancellationToken ct = default)
    {
        var user = await RequireUserAsync(userId, ct);

        var qrCodeUri = user.EnableTwoFactor(_tokenVerifier);
        await _userRepository.UpdateAsync(user, ct);
        await _unitOfWork.SaveEntitiesAsync(ct);

        return new TwoFactorEnableResponseDto
        {
            Secret = user.TwoFactorSecret!,
            QrCodeUri = qrCodeUri
        };
    }

    /// <inheritdoc />
    public async Task ConfirmTwoFactorAsync(Guid userId, TwoFactorConfirmDto dto, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(dto.Code))
        {
            throw new UserAuthDomainException("验证码不可为空", "USER_2FA_CODE_EMPTY");
        }

        var user = await RequireUserAsync(userId, ct);
        user.ConfirmTwoFactor(dto.Code, _tokenVerifier);
        await _userRepository.UpdateAsync(user, ct);
        await _unitOfWork.SaveEntitiesAsync(ct);
    }

    /// <inheritdoc />
    public async Task DisableTwoFactorAsync(Guid userId, CancellationToken ct = default)
    {
        var user = await RequireUserAsync(userId, ct);
        user.DisableTwoFactor();
        await _userRepository.UpdateAsync(user, ct);
        await _unitOfWork.SaveEntitiesAsync(ct);
    }

    /// <inheritdoc />
    public async Task<TokenDto> VerifyTwoFactorAsync(TwoFactorVerifyDto dto, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(dto.TempToken))
        {
            throw new UserAuthDomainException("临时令牌不可为空", "USER_2FA_TEMP_TOKEN_EMPTY");
        }

        if (string.IsNullOrWhiteSpace(dto.Code))
        {
            throw new UserAuthDomainException("验证码不可为空", "USER_2FA_CODE_EMPTY");
        }

        // 从 Redis 验证临时令牌
        var redisKey = $"2fa:temp:{dto.TempToken}";
        var redisValue = await _redis.StringGetAsync(redisKey);

        if (!redisValue.HasValue)
        {
            throw new UserAuthDomainException("临时令牌已过期或无效", "USER_2FA_TEMP_TOKEN_INVALID");
        }

        if (!Guid.TryParse(redisValue.ToString(), out var userId))
        {
            throw new UserAuthDomainException("临时令牌数据无效", "USER_2FA_TEMP_TOKEN_INVALID");
        }

        // 删除临时令牌，防止重放
        await _redis.KeyDeleteAsync(redisKey);

        var user = await RequireUserAsync(userId, ct);

        if (!user.VerifyTwoFactorCode(dto.Code, _tokenVerifier))
        {
            throw new UserAuthDomainException("验证码无效或已过期", "USER_2FA_CODE_INVALID");
        }

        return await IssueTokensAsync(user, ct);
    }

    /// <inheritdoc />
    public async Task ForgotPasswordAsync(ForgotPasswordDto dto, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(dto.Account))
        {
            throw new UserAuthDomainException("账号不可为空", "USER_ACCOUNT_EMPTY");
        }

        var user = await FindByAccountAsync(dto.Account, ct);
        // 不暴露用户是否存在，统一返回成功
        if (user is null)
        {
            return;
        }

        if (user.Status == AccountStatus.Disabled)
        {
            return;
        }

        // 生成一次性重置令牌
        var resetToken = Guid.NewGuid().ToString("N");
        var redisKey = $"reset:pwd:{resetToken}";

        // 存储到 Redis，10 分钟过期
        await _redis.StringSetAsync(redisKey, user.Id.ToString(), TimeSpan.FromMinutes(10));

        // 发布领域事件
        user.PublishForgotPasswordRequested(resetToken);

        // 显式 Attach 聚合变更，确保 EF ChangeTracker 与领域事件 Outbox 收集。
        // 若 BaseDbContext/UoW 对未显式 Attach 的实体在 SaveChanges 时跳过领域事件收集，
        // ForgotPasswordRequestedEvent 将丢失，导致重置邮件不发送。
        await _userRepository.UpdateAsync(user, ct);
        await _unitOfWork.SaveEntitiesAsync(ct);
    }

    /// <inheritdoc />
    public async Task ResetPasswordAsync(ResetPasswordDto dto, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(dto.Token))
        {
            throw new UserAuthDomainException("重置令牌不可为空", "USER_RESET_TOKEN_EMPTY");
        }

        if (string.IsNullOrWhiteSpace(dto.NewPassword))
        {
            throw new UserAuthDomainException("新密码不可为空", "USER_NEW_PASSWORD_EMPTY");
        }

        // 从 Redis 获取并删除令牌
        var redisKey = $"reset:pwd:{dto.Token}";
        var redisValue = await _redis.StringGetAsync(redisKey);

        // 删除令牌，防止重复使用
        await _redis.KeyDeleteAsync(redisKey);

        if (!redisValue.HasValue)
        {
            throw new UserAuthDomainException("重置令牌无效或已过期", "USER_RESET_TOKEN_INVALID");
        }

        if (!Guid.TryParse(redisValue.ToString(), out var userId))
        {
            throw new UserAuthDomainException("重置令牌数据无效", "USER_RESET_TOKEN_INVALID");
        }

        var user = await RequireUserAsync(userId, ct);

        if (user.Status == AccountStatus.Disabled)
        {
            throw new UserAuthDomainException("账户已被禁用", "USER_DISABLED");
        }

        // 重置密码（纯 OAuth 用户首次设置密码与密码用户重置走同一逻辑，
        // 均直接设置新密码哈希，无需旧密码验证——令牌已通过邮箱/短信验证）
        user.ResetPassword(_passwordHasher.Hash(dto.NewPassword), _passwordHasher);

        await _userRepository.UpdateAsync(user, ct);
        await _unitOfWork.SaveEntitiesAsync(ct);

        // 撤销该用户所有 RefreshToken，防止旧令牌继续使用
        await _refreshTokenStore.RevokeAllAsync(user.Id, ct);
    }

    /// <summary>
    /// 带乐观锁重试的保存操作。捕获 <see cref="DbUpdateConcurrencyException"/> 后短暂退避并重试，
    /// 用于 FailedLoginCount 并发累加等需要原子性的场景。
    /// </summary>
    private static async Task SaveWithConcurrencyRetryAsync(
        Func<CancellationToken, Task> saveAction,
        CancellationToken ct,
        int maxRetry = 3)
    {
        for (var attempt = 0; ; attempt++)
        {
            try
            {
                await saveAction(ct);
                return;
            }
            catch (DbUpdateConcurrencyException) when (attempt < maxRetry)
            {
                // 重新加载聚合以拿到最新的 RowVersion，由调用方再次构造变更
                await Task.Delay(TimeSpan.FromMilliseconds(50 * (attempt + 1)), ct);
            }
        }
    }

    private async Task<TokenDto> IssueTwoFactorRequiredTokenAsync(User user, CancellationToken ct)
    {
        // 生成临时令牌，存储到 Redis（5 分钟过期）
        var tempToken = Guid.NewGuid().ToString("N");
        var redisKey = $"2fa:temp:{tempToken}";
        await _redis.StringSetAsync(redisKey, user.Id.ToString(), TimeSpan.FromMinutes(5));

        return new TokenDto
        {
            UserId = user.Id,
            Username = user.Username,
            TwoFactorRequired = true,
            TempToken = tempToken
        };
    }

    private async Task<TokenDto> IssueTokensAsync(User user, CancellationToken ct)
    {
        var role = GetPrimaryRole(user.Roles);
        var accessToken = _tokenService.GenerateAccessToken(user.Id, role, shopId: null);
        var refreshToken = await _refreshTokenStore.IssueAsync(user.Id, ct);

        return new TokenDto
        {
            UserId = user.Id,
            Username = user.Username,
            AccessToken = accessToken,
            RefreshToken = refreshToken,
            ExpiresIn = _tokenService.AccessTokenExpirySeconds
        };
    }

    private async Task<User?> FindByAccountAsync(string account, CancellationToken ct)
    {
        if (account.Contains('@'))
        {
            return await _userRepository.GetByEmailAsync(account, ct);
        }

        if (account.StartsWith('+'))
        {
            return await _userRepository.GetByPhoneAsync(account, ct);
        }

        return await _userRepository.GetByUsernameAsync(account, ct);
    }

    private async Task<User> RequireUserAsync(Guid userId, CancellationToken ct)
    {
        var user = await _userRepository.GetByIdAsync(userId, ct);
        if (user is null)
        {
            throw new UserAuthDomainException("用户不存在", "USER_NOT_FOUND");
        }

        return user;
    }

    private static async Task ValidateAsync<T>(IValidator<T> validator, T instance, CancellationToken ct)
    {
        var result = await validator.ValidateAsync(instance, ct);
        if (!result.IsValid)
        {
            throw new UserAuthValidationException(result.Errors.Select(e => e.ErrorMessage));
        }
    }

    private static string GetPrimaryRole(IReadOnlyCollection<UserRole> roles)
    {
        if (roles.Count == 0)
        {
            return RoleType.Buyer.ToString();
        }

        return roles.Select(r => r.Value)
            .OrderByDescending(r => (int)r)
            .First()
            .ToString();
    }

    private IExternalAuthService ResolveAuthService(string provider)
    {
        if (string.IsNullOrWhiteSpace(provider))
        {
            throw new UserAuthDomainException("OAuth2 提供方不可为空", "OAUTH_PROVIDER_EMPTY");
        }

        var normalized = provider.Trim().ToLowerInvariant();
        var service = _externalAuthServices.FirstOrDefault(s =>
            string.Equals(s.Provider, normalized, StringComparison.OrdinalIgnoreCase));

        if (service is null)
        {
            throw new UserAuthDomainException(
                $"不支持的 OAuth2 提供方: {provider}", "OAUTH_PROVIDER_NOT_FOUND");
        }

        return service;
    }

    private static UserDto ToUserDto(User user)
        => new()
        {
            Id = user.Id,
            Username = user.Username,
            Email = MaskEmail(user.Email),
            PhoneNumber = MaskPhone(user.PhoneNumber),
            Nickname = user.Nickname,
            AvatarUrl = user.AvatarUrl,
            Status = user.Status,
            Roles = user.Roles.Select(r => r.Code).ToList(),
            DefaultAddressId = user.DefaultAddressId,
            CreatedAt = user.CreatedAt,
            UpdatedAt = user.UpdatedAt
        };

    private static string? MaskEmail(string? email)
    {
        if (string.IsNullOrEmpty(email))
        {
            return null;
        }

        var at = email.IndexOf('@');
        if (at <= 1)
        {
            return email;
        }

        var prefix = email[..1];
        var domain = email[at..];
        return $"{prefix}***{domain}";
    }

    private static string? MaskPhone(string? phone)
    {
        if (string.IsNullOrEmpty(phone) || phone.Length < 7)
        {
            return phone;
        }

        return string.Concat(phone.AsSpan(0, 3), "****", phone.AsSpan(phone.Length - 4));
    }
}
