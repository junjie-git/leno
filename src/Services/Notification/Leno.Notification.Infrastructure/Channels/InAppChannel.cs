using System.Text.Json;
using Leno.Notification.Domain.Channels;
using Leno.Notification.Domain.Services;
using Leno.Notification.Domain.ValueObjects;
using Microsoft.Extensions.Logging;
using StackExchange.Redis;

namespace Leno.Notification.Infrastructure.Channels;

/// <summary>
/// 站内信渠道实现。
/// 通知记录已持久化到 DB 即视为送达，本渠道仅更新 Redis 未读计数缓存。
/// </summary>
public sealed class InAppChannel : INotificationChannel
{
    private static readonly TimeSpan CountTtl = TimeSpan.FromDays(30);

    private static readonly NotificationChannelMetadata MetadataValue = new(
        ChannelKey.InApp,
        "站内信",
        new NotificationChannelCapabilities(
            RequiresRateLimit: false,
            SupportsAsyncReceipt: false,
            IsIdempotent: true,
            SupportsTemplate: true,
            Timeout: null),
        IsEnabled: true,
        Priority: 30);

    private readonly IConnectionMultiplexer _redis;
    private readonly ILogger<InAppChannel> _logger;

    public InAppChannel(IConnectionMultiplexer redis, ILogger<InAppChannel> logger)
    {
        ArgumentNullException.ThrowIfNull(redis);
        ArgumentNullException.ThrowIfNull(logger);
        _redis = redis;
        _logger = logger;
    }

    /// <inheritdoc />
    public NotificationChannel Channel => NotificationChannel.InApp;

    /// <inheritdoc />
    public ChannelKey ChannelKey => ChannelKey.InApp;

    /// <inheritdoc />
    public NotificationChannelMetadata Metadata => MetadataValue;

    /// <inheritdoc />
    public async Task<ChannelSendResult> SendAsync(ChannelSendRequest request, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        try
        {
            var db = _redis.GetDatabase();
            var key = $"notification:unread:{request.Recipient.UserId}";
            await db.StringIncrementAsync(key);
            await db.KeyExpireAsync(key, CountTtl);
            return new ChannelSendResult(true, null, null, null);
        }
        catch (Exception ex)
        {
            // P1-28：区分"DB 写入成功"与"缓存更新成功"。
            // 站内信 DB 写入由 NotificationService 在调用本渠道前已完成，Redis 失败不影响送达状态。
            // 但需通过 ErrorCode="CACHE_SYNC_FAILED" 标记缓存同步失败，便于监控告警与定时重建。
            // 缓存恢复后可通过 CountByUserAsync(userId, false) 从 DB 重建未读计数。
            _logger.LogWarning(ex, "站内信未读计数缓存同步失败 UserId={UserId}，待定时同步 Job 重建", request.Recipient.UserId);
            return new ChannelSendResult(true, "站内信未读计数缓存同步失败，待重建", "CACHE_SYNC_FAILED", null);
        }
    }
}