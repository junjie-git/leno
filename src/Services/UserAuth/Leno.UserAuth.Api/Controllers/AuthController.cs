using Leno.SharedContracts.Responses;
using Leno.UserAuth.Application;
using Leno.UserAuth.Application.DTOs;
using Microsoft.AspNetCore.Mvc;

namespace Leno.UserAuth.Api.Controllers;

/// <summary>
/// 认证控制器，提供账号注册、登录、刷新令牌与 OAuth2 第三方登录端点。
/// 匿名可访问，异常经全局异常中间件统一转换为标准响应。
/// </summary>
[ApiController]
[Route("api/auth")]
public sealed class AuthController : ControllerBase
{
    private readonly IUserAppService _userAppService;

    public AuthController(IUserAppService userAppService)
    {
        ArgumentNullException.ThrowIfNull(userAppService);
        _userAppService = userAppService;
    }

    /// <summary>注册账户并签发令牌。</summary>
    [HttpPost("register")]
    [ProducesResponseType(typeof(ApiResponse<TokenDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> RegisterAsync([FromBody] RegisterDto dto, CancellationToken ct)
    {
        var token = await _userAppService.RegisterAsync(dto, ct);
        return Ok(ApiResponse.Success(token));
    }

    /// <summary>账号密码登录并签发令牌。</summary>
    [HttpPost("login")]
    [ProducesResponseType(typeof(ApiResponse<TokenDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> LoginAsync([FromBody] LoginDto dto, CancellationToken ct)
    {
        var token = await _userAppService.LoginAsync(dto, ct);
        return Ok(ApiResponse.Success(token));
    }

    /// <summary>使用刷新令牌换取新的访问与刷新令牌。</summary>
    [HttpPost("refresh-token")]
    [ProducesResponseType(typeof(ApiResponse<TokenDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> RefreshTokenAsync([FromBody] RefreshTokenDto dto, CancellationToken ct)
    {
        var token = await _userAppService.RefreshTokenAsync(dto.RefreshToken, ct);
        return Ok(ApiResponse.Success(token));
    }

    /// <summary>
    /// OAuth2 第三方登录入口，生成授权 URL 并返回。
    /// 前端收到后跳转至第三方授权页面。
    /// </summary>
    [HttpGet("oauth/{provider}/login")]
    [ProducesResponseType(typeof(ApiResponse<OAuthLoginResponseDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> OAuthLoginAsync(
        [FromRoute] string provider,
        [FromQuery] string redirectUri,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(redirectUri))
        {
            return BadRequest(ApiResponse.Fail(400, "redirectUri 不可为空"));
        }

        var authorizationUrl = await _userAppService.GetOAuthLoginUrlAsync(provider, redirectUri, ct);
        return Ok(ApiResponse.Success(new OAuthLoginResponseDto { AuthorizationUrl = authorizationUrl }));
    }

    /// <summary>
    /// OAuth2 回调端点，第三方授权后前端将 code 与 state 提交至此完成登录/注册。
    /// </summary>
    [HttpGet("oauth/{provider}/callback")]
    [ProducesResponseType(typeof(ApiResponse<TokenDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> OAuthCallbackAsync(
        [FromRoute] string provider,
        [FromQuery] string code,
        [FromQuery] string state,
        [FromQuery] string redirectUri,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(code))
        {
            return BadRequest(ApiResponse.Fail(400, "code 不可为空"));
        }

        if (string.IsNullOrWhiteSpace(state))
        {
            return BadRequest(ApiResponse.Fail(400, "state 不可为空"));
        }

        // 从 state 中恢复 redirectUri（若 query 未提供则使用回调 URL 自身）
        if (string.IsNullOrWhiteSpace(redirectUri))
        {
            redirectUri = $"{Request.Scheme}://{Request.Host}/api/auth/oauth/{provider}/callback";
        }

        var token = await _userAppService.HandleOAuthCallbackAsync(provider, code, state, redirectUri, ct);
        return Ok(ApiResponse.Success(token));
    }

    /// <summary>双因子认证二次验证（登录流程），验证 TOTP 码并签发 JWT。</summary>
    [HttpPost("two-factor/verify")]
    [ProducesResponseType(typeof(ApiResponse<TokenDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> VerifyTwoFactorAsync([FromBody] TwoFactorVerifyDto dto, CancellationToken ct)
    {
        var token = await _userAppService.VerifyTwoFactorAsync(dto, ct);
        return Ok(ApiResponse.Success(token));
    }

    /// <summary>忘记密码：发送验证码/重置链接。</summary>
    [HttpPost("forgot-password")]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> ForgotPasswordAsync([FromBody] ForgotPasswordDto dto, CancellationToken ct)
    {
        await _userAppService.ForgotPasswordAsync(dto, ct);
        return Ok(ApiResponse.Success("若账号存在，重置链接已发送"));
    }

    /// <summary>重置密码。</summary>
    [HttpPost("reset-password")]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> ResetPasswordAsync([FromBody] ResetPasswordDto dto, CancellationToken ct)
    {
        await _userAppService.ResetPasswordAsync(dto, ct);
        return Ok(ApiResponse.Success("密码重置成功"));
    }
}
