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

        // 修复审计 #9：原实现 (Score * Count + Rating) / (Count + 1) 每次回写 Math.Round(Score, 2)，
        // 加权累计值不等于真实总评分，千次评价后漂移 ±0.05。
        // 现维护原始累计 TotalScore，增量时 TotalScore += Rating，展示时 Score = Round(TotalScore / Count, 2)。
        existing.TotalScore += integrationEvent.Rating;
        existing.ReviewCount += 1;
        existing.Score = Math.Round(existing.TotalScore / existing.ReviewCount, 2);
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
            // 修复审计 #9：同步重置 TotalScore，与 ReviewCount=0 保持一致
            existing.TotalScore = 0;
            existing.Score = 0;
            existing.ReviewCount = 0;
        }
        else
        {
            // 修复审计 #9：使用原始累计 TotalScore 减去 Rating，消除回写 round 导致的漂移
            existing.TotalScore -= integrationEvent.Rating;
            existing.ReviewCount -= 1;
            existing.Score = Math.Round(existing.TotalScore / existing.ReviewCount, 2);
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

/// <summary>
/// 评价审核评分摘要消费者：消费 <see cref="ReviewModeratedEvent"/>，
/// 根据审核动作（approve/reject/hide/appeal）增量更新 ES <see cref="ProductReadModel"/> 评分摘要。
/// <para>
/// 动作语义：
/// <list type="bullet">
/// <item>approve（审核通过）：评分计入摘要，TotalScore += Rating、ReviewCount += 1。</item>
/// <item>appeal（申诉恢复）：评分重新计入摘要，TotalScore += Rating、ReviewCount += 1。</item>
/// <item>reject（驳回）/ hide（隐藏）：评分从摘要移除，TotalScore -= Rating、ReviewCount -= 1。</item>
/// </list>
/// </para>
/// 修复审计 #10：原实现仅有 TODO 占位，审核驳回后商品评分读模型仍包含被驳回评价。
/// 现与评价域对齐 schema（SpuId + Rating），实现增量更新消费者。
/// 幂等：通过 EventId + Redis SET NX 去重；ES 索引以商品标识为 _id，重复索引为覆盖更新。
/// </summary>
public sealed class SpuReviewModeratedSummaryConsumer : IntegrationEventConsumerBase<ReviewModeratedEvent>
{
    private readonly IEsReadModelRepository<ProductReadModel> _repository;

    public SpuReviewModeratedSummaryConsumer(
        IEsReadModelRepository<ProductReadModel> repository,
        ILogger<SpuReviewModeratedSummaryConsumer> logger,
        IIdempotencyStore idempotencyStore)
        : base(logger, idempotencyStore)
    {
        ArgumentNullException.ThrowIfNull(repository);
        _repository = repository;
    }

    /// <inheritdoc />
    protected override async Task HandleAsync(ReviewModeratedEvent integrationEvent, CancellationToken ct)
    {
        if (integrationEvent.SpuId == Guid.Empty)
        {
            Logger.LogWarning("ReviewModeratedEvent 缺少 SpuId，跳过读模型更新 ReviewId={ReviewId}",
                integrationEvent.ReviewId);
            return;
        }

        if (integrationEvent.Rating is < 1 or > 5)
        {
            Logger.LogWarning("评分越界，跳过读模型更新 SpuId={SpuId} Rating={Rating}",
                integrationEvent.SpuId, integrationEvent.Rating);
            return;
        }

        var action = (integrationEvent.Action ?? string.Empty).Trim().ToLowerInvariant();
        var isAdditive = action is "approve" or "appeal";
        var isRemoval = action is "reject" or "hide";
        if (!isAdditive && !isRemoval)
        {
            Logger.LogWarning("未知的审核动作，跳过读模型更新 SpuId={SpuId} Action={Action}",
                integrationEvent.SpuId, integrationEvent.Action);
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

        if (isRemoval)
        {
            if (existing.ReviewCount <= 0)
            {
                Logger.LogInformation("读模型评价数为 0，跳过移除 SpuId={SpuId}", integrationEvent.SpuId);
                return;
            }

            if (existing.ReviewCount == 1)
            {
                existing.TotalScore = 0;
                existing.Score = 0;
                existing.ReviewCount = 0;
            }
            else
            {
                existing.TotalScore -= integrationEvent.Rating;
                existing.ReviewCount -= 1;
                existing.Score = Math.Round(existing.TotalScore / existing.ReviewCount, 2);
            }
        }
        else
        {
            // isAdditive（approve / appeal）
            existing.TotalScore += integrationEvent.Rating;
            existing.ReviewCount += 1;
            existing.Score = Math.Round(existing.TotalScore / existing.ReviewCount, 2);
        }

        existing.ScoreUpdatedAt = DateTime.UtcNow;

        var success = await _repository.IndexAsync(
            existing,
            integrationEvent.SpuId.ToString(),
            ProductSearchService.ProductIndexName,
            ct);

        if (!success)
        {
            throw new InvalidOperationException($"ES 读模型评分更新失败 SpuId={integrationEvent.SpuId} Action={action}");
        }

        Logger.LogInformation("商品评分读模型已按审核动作更新 SpuId={SpuId} Action={Action} Score={Score} ReviewCount={ReviewCount}",
            integrationEvent.SpuId, action, existing.Score, existing.ReviewCount);
    }
}
