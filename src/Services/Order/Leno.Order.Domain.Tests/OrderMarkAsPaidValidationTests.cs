using Leno.Order.Domain.Aggregates;
using Leno.Order.Domain.Exceptions;
using Leno.Order.Domain.ValueObjects;
using OrderAggregate = Leno.Order.Domain.Aggregates.Order;

namespace Leno.Order.Domain.Tests;

/// <summary>
/// MarkAsPaid 支付校验测试，验证支付已发起、支付单标识非空、实付金额匹配等不变量。
/// CreateOrder 工厂：ItemsAmount=99.99 + FreightAmount=10 = TotalAmount=109.99。
/// </summary>
public class OrderMarkAsPaidValidationTests
{
    private static readonly Guid UserId = Guid.NewGuid();
    private static readonly Guid SellerId = Guid.NewGuid();
    private static readonly Guid SkuId = Guid.NewGuid();
    private static readonly Guid SpuId = Guid.NewGuid();

    private const decimal ExpectedTotalAmount = 99.99m + 10m;

    [Fact]
    public void MarkAsPaid_NotInitiated_ShouldThrowException()
    {
        var order = CreateOrder();

        var act = () => order.MarkAsPaid(Guid.NewGuid(), "WeChatPay", DateTime.UtcNow, "T001", ExpectedTotalAmount);

        act.Should().Throw<OrderDomainException>().Which.ErrorCode.Should().Be("ORDER_PAY_NOT_INITIATED");
    }

    [Fact]
    public void MarkAsPaid_AmountMismatch_ShouldThrowException()
    {
        var order = CreateOrder();
        order.MarkPaymentInitiated(PaymentMethod.WeChatPay);

        var act = () => order.MarkAsPaid(Guid.NewGuid(), "WeChatPay", DateTime.UtcNow, "T001", 50m);

        act.Should().Throw<OrderDomainException>().Which.ErrorCode.Should().Be("ORDER_PAID_AMOUNT_MISMATCH");
    }

    [Fact]
    public void MarkAsPaid_EmptyPaymentId_ShouldThrowException()
    {
        var order = CreateOrder();
        order.MarkPaymentInitiated(PaymentMethod.WeChatPay);

        var act = () => order.MarkAsPaid(Guid.Empty, "WeChatPay", DateTime.UtcNow, "T001", ExpectedTotalAmount);

        act.Should().Throw<OrderDomainException>().Which.ErrorCode.Should().Be("ORDER_PAYMENT_ID_EMPTY");
    }

    [Fact]
    public void MarkAsPaid_ValidWithAmount_ShouldSucceed()
    {
        var order = CreateOrder();
        order.MarkPaymentInitiated(PaymentMethod.WeChatPay);

        order.MarkAsPaid(Guid.NewGuid(), "WeChatPay", DateTime.UtcNow, "T001", ExpectedTotalAmount);

        order.Status.Should().Be(OrderStatus.Paid);
    }

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
