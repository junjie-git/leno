using Leno.Identity.Application;
using Leno.Identity.Application.DTOs;
using Leno.Infrastructure.Auth;
using Leno.SharedContracts.Responses;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Leno.Identity.Api.Controllers;

/// <summary>
/// 认证控制器（Identity BC，Task A3 返工 + 新建 9 端点）。
/// <para>
/// 暴露注册、登录、刷新令牌、登出、OAuth2 第三方登录、双因子验证、忘记/重置密码 REST 端点。
/// 匿名端点：register / login / refresh-token / oauth / forgot-password / reset-password。
/// 鉴权端点：logout / two-factor/verify。
/// </para>
/// <para>
/// 统一使用 <see cref="ApiResponse{T}"/> 包装响应；POST 创建/启停返回 200 OK（不用 201/204）；
/// 当前用户标识从 <see cref="ICurrentUserContext"/> 取，禁止从 Claim 手动解析。
/// </para>
/// </summary>
[ApiController]
[Route("api/auth")]
public sealed class AuthController : ControllerBase
{
    private readonly IAuthAppService _authAppService;
    private readonly IOAuthService _oauthService;
    private readonly ITwoFactorService _twoFactorService;
    private readonly IPasswordService _passwordService;
    private readonly ICurrentUserContext _currentUser;
    private readonly ILogger<AuthController> _logger;

