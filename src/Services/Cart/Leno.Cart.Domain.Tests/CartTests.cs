using Leno.Cart.Domain.Aggregates;
using Leno.Cart.Domain.Exceptions;
using CartAggregate = Leno.Cart.Domain.Aggregates.Cart;

namespace Leno.Cart.Domain.Tests;

public class CartTests
{
    private static readonly Guid UserId = Guid.NewGuid();

    [Fact]
    public void Create_ValidUserId_ShouldCreateEmptyCart()
    {
        var cart = CartAggregate.Create(Guid.NewGuid(), UserId);

        cart.UserId.Should().Be(UserId);
        cart.Items.Should().BeEmpty();
    }

    [Fact]
    public void Create_EmptyUserId_ShouldThrowException()
    {
        var act = () => CartAggregate.Create(Guid.NewGuid(), Guid.Empty);

        act.Should().Throw<ArgumentException>().WithMessage("*UserId*");
    }

    [Fact]
    public void AddItem_NewSku_ShouldAddToCart()
    {
        var cart = CreateCart();
        var skuId = Guid.NewGuid();
        var sellerId = Guid.NewGuid();

        cart.AddItem(skuId, 3, sellerId);

        cart.Items.Should().HaveCount(1);
        cart.Items.First().SkuId.Should().Be(skuId);
        cart.Items.First().Quantity.Should().Be(3);
    }

    [Fact]
    public void AddItem_ExistingSku_ShouldMergeQuantity()
    {
        var cart = CreateCart();
        var skuId = Guid.NewGuid();
        var sellerId = Guid.NewGuid();
        cart.AddItem(skuId, 3, sellerId);

        cart.AddItem(skuId, 2, sellerId);

        cart.Items.Should().HaveCount(1);
        cart.Items.First().Quantity.Should().Be(5);
    }

    [Fact]
    public void AddItem_MergeExceedsLimit_ShouldThrowException()
    {
        var cart = CreateCart();
        var skuId = Guid.NewGuid();
        var sellerId = Guid.NewGuid();
        cart.AddItem(skuId, 50, sellerId);

        var act = () => cart.AddItem(skuId, 50, sellerId);

        act.Should().Throw<CartDomainException>().WithMessage("*上限*");
    }

    [Fact]
    public void AddItem_EmptySkuId_ShouldThrowException()
    {
        var cart = CreateCart();

        var act = () => cart.AddItem(Guid.Empty, 1, Guid.NewGuid());

        act.Should().Throw<ArgumentException>().WithMessage("*SkuId*");
    }

    [Fact]
    public void UpdateItemQuantity_ValidQuantity_ShouldUpdate()
    {
        var cart = CreateCart();
        var skuId = Guid.NewGuid();
        cart.AddItem(skuId, 3, Guid.NewGuid());

        cart.UpdateItemQuantity(skuId, 5);

        cart.Items.First().Quantity.Should().Be(5);
    }

    [Fact]
    public void UpdateItemQuantity_NotInCart_ShouldThrowException()
    {
        var cart = CreateCart();

        var act = () => cart.UpdateItemQuantity(Guid.NewGuid(), 5);

        act.Should().Throw<CartDomainException>().WithMessage("*不存在*");
    }

    [Fact]
    public void RemoveItem_ExistingSku_ShouldRemove()
    {
        var cart = CreateCart();
        var skuId = Guid.NewGuid();
        cart.AddItem(skuId, 3, Guid.NewGuid());

        cart.RemoveItem(skuId);

        cart.Items.Should().BeEmpty();
    }

    [Fact]
    public void RemoveItem_NotInCart_ShouldThrowException()
    {
        var cart = CreateCart();

        var act = () => cart.RemoveItem(Guid.NewGuid());

        act.Should().Throw<CartDomainException>().WithMessage("*不存在*");
    }

    [Fact]
    public void SelectItems_ShouldSetSelected()
    {
        var cart = CreateCart();
        var skuId1 = Guid.NewGuid();
        var skuId2 = Guid.NewGuid();
        cart.AddItem(skuId1, 1, Guid.NewGuid());
        cart.AddItem(skuId2, 1, Guid.NewGuid());
        cart.DeselectItems(new[] { skuId1, skuId2 });

        cart.SelectItems(new[] { skuId1 });

        cart.Items.First(i => i.SkuId == skuId1).IsSelected.Should().BeTrue();
        cart.Items.First(i => i.SkuId == skuId2).IsSelected.Should().BeFalse();
    }

