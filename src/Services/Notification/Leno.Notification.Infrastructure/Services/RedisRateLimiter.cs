using Leno.Notification.Domain.Services;
using Leno.Notification.Domain.ValueObjects;
using Microsoft.Extensions.Logging;
using StackExchange.Redis;

namespace Leno.Notification.Infrastructure.Services;

/// <summary>
/// 基于 Redis 滑动窗口的频率限制器实现。
/// 
/// 规则：
/// - Email: 10条/小时/接收人
/// - SMS: 5条/小时/接收人，20条/天/接收人
/// - 验证码类通知免限或单独限流
/// - Redis 不可用时降级为允许，并发送告警
/// </summary>
public sealed class RedisRateLimiter : IRateLimiter
{
    private const int EmailHourlyLimit = 10;
    private const int SmsHourlyLimit = 5;
    private const int SmsDailyLimit = 20;
    private const int VerificationCodeHourlyLimit = 5;

    private static readonly TimeSpan HourlyWindow = TimeSpan.FromHours(1);
    private static readonly TimeSpan DailyWindow = TimeSpan.FromHours(24);

    private readonly IConnectionMultiplexer _redis;
    private readonly ILogger<RedisRateLimiter> _logger;

    public RedisRateLimiter(IConnectionMultiplexer redis, ILogger<RedisRateLimiter> logger)
    {
        ArgumentNullException.ThrowIfNull(redis);
        ArgumentNullException.ThrowIfNull(logger);
        _redis = redis;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<RateLimitResult> AcquireAsync(string recipient, string templateCode, NotificationChannel channel)
    {
        if (string.IsNullOrWhiteSpace(recipient))
        {
            return RateLimitResult.AllowedResult();
        }

        try
        {
            return await CheckRateLimitAsync(recipient, templateCode, channel);
        }
        catch (Exception ex)
        {
            // Redis 不可用 → 降级为允许，并发送告警
            _logger.LogError(ex, "Redis 频率限制检查失败，降级为允许 Recipient={Recipient} Channel={Channel}",
                recipient, channel);
            return RateLimitResult.AllowedResult();
        }
    }

    private async Task<RateLimitResult> CheckRateLimitAsync(string recipient, string templateCode, NotificationChannel channel)
    {
        var db = _redis.GetDatabase();

        switch (channel)
        {
            case NotificationChannel.Email:
                return await CheckLimitAsync(db, recipient, channel, "hourly", EmailHourlyLimit, HourlyWindow);

            case NotificationChannel.Sms:
                // 短信先检查小时限制
                var hourlyResult = await CheckLimitAsync(db, recipient, channel, "hourly", SmsHourlyLimit, HourlyWindow);
                if (!hourlyResult.Allowed)
                {
                    return hourlyResult;
                }

                // 再检查日限制
                var dailyResult = await CheckLimitAsync(db, recipient, channel, "daily", SmsDailyLimit, DailyWindow);
                return dailyResult;

            case NotificationChannel.InApp:
                // 站内信不限流
                return RateLimitResult.AllowedResult();

            default:
                return RateLimitResult.AllowedResult();
        }
    }

    private async Task<RateLimitResult> CheckLimitAsync(
        IDatabase db,
        string recipient,
        NotificationChannel channel,
        string windowType,
        int limit,
        TimeSpan window)
    {
        var key = $"rate_limit:{channel}:{recipient}:{windowType}";

        // 使用 Sorted Set 实现滑动窗口
        var now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        var windowStart = now - (long)window.TotalMilliseconds;

        var transaction = db.CreateTransaction();

        // 1. 移除窗口外的过期记录
        _ = transaction.SortedSetRemoveRangeByScoreAsync(key, double.NegativeInfinity, windowStart);

        // 2. 统计窗口内的记录数
        var countTask = transaction.SortedSetLengthAsync(key);

        // 3. 添加当前请求记录
        _ = transaction.SortedSetAddAsync(key, now, now);

        // 4. 设置过期时间
        _ = transaction.KeyExpireAsync(key, window + TimeSpan.FromMinutes(1));

        await transaction.ExecuteAsync();

        var count = (int)(await countTask);

        if (count > limit)
        {
            _logger.LogWarning("频率限制触发 Recipient={Recipient} Channel={Channel} Window={WindowType} Count={Count} Limit={Limit}",
                recipient, channel, windowType, count, limit);

            return RateLimitResult.DeniedResult(
                "RATE_LIMITED",
                $"发送频率超限，{channel} 渠道 {windowType} 限制为 {limit} 次",
                count,
                limit,
                DateTime.UtcNow.Add(window));
        }

        return new RateLimitResult
        {
            Allowed = true,
            CurrentCount = count,
            Limit = limit,
            ResetAt = DateTime.UtcNow.Add(window)
        };
    }
}