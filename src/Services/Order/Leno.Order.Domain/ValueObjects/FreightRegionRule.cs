using Leno.Order.Domain.Exceptions;

namespace Leno.Order.Domain.ValueObjects;

/// <summary>
/// 区域运费规则值对象，表达“首 <see cref="FirstUnit"/> 件 <see cref="FirstPrice"/> 元，每续 <see cref="AdditionalUnit"/> 件 <see cref="AdditionalPrice"/> 元”。
/// 不可变，按值相等，隶属于 <see cref="Aggregates.FreightTemplate"/> 的区域规则集合。
/// </summary>
public sealed record FreightRegionRule
{
    /// <summary>区域编码（省/市/区编码）。</summary>
    public string RegionCode { get; init; } = string.Empty;

    /// <summary>首件数量，须 &gt; 0。</summary>
    public int FirstUnit { get; init; }

    /// <summary>首件运费，须 ≥ 0。</summary>
    public decimal FirstPrice { get; init; }

    /// <summary>续件单位数量，须 &gt; 0。</summary>
    public int AdditionalUnit { get; init; }

    /// <summary>续件单位运费，须 ≥ 0。</summary>
    public decimal AdditionalPrice { get; init; }

    /// <summary>
    /// 供 EF Core 与反序列化使用的无参构造（P1-T22 改为 private）。
    /// 强制外部经 <see cref="Create"/> 工厂方法构造合法对象，避免 new FreightRegionRule() 创建 FirstUnit=0/AdditionalUnit=0 的非法实例。
    /// EF Core 通过反射访问 private 构造完成实体物化。
    /// </summary>
    private FreightRegionRule() { }

    private FreightRegionRule(string regionCode, int firstUnit, decimal firstPrice, int additionalUnit, decimal additionalPrice)
    {
        RegionCode = regionCode;
        FirstUnit = firstUnit;
        FirstPrice = firstPrice;
        AdditionalUnit = additionalUnit;
        AdditionalPrice = additionalPrice;
    }

    /// <summary>
    /// 工厂方法，校验区域编码非空、首件/续件单位 &gt; 0、运费 ≥ 0。
    /// </summary>
    /// <param name="regionCode">区域编码。</param>
    /// <param name="firstUnit">首件数量，须 &gt; 0。</param>
    /// <param name="firstPrice">首件运费，须 ≥ 0。</param>
    /// <param name="additionalUnit">续件单位数量，须 &gt; 0。</param>
    /// <param name="additionalPrice">续件单位运费，须 ≥ 0。</param>
    public static FreightRegionRule Create(
        string regionCode,
        int firstUnit,
        decimal firstPrice,
        int additionalUnit,
        decimal additionalPrice)
    {
        if (string.IsNullOrWhiteSpace(regionCode))
        {
            throw new OrderDomainException("区域编码不可为空", "FREIGHT_REGION_CODE_EMPTY");
        }

        if (firstUnit <= 0)
        {
            throw new OrderDomainException("首件数量须大于 0", "FREIGHT_FIRST_UNIT_INVALID");
        }

        if (firstPrice < 0)
        {
            throw new OrderDomainException("首件运费不可为负", "FREIGHT_FIRST_PRICE_INVALID");
        }

        if (additionalUnit <= 0)
        {
            throw new OrderDomainException("续件单位数量须大于 0", "FREIGHT_ADDITIONAL_UNIT_INVALID");
        }

        if (additionalPrice < 0)
        {
            throw new OrderDomainException("续件单位运费不可为负", "FREIGHT_ADDITIONAL_PRICE_INVALID");
        }

        return new FreightRegionRule(regionCode, firstUnit, firstPrice, additionalUnit, additionalPrice);
    }
}
