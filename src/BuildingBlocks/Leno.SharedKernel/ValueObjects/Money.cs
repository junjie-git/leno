using System.Globalization;

namespace Leno.SharedKernel.ValueObjects;

/// <summary>
/// 金额值对象，金额与币种（ISO 4217），四舍五入到两位小数。
/// 所有上下文统一货币运算，不可变。
/// </summary>
public sealed record Money : IComparable<Money>
{
    private const int Scale = 2;

    // T29：改为 init 以支持 EF Core 反序列化与 JSON 反序列化，
    // 同时保持不可变性（init 仅在构造阶段可赋值，构造完成后对外只读）。
    public decimal Amount { get; init; }

    public string Currency { get; init; } = default!;

    private Money() { }

    private Money(decimal amount, string currency)
    {
        Amount = Math.Round(amount, Scale, MidpointRounding.AwayFromZero);
        Currency = currency;
    }

    public static Money Create(decimal amount, string currency)
    {
        if (string.IsNullOrWhiteSpace(currency))
        {
            throw new ArgumentException("币种不可为空", nameof(currency));
        }

        if (amount < 0)
        {
            throw new ArgumentException("金额不可为负", nameof(amount));
        }

        var normalized = currency.Trim().ToUpperInvariant();
        // T30：ISO 4217 币种码固定为 3 位大写字母，原 `is < 3 or > 3` 等价于 != 3 但可读性差。
        if (normalized.Length != 3)
        {
            throw new ArgumentException("币种须为 3 位 ISO 4217 代码", nameof(currency));
        }

        return new Money(amount, normalized);
    }

    public static Money Zero(string currency) => Create(0, currency);

    public Money Add(Money other)
    {
        AssertSameCurrency(other);
        return new Money(Amount + other.Amount, Currency);
    }

    public Money Subtract(Money other)
    {
        AssertSameCurrency(other);
        return new Money(Amount - other.Amount, Currency);
    }

    public Money Multiply(int factor) => new(Amount * factor, Currency);

    public Money Multiply(decimal factor) => new(Amount * factor, Currency);

    public static Money Sum(IEnumerable<Money> source)
    {
        ArgumentNullException.ThrowIfNull(source);

        var list = source.ToList();
        if (list.Count == 0)
        {
            throw new ArgumentException("集合不可为空", nameof(source));
        }

        var currency = list[0].Currency;
        var total = 0m;
        foreach (var money in list)
        {
            if (money.Currency != currency)
            {
                throw new InvalidOperationException($"币种不匹配: {currency} vs {money.Currency}");
            }

            total += money.Amount;
        }

        return new Money(total, currency);
    }

    private void AssertSameCurrency(Money other)
    {
        if (!string.Equals(Currency, other.Currency, StringComparison.Ordinal))
        {
            throw new InvalidOperationException($"币种不匹配: {Currency} vs {other.Currency}");
        }
    }

    public int CompareTo(Money? other)
    {
        if (other is null)
        {
            return 1;
        }

        AssertSameCurrency(other);
        return Amount.CompareTo(other.Amount);
    }

    public override string ToString() => $"{Amount.ToString("F2", CultureInfo.InvariantCulture)} {Currency}";

    public static Money operator +(Money left, Money right) => left.Add(right);

    public static Money operator -(Money left, Money right) => left.Subtract(right);

    public static Money operator *(Money money, int factor) => money.Multiply(factor);

    public static Money operator *(Money money, decimal factor) => money.Multiply(factor);

    public static bool operator <(Money left, Money right) => left.CompareTo(right) < 0;

    public static bool operator >(Money left, Money right) => left.CompareTo(right) > 0;

    public static bool operator <=(Money left, Money right) => left.CompareTo(right) <= 0;

    public static bool operator >=(Money left, Money right) => left.CompareTo(right) >= 0;
}
