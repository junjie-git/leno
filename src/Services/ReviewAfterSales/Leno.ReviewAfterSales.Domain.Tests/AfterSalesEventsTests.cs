using Leno.ReviewAfterSales.Domain.Aggregates;
using Leno.ReviewAfterSales.Domain.Events;
using Leno.ReviewAfterSales.Domain.Exceptions;
using Leno.ReviewAfterSales.Domain.ValueObjects;

namespace Leno.ReviewAfterSales.Domain.Tests;

/// <summary>
/// AfterSales 聚合 Cancel / MarkRefundFailed 领域事件发布单元测试（审计 2.3）。
/// 验证 Cancel / MarkRefundFailed 收集对应领域事件，并合并 4.2/4.5 reason 非空与长度校验。
/// </summary>
public sealed class AfterSalesEventsTests
{
    private static readonly Guid ValidAfterSalesId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid ValidOrderId = Guid.Parse("22222222-2222-2222-2222-222222222222");
    private static readonly Guid ValidUserId = Guid.Parse("33333333-3333-3333-3333-333333333333");
    private static readonly Guid ValidSellerId = Guid.Parse("44444444-4444-4444-4444-444444444444");
    private static readonly Guid ValidOperatorId = Guid.Parse("55555555-5555-5555-5555-555555555555");

    private static AfterSales CreateRefunding()
    {
        var afterSales = AfterSales.Create(
            ValidAfterSalesId, ValidOrderId, null, ValidUserId, ValidSellerId,
            AfterSalesType.RefundOnly, "quality", "broken", null, 10m, "CNY");
        afterSales.Approve(ValidOperatorId, 10m);
        afterSales.MarkRefunding();
        return afterSales;
    }

    [Fact]
    public void MarkRefundFailed_Should_Raise_AfterSalesRefundFailedDomainEvent()
    {
        var afterSales = CreateRefunding();
        afterSales.ClearDomainEvents();

        afterSales.MarkRefundFailed("channel timeout");

        afterSales.DomainEvents.OfType<AfterSalesRefundFailedDomainEvent>().Should().HaveCount(1);
        var evt = afterSales.DomainEvents.OfType<AfterSalesRefundFailedDomainEvent>().Single();
        evt.AfterSalesId.Should().Be(ValidAfterSalesId);
        evt.OrderId.Should().Be(ValidOrderId);
        evt.UserId.Should().Be(ValidUserId);
        evt.Reason.Should().Be("channel timeout");
        afterSales.Status.Should().Be(AfterSalesStatus.Failed);
        afterSales.FailReason.Should().Be("channel timeout");
    }

    [Fact]
    public void MarkRefundFailed_EmptyReason_ShouldThrow()
    {
        var afterSales = CreateRefunding();

        var act = () => afterSales.MarkRefundFailed("");

        act.Should().Throw<ReviewDomainException>()
            .Where(e => e.ErrorCode == "AFTERSALES_FAIL_REASON_EMPTY");
    }

    [Fact]
    public void MarkRefundFailed_ReasonTooLong_ShouldThrow()
    {
        var afterSales = CreateRefunding();
        var reason = new string('X', 513);

        var act = () => afterSales.MarkRefundFailed(reason);

        act.Should().Throw<ReviewDomainException>()
            .Where(e => e.ErrorCode == "AFTERSALES_FAIL_REASON_TOO_LONG");
    }

    [Fact]
    public void Cancel_Should_Raise_AfterSalesCancelledDomainEvent()
    {
        var afterSales = AfterSales.Create(
            ValidAfterSalesId, ValidOrderId, null, ValidUserId, ValidSellerId,
            AfterSalesType.RefundOnly, "quality", "broken", null, 10m, "CNY");
        afterSales.ClearDomainEvents();

        afterSales.Cancel(ValidUserId, "changed mind");

        afterSales.DomainEvents.OfType<AfterSalesCancelledDomainEvent>().Should().HaveCount(1);
        var evt = afterSales.DomainEvents.OfType<AfterSalesCancelledDomainEvent>().Single();
        evt.AfterSalesId.Should().Be(ValidAfterSalesId);
        evt.OrderId.Should().Be(ValidOrderId);
        evt.UserId.Should().Be(ValidUserId);
        evt.SellerId.Should().Be(ValidSellerId);
        evt.Reason.Should().Be("changed mind");
        afterSales.Status.Should().Be(AfterSalesStatus.Cancelled);
        afterSales.CancelReason.Should().Be("changed mind");
    }

    [Fact]
    public void Cancel_NotOwner_ShouldThrow()
    {
        var afterSales = AfterSales.Create(
            ValidAfterSalesId, ValidOrderId, null, ValidUserId, ValidSellerId,
            AfterSalesType.RefundOnly, "quality", "broken", null, 10m, "CNY");

        var attacker = Guid.NewGuid();
        var act = () => afterSales.Cancel(attacker, "changed mind");

        act.Should().Throw<ReviewDomainException>()
            .Where(e => e.ErrorCode == "AFTERSALES_CANCEL_NOT_OWNER");
    }

    [Fact]
    public void Cancel_EmptyReason_ShouldThrow()
    {
        var afterSales = AfterSales.Create(
            ValidAfterSalesId, ValidOrderId, null, ValidUserId, ValidSellerId,
            AfterSalesType.RefundOnly, "quality", "broken", null, 10m, "CNY");

        var act = () => afterSales.Cancel(ValidUserId, "");

        act.Should().Throw<ReviewDomainException>()
            .Where(e => e.ErrorCode == "AFTERSALES_CANCEL_REASON_EMPTY");
    }

    [Fact]
    public void Cancel_ReasonTooLong_ShouldThrow()
    {
        var afterSales = AfterSales.Create(
            ValidAfterSalesId, ValidOrderId, null, ValidUserId, ValidSellerId,
            AfterSalesType.RefundOnly, "quality", "broken", null, 10m, "CNY");
        var reason = new string('Y', 201);

        var act = () => afterSales.Cancel(ValidUserId, reason);

        act.Should().Throw<ReviewDomainException>()
            .Where(e => e.ErrorCode == "AFTERSALES_CANCEL_REASON_TOO_LONG");
    }

    [Fact]
    public void Cancel_EmptyUserId_ShouldThrow()
    {
        var afterSales = AfterSales.Create(
            ValidAfterSalesId, ValidOrderId, null, ValidUserId, ValidSellerId,
            AfterSalesType.RefundOnly, "quality", "broken", null, 10m, "CNY");

        var act = () => afterSales.Cancel(Guid.Empty, "reason");

        act.Should().Throw<ReviewDomainException>()
            .Where(e => e.ErrorCode == "AFTERSALES_USER_EMPTY");
    }
}
