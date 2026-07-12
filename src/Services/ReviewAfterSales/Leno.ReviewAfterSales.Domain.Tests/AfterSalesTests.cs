using Leno.ReviewAfterSales.Domain.Aggregates;
using Leno.ReviewAfterSales.Domain.Exceptions;
using Leno.ReviewAfterSales.Domain.ValueObjects;
using Leno.SharedContracts.Events;
using Leno.SharedKernel.Abstractions;

namespace Leno.ReviewAfterSales.Domain.Tests;

public class AfterSalesTests
{
    private static readonly Guid ValidAfterSalesId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid ValidOrderId = Guid.Parse("22222222-2222-2222-2222-222222222222");
    private static readonly Guid ValidUserId = Guid.Parse("33333333-3333-3333-3333-333333333333");
    private static readonly Guid ValidSellerId = Guid.Parse("44444444-4444-4444-4444-444444444444");
    private static readonly Guid ValidOperatorId = Guid.Parse("55555555-5555-5555-5555-555555555555");

    #region Create - Happy Path

    [Fact]
    public void Create_ReturnRefund_AllValidParameters_ShouldCreateAfterSales()
    {
        var afterSales = AfterSales.Create(
            ValidAfterSalesId, ValidOrderId, Guid.NewGuid(),
            ValidUserId, ValidSellerId,
            AfterSalesType.ReturnRefund, "质量问题", "商品有瑕疵",
            new List<string> { "img1.jpg" }, 100m, "CNY");

        afterSales.Id.Should().Be(ValidAfterSalesId);
        afterSales.OrderId.Should().Be(ValidOrderId);
        afterSales.Type.Should().Be(AfterSalesType.ReturnRefund);
        afterSales.Status.Should().Be(AfterSalesStatus.Pending);
        afterSales.RequestedAmount.Should().Be(100m);
        afterSales.AppliedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
    }

    [Fact]
    public void Create_RefundOnly_AllValidParameters_ShouldCreateAfterSales()
    {
        var afterSales = AfterSales.Create(
            ValidAfterSalesId, ValidOrderId, null,
            ValidUserId, ValidSellerId,
            AfterSalesType.RefundOnly, "不想要了", "七天无理由退货",
            new List<string>(), 50m, "CNY");

        afterSales.Type.Should().Be(AfterSalesType.RefundOnly);
        afterSales.OrderLineId.Should().BeNull();
        afterSales.Images.Should().BeEmpty();
    }

    [Fact]
    public void Create_ShouldRaiseAfterSalesSubmittedEvent()
    {
        var afterSales = AfterSales.Create(
            ValidAfterSalesId, ValidOrderId, Guid.NewGuid(),
            ValidUserId, ValidSellerId,
            AfterSalesType.ReturnRefund, "质量问题", "商品有瑕疵",
            new List<string>(), 100m, "CNY");

        afterSales.DomainEvents.Should().HaveCount(1);
        var evt = afterSales.DomainEvents.Single().Should().BeOfType<AfterSalesSubmittedEvent>().Subject;
        evt.AfterSalesId.Should().Be(ValidAfterSalesId);
        evt.UserId.Should().Be(ValidUserId);
        evt.RequestedAmount.Should().Be(100m);
        evt.Type.Should().Be((int)AfterSalesType.ReturnRefund);
    }

    [Fact]
    public void Create_DefaultCurrency_ShouldBeCNY()
    {
        var afterSales = AfterSales.Create(
            ValidAfterSalesId, ValidOrderId, null,
            ValidUserId, ValidSellerId,
            AfterSalesType.RefundOnly, "不想要了", "七天无理由",
            new List<string>(), 50m, "");

        afterSales.Currency.Should().Be("CNY");
    }

    [Fact]
    public void Create_ImagesAtMaximumBoundary_ShouldCreateSuccessfully()
    {
        var images = Enumerable.Range(1, 5).Select(i => $"img{i}.jpg").ToList();

        var afterSales = AfterSales.Create(
            ValidAfterSalesId, ValidOrderId, null, ValidUserId, ValidSellerId,
            AfterSalesType.RefundOnly, "不想要了", "七天无理由", images, 50m, "CNY");

        afterSales.Images.Should().HaveCount(5);
    }

    [Fact]
    public void Create_ZeroImages_ShouldSucceed()
    {
        var afterSales = AfterSales.Create(
            ValidAfterSalesId, ValidOrderId, null, ValidUserId, ValidSellerId,
            AfterSalesType.RefundOnly, "不想要了", "七天无理由", new List<string>(), 50m, "CNY");

        afterSales.Images.Should().BeEmpty();
    }

    [Fact]
    public void Create_ReasonAtMaximumBoundary_ShouldCreateSuccessfully()
    {
        var reason = new string('A', 500);

        var afterSales = AfterSales.Create(
            ValidAfterSalesId, ValidOrderId, null, ValidUserId, ValidSellerId,
            AfterSalesType.RefundOnly, "不想要了", reason, new List<string>(), 50m, "CNY");

        afterSales.Reason.Should().Be(reason);
        afterSales.Reason.Length.Should().Be(500);
    }

