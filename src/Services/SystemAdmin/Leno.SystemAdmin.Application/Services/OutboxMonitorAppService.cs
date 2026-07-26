using Leno.SystemAdmin.Application.DTOs;
using Leno.SystemAdmin.Domain.Aggregates;
using Leno.SystemAdmin.Domain.Repositories;
using Leno.SystemAdmin.Domain.Services;
using Leno.SharedKernel.Abstractions;
using Microsoft.Extensions.Logging;

namespace Leno.SystemAdmin.Application.Services;

/// <summary>
/// Outbox 监控应用服务实现。
/// 委托 <see cref="IOutboxQueryService"/> 跨域查询各域 outbox_messages 表，
/// 委托 <see cref="IOutboxArchiveRecordRepository"/> 持久化归档历史。
/// </summary>
public sealed class OutboxMonitorAppService : IOutboxMonitorAppService
{
    private const int MaxPageSize = 200;
    private const int MaxTrendHours = 168;
    private const int MinReasonLength = 1;
    private const int MaxReasonLength = 1000;

    private readonly IOutboxQueryService _outboxQueryService;
    private readonly IOutboxArchiveRecordRepository _archiveRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<OutboxMonitorAppService> _logger;

    public OutboxMonitorAppService(
        IOutboxQueryService outboxQueryService,
        IOutboxArchiveRecordRepository archiveRepository,
        IUnitOfWork unitOfWork,
        ILogger<OutboxMonitorAppService> logger)
    {
        ArgumentNullException.ThrowIfNull(outboxQueryService);
        ArgumentNullException.ThrowIfNull(archiveRepository);
        ArgumentNullException.ThrowIfNull(unitOfWork);
        ArgumentNullException.ThrowIfNull(logger);
        _outboxQueryService = outboxQueryService;
        _archiveRepository = archiveRepository;
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<List<OutboxContextSummaryDto>> GetSummaryAsync(CancellationToken ct = default)
    {
        var summaries = await _outboxQueryService.GetSummaryAsync(ct);

        _logger.LogInformation("获取 Outbox 积压汇总 域数={Count}", summaries.Count);

        return summaries.Select(ToDto).ToList();
    }

    /// <inheritdoc />
    public async Task<List<OutboxTrendPointDto>> GetTrendAsync(int hours = 24, CancellationToken ct = default)
    {
        var normalizedHours = NormalizeHours(hours);

        var points = await _outboxQueryService.GetTrendAsync(normalizedHours, ct);

        _logger.LogInformation("获取 Outbox 积压趋势 Hours={Hours} Points={Count}", normalizedHours, points.Count);

        return points.Select(ToDto).ToList();
    }

    /// <inheritdoc />
    public async Task<OutboxMessageListResultDto> GetMessagesAsync(
        string context,
        string? status,
        int page,
        int pageSize,
        CancellationToken ct = default)
    {
        ValidateContext(context);
        var normalizedPage = NormalizePage(page);
        var normalizedPageSize = NormalizePageSize(pageSize);

        var result = await _outboxQueryService.GetMessagesAsync(
            context.Trim(),
            string.IsNullOrWhiteSpace(status) ? null : status.Trim(),
            normalizedPage,
            normalizedPageSize,
            ct);

        _logger.LogInformation(
            "查询 Outbox 积压事件 Context={Context} Status={Status} Page={Page} PageSize={PageSize} Total={Total}",
            context, status, normalizedPage, normalizedPageSize, result.Total);

        return new OutboxMessageListResultDto
        {
            Items = result.Items.Select(ToDto).ToList(),
            Total = result.Total,
            Page = normalizedPage,
            PageSize = normalizedPageSize
        };
    }

    /// <inheritdoc />
    public async Task<OutboxRepublishResultDto> RepublishAsync(
        string context,
        List<Guid>? messageIds,
        string operatorId,
        CancellationToken ct = default)
    {
        ValidateContext(context);
        if (string.IsNullOrWhiteSpace(operatorId))
        {
            throw new ArgumentException("操作者标识不可为空", nameof(operatorId));
        }

        IReadOnlyCollection<Guid>? ids = messageIds is null || messageIds.Count == 0
            ? null
            : messageIds.Distinct().ToList();

        var result = await _outboxQueryService.RepublishAsync(context.Trim(), ids, operatorId, ct);

        _logger.LogInformation(
            "重投 Outbox 积压事件 Context={Context} OperatorId={OperatorId} Success={Success} Failure={Failure}",
            context, operatorId, result.SuccessCount, result.FailureCount);

        return new OutboxRepublishResultDto
        {
            SuccessCount = result.SuccessCount,
            FailureCount = result.FailureCount,
            Errors = result.Errors.Select(e => new OutboxRepublishErrorDto
            {
                MessageId = e.MessageId,
                Error = e.Error
            }).ToList()
        };
    }

    /// <inheritdoc />
    public async Task<OutboxArchiveResultDto> ArchiveAsync(
        string context,
        DateTime before,
        string operatorId,
        string reason,
        CancellationToken ct = default)
    {
        ValidateContext(context);
        if (string.IsNullOrWhiteSpace(operatorId))
        {
            throw new ArgumentException("操作者标识不可为空", nameof(operatorId));
        }
        ValidateReason(reason);

        if (before == default || before > DateTime.UtcNow)
        {
            throw new ArgumentException("归档阈值必须为过去的有效时间", nameof(before));
        }

        var archivedCount = await _outboxQueryService.ArchiveAsync(context.Trim(), before, operatorId, reason, ct);

        var record = OutboxArchiveRecord.Create(
            Guid.NewGuid(),
            context.Trim(),
            archivedCount,
            before,
            DateTime.UtcNow,
            operatorId,
            reason);

        await _archiveRepository.AddAsync(record, ct);
        await _unitOfWork.SaveEntitiesAsync(ct);

        _logger.LogInformation(
            "归档 Outbox 积压事件 Context={Context} Before={Before} ArchivedCount={Count} OperatorId={OperatorId} RecordId={RecordId}",
            context, before, archivedCount, operatorId, record.Id);

        return new OutboxArchiveResultDto
        {
            ArchivedCount = archivedCount,
            RecordId = record.Id
        };
    }

    /// <inheritdoc />
    public async Task<OutboxArchiveHistoryListResultDto> GetArchiveHistoryAsync(
        string context,
        int page,
        int pageSize,
        CancellationToken ct = default)
    {
        ValidateContext(context);
        var normalizedPage = NormalizePage(page);
        var normalizedPageSize = NormalizePageSize(pageSize);

        var items = await _archiveRepository.QueryAsync(context.Trim(), normalizedPage, normalizedPageSize, ct);
        var total = await _archiveRepository.CountAsync(context.Trim(), ct);

        _logger.LogInformation(
            "查询 Outbox 归档历史 Context={Context} Page={Page} PageSize={PageSize} Total={Total}",
            context, normalizedPage, normalizedPageSize, total);

        return new OutboxArchiveHistoryListResultDto
        {
            Items = items.Select(ToDto).ToList(),
            Total = total,
            Page = normalizedPage,
            PageSize = normalizedPageSize
        };
    }

    private static int NormalizePage(int page) => page < 1 ? 1 : page;

    private static int NormalizePageSize(int pageSize)
        => pageSize < 1 ? 20 : (pageSize > MaxPageSize ? MaxPageSize : pageSize);

    private static int NormalizeHours(int hours)
        => hours < 1 ? 24 : (hours > MaxTrendHours ? MaxTrendHours : hours);

    private static void ValidateContext(string context)
    {
        if (string.IsNullOrWhiteSpace(context))
        {
            throw new ArgumentException("限界上下文不可为空", nameof(context));
        }
        if (context.Trim().Length > 128)
        {
            throw new ArgumentException("限界上下文长度不可超过 128 字符", nameof(context));
        }
    }

    private static void ValidateReason(string reason)
    {
        if (string.IsNullOrWhiteSpace(reason))
        {
            throw new ArgumentException("归档原因不可为空", nameof(reason));
        }
        if (reason.Trim().Length > MaxReasonLength)
        {
            throw new ArgumentException($"归档原因长度不可超过 {MaxReasonLength} 字符", nameof(reason));
        }
        if (reason.Trim().Length < MinReasonLength)
        {
            throw new ArgumentException("归档原因不可为空", nameof(reason));
        }
    }

    private static OutboxContextSummaryDto ToDto(Domain.Services.OutboxContextSummary summary)
        => new()
        {
            Context = summary.Context,
            PendingCount = summary.PendingCount,
            OldestPendingAt = summary.OldestPendingAt,
            MaxAgeMinutes = summary.MaxAgeMinutes,
            LastArchivedAt = summary.LastArchivedAt,
            Status = summary.Status
        };

    private static OutboxTrendPointDto ToDto(Domain.Services.OutboxTrendPoint point)
        => new()
        {
            Timestamp = point.Timestamp,
            Context = point.Context,
            PendingCount = point.PendingCount
        };

    private static OutboxMessageDto ToDto(OutboxMessageEntry message)
        => new()
        {
            MessageId = message.Id,
            AggregateId = message.AggregateId,
            EventType = message.EventType,
            Payload = message.Payload,
            Status = message.Status,
            RetryCount = message.RetryCount,
            Error = message.Error,
            CreatedAt = message.CreatedAt,
            ProcessedAt = message.ProcessedAt
        };

    private static OutboxArchiveHistoryDto ToDto(OutboxArchiveRecord record)
        => new()
        {
            RecordId = record.Id,
            Context = record.Context,
            ArchivedCount = record.ArchivedCount,
            ArchivedBefore = record.ArchivedBefore,
            ArchivedAt = record.ArchivedAt,
            ArchivedBy = record.ArchivedBy,
            Reason = record.Reason
        };
}
