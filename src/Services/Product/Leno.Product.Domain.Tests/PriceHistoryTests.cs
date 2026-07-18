using FluentAssertions;
using Leno.Product.Domain.Aggregates;
using Xunit;

namespace Leno.Product.Domain.Tests;

public class PriceHistoryTests
{
    [Fact]
    public void Create_Valid_ShouldSetProperties()
    {
        var spuId = Guid.NewGuid();
        var skuId = Guid.NewGuid();

        var history = PriceHistory.Create(spuId, skuId, oldPrice: 99.9m, newPrice: 89.9m, reason: "促销调价");

        history.SpuId.Should().Be(spuId);
        history.SkuId.Should().Be(skuId);
        history.OldPrice.Should().Be(99.9m);
        history.NewPrice.Should().Be(89.9m);
        history.Currency.Should().Be("CNY");
        history.Reason.Should().Be("促销调价");
        history.ChangedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
        history.Id.Should().NotBeEmpty();
    }

    [Fact]
    public void Create_NegativePrice_ShouldThrow()
    {
        var act = () => PriceHistory.Create(Guid.NewGuid(), Guid.NewGuid(), 100m, -1m);

        act.Should().Throw<ArgumentException>().WithMessage("*价格不能为负*");
    }

    [Fact]
    public void Create_EmptySpuId_ShouldThrow()
    {
        var act = () => PriceHistory.Create(Guid.Empty, Guid.NewGuid(), 100m, 90m);

        act.Should().Throw<ArgumentException>().WithMessage("*SPU*");
    }

    [Fact]
    public void Create_EmptySkuId_ShouldThrow()
    {
        var act = () => PriceHistory.Create(Guid.NewGuid(), Guid.Empty, 100m, 90m);

        act.Should().Throw<ArgumentException>().WithMessage("*SKU*");
    }

    [Fact]
    public void Create_EmptyCurrency_ShouldThrow()
    {
        var act = () => PriceHistory.Create(Guid.NewGuid(), Guid.NewGuid(), 100m, 90m, currency: "");

        act.Should().Throw<ArgumentException>().WithMessage("*币种*");
    }

    [Fact]
    public void Create_NullReason_ShouldBeAllowed()
    {
        var history = PriceHistory.Create(Guid.NewGuid(), Guid.NewGuid(), 100m, 90m, reason: null);

        history.Reason.Should().BeNull();
    }

    [Fact]
    public void Create_WhitespaceReason_ShouldBeNormalizedToNull()
    {
        var history = PriceHistory.Create(Guid.NewGuid(), Guid.NewGuid(), 100m, 90m, reason: "   ");

        history.Reason.Should().BeNull();
    }

    [Fact]
    public void Create_CustomCurrency_ShouldSetCurrency()
    {
        var history = PriceHistory.Create(Guid.NewGuid(), Guid.NewGuid(), 100m, 90m, currency: "USD");

        history.Currency.Should().Be("USD");
    }

    [Fact]
    public void Create_ZeroNewPrice_ShouldBeAllowed()
    {
        var history = PriceHistory.Create(Guid.NewGuid(), Guid.NewGuid(), 100m, 0m);

        history.NewPrice.Should().Be(0m);
    }
}