    #endregion

    #region Create - Validation Guards

    [Fact]
    public void Create_EmptyAfterSalesId_ShouldThrow()
    {
        var act = () => AfterSales.Create(
            Guid.Empty, ValidOrderId, null, ValidUserId, ValidSellerId,
            AfterSalesType.RefundOnly, "x", "reason", [], 10m, "CNY");

        act.Should().Throw<ReviewDomainException>()
            .Where(e => e.ErrorCode == "AFTERSALES_ID_EMPTY");
    }

    [Fact]
    public void Create_EmptyOrderId_ShouldThrow()
    {
        var act = () => AfterSales.Create(
            ValidAfterSalesId, Guid.Empty, null, ValidUserId, ValidSellerId,
            AfterSalesType.RefundOnly, "x", "reason", [], 10m, "CNY");

        act.Should().Throw<ReviewDomainException>()
            .Where(e => e.ErrorCode == "AFTERSALES_ORDER_EMPTY");
    }

    [Fact]
    public void Create_EmptyUserId_ShouldThrow()
    {
        var act = () => AfterSales.Create(
            ValidAfterSalesId, ValidOrderId, null, Guid.Empty, ValidSellerId,
            AfterSalesType.RefundOnly, "x", "reason", [], 10m, "CNY");

        act.Should().Throw<ReviewDomainException>()
            .Where(e => e.ErrorCode == "AFTERSALES_USER_EMPTY");
    }

    [Fact]
    public void Create_EmptySellerId_ShouldThrow()
    {
        var act = () => AfterSales.Create(
            ValidAfterSalesId, ValidOrderId, null, ValidUserId, Guid.Empty,
            AfterSalesType.RefundOnly, "x", "reason", [], 10m, "CNY");

        act.Should().Throw<ReviewDomainException>()
            .Where(e => e.ErrorCode == "AFTERSALES_SELLER_EMPTY");
    }

    [Fact]
    public void Create_EmptyReasonCategory_ShouldThrow()
    {
        var act = () => AfterSales.Create(
            ValidAfterSalesId, ValidOrderId, null, ValidUserId, ValidSellerId,
            AfterSalesType.RefundOnly, "", "reason", [], 10m, "CNY");

        act.Should().Throw<ReviewDomainException>()
            .Where(e => e.ErrorCode == "AFTERSALES_REASON_CATEGORY_EMPTY");
    }

    [Fact]
    public void Create_EmptyReason_ShouldThrow()
    {
        var act = () => AfterSales.Create(
            ValidAfterSalesId, ValidOrderId, null, ValidUserId, ValidSellerId,
            AfterSalesType.RefundOnly, "x", "", [], 10m, "CNY");

        act.Should().Throw<ReviewDomainException>()
            .Where(e => e.ErrorCode == "AFTERSALES_REASON_EMPTY");
    }

    [Fact]
    public void Create_ReasonTooLong_ShouldThrow()
    {
        var reason = new string('B', 501);
        var act = () => AfterSales.Create(
            ValidAfterSalesId, ValidOrderId, null, ValidUserId, ValidSellerId,
            AfterSalesType.RefundOnly, "x", reason, [], 10m, "CNY");

        act.Should().Throw<ReviewDomainException>()
            .Where(e => e.ErrorCode == "AFTERSALES_REASON_TOO_LONG");
    }

    [Fact]
    public void Create_ImagesTooMany_ShouldThrow()
    {
        var images = Enumerable.Range(1, 6).Select(i => $"img{i}.jpg").ToList();
        var act = () => AfterSales.Create(
            ValidAfterSalesId, ValidOrderId, null, ValidUserId, ValidSellerId,
            AfterSalesType.RefundOnly, "x", "reason", images, 10m, "CNY");

        act.Should().Throw<ReviewDomainException>()
            .Where(e => e.ErrorCode == "AFTERSALES_IMAGES_TOO_MANY");
    }

    [Fact]
    public void Create_ZeroRequestedAmount_ShouldThrow()
    {
        var act = () => AfterSales.Create(
            ValidAfterSalesId, ValidOrderId, null, ValidUserId, ValidSellerId,
            AfterSalesType.RefundOnly, "x", "reason", [], 0m, "CNY");

        act.Should().Throw<ReviewDomainException>()
            .Where(e => e.ErrorCode == "AFTERSALES_AMOUNT_INVALID");
    }

    [Fact]
    public void Create_NegativeRequestedAmount_ShouldThrow()
    {
        var act = () => AfterSales.Create(
            ValidAfterSalesId, ValidOrderId, null, ValidUserId, ValidSellerId,
            AfterSalesType.RefundOnly, "x", "reason", [], -1m, "CNY");

        act.Should().Throw<ReviewDomainException>()
            .Where(e => e.ErrorCode == "AFTERSALES_AMOUNT_INVALID");
    }

    #endregion

    #region Approve - Happy Path

