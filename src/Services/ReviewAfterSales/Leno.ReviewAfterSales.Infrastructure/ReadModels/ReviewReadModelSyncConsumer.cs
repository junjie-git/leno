using Leno.Infrastructure.Abstractions;
using Leno.Infrastructure.EventBus;
using Leno.Infrastructure.ReadModel;
using Leno.ReviewAfterSales.Domain.Repositories;
using Leno.SharedContracts.Events;
using MassTransit;
using Microsoft.Extensions.Logging;

namespace Leno.ReviewAfterSales.Infrastructure.ReadModels;

/// <summary>
/// 评价读模型同步消费者。
/// ReviewSubmittedEvent 走 <see cref="IntegrationEventConsumerBase{T}"/> 基类幂等去重，
/// ReviewApprovedEvent / ReviewHiddenEvent 手动委托 <see cref="IIdempotencyStore"/> 做幂等去重，
/// 避免基类仅支持单事件类型的限制（合并审计 3.6）。
/// 在评价生命周期事件（提交/审核通过/隐藏）时将评价聚合同步索引到 Elasticsearch。
/// 索引失败抛出异常以触发 MassTransit 重试与死信队列。
/// </summary>
public sealed class ReviewReadModelSyncConsumer :
    IntegrationEventConsumerBase<ReviewSubmittedEvent>,
    IConsumer<ReviewApprovedEvent>,
    IConsumer<ReviewHiddenEvent>
{
    private const string IndexName = "reviews";

    private readonly IReviewRepository _reviewRepository;
    private readonly IEsReadModelRepository<ReviewReadModel> _repository;

    public ReviewReadModelSyncConsumer(
        IReviewRepository reviewRepository,
        IEsReadModelRepository<ReviewReadModel> repository,
        ILogger<ReviewReadModelSyncConsumer> logger,
        IIdempotencyStore idempotencyStore)
        : base(logger, idempotencyStore)
    {
        ArgumentNullException.ThrowIfNull(reviewRepository);
        ArgumentNullException.ThrowIfNull(repository);
        _reviewRepository = reviewRepository;
        _repository = repository;
    }

    /// <inheritdoc />
    protected override async Task HandleAsync(ReviewSubmittedEvent integrationEvent, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(integrationEvent);
        await SyncAsync(integrationEvent.ReviewId, nameof(ReviewSubmittedEvent), ct);
    }

    /// <inheritdoc />
    public async Task Consume(ConsumeContext<ReviewApprovedEvent> context)
    {
        ArgumentNullException.ThrowIfNull(context);
        var evt = context.Message;
        // 手动幂等去重（基类仅支持 Submitted 单事件）
        if (evt.EventId == Guid.Empty)
        {
            Logger.LogWarning("ReviewApprovedEvent EventId 为 Guid.Empty，拒绝消费 ReviewId={ReviewId}", evt.ReviewId);
            throw new InvalidOperationException("ReviewApprovedEvent 的 EventId 为 Guid.Empty，无法保证幂等性");
        }

        if (await IdempotencyStore.IsProcessedAsync(evt.EventId, context.CancellationToken))
        {
            Logger.LogInformation("ReviewApprovedEvent 已处理，跳过重复消费 EventId={EventId} ReviewId={ReviewId}",
                evt.EventId, evt.ReviewId);
            return;
        }

        try
        {
            await SyncAsync(evt.ReviewId, nameof(ReviewApprovedEvent), context.CancellationToken);
        }
        catch
        {
            await IdempotencyStore.ReleaseProcessingLockAsync(evt.EventId, context.CancellationToken);
            throw;
        }

        await IdempotencyStore.MarkAsProcessedAsync(evt.EventId, context.CancellationToken);
    }

    /// <inheritdoc />
    public async Task Consume(ConsumeContext<ReviewHiddenEvent> context)
    {
        ArgumentNullException.ThrowIfNull(context);
        var evt = context.Message;
        // 手动幂等去重
        if (evt.EventId == Guid.Empty)
        {
            Logger.LogWarning("ReviewHiddenEvent EventId 为 Guid.Empty，拒绝消费 ReviewId={ReviewId}", evt.ReviewId);
            throw new InvalidOperationException("ReviewHiddenEvent 的 EventId 为 Guid.Empty，无法保证幂等性");
        }

        if (await IdempotencyStore.IsProcessedAsync(evt.EventId, context.CancellationToken))
        {
            Logger.LogInformation("ReviewHiddenEvent 已处理，跳过重复消费 EventId={EventId} ReviewId={ReviewId}",
                evt.EventId, evt.ReviewId);
            return;
        }

        try
        {
            // 合并审计 3.12：Hidden 事件从 ES 删除文档，避免隐藏评价仍可被搜索
            await DeleteReadModelAsync(evt.ReviewId, nameof(ReviewHiddenEvent), context.CancellationToken);
        }
        catch
        {
            await IdempotencyStore.ReleaseProcessingLockAsync(evt.EventId, context.CancellationToken);
            throw;
        }

        await IdempotencyStore.MarkAsProcessedAsync(evt.EventId, context.CancellationToken);
    }

    /// <summary>
    /// 加载评价聚合并同步索引到 ES。
    /// </summary>
    private async Task SyncAsync(Guid reviewId, string eventType, CancellationToken ct)
    {
        var readModel = await BuildReadModelAsync(reviewId, ct);
        if (readModel is null)
        {
            Logger.LogWarning("评价读模型同步跳过：评价不存在 ReviewId={ReviewId} Event={EventType}",
                reviewId, eventType);
            return;
        }

        var success = await _repository.IndexAsync(readModel, reviewId.ToString(), IndexName, ct);
        if (!success)
        {
            throw new InvalidOperationException($"ES 读模型索引失败 Id={reviewId} Index={IndexName}");
        }

        Logger.LogInformation("评价读模型已同步 ReviewId={ReviewId} Event={EventType} Index={Index}",
            reviewId, eventType, IndexName);
    }

    /// <summary>
    /// 从 ES 删除评价读模型文档（合并审计 3.12：隐藏评价从搜索结果中移除）。
    /// </summary>
    private async Task DeleteReadModelAsync(Guid reviewId, string eventType, CancellationToken ct)
    {
        var success = await _repository.DeleteByIdAsync(reviewId.ToString(), IndexName, ct);
        if (!success)
        {
            // 删除失败不抛异常（评价可能已被删除），仅打日志
            Logger.LogWarning("评价读模型删除失败或文档不存在 ReviewId={ReviewId} Event={EventType}",
                reviewId, eventType);
            return;
        }

        Logger.LogInformation("评价读模型已从 ES 删除 ReviewId={ReviewId} Event={EventType} Index={Index}",
            reviewId, eventType, IndexName);
    }

    /// <summary>
    /// 加载评价聚合并映射为读模型文档。
    /// </summary>
    private async Task<ReviewReadModel?> BuildReadModelAsync(Guid reviewId, CancellationToken ct)
    {
        // 合并审计 3.8：只读查询路径加 AsNoTracking
        var review = await _reviewRepository.GetByIdAsync(reviewId, ct);
        if (review is null)
        {
            return null;
        }

        return new ReviewReadModel
        {
            ReviewId = review.Id.ToString(),
            OrderId = review.OrderId.ToString(),
            SpuId = review.SpuId.ToString(),
            SkuId = review.SkuId.ToString(),
            UserId = review.UserId.ToString(),
            Rating = review.Rating,
            Content = review.Content,
            Images = review.Images.ToList(),
            Status = review.Status.ToString(),
            SellerReplyContent = review.SellerReplyContent,
            SellerReplyBy = review.SellerReplyBy?.ToString(),
            SellerReplyAt = review.SellerReplyAt,
            SubmittedAt = review.SubmittedAt
        };
    }
}
