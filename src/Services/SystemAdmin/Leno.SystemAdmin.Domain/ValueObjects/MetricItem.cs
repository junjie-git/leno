using Leno.SystemAdmin.Domain.Exceptions;

namespace Leno.SystemAdmin.Domain.ValueObjects;

/// <summary>
/// 指标项值对象，表示报表中的单个度量指标。
/// 不可变记录，包含键、值和单位。
/// </summary>
public sealed record MetricItem
{
    private const int MaxKeyLength = 128;
    private const int MaxUnitLength = 32;

    /// <summary>指标键，如 "total_gmv", "success_rate"。</summary>
    public string Key { get; }

    /// <summary>指标数值。</summary>
    public decimal Value { get; }

    /// <summary>指标单位，如 "CNY", "%", "次"。</summary>
    public string Unit { get; }

    public MetricItem(string key, decimal value, string unit)
    {
        if (string.IsNullOrWhiteSpace(key))
        {
            throw new SystemAdminDomainException("指标键不可为空", "METRIC_KEY_EMPTY");
        }

        if (key.Trim().Length > MaxKeyLength)
        {
            throw new SystemAdminDomainException($"指标键长度不可超过 {MaxKeyLength} 字符", "METRIC_KEY_LENGTH");
        }

        if (string.IsNullOrWhiteSpace(unit))
        {
            throw new SystemAdminDomainException("指标单位不可为空", "METRIC_UNIT_EMPTY");
        }

        if (unit.Trim().Length > MaxUnitLength)
        {
            throw new SystemAdminDomainException($"指标单位长度不可超过 {MaxUnitLength} 字符", "METRIC_UNIT_LENGTH");
        }

        Key = key.Trim();
        Value = value;
        Unit = unit.Trim();
    }
}