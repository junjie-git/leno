using Leno.Identity.Application;
using Leno.Identity.Application.DTOs;
using Leno.Infrastructure.Auth;
using Leno.SharedContracts.Responses;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Leno.Identity.Api.Controllers;

/// <summary>
/// 当前用户资料控制器（Identity BC，Task A3 新建 6 端点）。
/// 提供个人资料查询、修改与密码修改、双因子认证启用/确认/禁用端点。
/// <para>
/// 全部端点需认证；类级路由 <c>api/users/me</c>；UserId 从 <see cref="ICurrentUserContext"/> 取，禁止客户端传 userId 参数。
/// </para>
/// <para>
/// 统一使用 <see cref="ApiResponse{T}"/> 包装响应；POST 启停返回 200 OK（不用 201/204）。
/// </para>
/// </summary>
[ApiController]
[Authorize]
[Route("api/users/me")]
public sealed class UsersController : ControllerBase
{
    private readonly ICurrentUserContext _currentUser;
    private readonly IUserProfileAppService _userProfileAppService;
    private readonly ITwoFactorService _twoFactorService;

    public UsersController(
        ICurrentUserContext currentUser,
        IUserProfileAppService userProfileAppService,
        ITwoFactorService twoFactorService)
    {
        _currentUser = currentUser ?? throw new ArgumentNullException(nameof(currentUser));
        _userProfileAppService = userProfileAppService ?? throw new ArgumentNullException(nameof(userProfileAppService));
        _twoFactorService = twoFactorService ?? throw new ArgumentNullException(nameof(twoFactorService));
    }

    /// <summary>查询当前用户资料。</summary>
    /// <param name="ct">取消令牌。</param>
    /// <response code="200">返回当前用户资料。</response>
    /// <response code="401">未鉴权。</response>
    [HttpGet]
    [ProducesResponseType(typeof(ApiResponse<UserDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> GetProfileAsync(CancellationToken ct)
    {
        var userId = GetCurrentUserId();
        if (userId is null)
        {
            return Unauthorized();
        }

        var profile = await _userProfileAppService.GetProfileAsync(userId.Value, ct).ConfigureAwait(false);
        return Ok(ApiResponse.Success(profile));
    }

    /// <summary>修改当前用户资料（昵称、头像）。</summary>
    /// <param name="dto">更新请求。</param>
    /// <param name="ct">取消令牌。</param>
    /// <response code="200">返回更新后的用户资料。</response>
    /// <response code="401">未鉴权。</response>
    [HttpPut]
    [ProducesResponseType(typeof(ApiResponse<UserDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> UpdateProfileAsync([FromBody] UpdateProfileDto dto, CancellationToken ct)
    {
        var userId = GetCurrentUserId();
        if (userId is null)
        {
            return Unauthorized();
        }

        var profile = await _userProfileAppService.UpdateProfileAsync(userId.Value, dto, ct).ConfigureAwait(false);
        return Ok(ApiResponse.Success(profile));
    }

    /// <summary>修改当前用户密码。成功后撤销该用户所有刷新令牌，强制重新登录。</summary>
    /// <param name="dto">修改密码请求（含旧密码与新密码）。</param>
    /// <param name="ct">取消令牌。</param>
    /// <response code="200">密码修改成功。</response>
    /// <response code="401">未鉴权。</response>
    /// <response code="400">旧密码错误或新密码不符合复杂度要求。</response>
    [HttpPut("password")]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> ChangePasswordAsync([FromBody] ChangePasswordDto dto, CancellationToken ct)
    {
        var userId = GetCurrentUserId();
        if (userId is null)
        {
            return Unauthorized();
        }

        await _userProfileAppService.ChangePasswordAsync(userId.Value, dto, ct).ConfigureAwait(false);
        return Ok(ApiResponse.Success("密码修改成功"));
    }

    /// <summary>启用双因子认证：生成 TOTP 密钥与 QR 码 URI。</summary>
    /// <param name="ct">取消令牌。</param>
    /// <response code="200">返回 Base32 密钥与 QR 码 URI。</response>
    /// <response code="401">未鉴权。</response>
    [HttpPost("two-factor/enable")]
    [ProducesResponseType(typeof(ApiResponse<TwoFactorEnableResponseDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> EnableTwoFactorAsync(CancellationToken ct)
    {
        var userId = GetCurrentUserId();
        if (userId is null)
        {
            return Unauthorized();
        }

        var result = await _twoFactorService.EnableTwoFactorAsync(userId.Value, ct).ConfigureAwait(false);
        return Ok(ApiResponse.Success(result));
    }

    /// <summary>确认双因子认证：验证 TOTP 码后正式启用。</summary>
    /// <param name="dto">确认请求，含 6 位 TOTP 码。</param>
    /// <param name="ct">取消令牌。</param>
    /// <response code="200">双因子认证已启用。</response>
    /// <response code="401">未鉴权。</response>
    /// <response code="400">验证码错误。</response>
    [HttpPost("two-factor/confirm")]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> ConfirmTwoFactorAsync([FromBody] TwoFactorConfirmDto dto, CancellationToken ct)
    {
        var userId = GetCurrentUserId();
        if (userId is null)
        {
            return Unauthorized();
        }

        await _twoFactorService.ConfirmTwoFactorAsync(userId.Value, dto.Code, ct).ConfigureAwait(false);
        return Ok(ApiResponse.Success("双因子认证已启用"));
    }

    /// <summary>禁用双因子认证，清除密钥与启用状态。</summary>
    /// <param name="ct">取消令牌。</param>
    /// <response code="200">双因子认证已禁用。</response>
    /// <response code="401">未鉴权。</response>
    [HttpPost("two-factor/disable")]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> DisableTwoFactorAsync(CancellationToken ct)
    {
        var userId = GetCurrentUserId();
        if (userId is null)
        {
            return Unauthorized();
        }

        await _twoFactorService.DisableTwoFactorAsync(userId.Value, ct).ConfigureAwait(false);
        return Ok(ApiResponse.Success("双因子认证已禁用"));
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
