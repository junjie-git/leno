using Leno.Membership.Domain.Exceptions;

namespace Leno.Membership.Domain.ValueObjects;

/// <summary>
/// 成长值值对象，封装会员成长值数量与最后更新时间的不变量。
/// 不可变，按值相等；所有变更经聚合根方法回写新实例。
/// 1 积分 = 1 成长值（消费积分入账时同步累加）。
/// </summary>
public sealed record GrowthValue
{
    /// <summary>当前成长值数量。</summary>
    public int Value { get; init; }

    /// <summary>最近一次成长值更新时间（UTC）。</summary>
    public DateTime UpdatedAt { get; init; }

    /// <summary>供 EF Core 与反序列化使用的无参构造。</summary>
    public GrowthValue() { }

    public GrowthValue(int value, DateTime updatedAt)
    {
        if (value < 0)
        {
            throw new MembershipDomainException("成长值不可为负", "MEMBER_GROWTH_VALUE_NEGATIVE");
        }

        Value = value;
        UpdatedAt = updatedAt;
    }

    /// <summary>初始零成长值。</summary>
    public static GrowthValue Zero => new(0, DateTime.UtcNow);

    /// <summary>累加成长值，返回新值对象。</summary>
    public GrowthValue Add(int amount)
    {
        if (amount <= 0)
        {
            throw new MembershipDomainException("成长值须大于 0", "MEMBER_GROWTH_VALUE_INVALID");
        }

        return this with { Value = Value + amount, UpdatedAt = DateTime.UtcNow };
    }

    /// <summary>扣减成长值（退款扣回场景），返回新值对象。</summary>
    public GrowthValue Subtract(int amount)
    {
        if (amount <= 0)
        {
            throw new MembershipDomainException("扣减成长值须大于 0", "MEMBER_GROWTH_VALUE_INVALID");
        }

        if (Value < amount)
        {
            throw new MembershipDomainException(
                $"成长值不足：当前 {Value}，本次扣减 {amount}",
                "MEMBER_GROWTH_VALUE_INSUFFICIENT");
        }

        return this with { Value = Value - amount, UpdatedAt = DateTime.UtcNow };
    }
}

/// <summary>
/// 会员等级规则值对象，定义单个等级的门槛与权益描述。
/// 不可变，按值相等；由运营配置持久化为聚合根，转换为值对象供等级评估使用。
/// 成长值等级体系（V0-V4）：
///   V0: 0-99
///   V1: 100-499
///   V2: 500-1999
///   V3: 2000-9999
///   V4: 10000+
/// </summary>
public sealed record MemberLevel
{
    /// <summary>等级编号（0-4）。</summary>
    public int Level { get; init; }

    /// <summary>等级名称。</summary>
    public string Name { get; init; } = string.Empty;

    /// <summary>达到该等级所需的最低成长值（含）。</summary>
    public int MinGrowthValue { get; init; }

    /// <summary>达到该等级所需的最大成长值（不含），0 表示无上限。</summary>
    public int MaxGrowthValue { get; init; }

    /// <summary>等级描述。</summary>
    public string Description { get; init; } = string.Empty;

    /// <summary>等级提升奖励积分数（由 Points BC 消费 MemberLevelChangedIntegrationEvent 时发放）。</summary>
    public int LevelUpBonusPoints { get; init; }

    /// <summary>供 EF Core 与反序列化使用的无参构造。</summary>
    public MemberLevel() { }

    public MemberLevel(int level, string name, int minGrowthValue, int maxGrowthValue, string description, int levelUpBonusPoints = 0)
    {
        if (level < 0 || level > 4)
        {
            throw new MembershipDomainException("等级编号须在 0-4 之间", "MEMBER_LEVEL_INVALID");
        }

        if (string.IsNullOrWhiteSpace(name))
        {
            throw new MembershipDomainException("等级名称不可为空", "MEMBER_LEVEL_NAME_EMPTY");
        }

        if (minGrowthValue < 0)
        {
            throw new MembershipDomainException("最低成长值不可为负", "MEMBER_LEVEL_MIN_GROWTH_INVALID");
        }

        if (maxGrowthValue < 0)
        {
            throw new MembershipDomainException("最高成长值不可为负", "MEMBER_LEVEL_MAX_GROWTH_INVALID");
        }

        if (maxGrowthValue > 0 && maxGrowthValue <= minGrowthValue)
        {
            throw new MembershipDomainException("最高成长值须大于最低成长值", "MEMBER_LEVEL_RANGE_INVALID");
        }

        if (levelUpBonusPoints < 0)
        {
            throw new MembershipDomainException("等级提升奖励积分不可为负", "MEMBER_LEVEL_BONUS_INVALID");
        }

        Level = level;
        Name = name;
        MinGrowthValue = minGrowthValue;
        MaxGrowthValue = maxGrowthValue;
        Description = description ?? string.Empty;
        LevelUpBonusPoints = levelUpBonusPoints;
    }

    /// <summary>判断给定的成长值是否达到当前等级。</summary>
    public bool Matches(int growthValue)
    {
        if (growthValue < MinGrowthValue)
        {
            return false;
        }

        if (MaxGrowthValue > 0 && growthValue >= MaxGrowthValue)
        {
            return false;
        }

        return true;
    }

    /// <summary>判断给定的成长值是否达到或超过当前等级的最低门槛。</summary>
    public bool IsQualified(int growthValue) => growthValue >= MinGrowthValue;

    /// <summary>
    /// 根据成长值计算应达到的等级编号，从所有等级中选出最高匹配的等级。
    /// 单遍扫描，按 Level 编号取最大匹配项，消除排序开销。
    /// </summary>
    /// <param name="growthValue">成长值。</param>
    /// <param name="allLevels">全部等级定义。</param>
    /// <returns>匹配的等级编号，若无任何等级门槛达标则返回 0。</returns>
    public static int EvaluateLevel(int growthValue, IReadOnlyList<MemberLevel> allLevels)
    {
        MemberLevel? matched = null;
        foreach (var level in allLevels)
        {
            if (growthValue >= level.MinGrowthValue &&
                (matched is null || level.Level > matched.Level))
            {
                matched = level;
            }
        }

        return matched?.Level ?? 0;
    }
}

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
