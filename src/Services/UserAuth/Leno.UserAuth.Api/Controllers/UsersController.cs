using Leno.Infrastructure.Auth;
using Leno.SharedContracts.Responses;
using Leno.UserAuth.Application;
using Leno.UserAuth.Application.DTOs;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Leno.UserAuth.Api.Controllers;

/// <summary>
/// 当前用户资料控制器，提供个人资料查询、修改与密码修改端点。
/// 全部端点需认证。
/// </summary>
[Authorize]
[ApiController]
[Route("api/users/me")]
public sealed class UsersController : UserAuthControllerBase
{
    private readonly IUserAppService _userAppService;

    public UsersController(ICurrentUserContext currentUser, IUserAppService userAppService)
        : base(currentUser)
    {
        ArgumentNullException.ThrowIfNull(userAppService);
        _userAppService = userAppService;
    }

    /// <summary>查询当前用户资料。</summary>
    [HttpGet]
    [ProducesResponseType(typeof(ApiResponse<UserDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetProfileAsync(CancellationToken ct)
    {
        var profile = await _userAppService.GetProfileAsync(GetCurrentUserId(), ct);
        return Ok(ApiResponse.Success(profile));
    }

    /// <summary>修改当前用户资料。</summary>
    [HttpPut]
    [ProducesResponseType(typeof(ApiResponse<UserDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> UpdateProfileAsync([FromBody] UpdateProfileDto dto, CancellationToken ct)
    {
        var profile = await _userAppService.UpdateProfileAsync(GetCurrentUserId(), dto, ct);
        return Ok(ApiResponse.Success(profile));
    }

    /// <summary>修改当前用户密码。</summary>
    [HttpPut("password")]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> ChangePasswordAsync([FromBody] ChangePasswordDto dto, CancellationToken ct)
    {
        await _userAppService.ChangePasswordAsync(GetCurrentUserId(), dto, ct);
        return Ok(ApiResponse.Success("密码修改成功"));
    }

    /// <summary>启用双因子认证：生成密钥与 QR 码 URI。</summary>
    [HttpPost("two-factor/enable")]
    [ProducesResponseType(typeof(ApiResponse<TwoFactorEnableResponseDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> EnableTwoFactorAsync(CancellationToken ct)
    {
        var result = await _userAppService.EnableTwoFactorAsync(GetCurrentUserId(), ct);
        return Ok(ApiResponse.Success(result));
    }

    /// <summary>确认双因子认证：验证 TOTP 码。</summary>
    [HttpPost("two-factor/confirm")]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> ConfirmTwoFactorAsync([FromBody] TwoFactorConfirmDto dto, CancellationToken ct)
    {
        await _userAppService.ConfirmTwoFactorAsync(GetCurrentUserId(), dto, ct);
        return Ok(ApiResponse.Success("双因子认证已启用"));
    }

    /// <summary>禁用双因子认证。</summary>
    [HttpPost("two-factor/disable")]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> DisableTwoFactorAsync(CancellationToken ct)
    {
        await _userAppService.DisableTwoFactorAsync(GetCurrentUserId(), ct);
        return Ok(ApiResponse.Success("双因子认证已禁用"));
    }
}
