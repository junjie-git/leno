using Leno.ReviewAfterSales.Domain.Events;
using Leno.ReviewAfterSales.Domain.Exceptions;
using Leno.ReviewAfterSales.Domain.ValueObjects;
using Leno.SharedKernel.Abstractions;

namespace Leno.ReviewAfterSales.Domain.Aggregates;

/// <summary>
/// 售后单聚合根，封装售后类型、申请金额与状态机。
/// 状态流转：
///   Pending → Approved/Rejected/Cancelled；
///   Approved → ReturnGoods（退货退款）→ ConfirmReturn → Refunding → Completed/Failed；
///   或 Approved → Refunding（仅退款）→ Completed/Failed。
/// 聚合标识 <see cref="Entity.Id"/> 即对外 <c>AfterSalesId</c>。
/// </summary>
public sealed class AfterSales : AggregateRoot
{
    /// <summary>订单标识。</summary>
    public Guid OrderId { get; private set; }

    /// <summary>订单行标识，整单售后时为空。</summary>
    public Guid? OrderLineId { get; private set; }

    /// <summary>申请人（买家）标识。</summary>
    public Guid UserId { get; private set; }

    /// <summary>被申请卖家标识，语义等同卖家与店铺管理域的 ShopId。</summary>
    public Guid SellerId { get; private set; }

    /// <summary>售后类型。</summary>
    public AfterSalesType Type { get; private set; }

    /// <summary>原因分类，如"质量问题""七天无理由"。</summary>
    public string ReasonCategory { get; private set; } = string.Empty;

    /// <summary>申请原因描述。</summary>
    public string Reason { get; private set; } = string.Empty;

    /// <summary>
    /// 凭证图片 URL 列表，仅经聚合根维护，最多 5 张。
    /// 持久化为聚合子集合，私有 setter 阻止外部整体替换。
    /// </summary>
    private List<string> _images = [];
    public List<string> Images { get => _images; private set => _images = value ?? []; }

    /// <summary>申请金额。</summary>
    public decimal RequestedAmount { get; private set; }

    /// <summary>币种（ISO 4217），默认 CNY。</summary>
    public string Currency { get; private set; } = "CNY";

    /// <summary>审核同意金额，审核后填充。</summary>
    public decimal? ApprovedAmount { get; private set; }

    /// <summary>实际退款金额，退款完成后填充。</summary>
    public decimal? RefundedAmount { get; private set; }

    /// <summary>售后状态。</summary>
    public AfterSalesStatus Status { get; private set; }

    /// <summary>申请时间（UTC）。</summary>
    public DateTime AppliedAt { get; private set; }

    /// <summary>审核时间（UTC）。</summary>
    public DateTime? ApprovedAt { get; private set; }

    /// <summary>审核人标识（卖家或运营）。</summary>
    public Guid? ApproverId { get; private set; }

    /// <summary>退款完成时间（UTC）。</summary>
    public DateTime? RefundedAt { get; private set; }

    /// <summary>渠道退款单号。</summary>
    public string? ChannelRefundNo { get; private set; }

    /// <summary>驳回原因。</summary>
    public string? RejectReason { get; private set; }

    /// <summary>退款失败原因。</summary>
    public string? FailReason { get; private set; }

    /// <summary>撤销时间（UTC）。</summary>
    public DateTime? CancelledAt { get; private set; }

    /// <summary>撤销原因。</summary>
    public string? CancelReason { get; private set; }

    /// <summary>买家退货时间（UTC）。</summary>
    public DateTime? ReturnedAt { get; private set; }

    /// <summary>退货物流单号。</summary>
    public string? TrackingNo { get; private set; }

    /// <summary>卖家确认收货时间（UTC）。</summary>
    public DateTime? ReturnConfirmedAt { get; private set; }

    /// <summary>EF Core 无参构造。</summary>
    private AfterSales() { }

    private AfterSales(Guid id) : base(id) { }

