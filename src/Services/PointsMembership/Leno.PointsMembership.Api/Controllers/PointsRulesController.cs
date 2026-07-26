using Leno.Infrastructure.Auth;
using Leno.PointsMembership.Application;
using Leno.PointsMembership.Application.DTOs;
using Leno.SharedContracts.Responses;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Leno.PointsMembership.Api.Controllers;

/// <summary>
/// 积分规则控制器（运营端）。
/// 路由 /api/admin/points/rules/*，需 Operator/Admin 角色。
/// 对应 design-prompts operations/08-membership-ops/points-rules.md 的 5 个规划端点。
/// </summary>
[ApiController]
public sealed class PointsRulesController : PointsMembershipControllerBase
{
    private readonly IPointsRuleAppService _ruleAppService;

    public PointsRulesController(
        ICurrentUserContext currentUser,
        IPointsRuleAppService ruleAppService)
        : base(currentUser)
    {
        ArgumentNullException.ThrowIfNull(ruleAppService);
        _ruleAppService = ruleAppService;
    }

    /// <summary>查询全部积分规则（含停用），按创建时间升序。</summary>
    [Authorize(Roles = "Operator,Admin")]
    [HttpGet("api/admin/points/rules")]
    [ProducesResponseType(typeof(ApiResponse<List<PointsRuleDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetRulesAsync(CancellationToken ct)
    {
        var rules = await _ruleAppService.GetRulesAsync(ct);
        return Ok(ApiResponse.Success(rules));
    }

    /// <summary>创建积分规则，编码唯一约束冲突返回 409。</summary>
    [Authorize(Roles = "Operator,Admin")]
    [HttpPost("api/admin/points/rules")]
    [ProducesResponseType(typeof(ApiResponse<PointsRuleDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> CreateRuleAsync([FromBody] CreatePointsRuleDto dto, CancellationToken ct)
    {
        var rule = await _ruleAppService.CreateRuleAsync(dto, ct);
        return Ok(ApiResponse.Success(rule));
    }

    /// <summary>更新积分规则（名称、行为类型、积分值、每日上限），支持正负积分值。</summary>
    [Authorize(Roles = "Operator,Admin")]
    [HttpPut("api/admin/points/rules/{ruleId:guid}")]
    [ProducesResponseType(typeof(ApiResponse<PointsRuleDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> UpdateRuleAsync(Guid ruleId, [FromBody] UpdatePointsRuleDto dto, CancellationToken ct)
    {
        var rule = await _ruleAppService.UpdateRuleAsync(ruleId, dto, ct);
        return Ok(ApiResponse.Success(rule));
    }

    /// <summary>启用积分规则，已启用返回 409。</summary>
    [Authorize(Roles = "Operator,Admin")]
    [HttpPost("api/admin/points/rules/{ruleId:guid}/enable")]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> EnableRuleAsync(Guid ruleId, CancellationToken ct)
    {
        await _ruleAppService.EnableRuleAsync(ruleId, ct);
        return Ok(ApiResponse.Success());
    }

    /// <summary>停用积分规则，已停用返回 409。</summary>
    [Authorize(Roles = "Operator,Admin")]
    [HttpPost("api/admin/points/rules/{ruleId:guid}/disable")]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> DisableRuleAsync(Guid ruleId, CancellationToken ct)
    {
        await _ruleAppService.DisableRuleAsync(ruleId, ct);
        return Ok(ApiResponse.Success());
    }
}
