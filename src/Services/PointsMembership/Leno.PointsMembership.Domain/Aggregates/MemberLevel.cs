using Leno.PointsMembership.Domain.Exceptions;
using Leno.SharedKernel.Abstractions;

namespace Leno.PointsMembership.Domain.Aggregates;

/// <summary>
/// 会员等级聚合根（基于成长值体系），定义 V0-V4 等级与成长值门槛。
/// 聚合标识 <see cref="Entity.Id"/> 即对外 <c>MemberLevelId</c>。
/// 成长值门槛：
///   V0: 0-99
///   V1: 100-499
///   V2: 500-1999
///   V3: 2000-9999
///   V4: 10000+
/// </summary>
public sealed class MemberLevel : AggregateRoot
{
    /// <summary>等级编号（0-4）。</summary>
    public int Level { get; private set; }

    /// <summary>等级名称。</summary>
    public string Name { get; private set; } = string.Empty;

    /// <summary>达到该等级所需的最低成长值（含）。</summary>
    public int MinGrowthValue { get; private set; }

    /// <summary>达到该等级所需的最大成长值（不含），0 表示无上限。</summary>
    public int MaxGrowthValue { get; private set; }

    /// <summary>等级描述。</summary>
    public string Description { get; private set; } = string.Empty;

    /// <summary>EF Core 无参构造。</summary>
    private MemberLevel() { }

    private MemberLevel(Guid id) : base(id) { }

    /// <summary>
    /// 工厂方法，创建会员等级定义。
    /// </summary>
    /// <param name="memberLevelId">等级标识，由应用层生成。</param>
    /// <param name="level">等级编号（0-4）。</param>
    /// <param name="name">等级名称。</param>
    /// <param name="minGrowthValue">最低成长值（含）。</param>
    /// <param name="maxGrowthValue">最高成长值（不含），0 表示无上限。</param>
    /// <param name="description">等级描述。</param>
    public static MemberLevel Create(
        Guid memberLevelId,
        int level,
        string name,
        int minGrowthValue,
        int maxGrowthValue,
        string description)
    {
        Validate(level, name, minGrowthValue, maxGrowthValue);

        return new MemberLevel(memberLevelId == Guid.Empty ? Guid.NewGuid() : memberLevelId)
        {
            Level = level,
            Name = name,
            MinGrowthValue = minGrowthValue,
            MaxGrowthValue = maxGrowthValue,
            Description = description ?? string.Empty
        };
    }

    /// <summary>
    /// 判断给定的成长值是否达到当前等级。
    /// </summary>
    /// <param name="growthValue">成长值。</param>
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

    /// <summary>
    /// 判断给定的成长值是否达到或超过当前等级的最低门槛。
    /// </summary>
    /// <param name="growthValue">成长值。</param>
    public bool IsQualified(int growthValue)
        => growthValue >= MinGrowthValue;

    /// <summary>
    /// 根据成长值计算应达到的等级编号，从所有等级中选出最高匹配的等级。
    /// PM-L03 修复：原先先 OrderBy(MinGrowthValue) 再遍历取最后一个匹配项，存在 O(n log n) 排序开销
    /// 且语义依赖 MinGrowthValue 与 Level 正相关假设。改为单遍扫描，按 Level 编号取最大匹配项，
    /// 消除排序开销并直接以 Level 为排名依据。
    /// </summary>
    /// <param name="growthValue">成长值。</param>
    /// <param name="allLevels">全部等级定义。</param>
    /// <returns>匹配的等级编号，若无任何等级门槛达标则返回 0。</returns>
    public static int EvaluateLevel(int growthValue, List<MemberLevel> allLevels)
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

    private static void Validate(int level, string name, int minGrowthValue, int maxGrowthValue)
    {
        if (level < 0 || level > 4)
        {
            throw new PointsDomainException("等级编号须在 0-4 之间", "MEMBER_LEVEL_INVALID");
        }

        if (string.IsNullOrWhiteSpace(name))
        {
            throw new PointsDomainException("等级名称不可为空", "MEMBER_LEVEL_NAME_EMPTY");
        }

        if (minGrowthValue < 0)
        {
            throw new PointsDomainException("最低成长值不可为负", "MEMBER_LEVEL_MIN_GROWTH_INVALID");
        }

        if (maxGrowthValue < 0)
        {
            throw new PointsDomainException("最高成长值不可为负", "MEMBER_LEVEL_MAX_GROWTH_INVALID");
        }

        if (maxGrowthValue > 0 && maxGrowthValue <= minGrowthValue)
        {
            throw new PointsDomainException("最高成长值须大于最低成长值", "MEMBER_LEVEL_RANGE_INVALID");
        }
    }
}