    [Fact]
    public void Approve_WhenPending_ShouldSetApprovedProperties()
    {
        var afterSales = CreatePendingReturnRefund();

        afterSales.Approve(ValidOperatorId, 80m);

        afterSales.Status.Should().Be(AfterSalesStatus.Approved);
        afterSales.ApprovedAmount.Should().Be(80m);
        afterSales.ApproverId.Should().Be(ValidOperatorId);
        afterSales.ApprovedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
    }

    [Fact]
    public void Approve_WhenPending_ShouldRaiseAfterSalesApprovedEvent()
    {
        var afterSales = CreatePendingReturnRefund();
        afterSales.ClearDomainEvents();

        afterSales.Approve(ValidOperatorId, 80m);

        afterSales.DomainEvents.Should().HaveCount(1);
        var evt = afterSales.DomainEvents.Single().Should().BeOfType<AfterSalesApprovedEvent>().Subject;
        evt.AfterSalesId.Should().Be(ValidAfterSalesId);
        evt.ApprovedAmount.Should().Be(80m);
    }

    [Fact]
    public void Approve_FullAmount_ShouldSetApprovedAmount()
    {
        var afterSales = CreatePendingReturnRefund();

        afterSales.Approve(ValidOperatorId, 100m);

        afterSales.ApprovedAmount.Should().Be(100m);
    }

    #endregion

    #region Approve - Validation Guards

    [Fact]
    public void Approve_WhenAlreadyApproved_ShouldThrow()
    {
        var afterSales = CreateApprovedReturnRefund();
        var act = () => afterSales.Approve(ValidOperatorId, 80m);

        act.Should().Throw<ReviewDomainException>()
            .Where(e => e.ErrorCode == "AFTERSALES_APPROVE_STATUS_INVALID");
    }

    [Fact]
    public void Approve_WhenRejected_ShouldThrow()
    {
        var afterSales = CreateRejectedReturnRefund();
        var act = () => afterSales.Approve(ValidOperatorId, 80m);

        act.Should().Throw<ReviewDomainException>()
            .Where(e => e.ErrorCode == "AFTERSALES_APPROVE_STATUS_INVALID");
    }

    [Fact]
    public void Approve_EmptyOperatorId_ShouldThrow()
    {
        var afterSales = CreatePendingReturnRefund();
        var act = () => afterSales.Approve(Guid.Empty, 80m);

        act.Should().Throw<ReviewDomainException>()
            .Where(e => e.ErrorCode == "AFTERSALES_OPERATOR_EMPTY");
    }

    [Fact]
    public void Approve_ZeroApprovedAmount_ShouldThrow()
    {
        var afterSales = CreatePendingReturnRefund();
        var act = () => afterSales.Approve(ValidOperatorId, 0m);

        act.Should().Throw<ReviewDomainException>()
            .Where(e => e.ErrorCode == "AFTERSALES_APPROVED_AMOUNT_INVALID");
    }

    [Fact]
    public void Approve_AmountExceedsRequested_ShouldThrow()
    {
        var afterSales = CreatePendingReturnRefund();
        var act = () => afterSales.Approve(ValidOperatorId, 101m);

        act.Should().Throw<ReviewDomainException>()
            .Where(e => e.ErrorCode == "AFTERSALES_APPROVED_AMOUNT_INVALID");
    }

    #endregion

    #region Reject - Happy Path

    [Fact]
    public void Reject_WhenPending_ShouldSetRejectedProperties()
    {
        var afterSales = CreatePendingReturnRefund();

        afterSales.Reject(ValidOperatorId, "不符合售后条件");

        afterSales.Status.Should().Be(AfterSalesStatus.Rejected);
        afterSales.RejectReason.Should().Be("不符合售后条件");
        afterSales.ApproverId.Should().Be(ValidOperatorId);
    }

    [Fact]
    public void Reject_WhenPending_ShouldRaiseAfterSalesRejectedEvent()
    {
        var afterSales = CreatePendingReturnRefund();
        afterSales.ClearDomainEvents();

        afterSales.Reject(ValidOperatorId, "不符合售后条件");

        afterSales.DomainEvents.Should().HaveCount(1);
        var evt = afterSales.DomainEvents.Single().Should().BeOfType<AfterSalesRejectedEvent>().Subject;
        evt.AfterSalesId.Should().Be(ValidAfterSalesId);
        evt.RejectReason.Should().Be("不符合售后条件");
    }

    #endregion

    #region Reject - Validation Guards

    [Fact]
    public void Reject_WhenAlreadyApproved_ShouldThrow()
    {
        var afterSales = CreateApprovedReturnRefund();
        var act = () => afterSales.Reject(ValidOperatorId, "reason");

        act.Should().Throw<ReviewDomainException>()
            .Where(e => e.ErrorCode == "AFTERSALES_REJECT_STATUS_INVALID");
    }

    [Fact]
    public void Reject_EmptyReason_ShouldThrow()
    {
        var afterSales = CreatePendingReturnRefund();
        var act = () => afterSales.Reject(ValidOperatorId, "");

        act.Should().Throw<ReviewDomainException>()
            .Where(e => e.ErrorCode == "AFTERSALES_REJECT_REASON_EMPTY");
    }

