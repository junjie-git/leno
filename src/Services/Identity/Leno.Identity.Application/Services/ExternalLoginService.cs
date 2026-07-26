using Leno.Identity.Domain.Aggregates;
using Leno.Identity.Domain.Exceptions;
using Leno.Identity.Domain.Repositories;
using Leno.Identity.Domain.Services;
using Leno.SharedKernel.Abstractions;
using Microsoft.Extensions.Logging;

namespace Leno.Identity.Application.Services;

/// <summary>
/// 外部登录绑定应用服务实现（Identity BC，Task A2 补齐）。
/// 承载外部登录的绑定与解绑用例，操作 User 聚合的 ExternalLogins 集合。
/// </summary>
public sealed class ExternalLoginService : IExternalLoginService
{
    private readonly IUserRepository _userRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<ExternalLoginService> _logger;

    public ExternalLoginService(
        IUserRepository userRepository,
        IUnitOfWork unitOfWork,
        ILogger<ExternalLoginService> logger)
    {
        _userRepository = userRepository ?? throw new ArgumentNullException(nameof(userRepository));
        _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <inheritdoc />
    public async Task BindAsync(Guid userId, string provider, string providerUserId, CancellationToken ct = default)
    {
        if (userId == Guid.Empty)
        {
            throw new ArgumentException("UserId 不可为空", nameof(userId));
        }

        if (string.IsNullOrWhiteSpace(provider))
        {
            throw new IdentityDomainException("OAuth2 提供方不可为空", "OAUTH_PROVIDER_EMPTY");
        }

        if (string.IsNullOrWhiteSpace(providerUserId))
        {
            throw new IdentityDomainException("第三方用户标识不可为空", "OAUTH_PROVIDER_USER_ID_EMPTY");
        }

        var user = await RequireUserAsync(userId, ct).ConfigureAwait(false);

        // 检查同 provider + providerUserId 是否已被其他用户绑定
        var existingUser = await _userRepository.FindByExternalLoginAsync(
            provider.Trim().ToLowerInvariant(), providerUserId.Trim(), ct).ConfigureAwait(false);
        if (existingUser is not null && existingUser.Id != userId)
        {
            throw new IdentityDomainException(
                $"该 {provider} 账户已被其他用户绑定", "EXTERNAL_LOGIN_ALREADY_BOUND");
        }

        user.LinkExternalLogin(
            provider,
            providerUserId,
            email: null,
            name: null,
            avatarUrl: null);

        await _userRepository.UpdateAsync(user, ct).ConfigureAwait(false);
        await _unitOfWork.SaveEntitiesAsync(ct).ConfigureAwait(false);

        _logger.LogInformation("外部登录绑定成功，UserId={UserId}, Provider={Provider}",
            userId, provider);
    }

    /// <inheritdoc />
    public async Task UnbindAsync(Guid userId, string provider, CancellationToken ct = default)
    {
        if (userId == Guid.Empty)
        {
            throw new ArgumentException("UserId 不可为空", nameof(userId));
        }

        if (string.IsNullOrWhiteSpace(provider))
        {
            throw new IdentityDomainException("OAuth2 提供方不可为空", "OAUTH_PROVIDER_EMPTY");
        }

        var user = await RequireUserAsync(userId, ct).ConfigureAwait(false);

        user.UnlinkExternalLogin(provider);

        await _userRepository.UpdateAsync(user, ct).ConfigureAwait(false);
        await _unitOfWork.SaveEntitiesAsync(ct).ConfigureAwait(false);

        _logger.LogInformation("外部登录解绑成功，UserId={UserId}, Provider={Provider}",
            userId, provider);
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