    [Fact]
    public void ClearSelectedItems_ShouldRemoveSelected()
    {
        var cart = CreateCart();
        var skuId1 = Guid.NewGuid();
        var skuId2 = Guid.NewGuid();
        cart.AddItem(skuId1, 1, Guid.NewGuid());
        cart.AddItem(skuId2, 1, Guid.NewGuid());
        cart.DeselectItems(new[] { skuId2 });

        var sourceIds = cart.ClearSelectedItems();

        sourceIds.Should().HaveCount(1);
        cart.Items.Should().HaveCount(1);
        cart.Items.First().SkuId.Should().Be(skuId2);
    }

    [Fact]
    public void ClearItemsBySourceIds_ShouldRemoveMatching()
    {
        var cart = CreateCart();
        var skuId1 = Guid.NewGuid();
        var skuId2 = Guid.NewGuid();
        cart.AddItem(skuId1, 1, Guid.NewGuid());
        cart.AddItem(skuId2, 1, Guid.NewGuid());
        var sourceId = cart.Items.First(i => i.SkuId == skuId1).SourceCartItemId;

        cart.ClearItemsBySourceIds(new[] { sourceId });

        cart.Items.Should().HaveCount(1);
        cart.Items.First().SkuId.Should().Be(skuId2);
    }

    #region Merge

    [Fact]
    public void MergeFrom_NewSku_ShouldAddToCart()
    {
        var cart = CreateCart();
        var anonymousCart = CartAggregate.CreateAnonymous(Guid.NewGuid());
        var skuId = Guid.NewGuid();
        var sellerId = Guid.NewGuid();
        anonymousCart.AddItem(skuId, 3, sellerId);

        var mergedCount = cart.MergeFrom(anonymousCart);

        mergedCount.Should().Be(1);
        cart.Items.Should().HaveCount(1);
        cart.Items.First().SkuId.Should().Be(skuId);
        cart.Items.First().Quantity.Should().Be(3);
    }

    [Fact]
    public void MergeFrom_ExistingSku_ShouldMergeQuantity()
    {
        var cart = CreateCart();
        var skuId = Guid.NewGuid();
        var sellerId = Guid.NewGuid();
        cart.AddItem(skuId, 3, sellerId);

        var anonymousCart = CartAggregate.CreateAnonymous(Guid.NewGuid());
        anonymousCart.AddItem(skuId, 2, sellerId);

        var mergedCount = cart.MergeFrom(anonymousCart);

        mergedCount.Should().Be(1);
        cart.Items.Should().HaveCount(1);
        cart.Items.First().Quantity.Should().Be(5);
    }

    [Fact]
    public void MergeFrom_SelectedItem_ShouldRemainSelected()
    {
        var cart = CreateCart();
        var skuId = Guid.NewGuid();
        var sellerId = Guid.NewGuid();
        cart.AddItem(skuId, 1, sellerId);
        cart.DeselectItems(new[] { skuId });

        var anonymousCart = CartAggregate.CreateAnonymous(Guid.NewGuid());
        anonymousCart.AddItem(skuId, 1, sellerId);
        // 匿名购物车中该项是选中的（默认选中）

        cart.MergeFrom(anonymousCart);

        cart.Items.First().IsSelected.Should().BeTrue(); // 任一来源选中则选中
    }

    [Fact]
    public void MergeFrom_BothDeselected_ShouldStayDeselected()
    {
        var cart = CreateCart();
        var skuId = Guid.NewGuid();
        var sellerId = Guid.NewGuid();
        cart.AddItem(skuId, 1, sellerId);
        cart.DeselectItems(new[] { skuId });

        var anonymousCart = CartAggregate.CreateAnonymous(Guid.NewGuid());
        anonymousCart.AddItem(skuId, 1, sellerId);
        anonymousCart.DeselectItems(new[] { skuId });

        cart.MergeFrom(anonymousCart);

        cart.Items.First().IsSelected.Should().BeFalse();
    }

    [Fact]
    public void MergeFrom_VarietyExceedsLimit_ShouldThrowException()
    {
        var cart = CreateCart();
        // 填充 50 个不同 SKU
        for (int i = 0; i < 50; i++)
        {
            cart.AddItem(Guid.NewGuid(), 1, Guid.NewGuid());
        }

        var anonymousCart = CartAggregate.CreateAnonymous(Guid.NewGuid());
        anonymousCart.AddItem(Guid.NewGuid(), 1, Guid.NewGuid());

        var act = () => cart.MergeFrom(anonymousCart);

        act.Should().Throw<CartDomainException>().WithMessage("*品类*");
    }

