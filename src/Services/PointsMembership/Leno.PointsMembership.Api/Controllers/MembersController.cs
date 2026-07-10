using Leno.Infrastructure.Auth;
using Leno.PointsMembership.Application;
using Leno.PointsMembership.Application.DTOs;
using Leno.SharedContracts.Responses;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Leno.PointsMembership.Api.Controllers;

/// <summary>
/// 会员控制器。
/// 买家端（/api/members）：查询当前会员信息，需 Buyer 角色。
/// 运营端（/api/admin/members/levels）：会员等级 CRUD 与启停，需 Operator/Admin 角色。
/// </summary>
[ApiController]
public sealed class MembersController : PointsMembershipControllerBase
{
    private readonly IMemberAppService _memberAppService;

    public MembersController(ICurrentUserContext currentUser, IMemberAppService memberAppService)
        : base(currentUser)
    {
        ArgumentNullException.ThrowIfNull(memberAppService);
        _memberAppService = memberAppService;
    }

    // ========== 买家端 ==========

    /// <summary>查询当前用户会员信息。</summary>
    [Authorize(Roles = "Buyer")]
    [HttpGet("api/members/me")]
    [ProducesResponseType(typeof(ApiResponse<MemberDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetMyMemberInfoAsync(CancellationToken ct)
    {
        var member = await _memberAppService.GetMemberInfoAsync(GetCurrentUserId(), ct);
        return Ok(ApiResponse.Success(member));
    }

    // ========== 运营端 ==========

    /// <summary>查询全部会员等级（按等级编号升序）。</summary>
    [Authorize(Roles = "Operator,Admin")]
    [HttpGet("api/admin/members/levels")]
    [ProducesResponseType(typeof(ApiResponse<List<MembershipLevelDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetLevelsAsync(CancellationToken ct)
    {
        var levels = await _memberAppService.GetLevelsAsync(ct);
        return Ok(ApiResponse.Success(levels));
    }

    /// <summary>创建会员等级。</summary>
    [Authorize(Roles = "Operator,Admin")]
    [HttpPost("api/admin/members/levels")]
    [ProducesResponseType(typeof(ApiResponse<MembershipLevelDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> CreateLevelAsync([FromBody] CreateMembershipLevelDto dto, CancellationToken ct)
    {
        var level = await _memberAppService.CreateLevelAsync(dto, ct);
        return Ok(ApiResponse.Success(level));
    }

    /// <summary>更新会员等级（名称、门槛、折扣率）。</summary>
    [Authorize(Roles = "Operator,Admin")]
    [HttpPut("api/admin/members/levels/{levelId:guid}")]
    [ProducesResponseType(typeof(ApiResponse<MembershipLevelDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> UpdateLevelAsync(Guid levelId, [FromBody] UpdateMembershipLevelDto dto, CancellationToken ct)
    {
        var level = await _memberAppService.UpdateLevelAsync(levelId, dto, ct);
        return Ok(ApiResponse.Success(level));
    }

    /// <summary>启用会员等级。</summary>
    [Authorize(Roles = "Operator,Admin")]
    [HttpPost("api/admin/members/levels/{levelId:guid}/enable")]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> EnableLevelAsync(Guid levelId, CancellationToken ct)
    {
        await _memberAppService.EnableLevelAsync(levelId, ct);
        return Ok(ApiResponse.Success());
    }

    /// <summary>停用会员等级。</summary>
    [Authorize(Roles = "Operator,Admin")]
    [HttpPost("api/admin/members/levels/{levelId:guid}/disable")]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> DisableLevelAsync(Guid levelId, CancellationToken ct)
    {
        await _memberAppService.DisableLevelAsync(levelId, ct);
        return Ok(ApiResponse.Success());
    }
}
