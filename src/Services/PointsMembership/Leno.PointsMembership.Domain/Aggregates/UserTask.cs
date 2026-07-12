using Leno.PointsMembership.Domain.Exceptions;
using Leno.PointsMembership.Domain.ValueObjects;
using Leno.SharedKernel.Abstractions;

namespace Leno.PointsMembership.Domain.Aggregates;

/// <summary>
/// 用户任务实体，记录用户对某个任务的完成状态。
/// 隶属于 Task 聚合，通过 <see cref="TaskId"/> 关联任务定义。
/// </summary>
public sealed class UserTask : AggregateRoot
{
    /// <summary>用户标识。</summary>
    public Guid UserId { get; private set; }

    /// <summary>任务标识。</summary>
    public Guid TaskId { get; private set; }

    /// <summary>完成状态。</summary>
    public UserTaskStatus Status { get; private set; }

    /// <summary>完成时间（UTC），未完成时为空。</summary>
    public DateTime? CompletedAt { get; private set; }

    /// <summary>完成日期（北京时间），用于每日任务重置判定。</summary>
    public DateOnly? CompletedDate { get; private set; }

    /// <summary>EF Core 无参构造。</summary>
    private UserTask() { }

    private UserTask(Guid id) : base(id) { }

    /// <summary>
    /// 工厂方法，创建待完成用户任务。
    /// </summary>
    /// <param name="userTaskId">用户任务标识。</param>
    /// <param name="userId">用户标识。</param>
    /// <param name="taskId">任务定义标识。</param>
    public static UserTask Create(Guid userTaskId, Guid userId, Guid taskId)
    {
        if (userTaskId == Guid.Empty)
        {
            throw new PointsDomainException("UserTaskId 不可为空", "USER_TASK_ID_EMPTY");
        }

        if (userId == Guid.Empty)
        {
            throw new PointsDomainException("UserId 不可为空", "POINTS_USER_EMPTY");
        }

        if (taskId == Guid.Empty)
        {
            throw new PointsDomainException("TaskId 不可为空", "TASK_ID_EMPTY");
        }

        return new UserTask(userTaskId)
        {
            UserId = userId,
            TaskId = taskId,
            Status = UserTaskStatus.Pending
        };
    }

    /// <summary>
    /// 完成任务，校验待完成态，置已完成态并记录完成时间与日期。
    /// </summary>
    public void Complete()
    {
        if (Status != UserTaskStatus.Pending)
        {
            throw new PointsDomainException(
                $"当前状态 {Status} 不可完成，仅 Pending 可完成",
                "USER_TASK_ALREADY_COMPLETED");
        }

        var now = DateTime.UtcNow;
        Status = UserTaskStatus.Completed;
        CompletedAt = now;
        CompletedDate = GetBeijingDate(now);
    }

    /// <summary>
    /// 重置为待完成状态，仅用于每日任务每日重置。
    /// </summary>
    public void Reset()
    {
        if (Status != UserTaskStatus.Completed)
        {
            throw new PointsDomainException(
                $"当前状态 {Status} 不可重置，仅 Completed 可重置",
                "USER_TASK_RESET_INVALID");
        }

        Status = UserTaskStatus.Pending;
        CompletedAt = null;
        CompletedDate = null;
    }

    /// <summary>
    /// 获取北京时间（UTC+8）对应的日期。
    /// </summary>
    private static DateOnly GetBeijingDate(DateTime utcTime)
    {
        var beijingTime = utcTime.AddHours(8);
        return DateOnly.FromDateTime(beijingTime);
    }
}