using Leno.SystemAdmin.Domain.ValueObjects;

namespace Leno.SystemAdmin.Domain.Services;

/// <summary>
/// 运营数据统计数据源接口，定义在领域层，由基础设施层实现。
/// 负责从各 BC 的只读模型（ES 索引、只读副本）聚合真实指标数据。
/// </summary>
public interface IStatisticsDataSource
{
    /// <summary>
    /// 按报表类型与时间周期从真实数据源获取指标列表。
    /// </summary>
    /// <param name="reportType">报表类型。</param>
    /// <param name="period">报表覆盖的时间周期。</param>
    /// <param name="ct">取消令牌。</param>
    /// <returns>指标项列表，不可为空。返回空列表时由调用方判定为异常。</returns>
    Task<List<MetricItem>> GetMetricsAsync(ReportType reportType, ReportPeriod period, CancellationToken ct = default);
}
