using Leno.Cart.Application.DTOs;
using Leno.Cart.Application.Services;
using Leno.Cart.Domain.Aggregates;
using Leno.Cart.Domain.Repositories;
using Leno.Cart.Domain.Services;
using Moq;
using CartAggregate = Leno.Cart.Domain.Aggregates.Cart;

namespace Leno.Cart.Application.Tests;

/// <summary>
/// AnonymousCartAppService TTL 刷新策略测试（P2-8）。
/// 验证读操作 GetCartAsync 不刷新 TTL（防止攻击者定时 GET 让匿名购物车永久驻留），
/// 写操作 AddItemAsync/UpdateQuantityAsync/RemoveItemAsync/SelectItemsAsync/PreviewCheckoutAsync 仍刷新 TTL。
/// </summary>
public class AnonymousCartAppServiceTtlTests
{
    private readonly Mock<IAnonymousCartRepository> _repoMock = new();
    private readonly Mock<ICartPriceService> _priceMock = new();
    private readonly AnonymousCartAppService _sut;

    private static readonly Guid SkuId = Guid.NewGuid();
    private static readonly Guid SellerId = Guid.NewGuid();
    private const string SessionId = "session-ttl-test";

    public AnonymousCartAppServiceTtlTests()
    {
        _sut = new AnonymousCartAppService(_repoMock.Object, _priceMock.Object);
    }

    [Fact]
    public async Task GetCartAsync_ShouldNotRefreshTtl_AvoidingPermanentResidence()
    {
        var cart = CreateAnonymousCartWithItem();
        _repoMock.Setup(r => r.GetAsync(SessionId, It.IsAny<CancellationToken>())).ReturnsAsync(cart);
        SetupPriceService();

        await _sut.GetCartAsync(SessionId);

        _repoMock.Verify(r => r.RefreshTtlAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task AddItemAsync_ShouldRefreshTtl_EncouragingActiveOperations()
    {
        var cart = CreateAnonymousCartWithItem();
        _repoMock.Setup(r => r.GetAsync(SessionId, It.IsAny<CancellationToken>())).ReturnsAsync(cart);
        SetupPriceService();

        await _sut.AddItemAsync(SessionId, new AddCartItemDto { SkuId = Guid.NewGuid(), Quantity = 1, SellerId = SellerId });

        _repoMock.Verify(r => r.RefreshTtlAsync(SessionId, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task UpdateQuantityAsync_ShouldRefreshTtl()
    {
        var cart = CreateAnonymousCartWithItem();
        _repoMock.Setup(r => r.GetAsync(SessionId, It.IsAny<CancellationToken>())).ReturnsAsync(cart);
        SetupPriceService();

        await _sut.UpdateQuantityAsync(SessionId, SkuId, new UpdateCartItemQuantityDto { Quantity = 5 });

        _repoMock.Verify(r => r.RefreshTtlAsync(SessionId, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task RemoveItemAsync_ShouldRefreshTtl()
    {
        var cart = CreateAnonymousCartWithItem();
        _repoMock.Setup(r => r.GetAsync(SessionId, It.IsAny<CancellationToken>())).ReturnsAsync(cart);
        SetupPriceService();

        await _sut.RemoveItemAsync(SessionId, SkuId);

        _repoMock.Verify(r => r.RefreshTtlAsync(SessionId, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task SelectItemsAsync_ShouldRefreshTtl()
    {
        var cart = CreateAnonymousCartWithItem();
        _repoMock.Setup(r => r.GetAsync(SessionId, It.IsAny<CancellationToken>())).ReturnsAsync(cart);
        SetupPriceService();

        await _sut.SelectItemsAsync(SessionId, new SelectCartItemsDto { SkuIds = new[] { SkuId }, Selected = false });

        _repoMock.Verify(r => r.RefreshTtlAsync(SessionId, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task PreviewCheckoutAsync_ShouldRefreshTtl()
    {
        var cart = CreateAnonymousCartWithItem();
        _repoMock.Setup(r => r.GetAsync(SessionId, It.IsAny<CancellationToken>())).ReturnsAsync(cart);
        SetupPriceService();

        await _sut.PreviewCheckoutAsync(SessionId);

        _repoMock.Verify(r => r.RefreshTtlAsync(SessionId, It.IsAny<CancellationToken>()), Times.Once);
    }

    private void SetupPriceService()
    {
        _priceMock.Setup(p => p.GetSkuPricesAsync(It.IsAny<IEnumerable<Guid>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { new SkuPriceSnapshot { SkuId = SkuId, Price = 10m, Currency = "CNY", Available = true, Title = "T", MainImageUrl = "", SellerId = SellerId } });
    }

    private static CartAggregate CreateAnonymousCartWithItem()
    {
        var cart = CartAggregate.CreateAnonymous(Guid.NewGuid());
        cart.AddItem(SkuId, 2, SellerId);
        return cart;
    }
}
