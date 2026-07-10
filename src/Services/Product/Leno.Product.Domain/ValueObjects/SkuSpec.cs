using Leno.SharedKernel.ValueObjects;

namespace Leno.Product.Domain.ValueObjects;

/// <summary>
/// SKU 规格属性集合值对象，封装一组 <see cref="SpecAttribute"/> 并保证非空与组合唯一性校验入口。
/// 不可变，通过工厂方法创建；相等性由规格属性集合整体序列化比对。
/// </summary>
public sealed record SkuSpec
{
    /// <summary>规格属性集合，至少 1 项。</summary>
    public IReadOnlyList<SpecAttribute> Attributes { get; private set; } = Array.Empty<SpecAttribute>();

    private SkuSpec() { }

    private SkuSpec(IReadOnlyList<SpecAttribute> attributes)
    {
        Attributes = attributes;
    }

    /// <summary>
    /// 创建 SKU 规格集合，要求至少 1 项规格属性。
    /// </summary>
    /// <param name="attributes">规格属性集合。</param>
    public static SkuSpec Create(IEnumerable<SpecAttribute>? attributes)
    {
        if (attributes is null)
        {
            throw new ArgumentException("规格属性集合不可为空", nameof(attributes));
        }

        var list = attributes.ToList();
        if (list.Count == 0)
        {
            throw new ArgumentException("SKU 规格属性至少 1 项", nameof(attributes));
        }

        return new SkuSpec(list);
    }

    /// <summary>
    /// 比较两个规格集合是否等价（按 Name+Value 联合判定，忽略顺序）。
    /// </summary>
    public bool Equals(SkuSpec? other)
    {
        if (other is null)
        {
            return false;
        }

        if (Attributes.Count != other.Attributes.Count)
        {
            return false;
        }

        var self = Attributes.Select(a => (a.Name, a.Value)).OrderBy(x => x.Name).ThenBy(x => x.Value);
        var that = other.Attributes.Select(a => (a.Name, a.Value)).OrderBy(x => x.Name).ThenBy(x => x.Value);
        return self.SequenceEqual(that);
    }

    public override int GetHashCode()
    {
        var hash = new HashCode();
        foreach (var attr in Attributes.OrderBy(a => a.Name).ThenBy(a => a.Value))
        {
            hash.Add(attr.Name);
            hash.Add(attr.Value);
        }

        return hash.ToHashCode();
    }
}
