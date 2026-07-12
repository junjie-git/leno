using Leno.Notification.Application.DTOs;
using Leno.Notification.Domain.Repositories;
using Leno.Notification.Domain.ValueObjects;

namespace Leno.Notification.Application;

/// <summary>
/// 通知记录查询应用服务接口（管理员端）。
/// </summary>
public interface INotificationRecordAppService
{
    /// <summary>多维度分页查询通知记录。</summary>
    Task<NotificationRecordListResultDto> QueryRecordsAsync(
        Guid? userId, NotificationChannel? channel, NotificationStatus? status,
        string? templateCode, string? businessRef, DateTime? fromTime, DateTime? toTime,
        int page, int pageSize, CancellationToken ct = default);

    /// <summary>获取通知记录详情。</summary>
    Task<NotificationRecordDetailDto?> GetRecordByIdAsync(Guid recordId, CancellationToken ct = default);

    /// <summary>按业务引用标识查询通知记录。</summary>
    Task<List<NotificationRecordListItemDto>> GetRecordsByBusinessRefAsync(string businessRef, CancellationToken ct = default);

    /// <summary>获取送达率统计。</summary>
    Task<DeliveryStatisticsListDto> GetDeliveryStatisticsAsync(DateTime? fromTime, DateTime? toTime, CancellationToken ct = default);
}