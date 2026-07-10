using FluentValidation;
using Leno.UserAuth.Application.Abstractions;
using Leno.UserAuth.Application.DTOs;
using Leno.UserAuth.Application.Exceptions;
using Leno.UserAuth.Domain.Aggregates;
using Leno.UserAuth.Domain.Exceptions;
using Leno.UserAuth.Domain.Repositories;
using Leno.UserAuth.Domain.Services;
using Leno.UserAuth.Domain.ValueObjects;
using Leno.SharedKernel.Abstractions;

namespace Leno.UserAuth.Application.Services;

/// <summary>
/// 用户认证与个人资料应用服务实现。
/// 编排注册、登录、Token 刷新与资料维护，事务边界由工作单元统一控制。
/// </summary>
public sealed class UserAppService : IUserAppService
{
    private readonly IUserRepository _userRepository;
    private readonly IPasswordHasher _passwordHasher;
    private readonly IUserUniquenessChecker _uniquenessChecker;
    private readonly ITokenService _tokenService;
    private readonly IRefreshTokenStore _refreshTokenStore;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IValidator<RegisterDto> _registerValidator;
    private readonly IValidator<LoginDto> _loginValidator;
    private readonly IValidator<UpdateProfileDto> _updateProfileValidator;
    private readonly IValidator<ChangePasswordDto> _changePasswordValidator;

    public UserAppService(
        IUserRepository userRepository,
        IPasswordHasher passwordHasher,
        IUserUniquenessChecker uniquenessChecker,
        ITokenService tokenService,
        IRefreshTokenStore refreshTokenStore,
        IUnitOfWork unitOfWork,
        IValidator<RegisterDto> registerValidator,
        IValidator<LoginDto> loginValidator,
        IValidator<UpdateProfileDto> updateProfileValidator,
        IValidator<ChangePasswordDto> changePasswordValidator)
    {
        _userRepository = userRepository;
        _passwordHasher = passwordHasher;
        _uniquenessChecker = uniquenessChecker;
        _tokenService = tokenService;
        _refreshTokenStore = refreshTokenStore;
        _unitOfWork = unitOfWork;
        _registerValidator = registerValidator;
        _loginValidator = loginValidator;
        _updateProfileValidator = updateProfileValidator;
        _changePasswordValidator = changePasswordValidator;
    }

    /// <inheritdoc />
    public async Task<TokenDto> RegisterAsync(RegisterDto dto, CancellationToken ct = default)
    {
        await ValidateAsync(_registerValidator, dto, ct);

        if (!await _uniquenessChecker.IsUsernameUniqueAsync(dto.Username, null, ct))
        {
            throw new UserAuthDomainException("用户名已被注册", "USER_USERNAME_EXISTS", 409);
        }

        if (!string.IsNullOrWhiteSpace(dto.Email)
            && !await _uniquenessChecker.IsEmailUniqueAsync(dto.Email, null, ct))
        {
            throw new UserAuthDomainException("邮箱已被注册", "USER_EMAIL_EXISTS", 409);
        }

        if (!string.IsNullOrWhiteSpace(dto.PhoneNumber)
            && !await _uniquenessChecker.IsPhoneUniqueAsync(dto.PhoneNumber, null, ct))
        {
            throw new UserAuthDomainException("手机号已被注册", "USER_PHONE_EXISTS", 409);
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

        // 账号不存在统一返回账号或密码错误，防账号枚举（INV-18）
        if (user is null)
        {
            throw new UnauthorizedAccessException("账号或密码错误");
        }

        if (user.Status == AccountStatus.Disabled)
        {
            throw new UserAuthDomainException("账户已被禁用，请联系管理员", "USER_DISABLED", 403);
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
                $"账户已锁定，请于 {user.LockedUntil:O} 后重试", "USER_LOCKED", 403);
        }

        var passwordOk = user.VerifyPassword(dto.Password, _passwordHasher);

        if (!passwordOk)
        {
            await _userRepository.UpdateAsync(user, ct);
            await _unitOfWork.SaveEntitiesAsync(ct);
            throw new UnauthorizedAccessException("账号或密码错误");
        }

        user.RecordLogin();
        await _userRepository.UpdateAsync(user, ct);
        await _unitOfWork.SaveEntitiesAsync(ct);

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
            throw new UnauthorizedAccessException("账户不可用");
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
            throw new UserAuthDomainException("用户不存在", "USER_NOT_FOUND", 404);
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
