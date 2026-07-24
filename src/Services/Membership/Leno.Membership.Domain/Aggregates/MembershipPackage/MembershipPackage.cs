using Leno.Membership.Domain.Exceptions;
using Leno.Membership.Domain.ValueObjects;
using Leno.SharedKernel.Abstractions;

namespace Leno.Membership.Domain.Aggregates.MembershipPackage;

/// <summary>
/// 会员套餐聚合根，运营配置的可购买会员套餐，封装价格、时长、权益与状态的不变量。
/// 聚合标识 <see cref="Entity.Id"/> 即对外 <c>PackageId</c>。
/// </summary>
public sealed class MembershipPackage : AggregateRoot
{
    /// <summary>套餐名称。</summary>
    public string Name { get; private set; } = string.Empty;

    /// <summary>套餐对应会员等级编号，须 &gt; 0。</summary>
    public int Level { get; private set; }

    /// <summary>套餐价格，须 &gt; 0。</summary>
    public decimal Price { get; private set; }

    /// <summary>套餐时长（天），须 &gt; 0。</summary>
    public int DurationDays { get; private set; }

    /// <summary>套餐权益描述（JSON 文本）。</summary>
    public string Benefits { get; private set; } = string.Empty;

    /// <summary>套餐状态。</summary>
    public PackageStatus Status { get; private set; }

    /// <summary>EF Core 无参构造。</summary>
    private MembershipPackage() { }

    private MembershipPackage(Guid id) : base(id) { }

    /// <summary>
    /// 工厂方法，校验各字段合法性，初始状态为 Enabled。
    /// </summary>
    /// <param name="packageId">套餐标识，由应用层生成。</param>
    /// <param name="name">套餐名称。</param>
    /// <param name="level">对应会员等级编号，须 &gt; 0。</param>
    /// <param name="price">套餐价格，须 &gt; 0。</param>
    /// <param name="durationDays">套餐时长（天），须 &gt; 0。</param>
    /// <param name="benefits">权益描述（JSON 文本）。</param>
    public static MembershipPackage Create(
        Guid packageId,
        string name,
        int level,
        decimal price,
        int durationDays,
        string benefits)
    {
        Validate(name, level, price, durationDays, benefits);

        return new MembershipPackage(packageId == Guid.Empty ? Guid.NewGuid() : packageId)
        {
            Name = name,
            Level = level,
            Price = price,
            DurationDays = durationDays,
            Benefits = benefits,
            Status = PackageStatus.Enabled
        };
    }

    /// <summary>
    /// 更新套餐可编辑字段。
    /// </summary>
    /// <param name="name">套餐名称。</param>
    /// <param name="level">对应会员等级编号，须 &gt; 0。</param>
    /// <param name="price">套餐价格，须 &gt; 0。</param>
    /// <param name="durationDays">套餐时长（天），须 &gt; 0。</param>
    /// <param name="benefits">权益描述（JSON 文本）。</param>
    public void Update(
        string name,
        int level,
        decimal price,
        int durationDays,
        string benefits)
    {
        Validate(name, level, price, durationDays, benefits);

        Name = name;
        Level = level;
        Price = price;
        DurationDays = durationDays;
        Benefits = benefits;
    }

    /// <summary>启用套餐。</summary>
    public void Enable()
    {
        if (Status == PackageStatus.Enabled)
        {
            throw new MembershipDomainException("套餐已启用", "PACKAGE_ALREADY_ENABLED");
        }

        Status = PackageStatus.Enabled;
    }

    /// <summary>停用套餐，停用后不可购买。</summary>
    public void Disable()
    {
        if (Status == PackageStatus.Disabled)
        {
            throw new MembershipDomainException("套餐已停用", "PACKAGE_ALREADY_DISABLED");
        }

        Status = PackageStatus.Disabled;
    }

    private static void Validate(string name, int level, decimal price, int durationDays, string benefits)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new MembershipDomainException("套餐名称不可为空", "PACKAGE_NAME_EMPTY");
        }

        if (level <= 0)
        {
            throw new MembershipDomainException("套餐等级编号须大于 0", "PACKAGE_LEVEL_INVALID");
        }

        if (price <= 0)
        {
            throw new MembershipDomainException("套餐价格须大于 0", "PACKAGE_PRICE_INVALID");
        }

        if (durationDays <= 0)
        {
            throw new MembershipDomainException("套餐时长须大于 0", "PACKAGE_DURATION_INVALID");
        }

        if (string.IsNullOrWhiteSpace(benefits))
        {
            throw new MembershipDomainException("套餐权益不可为空", "PACKAGE_BENEFITS_EMPTY");
        }
    }
}