    public AuthController(
        IAuthAppService authAppService,
        IOAuthService oauthService,
        ITwoFactorService twoFactorService,
        IPasswordService passwordService,
        ICurrentUserContext currentUser,
        ILogger<AuthController> logger)
    {
        _authAppService = authAppService ?? throw new ArgumentNullException(nameof(authAppService));
        _oauthService = oauthService ?? throw new ArgumentNullException(nameof(oauthService));
        _twoFactorService = twoFactorService ?? throw new ArgumentNullException(nameof(twoFactorService));
        _passwordService = passwordService ?? throw new ArgumentNullException(nameof(passwordService));
        _currentUser = currentUser ?? throw new ArgumentNullException(nameof(currentUser));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>注册账户并签发令牌。</summary>
    /// <param name="dto">注册请求。</param>
    /// <param name="ct">取消令牌。</param>
    /// <response code="200">注册成功，返回访问与刷新令牌。</response>
    /// <response code="400">请求参数无效或用户名/邮箱/手机号已存在。</response>
    [HttpPost("register")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(ApiResponse<TokenDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> RegisterAsync([FromBody] RegisterDto dto, CancellationToken ct)
    {
        var token = await _authAppService.RegisterAsync(dto, ct).ConfigureAwait(false);
        return Ok(ApiResponse.Success(token));
    }

    /// <summary>账号密码登录并签发令牌。</summary>
    /// <param name="dto">登录请求。</param>
    /// <param name="ct">取消令牌。</param>
    /// <response code="200">登录成功，返回访问与刷新令牌。</response>
    /// <response code="400">请求参数无效。</response>
    /// <response code="401">用户名或密码错误。</response>
    [HttpPost("login")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(ApiResponse<TokenDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> LoginAsync([FromBody] LoginDto dto, CancellationToken ct)
    {
        var token = await _authAppService.LoginAsync(dto, ct).ConfigureAwait(false);
        return Ok(ApiResponse.Success(token));
    }

    /// <summary>使用刷新令牌换取新的访问与刷新令牌。</summary>
    /// <param name="dto">刷新请求。</param>
    /// <param name="ct">取消令牌。</param>
    /// <response code="200">刷新成功，返回新访问与新刷新令牌。</response>
    /// <response code="400">请求参数无效。</response>
    /// <response code="401">刷新令牌无效或已过期。</response>
    [HttpPost("refresh-token")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(ApiResponse<TokenDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> RefreshTokenAsync([FromBody] RefreshTokenDto dto, CancellationToken ct)
    {
        var token = await _authAppService.RefreshTokenAsync(dto, ct).ConfigureAwait(false);
        return Ok(ApiResponse.Success(token));
    }

    /// <summary>
    /// 登出，吊销当前用户的所有活跃刷新令牌。
    /// 需鉴权，UserId 从 <see cref="ICurrentUserContext"/> 取。
    /// </summary>
    /// <param name="ct">取消令牌。</param>
    /// <response code="200">登出成功。</response>
    /// <response code="401">未鉴权。</response>
    [HttpPost("logout")]
    [Authorize]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> LogoutAsync(CancellationToken ct)
    {
        if (!_currentUser.IsAuthenticated || !_currentUser.UserId.HasValue)
        {
            _logger.LogWarning("登出请求缺少有效的用户标识");
            return Unauthorized();
        }

        await _authAppService.LogoutAsync(_currentUser.UserId.Value, ct).ConfigureAwait(false);
        return Ok(ApiResponse.Success());
    }

    /// <summary>
    /// OAuth2 第三方登录入口，生成授权 URL 并返回。
    /// 前端收到后跳转至第三方授权页面。
    /// </summary>
    /// <param name="provider">OAuth2 提供方标识（如 google / wechat）。</param>
    /// <param name="redirectUri">回调完成后跳转的业务地址。</param>
    /// <param name="ct">取消令牌。</param>
    /// <response code="200">返回授权 URL。</response>
    /// <response code="400">redirectUri 不可为空或提供方未配置。</response>
    [HttpGet("oauth/{provider}/login")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> OAuthLoginAsync(
        [FromRoute] string provider,
        [FromQuery] string? redirectUri,
        CancellationToken ct)
    {
        var url = await _oauthService.GetLoginUrlAsync(provider, redirectUri, ct).ConfigureAwait(false);
        return Ok(ApiResponse.Success(new { AuthorizationUrl = url }));
    }

    /// <summary>
    /// OAuth2 回调端点，第三方授权后前端将 code 与 state 提交至此完成登录/注册。
    /// </summary>
    /// <param name="provider">OAuth2 提供方标识。</param>
    /// <param name="code">回调返回的授权码。</param>
    /// <param name="state">CSRF 防护 state 参数。</param>
    /// <param name="ct">取消令牌。</param>
    /// <response code="200">登录/注册成功，返回访问与刷新令牌。</response>
    /// <response code="400">code 或 state 不可为空。</response>
    /// <response code="401">state 无效或已过期。</response>
    [HttpGet("oauth/{provider}/callback")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(ApiResponse<TokenDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> OAuthCallbackAsync(
        [FromRoute] string provider,
        [FromQuery] string code,
        [FromQuery] string? state,
        CancellationToken ct)
    {
        var token = await _oauthService.HandleCallbackAsync(provider, code, state, ct).ConfigureAwait(false);
        return Ok(ApiResponse.Success(token));
    }

    /// <summary>
    /// 双因子认证二次验证（登录流程），验证 TOTP 码。
    /// 需鉴权，UserId 从 <see cref="ICurrentUserContext"/> 取。
    /// </summary>
    /// <param name="dto">验证请求，含 6 位 TOTP 码。</param>
    /// <param name="ct">取消令牌。</param>
    /// <response code="200">验证结果（true=通过）。</response>
    /// <response code="401">未鉴权。</response>
    [HttpPost("two-factor/verify")]
    [Authorize]
    [ProducesResponseType(typeof(ApiResponse<bool>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> VerifyTwoFactorAsync([FromBody] TwoFactorVerifyDto dto, CancellationToken ct)
    {
        if (!_currentUser.IsAuthenticated || !_currentUser.UserId.HasValue)
        {
            _logger.LogWarning("双因子验证请求缺少有效的用户标识");
            return Unauthorized();
        }

        var result = await _twoFactorService.VerifyAsync(_currentUser.UserId.Value, dto.Code, ct).ConfigureAwait(false);
        return Ok(ApiResponse.Success(result));
    }

    /// <summary>忘记密码：根据账号（邮箱或手机号）发送重置链接/验证码。</summary>
    /// <param name="dto">忘记密码请求，含账号。</param>
    /// <param name="ct">取消令牌。</param>
    /// <response code="200">无论账号是否存在均返回相同消息，防止账号枚举。</response>
    [HttpPost("forgot-password")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> ForgotPasswordAsync([FromBody] ForgotPasswordDto dto, CancellationToken ct)
    {
        await _passwordService.ForgotPasswordAsync(dto.Account, ct).ConfigureAwait(false);
        return Ok(ApiResponse.Success("若账号存在，重置链接已发送"));
    }

    /// <summary>重置密码：校验重置令牌并设置新密码。</summary>
    /// <param name="dto">重置密码请求，含令牌与新密码。</param>
    /// <param name="ct">取消令牌。</param>
    /// <response code="200">密码重置成功。</response>
    /// <response code="400">重置令牌无效或已过期。</response>
    [HttpPost("reset-password")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> ResetPasswordAsync([FromBody] ResetPasswordDto dto, CancellationToken ct)
    {
        await _passwordService.ResetPasswordAsync(dto, ct).ConfigureAwait(false);
        return Ok(ApiResponse.Success("密码重置成功"));
    }
}
