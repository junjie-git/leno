using Leno.UserAuth.Domain.Aggregates;

namespace Leno.UserAuth.Domain.Repositories;

/// <summary>
/// OAuth2 客户端配置仓储接口，定义在领域层，由基础设施层实现。
/// </summary>
public interface IOAuthClientRepository
{
    /// <summary>按提供方标识查询 OAuth 客户端配置。</summary>
    Task<OAuthClient?> GetByProviderAsync(string provider, CancellationToken ct = default);

    /// <summary>查询所有 OAuth 客户端配置列表。</summary>
    Task<IReadOnlyList<OAuthClient>> GetAllAsync(CancellationToken ct = default);

    /// <summary>新增 OAuth 客户端配置。</summary>
    Task AddAsync(OAuthClient client, CancellationToken ct = default);

    /// <summary>更新 OAuth 客户端配置。</summary>
    Task UpdateAsync(OAuthClient client, CancellationToken ct = default);
}