    /// <summary>
    /// 工厂方法，校验入参合法、申请金额 &gt; 0，置待审核态并发布 <see cref="AfterSalesSubmittedDomainEvent"/>。
    /// </summary>
    /// <param name="afterSalesId">售后单标识，由应用层生成。</param>
    /// <param name="orderId">订单标识。</param>
    /// <param name="orderLineId">订单行标识，整单售后可空。</param>
    /// <param name="userId">申请人标识。</param>
    /// <param name="sellerId">被申请卖家标识。</param>
    /// <param name="type">售后类型。</param>
    /// <param name="reasonCategory">原因分类。</param>
    /// <param name="reason">申请原因描述，1-500 字。</param>
    /// <param name="images">凭证图片 URL 列表，最多 5 张。</param>
    /// <param name="requestedAmount">申请金额，须 &gt; 0。</param>
    /// <param name="currency">币种，为空默认 CNY。</param>
    public static AfterSales Create(
        Guid afterSalesId,
        Guid orderId,
        Guid? orderLineId,
        Guid userId,
        Guid sellerId,
        AfterSalesType type,
        string reasonCategory,
        string reason,
        List<string> images,
        decimal requestedAmount,
        string currency)
    {
        if (afterSalesId == Guid.Empty)
        {
            throw new ReviewDomainException("AfterSalesId 不可为空", "AFTERSALES_ID_EMPTY");
        }

        if (orderId == Guid.Empty)
        {
            throw new ReviewDomainException("OrderId 不可为空", "AFTERSALES_ORDER_EMPTY");
        }

        if (userId == Guid.Empty)
        {
            throw new ReviewDomainException("UserId 不可为空", "AFTERSALES_USER_EMPTY");
        }

        if (sellerId == Guid.Empty)
        {
            throw new ReviewDomainException("SellerId 不可为空", "AFTERSALES_SELLER_EMPTY");
        }

        if (!Enum.IsDefined(type))
        {
            throw new ReviewDomainException($"售后类型非法：{type}", "AFTERSALES_TYPE_INVALID");
        }

        if (string.IsNullOrWhiteSpace(reasonCategory))
        {
            throw new ReviewDomainException("原因分类不可为空", "AFTERSALES_REASON_CATEGORY_EMPTY");
        }

        if (string.IsNullOrWhiteSpace(reason))
        {
            throw new ReviewDomainException("申请原因不可为空", "AFTERSALES_REASON_EMPTY");
        }

        if (reason.Length > 500)
        {
            throw new ReviewDomainException("申请原因不可超过 500 字", "AFTERSALES_REASON_TOO_LONG");
        }

        var imageList = images ?? [];
        if (imageList.Count > 5)
        {
            throw new ReviewDomainException($"凭证图片数量超限：{imageList.Count}，最多 5 张", "AFTERSALES_IMAGES_TOO_MANY");
        }

        if (requestedAmount <= 0)
        {
            throw new ReviewDomainException("申请金额须大于 0", "AFTERSALES_AMOUNT_INVALID");
        }

        var afterSales = new AfterSales(afterSalesId)
        {
            OrderId = orderId,
            OrderLineId = orderLineId,
            UserId = userId,
            SellerId = sellerId,
            Type = type,
            ReasonCategory = reasonCategory,
            Reason = reason,
            RequestedAmount = requestedAmount,
            Currency = string.IsNullOrWhiteSpace(currency) ? "CNY" : currency,
            Status = AfterSalesStatus.Pending,
            AppliedAt = DateTime.UtcNow,
            Images = imageList
        };

        afterSales.AddDomainEvent(new AfterSalesSubmittedDomainEvent(
            afterSalesId, orderId, orderLineId, userId, sellerId, (int)type, requestedAmount, currency));

        return afterSales;
    }

