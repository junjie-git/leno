using Leno.Infrastructure.EventBus;
using Leno.Product.Domain.Aggregates;
using Leno.Product.Domain.Repositories;
using Leno.SharedContracts.Events;
using Leno.SharedKernel.Abstractions;
using Microsoft.Extensions.Logging;
using StackExchange.Redis;

namespace Leno.Product.Infrastructure.Consumers;

/// <summary>
/// 评价提交事件消费者：消费 <see cref="ReviewSubmittedEvent"/>，
/// 加载对应 SPU 聚合并更新评分摘要（加权平均分与评价数）。
/// 幂等：通过 EventId + Redis SET NX 去重；SPU 方法幂等，重复调用不产生副作用。
/// </summary>
public sealed class ReviewSubmittedEventConsumer : RedisIntegrationEventConsumerBase<ReviewSubmittedEvent>
{
    private readonly ISPURepository _spuRepository;
    private readonly IUnitOfWork _unitOfWork;

    public ReviewSubmittedEventConsumer(
        ISPURepository spuRepository,
        IUnitOfWork unitOfWork,
        ILogger<ReviewSubmittedEventConsumer> logger,
        IConnectionMultiplexer redisMultiplexer)
        : base(logger, redisMultiplexer)
    {
        ArgumentNullException.ThrowIfNull(spuRepository);
        ArgumentNullException.ThrowIfNull(unitOfWork);
        _spuRepository = spuRepository;
        _unitOfWork = unitOfWork;
    }

    /// <inheritdoc />
    protected override async Task HandleAsync(ReviewSubmittedEvent integrationEvent, CancellationToken ct)
    {
        var spu = await _spuRepository.GetByIdAsync(integrationEvent.SpuId, ct);
        if (spu is null)
        {
            Logger.LogWarning("商品不存在，跳过评分更新 SpuId={SpuId} ReviewId={ReviewId}",
                integrationEvent.SpuId, integrationEvent.ReviewId);
            return;
        }

        spu.UpdateReviewScore(integrationEvent.Rating);

        await _spuRepository.UpdateAsync(spu, ct);
        await _unitOfWork.SaveEntitiesAsync(ct);

        Logger.LogInformation("商品评分已更新 SpuId={SpuId} Score={Score} ReviewCount={ReviewCount}",
            spu.Id, spu.Score, spu.ReviewCount);
    }
}

/// <summary>
/// 评价隐藏事件消费者：消费 <see cref="ReviewHiddenEvent"/>，
/// 加载对应 SPU 聚合并从评分统计中移除被隐藏评价。
/// 幂等：通过 EventId + Redis SET NX 去重；SPU 方法幂等，重复调用不产生副作用。
/// </summary>
public sealed class ReviewHiddenEventConsumer : RedisIntegrationEventConsumerBase<ReviewHiddenEvent>
{
    private readonly ISPURepository _spuRepository;
    private readonly IUnitOfWork _unitOfWork;

    public ReviewHiddenEventConsumer(
        ISPURepository spuRepository,
        IUnitOfWork unitOfWork,
        ILogger<ReviewHiddenEventConsumer> logger,
        IConnectionMultiplexer redisMultiplexer)
        : base(logger, redisMultiplexer)
    {
        ArgumentNullException.ThrowIfNull(spuRepository);
        ArgumentNullException.ThrowIfNull(unitOfWork);
        _spuRepository = spuRepository;
        _unitOfWork = unitOfWork;
    }

    /// <inheritdoc />
    protected override async Task HandleAsync(ReviewHiddenEvent integrationEvent, CancellationToken ct)
    {
        var spu = await _spuRepository.GetByIdAsync(integrationEvent.SpuId, ct);
        if (spu is null)
        {
            Logger.LogWarning("商品不存在，跳过评分重算 SpuId={SpuId} ReviewId={ReviewId}",
                integrationEvent.SpuId, integrationEvent.ReviewId);
            return;
        }

        spu.RemoveReviewScore(integrationEvent.Rating);

        await _spuRepository.UpdateAsync(spu, ct);
        await _unitOfWork.SaveEntitiesAsync(ct);

        Logger.LogInformation("商品评分已重算（移除隐藏评价）SpuId={SpuId} Score={Score} ReviewCount={ReviewCount}",
            spu.Id, spu.Score, spu.ReviewCount);
    }
}