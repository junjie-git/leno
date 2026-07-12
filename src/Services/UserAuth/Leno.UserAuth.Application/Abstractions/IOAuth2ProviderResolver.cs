using Leno.UserAuth.Domain.Services;

namespace Leno.UserAuth.Application.Abstractions;

/// <summary>
/// OAuth2 提供方解析器抽象，定义在应用层，由基础设施层实现。
/// </summary>
public interface IOAuth2ProviderResolver
{
    /// <summary>根据提供方标识解析对应的 OAuth2 服务实现。</summary>
    IExternalAuthService Resolve(string provider);
}