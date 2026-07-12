using Leno.SystemAdmin.Domain.Exceptions;
using Leno.SystemAdmin.Domain.ValueObjects;
using Leno.SharedKernel.Abstractions;

namespace Leno.SystemAdmin.Domain.Aggregates;

/// <summary>
/// 对账记录聚合根，记录每次统计对账的快照结果。
/// 对账记录生成后不可变，仅追加不可修改。聚合标识 <see cref="Entity.Id"/> 即对外 <c>RecordId</c>。
/// </summary>
public sealed class ReconciliationRecord : AggregateRoot
{
    /// <summary>聚合标识，等同 <see cref="Entity.Id"/>。</summary>
    public Guid RecordId => Id;

    /// <summary>对账的报表类型。</summary>
    public ReportType ReportType { get; private set; }

    /// <summary>对账快照。</summary>
    public StatisticsSnapshot Snapshot { get; private set; } = default!;

    /// <summary>对账执行时间（UTC）。</summary>
    public DateTime ReconciledAt { get; private set; }

    /// <summary>对账状态。</summary>
    public ReconciliationStatus Status { get; private set; }

    /// <summary>是否触发告警。</summary>
    public bool AlertTriggered { get; private set; }

    /// <summary>是否触发自动修正。</summary>
    public bool CorrectionTriggered { get; private set; }

    /// <summary>EF Core 无参构造。</summary>
    private ReconciliationRecord() { }

    private ReconciliationRecord(Guid id) : base(id) { }

    /// <summary>
    /// 工厂方法，创建对账记录。
    /// </summary>
    /// <param name="recordId">对账记录标识，由应用层生成。</param>
    /// <param name="snapshot">对账快照。</param>
    public static ReconciliationRecord Create(
        Guid recordId,
        StatisticsSnapshot snapshot)
    {
        if (recordId == Guid.Empty)
        {
            throw new SystemAdminDomainException("对账记录标识不可为空", "RECONCILIATION_RECORD_ID_EMPTY");
        }

        ArgumentNullException.ThrowIfNull(snapshot);

        return new ReconciliationRecord(recordId)
        {
            ReportType = snapshot.ReportType,
            Snapshot = snapshot,
            ReconciledAt = DateTime.UtcNow,
            Status = snapshot.Status,
            AlertTriggered = false,
            CorrectionTriggered = false
        };
    }

    /// <summary>
    /// 标记告警已触发。
    /// </summary>
    public void MarkAlertTriggered()
    {
        AlertTriggered = true;
    }

    /// <summary>
    /// 标记自动修正已触发。
    /// </summary>
    public void MarkCorrectionTriggered()
    {
        CorrectionTriggered = true;
    }
}