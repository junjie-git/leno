using Leno.Review.Application.DTOs;
using Leno.Review.Domain.ValueObjects;

namespace Leno.Review.Application;

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

    /// <summary>
    /// 买家追评，仅已通过（Approved）态评价可追评一次。
    /// 校验当前用户为评价归属买家，防止越权追评他人评价。
    /// </summary>
    /// <param name="reviewId">评价标识。</param>
    /// <param name="userId">当前用户标识，须等于评价的 UserId。</param>
    /// <param name="dto">追评请求 DTO。</param>
    Task<ReviewDto> AppendAdditionalReviewAsync(Guid reviewId, Guid userId, AppendReviewDto dto, CancellationToken ct = default);

    /// <summary>
    /// 卖家端分页查询本店铺商品评价，仅返回已通过（Approved）态评价。
    /// 支持按评分、回复状态、商品名称（经商品域 ACL 过滤 SpuId 列表）、时间范围过滤。
    /// </summary>
    /// <param name="sellerId">卖家标识，从 JWT 注入，仅返回 SellerId 匹配的评价。</param>
    /// <param name="rating">评分过滤（1-5），为空不过滤。</param>
    /// <param name="replied">回复状态过滤：true=已回复 / false=待回复 / null=全部。</param>
    /// <param name="productName">商品名称模糊搜索，为空不过滤；非空时经商品域 ACL 过滤 SpuId 列表。</param>
    /// <param name="startDate">评价提交时间起点（含），为空不过滤。</param>
    /// <param name="endDate">评价提交时间终点（含），为空不过滤。</param>
    /// <param name="page">页码（从 1 起）。</param>
    /// <param name="pageSize">每页大小。</param>
    Task<ReviewListResultDto> GetBySellerAsync(
        Guid sellerId,
        int? rating,
        bool? replied,
        string? productName,
        DateTime? startDate,
        DateTime? endDate,
        int page,
        int pageSize,
        CancellationToken ct = default);

    /// <summary>
    /// 卖家端查询评价详情，校验归属卖家（sellerId 匹配评价聚合 SellerId）后返回单条评价。
    /// 通过 JWT sellerId 与评价聚合 SellerId 比对，防止越权查看他人店铺评价。
    /// </summary>
    /// <param name="reviewId">评价标识。</param>
    /// <param name="sellerId">当前卖家标识，须等于评价的 SellerId。</param>
    Task<ReviewDto> GetSellerReviewDetailAsync(Guid reviewId, Guid sellerId, CancellationToken ct = default);
}
