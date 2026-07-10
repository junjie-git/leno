using Leno.ReviewAfterSales.Domain.Exceptions;
using Leno.ReviewAfterSales.Domain.ValueObjects;
using Leno.SharedContracts.Events;
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

    /// <summary>评分（1-5）。</summary>
    public int Rating { get; private set; }

    /// <summary>评价文字内容。</summary>
    public string Content { get; private set; } = string.Empty;

    /// <summary>
    /// 图片 URL 列表，仅经聚合根维护，最多 9 张。
    /// 持久化为聚合子集合，私有 setter 阻止外部整体替换。
    /// </summary>
    private List<string> _images = [];
    public List<string> Images { get => _images; private set => _images = value ?? []; }

    /// <summary>审核状态。</summary>
    public ReviewStatus Status { get; private set; }

    /// <summary>卖家回复内容，可空。</summary>
    public string? SellerReplyContent { get; private set; }

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
    /// 工厂方法，校验评分 1-5、图片不超过 9 张、内容非空，置待审核态并发布 <see cref="ReviewSubmittedEvent"/>。
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
    public static Review Create(
        Guid reviewId,
        Guid orderId,
        Guid orderLineId,
        Guid spuId,
        Guid skuId,
        Guid userId,
        int rating,
        string content,
        List<string> images)
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

        var imageList = images ?? [];
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
            Rating = rating,
            Content = content,
            Status = ReviewStatus.Pending,
            SubmittedAt = DateTime.UtcNow,
            Images = imageList
        };

        review.AddDomainEvent(new ReviewSubmittedEvent(reviewId, userId, spuId, rating));

        return review;
    }

    /// <summary>
    /// 卖家回复评价，校验已通过态与回复内容长度，写入 <see cref="SellerReplyContent"/>。
    /// 仅已通过评价可回复。
    /// </summary>
    /// <param name="content">回复内容，1-500 字。</param>
    public void SellerReply(string content)
    {
        if (Status != ReviewStatus.Approved)
        {
            throw new ReviewDomainException(
                $"当前状态 {Status} 不可回复，仅 Approved 可回复",
                "REVIEW_REPLY_STATUS_INVALID");
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
    }

    /// <summary>
    /// 运营审核通过，校验待审核态，置已通过态并发布 <see cref="ReviewModeratedEvent"/>。
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
        AddDomainEvent(new ReviewModeratedEvent(Id, (int)ReviewStatus.Approved, "approve"));
    }

    /// <summary>
    /// 运营隐藏违规评价，校验已通过态，置已隐藏态并发布 <see cref="ReviewModeratedEvent"/>。
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
        AddDomainEvent(new ReviewModeratedEvent(Id, (int)ReviewStatus.Hidden, "hide"));
    }
}
