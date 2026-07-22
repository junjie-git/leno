using Leno.Cart.Application.DTOs;
using Leno.Cart.Application.Services;
using Leno.Cart.Domain.Aggregates;
using Leno.Cart.Domain.Exceptions;
using Leno.Cart.Domain.Repositories;
using Leno.Cart.Domain.Services;
using Leno.SharedKernel.Abstractions;
using Microsoft.Extensions.Logging;
using Moq;
using CartAggregate = Leno.Cart.Domain.Aggregates.Cart;

namespace Leno.Cart.Application.Tests;

public class CartAppServiceTests
{
    private readonly Mock<ICartRepository> _cartRepoMock = new();
    private readonly Mock<ICartPriceService> _priceServiceMock = new();
    private readonly Mock<IUnitOfWork> _uowMock = new();
    private readonly Mock<IAnonymousCartRepository> _anonymousCartRepoMock = new();
    private readonly Mock<ICartMergeRecordRepository> _cartMergeRecordRepoMock = new();
    private readonly Mock<ILogger<CartAppService>> _loggerMock = new();
    private readonly CartAppService _sut;

    private static readonly Guid UserId = Guid.NewGuid();
    private static readonly Guid SkuId = Guid.NewGuid();
    private static readonly Guid SellerId = Guid.NewGuid();

    public CartAppServiceTests()
    {
        _sut = new CartAppService(
            _cartRepoMock.Object,
            _priceServiceMock.Object,
            _uowMock.Object,
            _anonymousCartRepoMock.Object,
            _cartMergeRecordRepoMock.Object,
            _loggerMock.Object);
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

    #region PriceFailure

    [Fact]
    public async Task GetCartAsync_WhenPriceServiceThrows_ShouldDegradeAndMarkPriceUnavailable()
    {
        // Arrange：购物车"查看"场景不应因价格服务故障整体崩溃
        var cart = CreateCart();
        cart.AddItem(SkuId, 2, SellerId);
        _cartRepoMock.Setup(r => r.GetByUserIdAsync(UserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(cart);
        _priceServiceMock.Setup(p => p.GetSkuPricesAsync(It.IsAny<IEnumerable<Guid>>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new CartDomainException("商品价格服务暂时不可用", "CART_PRICE_UNAVAILABLE"));

        // Act
        var result = await _sut.GetCartAsync(UserId);

        // Assert：DTO 降级展示，标记 PriceUnavailable=true，Available=false，Title 提示加载失败
        result.Should().NotBeNull();
        result.Items.Should().HaveCount(1);
        result.Items[0].PriceUnavailable.Should().BeTrue();
        result.Items[0].Available.Should().BeFalse();
        result.Items[0].UnitPrice.Should().Be(0m);
        result.Items[0].Title.Should().Be("[价格加载失败]");
        // 选中项总金额不把价格失败项以 0 元计入
        result.SelectedTotalAmount.Should().Be(0m);
        // SKU 与数量仍保留，便于前端提示并引导用户刷新
        result.Items[0].SkuId.Should().Be(SkuId);
        result.Items[0].Quantity.Should().Be(2);
    }

    [Fact]
    public async Task GetCartAsync_WhenPriceMapMissesSku_ShouldMarkOnlyMissedItemUnavailable()
    {
        // Arrange：价格服务返回部分 SKU，未命中项应标记 PriceUnavailable=true
        var cart = CreateCart();
        var skuId2 = Guid.NewGuid();
        cart.AddItem(SkuId, 2, SellerId);
        cart.AddItem(skuId2, 1, SellerId);
        _cartRepoMock.Setup(r => r.GetByUserIdAsync(UserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(cart);
        // 价格服务仅返回 SkuId，skuId2 未命中
        _priceServiceMock.Setup(p => p.GetSkuPricesAsync(It.IsAny<IEnumerable<Guid>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((IEnumerable<Guid> ids, CancellationToken _) =>
                ids.Where(id => id == SkuId)
                    .Select(id => new SkuPriceSnapshot
                    {
                        SkuId = id,
                        Price = 19.9m,
                        Currency = "CNY",
                        Available = true,
                        Title = "在售商品",
                        MainImageUrl = "https://img.example.com/a.jpg",
                        SellerId = SellerId
                    }).ToList());

        // Act
        var result = await _sut.GetCartAsync(UserId);

        // Assert
        var hitItem = result.Items.Single(i => i.SkuId == SkuId);
        hitItem.PriceUnavailable.Should().BeFalse();
        hitItem.Available.Should().BeTrue();
        hitItem.UnitPrice.Should().Be(19.9m);

        var missedItem = result.Items.Single(i => i.SkuId == skuId2);
        missedItem.PriceUnavailable.Should().BeTrue();
        missedItem.Available.Should().BeFalse();
        missedItem.UnitPrice.Should().Be(0m);
        missedItem.Title.Should().Be("[价格加载失败]");
    }

    [Fact]
    public async Task GetCartAsync_WhenPriceUnavailable_ShouldNotCountZeroSubtotalInSelectedTotal()
    {
        // Arrange：选中项价格不可用时，SelectedTotalAmount 不应包含 0 元可结算误导
        var cart = CreateCart();
        cart.AddItem(SkuId, 3, SellerId);
        _cartRepoMock.Setup(r => r.GetByUserIdAsync(UserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(cart);
        _priceServiceMock.Setup(p => p.GetSkuPricesAsync(It.IsAny<IEnumerable<Guid>>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new CartDomainException("商品价格服务暂时不可用", "CART_PRICE_UNAVAILABLE"));

        // Act
        var result = await _sut.GetCartAsync(UserId);

        // Assert：选中项默认 IsSelected=true，但 PriceUnavailable=true，不应计入 SelectedTotalAmount
        result.Items[0].IsSelected.Should().BeTrue();
        result.Items[0].PriceUnavailable.Should().BeTrue();
        result.SelectedTotalAmount.Should().Be(0m);
    }

    [Fact]
    public async Task PreviewCheckoutAsync_WhenPriceServiceThrows_ShouldPropagateAndBlockCheckout()
    {
        // Arrange：结算预览场景价格不可用应阻止结算，不返回 0 元结算单
        var cart = CreateCart();
        cart.AddItem(SkuId, 2, SellerId);
        _cartRepoMock.Setup(r => r.GetByUserIdAsync(UserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(cart);
        _priceServiceMock.Setup(p => p.GetSkuPricesAsync(It.IsAny<IEnumerable<Guid>>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new CartDomainException("商品价格服务暂时不可用", "CART_PRICE_UNAVAILABLE"));

        // Act
        var act = () => _sut.PreviewCheckoutAsync(UserId);

        // Assert：异常由全局中间件转为明确错误响应，避免 0 元结算
        await act.Should().ThrowAsync<CartDomainException>()
            .WithMessage("*商品价格服务暂时不可用*");
    }

    [Fact]
    public async Task PreviewCheckoutAsync_WhenPriceMapMisses_ShouldBlockCheckout()
    {
        // Arrange：价格服务返回空（无异常），priceMap 未命中任一 SKU，仍应阻止结算
        var cart = CreateCart();
        cart.AddItem(SkuId, 2, SellerId);
        _cartRepoMock.Setup(r => r.GetByUserIdAsync(UserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(cart);
        _priceServiceMock.Setup(p => p.GetSkuPricesAsync(It.IsAny<IEnumerable<Guid>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<SkuPriceSnapshot>());

        // Act
        var act = () => _sut.PreviewCheckoutAsync(UserId);

        // Assert
        await act.Should().ThrowAsync<CartDomainException>()
            .WithMessage("*部分商品价格加载失败，暂不可结算*");
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