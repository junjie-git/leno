using Leno.Product.Domain.ValueObjects;

namespace Leno.Product.Domain.Tests;

public class PriceChangeRecordTests
{
    [Fact]
    public void Create_ValidParameters_ShouldCreateRecord()
    {
        var record = PriceChangeRecord.Create("sku-001", 99.99m, 79.99m, "seller-1");

        record.SkuId.Should().Be("sku-001");
        record.OldPrice.Should().Be(99.99m);
        record.NewPrice.Should().Be(79.99m);
        record.ChangedBy.Should().Be("seller-1");
        record.ChangedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
    }

    [Fact]
    public void Create_EmptySkuId_ShouldThrowException()
    {
        var act = () => PriceChangeRecord.Create("", 99.99m, 79.99m, "seller-1");

        act.Should().Throw<ArgumentException>().WithMessage("*SKU*");
    }

    [Fact]
    public void Create_EmptyChangedBy_ShouldThrowException()
    {
        var act = () => PriceChangeRecord.Create("sku-001", 99.99m, 79.99m, "");

        act.Should().Throw<ArgumentException>().WithMessage("*变更人*");
    }

    [Fact]
    public void Create_NonPositiveNewPrice_ShouldThrowException()
    {
        var act = () => PriceChangeRecord.Create("sku-001", 99.99m, 0m, "seller-1");

        act.Should().Throw<ArgumentException>().WithMessage("*新价格*");
    }

    [Fact]
    public void Create_PriceIncrease_ShouldRecordCorrectly()
    {
        var record = PriceChangeRecord.Create("sku-001", 50m, 100m, "seller-1");

        record.OldPrice.Should().Be(50m);
        record.NewPrice.Should().Be(100m);
    }
}