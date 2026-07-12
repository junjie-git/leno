using Leno.PointsMembership.Domain.ValueObjects;

namespace Leno.PointsMembership.Application.DTOs;

/// <summary>
/// 任务 DTO，返回任务定义信息。
/// </summary>
public sealed class TaskDto
{
    public Guid Id { get; init; }

    public TaskType Type { get; init; }

    public string Name { get; init; } = string.Empty;

    public string Description { get; init; } = string.Empty;

    public int RewardPoints { get; init; }

    public string CompletionCondition { get; init; } = string.Empty;

    public bool IsDaily { get; init; }

    public bool IsOneTime { get; init; }

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
    public Guid UserTaskId { get; init; }

    public Guid TaskId { get; init; }

    public Guid UserId { get; init; }

    public int PointsAwarded { get; init; }

    public DateTime CompletedAt { get; init; }
}