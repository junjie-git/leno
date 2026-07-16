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

        // 每日评价积分上限检查
        var today = DateTime.UtcNow.ToString("yyyyMMdd");
        var dailyKey = $"review:points:{integrationEvent.UserId}:{today}";
        var dailyCount = await _redisDb.StringGetAsync(dailyKey);
        var currentCount = dailyCount.HasValue ? (int)dailyCount : 0;

        if (currentCount >= MaxDailyReviewPoints)
        {
            Logger.LogInformation("用户 {UserId} 今日评价积分已达上限 {Max}，跳过发放",
                integrationEvent.UserId, MaxDailyReviewPoints);
            return;
        }

        var account = await _accountRepository.GetByUserIdAsync(integrationEvent.UserId, ct);
        if (account is null)
        {
            Logger.LogWarning("用户 {UserId} 积分账户不存在，跳过评价积分发放", integrationEvent.UserId);
            return;
        }

        account.Earn(PointsSource.Review, ReviewPointsPerReview,
            $"评价 {integrationEvent.ReviewId} 返积分");

        // 更新每日计数
        await _redisDb.StringSetAsync(dailyKey, currentCount + 1, TimeSpan.FromHours(25));

        await _unitOfWork.SaveEntitiesAsync(ct);

        Logger.LogInformation("评价 {ReviewId} 审核通过，发放 {Points} 积分给用户 {UserId}（今日第 {Count} 条）",
            integrationEvent.ReviewId, ReviewPointsPerReview, integrationEvent.UserId, currentCount + 1);
    }
}
