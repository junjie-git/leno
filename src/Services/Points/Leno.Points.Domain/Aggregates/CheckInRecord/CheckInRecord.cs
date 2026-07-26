using Leno.Points.Domain.Exceptions;
using Leno.SharedKernel.Abstractions;

namespace Leno.Points.Domain.Aggregates.CheckInRecord;

/// <summary>
/// 签到记录聚合根，记录用户单日签到结果。
/// 连续签到天数与奖励积分的计算逻辑位于应用服务，本聚合仅持久化结果。
/// 聚合标识 <see cref="Entity.Id"/> 即对外 <c>RecordId</c>。
/// </summary>
public sealed class CheckInRecord : AggregateRoot
{
    /// <summary>签到用户标识。</summary>
    public Guid UserId { get; private set; }

    /// <summary>签到日期（按用户时区归一化）。</summary>
    public DateOnly CheckInDate { get; private set; }

    /// <summary>连续签到天数。</summary>
    public int ContinuousDays { get; private set; }

    /// <summary>本次签到奖励积分。</summary>
    public int PointsAwarded { get; private set; }

    /// <summary>EF Core 无参构造。</summary>
    private CheckInRecord() { }

    private CheckInRecord(Guid id) : base(id) { }

    /// <summary>
    /// 工厂方法，校验用户标识非空、连续天数与奖励积分合法。
    /// </summary>
    /// <param name="recordId">记录标识，由应用层生成。</param>
    /// <param name="userId">签到用户标识。</param>
    /// <param name="checkInDate">签到日期。</param>
    /// <param name="continuousDays">连续签到天数，须 &gt; 0。</param>
    /// <param name="pointsAwarded">奖励积分，须 ≥ 0。</param>
    public static CheckInRecord CheckIn(
        Guid recordId,
        Guid userId,
        DateOnly checkInDate,
        int continuousDays,
        int pointsAwarded)
    {
        if (userId == Guid.Empty)
        {
            throw new PointsDomainException("UserId 不可为空", "POINTS_USER_EMPTY");
        }

        if (continuousDays <= 0)
        {
            throw new PointsDomainException("连续签到天数须大于 0", "CHECKIN_CONTINUOUS_INVALID");
        }

        if (pointsAwarded < 0)
        {
            throw new PointsDomainException("奖励积分不可为负", "CHECKIN_POINTS_INVALID");
        }

        return new CheckInRecord(recordId == Guid.Empty ? Guid.NewGuid() : recordId)
        {
            UserId = userId,
            CheckInDate = checkInDate,
            ContinuousDays = continuousDays,
            PointsAwarded = pointsAwarded
        };
    }
}
