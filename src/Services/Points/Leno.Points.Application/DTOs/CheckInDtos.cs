namespace Leno.Points.Application.DTOs;

/// <summary>
/// 签到结果 DTO，返回本次签到记录与奖励积分。
/// </summary>
public sealed class CheckInResultDto
{
    /// <summary>签到记录标识。</summary>
    public Guid RecordId { get; init; }

    /// <summary>签到用户标识。</summary>
    public Guid UserId { get; init; }

    /// <summary>签到日期（用户时区归一化）。</summary>
    public DateOnly CheckInDate { get; init; }

    /// <summary>连续签到天数。</summary>
    public int ContinuousDays { get; init; }

    /// <summary>本次签到奖励积分。</summary>
    public int PointsAwarded { get; init; }
}
