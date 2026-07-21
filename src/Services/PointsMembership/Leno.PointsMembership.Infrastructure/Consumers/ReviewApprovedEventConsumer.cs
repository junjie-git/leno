using Leno.Infrastructure.Abstractions;
using Leno.Infrastructure.EventBus;
using Leno.PointsMembership.Application;
using Leno.PointsMembership.Domain.Aggregates;
using Leno.PointsMembership.Domain.Repositories;
using Leno.PointsMembership.Domain.ValueObjects;
using Leno.SharedContracts.Events;
using Leno.SharedKernel.Abstractions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using StackExchange.Redis;

namespace Leno.PointsMembership.Infrastructure.Consumers;

/// <summary>
/// 评价审核通过事件消费者，发放评价返积分（10 分/条），每日最多 5 条评价获得积分。
/// 通过 EventId 幂等去重，通过 Redis 计数每日评价积分发放次数。
/// PM-L06 修复：Redis Key 的"日"计算改用配置的默认用户时区，避免 UTC 跨日导致计数错位。
/// PM-L02 修复：每日上限与 Redis Key TTL 从配置读取，不再硬编码。
/// </summary>
public sealed class ReviewApprovedEventConsumer : IntegrationEventConsumerBase<ReviewApprovedEvent>
{
    private const int ReviewPointsPerReview = 10;
    private const int DefaultMaxDailyReviewPoints = 5;
    private const int DefaultRedisDailyKeyTtlHours = 25;
    private const string DefaultTimeZoneId = "Asia/Shanghai";

    private readonly IPointsAccountRepository _accountRepository;
    private readonly IMemberRepository _memberRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IDatabase _redisDb;
    private readonly int _maxDailyReviewPoints;
    private readonly TimeSpan _redisDailyKeyTtl;
    private readonly TimeZoneInfo _userTimeZone;

    public ReviewApprovedEventConsumer(
        IPointsAccountRepository accountRepository,
        IMemberRepository memberRepository,
        IUnitOfWork unitOfWork,
        ILogger<ReviewApprovedEventConsumer> logger,
        IIdempotencyStore idempotencyStore,
        IConnectionMultiplexer redisMultiplexer,
        IOptions<PointsMembershipOptions>? options = null)
        : base(logger, idempotencyStore)
    {
        _accountRepository = accountRepository;
        _memberRepository = memberRepository;
        _unitOfWork = unitOfWork;
        _redisDb = redisMultiplexer.GetDatabase();

        // PM-L02 修复：从配置读取每日上限与 Redis Key TTL
        var opts = options?.Value;
        _maxDailyReviewPoints = opts?.ReviewDailyLimit > 0
            ? opts.ReviewDailyLimit
            : DefaultMaxDailyReviewPoints;
        var ttlHours = opts?.RedisDailyKeyTtlHours > 0
            ? opts.RedisDailyKeyTtlHours
            : DefaultRedisDailyKeyTtlHours;
        _redisDailyKeyTtl = TimeSpan.FromHours(ttlHours);

        // PM-L06 修复：从配置读取默认用户时区，解析失败时回退 Asia/Shanghai
        var timeZoneId = opts?.DefaultTimeZone ?? DefaultTimeZoneId;
        _userTimeZone = TimeZoneInfo.TryFindSystemTimeZoneById(timeZoneId, out var tz)
            ? tz
            : TimeZoneInfo.Utc;
    }

    protected override async Task HandleAsync(ReviewApprovedEvent integrationEvent, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(integrationEvent);

        // PM-L06 修复：使用用户时区计算"今日"作为 Redis Key 后缀，避免 UTC 跨日导致计数错位
        var today = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, _userTimeZone).ToString("yyyyMMdd");
        var dailyKey = $"review:points:{integrationEvent.UserId}:{today}";

        // 原子自增并返回新值
        var newCount = await _redisDb.StringIncrementAsync(dailyKey);

        // 设置过期时间（仅首次自增时设置，避免每次重置 TTL）
        if (newCount == 1)
        {
            await _redisDb.KeyExpireAsync(dailyKey, _redisDailyKeyTtl);
        }

        if (newCount > _maxDailyReviewPoints)
        {
            // 超过上限，回滚计数并跳过积分发放
            await _redisDb.StringDecrementAsync(dailyKey);
            Logger.LogInformation("用户 {UserId} 今日评价积分已达上限 {Max}，跳过发放",
                integrationEvent.UserId, _maxDailyReviewPoints);
            return;
        }

        var account = await _accountRepository.GetByUserIdAsync(integrationEvent.UserId, ct);
        if (account is null)
        {
            // 账户不存在也回滚计数，避免占用当日配额
            await _redisDb.StringDecrementAsync(dailyKey);
            Logger.LogWarning("用户 {UserId} 积分账户不存在，跳过评价积分发放", integrationEvent.UserId);
            return;
        }

        var reviewReason = $"评价 {integrationEvent.ReviewId} 返积分";
        account.Earn(PointsSource.Review, ReviewPointsPerReview, reviewReason);

        // PM-H01 修复：1 积分 = 1 成长值，同步累加成长值打通 V0-V4 等级体系
        var member = await _memberRepository.GetByUserIdAsync(integrationEvent.UserId, ct);
        if (member is not null)
        {
            member.AddGrowthValue(ReviewPointsPerReview, reviewReason);
        }

        await _unitOfWork.SaveEntitiesAsync(ct);

        Logger.LogInformation("评价 {ReviewId} 审核通过，发放 {Points} 积分给用户 {UserId}（今日第 {Count} 条）",
            integrationEvent.ReviewId, ReviewPointsPerReview, integrationEvent.UserId, newCount);
    }
}
