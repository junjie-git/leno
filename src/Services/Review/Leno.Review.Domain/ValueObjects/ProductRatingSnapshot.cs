namespace Leno.Review.Domain.ValueObjects;

/// <summary>
/// 商品 SPU 评价聚合快照值对象（合并审计 3.4：SQL 聚合结果传输）。
/// 用于跨 BC 内部查询评分聚合，避免加载全部评价到内存。
/// </summary>
public sealed class ProductRatingSnapshot
{
    /// <summary>商品 SPU 标识。</summary>
    public Guid SpuId { get; init; }

    /// <summary>平均评分（1-5）。</summary>
    public double AverageRating { get; init; }

    /// <summary>评价总数。</summary>
    public int TotalCount { get; init; }

    /// <summary>好评数（rating ≥ 4）。</summary>
    public int PositiveCount { get; init; }
}