    /// <summary>
    /// 审核同意，校验待审核态与同意金额 ≤ 申请金额，置已同意态并发布 <see cref="AfterSalesApprovedDomainEvent"/>。
    /// </summary>
    /// <param name="operatorId">审核人标识（卖家或运营）。</param>
    /// <param name="approvedAmount">审核同意金额，须 ≤ <see cref="RequestedAmount"/>。</param>
    public void Approve(Guid operatorId, decimal approvedAmount)
    {
        if (Status != AfterSalesStatus.Pending)
        {
            throw new ReviewDomainException(
                $"当前状态 {Status} 不可审核同意，仅 Pending 可审核",
                "AFTERSALES_APPROVE_STATUS_INVALID");
        }

        if (operatorId == Guid.Empty)
        {
            throw new ReviewDomainException("OperatorId 不可为空", "AFTERSALES_OPERATOR_EMPTY");
        }

        if (approvedAmount <= 0 || approvedAmount > RequestedAmount)
        {
            throw new ReviewDomainException(
                $"审核同意金额非法：{approvedAmount}，须 > 0 且 ≤ 申请金额 {RequestedAmount}",
                "AFTERSALES_APPROVED_AMOUNT_INVALID");
        }

        Status = AfterSalesStatus.Approved;
        ApprovedAmount = approvedAmount;
        ApprovedAt = DateTime.UtcNow;
        ApproverId = operatorId;
        AddDomainEvent(new AfterSalesApprovedDomainEvent(
            Id, OrderId, UserId, SellerId, approvedAmount, Currency, (int)Type));
    }

    /// <summary>
    /// 审核驳回，校验待审核态，置已驳回态并记录驳回原因，发布 <see cref="AfterSalesRejectedDomainEvent"/>。
    /// </summary>
    /// <param name="operatorId">审核人标识。</param>
    /// <param name="reason">驳回原因，1-200 字。</param>
    public void Reject(Guid operatorId, string reason)
    {
        if (Status != AfterSalesStatus.Pending)
        {
            throw new ReviewDomainException(
                $"当前状态 {Status} 不可驳回，仅 Pending 可驳回",
                "AFTERSALES_REJECT_STATUS_INVALID");
        }

        if (operatorId == Guid.Empty)
        {
            throw new ReviewDomainException("OperatorId 不可为空", "AFTERSALES_OPERATOR_EMPTY");
        }

        if (string.IsNullOrWhiteSpace(reason))
        {
            throw new ReviewDomainException("驳回原因不可为空", "AFTERSALES_REJECT_REASON_EMPTY");
        }

        if (reason.Length > 200)
        {
            throw new ReviewDomainException("驳回原因不可超过 200 字", "AFTERSALES_REJECT_REASON_TOO_LONG");
        }

        Status = AfterSalesStatus.Rejected;
        RejectReason = reason;
        ApproverId = operatorId;
        ApprovedAt = DateTime.UtcNow;
        AddDomainEvent(new AfterSalesRejectedDomainEvent(Id, OrderId, UserId, reason));
    }

    /// <summary>
    /// 买家退货，仅退货退款类型在已同意态可调用，置已退货态并发布 <see cref="AfterSalesReturnedDomainEvent"/>。
    /// </summary>
    /// <param name="trackingNo">退货物流单号，不可为空。</param>
    public void ReturnGoods(string trackingNo)
    {
        if (Type != AfterSalesType.ReturnRefund)
        {
            throw new ReviewDomainException(
                $"售后类型 {Type} 不可退货，仅 ReturnRefund 可退货",
                "AFTERSALES_RETURN_TYPE_INVALID");
        }

        if (Status != AfterSalesStatus.Approved)
        {
            throw new ReviewDomainException(
                $"当前状态 {Status} 不可退货，仅 Approved 可退货",
                "AFTERSALES_RETURN_STATUS_INVALID");
        }

        if (string.IsNullOrWhiteSpace(trackingNo))
        {
            throw new ReviewDomainException("退货物流单号不可为空", "AFTERSALES_TRACKING_NO_EMPTY");
        }

        if (trackingNo.Length > 64)
        {
            throw new ReviewDomainException("退货物流单号不可超过 64 字", "AFTERSALES_TRACKING_NO_TOO_LONG");
        }

        Status = AfterSalesStatus.ReturnGoods;
        ReturnedAt = DateTime.UtcNow;
        TrackingNo = trackingNo;
        AddDomainEvent(new AfterSalesReturnedDomainEvent(Id, OrderId, SellerId, trackingNo));
    }

