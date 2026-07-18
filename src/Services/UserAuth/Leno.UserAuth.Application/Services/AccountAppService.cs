using Leno.SharedKernel.Abstractions;
using Leno.UserAuth.Application.Abstractions;
using Leno.UserAuth.Application.DTOs;
using Leno.UserAuth.Domain.Exceptions;
using Leno.UserAuth.Domain.Repositories;

namespace Leno.UserAuth.Application.Services;

/// <summary>
/// 账户管理应用服务实现，处理外部登录绑定/解绑等操作。
/// </summary>
public sealed class AccountAppService : IAccountAppService
{
    private readonly IUserRepository _userRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IOAuth2ProviderResolver _providerResolver;

    public AccountAppService(
        IUserRepository userRepository,
        IUnitOfWork unitOfWork,
        IOAuth2ProviderResolver providerResolver)
    {
        ArgumentNullException.ThrowIfNull(userRepository);
        ArgumentNullException.ThrowIfNull(unitOfWork);
        ArgumentNullException.ThrowIfNull(providerResolver);
        _userRepository = userRepository;
        _unitOfWork = unitOfWork;
        _providerResolver = providerResolver;
    }

    public async Task BindExternalLoginAsync(Guid userId, BindExternalLoginDto dto, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(dto);

        if (string.IsNullOrWhiteSpace(dto.Provider))
        {
            throw new UserAuthDomainException("OAuth2 提供方不可为空", "OAUTH_PROVIDER_EMPTY");
        }

        if (string.IsNullOrWhiteSpace(dto.Code))
        {
            throw new UserAuthDomainException("授权码不可为空", "OAUTH_CODE_EMPTY");
        }

        if (string.IsNullOrWhiteSpace(dto.RedirectUri))
        {
            throw new UserAuthDomainException("回调地址不可为空", "OAUTH_REDIRECT_URI_EMPTY");
        }

        var user = await _userRepository.GetByIdAsync(userId, ct);
        if (user is null)
        {
            throw new UserAuthDomainException("用户不存在", "USER_NOT_FOUND");
        }

        // 通过 OAuth2 提供方交换授权码，获取第三方用户信息
        var authService = _providerResolver.Resolve(dto.Provider);
        var externalInfo = await authService.ExchangeCodeAsync(dto.Code, dto.RedirectUri, ct);

        // 检查同 provider + providerUserId 是否已被其他用户绑定
        var existingUser = await _userRepository.FindByExternalLoginAsync(
            externalInfo.Provider, externalInfo.ProviderUserId, ct);
        if (existingUser is not null && existingUser.Id != userId)
        {
            throw new UserAuthDomainException(
                $"该 {externalInfo.Provider} 账户已被其他用户绑定", "EXTERNAL_LOGIN_ALREADY_BOUND");
        }

        user.LinkExternalLogin(
            externalInfo.Provider,
            externalInfo.ProviderUserId,
            externalInfo.Email,
            externalInfo.Name,
            externalInfo.AvatarUrl);

        await _userRepository.UpdateAsync(user, ct);
        await _unitOfWork.SaveChangesAsync(ct);
    }

    public async Task UnbindExternalLoginAsync(Guid userId, string provider, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(provider))
        {
            throw new UserAuthDomainException("OAuth2 提供方不可为空", "OAUTH_PROVIDER_EMPTY");
        }

        var user = await _userRepository.GetByIdAsync(userId, ct);
        if (user is null)
        {
            throw new UserAuthDomainException("用户不存在", "USER_NOT_FOUND");
        }

        user.UnlinkExternalLogin(provider);

        await _userRepository.UpdateAsync(user, ct);
        await _unitOfWork.SaveChangesAsync(ct);
    }
}