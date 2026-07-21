using Leno.Order.Domain.Aggregates;
using Leno.Order.Domain.Events;
using Leno.Order.Domain.Exceptions;
using Leno.Order.Domain.ValueObjects;
using OrderAggregate = Leno.Order.Domain.Aggregates.Order;

namespace Leno.Order.Domain.Tests;

public class OrderTests
{
    private static readonly Guid UserId = Guid.NewGuid();
    private static readonly Guid SellerId = Guid.NewGuid();
    private static readonly Guid SkuId = Guid.NewGuid();
    private static readonly Guid SpuId = Guid.NewGuid();

    [Fact]
    public void Create_ValidInput_ShouldCreatePendingPaymentOrder()
    {
        var order = CreateOrder();

        order.Status.Should().Be(OrderStatus.PendingPayment);
        order.OrderNo.Should().NotBeEmpty();
        order.UserId.Should().Be(UserId);
        order.SellerId.Should().Be(SellerId);
        order.Items.Should().HaveCount(1);
        order.TotalAmount.Should().Be(99.99m + 10m); // itemsAmount + freight
        order.DomainEvents.Should().NotBeEmpty();
    }

    [Fact]
    public void Create_EmptyOrderId_ShouldThrowException()
    {
        var act = () => OrderAggregate.Create(
            Guid.Empty, "ORD-001", OrderType.Normal, UserId, SellerId,
            CreateOrderItems(), CreateAddress(), 10m, 0m, DateTime.UtcNow.AddHours(1));

        act.Should().Throw<OrderDomainException>().WithMessage("*OrderId*");
    }

    [Fact]
    public void Create_EmptyOrderNo_ShouldThrowException()
    {
        var act = () => OrderAggregate.Create(
            Guid.NewGuid(), "", OrderType.Normal, UserId, SellerId,
            CreateOrderItems(), CreateAddress(), 10m, 0m, DateTime.UtcNow.AddHours(1));

        act.Should().Throw<OrderDomainException>().WithMessage("*订单编号*");
    }

    [Fact]
    public void Create_EmptyUserId_ShouldThrowException()
    {
        var act = () => OrderAggregate.Create(
            Guid.NewGuid(), "ORD-001", OrderType.Normal, Guid.Empty, SellerId,
            CreateOrderItems(), CreateAddress(), 10m, 0m, DateTime.UtcNow.AddHours(1));

        act.Should().Throw<OrderDomainException>().WithMessage("*UserId*");
    }

    [Fact]
    public void Create_EmptySellerId_ShouldThrowException()
    {
        var act = () => OrderAggregate.Create(
            Guid.NewGuid(), "ORD-001", OrderType.Normal, UserId, Guid.Empty,
            CreateOrderItems(), CreateAddress(), 10m, 0m, DateTime.UtcNow.AddHours(1));

        act.Should().Throw<OrderDomainException>().WithMessage("*SellerId*");
    }

    [Fact]
    public void Create_EmptyItems_ShouldThrowException()
    {
        var act = () => OrderAggregate.Create(
            Guid.NewGuid(), "ORD-001", OrderType.Normal, UserId, SellerId,
            new List<OrderItem>(), CreateAddress(), 10m, 0m, DateTime.UtcNow.AddHours(1));

        act.Should().Throw<OrderDomainException>().WithMessage("*明细*");
    }

    [Fact]
    public void Create_NullAddress_ShouldThrowException()
    {
        var act = () => OrderAggregate.Create(
            Guid.NewGuid(), "ORD-001", OrderType.Normal, UserId, SellerId,
            CreateOrderItems(), null!, 10m, 0m, DateTime.UtcNow.AddHours(1));

        act.Should().Throw<OrderDomainException>().WithMessage("*地址*");
    }

    [Fact]
    public void Create_PastExpireAt_ShouldThrowException()
    {
        var act = () => OrderAggregate.Create(
            Guid.NewGuid(), "ORD-001", OrderType.Normal, UserId, SellerId,
            CreateOrderItems(), CreateAddress(), 10m, 0m, DateTime.UtcNow.AddHours(-1));

        act.Should().Throw<OrderDomainException>().WithMessage("*截止时间*");
    }

