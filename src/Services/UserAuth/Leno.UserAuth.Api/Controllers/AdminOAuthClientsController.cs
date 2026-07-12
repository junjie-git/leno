using Leno.Infrastructure.Auth;
using Leno.SharedContracts.Responses;
using Leno.UserAuth.Application;
using Leno.UserAuth.Application.DTOs;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Leno.UserAuth.Api.Controllers;

/// <summary>
/// OAuth2 客户端配置管理控制器，提供 OAuth 提供方参数配置端点。
/// 仅 Admin 角色可访问。
/// </summary>
[Authorize(Roles = "Admin")]
[ApiController]
[Route("api/admin/oauth-clients")]
public sealed class AdminOAuthClientsController : UserAuthControllerBase
{
    private readonly IOAuthClientAppService _oauthClientAppService;

    public AdminOAuthClientsController(ICurrentUserContext currentUser, IOAuthClientAppService oauthClientAppService)
        : base(currentUser)
    {
        ArgumentNullException.ThrowIfNull(oauthClientAppService);
        _oauthClientAppService = oauthClientAppService;
    }

    /// <summary>查询所有 OAuth 客户端配置列表（ClientSecret 掩码返回）。</summary>
    [HttpGet]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyList<OAuthClientDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAllAsync(CancellationToken ct)
    {
        var clients = await _oauthClientAppService.GetAllAsync(ct);
        return Ok(ApiResponse.Success(clients));
    }

    /// <summary>更新指定提供方的 OAuth 客户端配置（不存在则创建）。</summary>
    [HttpPut("{provider}")]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> UpdateAsync(string provider, [FromBody] UpdateOAuthClientDto dto, CancellationToken ct)
    {
        await _oauthClientAppService.UpdateAsync(provider, dto, ct);
        return Ok(ApiResponse.Success("OAuth 客户端配置已更新"));
    }

    /// <summary>启用指定提供方。</summary>
    [HttpPost("{provider}/enable")]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> EnableAsync(string provider, CancellationToken ct)
    {
        await _oauthClientAppService.EnableAsync(provider, ct);
        return Ok(ApiResponse.Success("OAuth 提供方已启用"));
    }

    /// <summary>禁用指定提供方。</summary>
    [HttpPost("{provider}/disable")]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> DisableAsync(string provider, CancellationToken ct)
    {
        await _oauthClientAppService.DisableAsync(provider, ct);
        return Ok(ApiResponse.Success("OAuth 提供方已禁用"));
    }
}