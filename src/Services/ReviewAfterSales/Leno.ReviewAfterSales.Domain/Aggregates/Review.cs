using Leno.ReviewAfterSales.Domain.Events;
using Leno.ReviewAfterSales.Domain.Exceptions;
using Leno.ReviewAfterSales.Domain.ValueObjects;
using Leno.SharedKernel.Abstractions;

namespace Leno.ReviewAfterSales.Domain.Aggregates;

/// <summary>
/// 评价聚合根，封装评分、文字、图片与审核状态。
/// 状态流转：Pending → Approved；Approved → Hidden（运营隐藏违规评价）。
/// 聚合标识 <see cref="Entity.Id"/> 即对外 <c>ReviewId</c>。
/// </summary>
public sealed class Review : AggregateRoot
{
    /// <summary>订单标识。</summary>
    public Guid OrderId { get; private set; }

    /// <summary>订单行标识，同一订单行仅一条主评价。</summary>
    public Guid OrderLineId { get; private set; }

    /// <summary>商品 SPU 标识。</summary>
    public Guid SpuId { get; private set; }

    /// <summary>SKU 标识。</summary>
    public Guid SkuId { get; private set; }

    /// <summary>评价人（买家）标识。</summary>
    public Guid UserId { get; private set; }

    /// <summary>被评价商品归属卖家标识，由订单域防腐层查询填充，用于卖家回复归属校验。</summary>
    public Guid SellerId { get; private set; }

    /// <summary>评分（1-5）。</summary>
    public int Rating { get; private set; }

    /// <summary>评价文字内容。</summary>
    public string Content { get; private set; } = string.Empty;

    /// <summary>
    /// 图片 URL 列表，仅经聚合根维护，最多 9 张。
    /// 通过 <see cref="Images"/> 只读视图对外暴露，防止外部 mutate 内部集合。
    /// </summary>
    private List<string> _images = [];
    public IReadOnlyList<string> Images => _images.AsReadOnly();

    /// <summary>审核状态。</summary>
    public ReviewStatus Status { get; private set; }

    /// <summary>卖家回复内容，可空。</summary>
    public string? SellerReplyContent { get; private set; }

    /// <summary>卖家回复操作人标识，回复后填充，用于审计。</summary>
    public Guid? SellerReplyBy { get; private set; }

    /// <summary>卖家回复时间（UTC），回复后填充。</summary>
    public DateTime? SellerReplyAt { get; private set; }

    /// <summary>提交时间（UTC）。</summary>
    public DateTime SubmittedAt { get; private set; }

    /// <summary>审核时间（UTC），审核后填充。</summary>
    public DateTime? AuditedAt { get; private set; }

    /// <summary>审核人标识。</summary>
    public Guid? AuditorId { get; private set; }

    /// <summary>隐藏时间（UTC），隐藏后填充。</summary>
    public DateTime? HiddenAt { get; private set; }

    /// <summary>隐藏操作人标识。</summary>
    public Guid? HiddenBy { get; private set; }

    /// <summary>隐藏原因。</summary>
    public string? HideReason { get; private set; }

    /// <summary>EF Core 无参构造。</summary>
    private Review() { }

    private Review(Guid id) : base(id) { }

    /// <summary>
    /// 工厂方法，校验评分 1-5、图片不超过 9 张、内容非空，置待审核态并发布 <see cref="ReviewSubmittedDomainEvent"/>。
    /// </summary>
    /// <param name="reviewId">评价标识，由应用层生成。</param>
    /// <param name="orderId">订单标识。</param>
    /// <param name="orderLineId">订单行标识。</param>
    /// <param name="spuId">商品 SPU 标识。</param>
    /// <param name="skuId">SKU 标识。</param>
    /// <param name="userId">评价人标识。</param>
    /// <param name="rating">评分，须 1-5。</param>
    /// <param name="content">文字内容，1-500 字。</param>
    /// <param name="images">图片 URL 列表，最多 9 张。</param>
    /// <param name="sellerId">被评价商品归属卖家标识，由订单域防腐层查询填充，用于卖家回复归属校验。</param>
    /// <param name="newScore">提交后商品的新加权平均分，由应用层计算后传入。</param>
    /// <param name="reviewCount">提交后商品的可见评价总数，由应用层计算后传入。</param>
    public static Review Create(
        Guid reviewId,
        Guid orderId,
        Guid orderLineId,
        Guid spuId,
        Guid skuId,
        Guid userId,
        int rating,
        string content,
        List<string> images,
        Guid sellerId,
        double newScore = 0,
        int reviewCount = 0)
    {
        if (reviewId == Guid.Empty)
        {
            throw new ReviewDomainException("ReviewId 不可为空", "REVIEW_ID_EMPTY");
        }

        if (orderId == Guid.Empty)
        {
            throw new ReviewDomainException("OrderId 不可为空", "REVIEW_ORDER_EMPTY");
        }

        if (orderLineId == Guid.Empty)
        {
            throw new ReviewDomainException("OrderLineId 不可为空", "REVIEW_ORDER_LINE_EMPTY");
        }

        if (spuId == Guid.Empty)
        {
            throw new ReviewDomainException("SpuId 不可为空", "REVIEW_SPU_EMPTY");
        }

        if (skuId == Guid.Empty)
        {
            throw new ReviewDomainException("SkuId 不可为空", "REVIEW_SKU_EMPTY");
        }

        if (userId == Guid.Empty)
        {
            throw new ReviewDomainException("UserId 不可为空", "REVIEW_USER_EMPTY");
        }

        if (sellerId == Guid.Empty)
        {
            throw new ReviewDomainException("SellerId 不可为空", "REVIEW_SELLER_EMPTY");
        }

        if (rating < 1 || rating > 5)
        {
            throw new ReviewDomainException($"评分越界：{rating}，须 1-5", "REVIEW_RATING_INVALID");
        }

        if (string.IsNullOrWhiteSpace(content))
        {
            throw new ReviewDomainException("评价内容不可为空", "REVIEW_CONTENT_EMPTY");
        }

        if (content.Length > 500)
        {
            throw new ReviewDomainException("评价内容不可超过 500 字", "REVIEW_CONTENT_TOO_LONG");
        }

        var imageList = (images ?? []).ToList();
        if (imageList.Count > 9)
        {
            throw new ReviewDomainException($"图片数量超限：{imageList.Count}，最多 9 张", "REVIEW_IMAGES_TOO_MANY");
        }

        var review = new Review(reviewId)
        {
            OrderId = orderId,
            OrderLineId = orderLineId,
            SpuId = spuId,
            SkuId = skuId,
            UserId = userId,
            SellerId = sellerId,
            Rating = rating,
            Content = content,
            Status = ReviewStatus.Pending,
            SubmittedAt = DateTime.UtcNow
        };

        // 直接赋 backing field，避免经 Images 只读视图（无 setter）赋值；
        // imageList 已为防御性拷贝，外部 mutate 不影响聚合内部状态。
        review._images = imageList;

        review.AddDomainEvent(new ReviewSubmittedDomainEvent(reviewId, userId, spuId, rating, newScore, reviewCount));

        return review;
    }

