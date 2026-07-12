using Leno.Payment.Application.DTOs;
using Leno.Payment.Domain.ValueObjects;

namespace Leno.Payment.Application;

/// <summary>
/// 对账应用服务，编排对账差异查询与手动触发对账用例。
/// </summary>
public interface IReconciliationAppService
{
    /// <summary>
    /// 分页查询对账差异列表。
    /// </summary>
    Task<ReconciliationDiffListResultDto> QueryDiffsAsync(
        DateTime? billDate,
        PaymentChannel? channel,
        ReconciliationDiffType? diffType,
        ReconciliationDiffStatus? status,
        int page,
        int pageSize,
        CancellationToken ct = default);

    /// <summary>
    /// 手动触发对账（指定日期）。
    /// </summary>
    Task TriggerReconciliationAsync(DateTime billDate, CancellationToken ct = default);
}