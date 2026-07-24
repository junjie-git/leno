using Leno.Membership.Application;
using Leno.Membership.Application.DTOs;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Leno.Membership.Api.Controllers;

/// <summary>
/// 会员信息查询与运营端等级定义管理接口。
/// </summary>
[ApiController]
[Route("api/[controller]")]
public sealed class MembersController : ControllerBase
{
    private readonly IMemberAppService _memberAppService;

    public MembersController(IMemberAppService memberAppService)
    {
        _memberAppService = memberAppService;
    }

    /// <summary>
    /// 获取当前用户会员档案。
    /// </summary>
    /// <param name="userId">用户标识。</param>
    /// <param name="ct">取消令牌。</param>
    [HttpGet("{userId:guid}")]
    [Authorize]
    public async Task<ActionResult<MemberDto>> GetMemberInfo(Guid userId, CancellationToken ct)
        => Ok(await _memberAppService.GetMemberInfoAsync(userId, ct));

    /// <summary>
    /// 获取全部会员等级定义，按成长值门槛升序。
    /// </summary>
    [HttpGet("levels")]
    public async Task<ActionResult<List<MemberLevelDefinitionDto>>> GetLevels(CancellationToken ct)
        => Ok(await _memberAppService.GetLevelsAsync(ct));

    /// <summary>
    /// 创建会员等级定义（运营端）。
    /// </summary>
    [HttpPost("levels")]
    [Authorize(Policy = "AdminOnly")]
    public async Task<ActionResult<MemberLevelDefinitionDto>> CreateLevel(
        [FromBody] CreateMemberLevelDefinitionDto dto, CancellationToken ct)
    {
        var level = await _memberAppService.CreateLevelAsync(dto, ct);
        return CreatedAtAction(nameof(GetLevels), new { }, level);
    }

    /// <summary>
    /// 更新会员等级定义（运营端，等级编号不可改）。
    /// </summary>
    [HttpPut("levels/{levelId:guid}")]
    [Authorize(Policy = "AdminOnly")]
    public async Task<ActionResult<MemberLevelDefinitionDto>> UpdateLevel(
        Guid levelId, [FromBody] UpdateMemberLevelDefinitionDto dto, CancellationToken ct)
        => Ok(await _memberAppService.UpdateLevelAsync(levelId, dto, ct));
}
