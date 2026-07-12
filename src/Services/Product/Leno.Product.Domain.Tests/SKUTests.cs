using Leno.Product.Domain.Aggregates;
using Leno.Product.Domain.Exceptions;
using Leno.Product.Domain.ValueObjects;
using Leno.SharedKernel.ValueObjects;

namespace Leno.Product.Domain.Tests;

public class SKUTests
{
    [Fact]
    public void Create_ValidParameters_ShouldCreateActiveSku()
    {
        var sku = SKU.Create(Guid.NewGuid(), Guid.NewGuid(), "SKU-001",
            Money.Create(99.99m, "CNY"), 100, SkuSpec.Create([SpecAttribute.Create("Color", "Red")]));

        sku.SkuCode.Should().Be("SKU-001");
        sku.Price.Should().Be(Money.Create(99.99m, "CNY"));
        sku.StockQty.Should().Be(100);
        sku.Status.Should().Be(SkuStatus.Active);
    }

    [Fact]
    public void Create_EmptySkuCode_ShouldThrowException()
    {
        var act = () => SKU.Create(Guid.NewGuid(), Guid.NewGuid(), "",
            Money.Create(10m, "CNY"), 1, SkuSpec.Create([SpecAttribute.Create("C", "R")]));

        act.Should().Throw<ProductDomainException>().WithMessage("*SKU*编码*");
    }

    [Fact]
    public void Create_PriceZero_ShouldThrowException()
    {
        var act = () => SKU.Create(Guid.NewGuid(), Guid.NewGuid(), "SKU-001",
            Money.Create(0m, "CNY"), 1, SkuSpec.Create([SpecAttribute.Create("C", "R")]));

        act.Should().Throw<ProductDomainException>().WithMessage("*价格*");
    }

    [Fact]
    public void Create_NegativeStock_ShouldThrowException()
    {
        var act = () => SKU.Create(Guid.NewGuid(), Guid.NewGuid(), "SKU-001",
            Money.Create(10m, "CNY"), -1, SkuSpec.Create([SpecAttribute.Create("C", "R")]));

        act.Should().Throw<ProductDomainException>().WithMessage("*库存*");
    }

    [Fact]
    public void UpdatePrice_ValidPrice_ShouldUpdate()
    {
        var sku = CreateSku();

        sku.UpdatePrice(Money.Create(199.99m, "CNY"));

        sku.Price.Should().Be(Money.Create(199.99m, "CNY"));
    }

    [Fact]
    public void UpdatePrice_Zero_ShouldThrowException()
    {
        var sku = CreateSku();

        var act = () => sku.UpdatePrice(Money.Create(0m, "CNY"));

        act.Should().Throw<ProductDomainException>().WithMessage("*价格*");
    }

    [Fact]
    public void UpdateStock_ValidQty_ShouldUpdate()
    {
        var sku = CreateSku();

        sku.UpdateStock(50);

        sku.StockQty.Should().Be(50);
    }

    [Fact]
    public void UpdateStock_Negative_ShouldThrowException()
    {
        var sku = CreateSku();

        var act = () => sku.UpdateStock(-1);

        act.Should().Throw<ProductDomainException>().WithMessage("*库存*");
    }

    [Fact]
    public void Activate_InactiveSku_ShouldSetActive()
    {
        var sku = CreateSku();
        sku.Deactivate();

        sku.Activate();

        sku.Status.Should().Be(SkuStatus.Active);
    }

    [Fact]
    public void Deactivate_ActiveSku_ShouldSetInactive()
    {
        var sku = CreateSku();

        sku.Deactivate();

        sku.Status.Should().Be(SkuStatus.Inactive);
    }

    private static SKU CreateSku()
    {
        return SKU.Create(Guid.NewGuid(), Guid.NewGuid(), "SKU-001",
            Money.Create(99.99m, "CNY"), 100, SkuSpec.Create([SpecAttribute.Create("Color", "Red")]));
    }
}