    [Fact]
    public void Reject_ReasonTooLong_ShouldThrow()
    {
        var afterSales = CreatePendingReturnRefund();
        var reason = new string('C', 201);
        var act = () => afterSales.Reject(ValidOperatorId, reason);

        act.Should().Throw<ReviewDomainException>()
            .Where(e => e.ErrorCode == "AFTERSALES_REJECT_REASON_TOO_LONG");
    }

    #endregion

    #region ReturnGoods - Happy Path

    [Fact]
    public void ReturnGoods_WhenApprovedReturnRefund_ShouldSetReturnedProperties()
    {
        var afterSales = CreateApprovedReturnRefund();

        afterSales.ReturnGoods("SF1234567890");

        afterSales.Status.Should().Be(AfterSalesStatus.ReturnGoods);
        afterSales.TrackingNo.Should().Be("SF1234567890");
        afterSales.ReturnedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
    }

    [Fact]
    public void ReturnGoods_WhenApproved_ShouldRaiseAfterSalesReturnedEvent()
    {
        var afterSales = CreateApprovedReturnRefund();
        afterSales.ClearDomainEvents();

        afterSales.ReturnGoods("SF1234567890");

        afterSales.DomainEvents.Should().HaveCount(1);
        var evt = afterSales.DomainEvents.Single().Should().BeOfType<AfterSalesReturnedEvent>().Subject;
        evt.AfterSalesId.Should().Be(ValidAfterSalesId);
        evt.TrackingNo.Should().Be("SF1234567890");
        evt.SellerId.Should().Be(ValidSellerId);
    }

    #endregion

    #region ReturnGoods - Validation Guards

    [Fact]
    public void ReturnGoods_WhenPending_ShouldThrow()
    {
        var afterSales = CreatePendingReturnRefund();
        var act = () => afterSales.ReturnGoods("tracking");

        act.Should().Throw<ReviewDomainException>()
            .Where(e => e.ErrorCode == "AFTERSALES_RETURN_STATUS_INVALID");
    }

    [Fact]
    public void ReturnGoods_WhenRefundOnly_ShouldThrow()
    {
        var afterSales = CreateApprovedRefundOnly();
        var act = () => afterSales.ReturnGoods("tracking");

        act.Should().Throw<ReviewDomainException>()
            .Where(e => e.ErrorCode == "AFTERSALES_RETURN_TYPE_INVALID");
    }

    [Fact]
    public void ReturnGoods_WhenAlreadyReturned_ShouldThrow()
    {
        var afterSales = CreateReturnedReturnRefund();
        var act = () => afterSales.ReturnGoods("tracking2");

        act.Should().Throw<ReviewDomainException>()
            .Where(e => e.ErrorCode == "AFTERSALES_RETURN_STATUS_INVALID");
    }

    [Fact]
    public void ReturnGoods_EmptyTrackingNo_ShouldThrow()
    {
        var afterSales = CreateApprovedReturnRefund();
        var act = () => afterSales.ReturnGoods("");

        act.Should().Throw<ReviewDomainException>()
            .Where(e => e.ErrorCode == "AFTERSALES_TRACKING_NO_EMPTY");
    }

    [Fact]
    public void ReturnGoods_TrackingNoTooLong_ShouldThrow()
    {
        var afterSales = CreateApprovedReturnRefund();
        var trackingNo = new string('T', 65);
        var act = () => afterSales.ReturnGoods(trackingNo);

        act.Should().Throw<ReviewDomainException>()
            .Where(e => e.ErrorCode == "AFTERSALES_TRACKING_NO_TOO_LONG");
    }

    #endregion

    #region ConfirmReturn - Happy Path

    [Fact]
    public void ConfirmReturn_WhenReturnGoods_ShouldSetConfirmedProperties()
    {
        var afterSales = CreateReturnedReturnRefund();

        afterSales.ConfirmReturn();

        afterSales.Status.Should().Be(AfterSalesStatus.ConfirmReturn);
        afterSales.ReturnConfirmedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
    }

    [Fact]
    public void ConfirmReturn_WhenReturnGoods_ShouldRaiseAfterSalesReturnConfirmedEvent()
    {
        var afterSales = CreateReturnedReturnRefund();
        afterSales.ClearDomainEvents();

        afterSales.ConfirmReturn();

        afterSales.DomainEvents.Should().HaveCount(1);
        var evt = afterSales.DomainEvents.Single().Should().BeOfType<AfterSalesReturnConfirmedEvent>().Subject;
        evt.AfterSalesId.Should().Be(ValidAfterSalesId);
        evt.UserId.Should().Be(ValidUserId);
    }

    #endregion

    #region ConfirmReturn - Validation Guards

    [Fact]
    public void ConfirmReturn_WhenApproved_ShouldThrow()
    {
        var afterSales = CreateApprovedReturnRefund();
        var act = () => afterSales.ConfirmReturn();

        act.Should().Throw<ReviewDomainException>()
            .Where(e => e.ErrorCode == "AFTERSALES_CONFIRM_RETURN_STATUS_INVALID");
    }

