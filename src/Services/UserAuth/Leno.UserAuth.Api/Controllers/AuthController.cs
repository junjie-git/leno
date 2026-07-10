using Leno.SharedContracts.Responses;
using Leno.UserAuth.Application;
using Leno.UserAuth.Application.DTOs;
using Microsoft.AspNetCore.Mvc;

namespace Leno.UserAuth.Api.Controllers;

/// <summary>
/// 认证控制器，提供账号注册、登录与刷新令牌端点。
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
}
