using Leno.Infrastructure.Auth;
using Leno.SystemAdmin.Application;
using Leno.SystemAdmin.Application.DTOs;
using Leno.SystemAdmin.Domain.ValueObjects;
using Leno.SharedContracts.Responses;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Leno.SystemAdmin.Api.Controllers;

/// <summary>
/// 索引重建管理控制器（运营端触发、查询进度与重试）。
/// </summary>
[Authorize(Roles = "Operator,Admin")]
[ApiController]
public sealed class IndexRebuildController : SystemAdminControllerBase
{
    private readonly IIndexRebuildAppService _indexRebuildAppService;

    public IndexRebuildController(ICurrentUserContext currentUser, IIndexRebuildAppService indexRebuildAppService)
        : base(currentUser)
    {
        ArgumentNullException.ThrowIfNull(indexRebuildAppService);
        _indexRebuildAppService = indexRebuildAppService;
    }

    /// <summary>分页查询索引重建任务，支持目标上下文与状态过滤。</summary>
    [HttpGet("api/admin/index-rebuild/tasks")]
    [ProducesResponseType(typeof(ApiResponse<IndexRebuildTaskListResultDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> QueryAsync(
        [FromQuery] string? targetContext,
        [FromQuery] RebuildTaskStatus? status,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken ct = default)
    {
        var result = await _indexRebuildAppService.QueryAsync(targetContext, status, page, pageSize, ct);
        return Ok(ApiResponse.Success(result));
    }

    /// <summary>触发索引重建，创建任务并开始执行。</summary>
    [HttpPost("api/admin/index-rebuild/trigger")]
    [ProducesResponseType(typeof(ApiResponse<IndexRebuildTaskDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> TriggerAsync([FromBody] TriggerIndexRebuildDto dto, CancellationToken ct)
    {
        var triggeredBy = GetCurrentOperatorId().ToString();
        var result = await _indexRebuildAppService.TriggerAsync(dto, triggeredBy, ct);
        return Ok(ApiResponse.Success(result));
    }

    /// <summary>获取索引重建任务详情/进度。</summary>
    [HttpGet("api/admin/index-rebuild/tasks/{id:guid}")]
    [ProducesResponseType(typeof(ApiResponse<IndexRebuildTaskDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetByIdAsync(Guid id, CancellationToken ct)
    {
        var result = await _indexRebuildAppService.GetByIdAsync(id, ct);
        if (result is null)
        {
            return NotFound(ApiResponse.Fail(404, "索引重建任务不存在"));
        }

        return Ok(ApiResponse.Success(result));
    }

    /// <summary>重试失败的索引重建任务。</summary>
    [HttpPost("api/admin/index-rebuild/tasks/{id:guid}/retry")]
    [ProducesResponseType(typeof(ApiResponse<IndexRebuildTaskDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> RetryAsync(Guid id, CancellationToken ct)
    {
        var triggeredBy = GetCurrentOperatorId().ToString();
        var result = await _indexRebuildAppService.RetryAsync(id, triggeredBy, ct);
        return Ok(ApiResponse.Success(result));
    }
}