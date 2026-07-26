using Leno.Membership.Application;
using Leno.Membership.Application.DTOs;
using Leno.SharedContracts.Responses;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Leno.Membership.Api.Controllers;

/// <summary>
/// 会员等级定义运营控制器（运营端）。
/// 路由 /api/admin/members/levels/*，需 Operator/Admin 角色。
/// 对应 design-prompts operations/08-membership-ops/member-levels.md 的 5 个运营端端点：
/// 等级列表、创建等级、更新等级、启用等级（新建）、停用等级（新建）。
/// 返工：路径从 api/members/levels 改为 api/admin/members/levels，鉴权从 Policy AdminOnly 改角色 RBAC，
/// 响应统一 ApiResponse 包装，创建返回 200 OK（不用 201 CreatedAtAction）。
/// </summary>
[ApiController]
[Route("api/admin/members/levels")]
[Authorize(Roles = "Operator,Admin")]
public sealed class AdminMemberLevelsController : ControllerBase
{
    private readonly IMemberAppService _memberAppService;

    public AdminMemberLevelsController(IMemberAppService memberAppService)
    {
        ArgumentNullException.ThrowIfNull(memberAppService);
        _memberAppService = memberAppService;
    }

    /// <summary>查询全部会员等级定义，按成长值门槛升序。</summary>
    [HttpGet]
    [ProducesResponseType(typeof(ApiResponse<List<MemberLevelDefinitionDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> ListAsync(CancellationToken ct)
    {
        var levels = await _memberAppService.GetLevelsAsync(ct);
        return Ok(ApiResponse.Success(levels));
    }

    /// <summary>创建会员等级定义。</summary>
    [HttpPost]
    [ProducesResponseType(typeof(ApiResponse<MemberLevelDefinitionDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> CreateAsync([FromBody] CreateMemberLevelDefinitionDto dto, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(dto);
        var level = await _memberAppService.CreateLevelAsync(dto, ct);
        return Ok(ApiResponse.Success(level));
    }

    /// <summary>更新会员等级定义（等级编号不可改）。</summary>
    [HttpPut("{id:guid}")]
    [ProducesResponseType(typeof(ApiResponse<MemberLevelDefinitionDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> UpdateAsync([FromRoute] Guid id, [FromBody] UpdateMemberLevelDefinitionDto dto, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(dto);
        var level = await _memberAppService.UpdateLevelAsync(id, dto, ct);
        return Ok(ApiResponse.Success(level));
    }

    /// <summary>启用会员等级定义，已启用返回 409。</summary>
    [HttpPost("{id:guid}/enable")]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> EnableAsync([FromRoute] Guid id, CancellationToken ct)
    {
        await _memberAppService.EnableLevelAsync(id, ct);
        return Ok(ApiResponse.Success());
    }

    /// <summary>停用会员等级定义，停用后不参与等级评估，已停用返回 409。</summary>
    [HttpPost("{id:guid}/disable")]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> DisableAsync([FromRoute] Guid id, CancellationToken ct)
    {
        await _memberAppService.DisableLevelAsync(id, ct);
        return Ok(ApiResponse.Success());
    }
}
