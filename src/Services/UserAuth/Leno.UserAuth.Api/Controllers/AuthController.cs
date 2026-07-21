using Leno.SharedContracts.Responses;
using Leno.UserAuth.Application;
using Leno.UserAuth.Application.Abstractions;
using Leno.UserAuth.Application.DTOs;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;

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
    private readonly IJwtRevocationService _revocationService;
    private readonly OAuth2Options _oauth2Options;
    private readonly IHostEnvironment _environment;

    public AuthController(
        IUserAppService userAppService,
        IJwtRevocationService revocationService,
        IOptions<OAuth2Options> oauth2Options,
        IHostEnvironment environment)
    {
        ArgumentNullException.ThrowIfNull(userAppService);
        ArgumentNullException.ThrowIfNull(revocationService);
        ArgumentNullException.ThrowIfNull(oauth2Options);
        ArgumentNullException.ThrowIfNull(environment);
        _userAppService = userAppService;
        _revocationService = revocationService;
        _oauth2Options = oauth2Options.Value;
        _environment = environment;
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

    /// <summary>登出并吊销当前 JWT（写入黑名单，TTL 为 token 剩余有效期）。</summary>
    [HttpPost("logout")]
    [Authorize]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> LogoutAsync(CancellationToken ct)
    {
        // 从 JWT 提取 jti 与剩余有效期
        var jti = User.FindFirst(JwtRegisteredClaimNames.Jti)?.Value;
        var expClaim = User.FindFirst(JwtRegisteredClaimNames.Exp)?.Value;

        if (string.IsNullOrEmpty(jti) || string.IsNullOrEmpty(expClaim))
        {
            return BadRequest(ApiResponse.Fail(400, "Token 缺少必要声明"));
        }

        var exp = long.Parse(expClaim, System.Globalization.CultureInfo.InvariantCulture);
        var expiry = DateTimeOffset.FromUnixTimeSeconds(exp);
        var ttl = expiry - DateTimeOffset.UtcNow;
        if (ttl > TimeSpan.Zero)
        {
            await _revocationService.RevokeAsync(jti, ttl, ct);
        }

        return Ok(ApiResponse.Success());
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

        // 从 state 中恢复 redirectUri（若 query 未提供则使用回调 URL 自身）。
        // 必须使用配置中的 PublicBaseUrl，禁止直接信任 Request.Host，
        // 否则反向代理未设置 ForwardedHost 时攻击者可构造 Host: evil.com 注入开放重定向（P1-15）。
        // 仅在开发环境且 PublicBaseUrl 未配置时回退到 Request.Host，便于本地调试。
        if (string.IsNullOrWhiteSpace(redirectUri))
        {
            var configuredBase = _oauth2Options.PublicBaseUrl;
            if (!string.IsNullOrWhiteSpace(configuredBase))
            {
                redirectUri = $"{configuredBase.TrimEnd('/')}/api/auth/oauth/{provider}/callback";
            }
            else
            {
                if (!_environment.IsDevelopment())
                {
                    throw new InvalidOperationException(
                        "OAuth2:PublicBaseUrl 配置缺失，生产环境禁止使用 Request.Host 构造回调 URL（Host Header 注入风险，参见 P1-15）");
                }

                redirectUri = $"{Request.Scheme}://{Request.Host}/api/auth/oauth/{provider}/callback";
            }
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
