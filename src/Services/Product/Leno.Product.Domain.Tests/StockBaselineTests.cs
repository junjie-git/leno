using Leno.Product.Domain.Aggregates;
using Leno.Product.Domain.Exceptions;

namespace Leno.Product.Domain.Tests;

public class StockBaselineTests
{
    [Fact]
    public void Create_ValidParameters_ShouldCreateStockBaseline()
    {
        var baseline = StockBaseline.Create(Guid.NewGuid(), Guid.NewGuid(), 100);

        baseline.AvailableQty.Should().Be(100);
        baseline.ReservedQty.Should().Be(0);
        baseline.DeductedQty.Should().Be(0);
    }

    [Fact]
    public void Create_NegativeInitialQty_ShouldThrowException()
    {
        var act = () => StockBaseline.Create(Guid.NewGuid(), Guid.NewGuid(), -1);

        act.Should().Throw<ProductDomainException>().WithMessage("*库存*");
    }

    [Fact]
    public void Replenish_PositiveQty_ShouldIncreaseAvailable()
    {
        var baseline = CreateBaseline();

        baseline.Replenish(50);

        baseline.AvailableQty.Should().Be(150);
    }

    [Fact]
    public void Replenish_ZeroOrNegative_ShouldThrowException()
    {
        var baseline = CreateBaseline();

        var act = () => baseline.Replenish(0);

        act.Should().Throw<ProductDomainException>().WithMessage("*补货*");
    }

    [Fact]
    public void SyncReserved_ValidQty_ShouldUpdateReserved()
    {
        var baseline = CreateBaseline();

        baseline.SyncReserved(30);

        baseline.ReservedQty.Should().Be(30);
    }

    [Fact]
    public void SyncReserved_ExceedAvailable_ShouldThrowException()
    {
        var baseline = CreateBaseline();

        var act = () => baseline.SyncReserved(101);

        act.Should().Throw<ProductDomainException>().WithMessage("*预占*");
    }

    [Fact]
    public void SyncDeducted_ValidQty_ShouldReduceAvailableAndReserved()
    {
        var baseline = CreateBaseline();
        baseline.SyncReserved(30);

        baseline.SyncDeducted(20);

        baseline.ReservedQty.Should().Be(10);
        baseline.DeductedQty.Should().Be(20);
        baseline.AvailableQty.Should().Be(80);
    }

    [Fact]
    public void SyncDeducted_ExceedAvailable_ShouldThrowException()
    {
        var baseline = CreateBaseline();

        var act = () => baseline.SyncDeducted(101);

        act.Should().Throw<ProductDomainException>().WithMessage("*可用库存*");
    }

    [Fact]
    public void SyncReleased_ValidQty_ShouldReduceReserved()
    {
        var baseline = CreateBaseline();
        baseline.SyncReserved(30);

        baseline.SyncReleased(20);

        baseline.ReservedQty.Should().Be(10);
    }

    [Fact]
    public void SyncReleased_ExceedReserved_ShouldThrowException()
    {
        var baseline = CreateBaseline();
        baseline.SyncReserved(30);

        var act = () => baseline.SyncReleased(31);

        act.Should().Throw<ProductDomainException>().WithMessage("*释放*");
    }

    private static StockBaseline CreateBaseline()
    {
        return StockBaseline.Create(Guid.NewGuid(), Guid.NewGuid(), 100);
    }
}