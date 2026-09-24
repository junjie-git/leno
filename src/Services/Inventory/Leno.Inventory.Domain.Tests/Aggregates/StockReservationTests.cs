using Leno.Inventory.Domain.Aggregates;
using Leno.Inventory.Domain.Exceptions;

namespace Leno.Inventory.Domain.Tests.Aggregates;

/// <summary>
/// StockReservation（库存台账聚合）单元测试。
/// 覆盖工厂校验、单向状态机迁移、终态幂等 no-op、非法迁移拒绝等核心不变量。
/// </summary>
public class StockReservationTests
{
    private static StockReservation CreateReserved() => StockReservation.CreateReserved(
        Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), 2, Guid.NewGuid());

    #region CreateReserved

    [Fact]
    public void CreateReserved_With_Valid_Input_Should_Start_In_Reserved()
    {
        var orderId = Guid.NewGuid();
        var skuId = Guid.NewGuid();
        var idempotencyKey = Guid.NewGuid();

        var reservation = StockReservation.CreateReserved(Guid.NewGuid(), orderId, skuId, 3, idempotencyKey);

        reservation.OrderId.Should().Be(orderId);
        reservation.SkuId.Should().Be(skuId);
        reservation.Quantity.Should().Be(3);
        reservation.Status.Should().Be(StockReservationStatus.Reserved);
        reservation.IdempotencyKey.Should().Be(idempotencyKey);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void CreateReserved_With_NonPositive_Quantity_Should_Throw_DomainException(int quantity)
    {
        var create = () => StockReservation.CreateReserved(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), quantity, Guid.NewGuid());

        create.Should().Throw<InventoryDomainException>()
            .WithMessage("*大于 0*");
    }

    [Fact]
    public void CreateReserved_With_Empty_OrderId_Should_Throw_DomainException()
    {
        var create = () => StockReservation.CreateReserved(
            Guid.NewGuid(), Guid.Empty, Guid.NewGuid(), 1, Guid.NewGuid());

        create.Should().Throw<InventoryDomainException>()
            .WithMessage("*OrderId 不可为空*");
    }

    [Fact]
    public void CreateReserved_With_Empty_SkuId_Should_Throw_DomainException()
    {
        var create = () => StockReservation.CreateReserved(
            Guid.NewGuid(), Guid.NewGuid(), Guid.Empty, 1, Guid.NewGuid());

        create.Should().Throw<InventoryDomainException>();
    }

    #endregion

    #region 状态机：合法迁移

    [Fact]
    public void Transition_Reserve_To_Confirmed_Should_Succeed_And_Return_True()
    {
        var reservation = CreateReserved();

        var changed = reservation.Transition(StockReservationStatus.Confirmed);

        changed.Should().BeTrue();
        reservation.Status.Should().Be(StockReservationStatus.Confirmed);
    }

    [Fact]
    public void Transition_Reserve_To_Released_Should_Succeed()
    {
        var reservation = CreateReserved();

        reservation.Transition(StockReservationStatus.Released);

        reservation.Status.Should().Be(StockReservationStatus.Released);
    }

    [Fact]
    public void Transition_Confirmed_To_Returned_Should_Succeed()
    {
        var reservation = CreateReserved();
        reservation.Transition(StockReservationStatus.Confirmed);

        var changed = reservation.Transition(StockReservationStatus.Returned);

        changed.Should().BeTrue();
        reservation.Status.Should().Be(StockReservationStatus.Returned);
    }

    [Fact]
    public void MarkReturned_On_Confirmed_Should_Succeed()
    {
        var reservation = CreateReserved();
        reservation.Transition(StockReservationStatus.Confirmed);

        reservation.MarkReturned();

        reservation.Status.Should().Be(StockReservationStatus.Returned);
    }

    #endregion

    #region 状态机：终态幂等 no-op

    [Fact]
    public void Transition_To_Same_Status_Should_Be_Idempotent_NoOp()
    {
        var reservation = CreateReserved();

        var changed = reservation.Transition(StockReservationStatus.Reserved);

        changed.Should().BeFalse("重复命令在终态/同态上应为幂等 no-op");
        reservation.Status.Should().Be(StockReservationStatus.Reserved);
    }

    [Fact]
    public void Transition_Confirmed_To_Confirmed_Should_Be_Idempotent()
    {
        var reservation = CreateReserved();
        reservation.Transition(StockReservationStatus.Confirmed);

        var changed = reservation.Transition(StockReservationStatus.Confirmed);

        changed.Should().BeFalse();
    }

    [Fact]
    public void Transition_Returned_To_Returned_Should_Be_Idempotent()
    {
        var reservation = CreateReserved();
        reservation.Transition(StockReservationStatus.Confirmed);
        reservation.Transition(StockReservationStatus.Returned);

        var changed = reservation.Transition(StockReservationStatus.Returned);

        changed.Should().BeFalse();
    }

    #endregion

    #region 状态机：非法迁移

    [Fact]
    public void Transition_Reserve_To_Returned_Should_Throw()
    {
        var reservation = CreateReserved();

        var transition = () => reservation.Transition(StockReservationStatus.Returned);

        transition.Should().Throw<InventoryDomainException>()
            .WithMessage("*非法的台账状态迁移*");
    }

    [Fact]
    public void Transition_Confirmed_To_Released_Should_Throw()
    {
        var reservation = CreateReserved();
        reservation.Transition(StockReservationStatus.Confirmed);

        var transition = () => reservation.Transition(StockReservationStatus.Released);

        transition.Should().Throw<InventoryDomainException>("已扣减库存不可直接释放，应走归还");
    }

    [Fact]
    public void Transition_Released_To_Confirmed_Should_Throw()
    {
        var reservation = CreateReserved();
        reservation.Transition(StockReservationStatus.Released);

        var transition = () => reservation.Transition(StockReservationStatus.Confirmed);

        transition.Should().Throw<InventoryDomainException>("终态不可逆");
    }

    [Fact]
    public void MarkReturned_On_Reserved_Should_Throw()
    {
        var reservation = CreateReserved();

        var markReturned = () => reservation.MarkReturned();

        markReturned.Should().Throw<InventoryDomainException>()
            .WithMessage("*仅已确认*");
    }

    [Fact]
    public void MarkReturned_On_Released_Should_Throw()
    {
        var reservation = CreateReserved();
        reservation.Transition(StockReservationStatus.Released);

        var markReturned = () => reservation.MarkReturned();

        markReturned.Should().Throw<InventoryDomainException>();
    }

    #endregion
}
