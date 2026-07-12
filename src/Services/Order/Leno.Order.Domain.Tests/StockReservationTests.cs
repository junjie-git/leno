using Leno.Order.Domain.Aggregates;
using Leno.Order.Domain.Exceptions;

namespace Leno.Order.Domain.Tests;

public class StockReservationTests
{
    private static readonly Guid SkuId = Guid.NewGuid();
    private static readonly Guid OrderId = Guid.NewGuid();

    [Fact]
    public void Create_ValidInput_ShouldInitialize()
    {
        var reservation = StockReservation.Create(Guid.NewGuid(), SkuId, 100);

        reservation.SkuId.Should().Be(SkuId);
        reservation.BaseLineQty.Should().Be(100);
        reservation.ReservedQty.Should().Be(0);
        reservation.DeductedQty.Should().Be(0);
        reservation.AvailableQty.Should().Be(100);
    }

    [Fact]
    public void Create_EmptySkuId_ShouldThrowException()
    {
        var act = () => StockReservation.Create(Guid.NewGuid(), Guid.Empty, 100);

        act.Should().Throw<OrderDomainException>().WithMessage("*SkuId*");
    }

    [Fact]
    public void Create_NegativeBaseline_ShouldThrowException()
    {
        var act = () => StockReservation.Create(Guid.NewGuid(), SkuId, -1);

        act.Should().Throw<OrderDomainException>().WithMessage("*基线*");
    }

    [Fact]
    public void ReserveStock_Valid_ShouldUpdateReserved()
    {
        var reservation = CreateReservation();

        reservation.ReserveStock(OrderId, 30);

        reservation.ReservedQty.Should().Be(30);
        reservation.AvailableQty.Should().Be(70);
        reservation.DomainEvents.Should().NotBeEmpty();
    }

    [Fact]
    public void ReserveStock_EmptyOrderId_ShouldThrowException()
    {
        var reservation = CreateReservation();

        var act = () => reservation.ReserveStock(Guid.Empty, 10);

        act.Should().Throw<OrderDomainException>().WithMessage("*OrderId*");
    }

    [Fact]
    public void ReserveStock_ZeroQuantity_ShouldThrowException()
    {
        var reservation = CreateReservation();

        var act = () => reservation.ReserveStock(OrderId, 0);

        act.Should().Throw<OrderDomainException>().WithMessage("*大于*");
    }

    [Fact]
    public void ReserveStock_Insufficient_ShouldThrowException()
    {
        var reservation = CreateReservation();

        var act = () => reservation.ReserveStock(OrderId, 200);

        act.Should().Throw<OrderDomainException>().WithMessage("*不足*");
    }

    [Fact]
    public void ConfirmStockDeduction_Valid_ShouldMoveReservedToDeducted()
    {
        var reservation = CreateReservation();
        reservation.ReserveStock(OrderId, 30);

        reservation.ConfirmStockDeduction(OrderId, 20);

        reservation.ReservedQty.Should().Be(10);
        reservation.DeductedQty.Should().Be(20);
        reservation.AvailableQty.Should().Be(70);
    }

    [Fact]
    public void ConfirmStockDeduction_ReservedInsufficient_ShouldThrowException()
    {
        var reservation = CreateReservation();
        reservation.ReserveStock(OrderId, 10);

        var act = () => reservation.ConfirmStockDeduction(OrderId, 20);

        act.Should().Throw<OrderDomainException>().WithMessage("*预占不足*");
    }

    [Fact]
    public void ReleaseStock_Valid_ShouldReduceReserved()
    {
        var reservation = CreateReservation();
        reservation.ReserveStock(OrderId, 30);

        reservation.ReleaseStock(OrderId, 20);

        reservation.ReservedQty.Should().Be(10);
        reservation.AvailableQty.Should().Be(90);
    }

    [Fact]
    public void ReleaseStock_ReservedInsufficient_ShouldThrowException()
    {
        var reservation = CreateReservation();
        reservation.ReserveStock(OrderId, 10);

        var act = () => reservation.ReleaseStock(OrderId, 20);

        act.Should().Throw<OrderDomainException>().WithMessage("*预占不足*");
    }

    [Fact]
    public void Replenish_Valid_ShouldUpdateBaseline()
    {
        var reservation = CreateReservation();

        reservation.Replenish(50);

        reservation.BaseLineQty.Should().Be(150);
        reservation.AvailableQty.Should().Be(150);
    }

    [Fact]
    public void Replenish_ZeroDelta_ShouldThrowException()
    {
        var reservation = CreateReservation();

        var act = () => reservation.Replenish(0);

        act.Should().Throw<OrderDomainException>().WithMessage("*不可为 0*");
    }

    [Fact]
    public void Replenish_ResultingNegative_ShouldThrowException()
    {
        var reservation = CreateReservation();

        var act = () => reservation.Replenish(-200);

        act.Should().Throw<OrderDomainException>().WithMessage("*基线*");
    }

    private static StockReservation CreateReservation()
    {
        return StockReservation.Create(Guid.NewGuid(), SkuId, 100);
    }
}