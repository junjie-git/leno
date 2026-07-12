using Leno.Cart.Domain.Aggregates;
using Leno.Cart.Domain.Exceptions;
using CartAggregate = Leno.Cart.Domain.Aggregates.Cart;

namespace Leno.Cart.Domain.Tests;

public class AnonymousCartTests
{
    private static readonly Guid SellerId = Guid.NewGuid();

    [Fact]
    public void CreateAnonymous_ShouldCreateEmptyCartWithEmptyUserId()
    {
        var cart = CartAggregate.CreateAnonymous(Guid.NewGuid());

        cart.UserId.Should().Be(Guid.Empty);
        cart.Items.Should().BeEmpty();
    }

    [Fact]
    public void CreateAnonymous_EmptyCartId_ShouldGenerateNewId()
    {
        var cart = CartAggregate.CreateAnonymous(Guid.Empty);

        cart.Id.Should().NotBe(Guid.Empty);
        cart.UserId.Should().Be(Guid.Empty);
    }

    [Fact]
    public void AddItem_NewSku_ShouldAddToAnonymousCart()
    {
        var cart = CreateAnonymousCart();
        var skuId = Guid.NewGuid();

        cart.AddItem(skuId, 3, SellerId);

        cart.Items.Should().HaveCount(1);
        cart.Items.First().SkuId.Should().Be(skuId);
        cart.Items.First().Quantity.Should().Be(3);
    }

    [Fact]
    public void AddItem_ExistingSku_ShouldMergeQuantity()
    {
        var cart = CreateAnonymousCart();
        var skuId = Guid.NewGuid();
        cart.AddItem(skuId, 3, SellerId);

        cart.AddItem(skuId, 2, SellerId);

        cart.Items.Should().HaveCount(1);
        cart.Items.First().Quantity.Should().Be(5);
    }

    [Fact]
    public void AddItem_MergeExceedsLimit_ShouldThrowException()
    {
        var cart = CreateAnonymousCart();
        var skuId = Guid.NewGuid();
        cart.AddItem(skuId, 50, SellerId);

        var act = () => cart.AddItem(skuId, 50, SellerId);

        act.Should().Throw<CartDomainException>().WithMessage("*上限*");
    }

    [Fact]
    public void AddItem_EmptySkuId_ShouldThrowException()
    {
        var cart = CreateAnonymousCart();

        var act = () => cart.AddItem(Guid.Empty, 1, SellerId);

        act.Should().Throw<ArgumentException>().WithMessage("*SkuId*");
    }

    [Fact]
    public void AddItem_EmptySellerId_ShouldThrowException()
    {
        var cart = CreateAnonymousCart();

        var act = () => cart.AddItem(Guid.NewGuid(), 1, Guid.Empty);

        act.Should().Throw<ArgumentException>().WithMessage("*SellerId*");
    }

    [Fact]
    public void UpdateItemQuantity_ValidQuantity_ShouldUpdate()
    {
        var cart = CreateAnonymousCart();
        var skuId = Guid.NewGuid();
        cart.AddItem(skuId, 3, SellerId);

        cart.UpdateItemQuantity(skuId, 5);

        cart.Items.First().Quantity.Should().Be(5);
    }

    [Fact]
    public void UpdateItemQuantity_NotInCart_ShouldThrowException()
    {
        var cart = CreateAnonymousCart();

        var act = () => cart.UpdateItemQuantity(Guid.NewGuid(), 5);

        act.Should().Throw<CartDomainException>().WithMessage("*不存在*");
    }

    [Fact]
    public void RemoveItem_ExistingSku_ShouldRemove()
    {
        var cart = CreateAnonymousCart();
        var skuId = Guid.NewGuid();
        cart.AddItem(skuId, 3, SellerId);

        cart.RemoveItem(skuId);

        cart.Items.Should().BeEmpty();
    }

    [Fact]
    public void RemoveItem_NotInCart_ShouldThrowException()
    {
        var cart = CreateAnonymousCart();

        var act = () => cart.RemoveItem(Guid.NewGuid());

        act.Should().Throw<CartDomainException>().WithMessage("*不存在*");
    }

    [Fact]
    public void SelectItems_ShouldSetSelected()
    {
        var cart = CreateAnonymousCart();
        var skuId1 = Guid.NewGuid();
        var skuId2 = Guid.NewGuid();
        cart.AddItem(skuId1, 1, SellerId);
        cart.AddItem(skuId2, 1, SellerId);
        cart.DeselectItems(new[] { skuId1, skuId2 });

        cart.SelectItems(new[] { skuId1 });

        cart.Items.First(i => i.SkuId == skuId1).IsSelected.Should().BeTrue();
        cart.Items.First(i => i.SkuId == skuId2).IsSelected.Should().BeFalse();
    }

    [Fact]
    public void DeselectItems_ShouldSetNotSelected()
    {
        var cart = CreateAnonymousCart();
        var skuId = Guid.NewGuid();
        cart.AddItem(skuId, 1, SellerId);

        cart.DeselectItems(new[] { skuId });

        cart.Items.First().IsSelected.Should().BeFalse();
    }

    [Fact]
    public void ClearSelectedItems_ShouldRemoveSelected()
    {
        var cart = CreateAnonymousCart();
        var skuId1 = Guid.NewGuid();
        var skuId2 = Guid.NewGuid();
        cart.AddItem(skuId1, 1, SellerId);
        cart.AddItem(skuId2, 1, SellerId);
        cart.DeselectItems(new[] { skuId2 });

        var sourceIds = cart.ClearSelectedItems();

        sourceIds.Should().HaveCount(1);
        cart.Items.Should().HaveCount(1);
        cart.Items.First().SkuId.Should().Be(skuId2);
    }

    [Fact]
    public void ClearItemsBySourceIds_ShouldRemoveMatching()
    {
        var cart = CreateAnonymousCart();
        var skuId1 = Guid.NewGuid();
        var skuId2 = Guid.NewGuid();
        cart.AddItem(skuId1, 1, SellerId);
        cart.AddItem(skuId2, 1, SellerId);
        var sourceId = cart.Items.First(i => i.SkuId == skuId1).SourceCartItemId;

        cart.ClearItemsBySourceIds(new[] { sourceId });

        cart.Items.Should().HaveCount(1);
        cart.Items.First().SkuId.Should().Be(skuId2);
    }

    [Fact]
    public void Quantity_Default_ShouldBeOne()
    {
        var cart = CreateAnonymousCart();
        var skuId = Guid.NewGuid();

        cart.AddItem(skuId, 1, SellerId);

        cart.Items.First().Quantity.Should().Be(1);
    }

    [Fact]
    public void IsSelected_Default_ShouldBeTrue()
    {
        var cart = CreateAnonymousCart();
        var skuId = Guid.NewGuid();

        cart.AddItem(skuId, 1, SellerId);

        cart.Items.First().IsSelected.Should().BeTrue();
    }

    private static CartAggregate CreateAnonymousCart()
    {
        return CartAggregate.CreateAnonymous(Guid.NewGuid());
    }
}