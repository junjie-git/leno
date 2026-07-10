using Leno.Notification.Application.DTOs;
using Leno.Notification.Domain.Aggregates;
using Leno.Notification.Domain.Repositories;
using Leno.Notification.Domain.ValueObjects;
using Leno.SharedKernel.Abstractions;
using Microsoft.Extensions.Logging;

namespace Leno.Notification.Application.Services;

/// <summary>
/// 通知查询应用服务实现。
/// </summary>
public sealed class NotificationAppService : INotificationAppService
{
    private readonly INotificationRecordRepository _recordRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<NotificationAppService> _logger;

    public NotificationAppService(
        INotificationRecordRepository recordRepository,
        IUnitOfWork unitOfWork,
        ILogger<NotificationAppService> logger)
    {
        ArgumentNullException.ThrowIfNull(recordRepository);
        ArgumentNullException.ThrowIfNull(unitOfWork);
        ArgumentNullException.ThrowIfNull(logger);
        _recordRepository = recordRepository;
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<NotificationListResultDto> GetNotificationsAsync(Guid userId, bool? isRead, int page, int pageSize, CancellationToken ct = default)
    {
        var items = await _recordRepository.QueryByUserAsync(userId, isRead, page, pageSize, ct);
        var total = await _recordRepository.CountByUserAsync(userId, null, ct);
        var unreadCount = await _recordRepository.CountByUserAsync(userId, false, ct);

        return new NotificationListResultDto
        {
            Items = items.ConvertAll(ToDto),
            Total = total,
            UnreadCount = unreadCount,
            Page = page,
            PageSize = pageSize
        };
    }

    /// <inheritdoc />
    public async Task MarkAsReadAsync(Guid userId, List<Guid> recordIds, CancellationToken ct = default)
    {
        foreach (var recordId in recordIds)
        {
            var record = await _recordRepository.GetByIdAsync(recordId, ct);
            if (record is null || record.UserId != userId)
            {
                continue;
            }

            record.MarkAsRead();
            await _recordRepository.UpdateAsync(record, ct);
        }

        await _unitOfWork.SaveChangesAsync(ct);
    }

    /// <inheritdoc />
    public async Task MarkAllAsReadAsync(Guid userId, CancellationToken ct = default)
    {
        await _recordRepository.MarkAllAsReadAsync(userId, ct);
        await _unitOfWork.SaveChangesAsync(ct);
    }

    /// <inheritdoc />
    public Task<int> GetUnreadCountAsync(Guid userId, CancellationToken ct = default)
        => _recordRepository.CountByUserAsync(userId, false, ct);

    private static NotificationRecordDto ToDto(NotificationRecord record)
    {
        return new NotificationRecordDto
        {
            RecordId = record.Id,
            UserId = record.UserId,
            EventType = record.EventType,
            Channel = record.Channel,
            Title = record.Title,
            Content = record.Content,
            Status = record.Status,
            IsRead = record.IsRead,
            SentAt = record.SentAt,
            CreatedAt = record.CreatedAt
        };
    }
}
