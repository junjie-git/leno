namespace Leno.SystemAdmin.Domain.ValueObjects;

/// <summary>单个监控指标数据点。</summary>
public sealed class MetricPointDto
{
    public DateTime Timestamp { get; set; }
    public double Value { get; set; }
}
