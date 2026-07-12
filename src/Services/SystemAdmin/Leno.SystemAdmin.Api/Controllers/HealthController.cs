using Leno.Infrastructure.Auth;
using Leno.SystemAdmin.Application;
using Leno.SystemAdmin.Application.DTOs;
using Leno.SharedContracts.Responses;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Leno.SystemAdmin.Api.Controllers;

/// <summary>
/// 系统健康监控控制器，提供聚合健康状态与各模块健康详情查询。
/// </summary>
[ApiController]
[Route("api/admin/health")]
[Authorize(Roles = "Operator,Admin")]
public sealed class HealthController : SystemAdminControllerBase
{
    private readonly IHealthAppService _healthAppService;

    public HealthController(ICurrentUserContext currentUser, IHealthAppService healthAppService)
        : base(currentUser)
    {
        ArgumentNullException.ThrowIfNull(healthAppService);
        _healthAppService = healthAppService;
    }

    /// <summary>
    /// 获取聚合健康状态（整体状态 + 各模块健康详情）。
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(ApiResponse<HealthAggregationResultDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAggregatedHealthAsync(CancellationToken ct)
    {
        var result = await _healthAppService.GetAggregatedHealthAsync(ct);
        return Ok(ApiResponse.Success(result));
    }

    /// <summary>
    /// 获取各模块健康详情列表。
    /// </summary>
    [HttpGet("modules")]
    [ProducesResponseType(typeof(ApiResponse<List<ModuleHealthDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetModuleHealthDetailsAsync(CancellationToken ct)
    {
        var result = await _healthAppService.GetModuleHealthDetailsAsync(ct);
        return Ok(ApiResponse.Success(result));
    }
}