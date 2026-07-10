using Leno.Infrastructure.Auth;
using Leno.SystemAdmin.Application;
using Leno.SystemAdmin.Application.DTOs;
using Leno.SystemAdmin.Domain.ValueObjects;
using Leno.SharedContracts.Responses;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Leno.SystemAdmin.Api.Controllers;

/// <summary>
/// 定时任务管理控制器（运营端 CRUD、启停与立即触发）。
/// </summary>
[Authorize(Roles = "Operator,Admin")]
[ApiController]
public sealed class ScheduledTasksController : SystemAdminControllerBase
{
    private readonly IScheduledTaskAppService _scheduledTaskAppService;

    public ScheduledTasksController(ICurrentUserContext currentUser, IScheduledTaskAppService scheduledTaskAppService)
        : base(currentUser)
    {
        ArgumentNullException.ThrowIfNull(scheduledTaskAppService);
        _scheduledTaskAppService = scheduledTaskAppService;
    }

    /// <summary>分页查询定时任务，支持名称与状态过滤。</summary>
    [HttpGet("api/admin/scheduled-tasks")]
    [ProducesResponseType(typeof(ApiResponse<ScheduledTaskListResultDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> QueryAsync(
        [FromQuery] string? name,
        [FromQuery] ScheduledTaskStatus? status,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken ct = default)
    {
        var result = await _scheduledTaskAppService.QueryAsync(name, status, page, pageSize, ct);
        return Ok(ApiResponse.Success(result));
    }

    /// <summary>创建定时任务（初始为停用态）。</summary>
    [HttpPost("api/admin/scheduled-tasks")]
    [ProducesResponseType(typeof(ApiResponse<ScheduledTaskDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> CreateAsync([FromBody] SaveScheduledTaskDto dto, CancellationToken ct)
    {
        var result = await _scheduledTaskAppService.CreateAsync(dto, ct);
        return Ok(ApiResponse.Success(result));
    }

    /// <summary>更新定时任务（作业类型不可变）。</summary>
    [HttpPut("api/admin/scheduled-tasks/{taskId:guid}")]
    [ProducesResponseType(typeof(ApiResponse<ScheduledTaskDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> UpdateAsync(Guid taskId, [FromBody] UpdateScheduledTaskDto dto, CancellationToken ct)
    {
        var result = await _scheduledTaskAppService.UpdateAsync(taskId, dto, ct);
        return Ok(ApiResponse.Success(result));
    }

    /// <summary>启用任务并向调度器注册。</summary>
    [HttpPost("api/admin/scheduled-tasks/{taskId:guid}/enable")]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> EnableAsync(Guid taskId, CancellationToken ct)
    {
        await _scheduledTaskAppService.EnableAsync(taskId, ct);
        return Ok(ApiResponse.Success());
    }

    /// <summary>停用任务并从调度器注销。</summary>
    [HttpPost("api/admin/scheduled-tasks/{taskId:guid}/disable")]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> DisableAsync(Guid taskId, CancellationToken ct)
    {
        await _scheduledTaskAppService.DisableAsync(taskId, ct);
        return Ok(ApiResponse.Success());
    }

    /// <summary>立即触发任务执行。</summary>
    [HttpPost("api/admin/scheduled-tasks/{taskId:guid}/run-now")]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> RunNowAsync(Guid taskId, CancellationToken ct)
    {
        await _scheduledTaskAppService.RunNowAsync(taskId, ct);
        return Ok(ApiResponse.Success());
    }
}
