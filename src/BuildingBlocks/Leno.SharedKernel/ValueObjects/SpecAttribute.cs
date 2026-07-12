using System.Diagnostics.CodeAnalysis;
using System.Text.Json.Serialization;

namespace Leno.SharedKernel.ValueObjects;

/// <summary>
/// 商品规格属性值对象（Name + Value），商品域与购物车域复用。
/// 不可变，通过工厂方法创建。
/// </summary>
[SuppressMessage("Naming", "CA1711", Justification = "SpecAttribute 为领域统一语言的规格属性值对象，非 System.Attribute 子类。")]
public sealed record SpecAttribute
{
    public string Name { get; init; } = default!;

    public string Value { get; init; } = default!;

    [JsonConstructor]
    private SpecAttribute() { }

    private SpecAttribute(string name, string value)
    {
        Name = name;
        Value = value;
    }

    public static SpecAttribute Create(string name, string value)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException("规格名不可为空", nameof(name));
        }

        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException("规格值不可为空", nameof(value));
        }

        return new SpecAttribute(name.Trim(), value.Trim());
    }

    public override string ToString() => $"{Name}: {Value}";
}