    [Fact]
    public void Create_NegativeFreight_ShouldThrowException()
    {
        var act = () => OrderAggregate.Create(
            Guid.NewGuid(), "ORD-001", OrderType.Normal, UserId, SellerId,
            CreateOrderItems(), CreateAddress(), -1m, 0m, DateTime.UtcNow.AddHours(1));

        act.Should().Throw<OrderDomainException>().WithMessage("*运费*");
    }

    [Fact]
    public void Create_NegativePointsOffset_ShouldThrowException()
    {
        var act = () => OrderAggregate.Create(
            Guid.NewGuid(), "ORD-001", OrderType.Normal, UserId, SellerId,
            CreateOrderItems(), CreateAddress(), 10m, -1m, DateTime.UtcNow.AddHours(1));

        act.Should().Throw<OrderDomainException>().WithMessage("*积分*");
    }

    [Fact]
    public void Create_PointsOffsetExceedsItemsAmount_ShouldThrowException()
    {
        var act = () => OrderAggregate.Create(
            Guid.NewGuid(), "ORD-001", OrderType.Normal, UserId, SellerId,
            CreateOrderItems(), CreateAddress(), 10m, 200m, DateTime.UtcNow.AddHours(1));

        act.Should().Throw<OrderDomainException>().WithMessage("*积分*");
    }

    [Fact]
    public void Create_PointsOffsetExceedsMaxLimit_ShouldThrowException()
    {
        var act = () => OrderAggregate.Create(
            Guid.NewGuid(), "ORD-001", OrderType.Normal, UserId, SellerId,
            CreateOrderItems(), CreateAddress(), 10m, 60m, DateTime.UtcNow.AddHours(1));

        act.Should().Throw<OrderDomainException>().WithMessage("*上限*");
    }

    [Fact]
    public void Create_ShouldCalculateTotalAmountCorrectly()
    {
        var order = OrderAggregate.Create(
            Guid.NewGuid(), "ORD-001", OrderType.Normal, UserId, SellerId,
            CreateOrderItems(), CreateAddress(), 5m, 3m, DateTime.UtcNow.AddHours(1));

        order.TotalAmount.Should().Be(99.99m - 3m + 5m); // 101.99
    }

    #region ApplyDiscount

    [Fact]
    public void ApplyDiscount_Valid_ShouldUpdateDiscountAmount()
    {
        var order = CreateOrder();
        var allocations = new List<(Guid, decimal)> { (SkuId, 10m) };

        order.ApplyDiscount(10m, allocations);

        order.DiscountAmount.Should().Be(10m);
        order.TotalAmount.Should().Be(99.99m - 10m + 10m); // 99.99
    }

    [Fact]
    public void ApplyDiscount_MultipleSkus_ShouldDistributeCorrectly()
    {
        var skuId2 = Guid.NewGuid();
        var snapshot1 = ProductSnapshot.Create(SkuId, SpuId, "商品A", "红色", null, SellerId);
        var snapshot2 = ProductSnapshot.Create(skuId2, SpuId, "商品B", "蓝色", null, SellerId);
        var item1 = OrderItem.Create(Guid.NewGuid(), SkuId, snapshot1, 100m, 1, null);
        var item2 = OrderItem.Create(Guid.NewGuid(), skuId2, snapshot2, 50m, 2, null);
        var order = OrderAggregate.Create(
            Guid.NewGuid(), "ORD-MULTI", OrderType.Normal, UserId, SellerId,
            new List<OrderItem> { item1, item2 }, CreateAddress(), 10m, 0m, DateTime.UtcNow.AddHours(1));

        var allocations = new List<(Guid, decimal)>
        {
            (SkuId, 20m),
            (skuId2, 10m)
        };

        order.ApplyDiscount(30m, allocations);

        order.DiscountAmount.Should().Be(30m);
        item1.DiscountAllocation.Should().Be(20m);
        item2.DiscountAllocation.Should().Be(10m);
        order.TotalAmount.Should().Be(200m - 30m + 10m); // 180
    }

