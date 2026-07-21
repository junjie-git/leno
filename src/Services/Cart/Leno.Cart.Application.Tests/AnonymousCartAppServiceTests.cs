using Leno.Cart.Application.DTOs;
using Leno.Cart.Application.Services;
using Leno.Cart.Domain.Aggregates;
using Leno.Cart.Domain.Exceptions;
using Leno.Cart.Domain.Repositories;
using Leno.Cart.Domain.Services;
using Moq;
using CartAggregate = Leno.Cart.Domain.Aggregates.Cart;

namespace Leno.Cart.Application.Tests;

/// <summary>
/// AnonymousCartAppService 0 元结算漏洞测试。
/// 验证匿名购物车在缺价场景下：
/// 1. GetCartAsync 标记 PriceUnavailable=true，选中项不计入可结算金额
/// 2. PreviewCheckoutAsync 缺价硬拦截，抛 CartDomainException 阻止 0 元结算
/// </summary>
public class AnonymousCartAppServiceTests
{
    private readonly Mock<IAnonymousCartRepository> _repoMock = new();
    private readonly Mock<ICartPriceService> _priceMock = new();
    private readonly AnonymousCartAppService _sut;

    private static readonly Guid SkuId = Guid.NewGuid();
    private static readonly Guid SellerId = Guid.NewGuid();
    private const string SessionId = "session-1";

    public AnonymousCartAppServiceTests()
    {
        _sut = new AnonymousCartAppService(_repoMock.Object, _priceMock.Object);
    }

    private CartAggregate CreateAnonymousCartWithItem()
    {
        var cart = CartAggregate.CreateAnonymous(Guid.NewGuid());
        cart.AddItem(SkuId, 2, SellerId);
        return cart;
    }

    [Fact]
    public async Task GetCartAsync_PriceMapMissesSku_ShouldMarkPriceUnavailableTrue()
    {
        var cart = CreateAnonymousCartWithItem();
        _repoMock.Setup(r => r.GetAsync(SessionId, It.IsAny<CancellationToken>())).ReturnsAsync(cart);
        _repoMock.Setup(r => r.RefreshTtlAsync(SessionId, It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        _priceMock.Setup(p => p.GetSkuPricesAsync(It.IsAny<IEnumerable<Guid>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<SkuPriceSnapshot>());

        var result = await _sut.GetCartAsync(SessionId);

        result.Items[0].PriceUnavailable.Should().BeTrue();
        result.Items[0].Available.Should().BeFalse();
        result.Items[0].UnitPrice.Should().Be(0m);
        result.Items[0].Title.Should().Be("[价格加载失败]");
        // 选中项缺价不应计入可结算金额
        result.SelectedTotalAmount.Should().Be(0m);
    }

    [Fact]
    public async Task PreviewCheckoutAsync_PriceMapMissesSku_ShouldThrowCartDomainExceptionBlockingZeroCheckout()
    {
        var cart = CreateAnonymousCartWithItem();
        _repoMock.Setup(r => r.GetAsync(SessionId, It.IsAny<CancellationToken>())).ReturnsAsync(cart);
        _repoMock.Setup(r => r.RefreshTtlAsync(SessionId, It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        _priceMock.Setup(p => p.GetSkuPricesAsync(It.IsAny<IEnumerable<Guid>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<SkuPriceSnapshot>());

        var act = () => _sut.PreviewCheckoutAsync(SessionId);

        await act.Should().ThrowAsync<CartDomainException>()
            .WithMessage("*部分商品价格加载失败，暂不可结算*");
    }

    [Fact]
    public async Task PreviewCheckoutAsync_AllPricesAvailable_ShouldReturnNormalPreview()
    {
        var cart = CreateAnonymousCartWithItem();
        _repoMock.Setup(r => r.GetAsync(SessionId, It.IsAny<CancellationToken>())).ReturnsAsync(cart);
        _repoMock.Setup(r => r.RefreshTtlAsync(SessionId, It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        _priceMock.Setup(p => p.GetSkuPricesAsync(It.IsAny<IEnumerable<Guid>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[]
            {
                new SkuPriceSnapshot
                {
                    SkuId = SkuId,
                    Price = 19.9m,
                    Currency = "CNY",
                    Available = true,
                    Title = "在售商品",
                    MainImageUrl = "https://img.example.com/a.jpg",
                    SellerId = SellerId
                }
            });

        var result = await _sut.PreviewCheckoutAsync(SessionId);

        result.Groups.Should().HaveCount(1);
        result.Groups[0].Items[0].PriceUnavailable.Should().BeFalse();
        result.Groups[0].SubtotalAmount.Should().Be(19.9m * 2);
        result.TotalAmount.Should().Be(19.9m * 2);
    }

    [Fact]
    public async Task PreviewCheckoutAsync_PartialPriceMissing_ShouldThrowBlockingCheckout()
    {
        var cart = CartAggregate.CreateAnonymous(Guid.NewGuid());
        cart.AddItem(SkuId, 2, SellerId);
        var sku2 = Guid.NewGuid();
        cart.AddItem(sku2, 1, SellerId);
        _repoMock.Setup(r => r.GetAsync(SessionId, It.IsAny<CancellationToken>())).ReturnsAsync(cart);
        _repoMock.Setup(r => r.RefreshTtlAsync(SessionId, It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        _priceMock.Setup(p => p.GetSkuPricesAsync(It.IsAny<IEnumerable<Guid>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[]
            {
                new SkuPriceSnapshot
                {
                    SkuId = SkuId,
                    Price = 19.9m,
                    Currency = "CNY",
                    Available = true,
                    Title = "在售商品",
                    MainImageUrl = string.Empty,
                    SellerId = SellerId
                }
                // sku2 未返回
            });

        var act = () => _sut.PreviewCheckoutAsync(SessionId);

        await act.Should().ThrowAsync<CartDomainException>()
            .WithMessage("*部分商品价格加载失败，暂不可结算*");
    }
}