    [Fact]
    public void ConfirmReturn_WhenPending_ShouldThrow()
    {
        var afterSales = CreatePendingReturnRefund();
        var act = () => afterSales.ConfirmReturn();

        act.Should().Throw<ReviewDomainException>()
            .Where(e => e.ErrorCode == "AFTERSALES_CONFIRM_RETURN_STATUS_INVALID");
    }

    [Fact]
    public void ConfirmReturn_WhenAlreadyConfirmed_ShouldThrow()
    {
        var afterSales = CreateConfirmedReturnRefund();
        var act = () => afterSales.ConfirmReturn();

        act.Should().Throw<ReviewDomainException>()
            .Where(e => e.ErrorCode == "AFTERSALES_CONFIRM_RETURN_STATUS_INVALID");
    }

    #endregion

    #region MarkRefunding - Happy Path

    [Fact]
    public void MarkRefunding_WhenApprovedRefundOnly_ShouldSetRefunding()
    {
        var afterSales = CreateApprovedRefundOnly();

        afterSales.MarkRefunding();

        afterSales.Status.Should().Be(AfterSalesStatus.Refunding);
    }

    [Fact]
    public void MarkRefunding_WhenConfirmReturn_ShouldSetRefunding()
    {
        var afterSales = CreateConfirmedReturnRefund();

        afterSales.MarkRefunding();

        afterSales.Status.Should().Be(AfterSalesStatus.Refunding);
    }

    #endregion

    #region MarkRefunding - Validation Guards

    [Fact]
    public void MarkRefunding_WhenPending_ShouldThrow()
    {
        var afterSales = CreatePendingReturnRefund();
        var act = () => afterSales.MarkRefunding();

        act.Should().Throw<ReviewDomainException>()
            .Where(e => e.ErrorCode == "AFTERSALES_REFUNDING_STATUS_INVALID");
    }

    [Fact]
    public void MarkRefunding_WhenApprovedReturnRefund_ShouldThrow()
    {
        var afterSales = CreateApprovedReturnRefund();
        var act = () => afterSales.MarkRefunding();

        act.Should().Throw<ReviewDomainException>()
            .Where(e => e.ErrorCode == "AFTERSALES_REFUNDING_STATUS_INVALID");
    }

    [Fact]
    public void MarkRefunding_WhenAlreadyRefunding_ShouldThrow()
    {
        var afterSales = CreateRefundingReturnRefund();
        var act = () => afterSales.MarkRefunding();

        act.Should().Throw<ReviewDomainException>()
            .Where(e => e.ErrorCode == "AFTERSALES_REFUNDING_STATUS_INVALID");
    }

    #endregion

    #region MarkRefundCompleted - Happy Path

    [Fact]
    public void MarkRefundCompleted_WhenRefunding_ShouldSetCompleted()
    {
        var afterSales = CreateRefundingReturnRefund();
        afterSales.ClearDomainEvents();

        afterSales.MarkRefundCompleted(Guid.NewGuid(), 80m, "CH123");

        afterSales.Status.Should().Be(AfterSalesStatus.Completed);
        afterSales.RefundedAmount.Should().Be(80m);
        afterSales.ChannelRefundNo.Should().Be("CH123");
        afterSales.RefundedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
    }

    [Fact]
    public void MarkRefundCompleted_WhenRefunding_ShouldRaiseRefundCompletedEvent()
    {
        var afterSales = CreateRefundingReturnRefund();
        afterSales.ClearDomainEvents();
        var refundId = Guid.NewGuid();

        afterSales.MarkRefundCompleted(refundId, 80m, "CH123");

        afterSales.DomainEvents.Should().HaveCount(1);
        var evt = afterSales.DomainEvents.Single().Should().BeOfType<RefundCompletedEvent>().Subject;
        evt.RefundId.Should().Be(refundId);
        evt.RefundAmount.Should().Be(80m);
    }

    #endregion

    #region MarkRefundCompleted - Validation Guards

    [Fact]
    public void MarkRefundCompleted_WhenNotRefunding_ShouldThrow()
    {
        var afterSales = CreateApprovedReturnRefund();
        var act = () => afterSales.MarkRefundCompleted(Guid.NewGuid(), 80m, null);

        act.Should().Throw<ReviewDomainException>()
            .Where(e => e.ErrorCode == "AFTERSALES_REFUND_COMPLETED_STATUS_INVALID");
    }

    [Fact]
    public void MarkRefundCompleted_AmountExceedsApproved_ShouldThrow()
    {
        var afterSales = CreateRefundingReturnRefund();
        var act = () => afterSales.MarkRefundCompleted(Guid.NewGuid(), 81m, null);

        act.Should().Throw<ReviewDomainException>()
            .Where(e => e.ErrorCode == "AFTERSALES_REFUND_AMOUNT_INVALID");
    }

    #endregion

    #region MarkRefundFailed

