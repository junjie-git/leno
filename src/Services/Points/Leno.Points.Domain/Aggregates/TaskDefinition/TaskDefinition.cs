using Leno.Points.Domain.Exceptions;
using Leno.Points.Domain.ValueObjects;
using Leno.SharedKernel.Abstractions;

namespace Leno.Points.Domain.Aggregates.TaskDefinition;

/// <summary>
/// 任务定义聚合根，描述系统支持的任务类型、奖励积分与完成条件。
/// 聚合标识 <see cref="Entity.Id"/> 即对外 <c>TaskId</c>。
/// </summary>
public sealed class TaskDefinition : AggregateRoot
{
    /// <summary>任务类型。</summary>
    public TaskType Type { get; private set; }

    /// <summary>任务名称。</summary>
    public string Name { get; private set; } = string.Empty;

    /// <summary>任务描述。</summary>
    public string Description { get; private set; } = string.Empty;

    /// <summary>奖励积分。</summary>
    public int RewardPoints { get; private set; }

    /// <summary>完成条件描述。</summary>
    public string CompletionCondition { get; private set; } = string.Empty;

    /// <summary>是否为每日任务，每日任务在北京时间 00:00 重置。</summary>
    public bool IsDaily { get; private set; }

    /// <summary>是否为一次性任务，完成后不可重复领取。</summary>
    public bool IsOneTime { get; private set; }

    /// <summary>是否启用。</summary>
    public bool IsEnabled { get; private set; }

    /// <summary>EF Core 无参构造。</summary>
    private TaskDefinition() { }

    private TaskDefinition(Guid id) : base(id) { }

    /// <summary>
    /// 工厂方法，创建任务定义。
    /// </summary>
    /// <param name="taskId">任务标识，由应用层生成。</param>
    /// <param name="type">任务类型。</param>
    /// <param name="name">任务名称。</param>
    /// <param name="description">任务描述。</param>
    /// <param name="rewardPoints">奖励积分，须 ≥ 0。</param>
    /// <param name="completionCondition">完成条件描述。</param>
    /// <param name="isDaily">是否每日任务。</param>
    /// <param name="isOneTime">是否一次性任务。</param>
    public static TaskDefinition Create(
        Guid taskId,
        TaskType type,
        string name,
        string description,
        int rewardPoints,
        string completionCondition,
        bool isDaily,
        bool isOneTime)
    {
        if (taskId == Guid.Empty)
        {
            throw new PointsDomainException("TaskId 不可为空", "TASK_ID_EMPTY");
        }

        if (!Enum.IsDefined(type))
        {
            throw new PointsDomainException($"任务类型非法：{type}", "TASK_TYPE_INVALID");
        }

        if (string.IsNullOrWhiteSpace(name))
        {
            throw new PointsDomainException("任务名称不可为空", "TASK_NAME_EMPTY");
        }

        if (rewardPoints < 0)
        {
            throw new PointsDomainException("奖励积分不可为负", "TASK_REWARD_INVALID");
        }

        if (isDaily && isOneTime)
        {
            throw new PointsDomainException("任务不可同时为每日与一次性", "TASK_CONFLICT");
        }

        return new TaskDefinition(taskId)
        {
            Type = type,
            Name = name,
            Description = description ?? string.Empty,
            RewardPoints = rewardPoints,
            CompletionCondition = completionCondition ?? string.Empty,
            IsDaily = isDaily,
            IsOneTime = isOneTime,
            IsEnabled = true
        };
    }

    /// <summary>
    /// 启用任务。
    /// </summary>
    public void Enable()
    {
        if (IsEnabled)
        {
            throw new PointsDomainException("任务已启用，不可重复启用", "TASK_ALREADY_ENABLED");
        }

        IsEnabled = true;
    }

    /// <summary>
    /// 停用任务。
    /// </summary>
    public void Disable()
    {
        if (!IsEnabled)
        {
            throw new PointsDomainException("任务已停用，不可重复停用", "TASK_ALREADY_DISABLED");
        }

        IsEnabled = false;
    }
}
