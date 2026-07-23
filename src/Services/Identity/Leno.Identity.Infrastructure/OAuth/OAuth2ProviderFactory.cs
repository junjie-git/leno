using Leno.Identity.Domain.Services;

namespace Leno.Identity.Infrastructure.OAuth;

/// <summary>
/// OAuth2 / OIDC / SAML2 提供方适配器工厂（Identity BC，3.7 OAuth/SSO 通用化）。
/// <para>
/// 注入所有 <see cref="IOAuth2ProviderAdapter"/> 实现，按 <see cref="IOAuth2ProviderAdapter.ProviderType"/>
/// 路由解析（大小写不敏感）。新增协议类型仅需实现接口并注册到 DI，工厂自动支持。
/// </para>
/// </summary>
public sealed class OAuth2ProviderFactory : IOAuth2ProviderFactory
{
    private readonly IReadOnlyDictionary<string, IOAuth2ProviderAdapter> _adaptersByType;

    /// <summary>
    /// 初始化工厂，按 <see cref="IOAuth2ProviderAdapter.ProviderType"/> 建立大小写不敏感的查找索引。
    /// 重复注册同一 ProviderType 时以最后注册的实现为准（容器解析顺序）。
    /// </summary>
    public OAuth2ProviderFactory(IEnumerable<IOAuth2ProviderAdapter> adapters)
    {
        ArgumentNullException.ThrowIfNull(adapters);

        var dict = new Dictionary<string, IOAuth2ProviderAdapter>(StringComparer.OrdinalIgnoreCase);
        foreach (var adapter in adapters)
        {
            if (adapter is null || string.IsNullOrWhiteSpace(adapter.ProviderType))
            {
                continue;
            }

            // 后注册覆盖先注册，与 DI 容器解析顺序一致
            dict[adapter.ProviderType] = adapter;
        }

        _adaptersByType = dict;
    }

    /// <summary>
    /// 按 <see cref="IOAuth2ProviderAdapter.ProviderType"/> 查找适配器。
    /// 大小写不敏感比较。
    /// </summary>
    /// <param name="providerType">协议类型：Oidc / Google / WeChat / Saml2。</param>
    /// <returns>对应的适配器实现。</returns>
    /// <exception cref="InvalidOperationException">未注册该协议类型的适配器。</exception>
    public IOAuth2ProviderAdapter GetAdapter(string providerType)
    {
        if (string.IsNullOrWhiteSpace(providerType))
        {
            throw new InvalidOperationException("ProviderType 不可为空");
        }

        if (_adaptersByType.TryGetValue(providerType, out var adapter))
        {
            return adapter;
        }

        var available = _adaptersByType.Count > 0
            ? string.Join(", ", _adaptersByType.Keys.OrderBy(k => k))
            : "(无已注册适配器)";
        throw new InvalidOperationException(
            $"不支持的 OAuth2 提供方协议类型：{providerType}。已注册类型：{available}");
    }

    /// <summary>获取所有已注册的协议类型，供健康检查与调试使用。</summary>
    public IReadOnlyCollection<string> GetRegisteredProviderTypes()
        => _adaptersByType.Keys.ToList();
}
