using Leno.Order.Domain.Aggregates;
using Leno.Order.Domain.Exceptions;
using Leno.Order.Domain.ValueObjects;

namespace Leno.Order.Domain.Tests;

public class OrderItemTests
{
    private static readonly Guid SkuId = Guid.NewGuid();
    private static readonly Guid SpuId = Guid.NewGuid();
    private static readonly Guid SellerId = Guid.NewGuid();

    [Fact]
    public void Create_ValidInput_ShouldSetProperties()
    {
        var snapshot = CreateSnapshot();

        var item = OrderItem.Create(Guid.NewGuid(), SkuId, snapshot, 99.99m, 2, Guid.NewGuid());

        item.SkuId.Should().Be(SkuId);
        item.UnitPrice.Should().Be(99.99m);
        item.Quantity.Should().Be(2);
        item.Subtotal.Should().Be(199.98m);
        item.DiscountAllocation.Should().Be(0);
        item.SourceCartItemId.Should().NotBeNull();
    }

    [Fact]
    public void Create_EmptySkuId_ShouldThrowException()
    {
        var snapshot = CreateSnapshot();

        var act = () => OrderItem.Create(Guid.NewGuid(), Guid.Empty, snapshot, 99.99m, 1, null);

        act.Should().Throw<OrderDomainException>().WithMessage("*SkuId*");
    }

    [Fact]
    public void Create_NullSnapshot_ShouldThrowException()
    {
        var act = () => OrderItem.Create(Guid.NewGuid(), SkuId, null!, 99.99m, 1, null);

        act.Should().Throw<OrderDomainException>().WithMessage("*快照*");
    }

    [Fact]
    public void Create_NegativeUnitPrice_ShouldThrowException()
    {
        var snapshot = CreateSnapshot();

        var act = () => OrderItem.Create(Guid.NewGuid(), SkuId, snapshot, -1m, 1, null);

        act.Should().Throw<OrderDomainException>().WithMessage("*单价*");
    }

    [Fact]
    public void Create_ZeroQuantity_ShouldThrowException()
    {
        var snapshot = CreateSnapshot();

        var act = () => OrderItem.Create(Guid.NewGuid(), SkuId, snapshot, 99.99m, 0, null);

        act.Should().Throw<OrderDomainException>().WithMessage("*数量*");
    }

    [Fact]
    public void ApplyDiscount_Valid_ShouldSetDiscountAllocation()
    {
        var snapshot = CreateSnapshot();
        var item = OrderItem.Create(Guid.NewGuid(), SkuId, snapshot, 100m, 2, null);

        item.ApplyDiscount(50m);

        item.DiscountAllocation.Should().Be(50m);
    }

    [Fact]
    public void ApplyDiscount_Negative_ShouldThrowException()
    {
        var snapshot = CreateSnapshot();
        var item = OrderItem.Create(Guid.NewGuid(), SkuId, snapshot, 100m, 1, null);

        var act = () => item.ApplyDiscount(-1m);

        act.Should().Throw<OrderDomainException>().WithMessage("*非法*");
    }

    [Fact]
    public void ApplyDiscount_ExceedsSubtotal_ShouldThrowException()
    {
        var snapshot = CreateSnapshot();
        var item = OrderItem.Create(Guid.NewGuid(), SkuId, snapshot, 100m, 1, null);

        var act = () => item.ApplyDiscount(200m);

        act.Should().Throw<OrderDomainException>().WithMessage("*非法*");
    }

    private static ProductSnapshot CreateSnapshot()
    {
        return ProductSnapshot.Create(SkuId, SpuId, "Test Product", "Red-XL", null, SellerId);
    }
}