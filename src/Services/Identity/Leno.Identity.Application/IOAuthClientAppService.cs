using Leno.Identity.Application.DTOs;

namespace Leno.Identity.Application;

/// <summary>
/// OAuth2 客户端配置管理应用服务接口（Identity BC，Task A2 补齐）。
/// 承载 OAuth2 客户端配置的查询、新建、更新与启停用例，供 A4 AdminOAuthClientsController 消费。
/// ClientSecret 加密存储，查询时掩码返回。
/// </summary>
public interface IOAuthClientAppService
{
    /// <summary>查询所有 OAuth2 客户端配置列表（ClientSecret 掩码返回）。</summary>
    /// <param name="ct">取消令牌。</param>
    /// <returns>OAuth2 客户端配置列表。</returns>
    Task<IReadOnlyList<OAuthClientDto>> GetAllAsync(CancellationToken ct = default);

    /// <summary>
    /// 新建 OAuth2 客户端配置，默认 Enabled=false，需显式调用 <see cref="EnableAsync"/> 启用。
    /// 若 provider 已存在则抛异常。
    /// </summary>
    /// <param name="request">配置请求。</param>
    /// <param name="ct">取消令牌。</param>
    Task CreateAsync(OAuthClientDto request, CancellationToken ct = default);

    /// <summary>
    /// 更新指定提供方的 OAuth2 客户端配置。不存在则抛异常，不自动创建。
    /// </summary>
    /// <param name="provider">提供方标识。</param>
    /// <param name="request">配置请求。</param>
    /// <param name="ct">取消令牌。</param>
    Task UpdateAsync(string provider, OAuthClientDto request, CancellationToken ct = default);

    /// <summary>启用指定提供方。</summary>
    /// <param name="provider">提供方标识。</param>
    /// <param name="ct">取消令牌。</param>
    Task EnableAsync(string provider, CancellationToken ct = default);

    /// <summary>禁用指定提供方。</summary>
    /// <param name="provider">提供方标识。</param>
    /// <param name="ct">取消令牌。</param>
    Task DisableAsync(string provider, CancellationToken ct = default);
}