    /// <summary>
    /// 卖家确认收货，校验已退货态，置已确认收货态并发布 <see cref="AfterSalesReturnConfirmedDomainEvent"/>。
    /// </summary>
    public void ConfirmReturn()
    {
        if (Status != AfterSalesStatus.ReturnGoods)
        {
            throw new ReviewDomainException(
                $"当前状态 {Status} 不可确认收货，仅 ReturnGoods 可确认",
                "AFTERSALES_CONFIRM_RETURN_STATUS_INVALID");
        }

        Status = AfterSalesStatus.ConfirmReturn;
        ReturnConfirmedAt = DateTime.UtcNow;
        AddDomainEvent(new AfterSalesReturnConfirmedDomainEvent(
            Id, OrderId, UserId, ApprovedAmount ?? RequestedAmount));
    }

    /// <summary>
	    /// 进入退款中，校验类型与状态：仅退款须 Approved 态，退货退款须 ConfirmReturn 态，置退款中态。
	    /// </summary>
	    public void MarkRefunding()
	    {
	        var isValid = Type switch
	        {
	            AfterSalesType.RefundOnly => Status == AfterSalesStatus.Approved,
	            AfterSalesType.ReturnRefund => Status == AfterSalesStatus.ConfirmReturn,
	            _ => false
	        };

	        if (!isValid)
	        {
	            throw new ReviewDomainException(
	                $"当前状态 {Status} 与类型 {Type} 不可进入退款中",
	                "AFTERSALES_REFUNDING_STATUS_INVALID");
	        }

	        Status = AfterSalesStatus.Refunding;
	    }

    /// <summary>
    /// 标记退款完成，校验退款中态与退款金额 ≤ 审核同意金额，置已完成态并发布 <see cref="AfterSalesRefundCompletedDomainEvent"/>。
    /// 实际退款由支付集成域执行，本方法仅记录退款事实并通知下游（订单域回滚销量、促销域退还优惠券等）。
    /// </summary>
    /// <param name="refundId">退款单标识。</param>
    /// <param name="amount">退款金额，须 ≤ <see cref="ApprovedAmount"/>。</param>
    /// <param name="channelRefundNo">渠道退款单号。</param>
    public void MarkRefundCompleted(Guid refundId, decimal amount, string? channelRefundNo)
    {
        if (Status != AfterSalesStatus.Refunding)
        {
            throw new ReviewDomainException(
                $"当前状态 {Status} 不可标记退款完成，仅 Refunding 可标记",
                "AFTERSALES_REFUND_COMPLETED_STATUS_INVALID");
        }

        if (refundId == Guid.Empty)
        {
            throw new ReviewDomainException("RefundId 不可为空", "AFTERSALES_REFUND_ID_EMPTY");
        }

        var approved = ApprovedAmount ?? 0;
        if (amount <= 0 || amount > approved)
        {
            throw new ReviewDomainException(
                $"退款金额非法：{amount}，须 > 0 且 ≤ 审核同意金额 {approved}",
                "AFTERSALES_REFUND_AMOUNT_INVALID");
        }

        Status = AfterSalesStatus.Completed;
        RefundedAmount = amount;
        RefundedAt = DateTime.UtcNow;
        ChannelRefundNo = channelRefundNo;
        AddDomainEvent(new AfterSalesRefundCompletedDomainEvent(OrderId, UserId, refundId, Id, amount, Currency, RefundedAt.Value));
    }

    /// <summary>
    /// 标记退款失败，校验退款中态，置已失败态并记录失败原因，发布 <see cref="AfterSalesRefundFailedDomainEvent"/>。
    /// </summary>
    /// <param name="reason">失败原因，1-512 字。</param>
    public void MarkRefundFailed(string reason)
    {
        if (Status != AfterSalesStatus.Refunding)
        {
            throw new ReviewDomainException(
                $"当前状态 {Status} 不可标记退款失败，仅 Refunding 可标记",
                "AFTERSALES_REFUND_FAILED_STATUS_INVALID");
        }

        if (string.IsNullOrWhiteSpace(reason))
        {
            throw new ReviewDomainException("失败原因不可为空", "AFTERSALES_FAIL_REASON_EMPTY");
        }

        if (reason.Length > 512)
        {
            throw new ReviewDomainException("失败原因不可超过 512 字", "AFTERSALES_FAIL_REASON_TOO_LONG");
        }

        Status = AfterSalesStatus.Failed;
        FailReason = reason;
        AddDomainEvent(new AfterSalesRefundFailedDomainEvent(Id, OrderId, UserId, reason));
    }

