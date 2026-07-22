using Leno.Product.Domain.Aggregates;
using Leno.Product.Domain.Exceptions;

namespace Leno.Product.Domain.Tests;

/// <summary>
/// P1-T14 单元测试：验证 <see cref="StockBaseline.SyncDeducted"/> 在扣减超过可用库存时
/// 抛出异常且不修改聚合状态（先校验后赋值）。
/// 修复审计 #14：原实现在 AvailableQty -= delta、DeductedQty = deductedQty 之后才校验
/// AvailableQty < 0 并抛异常，导致异常抛出后聚合状态已被修改。
/// </summary>
public class StockBaselineSyncDeductedTests
{
    /// <summary>
    /// 扣减超过可用库存时抛异常，且 AvailableQty/ReservedQty/DeductedQty 保持不变。
    /// </summary>
    [Fact]
    public void SyncDeducted_ExceedAvailable_ThrowsAndDoesNotMutateState()
    {
        // Arrange — 可用 100，无预占
        var baseline = StockBaseline.Create(Guid.NewGuid(), Guid.NewGuid(), 100, Guid.NewGuid());

        // Act — 扣减 101 超过可用
        var act = () => baseline.SyncDeducted(101);

        // Assert — 抛异常
        act.Should().Throw<ProductDomainException>().WithMessage("*可用库存*");

        // 关键断言：状态未被修改（修复前的 bug 是状态已被修改）
        baseline.AvailableQty.Should().Be(100, "异常抛出后可用库存不应被修改");
        baseline.ReservedQty.Should().Be(0, "异常抛出后预占库存不应被修改");
        baseline.DeductedQty.Should().Be(0, "异常抛出后扣减库存不应被修改");
    }

    /// <summary>
    /// 有预占时扣减超过可用库存，抛异常且预占也不变。
    /// </summary>
    [Fact]
    public void SyncDeducted_ExceedAvailableWithReserved_ThrowsAndDoesNotMutateState()
    {
        // Arrange — 可用 100，预占 30
        var baseline = StockBaseline.Create(Guid.NewGuid(), Guid.NewGuid(), 100, Guid.NewGuid());
        baseline.SyncReserved(30);

        // Act — 扣减 101 超过可用
        var act = () => baseline.SyncDeducted(101);

        // Assert
        act.Should().Throw<ProductDomainException>().WithMessage("*可用库存*");
        baseline.AvailableQty.Should().Be(100);
        baseline.ReservedQty.Should().Be(30);
        baseline.DeductedQty.Should().Be(0);
    }

    /// <summary>
    /// 正常扣减（不超过可用）应正确更新三个字段。
    /// </summary>
    [Fact]
    public void SyncDeducted_ValidQty_ShouldUpdateAllFields()
    {
        // Arrange — 可用 100，预占 30
        var baseline = StockBaseline.Create(Guid.NewGuid(), Guid.NewGuid(), 100, Guid.NewGuid());
        baseline.SyncReserved(30);

        // Act — 扣减 20
        baseline.SyncDeducted(20);

        // Assert — 可用 80、预占 10、扣减 20
        baseline.AvailableQty.Should().Be(80);
        baseline.ReservedQty.Should().Be(10);
        baseline.DeductedQty.Should().Be(20);
    }

    /// <summary>
    /// 增量扣减（第二次扣减）应基于上次扣减量计算 delta。
    /// </summary>
    [Fact]
    public void SyncDeducted_IncrementalDeduction_ShouldCalculateDeltaFromLast()
    {
        // Arrange
        var baseline = StockBaseline.Create(Guid.NewGuid(), Guid.NewGuid(), 100, Guid.NewGuid());
        baseline.SyncReserved(50);
        baseline.SyncDeducted(20); // 第一次扣减 20

        // Act — 第二次扣减至 50（delta = 30）
        baseline.SyncDeducted(50);

        // Assert — 可用 50、预占 0、扣减 50
        baseline.AvailableQty.Should().Be(50);
        baseline.ReservedQty.Should().Be(0);
        baseline.DeductedQty.Should().Be(50);
    }

    /// <summary>
    /// 增量扣减超过可用时抛异常且状态不变。
    /// </summary>
    [Fact]
    public void SyncDeducted_IncrementalExceedAvailable_ThrowsAndDoesNotMutateState()
    {
        // Arrange
        var baseline = StockBaseline.Create(Guid.NewGuid(), Guid.NewGuid(), 100, Guid.NewGuid());
        baseline.SyncReserved(30);
        baseline.SyncDeducted(20); // 第一次扣减 20，可用 80

        // Act — 第二次扣减至 101（delta = 81 > 80 可用）
        var act = () => baseline.SyncDeducted(101);

        // Assert
        act.Should().Throw<ProductDomainException>().WithMessage("*可用库存*");
        baseline.AvailableQty.Should().Be(80, "增量扣减失败后可用库存不应被修改");
        baseline.ReservedQty.Should().Be(10);
        baseline.DeductedQty.Should().Be(20);
    }

    /// <summary>
    /// 扣减量减少（delta ≤ 0）时不应改变可用和预占，仅更新 DeductedQty。
    /// </summary>
    [Fact]
    public void SyncDeducted_DeltaZeroOrNegative_ShouldOnlyUpdateDeductedQty()
    {
        // Arrange
        var baseline = StockBaseline.Create(Guid.NewGuid(), Guid.NewGuid(), 100, Guid.NewGuid());
        baseline.SyncReserved(30);
        baseline.SyncDeducted(50); // 可用 50、预占 0（max(0, 30-50)）、扣减 50

        // Act — 同步更小的扣减量（delta = -10）
        baseline.SyncDeducted(40);

        // Assert — 可用和预占不变，DeductedQty 更新
        baseline.AvailableQty.Should().Be(50);
        baseline.ReservedQty.Should().Be(0);
        baseline.DeductedQty.Should().Be(40);
    }
}
