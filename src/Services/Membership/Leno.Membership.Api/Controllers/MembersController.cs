using Leno.Infrastructure.Auth;
using Leno.Membership.Application;
using Leno.Membership.Application.DTOs;
using Leno.SharedContracts.Responses;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Leno.Membership.Api.Controllers;

/// <summary>
/// 会员信息控制器（买家端）。
/// 路由 /api/members/*，需 Buyer 角色。
/// 对应 design-prompts operations/08-membership-ops/members.md 的 1 个买家端端点：
/// 查询当前用户会员档案。
/// 返工：从 GET api/members/{userId} 改为 GET api/members/me，userId 从 JWT 解析，禁止客户端传 userId。
/// </summary>
[ApiController]
[Route("api/members")]
[Authorize(Roles = "Buyer")]
public sealed class MembersController : ControllerBase
{
    private readonly IMemberAppService _memberAppService;
    private readonly ICurrentUserContext _currentUser;

    public MembersController(
        IMemberAppService memberAppService,
        ICurrentUserContext currentUser)
    {
        ArgumentNullException.ThrowIfNull(memberAppService);
        ArgumentNullException.ThrowIfNull(currentUser);
        _memberAppService = memberAppService;
        _currentUser = currentUser;
    }

    /// <summary>查询当前用户会员档案，userId 从 JWT 解析。</summary>
    [HttpGet("me")]
    [ProducesResponseType(typeof(ApiResponse<MemberDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetMyMemberInfoAsync(CancellationToken ct)
    {
        var result = await _memberAppService.GetMemberInfoAsync(GetCurrentUserId(), ct);
        return Ok(ApiResponse.Success(result));
    }

    private Guid GetCurrentUserId()
    {
        if (!_currentUser.IsAuthenticated || !_currentUser.UserId.HasValue)
        {
            throw new UnauthorizedAccessException("未认证");
        }

        return _currentUser.UserId.Value;
    }
}
