using Leno.PointsMembership.Application.DTOs;
using Leno.PointsMembership.Domain.Aggregates;
using Leno.PointsMembership.Domain.Exceptions;
using Leno.PointsMembership.Domain.Repositories;
using Leno.PointsMembership.Domain.ValueObjects;
using Leno.SharedKernel.Abstractions;

namespace Leno.PointsMembership.Application.Services;

/// <summary>
/// 任务中心应用服务实现，编排任务列表查询与任务完成用例。
/// 每日任务在北京时间 00:00 重置。
/// </summary>
public sealed class TaskAppService : ITaskAppService
{
    private readonly ITaskRepository _taskRepository;
    private readonly IUserTaskRepository _userTaskRepository;
    private readonly IPointsAccountRepository _accountRepository;
    private readonly IUnitOfWork _unitOfWork;

    public TaskAppService(
        ITaskRepository taskRepository,
        IUserTaskRepository userTaskRepository,
        IPointsAccountRepository accountRepository,
        IUnitOfWork unitOfWork)
    {
        _taskRepository = taskRepository;
        _userTaskRepository = userTaskRepository;
        _accountRepository = accountRepository;
        _unitOfWork = unitOfWork;
    }

    /// <inheritdoc />
    public async Task<List<TaskDto>> GetTasksAsync(Guid userId, CancellationToken ct = default)
    {
        var tasks = await _taskRepository.GetAllEnabledAsync(ct);
        var userTasks = await _userTaskRepository.GetByUserIdAsync(userId, ct);
        var todayBeijing = GetBeijingDate(DateTime.UtcNow);

        var result = new List<TaskDto>();
        foreach (var task in tasks)
        {
            var userTask = userTasks.FirstOrDefault(ut => ut.TaskId == task.Id);

            // 每日任务：如果已完成日期不是今天，重置状态
            if (task.IsDaily && userTask is not null && userTask.Status == UserTaskStatus.Completed)
            {
                if (userTask.CompletedDate != todayBeijing)
                {
                    userTask.Reset();
                }
            }

            result.Add(new TaskDto
            {
                Id = task.Id,
                Type = task.Type,
                Name = task.Name,
                Description = task.Description,
                RewardPoints = task.RewardPoints,
                CompletionCondition = task.CompletionCondition,
                IsDaily = task.IsDaily,
                IsOneTime = task.IsOneTime,
                IsEnabled = task.IsEnabled,
                UserStatus = userTask?.Status,
                CompletedAt = userTask?.CompletedAt
            });
        }

        return result;
    }

    /// <inheritdoc />
    public async Task<TaskCompleteResultDto> CompleteTaskAsync(Guid userId, Guid taskId, CancellationToken ct = default)
    {
        var task = await _taskRepository.GetByIdAsync(taskId, ct)
            ?? throw new PointsDomainException($"任务 {taskId} 不存在", "TASK_NOT_FOUND");

        if (!task.IsEnabled)
        {
            throw new PointsDomainException("任务已停用", "TASK_DISABLED");
        }

        var userTask = await _userTaskRepository.GetByUserIdAndTaskIdAsync(userId, taskId, ct);

        if (task.IsOneTime && userTask is not null && userTask.Status == UserTaskStatus.Completed)
        {
            throw new PointsDomainException("一次性任务已完成，不可重复完成", "TASK_ONETIME_ALREADY_DONE");
        }

        if (task.IsDaily && userTask is not null)
        {
            var todayBeijing = GetBeijingDate(DateTime.UtcNow);
            if (userTask.Status == UserTaskStatus.Completed && userTask.CompletedDate == todayBeijing)
            {
                throw new PointsDomainException("今日已完成该任务", "TASK_DAILY_ALREADY_DONE");
            }

            // 重置前一天的任务
            if (userTask.Status == UserTaskStatus.Completed && userTask.CompletedDate != todayBeijing)
            {
                userTask.Reset();
            }
        }

        if (userTask is null)
        {
            userTask = UserTask.Create(Guid.NewGuid(), userId, taskId);
            await _userTaskRepository.AddAsync(userTask, ct);
        }

        userTask.Complete();

        var account = await _accountRepository.GetByUserIdAsync(userId, ct)
            ?? throw new PointsDomainException(
                $"用户 {userId} 的积分账户不存在",
                "POINTS_ACCOUNT_NOT_FOUND");

        account.Earn(PointsSource.Task, task.RewardPoints, $"完成任务：{task.Name}");

        await _unitOfWork.SaveEntitiesAsync(ct);

        return new TaskCompleteResultDto
        {
            UserTaskId = userTask.Id,
            TaskId = task.Id,
            UserId = userId,
            PointsAwarded = task.RewardPoints,
            CompletedAt = userTask.CompletedAt!.Value
        };
    }

    /// <summary>
    /// 获取北京时间（UTC+8）对应的日期。
    /// </summary>
    private static DateOnly GetBeijingDate(DateTime utcTime)
    {
        return DateOnly.FromDateTime(utcTime.AddHours(8));
    }
}