using Leno.ReviewAfterSales.Application.DTOs;
using Leno.ReviewAfterSales.Domain.ValueObjects;

namespace Leno.ReviewAfterSales.Application;

/// <summary>
/// 评价应用服务接口，编排评价提交、卖家回复、运营审核与查询用例。
/// </summary>
public interface IReviewAppService
{
    /// <summary>买家提交评价，校验资格后创建评价聚合。</summary>
    Task<ReviewDto> SubmitReviewAsync(Guid userId, SubmitReviewDto dto, CancellationToken ct = default);

    /// <summary>卖家回复评价，仅已通过评价可回复，且仅归属卖家可回复。</summary>
    Task SellerReplyAsync(Guid reviewId, Guid sellerId, string content, CancellationToken ct = default);

    /// <summary>运营审核通过评价，将待审核态置为已通过态。</summary>
    Task ApproveReviewAsync(Guid reviewId, Guid auditorId, CancellationToken ct = default);

    /// <summary>运营隐藏违规评价，将已通过态置为已隐藏态。</summary>
    Task HideReviewAsync(Guid reviewId, Guid operatorId, string reason, CancellationToken ct = default);

    /// <summary>按 SPU 分页查询已通过评价（买家端商品详情）。</summary>
    Task<ReviewListResultDto> GetReviewsBySpuAsync(Guid spuId, int page, int pageSize, CancellationToken ct = default);

    /// <summary>按订单行查询评价（买家端订单详情）。</summary>
    Task<ReviewDto?> GetReviewByOrderLineAsync(Guid orderLineId, CancellationToken ct = default);

    /// <summary>
    /// 买家端按订单行查询评价，校验当前用户为订单归属买家。
    /// 通过评价聚合反查 OrderId，再经订单域防腐层校验 UserId，非归属买家抛 <c>REVIEW_FORBIDDEN</c>。
    /// </summary>
    Task<ReviewDto?> GetReviewByOrderLineForUserAsync(Guid orderLineId, Guid userId, CancellationToken ct = default);

    /// <summary>按用户分页查询评价（买家端我的评价）。</summary>
    Task<ReviewListResultDto> GetReviewsByUserAsync(Guid userId, int page, int pageSize, CancellationToken ct = default);

    /// <summary>运营端分页查询评价（按状态过滤）。</summary>
    Task<ReviewListResultDto> QueryReviewsAsync(ReviewStatus? status, int page, int pageSize, CancellationToken ct = default);
}
