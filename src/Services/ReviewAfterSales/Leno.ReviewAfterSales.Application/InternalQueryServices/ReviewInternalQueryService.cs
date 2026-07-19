using Leno.ReviewAfterSales.Domain.Repositories;
using Leno.ReviewAfterSales.Domain.ValueObjects;

namespace Leno.ReviewAfterSales.Application.InternalQueryServices;

/// <summary>
/// 评价与售后域跨 BC 内部查询服务实现（M4 双轨方案）。
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
        // 仅聚合 Approved 评价，与买家侧 GetReviewsBySpuAsync 视图一致
        var reviews = await _reviewRepository.GetBySpuIdAsync(spuId, ReviewStatus.Approved, ct);
        if (reviews is null || reviews.Count == 0)
        {
            return null;
        }

        var totalCount = reviews.Count;
        var positiveCount = reviews.Count(r => r.Rating >= 4);
        var averageRating = reviews.Average(r => (double)r.Rating);

        return new ProductRatingDto
        {
            SpuId = spuId,
            AverageRating = averageRating,
            TotalCount = totalCount,
            PositiveCount = positiveCount
        };
    }

    /// <inheritdoc />
    public async Task<OrderReviewsDto?> GetOrderReviewsAsync(Guid orderId, CancellationToken ct = default)
    {
        // 仅返回 Approved 评价摘要，与买家可见视图一致
        var reviews = await _reviewRepository.GetByOrderIdAsync(orderId, ReviewStatus.Approved, ct);
        if (reviews is null || reviews.Count == 0)
        {
            return null;
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
