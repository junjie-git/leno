using Leno.Review.Domain.Repositories;
using Leno.Review.Domain.ValueObjects;

namespace Leno.Review.Application.InternalQueryServices;

/// <summary>
/// 评价域跨 BC 内部查询服务实现（评价 BC 独立维护）。
/// 委托 <see cref="IReviewRepository"/> 既有查询能力，按 spuId 聚合评分、按 orderId 聚合评价列表。
/// 仅聚合已通过（Approved）状态的评价，与买家可见视图保持一致。
/// </summary>
public sealed class ReviewInternalQueryService : IReviewInternalQueryService
{
    private readonly IReviewRepository _reviewRepository;

    public ReviewInternalQueryService(IReviewRepository reviewRepository)
    {
        _reviewRepository = reviewRepository ?? throw new ArgumentNullException(nameof(reviewRepository));
    }

    /// <inheritdoc />
    public async Task<ProductRatingDto?> GetProductRatingAsync(Guid spuId, CancellationToken ct = default)
    {
        // 合并审计 3.4：使用 SQL 聚合替代内存计算，避免加载全部 Approved 评价到内存。
        // 仅聚合 Approved 评价，与买家侧 GetReviewsBySpuAsync 视图一致。
        var snapshot = await _reviewRepository.GetRatingSnapshotAsync(spuId, ct);
        if (snapshot is null)
        {
            return null;
        }

        return new ProductRatingDto
        {
            SpuId = snapshot.SpuId,
            AverageRating = snapshot.AverageRating,
            TotalCount = snapshot.TotalCount,
            PositiveCount = snapshot.PositiveCount
        };
    }

    /// <inheritdoc />
    public async Task<OrderReviewsDto?> GetOrderReviewsAsync(Guid orderId, CancellationToken ct = default)
    {
        // 审计 4.7：订单无可见评价时返回空 Reviews 列表的 OrderReviewsDto，而非 null。
        // 签名保留 nullable 以兼容既有消费方与防御性编程（如 ReviewGrpcService 的 null 检查），
        // 但实现层不再返回 null，简化下游空值处理。
        var reviews = await _reviewRepository.GetByOrderIdAsync(orderId, ReviewStatus.Approved, ct);
        if (reviews is null || reviews.Count == 0)
        {
            return new OrderReviewsDto();
        }

        return new OrderReviewsDto
        {
            Reviews = reviews.Select(r => new ReviewSummaryDto
            {
                ReviewId = r.Id,
                SpuId = r.SpuId,
                Rating = r.Rating,
                Content = r.Content ?? string.Empty,
                CreatedAt = r.CreatedAt
            }).ToList()
        };
    }
}
