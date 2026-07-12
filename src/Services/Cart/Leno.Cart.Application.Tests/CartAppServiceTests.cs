using Leno.Cart.Application.DTOs;
using Leno.Cart.Application.Services;
using Leno.Cart.Domain.Aggregates;
using Leno.Cart.Domain.Exceptions;
using Leno.Cart.Domain.Repositories;
using Leno.Cart.Domain.Services;
using Leno.SharedKernel.Abstractions;
using Moq;
using CartAggregate = Leno.Cart.Domain.Aggregates.Cart;

namespace Leno.Cart.Application.Tests;

public class CartAppServiceTests
{
    private readonly Mock<ICartRepository> _cartRepoMock = new();
    private readonly Mock<ICartPriceService> _priceServiceMock = new();
    private readonly Mock<IUnitOfWork> _uowMock = new();
    private readonly CartAppService _sut;

    private static readonly Guid UserId = Guid.NewGuid();
    private static readonly Guid SkuId = Guid.NewGuid();
    private static readonly Guid SellerId = Guid.NewGuid();

    public CartAppServiceTests()
    {
        _sut = new CartAppService(
            _cartRepoMock.Object,
            _priceServiceMock.Object,
            _uowMock.Object);
    }

    #region AddItemAsync

    [Fact]
    public async Task AddItemAsync_NewCart_ShouldCreateAndAddItem()
    {
        _cartRepoMock.Setup(r => r.GetByUserIdAsync(UserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((CartAggregate?)null);
        SetupPriceService();

        var result = await _sut.AddItemAsync(UserId, new AddCartItemDto { SkuId = SkuId, Quantity = 3, SellerId = SellerId });

        result.Should().NotBeNull();
        result.Items.Should().HaveCount(1);
        result.Items[0].SkuId.Should().Be(SkuId);
        result.Items[0].Quantity.Should().Be(3);
        _cartRepoMock.Verify(r => r.AddAsync(It.IsAny<CartAggregate>(), It.IsAny<CancellationToken>()), Times.Once);
        _uowMock.Verify(u => u.SaveEntitiesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task AddItemAsync_ExistingCart_ShouldAddToExisting()
    {
        var cart = CreateCart();
        _cartRepoMock.Setup(r => r.GetByUserIdAsync(UserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(cart);
        SetupPriceService();

        var result = await _sut.AddItemAsync(UserId, new AddCartItemDto { SkuId = SkuId, Quantity = 2, SellerId = SellerId });

        result.Items.Should().HaveCount(1);
        _cartRepoMock.Verify(r => r.AddAsync(It.IsAny<CartAggregate>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task AddItemAsync_EmptyUserId_ShouldThrowException()
    {
        var act = () => _sut.AddItemAsync(Guid.Empty, new AddCartItemDto { SkuId = SkuId, Quantity = 1, SellerId = SellerId });

        await act.Should().ThrowAsync<CartDomainException>().WithMessage("*UserId*");
    }

    [Fact]
    public async Task AddItemAsync_EmptySkuId_ShouldThrowException()
    {
        var cart = CreateCart();
        _cartRepoMock.Setup(r => r.GetByUserIdAsync(UserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(cart);

        var act = () => _sut.AddItemAsync(UserId, new AddCartItemDto { SkuId = Guid.Empty, Quantity = 1, SellerId = SellerId });

        await act.Should().ThrowAsync<ArgumentException>().WithMessage("*SkuId*");
    }

    #endregion

    #region UpdateQuantityAsync

    [Fact]
    public async Task UpdateQuantityAsync_ValidInput_ShouldUpdate()
    {
        var cart = CreateCart();
        cart.AddItem(SkuId, 3, SellerId);
        _cartRepoMock.Setup(r => r.GetByUserIdAsync(UserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(cart);
        SetupPriceService();

        var result = await _sut.UpdateQuantityAsync(UserId, SkuId, new UpdateCartItemQuantityDto { Quantity = 5 });

        result.Items[0].Quantity.Should().Be(5);
        _uowMock.Verify(u => u.SaveEntitiesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task UpdateQuantityAsync_CartNotFound_ShouldThrowException()
    {
        _cartRepoMock.Setup(r => r.GetByUserIdAsync(UserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((CartAggregate?)null);

        var act = () => _sut.UpdateQuantityAsync(UserId, SkuId, new UpdateCartItemQuantityDto { Quantity = 5 });

        await act.Should().ThrowAsync<CartDomainException>().WithMessage("*购物车不存在*");
    }

    #endregion

    #region RemoveItemAsync

    [Fact]
    public async Task RemoveItemAsync_ValidInput_ShouldRemove()
    {
        var cart = CreateCart();
        cart.AddItem(SkuId, 3, SellerId);
        _cartRepoMock.Setup(r => r.GetByUserIdAsync(UserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(cart);
        SetupPriceService();

        var result = await _sut.RemoveItemAsync(UserId, SkuId);

        result.Items.Should().BeEmpty();
        _uowMock.Verify(u => u.SaveEntitiesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task RemoveItemAsync_CartNotFound_ShouldThrowException()
    {
        _cartRepoMock.Setup(r => r.GetByUserIdAsync(UserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((CartAggregate?)null);

        var act = () => _sut.RemoveItemAsync(UserId, SkuId);

        await act.Should().ThrowAsync<CartDomainException>().WithMessage("*购物车不存在*");
    }

    #endregion

    #region SelectItemsAsync

    [Fact]
    public async Task SelectItemsAsync_Select_ShouldSetSelected()
    {
        var cart = CreateCart();
        cart.AddItem(SkuId, 1, SellerId);
        cart.DeselectItems(new[] { SkuId });
        _cartRepoMock.Setup(r => r.GetByUserIdAsync(UserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(cart);
        SetupPriceService();

        var result = await _sut.SelectItemsAsync(UserId, new SelectCartItemsDto { SkuIds = new[] { SkuId }, Selected = true });

        result.Items[0].IsSelected.Should().BeTrue();
    }

    [Fact]
    public async Task SelectItemsAsync_Deselect_ShouldSetDeselected()
    {
        var cart = CreateCart();
        cart.AddItem(SkuId, 1, SellerId);
        _cartRepoMock.Setup(r => r.GetByUserIdAsync(UserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(cart);
        SetupPriceService();

        var result = await _sut.SelectItemsAsync(UserId, new SelectCartItemsDto { SkuIds = new[] { SkuId }, Selected = false });

        result.Items[0].IsSelected.Should().BeFalse();
    }

    [Fact]
    public async Task SelectItemsAsync_CartNotFound_ShouldThrowException()
    {
        _cartRepoMock.Setup(r => r.GetByUserIdAsync(UserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((CartAggregate?)null);

        var act = () => _sut.SelectItemsAsync(UserId, new SelectCartItemsDto { SkuIds = new[] { SkuId }, Selected = true });

        await act.Should().ThrowAsync<CartDomainException>().WithMessage("*购物车不存在*");
    }

    #endregion

    #region GetCartAsync

    [Fact]
    public async Task GetCartAsync_ExistingCart_ShouldReturnCartDto()
    {
        var cart = CreateCart();
        cart.AddItem(SkuId, 3, SellerId);
        _cartRepoMock.Setup(r => r.GetByUserIdAsync(UserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(cart);
        SetupPriceService();

        var result = await _sut.GetCartAsync(UserId);

        result.Should().NotBeNull();
        result.UserId.Should().Be(UserId);
        result.Items.Should().HaveCount(1);
    }

    [Fact]
    public async Task GetCartAsync_NoCart_ShouldCreateAndReturnEmpty()
    {
        _cartRepoMock.Setup(r => r.GetByUserIdAsync(UserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((CartAggregate?)null);
        SetupPriceService();

        var result = await _sut.GetCartAsync(UserId);

        result.Should().NotBeNull();
        result.Items.Should().BeEmpty();
        _cartRepoMock.Verify(r => r.AddAsync(It.IsAny<CartAggregate>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GetCartAsync_EmptyUserId_ShouldThrowException()
    {
        var act = () => _sut.GetCartAsync(Guid.Empty);

        await act.Should().ThrowAsync<CartDomainException>().WithMessage("*UserId*");
    }

    #endregion

    #region PreviewCheckoutAsync

    [Fact]
    public async Task PreviewCheckoutAsync_WithSelectedItems_ShouldReturnPreview()
    {
        var cart = CreateCart();
        var skuId2 = Guid.NewGuid();
        cart.AddItem(SkuId, 2, SellerId);
        cart.AddItem(skuId2, 3, SellerId);
        cart.DeselectItems(new[] { skuId2 });
        _cartRepoMock.Setup(r => r.GetByUserIdAsync(UserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(cart);
        SetupPriceService(SkuId, 99.99m, "Test Product");

        var result = await _sut.PreviewCheckoutAsync(UserId);

        result.Should().NotBeNull();
        result.Groups.Should().HaveCount(1);
        result.Groups[0].Items.Should().HaveCount(1);
        result.Groups[0].SellerId.Should().Be(SellerId);
        result.TotalCount.Should().Be(2);
    }

    [Fact]
    public async Task PreviewCheckoutAsync_NoSelectedItems_ShouldReturnEmpty()
    {
        var cart = CreateCart();
        cart.AddItem(SkuId, 2, SellerId);
        cart.DeselectItems(new[] { SkuId });
        _cartRepoMock.Setup(r => r.GetByUserIdAsync(UserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(cart);

        var result = await _sut.PreviewCheckoutAsync(UserId);

        result.Should().NotBeNull();
        result.Groups.Should().BeEmpty();
    }

    [Fact]
    public async Task PreviewCheckoutAsync_CartNotFound_ShouldThrowException()
    {
        _cartRepoMock.Setup(r => r.GetByUserIdAsync(UserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((CartAggregate?)null);

        var act = () => _sut.PreviewCheckoutAsync(UserId);

        await act.Should().ThrowAsync<CartDomainException>().WithMessage("*购物车不存在*");
    }

    [Fact]
    public async Task PreviewCheckoutAsync_MultipleSellers_ShouldGroupBySeller()
    {
        var cart = CreateCart();
        var sellerId2 = Guid.NewGuid();
        cart.AddItem(SkuId, 2, SellerId);
        cart.AddItem(Guid.NewGuid(), 3, sellerId2);
        _cartRepoMock.Setup(r => r.GetByUserIdAsync(UserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(cart);
        SetupPriceService();

        var result = await _sut.PreviewCheckoutAsync(UserId);

        result.Groups.Should().HaveCount(2);
        result.Groups.Select(g => g.SellerId).Should().Contain(new[] { SellerId, sellerId2 });
    }

    #endregion

    private static CartAggregate CreateCart()
    {
        return CartAggregate.Create(Guid.NewGuid(), UserId);
    }

    private void SetupPriceService(Guid? skuId = null, decimal price = 49.99m, string title = "Product")
    {
        _priceServiceMock.Setup(p => p.GetSkuPricesAsync(It.IsAny<IEnumerable<Guid>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((IEnumerable<Guid> ids, CancellationToken _) =>
            {
                return ids.Select(id => new SkuPriceSnapshot
                {
                    SkuId = id,
                    Price = price,
                    Currency = "CNY",
                    Available = true,
                    Title = title,
                    MainImageUrl = "https://img.example.com/img.jpg",
                    SellerId = SellerId
                }).ToList();
            });
    }
}