    [Fact]
    public void MarkRefundFailed_WhenRefunding_ShouldSetFailed()
    {
        var afterSales = CreateRefundingReturnRefund();

        afterSales.MarkRefundFailed("支付渠道异常");

        afterSales.Status.Should().Be(AfterSalesStatus.Failed);
        afterSales.FailReason.Should().Be("支付渠道异常");
    }

    [Fact]
    public void MarkRefundFailed_WhenNotRefunding_ShouldThrow()
    {
        var afterSales = CreateApprovedReturnRefund();
        var act = () => afterSales.MarkRefundFailed("fail");

        act.Should().Throw<ReviewDomainException>()
            .Where(e => e.ErrorCode == "AFTERSALES_REFUND_FAILED_STATUS_INVALID");
    }

    #endregion

    #region AddRefundRequestedEvent

    [Fact]
    public void AddRefundRequestedEvent_WhenRefunding_ShouldRaiseRefundRequestedEvent()
    {
        var afterSales = CreateRefundingReturnRefund();
        afterSales.ClearDomainEvents();
        var refundId = Guid.NewGuid();
        var paymentId = Guid.NewGuid();

        afterSales.AddRefundRequestedEvent(refundId, paymentId, 80m, "Alipay", "质量问题");

        afterSales.DomainEvents.Should().HaveCount(1);
        var evt = afterSales.DomainEvents.Single().Should().BeOfType<RefundRequestedIntegrationEvent>().Subject;
        evt.RefundId.Should().Be(refundId);
        evt.PaymentId.Should().Be(paymentId);
        evt.RefundAmount.Should().Be(80m);
        evt.AfterSalesId.Should().Be(ValidAfterSalesId);
        evt.RefundReason.Should().Be("质量问题");
        evt.Channel.Should().Be("Alipay");
    }

    [Fact]
    public void AddRefundRequestedEvent_WhenRefundingRefundOnly_ShouldRaiseEvent()
    {
        var afterSales = CreateRefundingRefundOnly();
        afterSales.ClearDomainEvents();
        var refundId = Guid.NewGuid();
        var paymentId = Guid.NewGuid();

        afterSales.AddRefundRequestedEvent(refundId, paymentId, 50m, "WeChat", "七天无理由");

        afterSales.DomainEvents.Should().HaveCount(1);
        var evt = afterSales.DomainEvents.Single().Should().BeOfType<RefundRequestedIntegrationEvent>().Subject;
        evt.RefundAmount.Should().Be(50m);
        evt.RefundReason.Should().Be("七天无理由");
    }

    [Fact]
    public void AddRefundRequestedEvent_WhenNotRefunding_ShouldThrow()
    {
        var afterSales = CreateApprovedReturnRefund();
        var act = () => afterSales.AddRefundRequestedEvent(Guid.NewGuid(), Guid.NewGuid(), 80m, "Alipay", "reason");

        act.Should().Throw<ReviewDomainException>()
            .Where(e => e.ErrorCode == "AFTERSALES_REFUND_REQUEST_STATUS_INVALID");
    }

    [Fact]
    public void AddRefundRequestedEvent_EmptyRefundId_ShouldThrow()
    {
        var afterSales = CreateRefundingReturnRefund();
        var act = () => afterSales.AddRefundRequestedEvent(Guid.Empty, Guid.NewGuid(), 80m, "Alipay", "reason");

        act.Should().Throw<ReviewDomainException>()
            .Where(e => e.ErrorCode == "AFTERSALES_REFUND_ID_EMPTY");
    }

    [Fact]
    public void AddRefundRequestedEvent_EmptyPaymentId_ShouldThrow()
    {
        var afterSales = CreateRefundingReturnRefund();
        var act = () => afterSales.AddRefundRequestedEvent(Guid.NewGuid(), Guid.Empty, 80m, "Alipay", "reason");

        act.Should().Throw<ReviewDomainException>()
            .Where(e => e.ErrorCode == "AFTERSALES_PAYMENT_ID_EMPTY");
    }

    [Fact]
    public void AddRefundRequestedEvent_ZeroAmount_ShouldThrow()
    {
        var afterSales = CreateRefundingReturnRefund();
        var act = () => afterSales.AddRefundRequestedEvent(Guid.NewGuid(), Guid.NewGuid(), 0m, "Alipay", "reason");

        act.Should().Throw<ReviewDomainException>()
            .Where(e => e.ErrorCode == "AFTERSALES_REFUND_AMOUNT_INVALID");
    }

    [Fact]
    public void AddRefundRequestedEvent_NegativeAmount_ShouldThrow()
    {
        var afterSales = CreateRefundingReturnRefund();
        var act = () => afterSales.AddRefundRequestedEvent(Guid.NewGuid(), Guid.NewGuid(), -1m, "Alipay", "reason");

        act.Should().Throw<ReviewDomainException>()
            .Where(e => e.ErrorCode == "AFTERSALES_REFUND_AMOUNT_INVALID");
    }