    [Fact]
    public void MergeFrom_EmptyAnonymousCart_ShouldReturnZero()
    {
        var cart = CreateCart();
        var anonymousCart = CartAggregate.CreateAnonymous(Guid.NewGuid());

        var mergedCount = cart.MergeFrom(anonymousCart);

        mergedCount.Should().Be(0);
    }

    [Fact]
    public void MergeFrom_NullAnonymousCart_ShouldThrowException()
    {
        var cart = CreateCart();

        var act = () => cart.MergeFrom(null!);

        act.Should().Throw<ArgumentNullException>();
    }

    #endregion

    #region MarkInvalid / MarkValid

    [Fact]
    public void MarkInvalid_ExistingSku_ShouldMarkInvalidAndDeselect()
    {
        var cart = CreateCart();
        var skuId = Guid.NewGuid();
        cart.AddItem(skuId, 3, Guid.NewGuid());

        cart.MarkInvalid(skuId, "商品已下架");

        var item = cart.Items.First();
        item.IsValid.Should().BeFalse();
        item.InvalidReason.Should().Be("商品已下架");
        item.IsSelected.Should().BeFalse();
    }

    [Fact]
    public void MarkInvalid_NonExistentSku_ShouldNotThrow()
    {
        var cart = CreateCart();

        var act = () => cart.MarkInvalid(Guid.NewGuid(), "test");

        act.Should().NotThrow();
    }

    [Fact]
    public void MarkInvalid_AlreadyInvalid_ShouldBeIdempotent()
    {
        var cart = CreateCart();
        var skuId = Guid.NewGuid();
        cart.AddItem(skuId, 3, Guid.NewGuid());
        cart.MarkInvalid(skuId, "first reason");

        cart.MarkInvalid(skuId, "second reason");

        var item = cart.Items.First();
        item.IsValid.Should().BeFalse();
        item.InvalidReason.Should().Be("second reason"); // 覆盖原因
    }

    [Fact]
    public void MarkValid_ExistingInvalidSku_ShouldRestoreValidity()
    {
        var cart = CreateCart();
        var skuId = Guid.NewGuid();
        cart.AddItem(skuId, 3, Guid.NewGuid());
        cart.MarkInvalid(skuId, "商品已下架");

        cart.MarkValid(skuId);

        var item = cart.Items.First();
        item.IsValid.Should().BeTrue();
        item.InvalidReason.Should().BeNull();
    }

    [Fact]
    public void MarkValid_NonExistentSku_ShouldNotThrow()
    {
        var cart = CreateCart();

        var act = () => cart.MarkValid(Guid.NewGuid());

        act.Should().NotThrow();
    }

    [Fact]
    public void MarkValid_AlreadyValid_ShouldBeIdempotent()
    {
        var cart = CreateCart();
        var skuId = Guid.NewGuid();
        cart.AddItem(skuId, 3, Guid.NewGuid());

        cart.MarkValid(skuId);

        var item = cart.Items.First();
        item.IsValid.Should().BeTrue();
    }

    #endregion

    #region RefreshDisplaySnapshot

    [Fact]
    public void RefreshDisplaySnapshot_ExistingSku_ShouldUpdateSnapshot()
    {
        var cart = CreateCart();
        var skuId = Guid.NewGuid();
        cart.AddItem(skuId, 3, Guid.NewGuid());

        cart.RefreshDisplaySnapshot(skuId, "New Title", "https://new.img/1.jpg");

        var item = cart.Items.First();
        item.DisplayTitle.Should().Be("New Title");
        item.DisplayImageUrl.Should().Be("https://new.img/1.jpg");
    }

    [Fact]
    public void RefreshDisplaySnapshot_NonExistentSku_ShouldNotThrow()
    {
        var cart = CreateCart();

        var act = () => cart.RefreshDisplaySnapshot(Guid.NewGuid(), "title", "url");

        act.Should().NotThrow();
    }

    [Fact]
    public void RefreshDisplaySnapshot_EmptyValues_ShouldAccept()
    {
        var cart = CreateCart();
        var skuId = Guid.NewGuid();
        cart.AddItem(skuId, 3, Guid.NewGuid());

        cart.RefreshDisplaySnapshot(skuId, string.Empty, string.Empty);

        var item = cart.Items.First();
        item.DisplayTitle.Should().Be(string.Empty);
        item.DisplayImageUrl.Should().Be(string.Empty);
    }

    #endregion

    private static CartAggregate CreateCart()
    {
        return CartAggregate.Create(Guid.NewGuid(), UserId);
    }
}

