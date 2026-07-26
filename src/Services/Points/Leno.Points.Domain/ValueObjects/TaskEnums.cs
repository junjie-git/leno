namespace Leno.Points.Domain.ValueObjects;

/// <summary>
/// 任务类型枚举，定义用户可完成的任务种类。
/// </summary>
public enum TaskType
{
    /// <summary>每日签到（已通过 CheckInRecord 实现，此处保留供任务中心展示）。</summary>
    DailyCheckIn = 0,

    /// <summary>完善资料，一次性任务。</summary>
    CompleteProfile = 1,

    /// <summary>首单任务，一次性任务。</summary>
    FirstOrder = 2,

    /// <summary>分享商品，每日任务。</summary>
    ShareProduct = 3
}

/// <summary>
/// 用户任务状态枚举。
/// 流转：Pending → Completed（完成）；Daily 任务每日重置为 Pending。
/// </summary>
public enum UserTaskStatus
{
    /// <summary>未完成。</summary>
    Pending = 0,

    /// <summary>已完成。</summary>
    Completed = 1
}
