using Leno.SystemAdmin.Domain.Aggregates;
using Leno.SystemAdmin.Domain.ValueObjects;

namespace Leno.SystemAdmin.Domain.Services;

/// <summary>
/// 运营数据统计聚合服务接口，定义在领域层，由基础设施层实现。
/// 负责从各数据源聚合指标数据，生成运营数据看板报表。
/// </summary>
public interface IStatisticsAggregationService
{
    /// <summary>
    /// 按报表类型与时间周期聚合运营数据，生成报表。
    /// </summary>
    /// <param name="reportType">报表类型。</param>
    /// <param name="period">报表覆盖的时间周期。</param>
    /// <param name="ct">取消令牌。</param>
    Task<DashboardReport> AggregateAsync(ReportType reportType, ReportPeriod period, CancellationToken ct = default);
}