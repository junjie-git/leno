using Leno.Notification.Domain.Services;
using Leno.Notification.Domain.ValueObjects;
using Leno.Notification.Infrastructure.Options;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
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
///
/// P1-7：限流阈值与窗口改为通过 <see cref="IOptionsMonitor{RateLimitOptions}"/> 注入，
///       支持运行时热更新与按 templateCode 维度覆盖，缺省值与原 const 完全对齐（零行为变更）。
/// </summary>
public sealed class RedisRateLimiter : IRateLimiter
{
    private readonly IConnectionMultiplexer _redis;
    private readonly IOptionsMonitor<RateLimitOptions> _options;
    private readonly ILogger<RedisRateLimiter> _logger;

    public RedisRateLimiter(
        IConnectionMultiplexer redis,
        IOptionsMonitor<RateLimitOptions> options,
        ILogger<RedisRateLimiter> logger)
    {
        ArgumentNullException.ThrowIfNull(redis);
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(logger);
        _redis = redis;
        _options = options;
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
        var opts = _options.CurrentValue;

        // 查找按 templateCode 维度的限流规则覆盖
        TemplateRateLimitRule? templateRule = null;
        if (!string.IsNullOrEmpty(templateCode))
        {
            opts.PerTemplateCode.TryGetValue(templateCode, out templateRule);
        }

        var hourlyWindow = TimeSpan.FromSeconds(opts.HourlyWindowSeconds);
        var dailyWindow = TimeSpan.FromSeconds(opts.DailyWindowSeconds);

        switch (channel)
        {
            case NotificationChannel.Email:
                var emailLimit = templateRule?.EmailHourlyLimit ?? opts.EmailHourlyLimit;
                return await CheckLimitAsync(db, recipient, channel, "hourly", emailLimit, hourlyWindow);

            case NotificationChannel.Sms:
                // 短信先检查小时限制
                var smsHourlyLimit = templateRule?.SmsHourlyLimit ?? opts.SmsHourlyLimit;
                var hourlyResult = await CheckLimitAsync(db, recipient, channel, "hourly", smsHourlyLimit, hourlyWindow);
                if (!hourlyResult.Allowed)
                {
                    return hourlyResult;
                }

                // 再检查日限制
                var smsDailyLimit = templateRule?.SmsDailyLimit ?? opts.SmsDailyLimit;
                var dailyResult = await CheckLimitAsync(db, recipient, channel, "daily", smsDailyLimit, dailyWindow);
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
        var removeTask = transaction.SortedSetRemoveRangeByScoreAsync(key, double.NegativeInfinity, windowStart);

        // 2. 统计窗口内的记录数
        var countTask = transaction.SortedSetLengthAsync(key);

        // 3. 添加当前请求记录
        var addTask = transaction.SortedSetAddAsync(key, now, now);

        // 4. 设置过期时间
        var expireTask = transaction.KeyExpireAsync(key, window + TimeSpan.FromMinutes(1));

        var executed = await transaction.ExecuteAsync().ConfigureAwait(false);
        if (!executed)
        {
            // 事务未执行（条件不满足或 Redis 内部错误）→ fail-open 降级为允许
            // 此时排队中的 Task 可能未完成，不应阻塞 await，直接降级
            _logger.LogWarning("Redis 事务未执行 Recipient={Recipient} Channel={Channel} Window={WindowType}",
                recipient, channel, windowType);
            return RateLimitResult.AllowedResult();
        }

        // 显式等待所有事务 Task 完成，消除 unobserved exception 风险
        // （removeTask/addTask/expireTask 此前被 _ = 丢弃，faulted 时会触发 TaskScheduler.UnobservedTaskException）
        await Task.WhenAll(removeTask, countTask, addTask, expireTask).ConfigureAwait(false);

        var count = (int)(await countTask.ConfigureAwait(false));

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
