using Leno.Points.Domain.ValueObjects;

namespace Leno.Points.Application.DTOs;

/// <summary>
/// 任务 DTO，返回任务定义信息与当前用户完成状态。
/// </summary>
public sealed class TaskDto
{
    /// <summary>任务标识。</summary>
    public Guid Id { get; init; }

    /// <summary>任务类型。</summary>
    public TaskType Type { get; init; }

    /// <summary>任务名称。</summary>
    public string Name { get; init; } = string.Empty;

    /// <summary>任务描述。</summary>
    public string Description { get; init; } = string.Empty;

    /// <summary>奖励积分。</summary>
    public int RewardPoints { get; init; }

    /// <summary>完成条件描述。</summary>
    public string CompletionCondition { get; init; } = string.Empty;

    /// <summary>是否为每日任务。</summary>
    public bool IsDaily { get; init; }

    /// <summary>是否为一次性任务。</summary>
    public bool IsOneTime { get; init; }

    /// <summary>是否启用。</summary>
    public bool IsEnabled { get; init; }

    /// <summary>当前用户完成状态。</summary>
    public UserTaskStatus? UserStatus { get; init; }

    /// <summary>用户完成时间（UTC）。</summary>
    public DateTime? CompletedAt { get; init; }
}

/// <summary>
/// 任务完成结果 DTO。
/// </summary>
public sealed class TaskCompleteResultDto
{
    /// <summary>用户任务标识。</summary>
    public Guid UserTaskId { get; init; }

    /// <summary>任务标识。</summary>
    public Guid TaskId { get; init; }

    /// <summary>用户标识。</summary>
    public Guid UserId { get; init; }

    /// <summary>本次完成奖励积分。</summary>
    public int PointsAwarded { get; init; }

    /// <summary>完成时间（UTC）。</summary>
    public DateTime CompletedAt { get; init; }
}