    /// <summary>
    /// 卖家回复评价，校验已通过态、卖家归属与回复内容长度，写入 <see cref="SellerReplyContent"/>、<see cref="SellerReplyBy"/>、<see cref="SellerReplyAt"/>。
    /// 仅已通过评价可回复，且仅归属卖家可回复（防止任意卖家回复他人商品评价）。
    /// </summary>
    /// <param name="sellerId">回复卖家标识，须等于 <see cref="SellerId"/>。</param>
    /// <param name="content">回复内容，1-500 字。</param>
    public void SellerReply(Guid sellerId, string content)
    {
        if (Status != ReviewStatus.Approved)
        {
            throw new ReviewDomainException(
                $"当前状态 {Status} 不可回复，仅 Approved 可回复",
                "REVIEW_REPLY_STATUS_INVALID");
        }

        if (sellerId == Guid.Empty)
        {
            throw new ReviewDomainException("SellerId 不可为空", "REVIEW_SELLER_EMPTY");
        }

        if (sellerId != SellerId)
        {
            throw new ReviewDomainException("无权回复此评价", "REVIEW_NOT_OWNED");
        }

        if (string.IsNullOrWhiteSpace(content))
        {
            throw new ReviewDomainException("回复内容不可为空", "REVIEW_REPLY_EMPTY");
        }

        if (content.Length > 500)
        {
            throw new ReviewDomainException("回复内容不可超过 500 字", "REVIEW_REPLY_TOO_LONG");
        }

        SellerReplyContent = content;
        SellerReplyBy = sellerId;
        SellerReplyAt = DateTime.UtcNow;
    }

    /// <summary>
	    /// 运营审核通过，校验待审核态，置已通过态并发布 <see cref="ReviewApprovedDomainEvent"/>。
	    /// ReviewApprovedDomainEvent 驱动积分域发放评价积分。
	    /// </summary>
	    /// <param name="auditorId">审核人标识。</param>
	    public void Approve(Guid auditorId)
	    {
	        if (Status != ReviewStatus.Pending)
	        {
	            throw new ReviewDomainException(
	                $"当前状态 {Status} 不可审核通过，仅 Pending 可审核",
	                "REVIEW_APPROVE_STATUS_INVALID");
	        }

	        if (auditorId == Guid.Empty)
	        {
	            throw new ReviewDomainException("AuditorId 不可为空", "REVIEW_AUDITOR_EMPTY");
	        }

	        Status = ReviewStatus.Approved;
	        AuditedAt = DateTime.UtcNow;
	        AuditorId = auditorId;
	        AddDomainEvent(new ReviewApprovedDomainEvent(Id, UserId, SpuId, Rating));
	    }

    /// <summary>
	    /// 运营隐藏违规评价，校验已通过态，置已隐藏态并发布 <see cref="ReviewHiddenDomainEvent"/>。
	    /// ReviewHiddenDomainEvent 驱动商品域从评分统计中移除该评价。
	    /// 隐藏后买家侧不可见但聚合记录保留供审计，已隐藏为终态不可逆。
	    /// </summary>
	    /// <param name="operatorId">操作人标识。</param>
	    /// <param name="reason">隐藏原因，1-200 字。</param>
	    public void Hide(Guid operatorId, string reason)
	    {
	        if (Status != ReviewStatus.Approved)
	        {
	            throw new ReviewDomainException(
	                $"当前状态 {Status} 不可隐藏，仅 Approved 可隐藏",
	                "REVIEW_HIDE_STATUS_INVALID");
	        }

	        if (operatorId == Guid.Empty)
	        {
	            throw new ReviewDomainException("OperatorId 不可为空", "REVIEW_OPERATOR_EMPTY");
	        }

	        if (string.IsNullOrWhiteSpace(reason))
	        {
	            throw new ReviewDomainException("隐藏原因不可为空", "REVIEW_HIDE_REASON_EMPTY");
	        }

	        if (reason.Length > 200)
	        {
	            throw new ReviewDomainException("隐藏原因不可超过 200 字", "REVIEW_HIDE_REASON_TOO_LONG");
	        }

	        Status = ReviewStatus.Hidden;
	        HiddenAt = DateTime.UtcNow;
	        HiddenBy = operatorId;
	        HideReason = reason;
	        AddDomainEvent(new ReviewHiddenDomainEvent(Id, SpuId, Rating));
	    }
}
