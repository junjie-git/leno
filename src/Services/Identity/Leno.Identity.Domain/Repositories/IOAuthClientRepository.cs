using Leno.SharedKernel.Abstractions;
using Leno.Identity.Domain.Aggregates;

namespace Leno.Identity.Domain.Repositories;

/// <summary>
/// OAuth2 客户端配置仓储接口。
/// 从 UserAuth BC 迁入 Identity BC（3.6 AuthN/AuthZ 拆分）。
/// </summary>
public interface IOAuthClientRepository : IRepository<OAuthClient>
{
    /// <summary>按提供方标识查询 OAuth2 客户端配置。</summary>
    Task<OAuthClient?> GetByProviderAsync(string provider, CancellationToken ct = default);

    /// <summary>查询所有已启用的 OAuth2 客户端。</summary>
    Task<IReadOnlyList<OAuthClient>> GetEnabledAsync(CancellationToken ct = default);

    /// <summary>
    /// 查询全部 OAuth2 客户端配置（含已禁用），供管理后台列表展示。
    /// 按 Provider 字典序返回。
    /// </summary>
    Task<IReadOnlyList<OAuthClient>> GetAllAsync(CancellationToken ct = default);
}
