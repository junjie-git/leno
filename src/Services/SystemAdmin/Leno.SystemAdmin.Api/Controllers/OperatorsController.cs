using Leno.Infrastructure.Auth;
using Leno.SystemAdmin.Application;
using Leno.SystemAdmin.Application.DTOs;
using Leno.SystemAdmin.Domain.ValueObjects;
using Leno.SharedContracts.Responses;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Leno.SystemAdmin.Api.Controllers;

/// <summary>
/// 运营人员管理控制器（运营端 CRUD、启停与权限分配）。
/// </summary>
[ApiController]
public sealed class OperatorsController : SystemAdminControllerBase
{
    private readonly IOperatorAppService _operatorAppService;

    public OperatorsController(ICurrentUserContext currentUser, IOperatorAppService operatorAppService)
        : base(currentUser)
    {
        ArgumentNullException.ThrowIfNull(operatorAppService);
        _operatorAppService = operatorAppService;
    }

    /// <summary>分页查询运营人员，支持角色与状态过滤。</summary>
    [Authorize(Roles = "Operator,Admin")]
    [HttpGet("api/admin/operators")]
    [ProducesResponseType(typeof(ApiResponse<OperatorListResultDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> QueryAsync(
        [FromQuery] OperatorRole? role,
        [FromQuery] OperatorStatus? status,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken ct = default)
    {
        var result = await _operatorAppService.QueryAsync(role, status, page, pageSize, ct);
        return Ok(ApiResponse.Success(result));
    }

    /// <summary>创建运营人员。</summary>
    [Authorize]
    [HttpPost("api/admin/operators")]
    [ProducesResponseType(typeof(ApiResponse<OperatorDto>), StatusCodes.Status201Created)]
    public async Task<IActionResult> CreateAsync([FromBody] SaveOperatorDto dto, CancellationToken ct)
    {
        var result = await _operatorAppService.CreateAsync(dto, ct);
        return CreatedAtAction(nameof(GetByIdAsync), new { operatorId = result.OperatorId }, ApiResponse.Success(result));
    }

    /// <summary>更新运营人员权限（合并新增权限码）。</summary>
    [Authorize]
    [HttpPut("api/admin/operators/{operatorId:guid}/permissions")]
    [ProducesResponseType(typeof(ApiResponse<OperatorDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> UpdatePermissionsAsync(Guid operatorId, [FromBody] AssignPermissionsDto dto, CancellationToken ct)
    {
        var result = await _operatorAppService.UpdatePermissionsAsync(operatorId, dto, ct);
        return Ok(ApiResponse.Success(result));
    }

    /// <summary>启用运营人员。</summary>
    [Authorize]
    [HttpPost("api/admin/operators/{operatorId:guid}/activate")]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> ActivateAsync(Guid operatorId, CancellationToken ct)
    {
        await _operatorAppService.ActivateAsync(operatorId, ct);
        return Ok(ApiResponse.Success());
    }

    /// <summary>停用运营人员。</summary>
    [Authorize]
    [HttpPost("api/admin/operators/{operatorId:guid}/deactivate")]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> DeactivateAsync(Guid operatorId, CancellationToken ct)
    {
        await _operatorAppService.DeactivateAsync(operatorId, ct);
        return Ok(ApiResponse.Success());
    }

    /// <summary>按标识获取运营人员。</summary>
    [Authorize]
    [HttpGet("api/admin/operators/{operatorId:guid}")]
    [ProducesResponseType(typeof(ApiResponse<OperatorDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetByIdAsync(Guid operatorId, CancellationToken ct)
    {
        var result = await _operatorAppService.GetByIdAsync(operatorId, ct);
        return Ok(ApiResponse.Success(result));
    }
}