    [Fact]
    public void AddRefundRequestedEvent_EmptyReason_ShouldSucceed()
    {
        var afterSales = CreateRefundingReturnRefund();
        afterSales.ClearDomainEvents();

        afterSales.AddRefundRequestedEvent(Guid.NewGuid(), Guid.NewGuid(), 80m, "Alipay", "");

        afterSales.DomainEvents.Should().HaveCount(1);
        var evt = afterSales.DomainEvents.Single().Should().BeOfType<RefundRequestedIntegrationEvent>().Subject;
        evt.RefundReason.Should().Be("");
    }

    #endregion

    #region Cancel

    [Fact]
    public void Cancel_WhenPending_ShouldSetCancelled()
    {
        var afterSales = CreatePendingReturnRefund();

        afterSales.Cancel(ValidUserId, "不想要了");

        afterSales.Status.Should().Be(AfterSalesStatus.Cancelled);
        afterSales.CancelReason.Should().Be("不想要了");
        afterSales.CancelledAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
    }

    [Fact]
    public void Cancel_WhenApproved_ShouldSetCancelled()
    {
        var afterSales = CreateApprovedReturnRefund();

        afterSales.Cancel(ValidUserId, "改变主意");

        afterSales.Status.Should().Be(AfterSalesStatus.Cancelled);
    }

    [Fact]
    public void Cancel_WhenRejected_ShouldThrow()
    {
        var afterSales = CreateRejectedReturnRefund();
        var act = () => afterSales.Cancel(ValidUserId, "reason");

        act.Should().Throw<ReviewDomainException>()
            .Where(e => e.ErrorCode == "AFTERSALES_CANCEL_STATUS_INVALID");
    }

    [Fact]
    public void Cancel_WhenReturned_ShouldThrow()
    {
        var afterSales = CreateReturnedReturnRefund();
        var act = () => afterSales.Cancel(ValidUserId, "reason");

        act.Should().Throw<ReviewDomainException>()
            .Where(e => e.ErrorCode == "AFTERSALES_CANCEL_STATUS_INVALID");
    }

    #endregion

    #region Full State Machine - ReturnRefund

    [Fact]
    public void FullLifecycle_ReturnRefund_ShouldTransitionCorrectly()
    {
        var afterSales = CreatePendingReturnRefund();

        afterSales.Approve(ValidOperatorId, 80m);
        afterSales.Status.Should().Be(AfterSalesStatus.Approved);

        afterSales.ReturnGoods("SF1234567890");
        afterSales.Status.Should().Be(AfterSalesStatus.ReturnGoods);

        afterSales.ConfirmReturn();
        afterSales.Status.Should().Be(AfterSalesStatus.ConfirmReturn);

        afterSales.MarkRefunding();
        afterSales.Status.Should().Be(AfterSalesStatus.Refunding);

        afterSales.MarkRefundCompleted(Guid.NewGuid(), 80m, "CH123");
        afterSales.Status.Should().Be(AfterSalesStatus.Completed);
    }

    [Fact]
    public void FullLifecycle_ReturnRefund_ShouldPublishCorrectEvents()
    {
        var afterSales = CreatePendingReturnRefund();
        afterSales.ClearDomainEvents();

        afterSales.Approve(ValidOperatorId, 80m);
        afterSales.ReturnGoods("SF1234567890");
        afterSales.ConfirmReturn();
        afterSales.MarkRefunding();
        afterSales.MarkRefundCompleted(Guid.NewGuid(), 80m, "CH123");

        afterSales.DomainEvents.Should().HaveCount(4);
        afterSales.DomainEvents.OfType<AfterSalesApprovedEvent>().Should().HaveCount(1);
        afterSales.DomainEvents.OfType<AfterSalesReturnedEvent>().Should().HaveCount(1);
        afterSales.DomainEvents.OfType<AfterSalesReturnConfirmedEvent>().Should().HaveCount(1);
        afterSales.DomainEvents.OfType<RefundCompletedEvent>().Should().HaveCount(1);
    }

    #endregion

    #region Full State Machine - RefundOnly

    [Fact]
    public void FullLifecycle_RefundOnly_ShouldTransitionCorrectly()
    {
        var afterSales = CreatePendingRefundOnly();

        afterSales.Approve(ValidOperatorId, 50m);
        afterSales.Status.Should().Be(AfterSalesStatus.Approved);

        afterSales.MarkRefunding();
        afterSales.Status.Should().Be(AfterSalesStatus.Refunding);

        afterSales.MarkRefundCompleted(Guid.NewGuid(), 50m, null);
        afterSales.Status.Should().Be(AfterSalesStatus.Completed);
    }

    [Fact]
    public void FullLifecycle_RefundOnly_CannotReturnGoods()
    {
        var afterSales = CreatePendingRefundOnly();
        afterSales.Approve(ValidOperatorId, 50m);

        var act = () => afterSales.ReturnGoods("tracking");

        act.Should().Throw<ReviewDomainException>()
            .Where(e => e.ErrorCode == "AFTERSALES_RETURN_TYPE_INVALID");
    }

    #endregion

    #region Full State Machine - Reject Path

