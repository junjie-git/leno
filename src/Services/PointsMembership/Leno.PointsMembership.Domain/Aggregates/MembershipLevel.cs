using Leno.PointsMembership.Domain.Exceptions;
using Leno.PointsMembership.Domain.ValueObjects;
using Leno.SharedKernel.Abstractions;

namespace Leno.PointsMembership.Domain.Aggregates;

/// <summary>
/// 会员等级聚合根，运营配置的等级定义，封装等级门槛、折扣率与状态的不变量。
/// 聚合标识 <see cref="Entity.Id"/> 即对外 <c>LevelId</c>。
/// </summary>
public sealed class MembershipLevel : AggregateRoot
{
    /// <summary>等级名称。</summary>
    public string Name { get; private set; } = string.Empty;

    /// <summary>等级编号，须 &gt; 0。</summary>
    public int Level { get; private set; }

    /// <summary>达到该等级所需最低累计消费金额，须 ≥ 0。</summary>
    public decimal MinConsumption { get; private set; }

    /// <summary>折扣率（0-1，1 表示无折扣）。</summary>
    public decimal DiscountRate { get; private set; }

    /// <summary>等级状态。</summary>
    public MembershipLevelStatus Status { get; private set; }

    /// <summary>EF Core 无参构造。</summary>
    private MembershipLevel() { }

    private MembershipLevel(Guid id) : base(id) { }

    /// <summary>
    /// 工厂方法，校验名称非空、等级 &gt; 0、门槛 ≥ 0、折扣率在 0-1 之间，初始状态为 Enabled。
    /// </summary>
    /// <param name="levelId">等级标识，由应用层生成。</param>
    /// <param name="name">等级名称。</param>
    /// <param name="level">等级编号，须 &gt; 0。</param>
    /// <param name="minConsumption">最低累计消费金额。</param>
    /// <param name="discountRate">折扣率（0-1）。</param>
    public static MembershipLevel Create(
        Guid levelId,
        string name,
        int level,
        decimal minConsumption,
        decimal discountRate)
    {
        Validate(name, level, minConsumption, discountRate);

        return new MembershipLevel(levelId == Guid.Empty ? Guid.NewGuid() : levelId)
        {
            Name = name,
            Level = level,
            MinConsumption = minConsumption,
            DiscountRate = discountRate,
            Status = MembershipLevelStatus.Enabled
        };
    }

    /// <summary>
    /// 更新等级可编辑字段。
    /// </summary>
    /// <param name="name">等级名称。</param>
    /// <param name="level">等级编号，须 &gt; 0。</param>
    /// <param name="minConsumption">最低累计消费金额。</param>
    /// <param name="discountRate">折扣率（0-1）。</param>
    public void Update(string name, int level, decimal minConsumption, decimal discountRate)
    {
        Validate(name, level, minConsumption, discountRate);

        Name = name;
        Level = level;
        MinConsumption = minConsumption;
        DiscountRate = discountRate;
    }

    /// <summary>启用等级。</summary>
    public void Enable()
    {
        if (Status == MembershipLevelStatus.Enabled)
        {
            throw new PointsDomainException("等级已启用", "LEVEL_ALREADY_ENABLED");
        }

        Status = MembershipLevelStatus.Enabled;
    }

    /// <summary>停用等级，停用后不参与升级判定。</summary>
    public void Disable()
    {
        if (Status == MembershipLevelStatus.Disabled)
        {
            throw new PointsDomainException("等级已停用", "LEVEL_ALREADY_DISABLED");
        }

        Status = MembershipLevelStatus.Disabled;
    }

    private static void Validate(string name, int level, decimal minConsumption, decimal discountRate)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new PointsDomainException("等级名称不可为空", "LEVEL_NAME_EMPTY");
        }

        if (level <= 0)
        {
            throw new PointsDomainException("等级编号须大于 0", "LEVEL_INVALID");
        }

        if (minConsumption < 0)
        {
            throw new PointsDomainException("最低消费金额不可为负", "LEVEL_MIN_CONSUMPTION_INVALID");
        }

        if (discountRate < 0 || discountRate > 1)
        {
            throw new PointsDomainException("折扣率须在 0-1 之间", "LEVEL_DISCOUNT_RATE_INVALID");
        }
    }
}
