namespace Leno.SellerShop.Application.Services;

/// <summary>
/// 评论域防腐层服务接口（卖家店铺域视角）。
/// 用于卖家工作台读模型构建时反查评论域聚合评分统计。
/// 接口定义在应用层，实现位于基础设施层（GrpcReviewAntiCorruptionClient）。
/// </summary>
public interface IReviewAntiCorruptionService
{
    /// <summary>
    /// 按店铺标识反查评论统计（累计评价数、平均评分、五星/一星评价数）。
    /// </summary>
    /// <param name="shopId">店铺标识。</param>
    /// <param name="ct">取消令牌。</param>
    /// <returns>评论统计；评论域故障或店铺无评价时返回 null（fail-closed，由调用方按零值兜底）。</returns>
    Task<ReviewStatisticsDto?> GetReviewStatisticsAsync(Guid shopId, CancellationToken ct = default);
}

/// <summary>评论统计 DTO（跨 BC 查询用）。</summary>
public sealed class ReviewStatisticsDto
{
    /// <summary>累计评价总数。</summary>
    public int TotalReviews { get; init; }

    /// <summary>平均评分（1-5，保留两位小数）。</summary>
    public decimal AverageRating { get; init; }

    /// <summary>五星评价数。</summary>
    public int FiveStarReviews { get; init; }

    /// <summary>一星评价数。</summary>
    public int OneStarReviews { get; init; }
}