    [Fact]
    public void ApplyDiscount_SumMismatch_ShouldThrowException()
    {
        var order = CreateOrder();
        var allocations = new List<(Guid, decimal)> { (SkuId, 10m) };

        var act = () => order.ApplyDiscount(20m, allocations);

        act.Should().Throw<OrderDomainException>().WithMessage("*不匹配*");
    }

    [Fact]
    public void ApplyDiscount_NotPendingPayment_ShouldThrowException()
    {
        var order = CreateOrder();
        order.MarkAsPaid(Guid.NewGuid(), "WeChatPay", DateTime.UtcNow, "T001");

        var act = () => order.ApplyDiscount(5m, new List<(Guid, decimal)> { (SkuId, 5m) });

        act.Should().Throw<OrderDomainException>().WithMessage("*状态*");
    }

    [Fact]
    public void ApplyDiscount_EmptyAllocations_ShouldThrowException()
    {
        var order = CreateOrder();

        var act = () => order.ApplyDiscount(0m, new List<(Guid, decimal)>());

        act.Should().Throw<OrderDomainException>().WithMessage("*优惠*");
    }

    [Fact]
    public void ApplyDiscount_SkuNotFound_ShouldThrowException()
    {
        var order = CreateOrder();

        var act = () => order.ApplyDiscount(5m, new List<(Guid, decimal)> { (Guid.NewGuid(), 5m) });

        act.Should().Throw<OrderDomainException>().WithMessage("*不存在*");
    }

    [Fact]
    public void ApplyDiscount_AllocationExceedsSubtotal_ShouldThrowException()
    {
        var order = CreateOrder();

        var act = () => order.ApplyDiscount(200m, new List<(Guid, decimal)> { (SkuId, 200m) });

        act.Should().Throw<OrderDomainException>().WithMessage("*非法*");
    }

    #endregion

    #region ApplyPointsOffset

    [Fact]
    public void ApplyPointsOffset_Valid_ShouldUpdatePointsOffset()
    {
        var order = CreateOrder();

        order.ApplyPointsOffset(5m);

        order.PointsOffsetAmount.Should().Be(5m);
        order.TotalAmount.Should().Be(99.99m - 5m + 10m); // 104.99
    }

    [Fact]
    public void ApplyPointsOffset_ExceedsMaxLimit_ShouldThrowException()
    {
        var order = CreateOrder();

        var act = () => order.ApplyPointsOffset(60m);

        act.Should().Throw<OrderDomainException>().WithMessage("*上限*");
    }

    [Fact]
    public void ApplyPointsOffset_ExceedsItemsAmountMinusDiscount_ShouldThrowException()
    {
        var order = CreateOrder();

        var act = () => order.ApplyPointsOffset(100m);

        act.Should().Throw<OrderDomainException>().WithMessage("*积分*");
    }

    [Fact]
    public void ApplyPointsOffset_Negative_ShouldThrowException()
    {
        var order = CreateOrder();

        var act = () => order.ApplyPointsOffset(-1m);

        act.Should().Throw<OrderDomainException>().WithMessage("*积分*");
    }

    [Fact]
    public void ApplyPointsOffset_NotPendingPayment_ShouldThrowException()
    {
        var order = CreateOrder();
        order.MarkAsPaid(Guid.NewGuid(), "WeChatPay", DateTime.UtcNow, "T001");

        var act = () => order.ApplyPointsOffset(5m);

        act.Should().Throw<OrderDomainException>().WithMessage("*状态*");
    }

    #endregion

    #region State Machine

    [Fact]
    public void FullStateMachine_ShouldFlowCorrectly()
    {
        var order = CreateOrder();
        order.Status.Should().Be(OrderStatus.PendingPayment);

        order.MarkAsPaid(Guid.NewGuid(), "WeChatPay", DateTime.UtcNow, "T001");
        order.Status.Should().Be(OrderStatus.Paid);

        order.Ship("SF123456", "SF", DateTime.UtcNow, Guid.NewGuid());
        order.Status.Should().Be(OrderStatus.Shipped);

        order.ConfirmReceipt();
        order.Status.Should().Be(OrderStatus.Completed);
        order.AfterSalesWindowEndsAt.Should().NotBeNull();

        // 将售后窗口结束时间设置为过去以通过时间校验
        typeof(OrderAggregate).GetProperty(nameof(OrderAggregate.AfterSalesWindowEndsAt))!
            .SetValue(order, DateTime.UtcNow.AddDays(-1));

        order.CloseAfterSalesWindow();
        order.Status.Should().Be(OrderStatus.Closed);
    }

