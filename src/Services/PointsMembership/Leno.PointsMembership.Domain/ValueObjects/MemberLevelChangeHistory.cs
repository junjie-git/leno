namespace Leno.PointsMembership.Domain.ValueObjects;

/// <summary>
/// 会员等级变更历史记录值对象，记录每次等级变更的快照。
/// 不可变，按值相等。
/// </summary>
public sealed record MemberLevelChangeHistory
{
    /// <summary>变更前等级编号。</summary>
    public int OldLevel { get; init; }

    /// <summary>变更后等级编号。</summary>
    public int NewLevel { get; init; }

    /// <summary>变更时的成长值。</summary>
    public int GrowthValue { get; init; }

    /// <summary>变更时间（UTC）。</summary>
    public DateTime ChangedAt { get; init; }

    /// <summary>变更原因描述。</summary>
    public string Reason { get; init; } = string.Empty;

    /// <summary>供 EF Core 与反序列化使用的无参构造。</summary>
    public MemberLevelChangeHistory() { }

    public MemberLevelChangeHistory(int oldLevel, int newLevel, int growthValue, DateTime changedAt, string reason)
    {
        OldLevel = oldLevel;
        NewLevel = newLevel;
        GrowthValue = growthValue;
        ChangedAt = changedAt;
        Reason = reason ?? string.Empty;
    }
}