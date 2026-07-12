using Leno.Product.Domain.ValueObjects;

namespace Leno.Product.Domain.Tests;

public class StockOperationRecordTests
{
    [Fact]
    public void Create_ValidParameters_ShouldCreateRecord()
    {
        var record = StockOperationRecord.Create("sku-001", "operator-1", 10, 110);

        record.SkuId.Should().Be("sku-001");
        record.Operator.Should().Be("operator-1");
        record.Delta.Should().Be(10);
        record.NewStock.Should().Be(110);
        record.OperatedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
    }

    [Fact]
    public void Create_EmptySkuId_ShouldThrowException()
    {
        var act = () => StockOperationRecord.Create("", "operator-1", 10, 110);

        act.Should().Throw<ArgumentException>().WithMessage("*SKU*");
    }

    [Fact]
    public void Create_EmptyOperator_ShouldThrowException()
    {
        var act = () => StockOperationRecord.Create("sku-001", "", 10, 110);

        act.Should().Throw<ArgumentException>().WithMessage("*操作人*");
    }

    [Fact]
    public void Create_NegativeDelta_ShouldRecordCorrectly()
    {
        var record = StockOperationRecord.Create("sku-001", "operator-1", -5, 95);

        record.Delta.Should().Be(-5);
        record.NewStock.Should().Be(95);
    }
}