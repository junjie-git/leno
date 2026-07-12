using Leno.Infrastructure.Auth;
using Leno.SharedContracts.Responses;
using Leno.UserAuth.Application;
using Leno.UserAuth.Application.DTOs;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Leno.UserAuth.Api.Controllers;

/// <summary>
/// 账户管理控制器，提供外部登录绑定/解绑等账户操作端点。
/// 需认证用户访问。
/// </summary>
[Authorize]
[ApiController]
[Route("api/account")]
public sealed class AccountController : UserAuthControllerBase
{
    private readonly IAccountAppService _accountAppService;

    public AccountController(ICurrentUserContext currentUser, IAccountAppService accountAppService)
        : base(currentUser)
    {
        ArgumentNullException.ThrowIfNull(accountAppService);
        _accountAppService = accountAppService;
    }

    /// <summary>绑定外部登录（通过 OAuth2 授权码交换后绑定）。</summary>
    [HttpPost("external-logins")]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> BindExternalLoginAsync([FromBody] BindExternalLoginDto dto, CancellationToken ct)
    {
        var userId = GetCurrentUserId();
        await _accountAppService.BindExternalLoginAsync(userId, dto, ct);
        return Ok(ApiResponse.Success("外部登录绑定成功"));
    }

    /// <summary>解绑指定提供方的外部登录。</summary>
    [HttpDelete("external-logins/{provider}")]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> UnbindExternalLoginAsync(string provider, CancellationToken ct)
    {
        var userId = GetCurrentUserId();
        await _accountAppService.UnbindExternalLoginAsync(userId, provider, ct);
        return Ok(ApiResponse.Success("外部登录解绑成功"));
    }
}