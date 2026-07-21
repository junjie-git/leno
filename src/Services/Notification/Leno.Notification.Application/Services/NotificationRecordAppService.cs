using Leno.Notification.Application.DTOs;
using Leno.Notification.Domain.Aggregates;
using Leno.Notification.Domain.Repositories;
using Leno.Notification.Domain.ValueObjects;
using Leno.SharedKernel.Abstractions;
using Microsoft.Extensions.Logging;

namespace Leno.Notification.Application.Services;

/// <summary>
/// 通知记录查询应用服务实现（管理员端）。
/// </summary>
public sealed class NotificationRecordAppService : INotificationRecordAppService
{
    private readonly INotificationRecordRepository _recordRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<NotificationRecordAppService> _logger;

    public NotificationRecordAppService(
        INotificationRecordRepository recordRepository,
        IUnitOfWork unitOfWork,
        ILogger<NotificationRecordAppService> logger)
    {
        ArgumentNullException.ThrowIfNull(recordRepository);
        ArgumentNullException.ThrowIfNull(unitOfWork);
        ArgumentNullException.ThrowIfNull(logger);
        _recordRepository = recordRepository;
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<NotificationRecordListResultDto> QueryRecordsAsync(
        Guid? userId, NotificationChannel? channel, NotificationStatus? status,
        string? templateCode, string? businessRef, DateTime? fromTime, DateTime? toTime,
        int page, int pageSize, CancellationToken ct = default)
    {
        var items = await _recordRepository.QueryRecordsAsync(
            userId, channel, status, templateCode, businessRef, fromTime, toTime, page, pageSize, ct);
        var total = await _recordRepository.CountRecordsAsync(
            userId, channel, status, templateCode, businessRef, fromTime, toTime, ct);

        return new NotificationRecordListResultDto
        {
            Items = items.ConvertAll(ToListItemDto),
            Total = total,
            Page = page,
            PageSize = pageSize
        };
    }

    /// <inheritdoc />
    public async Task<NotificationRecordDetailDto?> GetRecordByIdAsync(Guid recordId, CancellationToken ct = default)
    {
        var record = await _recordRepository.GetByIdAsync(recordId, ct);
        return record is null ? null : ToDetailDto(record);
    }

    /// <inheritdoc />
    public async Task<List<NotificationRecordListItemDto>> GetRecordsByBusinessRefAsync(string businessRef, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(businessRef))
        {
            return [];
        }

        var items = await _recordRepository.GetByBusinessRefAsync(businessRef, ct);
        return items.ConvertAll(ToListItemDto);
    }

    /// <inheritdoc />
    public async Task<DeliveryStatisticsListDto> GetDeliveryStatisticsAsync(DateTime? fromTime, DateTime? toTime, CancellationToken ct = default)
    {
        var stats = await _recordRepository.GetDeliveryStatisticsAsync(fromTime, toTime, ct);

        return new DeliveryStatisticsListDto
        {
            Items = stats.ConvertAll(s => new DeliveryStatisticsDto
            {
                Channel = s.Channel,
                TemplateCode = s.TemplateCode,
                TotalCount = s.TotalCount,
                SucceededCount = s.SucceededCount,
                FailedCount = s.FailedCount,
                DeadLetteredCount = s.DeadLetteredCount,
                DeliveryRate = s.DeliveryRate
            }),
            From = fromTime,
            To = toTime
        };
    }

    /// <inheritdoc />
    public async Task ResendRecordAsync(Guid recordId, Guid operatorId, CancellationToken ct = default)
    {
        if (recordId == Guid.Empty)
        {
            throw new ArgumentException("通知记录标识不可为空", nameof(recordId));
        }

        var record = await _recordRepository.GetByIdAsync(recordId, ct);
        if (record is null)
        {
            throw new ArgumentException($"通知记录 {recordId} 不存在", nameof(recordId));
        }

        if (record.Status != NotificationStatus.DeadLettered)
        {
            throw new InvalidOperationException($"记录 {recordId} 当前状态为 {record.Status}，非死信状态无法重发");
        }

        // 修复 P0-7：将状态置为 Pending 而非 Sending，让 NotificationDispatchJob 接管实际发送。
        // 原实现调用 MarkResend() 置为 Sending，但没有任何 Job 拾取 Sending 状态记录，导致永久卡死。
        record.RequeueForSend();
        await _recordRepository.UpdateAsync(record, ct);
        await _unitOfWork.SaveChangesAsync(ct);

        _logger.LogInformation(
            "操作员 {OperatorId} 重发死信记录 RecordId={RecordId} 已重新置为 Pending 等待 DispatchJob 拾取",
            operatorId, recordId);
    }

    private static NotificationRecordListItemDto ToListItemDto(NotificationRecord record)
    {
        return new NotificationRecordListItemDto
        {
            RecordId = record.Id,
            UserId = record.UserId,
            TemplateCode = record.TemplateCode,
            Channel = record.Channel,
            Title = record.Title,
            Status = record.Status,
            MaskedContact = MaskContact(record),
            BusinessRef = record.BusinessRef,
            SentAt = record.SentAt,
            CreatedAt = record.CreatedAt
        };
    }

    private static NotificationRecordDetailDto ToDetailDto(NotificationRecord record)
    {
        return new NotificationRecordDetailDto
        {
            RecordId = record.Id,
            UserId = record.UserId,
            TemplateCode = record.TemplateCode,
            Channel = record.Channel,
            Title = record.Title,
            Content = record.Content,
            Status = record.Status,
            RetryCount = record.RetryCount,
            ErrorMessage = record.ErrorMessage,
            ErrorCode = record.ErrorCode,
            MaskedContact = MaskContact(record),
            BusinessRef = record.BusinessRef,
            ChannelMessageId = record.ChannelMessageId,
            SentAt = record.SentAt,
            FailedAt = record.FailedAt,
            CreatedAt = record.CreatedAt
        };
    }

    /// <summary>
    /// 脱敏联系方式（手机号/邮箱）。
    /// 手机号：138****1234
    /// 邮箱：abc***@domain.com
    /// </summary>
    private static string? MaskContact(NotificationRecord record)
    {
        // Contact info is not directly stored on NotificationRecord.
        // The masking is done on the receipt payload via ApplyReceipt.
        // For the list, we return null or a masked version if available.
        // Since contact info comes from Recipient which is not persisted on the record,
        // we rely on the ChannelReceipt for masking.
        return record.ChannelReceipt;
    }
}