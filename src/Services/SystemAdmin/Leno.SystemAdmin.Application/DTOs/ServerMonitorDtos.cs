using Leno.SystemAdmin.Domain.ValueObjects;

namespace Leno.SystemAdmin.Application.DTOs;

/// <summary>
/// 服务器监控历史指标响应，对应前端 spec §3.7。
/// 直接复用领域层 <see cref="MetricPointDto"/>。
/// </summary>
public sealed class MetricHistoryDto
{
    /// <summary>指标名称：cpu / memory / disk-io。</summary>
    public string Metric { get; set; } = string.Empty;

    /// <summary>查询范围（秒）。</summary>
    public int RangeSeconds { get; set; }

    /// <summary>数据点列表（按时间升序）。</summary>
    public List<MetricPointDto> Points { get; set; } = [];
}