    [Fact]
    public void MarkAsPaid_NotPendingPayment_ShouldThrowException()
    {
        var order = CreateOrder();
        order.MarkAsPaid(Guid.NewGuid(), "WeChatPay", DateTime.UtcNow, "T001");

        var act = () => order.MarkAsPaid(Guid.NewGuid(), "WeChatPay", DateTime.UtcNow, "T002");

        act.Should().Throw<OrderDomainException>().WithMessage("*状态*");
    }

    #region MarkPaymentInitiated

    [Fact]
    public void MarkPaymentInitiated_Valid_ShouldSetFlagAndPublishEvent()
    {
        var order = CreateOrder();

        order.MarkPaymentInitiated(PaymentMethod.Alipay);

        order.PaymentInitiated.Should().BeTrue();
        order.PaymentInitiatedAt.Should().NotBeNull();
        order.PaymentMethod.Should().Be(PaymentMethod.Alipay);
        // 订单状态保持待支付（不引入中间状态，仅置标记）
        order.Status.Should().Be(OrderStatus.PendingPayment);
        // 领域事件含 PaymentRequestedDomainEvent，供 Outbox 同事务发布
        order.DomainEvents.Should().Contain(e => e is PaymentRequestedDomainEvent);
        var evt = order.DomainEvents.OfType<PaymentRequestedDomainEvent>().Single();
        evt.OrderId.Should().Be(order.Id);
        evt.UserId.Should().Be(order.UserId);
        evt.Amount.Should().Be(order.TotalAmount);
        evt.Channel.Should().Be("Alipay");
    }

    [Fact]
    public void MarkPaymentInitiated_AlreadyInitiated_ShouldThrowException()
    {
        var order = CreateOrder();
        order.MarkPaymentInitiated(PaymentMethod.WeChatPay);

        var act = () => order.MarkPaymentInitiated(PaymentMethod.Alipay);

        act.Should().Throw<OrderDomainException>().WithMessage("*已发起*");
        // 重复发起不应再次产生 PaymentRequestedDomainEvent
        order.DomainEvents.OfType<PaymentRequestedDomainEvent>().Should().HaveCount(1);
    }

    [Fact]
    public void MarkPaymentInitiated_NotPendingPayment_ShouldThrowException()
    {
        var order = CreateOrder();
        order.MarkAsPaid(Guid.NewGuid(), "WeChatPay", DateTime.UtcNow, "T001");

        var act = () => order.MarkPaymentInitiated(PaymentMethod.WeChatPay);

        act.Should().Throw<OrderDomainException>().WithMessage("*状态*");
    }

    [Fact]
    public void MarkPaymentInitiated_AfterCancel_ShouldThrowException()
    {
        var order = CreateOrder();
        order.Cancel("timeout", "System");

        var act = () => order.MarkPaymentInitiated(PaymentMethod.WeChatPay);

        act.Should().Throw<OrderDomainException>().WithMessage("*状态*");
    }

    #endregion

    [Fact]
    public void Ship_NotPaid_ShouldThrowException()
    {
        var order = CreateOrder();

        var act = () => order.Ship("SF123", "SF", DateTime.UtcNow, Guid.NewGuid());

        act.Should().Throw<OrderDomainException>().WithMessage("*状态*");
    }

    [Fact]
    public void Ship_EmptyLogisticsNo_ShouldThrowException()
    {
        var order = CreateOrder();
        order.MarkAsPaid(Guid.NewGuid(), "WeChatPay", DateTime.UtcNow, "T001");

        var act = () => order.Ship("", "SF", DateTime.UtcNow, Guid.NewGuid());

        act.Should().Throw<OrderDomainException>().WithMessage("*物流*");
    }

