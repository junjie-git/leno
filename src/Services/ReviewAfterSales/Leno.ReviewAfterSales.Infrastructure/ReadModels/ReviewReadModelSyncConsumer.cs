using Leno.Infrastructure.ReadModel;
using Leno.ReviewAfterSales.Domain.Repositories;
using Leno.SharedContracts.Events;
using MassTransit;
using Microsoft.Extensions.Logging;

namespace Leno.ReviewAfterSales.Infrastructure.ReadModels;

/// <summary>
/// 评价读模型同步消费者，实现多个 IConsumer&lt;T&gt; 接口，
/// 在评价生命周期事件（提交/审核通过/隐藏）时将评价聚合同步索引到 Elasticsearch。
/// 索引失败抛出异常以触发 MassTransit 重试与死信队列。
/// </summary>
public sealed class ReviewReadModelSyncConsumer :
    IConsumer<ReviewSubmittedEvent>,
    IConsumer<ReviewApprovedEvent>,
    IConsumer<ReviewHiddenEvent>
{
    private const string IndexName = "reviews";

    private readonly IReviewRepository _reviewRepository;
    private readonly IEsReadModelRepository<ReviewReadModel> _repository;
    private readonly ILogger<ReviewReadModelSyncConsumer> _logger;

    public ReviewReadModelSyncConsumer(
        IReviewRepository reviewRepository,
        IEsReadModelRepository<ReviewReadModel> repository,
        ILogger<ReviewReadModelSyncConsumer> logger)
    {
        ArgumentNullException.ThrowIfNull(reviewRepository);
        ArgumentNullException.ThrowIfNull(repository);
        ArgumentNullException.ThrowIfNull(logger);
        _reviewRepository = reviewRepository;
        _repository = repository;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task Consume(ConsumeContext<ReviewSubmittedEvent> context)
    {
        ArgumentNullException.ThrowIfNull(context);
        await SyncAsync(context.Message.ReviewId, nameof(ReviewSubmittedEvent), context.CancellationToken);
    }

    /// <inheritdoc />
    public async Task Consume(ConsumeContext<ReviewApprovedEvent> context)
    {
        ArgumentNullException.ThrowIfNull(context);
        await SyncAsync(context.Message.ReviewId, nameof(ReviewApprovedEvent), context.CancellationToken);
    }

    /// <inheritdoc />
    public async Task Consume(ConsumeContext<ReviewHiddenEvent> context)
    {
        ArgumentNullException.ThrowIfNull(context);
        await SyncAsync(context.Message.ReviewId, nameof(ReviewHiddenEvent), context.CancellationToken);
    }

    /// <summary>
    /// 加载评价聚合并同步索引到 ES。
    /// </summary>
    private async Task SyncAsync(Guid reviewId, string eventType, CancellationToken ct)
    {
        var readModel = await BuildReadModelAsync(reviewId, ct);
        if (readModel is null)
        {
            _logger.LogWarning("评价读模型同步跳过：评价不存在 ReviewId={ReviewId} Event={EventType}",
                reviewId, eventType);
            return;
        }

        var success = await _repository.IndexAsync(readModel, reviewId.ToString(), IndexName, ct);
        if (!success)
        {
            throw new InvalidOperationException($"ES 读模型索引失败 Id={reviewId} Index={IndexName}");
        }

        _logger.LogInformation("评价读模型已同步 ReviewId={ReviewId} Event={EventType} Index={Index}",
            reviewId, eventType, IndexName);
    }

    /// <summary>
    /// 加载评价聚合并映射为读模型文档。
    /// </summary>
    private async Task<ReviewReadModel?> BuildReadModelAsync(Guid reviewId, CancellationToken ct)
    {
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
            SubmittedAt = review.SubmittedAt
        };
    }
}
