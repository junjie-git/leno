using Leno.SystemAdmin.Domain.Exceptions;

namespace Leno.SystemAdmin.Domain.ValueObjects;

/// <summary>
/// 报表时间周期值对象，表示报表覆盖的起止时间范围。
/// 不可变记录，Start 必须早于 End。
/// </summary>
public sealed record ReportPeriod
{
    /// <summary>周期起始时间（UTC）。</summary>
    public DateTime Start { get; }

    /// <summary>周期结束时间（UTC）。</summary>
    public DateTime End { get; }

    public ReportPeriod(DateTime start, DateTime end)
    {
        if (start == default)
        {
            throw new SystemAdminDomainException("报表周期起始时间不可为空", "REPORT_PERIOD_START_EMPTY");
        }

        if (end == default)
        {
            throw new SystemAdminDomainException("报表周期结束时间不可为空", "REPORT_PERIOD_END_EMPTY");
        }

        if (start >= end)
        {
            throw new SystemAdminDomainException("报表周期起始时间必须早于结束时间", "REPORT_PERIOD_INVALID");
        }

        Start = start;
        End = end;
    }
}