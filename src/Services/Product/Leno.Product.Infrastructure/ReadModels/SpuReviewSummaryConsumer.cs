using Leno.Infrastructure.Abstractions;
using Leno.Infrastructure.EventBus;
using Leno.Infrastructure.ReadModel;
using Leno.SharedContracts.Events;
using MassTransit;
using Microsoft.Extensions.Logging;

namespace Leno.Product.Infrastructure.ReadModels;

/// <summary>
/// 评价提交评分摘要消费者：消费 <see cref="ReviewSubmittedEvent"/>，
/// 增量更新 ES <see cref="ProductReadModel"/> 的 Score/ReviewCount（加权平均：((Score*Count)+Rating)/(Count+1)）。
/// 评价评分不再回写 SPU 聚合，仅维护读模型；SPU 仅保留基础信息与状态机。
/// 幂等：通过 EventId + Redis SET NX 去重；ES 索引以商品标识为 _id，重复索引为覆盖更新。
/// </summary>
public sealed class SpuReviewSubmittedSummaryConsumer : IntegrationEventConsumerBase<ReviewSubmittedEvent>
{
    private readonly IEsReadModelRepository<ProductReadModel> _repository;

    public SpuReviewSubmittedSummaryConsumer(
        IEsReadModelRepository<ProductReadModel> repository,
        ILogger<SpuReviewSubmittedSummaryConsumer> logger,
        IIdempotencyStore idempotencyStore)
        : base(logger, idempotencyStore)
    {
        ArgumentNullException.ThrowIfNull(repository);
        _repository = repository;
    }

    /// <inheritdoc />
    protected override async Task HandleAsync(ReviewSubmittedEvent integrationEvent, CancellationToken ct)
    {
        if (integrationEvent.Rating is < 1 or > 5)
        {
            Logger.LogWarning("评分越界，跳过读模型更新 SpuId={SpuId} Rating={Rating}",
                integrationEvent.SpuId, integrationEvent.Rating);
            return;
        }

        var existing = await _repository.GetByIdAsync(
            integrationEvent.SpuId.ToString(),
            ProductSearchService.ProductIndexName,
            ct);

        if (existing is null)
        {
            Logger.LogInformation("ES 读模型不存在，跳过评分更新 SpuId={SpuId} ReviewId={ReviewId}",
                integrationEvent.SpuId, integrationEvent.ReviewId);
            return;
        }

        // 加权平均增量更新：((Score * Count) + Rating) / (Count + 1)
        var totalScore = existing.Score * existing.ReviewCount + integrationEvent.Rating;
        existing.ReviewCount += 1;
        existing.Score = Math.Round(totalScore / existing.ReviewCount, 2);
        existing.ScoreUpdatedAt = DateTime.UtcNow;

        var success = await _repository.IndexAsync(
            existing,
            integrationEvent.SpuId.ToString(),
            ProductSearchService.ProductIndexName,
            ct);

        if (!success)
        {
            throw new InvalidOperationException($"ES 读模型评分更新失败 SpuId={integrationEvent.SpuId}");
        }

        Logger.LogInformation("商品评分读模型已更新 SpuId={SpuId} Score={Score} ReviewCount={ReviewCount}",
            integrationEvent.SpuId, existing.Score, existing.ReviewCount);
    }
}

/// <summary>
/// 评价隐藏评分摘要消费者：消费 <see cref="ReviewHiddenEvent"/>，
/// 从 ES <see cref="ProductReadModel"/> 的评分统计中移除被隐藏评价。
/// 幂等：通过 EventId + Redis SET NX 去重；ES 索引以商品标识为 _id，重复索引为覆盖更新。
/// </summary>
public sealed class SpuReviewHiddenSummaryConsumer : IntegrationEventConsumerBase<ReviewHiddenEvent>
{
    private readonly IEsReadModelRepository<ProductReadModel> _repository;

    public SpuReviewHiddenSummaryConsumer(
        IEsReadModelRepository<ProductReadModel> repository,
        ILogger<SpuReviewHiddenSummaryConsumer> logger,
        IIdempotencyStore idempotencyStore)
        : base(logger, idempotencyStore)
    {
        ArgumentNullException.ThrowIfNull(repository);
        _repository = repository;
    }

    /// <inheritdoc />
    protected override async Task HandleAsync(ReviewHiddenEvent integrationEvent, CancellationToken ct)
    {
        if (integrationEvent.Rating is < 1 or > 5)
        {
            Logger.LogWarning("评分越界，跳过读模型更新 SpuId={SpuId} Rating={Rating}",
                integrationEvent.SpuId, integrationEvent.Rating);
            return;
        }

        var existing = await _repository.GetByIdAsync(
            integrationEvent.SpuId.ToString(),
            ProductSearchService.ProductIndexName,
            ct);

        if (existing is null)
        {
            Logger.LogInformation("ES 读模型不存在，跳过评分重算 SpuId={SpuId} ReviewId={ReviewId}",
                integrationEvent.SpuId, integrationEvent.ReviewId);
            return;
        }

        if (existing.ReviewCount <= 0)
        {
            Logger.LogInformation("读模型评价数为 0，跳过移除 SpuId={SpuId}", integrationEvent.SpuId);
            return;
        }

        if (existing.ReviewCount == 1)
        {
            existing.Score = 0;
            existing.ReviewCount = 0;
        }
        else
        {
            var totalScore = existing.Score * existing.ReviewCount - integrationEvent.Rating;
            existing.ReviewCount -= 1;
            existing.Score = Math.Round(totalScore / existing.ReviewCount, 2);
        }

        existing.ScoreUpdatedAt = DateTime.UtcNow;

        var success = await _repository.IndexAsync(
            existing,
            integrationEvent.SpuId.ToString(),
            ProductSearchService.ProductIndexName,
            ct);

        if (!success)
        {
            throw new InvalidOperationException($"ES 读模型评分重算失败 SpuId={integrationEvent.SpuId}");
        }

        Logger.LogInformation("商品评分读模型已重算（移除隐藏评价）SpuId={SpuId} Score={Score} ReviewCount={ReviewCount}",
            integrationEvent.SpuId, existing.Score, existing.ReviewCount);
    }
}

// TODO: ReviewModeratedEvent 当前未实现评分同步消费者。
// 该事件仅含 ReviewId/Status/Action，缺少 SpuId 与 Rating，
// 需评价与售后域补全字段或商品域查询评价仓储后才能增量更新读模型。
// 暂由 SpuReviewSubmittedSummaryConsumer 与 SpuReviewHiddenSummaryConsumer 覆盖主流程。
