using Leno.Identity.Application;
using Leno.Identity.Application.DTOs;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Leno.Identity.Api.Controllers;

/// <summary>
/// 认证控制器（Identity BC，3.6 AuthN/AuthZ 拆分）。
/// 暴露登录、刷新与登出 REST 端点。
/// 角色填充由 <c>JwtTokenService</c> 通过 AccessControl BC <c>GetUserRoles</c> RPC 完成。
/// </summary>
[ApiController]
[Route("api/auth")]
public sealed class AuthController : ControllerBase
{
    private readonly IAuthenticationAppService _authService;
    private readonly ILogger<AuthController> _logger;

    public AuthController(
        IAuthenticationAppService authService,
        ILogger<AuthController> logger)
    {
        _authService = authService ?? throw new ArgumentNullException(nameof(authService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// 账号密码登录。
    /// </summary>
    /// <param name="dto">登录请求。</param>
    /// <param name="ct">取消令牌。</param>
    /// <response code="200">登录成功，返回访问与刷新令牌。</response>
    /// <response code="400">请求参数无效。</response>
    /// <response code="401">用户名或密码错误。</response>
    [HttpPost("login")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(TokenDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<TokenDto>> LoginAsync(
        [FromBody] LoginDto dto,
        CancellationToken ct)
    {
        var token = await _authService.LoginAsync(dto, ct).ConfigureAwait(false);
        return Ok(token);
    }

    /// <summary>
    /// 刷新令牌轮换。
    /// </summary>
    /// <param name="dto">刷新请求。</param>
    /// <param name="ct">取消令牌。</param>
    /// <response code="200">刷新成功，返回新访问与新刷新令牌。</response>
    /// <response code="400">请求参数无效。</response>
    /// <response code="401">刷新令牌无效或已过期。</response>
    [HttpPost("refresh")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(TokenDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<TokenDto>> RefreshAsync(
        [FromBody] RefreshTokenDto dto,
        CancellationToken ct)
    {
        var token = await _authService.RefreshAsync(dto, ct).ConfigureAwait(false);
        return Ok(token);
    }

    /// <summary>
    /// 登出，吊销当前用户的所有活跃刷新令牌。
    /// 需鉴权，<c>sub</c> claim 中携带的 UserId 用于定位需吊销的令牌。
    /// </summary>
    /// <param name="ct">取消令牌。</param>
    /// <response code="204">登出成功。</response>
    /// <response code="401">未鉴权。</response>
    [HttpPost("logout")]
    [Authorize]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> LogoutAsync(CancellationToken ct)
    {
        var userIdClaim = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value
                          ?? User.FindFirst("sub")?.Value;
        if (string.IsNullOrWhiteSpace(userIdClaim) || !Guid.TryParse(userIdClaim, out var userId))
        {
            _logger.LogWarning("登出请求缺少有效的 NameIdentifier/sub claim");
            return Unauthorized();
        }

        await _authService.LogoutAsync(userId, ct).ConfigureAwait(false);
        return NoContent();
    }
}
