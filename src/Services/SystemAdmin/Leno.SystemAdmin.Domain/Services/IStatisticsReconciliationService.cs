using Leno.SystemAdmin.Domain.Aggregates;
using Leno.SystemAdmin.Domain.ValueObjects;

namespace Leno.SystemAdmin.Domain.Services;

/// <summary>
/// 统计数据对账服务接口，定义在领域层，由基础设施层实现。
/// 负责比对 SystemAdmin 聚合统计与各域事件溯源统计，生成对账记录。
/// SystemAdmin 域仅以只读方式消费各域集成事件，不写回任何域的写库。
/// </summary>
public interface IStatisticsReconciliationService
{
    /// <summary>
    /// 执行全量对账：对所有报表类型按指定时间周期进行聚合数据与域数据的比对。
    /// 返回对账记录列表。
    /// </summary>
    /// <param name="period">对账覆盖的时间周期。</param>
    /// <param name="ct">取消令牌。</param>
    Task<List<ReconciliationRecord>> ReconcileAllAsync(ReportPeriod period, CancellationToken ct = default);

    /// <summary>
    /// 对指定报表类型执行对账。
    /// </summary>
    /// <param name="reportType">报表类型。</param>
    /// <param name="period">对账覆盖的时间周期。</param>
    /// <param name="ct">取消令牌。</param>
    Task<ReconciliationRecord> ReconcileAsync(ReportType reportType, ReportPeriod period, CancellationToken ct = default);
}