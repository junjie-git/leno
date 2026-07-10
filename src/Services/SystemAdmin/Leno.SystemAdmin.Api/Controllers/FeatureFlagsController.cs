using Leno.Infrastructure.Auth;
using Leno.SystemAdmin.Application;
using Leno.SystemAdmin.Application.DTOs;
using Leno.SystemAdmin.Domain.ValueObjects;
using Leno.SharedContracts.Responses;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Leno.SystemAdmin.Api.Controllers;

/// <summary>
/// 特性开关管理控制器（运营端 CRUD、启停与按上下文评估）。
/// </summary>
[Authorize(Roles = "Operator,Admin")]
[ApiController]
public sealed class FeatureFlagsController : SystemAdminControllerBase
{
    private readonly IFeatureFlagAppService _featureFlagAppService;

    public FeatureFlagsController(ICurrentUserContext currentUser, IFeatureFlagAppService featureFlagAppService)
        : base(currentUser)
    {
        ArgumentNullException.ThrowIfNull(featureFlagAppService);
        _featureFlagAppService = featureFlagAppService;
    }

    /// <summary>分页查询特性开关，支持键与状态过滤。</summary>
    [HttpGet("api/admin/feature-flags")]
    [ProducesResponseType(typeof(ApiResponse<FeatureFlagListResultDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> QueryAsync(
        [FromQuery] string? key,
        [FromQuery] FeatureFlagStatus? status,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken ct = default)
    {
        var result = await _featureFlagAppService.QueryAsync(key, status, page, pageSize, ct);
        return Ok(ApiResponse.Success(result));
    }

    /// <summary>创建特性开关。</summary>
    [HttpPost("api/admin/feature-flags")]
    [ProducesResponseType(typeof(ApiResponse<FeatureFlagDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> CreateAsync([FromBody] SaveFeatureFlagDto dto, CancellationToken ct)
    {
        var result = await _featureFlagAppService.CreateAsync(dto, ct);
        return Ok(ApiResponse.Success(result));
    }

    /// <summary>更新特性开关（键不可变）。</summary>
    [HttpPut("api/admin/feature-flags/{flagId:guid}")]
    [ProducesResponseType(typeof(ApiResponse<FeatureFlagDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> UpdateAsync(Guid flagId, [FromBody] UpdateFeatureFlagDto dto, CancellationToken ct)
    {
        var result = await _featureFlagAppService.UpdateAsync(flagId, dto, ct);
        return Ok(ApiResponse.Success(result));
    }

    /// <summary>启用开关。</summary>
    [HttpPost("api/admin/feature-flags/{flagId:guid}/enable")]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> EnableAsync(Guid flagId, CancellationToken ct)
    {
        await _featureFlagAppService.EnableAsync(flagId, ct);
        return Ok(ApiResponse.Success());
    }

    /// <summary>停用开关。</summary>
    [HttpPost("api/admin/feature-flags/{flagId:guid}/disable")]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> DisableAsync(Guid flagId, CancellationToken ct)
    {
        await _featureFlagAppService.DisableAsync(flagId, ct);
        return Ok(ApiResponse.Success());
    }

    /// <summary>按上下文评估开关是否生效。</summary>
    [HttpPost("api/admin/feature-flags/evaluate")]
    [ProducesResponseType(typeof(ApiResponse<bool>), StatusCodes.Status200OK)]
    public async Task<IActionResult> EvaluateAsync([FromBody] EvaluateFlagDto dto, CancellationToken ct)
    {
        var result = await _featureFlagAppService.EvaluateAsync(dto, ct);
        return Ok(ApiResponse.Success(result));
    }
}
