using Leno.UserAuth.Domain.Exceptions;
using Leno.UserAuth.Domain.Services;

namespace Leno.UserAuth.Infrastructure.Auth;

/// <summary>
/// OAuth2 提供方解析器，根据 Provider 字符串（google / wechat / alipay）解析对应的 <see cref="IExternalAuthService"/> 实现。
/// </summary>
public sealed class OAuth2ProviderResolver
{
    private readonly IEnumerable<IExternalAuthService> _services;

    public OAuth2ProviderResolver(IEnumerable<IExternalAuthService> services)
    {
        _services = services ?? throw new ArgumentNullException(nameof(services));
    }

    /// <summary>
    /// 根据提供方标识解析对应的 OAuth2 服务实现。
    /// </summary>
    /// <exception cref="UserAuthDomainException">当提供方未注册时抛出。</exception>
    public IExternalAuthService Resolve(string provider)
    {
        if (string.IsNullOrWhiteSpace(provider))
        {
            throw new UserAuthDomainException("OAuth2 提供方不可为空", "OAUTH_PROVIDER_EMPTY");
        }

        var normalized = provider.Trim().ToLowerInvariant();
        var service = _services.FirstOrDefault(s =>
            string.Equals(s.Provider, normalized, StringComparison.OrdinalIgnoreCase));

        if (service is null)
        {
            throw new UserAuthDomainException(
                $"不支持的 OAuth2 提供方: {provider}", "OAUTH_PROVIDER_NOT_FOUND", 400);
        }

        return service;
    }
}