    [Fact]
    public void Ship_EmptyLogisticsCompanyCode_ShouldThrowException()
    {
        var order = CreateOrder();
        order.MarkAsPaid(Guid.NewGuid(), "WeChatPay", DateTime.UtcNow, "T001");

        var act = () => order.Ship("SF123", "", DateTime.UtcNow, Guid.NewGuid());

        act.Should().Throw<OrderDomainException>().WithMessage("*物流公司编码*");
    }

    [Fact]
    public void Ship_ShouldSetLogisticsCompanyCode()
    {
        var order = CreateOrder();
        order.MarkAsPaid(Guid.NewGuid(), "WeChatPay", DateTime.UtcNow, "T001");

        order.Ship("SF123", "SF", DateTime.UtcNow, Guid.NewGuid());

        order.LogisticsCompanyCode.Should().Be("SF");
        order.LogisticsNo.Should().Be("SF123");
    }

    [Fact]
    public void ConfirmReceipt_NotShipped_ShouldThrowException()
    {
        var order = CreateOrder();

        var act = () => order.ConfirmReceipt();

        act.Should().Throw<OrderDomainException>().WithMessage("*状态*");
    }

    [Fact]
    public void CompleteMembershipOrder_Valid_ShouldTransitionToCompleted()
    {
        var order = OrderAggregate.Create(
            Guid.NewGuid(), "ORD-MEM", OrderType.Membership, UserId, SellerId,
            CreateOrderItems(), CreateAddress(), 0m, 0m, DateTime.UtcNow.AddHours(1));
        order.MarkAsPaid(Guid.NewGuid(), "WeChatPay", DateTime.UtcNow, "T001");

        order.CompleteMembershipOrder();

        order.Status.Should().Be(OrderStatus.Completed);
    }

    [Fact]
    public void CompleteMembershipOrder_NotMembershipType_ShouldThrowException()
    {
        var order = CreateOrder();
        order.MarkAsPaid(Guid.NewGuid(), "WeChatPay", DateTime.UtcNow, "T001");

        var act = () => order.CompleteMembershipOrder();

        act.Should().Throw<OrderDomainException>().WithMessage("*会员*");
    }

    [Fact]
    public void CloseAfterSalesWindow_NotCompleted_ShouldThrowException()
    {
        var order = CreateOrder();

        var act = () => order.CloseAfterSalesWindow();

        act.Should().Throw<OrderDomainException>().WithMessage("*状态*");
    }

    [Fact]
    public void Cancel_Valid_ShouldTransitionToCancelled()
    {
        var order = CreateOrder();

        order.Cancel("Changed mind", "Buyer");

        order.Status.Should().Be(OrderStatus.Cancelled);
        order.CancelReason.Should().Be("Changed mind");
        order.CancelledAt.Should().NotBeNull();
    }

    [Fact]
    public void Cancel_NotPendingPayment_ShouldThrowException()
    {
        var order = CreateOrder();
        order.MarkAsPaid(Guid.NewGuid(), "WeChatPay", DateTime.UtcNow, "T001");

        var act = () => order.Cancel("test", "Buyer");

        act.Should().Throw<OrderDomainException>().WithMessage("*状态*");
    }

    [Fact]
    public void ForceCancel_FromPaid_ShouldTransitionToCancelled()
    {
        var order = CreateOrder();
        order.MarkAsPaid(Guid.NewGuid(), "WeChatPay", DateTime.UtcNow, "T001");

        order.ForceCancel("Fraudulent", "Operator");

        order.Status.Should().Be(OrderStatus.Cancelled);
    }

    [Fact]
    public void ForceCancel_FromPaid_ShouldSetCancelReason()
    {
        var order = CreateOrder();
        order.MarkAsPaid(Guid.NewGuid(), "WeChatPay", DateTime.UtcNow, "T001");

        order.ForceCancel("Fraudulent order", "Admin-001");

        order.CancelReason.Should().Be("Fraudulent order");
        order.CancelledAt.Should().NotBeNull();
    }

