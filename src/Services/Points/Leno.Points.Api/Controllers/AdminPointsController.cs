using Leno.Points.Application;
using Leno.Points.Application.DTOs;
using Leno.SharedContracts.Responses;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Leno.Points.Api.Controllers;

/// <summary>
/// 积分运营控制器（运营端）。
/// 路由 /api/admin/points/*，需 Operator/Admin 角色。
/// 对应 design-prompts operations/08-membership-ops/points.md 的运营手动发放积分端点。
/// </summary>
[ApiController]
[Route("api/admin/points")]
[Authorize(Roles = "Operator,Admin")]
public sealed class AdminPointsController : ControllerBase
{
    private readonly IAwardAppService _awardAppService;

    public AdminPointsController(IAwardAppService awardAppService)
    {
        ArgumentNullException.ThrowIfNull(awardAppService);
        _awardAppService = awardAppService;
    }

    /// <summary>运营手动发放积分。</summary>
    [HttpPost("award")]
    [ProducesResponseType(typeof(ApiResponse<AwardResultDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> AwardAsync([FromBody] AwardPointsDto dto, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(dto);
        var result = await _awardAppService.AwardAsync(dto.UserId, dto.Amount, dto.Reason, ct);
        return Ok(ApiResponse.Success(result));
    }
}
