using Leno.Inventory.Domain.Aggregates;
using Leno.Inventory.Domain.Events;
using Leno.Inventory.Domain.Exceptions;

namespace Leno.Inventory.Domain.Tests.Aggregates;

/// <summary>
/// StockBaseline（SKU 库存基线聚合）单元测试。
/// 覆盖工厂校验、补货调整（含领域事件）、基线同步、不变量保护等场景。
/// </summary>
public class StockBaselineTests
{
    private static StockBaseline CreateBaseline(int initialQty = 100) => StockBaseline.Create(
        Guid.NewGuid(), Guid.NewGuid(), initialQty, Guid.NewGuid());

    #region Create

    [Fact]
    public void Create_With_Valid_Input_Should_Initialize_Counter()
    {
        var skuId = Guid.NewGuid();
        var productId = Guid.NewGuid();

        var baseline = StockBaseline.Create(Guid.NewGuid(), skuId, 100, productId);

        baseline.SkuId.Should().Be(skuId);
        baseline.ProductId.Should().Be(productId);
        baseline.AvailableQty.Should().Be(100);
        baseline.ReservedQty.Should().Be(0, "初始预占必须为 0");
        baseline.DeductedQty.Should().Be(0, "初始已扣减必须为 0");
    }

    [Fact]
    public void Create_With_Zero_InitialQty_Should_Be_Allowed()
    {
        var create = () => StockBaseline.Create(Guid.NewGuid(), Guid.NewGuid(), 0, Guid.NewGuid());

        create.Should().NotThrow("0 是合法初始库存（未同步基线的 SKU）");
    }

    [Theory]
    [InlineData(nameof(StockBaselineCreateEmptyCase.BaselineId))]
    [InlineData(nameof(StockBaselineCreateEmptyCase.SkuId))]
    [InlineData(nameof(StockBaselineCreateEmptyCase.ProductId))]
    public void Create_With_Empty_Id_Should_Throw_DomainException(string emptyField)
    {
        var baselineId = emptyField == nameof(StockBaselineCreateEmptyCase.BaselineId) ? Guid.Empty : Guid.NewGuid();
        var skuId = emptyField == nameof(StockBaselineCreateEmptyCase.SkuId) ? Guid.Empty : Guid.NewGuid();
        var productId = emptyField == nameof(StockBaselineCreateEmptyCase.ProductId) ? Guid.Empty : Guid.NewGuid();

        var create = () => StockBaseline.Create(baselineId, skuId, 10, productId);

        create.Should().Throw<InventoryDomainException>();
    }

    private enum StockBaselineCreateEmptyCase
    {
        BaselineId,
        SkuId,
        ProductId
    }

    [Fact]
    public void Create_With_Negative_InitialQty_Should_Throw_DomainException()
    {
        var create = () => StockBaseline.Create(Guid.NewGuid(), Guid.NewGuid(), -1, Guid.NewGuid());

        create.Should().Throw<InventoryDomainException>()
            .WithMessage("*初始库存不可为负*");
    }

    #endregion

    #region Adjust（补货/盘点）

    [Fact]
    public void Adjust_With_Positive_Delta_Should_Increase_Available_And_Raise_Event()
    {
        var baseline = CreateBaseline(100);

        baseline.Adjust(50);

        baseline.AvailableQty.Should().Be(150);
        baseline.DomainEvents.Should().ContainSingle("补货必须发布 StockAdjustedDomainEvent 通知下游")
            .Which.Should().BeOfType<StockAdjustedDomainEvent>();
        var evt = (StockAdjustedDomainEvent)baseline.DomainEvents.Single();
        evt.SkuId.Should().Be(baseline.SkuId);
        evt.AvailableQty.Should().Be(150);
        evt.Delta.Should().Be(50);
    }

    [Fact]
    public void Adjust_With_Negative_Delta_Should_Decrease_Available()
    {
        var baseline = CreateBaseline(100);

        baseline.Adjust(-30);

        baseline.AvailableQty.Should().Be(70);
    }

    [Fact]
    public void Adjust_With_Zero_Delta_Should_Throw_DomainException()
    {
        var baseline = CreateBaseline(100);

        var adjust = () => baseline.Adjust(0);

        adjust.Should().Throw<InventoryDomainException>()
            .WithMessage("*增量不可为 0*");
    }

    [Fact]
    public void Adjust_Below_Zero_Should_Throw_DomainException_And_Not_Mutate()
    {
        var baseline = CreateBaseline(10);

        var adjust = () => baseline.Adjust(-11);

        adjust.Should().Throw<InventoryDomainException>()
            .WithMessage("*不可为负*");
        baseline.AvailableQty.Should().Be(10, "失败调整不应改变聚合状态");
        baseline.DomainEvents.Should().BeEmpty("失败调整不应发布事件");
    }

    [Fact]
    public void Adjust_Should_Not_Touch_Reserved_And_Deducted()
    {
        var baseline = CreateBaseline(100);

        baseline.Adjust(20);

        baseline.ReservedQty.Should().Be(0);
        baseline.DeductedQty.Should().Be(0);
    }

    #endregion

    #region ApplyBaselineSync（商品域权威值覆盖）

    [Fact]
    public void ApplyBaselineSync_Should_Overwrite_Available_Only()
    {
        var baseline = StockBaseline.Create(Guid.NewGuid(), Guid.NewGuid(), 50, Guid.NewGuid());

        baseline.ApplyBaselineSync(200);

        baseline.AvailableQty.Should().Be(200, "基线同步应覆盖为商品域权威值");
    }

    [Fact]
    public void ApplyBaselineSync_With_Negative_Qty_Should_Throw_DomainException()
    {
        var baseline = CreateBaseline(10);

        var sync = () => baseline.ApplyBaselineSync(-1);

        sync.Should().Throw<InventoryDomainException>()
            .WithMessage("*不可为负*");
    }

    [Fact]
    public void ApplyBaselineSync_Should_Not_Raise_Domain_Event()
    {
        var baseline = CreateBaseline(10);

        baseline.ApplyBaselineSync(30);

        baseline.DomainEvents.Should().BeEmpty("基线同步是事件驱动的结果，不应再发事件造成回环");
    }

    #endregion
}
