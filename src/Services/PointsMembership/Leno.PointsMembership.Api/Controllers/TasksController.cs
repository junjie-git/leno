using Leno.Infrastructure.Auth;
using Leno.PointsMembership.Application;
using Leno.PointsMembership.Application.DTOs;
using Leno.SharedContracts.Responses;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Leno.PointsMembership.Api.Controllers;

/// <summary>
/// 任务中心控制器。
/// 买家端（/api/points/tasks）：任务列表查询、任务完成领取积分。
/// </summary>
[ApiController]
[Authorize(Roles = "Buyer")]
public sealed class TasksController : PointsMembershipControllerBase
{
    private readonly ITaskAppService _taskAppService;

    public TasksController(
        ICurrentUserContext currentUser,
        ITaskAppService taskAppService)
        : base(currentUser)
    {
        ArgumentNullException.ThrowIfNull(taskAppService);
        _taskAppService = taskAppService;
    }

    /// <summary>获取任务列表（含当前用户完成状态），每日任务自动重置。</summary>
    [HttpGet("api/points/tasks")]
    [ProducesResponseType(typeof(ApiResponse<List<TaskDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetTasksAsync(CancellationToken ct)
    {
        var tasks = await _taskAppService.GetTasksAsync(GetCurrentUserId(), ct);
        return Ok(ApiResponse.Success(tasks));
    }

    /// <summary>完成任务，领取积分奖励。</summary>
    [HttpPost("api/points/tasks/{taskId}/complete")]
    [ProducesResponseType(typeof(ApiResponse<TaskCompleteResultDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> CompleteTaskAsync(Guid taskId, CancellationToken ct)
    {
        var result = await _taskAppService.CompleteTaskAsync(GetCurrentUserId(), taskId, ct);
        return Ok(ApiResponse.Success(result));
    }
}