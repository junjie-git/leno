namespace Leno.ReviewAfterSales.Application;

/// <summary>
/// 评价与售后域跨 BC 内部查询服务（M4 双轨方案）。
/// 仅暴露跨 BC 查询所需的方法子集（只读），供 ReviewGrpcService 复用。
/// </summary>
public interface IReviewInternalQueryService
{
    /// <summary>
    /// 查询商品 SPU 的聚合评分（average_rating/total_count/positive_count）。
    /// 仅聚合已通过（Approved）状态的评价，与买家可见视图保持一致。
    /// </summary>
    /// <param name="spuId">商品 SPU 标识。</param>
    /// <param name="ct">取消令牌。</param>
    /// <returns>聚合评分；无可见评价返回 null。</returns>
    Task<ProductRatingDto?> GetProductRatingAsync(Guid spuId, CancellationToken ct = default);

    /// <summary>
    /// 查询订单关联的评价列表（按 orderId 聚合）。
    /// 仅返回已通过（Approved）状态的评价摘要，与买家可见视图保持一致。
    /// </summary>
    /// <param name="orderId">订单标识。</param>
    /// <param name="ct">取消令牌。</param>
    /// <returns>订单评价列表 DTO；订单无可见评价返回空 Reviews 列表（审计 4.7，实现层不再返回 null，签名保留 nullable 以兼容既有消费方与防御性编程）。</returns>
    Task<OrderReviewsDto?> GetOrderReviewsAsync(Guid orderId, CancellationToken ct = default);
}

/// <summary>商品 SPU 聚合评分 DTO（跨 BC 查询用）。</summary>
public sealed class ProductRatingDto
{
    public Guid SpuId { get; init; }

    /// <summary>平均评分（1-5）。</summary>
    public double AverageRating { get; init; }

    /// <summary>评价总数。</summary>
    public int TotalCount { get; init; }

    /// <summary>好评数（rating ≥ 4）。</summary>
    public int PositiveCount { get; init; }
}

/// <summary>订单评价列表 DTO（跨 BC 查询用）。</summary>
public sealed class OrderReviewsDto
{
    public IReadOnlyList<ReviewSummaryDto> Reviews { get; init; } = Array.Empty<ReviewSummaryDto>();
}

/// <summary>评价摘要 DTO（跨 BC 查询用）。</summary>
public sealed class ReviewSummaryDto
{
    public Guid ReviewId { get; init; }
    public Guid SpuId { get; init; }
    public int Rating { get; init; }
    public string Content { get; init; } = string.Empty;
    public DateTime CreatedAt { get; init; }
}