    [Fact]
    public void ForceCancel_FromPaid_ShouldPublishCancelledEvent()
    {
        var order = CreateOrder();
        order.MarkAsPaid(Guid.NewGuid(), "WeChatPay", DateTime.UtcNow, "T001");

        order.ForceCancel("Fraudulent", "Admin-001");

        order.DomainEvents.Should().Contain(e => e is OrderCancelledDomainEvent);
        var evt = order.DomainEvents.OfType<OrderCancelledDomainEvent>().Last();
        evt.CancelledBy.Should().Be("Admin-001");
        evt.CancelReason.Should().Be("Fraudulent");
    }

    [Fact]
    public void ForceCancel_FromShipped_ShouldTransitionToCancelled()
    {
        var order = CreateOrder();
        order.MarkAsPaid(Guid.NewGuid(), "WeChatPay", DateTime.UtcNow, "T001");
        order.Ship("SF123", "SF", DateTime.UtcNow, Guid.NewGuid());

        order.ForceCancel("Fraudulent", "Operator");

        order.Status.Should().Be(OrderStatus.Cancelled);
    }

    [Fact]
    public void ForceCancel_FromCompleted_ShouldThrowException()
    {
        var order = CreateOrder();
        order.MarkAsPaid(Guid.NewGuid(), "WeChatPay", DateTime.UtcNow, "T001");
        order.Ship("SF123", "SF", DateTime.UtcNow, Guid.NewGuid());
        order.ConfirmReceipt();

        var act = () => order.ForceCancel("test", "Operator");

        act.Should().Throw<OrderDomainException>().WithMessage("*状态*");
    }

    [Fact]
    public void ForceCancel_FromClosed_ShouldThrowException()
    {
        var order = CreateOrder();
        order.MarkAsPaid(Guid.NewGuid(), "WeChatPay", DateTime.UtcNow, "T001");
        order.Ship("SF123", "SF", DateTime.UtcNow, Guid.NewGuid());
        order.ConfirmReceipt();
        typeof(OrderAggregate).GetProperty(nameof(OrderAggregate.AfterSalesWindowEndsAt))!
            .SetValue(order, DateTime.UtcNow.AddDays(-1));
        order.CloseAfterSalesWindow();

        var act = () => order.ForceCancel("test", "Operator");

        act.Should().Throw<OrderDomainException>().WithMessage("*状态*");
    }

    [Fact]
    public void ForceCancel_NotValidStatus_ShouldThrowException()
    {
        var order = CreateOrder();

        var act = () => order.ForceCancel("test", "Operator");

        act.Should().Throw<OrderDomainException>().WithMessage("*状态*");
    }

    #endregion


    #region CloseAfterSalesWindow

    [Fact]
    public void CloseAfterSalesWindow_AfterSalesWindowNotEnded_ShouldThrowException()
    {
        var order = CreateOrder();
        order.MarkAsPaid(Guid.NewGuid(), "WeChatPay", DateTime.UtcNow, "T001");
        order.Ship("SF123", "SF", DateTime.UtcNow, Guid.NewGuid());
        order.ConfirmReceipt();
        // AfterSalesWindowEndsAt is 7 days from now, so CloseAfterSalesWindow should throw

        var act = () => order.CloseAfterSalesWindow();

        act.Should().Throw<OrderDomainException>().WithMessage("*售后窗口尚未结束*");
    }

    [Fact]
    public void CloseAfterSalesWindow_AfterSalesWindowEnded_ShouldTransitionToClosed()
    {
        var order = CreateOrder();
        order.MarkAsPaid(Guid.NewGuid(), "WeChatPay", DateTime.UtcNow, "T001");
        order.Ship("SF123", "SF", DateTime.UtcNow, Guid.NewGuid());
        order.ConfirmReceipt();
        // Set AfterSalesWindowEndsAt to past
        typeof(OrderAggregate).GetProperty(nameof(OrderAggregate.AfterSalesWindowEndsAt))!
            .SetValue(order, DateTime.UtcNow.AddDays(-1));

        order.CloseAfterSalesWindow();

        order.Status.Should().Be(OrderStatus.Closed);
        order.DomainEvents.Should().Contain(e => e is OrderAfterSalesWindowClosedDomainEvent);
    }

