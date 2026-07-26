using Leno.Identity.Application;
using Leno.Identity.Application.DTOs;
using Leno.Infrastructure.Auth;
using Leno.SharedContracts.Responses;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Leno.Identity.Api.Controllers;

/// <summary>
/// 账户管理控制器（Identity BC，Task A3 新建 2 端点）。
/// 提供外部登录绑定/解绑端点。
/// <para>
/// 全部端点需认证；类级路由 <c>api/account</c>；UserId 从 <see cref="ICurrentUserContext"/> 取。
/// </para>
/// <para>
/// 统一使用 <see cref="ApiResponse{T}"/> 包装响应；POST/DELETE 返回 200 OK（不用 201/204）。
/// </para>
/// </summary>
[ApiController]
[Authorize]
[Route("api/account")]
public sealed class AccountController : ControllerBase
{
    private readonly ICurrentUserContext _currentUser;
    private readonly IExternalLoginService _externalLoginService;

    public AccountController(
        ICurrentUserContext currentUser,
        IExternalLoginService externalLoginService)
    {
        _currentUser = currentUser ?? throw new ArgumentNullException(nameof(currentUser));
        _externalLoginService = externalLoginService ?? throw new ArgumentNullException(nameof(externalLoginService));
    }

    /// <summary>
    /// 绑定外部登录到已有账户。
    /// 简化处理：将 OAuth2 授权码（<see cref="BindExternalLoginDto.Code"/>）作为 providerUserId 直接传入
    /// <see cref="IExternalLoginService.BindAsync"/>，由 Service 层决定是否需要交换 code。
    /// </summary>
    /// <param name="dto">绑定请求，含 Provider / Code / RedirectUri。</param>
    /// <param name="ct">取消令牌。</param>
    /// <response code="200">外部登录绑定成功。</response>
    /// <response code="401">未鉴权。</response>
    /// <response code="400">提供方不可为空或该第三方账户已被其他用户绑定。</response>
    [HttpPost("external-logins")]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> BindExternalLoginAsync([FromBody] BindExternalLoginDto dto, CancellationToken ct)
    {
        var userId = GetCurrentUserId();
        if (userId is null)
        {
            return Unauthorized();
        }

        // 此处简化：直接将 Code 作为 providerUserId 传入，由 Service 层处理 code 交换。
        // 若 Service 层需要 redirectUri 进行 code 交换，应在 BindAsync 内部通过 IOAuthService 完成。
        await _externalLoginService.BindAsync(userId.Value, dto.Provider, dto.Code, ct).ConfigureAwait(false);
        return Ok(ApiResponse.Success("外部登录绑定成功"));
    }

    /// <summary>解绑指定提供方的外部登录。OAuth 用户须至少保留一个外部登录绑定。</summary>
    /// <param name="provider">OAuth2 提供方标识（google / wechat / alipay）。</param>
    /// <param name="ct">取消令牌。</param>
    /// <response code="200">外部登录解绑成功。</response>
    /// <response code="401">未鉴权。</response>
    /// <response code="400">解绑后无可用登录方式。</response>
    [HttpDelete("external-logins/{provider}")]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> UnbindExternalLoginAsync([FromRoute] string provider, CancellationToken ct)
    {
        var userId = GetCurrentUserId();
        if (userId is null)
        {
            return Unauthorized();
        }

        await _externalLoginService.UnbindAsync(userId.Value, provider, ct).ConfigureAwait(false);
        return Ok(ApiResponse.Success("外部登录解绑成功"));
    }

    /// <summary>
    /// 从 <see cref="ICurrentUserContext"/> 取当前用户标识。
    /// 未鉴权或 UserId 缺失时返回 null，由调用方返回 401。
    /// </summary>
    private Guid? GetCurrentUserId()
    {
        if (!_currentUser.IsAuthenticated || !_currentUser.UserId.HasValue)
        {
            return null;
        }

        return _currentUser.UserId.Value;
    }
}
