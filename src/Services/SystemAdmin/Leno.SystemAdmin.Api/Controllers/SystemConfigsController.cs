using Leno.Infrastructure.Auth;
using Leno.SystemAdmin.Application;
using Leno.SystemAdmin.Application.DTOs;
using Leno.SystemAdmin.Domain.ValueObjects;
using Leno.SharedContracts.Responses;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Leno.SystemAdmin.Api.Controllers;

/// <summary>
/// 系统配置管理控制器（运营端 CRUD、启停、按键与按分组查询）。
/// </summary>
[Authorize(Roles = "Operator,Admin")]
[ApiController]
public sealed class SystemConfigsController : SystemAdminControllerBase
{
    private readonly ISystemConfigAppService _configAppService;

    public SystemConfigsController(ICurrentUserContext currentUser, ISystemConfigAppService configAppService)
        : base(currentUser)
    {
        ArgumentNullException.ThrowIfNull(configAppService);
        _configAppService = configAppService;
    }

    /// <summary>分页查询系统配置，支持键、分组、状态过滤。</summary>
    [HttpGet("api/admin/system-configs")]
    [ProducesResponseType(typeof(ApiResponse<SystemConfigListResultDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> QueryAsync(
        [FromQuery] string? key,
        [FromQuery] string? group,
        [FromQuery] ConfigStatus? status,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken ct = default)
    {
        var result = await _configAppService.QueryAsync(key, group, status, page, pageSize, ct);
        return Ok(ApiResponse.Success(result));
    }

    /// <summary>获取全部配置分组（去重），SQL 层 SELECT DISTINCT Group。</summary>
    [HttpGet("api/admin/system-configs/groups")]
    [ProducesResponseType(typeof(ApiResponse<List<string>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetGroupsAsync(CancellationToken ct)
    {
        var groups = await _configAppService.GetDistinctGroupsAsync(ct);
        return Ok(ApiResponse.Success(groups));
    }

    /// <summary>创建系统配置。</summary>
    [HttpPost("api/admin/system-configs")]
    [ProducesResponseType(typeof(ApiResponse<SystemConfigDto>), StatusCodes.Status201Created)]
    public async Task<IActionResult> CreateAsync([FromBody] SaveSystemConfigDto dto, CancellationToken ct)
    {
        var result = await _configAppService.CreateAsync(dto, ct);
        return CreatedAtAction(nameof(GetByKeyAsync), new { key = result.Key }, ApiResponse.Success(result));
    }

    /// <summary>更新系统配置（键不可变）。</summary>
    [HttpPut("api/admin/system-configs/{configId:guid}")]
    [ProducesResponseType(typeof(ApiResponse<SystemConfigDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> UpdateAsync(Guid configId, [FromBody] UpdateSystemConfigDto dto, CancellationToken ct)
    {
        var result = await _configAppService.UpdateAsync(configId, dto, ct);
        return Ok(ApiResponse.Success(result));
    }

    /// <summary>启用配置。</summary>
    [HttpPost("api/admin/system-configs/{configId:guid}/enable")]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> EnableAsync(Guid configId, CancellationToken ct)
    {
        await _configAppService.EnableAsync(configId, ct);
        return Ok(ApiResponse.Success());
    }

    /// <summary>停用配置。</summary>
    [HttpPost("api/admin/system-configs/{configId:guid}/disable")]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> DisableAsync(Guid configId, CancellationToken ct)
    {
        await _configAppService.DisableAsync(configId, ct);
        return Ok(ApiResponse.Success());
    }

    /// <summary>按键获取配置（加密配置值将被掩码）。</summary>
    [HttpGet("api/admin/system-configs/by-key/{key}")]
    [ProducesResponseType(typeof(ApiResponse<SystemConfigDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetByKeyAsync(string key, CancellationToken ct)
    {
        var result = await _configAppService.GetByKeyAsync(key, ct);
        return Ok(ApiResponse.Success(result));
    }
}
