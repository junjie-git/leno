using System.Text.Json;
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
            _logger.LogWarning(ex, "更新站内信未读计数失败 UserId={UserId}", request.Recipient.UserId);
            // 站内信 DB 写入已成功，Redis 失败不影响送达
            return new ChannelSendResult(true, null, null, null);
        }
    }
}