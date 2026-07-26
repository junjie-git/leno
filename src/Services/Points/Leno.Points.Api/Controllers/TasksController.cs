using Leno.Infrastructure.Auth;
using Leno.Points.Application;
using Leno.Points.Application.DTOs;
using Leno.SharedContracts.Responses;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Leno.Points.Api.Controllers;

/// <summary>
/// 任务中心控制器（买家端）。
/// 路由 /api/points/tasks/*，需 Buyer 角色。
/// 对应 design-prompts operations/08-membership-ops/points.md 的 2 个任务端点：
/// 任务列表查询、任务完成领取积分。
/// </summary>
[ApiController]
[Route("api/points/tasks")]
[Authorize(Roles = "Buyer")]
public sealed class TasksController : ControllerBase
{
    private readonly ITaskAppService _taskAppService;
    private readonly ICurrentUserContext _currentUser;

    public TasksController(
        ITaskAppService taskAppService,
        ICurrentUserContext currentUser)
    {
        ArgumentNullException.ThrowIfNull(taskAppService);
        ArgumentNullException.ThrowIfNull(currentUser);
        _taskAppService = taskAppService;
        _currentUser = currentUser;
    }

    /// <summary>获取任务列表（含当前用户完成状态），每日任务自动重置。</summary>
    [HttpGet]
    [ProducesResponseType(typeof(ApiResponse<List<TaskDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetTasksAsync(CancellationToken ct)
    {
        var result = await _taskAppService.GetTasksAsync(GetCurrentUserId(), ct);
        return Ok(ApiResponse.Success(result));
    }

    /// <summary>完成任务，领取积分奖励。</summary>
    [HttpPost("{taskId:guid}/complete")]
    [ProducesResponseType(typeof(ApiResponse<TaskCompleteResultDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> CompleteTaskAsync([FromRoute] Guid taskId, CancellationToken ct)
    {
        var result = await _taskAppService.CompleteTaskAsync(GetCurrentUserId(), taskId, ct);
        return Ok(ApiResponse.Success(result));
    }

    private Guid GetCurrentUserId()
    {
        if (!_currentUser.IsAuthenticated || !_currentUser.UserId.HasValue)
        {
            throw new UnauthorizedAccessException("未认证");
        }

        return _currentUser.UserId.Value;
    }
}
