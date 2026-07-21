using Leno.UserAuth.Application.DTOs;

namespace Leno.UserAuth.Application;

/// <summary>
/// OAuth2 客户端配置管理应用服务接口。
/// 写操作需传入 <paramref name="operatorId" /> 用于审计日志追溯。
/// Create 与 Update 严格分离：CreateAsync 负责新建（默认 Enabled=false），
/// UpdateAsync 仅更新已存在配置，不存在则抛 <c>OAUTH_CLIENT_NOT_FOUND</c>。
/// </summary>
public interface IOAuthClientAppService
{
    /// <summary>查询所有 OAuth 客户端配置列表（ClientSecret 掩码返回）。</summary>
    Task<IReadOnlyList<OAuthClientDto>> GetAllAsync(CancellationToken ct = default);

    /// <summary>
    /// 新建 OAuth2 客户端配置，默认 <c>Enabled=false</c>，需显式调用 <see cref="EnableAsync"/> 启用。
    /// 若 provider 已存在则抛 <c>OAUTH_CLIENT_ALREADY_EXISTS</c>。
    /// </summary>
    Task CreateAsync(string provider, UpdateOAuthClientDto dto, Guid operatorId, CancellationToken ct = default);

    /// <summary>
    /// 更新指定提供方的 OAuth 客户端配置。不存在则抛 <c>OAUTH_CLIENT_NOT_FOUND</c>，不自动创建。
    /// </summary>
    Task UpdateAsync(string provider, UpdateOAuthClientDto dto, Guid operatorId, CancellationToken ct = default);

    /// <summary>启用指定提供方。</summary>
    Task EnableAsync(string provider, Guid operatorId, CancellationToken ct = default);

    /// <summary>禁用指定提供方。</summary>
    Task DisableAsync(string provider, Guid operatorId, CancellationToken ct = default);
}
