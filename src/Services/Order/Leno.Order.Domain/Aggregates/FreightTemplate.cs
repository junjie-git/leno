using Leno.Order.Domain.Exceptions;
using Leno.Order.Domain.ValueObjects;
using Leno.SharedKernel.Abstractions;

namespace Leno.Order.Domain.Aggregates;

/// <summary>
/// 运费模板聚合根，按卖家维度配置区域运费规则与包邮门槛。
/// 聚合标识 <see cref="Entity.Id"/> 即对外 <c>FreightTemplateId</c>。
/// </summary>
public sealed class FreightTemplate : AggregateRoot
{
    /// <summary>模板名称。</summary>
    public string Name { get; private set; } = string.Empty;

    /// <summary>计价类型（按重量/按件）。</summary>
    public FreightTemplateType Type { get; private set; }

    /// <summary>包邮门槛金额，订单金额 ≥ 此值则免运费，为空表示不包邮。</summary>
    public decimal? FreeShippingThreshold { get; private set; }

    /// <summary>
    /// 区域运费规则集合，仅经聚合根 <see cref="UpdateRules"/> 维护。
    /// 持久化为聚合子集合，故以可赋值 List 暴露给 EF Core，私有 setter 阻止外部整体替换。
    /// </summary>
    public List<FreightRegionRule> RegionRules { get; private set; } = new();

    /// <summary>卖家（店铺）标识。</summary>
    public Guid SellerId { get; private set; }

    /// <summary>启停状态。</summary>
    public FreightTemplateStatus Status { get; private set; }

    /// <summary>EF Core 无参构造。</summary>
    private FreightTemplate() { }

    private FreightTemplate(Guid id) : base(id) { }

    /// <summary>
    /// 工厂方法，校验卖家非空、名称非空，初始状态为 Enabled，区域规则为空集合。
    /// </summary>
    /// <param name="id">模板标识，由应用层生成。</param>
    /// <param name="sellerId">卖家标识。</param>
    /// <param name="name">模板名称。</param>
    /// <param name="type">计价类型。</param>
    /// <param name="freeShippingThreshold">包邮门槛，可为空。</param>
    public static FreightTemplate Create(Guid id, Guid sellerId, string name, FreightTemplateType type, decimal? freeShippingThreshold)
    {
        if (sellerId == Guid.Empty)
        {
            throw new OrderDomainException("SellerId 不可为空", "FREIGHT_SELLER_EMPTY");
        }

        if (string.IsNullOrWhiteSpace(name))
        {
            throw new OrderDomainException("运费模板名称不可为空", "FREIGHT_NAME_EMPTY");
        }

        if (freeShippingThreshold.HasValue && freeShippingThreshold.Value < 0)
        {
            throw new OrderDomainException("包邮门槛不可为负", "FREIGHT_FREE_THRESHOLD_INVALID");
        }

        return new FreightTemplate(id == Guid.Empty ? Guid.NewGuid() : id)
        {
            Name = name,
            Type = type,
            FreeShippingThreshold = freeShippingThreshold,
            SellerId = sellerId,
            Status = FreightTemplateStatus.Enabled
        };
    }

    /// <summary>
    /// 更新区域运费规则集合，整体替换为传入列表。
    /// </summary>
    /// <param name="rules">区域规则列表，须非空引用。</param>
    public void UpdateRules(List<FreightRegionRule> rules)
    {
        if (rules is null)
        {
            throw new OrderDomainException("区域运费规则列表不可为空", "FREIGHT_RULES_EMPTY");
        }

        RegionRules = rules;
    }

    /// <summary>启用运费模板。</summary>
    public void Enable()
    {
        if (Status == FreightTemplateStatus.Enabled)
        {
            throw new OrderDomainException("运费模板已启用", "FREIGHT_ALREADY_ENABLED");
        }

        Status = FreightTemplateStatus.Enabled;
    }

    /// <summary>停用运费模板，停用后下单不再引用。</summary>
    public void Disable()
    {
        if (Status == FreightTemplateStatus.Disabled)
        {
            throw new OrderDomainException("运费模板已停用", "FREIGHT_ALREADY_DISABLED");
        }

        Status = FreightTemplateStatus.Disabled;
    }

    /// <summary>
    /// 按区域与数量计算运费：满足包邮门槛返回 0；命中区域规则按首件+续件阶梯计价；未命中返回 0。
    /// </summary>
    /// <param name="regionCode">区域编码。</param>
    /// <param name="quantity">计价数量（件数或重量）。</param>
    /// <param name="orderAmount">订单金额，用于判断包邮。</param>
    /// <returns>运费金额。</returns>
    public decimal CalculateFreight(string regionCode, int quantity, decimal orderAmount)
    {
        // 边界校验：订单金额不可为负（负值无法判断包邮门槛）
        if (orderAmount < 0)
        {
            throw new OrderDomainException("订单金额不可为负", "FREIGHT_ORDER_AMOUNT_INVALID");
        }

        // 边界校验：数量为 0 或负值时直接返回 0 运费，避免误命中 FirstUnit 阈值返回 FirstPrice
        if (quantity <= 0)
        {
            return 0;
        }

        if (FreeShippingThreshold.HasValue && orderAmount >= FreeShippingThreshold.Value)
        {
            return 0;
        }

        var rule = RegionRules.FirstOrDefault(r => r.RegionCode == regionCode);
        if (rule is null)
        {
            return 0;
        }

        if (quantity <= rule.FirstUnit)
        {
            return rule.FirstPrice;
        }

        var extra = quantity - rule.FirstUnit;
        var additionalSteps = (int)Math.Ceiling((double)extra / rule.AdditionalUnit);
        return rule.FirstPrice + additionalSteps * rule.AdditionalPrice;
    }
}