    /// <summary>
    /// 发布退款请求集成事件，经发件箱模式在同一事务内持久化以保障原子性。
    /// 由应用层在审核通过/确认收货后调用，校验 Refunding 态。
    /// 事件携带 PaymentId、RefundAmount、RefundReason、AfterSalesId 供支付域执行退款。
    /// </summary>
    /// <param name="refundId">退款单标识，由应用层生成。</param>
    /// <param name="paymentId">支付单标识，由应用层通过防腐层查询。</param>
    /// <param name="refundAmount">退款金额。</param>
    /// <param name="channel">支付渠道。</param>
    /// <param name="refundReason">退款原因。</param>
    public void AddRefundRequestedEvent(Guid refundId, Guid paymentId, decimal refundAmount, string channel, string refundReason)
    {
        if (Status != AfterSalesStatus.Refunding)
        {
            throw new ReviewDomainException(
                $"当前状态 {Status} 不可发布退款请求，仅 Refunding 可发布",
                "AFTERSALES_REFUND_REQUEST_STATUS_INVALID");
        }

        if (refundId == Guid.Empty)
        {
            throw new ReviewDomainException("RefundId 不可为空", "AFTERSALES_REFUND_ID_EMPTY");
        }

        if (paymentId == Guid.Empty)
        {
            throw new ReviewDomainException("PaymentId 不可为空", "AFTERSALES_PAYMENT_ID_EMPTY");
        }

        if (refundAmount <= 0)
        {
            throw new ReviewDomainException("退款金额须大于 0", "AFTERSALES_REFUND_AMOUNT_INVALID");
        }

        AddDomainEvent(new AfterSalesRefundRequestedDomainEvent(
            refundId, OrderId, UserId, Id,
            paymentId, refundAmount, Currency, channel, refundReason));
    }

    /// <summary>
    /// 买家撤销，仅在待审核或已同意（未退货）态可调用，置已撤销态并记录撤销原因，发布 <see cref="AfterSalesCancelledDomainEvent"/>。
    /// 校验撤销人为申请人本人（合并审计 2.6：归属校验），reason 非空且不超过 200 字（合并审计 4.2）。
    /// </summary>
    /// <param name="userId">撤销人标识，须等于 <see cref="UserId"/>。</param>
    /// <param name="reason">撤销原因，1-200 字。</param>
    public void Cancel(Guid userId, string reason)
    {
        if (Status != AfterSalesStatus.Pending && Status != AfterSalesStatus.Approved)
        {
            throw new ReviewDomainException(
                $"当前状态 {Status} 不可撤销，仅 Pending 或 Approved 可撤销",
                "AFTERSALES_CANCEL_STATUS_INVALID");
        }

        if (userId == Guid.Empty)
        {
            throw new ReviewDomainException("UserId 不可为空", "AFTERSALES_USER_EMPTY");
        }

        if (userId != UserId)
        {
            throw new ReviewDomainException("仅申请人可撤销售后单", "AFTERSALES_CANCEL_NOT_OWNER");
        }

        if (string.IsNullOrWhiteSpace(reason))
        {
            throw new ReviewDomainException("撤销原因不可为空", "AFTERSALES_CANCEL_REASON_EMPTY");
        }

        if (reason.Length > 200)
        {
            throw new ReviewDomainException("撤销原因不可超过 200 字", "AFTERSALES_CANCEL_REASON_TOO_LONG");
        }

        Status = AfterSalesStatus.Cancelled;
        CancelledAt = DateTime.UtcNow;
        CancelReason = reason;
        AddDomainEvent(new AfterSalesCancelledDomainEvent(Id, OrderId, UserId, SellerId, reason));
    }
}
