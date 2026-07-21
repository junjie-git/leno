using Leno.Infrastructure.Abstractions;
using Leno.Infrastructure.EventBus;
using Leno.PointsMembership.Domain.Aggregates;
using Leno.PointsMembership.Domain.Repositories;
using Leno.PointsMembership.Domain.ValueObjects;
using Leno.SharedContracts.Events;
using Leno.SharedKernel.Abstractions;
using Microsoft.Extensions.Logging;
using StackExchange.Redis;

namespace Leno.PointsMembership.Infrastructure.Consumers;

/// <summary>
/// 评价审核通过事件消费者，发放评价返积分（10 分/条），每日最多 5 条评价获得积分。
/// 通过 EventId 幂等去重，通过 Redis 计数每日评价积分发放次数。
/// </summary>
public sealed class ReviewApprovedEventConsumer : IntegrationEventConsumerBase<ReviewApprovedEvent>
{
    private const int ReviewPointsPerReview = 10;
    private const int MaxDailyReviewPoints = 5;

    private readonly IPointsAccountRepository _accountRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IDatabase _redisDb;

    public ReviewApprovedEventConsumer(
        IPointsAccountRepository accountRepository,
        IUnitOfWork unitOfWork,
        ILogger<ReviewApprovedEventConsumer> logger,
        IIdempotencyStore idempotencyStore,
        IConnectionMultiplexer redisMultiplexer)
        : base(logger, idempotencyStore)
    {
        _accountRepository = accountRepository;
        _unitOfWork = unitOfWork;
        _redisDb = redisMultiplexer.GetDatabase();
    }

    protected override async Task HandleAsync(ReviewApprovedEvent integrationEvent, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(integrationEvent);

        // 每日评价积分上限检查：使用 StringIncrementAsync 原子自增并返回新值，避免并发场景下多个消费者同时读到相同计数突破每日 5 条上限
        var today = DateTime.UtcNow.ToString("yyyyMMdd");
        var dailyKey = $"review:points:{integrationEvent.UserId}:{today}";

        // 原子自增并返回新值
        var newCount = await _redisDb.StringIncrementAsync(dailyKey);

        // 设置过期时间（仅首次自增时设置，避免每次重置 TTL）
        if (newCount == 1)
        {
            await _redisDb.KeyExpireAsync(dailyKey, TimeSpan.FromHours(25));
        }

        if (newCount > MaxDailyReviewPoints)
        {
            // 超过上限，回滚计数并跳过积分发放
            await _redisDb.StringDecrementAsync(dailyKey);
            Logger.LogInformation("用户 {UserId} 今日评价积分已达上限 {Max}，跳过发放",
                integrationEvent.UserId, MaxDailyReviewPoints);
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

        account.Earn(PointsSource.Review, ReviewPointsPerReview,
            $"评价 {integrationEvent.ReviewId} 返积分");

        await _unitOfWork.SaveEntitiesAsync(ct);

        Logger.LogInformation("评价 {ReviewId} 审核通过，发放 {Points} 积分给用户 {UserId}（今日第 {Count} 条）",
            integrationEvent.ReviewId, ReviewPointsPerReview, integrationEvent.UserId, newCount);
    }
}
