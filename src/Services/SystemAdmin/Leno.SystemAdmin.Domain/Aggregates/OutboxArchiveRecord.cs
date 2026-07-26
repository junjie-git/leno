using Leno.SystemAdmin.Domain.Exceptions;
using Leno.SharedKernel.Abstractions;

namespace Leno.SystemAdmin.Domain.Aggregates;

/// <summary>
/// Outbox 归档历史聚合根，记录对某域陈旧积压事件的归档操作审计。
/// 持久化在 SystemAdmin 库（outbox_archive_records 表），支持归档历史查询与回溯。
/// </summary>
public sealed class OutboxArchiveRecord : AggregateRoot
{
    private const int MaxContextLength = 128;
    private const int MaxArchivedByLength = 64;
    private const int MaxReasonLength = 1000;

    /// <summary>归档所属限界上下文，如 Order。</summary>
    public string Context { get; private set; } = string.Empty;

    /// <summary>归档事件数量。</summary>
    public int ArchivedCount { get; private set; }

    /// <summary>归档阈值：CreatedAt 早于此时间的积压事件被归档（UTC）。</summary>
    public DateTime ArchivedBefore { get; private set; }

    /// <summary>归档时间（UTC）。</summary>
    public DateTime ArchivedAt { get; private set; }

    /// <summary>归档操作人标识。</summary>
    public string ArchivedBy { get; private set; } = string.Empty;

    /// <summary>归档原因。</summary>
    public string Reason { get; private set; } = string.Empty;

    /// <summary>EF Core 无参构造。</summary>
    private OutboxArchiveRecord() { }

    private OutboxArchiveRecord(Guid id) : base(id) { }

    /// <summary>
    /// 工厂方法，校验字段并构建归档历史记录。
    /// </summary>
    /// <param name="id">记录标识。</param>
    /// <param name="context">归档所属限界上下文。</param>
    /// <param name="archivedCount">归档事件数量。</param>
    /// <param name="archivedBefore">归档阈值（UTC）。</param>
    /// <param name="archivedAt">归档时间（UTC）。</param>
    /// <param name="archivedBy">归档操作人标识。</param>
    /// <param name="reason">归档原因。</param>
    public static OutboxArchiveRecord Create(
        Guid id,
        string context,
        int archivedCount,
        DateTime archivedBefore,
        DateTime archivedAt,
        string archivedBy,
        string reason)
    {
        if (id == Guid.Empty)
        {
            throw new SystemAdminDomainException("归档记录标识不可为空", "OUTBOX_ARCHIVE_ID_EMPTY");
        }
        ValidateContext(context);
        ValidateArchivedCount(archivedCount);
        ValidateArchivedBy(archivedBy);
        ValidateReason(reason);

        return new OutboxArchiveRecord(id)
        {
            Context = context.Trim(),
            ArchivedCount = archivedCount,
            ArchivedBefore = archivedBefore,
            ArchivedAt = archivedAt,
            ArchivedBy = archivedBy.Trim(),
            Reason = reason.Trim()
        };
    }

    private static void ValidateContext(string context)
    {
        if (string.IsNullOrWhiteSpace(context))
        {
            throw new SystemAdminDomainException("归档所属上下文不可为空", "OUTBOX_ARCHIVE_CONTEXT_EMPTY");
        }
        if (context.Trim().Length > MaxContextLength)
        {
            throw new SystemAdminDomainException($"归档所属上下文长度不可超过 {MaxContextLength} 字符", "OUTBOX_ARCHIVE_CONTEXT_LENGTH");
        }
    }

    private static void ValidateArchivedCount(int archivedCount)
    {
        if (archivedCount < 0)
        {
            throw new SystemAdminDomainException("归档事件数量不可为负数", "OUTBOX_ARCHIVE_COUNT_NEGATIVE");
        }
    }

    private static void ValidateArchivedBy(string archivedBy)
    {
        if (string.IsNullOrWhiteSpace(archivedBy))
        {
            throw new SystemAdminDomainException("归档操作人标识不可为空", "OUTBOX_ARCHIVE_BY_EMPTY");
        }
        if (archivedBy.Trim().Length > MaxArchivedByLength)
        {
            throw new SystemAdminDomainException($"归档操作人标识长度不可超过 {MaxArchivedByLength} 字符", "OUTBOX_ARCHIVE_BY_LENGTH");
        }
    }

    private static void ValidateReason(string reason)
    {
        if (string.IsNullOrWhiteSpace(reason))
        {
            throw new SystemAdminDomainException("归档原因不可为空", "OUTBOX_ARCHIVE_REASON_EMPTY");
        }
        if (reason.Trim().Length > MaxReasonLength)
        {
            throw new SystemAdminDomainException($"归档原因长度不可超过 {MaxReasonLength} 字符", "OUTBOX_ARCHIVE_REASON_LENGTH");
        }
    }
}
