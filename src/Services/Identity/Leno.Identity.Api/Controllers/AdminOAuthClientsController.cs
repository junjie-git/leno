using Leno.Identity.Application;
using Leno.Identity.Application.DTOs;
using Leno.SharedContracts.Responses;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Leno.Identity.Api.Controllers;

/// <summary>
/// OAuth2 客户端配置管理控制器（Identity BC，Task A4 新建 5 端点）。
/// <para>
/// 提供 OAuth2 提供方参数配置的查询、新建、更新与启停端点，供运营人员管理第三方登录集成。
/// 仅 <c>Operator</c> 与 <c>Admin</c> 角色可访问；新建默认 <c>Enabled=false</c>，需显式调用
/// <c>/enable</c> 启用，防止未经验证的提供方配置立即生效。
/// </para>
/// <para>
/// 统一使用 <see cref="ApiResponse{T}"/> 包装响应；POST 创建/启停返回 200 OK（不用 201/204）。
/// Identity 接口签名不传 currentUserId，与旧域 UserAuth 不同。
/// </para>
/// </summary>
[ApiController]
[Route("api/admin/oauth-clients")]
[Authorize(Roles = "Operator,Admin")]
public sealed class AdminOAuthClientsController : ControllerBase
{
    private readonly IOAuthClientAppService _oauthClientAppService;

    public AdminOAuthClientsController(IOAuthClientAppService oauthClientAppService)
    {
        _oauthClientAppService = oauthClientAppService ?? throw new ArgumentNullException(nameof(oauthClientAppService));
    }

    /// <summary>查询所有 OAuth 客户端配置列表（ClientSecret 掩码返回）。</summary>
    /// <param name="ct">取消令牌。</param>
    /// <response code="200">返回 OAuth 客户端配置列表。</response>
    /// <response code="401">未鉴权。</response>
    /// <response code="403">角色无权访问。</response>
    [HttpGet]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyList<OAuthClientDto>>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetAllAsync(CancellationToken ct)
    {
        var clients = await _oauthClientAppService.GetAllAsync(ct).ConfigureAwait(false);
        return Ok(ApiResponse.Success(clients));
    }

    /// <summary>
    /// 新建 OAuth 客户端配置（默认 Enabled=false，需显式调用 /enable 启用）。
    /// 路由 provider 会写入 dto.Provider 后调用 Service，确保客户端无法绕过路由覆盖其他提供方配置。
    /// </summary>
    /// <param name="provider">OAuth2 提供方标识（如 google / wechat）。</param>
    /// <param name="dto">配置请求体。</param>
    /// <param name="ct">取消令牌。</param>
    /// <response code="200">OAuth 客户端配置已创建。</response>
    /// <response code="400">请求参数无效。</response>
    /// <response code="409">provider 已存在。</response>
    [HttpPost("{provider}")]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> CreateAsync(
        [FromRoute] string provider,
        [FromBody] OAuthClientDto dto,
        CancellationToken ct)
    {
        // OAuthClientDto 是 sealed class 配合 init-only 属性，不支持 with 表达式，
        // 此处显式构造新实例用路由 provider 覆盖 dto.Provider，防止客户端篡改。
        var request = new OAuthClientDto
        {
            Provider = provider,
            ProviderType = dto.ProviderType,
            DiscoveryUrl = dto.DiscoveryUrl,
            ClientId = dto.ClientId,
            ClientSecret = dto.ClientSecret,
            RedirectUri = dto.RedirectUri,
            Scopes = dto.Scopes,
            Enabled = dto.Enabled
        };

        await _oauthClientAppService.CreateAsync(request, ct).ConfigureAwait(false);
        return Ok(ApiResponse.Success("OAuth 客户端配置已创建（默认禁用，需显式启用）"));
    }

    /// <summary>更新指定提供方的 OAuth 客户端配置（不存在返回 404，不自动创建）。</summary>
    /// <param name="provider">OAuth2 提供方标识。</param>
    /// <param name="dto">配置请求体。</param>
    /// <param name="ct">取消令牌。</param>
    /// <response code="200">OAuth 客户端配置已更新。</response>
    /// <response code="400">请求参数无效。</response>
    /// <response code="404">provider 不存在。</response>
    [HttpPut("{provider}")]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UpdateAsync(
        [FromRoute] string provider,
        [FromBody] OAuthClientDto dto,
        CancellationToken ct)
    {
        var request = new OAuthClientDto
        {
            Provider = provider,
            ProviderType = dto.ProviderType,
            DiscoveryUrl = dto.DiscoveryUrl,
            ClientId = dto.ClientId,
            ClientSecret = dto.ClientSecret,
            RedirectUri = dto.RedirectUri,
            Scopes = dto.Scopes,
            Enabled = dto.Enabled
        };

        await _oauthClientAppService.UpdateAsync(provider, request, ct).ConfigureAwait(false);
        return Ok(ApiResponse.Success("OAuth 客户端配置已更新"));
    }

    /// <summary>启用指定提供方。</summary>
    /// <param name="provider">OAuth2 提供方标识。</param>
    /// <param name="ct">取消令牌。</param>
    /// <response code="200">OAuth 提供方已启用。</response>
    /// <response code="404">provider 不存在。</response>
    [HttpPost("{provider}/enable")]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> EnableAsync([FromRoute] string provider, CancellationToken ct)
    {
        await _oauthClientAppService.EnableAsync(provider, ct).ConfigureAwait(false);
        return Ok(ApiResponse.Success("OAuth 提供方已启用"));
    }

    /// <summary>禁用指定提供方。</summary>
    /// <param name="provider">OAuth2 提供方标识。</param>
    /// <param name="ct">取消令牌。</param>
    /// <response code="200">OAuth 提供方已禁用。</response>
    /// <response code="404">provider 不存在。</response>
    [HttpPost("{provider}/disable")]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DisableAsync([FromRoute] string provider, CancellationToken ct)
    {
        await _oauthClientAppService.DisableAsync(provider, ct).ConfigureAwait(false);
        return Ok(ApiResponse.Success("OAuth 提供方已禁用"));
    }
}
