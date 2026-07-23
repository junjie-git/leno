namespace Leno.Identity.Domain.Services;

/// <summary>
/// OAuth2 / OIDC / SAML2 提供方适配器工厂抽象（Identity BC，3.7 OAuth/SSO 通用化）。
/// <para>
/// 注入所有 <see cref="IOAuth2ProviderAdapter"/> 实现，按 <see cref="IOAuth2ProviderAdapter.ProviderType"/>
/// 路由解析。Application 层通过本接口消费适配器，避免直接依赖 Infrastructure 层实现。
/// </para>
/// </summary>
public interface IOAuth2ProviderFactory
{
    /// <summary>
    /// 按 <see cref="IOAuth2ProviderAdapter.ProviderType"/> 查找适配器（大小写不敏感）。
    /// </summary>
    /// <param name="providerType">协议类型：Oidc / Google / WeChat / Saml2。</param>
    /// <returns>对应的适配器实现。</returns>
    /// <exception cref="InvalidOperationException">未注册该协议类型的适配器。</exception>
    IOAuth2ProviderAdapter GetAdapter(string providerType);

    /// <summary>获取所有已注册的协议类型，供健康检查与调试使用。</summary>
    IReadOnlyCollection<string> GetRegisteredProviderTypes();
}