    [Fact]
    public void FullLifecycle_Reject_ShouldTransitionToRejected()
    {
        var afterSales = CreatePendingReturnRefund();

        afterSales.Reject(ValidOperatorId, "不符合条件");

        afterSales.Status.Should().Be(AfterSalesStatus.Rejected);

        var act = () => afterSales.Approve(ValidOperatorId, 80m);
        act.Should().Throw<ReviewDomainException>()
            .Where(e => e.ErrorCode == "AFTERSALES_APPROVE_STATUS_INVALID");
    }

    #endregion

    #region Full State Machine - Cancel Path

    [Fact]
    public void FullLifecycle_Cancel_ShouldTransitionToCancelled()
    {
        var afterSales = CreatePendingReturnRefund();

        afterSales.Cancel(ValidUserId, "不想要了");

        afterSales.Status.Should().Be(AfterSalesStatus.Cancelled);

        var act = () => afterSales.Approve(ValidOperatorId, 80m);
        act.Should().Throw<ReviewDomainException>();
    }

    #endregion

    #region No Skip / No Rollback

    [Fact]
    public void CannotSkipFromPendingToReturnGoods()
    {
        var afterSales = CreatePendingReturnRefund();
        var act = () => afterSales.ReturnGoods("tracking");

        act.Should().Throw<ReviewDomainException>()
            .Where(e => e.ErrorCode == "AFTERSALES_RETURN_STATUS_INVALID");
    }

    [Fact]
    public void CannotSkipFromPendingToConfirmReturn()
    {
        var afterSales = CreatePendingReturnRefund();
        var act = () => afterSales.ConfirmReturn();

        act.Should().Throw<ReviewDomainException>()
            .Where(e => e.ErrorCode == "AFTERSALES_CONFIRM_RETURN_STATUS_INVALID");
    }

    [Fact]
    public void CannotRollbackFromRejectedToPending()
    {
        var afterSales = CreateRejectedReturnRefund();
        var act = () => afterSales.Approve(ValidOperatorId, 80m);

        act.Should().Throw<ReviewDomainException>()
            .Where(e => e.ErrorCode == "AFTERSALES_APPROVE_STATUS_INVALID");
    }

    [Fact]
    public void CannotRollbackFromCompletedToRefunding()
    {
        var afterSales = CreateCompletedReturnRefund();
        var act = () => afterSales.MarkRefunding();

        act.Should().Throw<ReviewDomainException>()
            .Where(e => e.ErrorCode == "AFTERSALES_REFUNDING_STATUS_INVALID");
    }

    #endregion

    #region ClearDomainEvents

    [Fact]
    public void ClearDomainEvents_ShouldClearAllEvents()
    {
        var afterSales = CreatePendingReturnRefund();

        afterSales.ClearDomainEvents();

        afterSales.DomainEvents.Should().BeEmpty();
    }

    #endregion

    #region Helpers

    private static AfterSales CreatePendingReturnRefund()
    {
        return AfterSales.Create(
            ValidAfterSalesId, ValidOrderId, Guid.NewGuid(),
            ValidUserId, ValidSellerId,
            AfterSalesType.ReturnRefund, "质量问题", "商品有瑕疵",
            new List<string> { "img1.jpg" }, 100m, "CNY");
    }

    private static AfterSales CreatePendingRefundOnly()
    {
        return AfterSales.Create(
            ValidAfterSalesId, ValidOrderId, null,
            ValidUserId, ValidSellerId,
            AfterSalesType.RefundOnly, "不想要了", "七天无理由",
            new List<string>(), 50m, "CNY");
    }

    private static AfterSales CreateApprovedReturnRefund()
    {
        var afterSales = CreatePendingReturnRefund();
        afterSales.Approve(ValidOperatorId, 80m);
        return afterSales;
    }

    private static AfterSales CreateApprovedRefundOnly()
    {
        var afterSales = CreatePendingRefundOnly();
        afterSales.Approve(ValidOperatorId, 50m);
        return afterSales;
    }

    private static AfterSales CreateRejectedReturnRefund()
    {
        var afterSales = CreatePendingReturnRefund();
        afterSales.Reject(ValidOperatorId, "不符合条件");
        return afterSales;
    }

    private static AfterSales CreateReturnedReturnRefund()
    {
        var afterSales = CreateApprovedReturnRefund();
        afterSales.ReturnGoods("SF1234567890");
        return afterSales;
    }

    private static AfterSales CreateConfirmedReturnRefund()
    {
        var afterSales = CreateReturnedReturnRefund();
        afterSales.ConfirmReturn();
        return afterSales;
    }

    private static AfterSales CreateRefundingReturnRefund()
    {
        var afterSales = CreateConfirmedReturnRefund();
        afterSales.MarkRefunding();
        return afterSales;
    }

    private static AfterSales CreateRefundingRefundOnly()
    {
        var afterSales = CreateApprovedRefundOnly();
        afterSales.MarkRefunding();
        return afterSales;
    }

    private static AfterSales CreateCompletedReturnRefund()
    {
        var afterSales = CreateRefundingReturnRefund();
        afterSales.MarkRefundCompleted(Guid.NewGuid(), 80m, "CH123");
        return afterSales;
    }

    #endregion
}