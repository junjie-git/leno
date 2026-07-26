using Leno.Identity.Application.DTOs;
using Leno.Identity.Domain.Aggregates;
using Leno.Identity.Domain.Exceptions;
using Leno.Identity.Domain.Repositories;
using Leno.Identity.Domain.Services;
using Leno.SharedKernel.Abstractions;
using Microsoft.Extensions.Logging;

namespace Leno.Identity.Application.Services;

/// <summary>
/// 双因子认证（TOTP）应用服务实现（Identity BC，Task A2 补齐）。
/// <para>
/// 面向基于 TOTP 共享密钥的认证器 App 模式（Google Authenticator 等），
/// 通过 <see cref="User"/> 聚合的 2FA 行为方法与 <see cref="ITokenVerifier"/> 领域服务完成
/// 启用、确认、禁用与登录二次验证。
/// </para>
/// </summary>
public sealed class TwoFactorService : ITwoFactorService
{
    private readonly IUserRepository _userRepository;
    private readonly ITokenVerifier _tokenVerifier;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<TwoFactorService> _logger;

    public TwoFactorService(
        IUserRepository userRepository,
        ITokenVerifier tokenVerifier,
        IUnitOfWork unitOfWork,
        ILogger<TwoFactorService> logger)
    {
        _userRepository = userRepository ?? throw new ArgumentNullException(nameof(userRepository));
        _tokenVerifier = tokenVerifier ?? throw new ArgumentNullException(nameof(tokenVerifier));
        _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <inheritdoc />
    public async Task<bool> VerifyAsync(Guid userId, string code, CancellationToken ct = default)
    {
        if (userId == Guid.Empty)
        {
            throw new ArgumentException("UserId 不可为空", nameof(userId));
        }

        if (string.IsNullOrWhiteSpace(code))
        {
            throw new IdentityDomainException("验证码不可为空", "USER_2FA_CODE_EMPTY");
        }

        var user = await RequireUserAsync(userId, ct).ConfigureAwait(false);

        var verified = user.VerifyTwoFactorCode(code.Trim(), _tokenVerifier);

        _logger.LogInformation("双因子验证结果：{Result}, UserId={UserId}", verified, userId);

        return verified;
    }

    /// <inheritdoc />
    public async Task<TwoFactorEnableResponseDto> EnableTwoFactorAsync(Guid userId, CancellationToken ct = default)
    {
        if (userId == Guid.Empty)
        {
            throw new ArgumentException("UserId 不可为空", nameof(userId));
        }

        var user = await RequireUserAsync(userId, ct).ConfigureAwait(false);

        var qrCodeUri = user.EnableTwoFactor(_tokenVerifier);
        await _userRepository.UpdateAsync(user, ct).ConfigureAwait(false);
        await _unitOfWork.SaveEntitiesAsync(ct).ConfigureAwait(false);

        _logger.LogInformation("双因子认证已启用（待确认），UserId={UserId}", userId);

        return new TwoFactorEnableResponseDto
        {
            Secret = user.TwoFactorSecret!,
            QrCodeUri = qrCodeUri
        };
    }

    /// <inheritdoc />
    public async Task ConfirmTwoFactorAsync(Guid userId, string code, CancellationToken ct = default)
    {
        if (userId == Guid.Empty)
        {
            throw new ArgumentException("UserId 不可为空", nameof(userId));
        }

        if (string.IsNullOrWhiteSpace(code))
        {
            throw new IdentityDomainException("验证码不可为空", "USER_2FA_CODE_EMPTY");
        }

        var user = await RequireUserAsync(userId, ct).ConfigureAwait(false);
        user.ConfirmTwoFactor(code.Trim(), _tokenVerifier);
        await _userRepository.UpdateAsync(user, ct).ConfigureAwait(false);
        await _unitOfWork.SaveEntitiesAsync(ct).ConfigureAwait(false);

        _logger.LogInformation("双因子认证已确认启用，UserId={UserId}", userId);
    }

    /// <inheritdoc />
    public async Task DisableTwoFactorAsync(Guid userId, CancellationToken ct = default)
    {
        if (userId == Guid.Empty)
        {
            throw new ArgumentException("UserId 不可为空", nameof(userId));
        }

        var user = await RequireUserAsync(userId, ct).ConfigureAwait(false);
        user.DisableTwoFactor();
        await _userRepository.UpdateAsync(user, ct).ConfigureAwait(false);
        await _unitOfWork.SaveEntitiesAsync(ct).ConfigureAwait(false);

        _logger.LogInformation("双因子认证已禁用，UserId={UserId}", userId);
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
}