    [Fact]
    public void CloseAfterSalesWindow_AfterSalesWindowEndsAtNull_ShouldThrowException()
    {
        var order = CreateOrder();
        // Use reflection to set status to Completed with null AfterSalesWindowEndsAt
        typeof(OrderAggregate).GetProperty(nameof(OrderAggregate.Status))!
            .SetValue(order, OrderStatus.Completed);

        var act = () => order.CloseAfterSalesWindow();

        act.Should().Throw<OrderDomainException>().WithMessage("*售后窗口尚未结束*");
    }

    #endregion

    #region CompleteMembershipOrder

    [Fact]
    public void CompleteMembershipOrder_NotPaid_ShouldThrowException()
    {
        var order = OrderAggregate.Create(
            Guid.NewGuid(), "ORD-MEM", OrderType.Membership, UserId, Guid.Empty,
            CreateOrderItems(), CreateAddress(), 0m, 0m, DateTime.UtcNow.AddHours(1));

        var act = () => order.CompleteMembershipOrder();

        act.Should().Throw<OrderDomainException>().WithMessage("*状态*");
    }

    [Fact]
    public void CompleteMembershipOrder_ShouldSetAfterSalesWindowToCompletedAt()
    {
        var order = OrderAggregate.Create(
            Guid.NewGuid(), "ORD-MEM", OrderType.Membership, UserId, Guid.Empty,
            CreateOrderItems(), CreateAddress(), 0m, 0m, DateTime.UtcNow.AddHours(1));
        order.MarkAsPaid(Guid.NewGuid(), "WeChatPay", DateTime.UtcNow, "T001");

        order.CompleteMembershipOrder();

        order.Status.Should().Be(OrderStatus.Completed);
        order.CompletedAt.Should().NotBeNull();
        order.AfterSalesWindowEndsAt.Should().Be(order.CompletedAt!.Value);
        order.DomainEvents.Should().Contain(e => e is OrderCompletedDomainEvent);
    }

    [Fact]
    public void Create_MembershipOrder_ShouldAllowEmptySellerId()
    {
        var order = OrderAggregate.Create(
            Guid.NewGuid(), "ORD-MEM", OrderType.Membership, UserId, Guid.Empty,
            CreateOrderItems(), CreateAddress(), 0m, 0m, DateTime.UtcNow.AddHours(1));

        order.SellerId.Should().BeNull();
        order.OrderType.Should().Be(OrderType.Membership);
    }

    [Fact]
    public void Create_NormalOrder_ShouldStillRequireSellerId()
    {
        var act = () => OrderAggregate.Create(
            Guid.NewGuid(), "ORD-NORM", OrderType.Normal, UserId, Guid.Empty,
            CreateOrderItems(), CreateAddress(), 10m, 0m, DateTime.UtcNow.AddHours(1));

        act.Should().Throw<OrderDomainException>().WithMessage("*SellerId*");
    }

    #endregion

    #region RowVersion

    [Fact]
    public void Order_Should_Have_RowVersion_Property_Initialized_Empty()
    {
        var order = CreateOrder();

        order.RowVersion.Should().NotBeNull();
        order.RowVersion.Should().HaveCount(0);
    }

    #endregion

    private static OrderAggregate CreateOrder()
    {
        return OrderAggregate.Create(
            Guid.NewGuid(), "ORD-001", OrderType.Normal, UserId, SellerId,
            CreateOrderItems(), CreateAddress(), 10m, 0m, DateTime.UtcNow.AddHours(1));
    }

    private static List<OrderItem> CreateOrderItems()
    {
        var snapshot = ProductSnapshot.Create(SkuId, SpuId, "Test Product", "Red-XL", null, SellerId);
        var item = OrderItem.Create(Guid.NewGuid(), SkuId, snapshot, 99.99m, 1, null);
        return new List<OrderItem> { item };
    }

    private static AddressSnapshot CreateAddress()
    {
        return AddressSnapshot.Create("张三", "13800138000", "广东", "深圳", "南山区", "科技园路1号");
    }
}