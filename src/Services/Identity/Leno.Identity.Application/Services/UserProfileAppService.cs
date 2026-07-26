using Leno.Identity.Application.DTOs;
using Leno.Identity.Domain.Aggregates;
using Leno.Identity.Domain.Exceptions;
using Leno.Identity.Domain.Repositories;
using Leno.Identity.Domain.Services;
using Leno.SharedKernel.Abstractions;
using Microsoft.Extensions.Logging;

namespace Leno.Identity.Application.Services;

/// <summary>
/// 用户资料应用服务实现（Identity BC，Task A2 补齐）。
/// 承载查询资料、修改资料与修改密码用例。
/// 修改密码成功后撤销该用户所有刷新令牌，强制重新登录。
/// </summary>
public sealed class UserProfileAppService : IUserProfileAppService
{
    /// <summary>撤销原因：密码变更。</summary>
    private const string RevokeReasonPasswordChange = "PasswordChange";

    private readonly IUserRepository _userRepository;
    private readonly IPasswordHasher _passwordHasher;
    private readonly IRefreshTokenRepository _refreshTokenRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<UserProfileAppService> _logger;

    public UserProfileAppService(
        IUserRepository userRepository,
        IPasswordHasher passwordHasher,
        IRefreshTokenRepository refreshTokenRepository,
        IUnitOfWork unitOfWork,
        ILogger<UserProfileAppService> logger)
    {
        _userRepository = userRepository ?? throw new ArgumentNullException(nameof(userRepository));
        _passwordHasher = passwordHasher ?? throw new ArgumentNullException(nameof(passwordHasher));
        _refreshTokenRepository = refreshTokenRepository ?? throw new ArgumentNullException(nameof(refreshTokenRepository));
        _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <inheritdoc />
    public async Task<UserDto> GetProfileAsync(Guid userId, CancellationToken ct = default)
    {
        var user = await RequireUserAsync(userId, ct).ConfigureAwait(false);
        return ToUserDto(user);
    }

    /// <inheritdoc />
    public async Task<UserDto> UpdateProfileAsync(Guid userId, UpdateProfileDto request, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (string.IsNullOrWhiteSpace(request.Nickname))
        {
            throw new IdentityDomainException("昵称不可为空", "USER_NICKNAME_EMPTY");
        }

        var user = await RequireUserAsync(userId, ct).ConfigureAwait(false);

        user.UpdateProfile(request.Nickname, request.AvatarUrl);
        await _userRepository.UpdateAsync(user, ct).ConfigureAwait(false);
        await _unitOfWork.SaveEntitiesAsync(ct).ConfigureAwait(false);

        _logger.LogInformation("用户资料更新成功，UserId={UserId}", userId);

        return ToUserDto(user);
    }

    /// <inheritdoc />
    public async Task ChangePasswordAsync(Guid userId, ChangePasswordDto request, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (string.IsNullOrWhiteSpace(request.OldPassword))
        {
            throw new IdentityDomainException("旧密码不可为空", "USER_OLD_PASSWORD_EMPTY");
        }

        if (string.IsNullOrWhiteSpace(request.NewPassword))
        {
            throw new IdentityDomainException("新密码不可为空", "USER_NEW_PASSWORD_EMPTY");
        }

        var user = await RequireUserAsync(userId, ct).ConfigureAwait(false);

        user.ChangePassword(request.OldPassword, request.NewPassword, _passwordHasher);
        await _userRepository.UpdateAsync(user, ct).ConfigureAwait(false);
        await _unitOfWork.SaveEntitiesAsync(ct).ConfigureAwait(false);

        // 撤销该用户所有 RefreshToken，强制重新登录
        await _refreshTokenRepository.RevokeAllByUserAsync(user.Id, RevokeReasonPasswordChange, ct)
            .ConfigureAwait(false);

        _logger.LogInformation("用户密码修改成功，UserId={UserId}", userId);
    }

    private async Task<User> RequireUserAsync(Guid userId, CancellationToken ct)
    {
        var user = await _userRepository.GetByIdAsync(userId, ct).ConfigureAwait(false);
        if (user is null)
        {
            throw new IdentityDomainException("用户不存在", "USER_NOT_FOUND");
        }

        return user;
    }

    private static UserDto ToUserDto(User user)
        => new()
        {
            Id = user.Id,
            Username = user.Username,
            Email = user.Email,
            PhoneNumber = user.PhoneNumber,
            Nickname = user.Nickname,
            AvatarUrl = user.AvatarUrl,
            Status = user.Status,
            CreatedAt = user.CreatedAt,
            UpdatedAt = user.UpdatedAt
        };
}
