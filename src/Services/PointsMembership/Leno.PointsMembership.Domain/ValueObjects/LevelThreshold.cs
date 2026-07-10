using Leno.PointsMembership.Domain.Exceptions;

namespace Leno.PointsMembership.Domain.ValueObjects;

/// <summary>
/// 会员等级门槛值对象，表达“累计消费满 <see cref="MinConsumption"/> 元达到 <see cref="Level"/> 级”。
/// 不可变，按值相等，由运营配置的 <see cref="Aggregates.MembershipLevel"/> 聚合转换而来，供升级判定。
/// </summary>
public sealed record LevelThreshold
{
    /// <summary>等级编号，须 &gt; 0。</summary>
    public int Level { get; init; }

    /// <summary>等级名称。</summary>
    public string Name { get; init; } = string.Empty;

    /// <summary>达到该等级所需最低累计消费金额，须 ≥ 0。</summary>
    public decimal MinConsumption { get; init; }

    /// <summary>供 EF Core 与反序列化使用的无参构造。</summary>
    public LevelThreshold() { }

    public LevelThreshold(int level, string name, decimal minConsumption)
    {
        if (level <= 0)
        {
            throw new PointsDomainException("等级编号须大于 0", "LEVEL_INVALID");
        }

        if (string.IsNullOrWhiteSpace(name))
        {
            throw new PointsDomainException("等级名称不可为空", "LEVEL_NAME_EMPTY");
        }

        if (minConsumption < 0)
        {
            throw new PointsDomainException("最低消费金额不可为负", "LEVEL_MIN_CONSUMPTION_INVALID");
        }

        Level = level;
        Name = name;
        MinConsumption = minConsumption;
    }
}
